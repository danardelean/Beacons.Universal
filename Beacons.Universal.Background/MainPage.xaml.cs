using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.Linq;
using Windows.ApplicationModel.Background;
using Windows.Storage;
using System.Text;
using System.Runtime.Serialization.Json;
using Windows.UI.Popups;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace Beacons.Universal.Background
{

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // The background task registration for the background advertisement watcher
        private IBackgroundTaskRegistration taskAdvertisementWatcher;
        // The watcher trigger used to configure the background task registration
        private BluetoothLEAdvertisementWatcherTrigger trigger;
        // A name is given to the task in order for it to be identifiable across context.
        private string taskName = "iBeacon_BackgroundTask";
        // Entry point for the background task.
        private string taskEntryPoint = "Beacons.Tasks.AdvertisementWatcherTask";


        //Beacons around that we bind to
        ObservableCollection<iBeaconData> beacons;
        public MainPage()
        {
            this.InitializeComponent();
            
            beacons = new ObservableCollection<iBeaconData>();
            this.DataContext = beacons;

            //Unregister the old task
            var taskAdvertisementWatcher = BackgroundTaskRegistration.AllTasks.Values.Where(t => t.Name == taskName).FirstOrDefault();
            if (taskAdvertisementWatcher != null)
            {
                taskAdvertisementWatcher.Unregister(true);
                taskAdvertisementWatcher = null;
                Button_Click(null, null);
            }
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Attach handlers for suspension to stop the watcher when the App is suspended.
            App.Current.Suspending += App_Suspending;
            App.Current.Resuming += App_Resuming;

        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            // Remove local suspension handlers from the App since this page is no longer active.
            App.Current.Suspending -= App_Suspending;
            App.Current.Resuming -= App_Resuming;

            
            base.OnNavigatingFrom(e);
        }

        // <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private void App_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            if (taskAdvertisementWatcher != null)
                // Always unregister the handlers to release the resources to prevent leaks.
                taskAdvertisementWatcher.Completed -= OnBackgroundTaskCompleted;
        }

        /// <summary>
        /// Invoked when application execution is being resumed.
        /// </summary>
        /// <param name="sender">The source of the resume request.</param>
        /// <param name="e"></param>
        private void App_Resuming(object sender, object e)
        {
            if (taskAdvertisementWatcher != null)
                taskAdvertisementWatcher.Completed += OnBackgroundTaskCompleted;
        }

        private async void OnBackgroundTaskCompleted(BackgroundTaskRegistration task, BackgroundTaskCompletedEventArgs eventArgs)
        {
            // We get the advertisement(s) processed by the background task
            if (ApplicationData.Current.LocalSettings.Values.Keys.Contains(taskName))
            {
                string backgroundMessage = (string)ApplicationData.Current.LocalSettings.Values[taskName];


                var bytes = Encoding.Unicode.GetBytes(backgroundMessage);
                var serializer = new DataContractJsonSerializer(typeof(List<iBeaconData>));

                var foundBeacons = (List<iBeaconData>)serializer.ReadObject(new MemoryStream(bytes));

                await this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    foreach (var beacon in foundBeacons)
                    {
                        var existing = beacons.Where(b => b.UUID == beacon.UUID && b.Major == beacon.Major && b.Minor == beacon.Minor).FirstOrDefault();
                        if (existing != null)
                        {
                            var idx = beacons.IndexOf(existing);
                            beacons.RemoveAt(idx);
                            beacons.Insert(idx, beacon);
                        }
                        else
                            beacons.Add(beacon);
                    }

                    txtTimeStamp.Text = DateTime.FromBinary((long)ApplicationData.Current.LocalSettings.Values[taskName + "TimeStamp"]).ToString();
                });
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (taskAdvertisementWatcher != null)
            {
                taskAdvertisementWatcher.Completed -= OnBackgroundTaskCompleted;
                taskAdvertisementWatcher.Unregister(true);
                taskAdvertisementWatcher = null;
                btnRegisterUnregister.Content = "Register Background Task";
            }
            else
            {
                //Register the new task
                // Applications registering for background trigger must request for permission.
                BackgroundExecutionManager.RequestAccessAsync().AsTask().ContinueWith(async (r) =>
                {
                    if (r.IsFaulted || r.IsCanceled)
                        return;
                    if ((r.Result == BackgroundAccessStatus.Denied) || (r.Result == BackgroundAccessStatus.Unspecified))
                    {
                        await new MessageDialog("Not able to run in background. Application must given permission to be added to lock screen.").ShowAsync();
                        Application.Current.Exit();
                    }

                    // Create and initialize a new trigger to configure it.
                    trigger = new BluetoothLEAdvertisementWatcherTrigger();
                    // Add the manufacturer data to the advertisement filter on the trigger:
                    trigger.AdvertisementFilter.Advertisement.SetiBeaconAdvertisement(new iBeaconData());
                    // By default, the sampling interval is set to be disabled, or the maximum sampling interval supported.
                    // The sampling interval set to MaxSamplingInterval indicates that the event will only trigger once after it comes into range.
                    // Here, set the sampling period to 1 second, which is the minimum supported for background.
                    trigger.SignalStrengthFilter.SamplingInterval = TimeSpan.FromMilliseconds(1000);

                    // At this point we assume we haven't found any existing tasks matching the one we want to register
                    // First, configure the task entry point, trigger and name
                    var builder = new BackgroundTaskBuilder();
                    builder.TaskEntryPoint = taskEntryPoint;
                    builder.SetTrigger(trigger);
                    builder.Name = taskName;

                    // Now perform the registration. The registration can throw an exception if the current 
                    // hardware does not support background advertisement offloading
                    try
                    {
                        taskAdvertisementWatcher = builder.Register();
                        Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                         {
                             // For this scenario, attach an event handler to display the result processed from the background task
                             taskAdvertisementWatcher.Completed += OnBackgroundTaskCompleted;
                             btnRegisterUnregister.Content = "Unregister Background Task";
                         });
                        
                    }
                    catch (Exception ex)
                    {
                        taskAdvertisementWatcher = null;
                        switch ((uint)ex.HResult)
                        {
                            case (0x80070032): // ERROR_NOT_SUPPORTED
                                Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                                {
                                    await new MessageDialog("The hardware does not support background advertisement offload.").ShowAsync();
                                    Application.Current.Exit();
                                });
                                break;
                            default:
                                throw ex;
                        }
                    }

                });

                
            }
        }
    }
}

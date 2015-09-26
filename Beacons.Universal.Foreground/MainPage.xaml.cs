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

namespace Beacons.Universal.Foreground
{

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        // The Bluetooth LE advertisement watcher class is used to control and customize Bluetooth LE scanning.
        private BluetoothLEAdvertisementWatcher watcher;


     
        //Beacons around that we bind to
        ObservableCollection<iBeaconData> beacons;
        public MainPage()
        {
            this.InitializeComponent();

            // Create and initialize a new watcher instance.
            watcher = new BluetoothLEAdvertisementWatcher();

            // Monitor all iBeacons advertisment
            watcher.AdvertisementFilter.Advertisement.iBeaconSetAdvertisement(new iBeaconData());

            //// Monitor all iBeacons with UUID
            //watcher.AdvertisementFilter.Advertisement.SetiBeaconAdvertismentManufacturerData(
            //    new iBeaconData()
            //    {
            //        UUID = Guid.Parse("{307f40b9-f8f5-6e46-aff9-25556b57fe6d}")
            //    });

            //// Monitor all iBeacons with UUID and Major 
            //watcher.AdvertisementFilter.Advertisement.SetiBeaconAdvertismentManufacturerData(
            //    new iBeaconData()
            //    {
            //        UUID = Guid.Parse("{307f40b9-f8f5-6e46-aff9-25556b57fe6d}"),
            //        Major = 18012
            //    });

            //// Monitor all iBeacons with UUID and Major 
            //watcher.AdvertisementFilter.Advertisement.SetiBeaconAdvertismentManufacturerData(
            //    new iBeaconData()
            //    {
            //        UUID = Guid.Parse("{307f40b9-f8f5-6e46-aff9-25556b57fe6d}"),
            //        Major = 18012,
            //        Minor = 1040
            //    });

            beacons = new ObservableCollection<iBeaconData>();

            this.DataContext = beacons;

           
         
        }
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            // Attach handlers for suspension to stop the watcher when the App is suspended.
            App.Current.Suspending += App_Suspending;
            App.Current.Resuming += App_Resuming;


            // Attach a handler to process the received advertisement. 
            // The watcher cannot be started without a Received handler attached
            watcher.Received += OnAdvertisementReceived;

            // Attach a handler to process watcher stopping due to various conditions,
            // such as the Bluetooth radio turning off or the Stop method was called
            watcher.Stopped += OnAdvertisementWatcherStopped;

            watcher.Start();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            // Remove local suspension handlers from the App since this page is no longer active.
            App.Current.Suspending -= App_Suspending;
            App.Current.Resuming -= App_Resuming;

            // Make sure to stop the watcher when leaving the context. Even if the watcher is not stopped,
            // scanning will be stopped automatically if the watcher is destroyed.
            watcher.Stop();
            // Always unregister the handlers to release the resources to prevent leaks.
            watcher.Received -= OnAdvertisementReceived;
            watcher.Stopped -= OnAdvertisementWatcherStopped;
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
            // Make sure to stop the watcher on suspend.
            watcher.Stop();
            // Always unregister the handlers to release the resources to prevent leaks.
            watcher.Received -= OnAdvertisementReceived;
            watcher.Stopped -= OnAdvertisementWatcherStopped;

        
        }

        /// <summary>
        /// Invoked when application execution is being resumed.
        /// </summary>
        /// <param name="sender">The source of the resume request.</param>
        /// <param name="e"></param>
        private void App_Resuming(object sender, object e)
        {
            watcher.Received += OnAdvertisementReceived;
            watcher.Stopped += OnAdvertisementWatcherStopped;
            watcher.Start();
        }

        /// <summary>
        /// Invoked as an event handler when an advertisement is received.
        /// </summary>
        /// <param name="watcher">Instance of watcher that triggered the event.</param>
        /// <param name="eventArgs">Event data containing information about the advertisement event.</param>
        private async void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementReceivedEventArgs eventArgs)
        {
            // We can obtain various information about the advertisement we just received by accessing 
            // the properties of the EventArgs class


            // The timestamp of the event
            DateTimeOffset timestamp = eventArgs.Timestamp;

            // The type of advertisement
            BluetoothLEAdvertisementType advertisementType = eventArgs.AdvertisementType;

            // The received signal strength indicator (RSSI)
            Int16 rssi = eventArgs.RawSignalStrengthInDBm;

            // The local name of the advertising device contained within the payload, if any
            string localName = eventArgs.Advertisement.LocalName;

            // Get iBeacon specific data
            var beaconData = eventArgs.Advertisement.iBeaconParseAdvertisement(eventArgs.RawSignalStrengthInDBm);


            if (beaconData == null)
                return;
            Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                var existing = beacons.Where(b => b.UUID == beaconData.UUID && b.Major == beaconData.Major && b.Minor == beaconData.Minor).FirstOrDefault();
                if (existing != null)
                {
                    var idx = beacons.IndexOf(existing);
                    beacons.RemoveAt(idx);
                    beacons.Insert(idx, beaconData);
                }
                else
                    beacons.Add(beaconData);
            });
        }

        /// <summary>
        /// Invoked as an event handler when the watcher is stopped or aborted.
        /// </summary>
        /// <param name="watcher">Instance of watcher that triggered the event.</param>
        /// <param name="eventArgs">Event data containing information about why the watcher stopped or aborted.</param>
        private async void OnAdvertisementWatcherStopped(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementWatcherStoppedEventArgs eventArgs)
        {
            // Notify the user that the watcher was stopped
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.ApplicationModel.Background;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.Background;
using Windows.Storage;
using Beacons;
using System.Runtime.Serialization.Json;
using System.IO;
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;

namespace Beacons.Tasks
{
    public sealed class AdvertisementWatcherTask : IBackgroundTask
    {
        private IBackgroundTaskInstance backgroundTaskInstance;

        /// <summary>
        /// The entry point of a background task.
        /// </summary>
        /// <param name="taskInstance">The current background task instance.</param>
        public void Run(IBackgroundTaskInstance taskInstance)
        {
            backgroundTaskInstance = taskInstance;

            var details = taskInstance.TriggerDetails as BluetoothLEAdvertisementWatcherTriggerDetails;

            if (details != null)
            {
                // If the background watcher stopped unexpectedly, an error will be available here.
                var error = details.Error;

                // The Advertisements property is a list of all advertisement events received
                // since the last task triggered. The list of advertisements here might be valid even if
                // the Error status is not Success since advertisements are stored until this task is triggered
                IReadOnlyList<BluetoothLEAdvertisementReceivedEventArgs> advertisements = details.Advertisements;

                // The signal strength filter configuration of the trigger is returned such that further 
                // processing can be performed here using these values if necessary. They are read-only here.
                var rssiFilter = details.SignalStrengthFilter;

                // Advertisements can contain multiple events that were aggregated, each represented by 
                // a BluetoothLEAdvertisementReceivedEventArgs object.
                List<iBeaconData> beacons = new List<iBeaconData>();
                foreach (var adv in advertisements)
                {
                    var beacon = adv.Advertisement.ParseiBeaconAdvertisement(adv.RawSignalStrengthInDBm);
                    if (beacon != null)
                        beacons.Add(beacon);
                }

                var serializer = new DataContractJsonSerializer(typeof(List<iBeaconData>));
                string content = string.Empty;
                using (MemoryStream stream = new MemoryStream())
                {
                    serializer.WriteObject(stream, beacons);
                    stream.Position = 0;
                    content = new StreamReader(stream).ReadToEnd();
                }

                // Store the message in a local settings indexed by this task's name so that the foreground App
                // can display this message.
                ApplicationData.Current.LocalSettings.Values[taskInstance.Task.Name] = content;
                ApplicationData.Current.LocalSettings.Values[taskInstance.Task.Name + "TimeStamp"] = DateTime.Now.ToBinary();



                //Warning each 5 minutes to uninstall task if not debugging anymore
                if (!ApplicationData.Current.LocalSettings.Values.ContainsKey(taskInstance.Task.Name + "DebugWarning"))
                    ApplicationData.Current.LocalSettings.Values.Add(taskInstance.Task.Name + "DebugWarning", DateTime.Now.ToBinary());
                else
                {
                    if ((DateTime.Now-DateTime.FromBinary((long)ApplicationData.Current.LocalSettings.Values[taskInstance.Task.Name + "DebugWarning"])).TotalMinutes>=5)
                    {
                        ApplicationData.Current.LocalSettings.Values[taskInstance.Task.Name + "DebugWarning"] = DateTime.Now.ToBinary();
                        Windows.Data.Xml.Dom.XmlDocument toastXml = ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText01);

                        Windows.Data.Xml.Dom.XmlNodeList elements = toastXml.GetElementsByTagName("text");
                        foreach (IXmlNode node in elements)
                        {
                            node.InnerText = taskInstance.Task.Name+ " remember to uninstall task if not debugging";
                        }
                        ToastNotification notification = new ToastNotification(toastXml);
                        ToastNotificationManager.CreateToastNotifier().Show(notification);
                    }
                }
            }
        }
    }
}

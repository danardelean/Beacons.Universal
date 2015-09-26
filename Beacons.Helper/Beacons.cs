using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Diagnostics;

namespace Beacons
{
    public sealed class iBeaconData
    {
        public Guid UUID { get; set; }
        public ushort Major { get; set; }
        public ushort Minor { get; set; }
        public short TxPower { get; set; }
        public double Distance { get; set; }
        public short Rssi { get; set; }
        
        public iBeaconData()
        {
            UUID = Guid.Empty;
            Major = 0;
            Minor = 0;
            TxPower = 0;
            Distance = 0;
            Rssi = 0;
        }
    }
    public static class iBeaconExtensions
    {
        public static void SetiBeaconAdvertisement(this BluetoothLEAdvertisement Advertisment, iBeaconData data)
        {
            BluetoothLEManufacturerData manufacturerData = new BluetoothLEManufacturerData();

            // Set Apple as the manufacturer data
            manufacturerData.CompanyId = 76;

            var writer = new DataWriter();
            writer.WriteUInt16(0x0215); //bytes 0 and 1 of the iBeacon advertisment indicator

            if (data!=null& data.UUID!= Guid.Empty)
            {
                //If UUID is null scanning for all iBeacons
                writer.WriteBytes( data.UUID.ToByteArray());
                if (data.Major!=0)
                {
                    //If Major not null searching with UUID and Major
                    writer.WriteBytes(BitConverter.GetBytes(data.Major).Reverse().ToArray());
                    if (data.Minor != 0)
                    {
                        //If Minor not null we are looking for a specific beacon not a class of beacons
                        writer.WriteBytes(BitConverter.GetBytes(data.Minor).Reverse().ToArray());
                        if (data.TxPower != 0)
                            writer.WriteBytes(BitConverter.GetBytes(data.TxPower));
                    }
                }
            }

            manufacturerData.Data = writer.DetachBuffer();

            Advertisment.ManufacturerData.Clear();
            Advertisment.ManufacturerData.Add(manufacturerData);
        }

        public static iBeaconData ParseiBeaconAdvertisement(this BluetoothLEAdvertisement Advertisment,short RawSignalStrengthInDBm)
        {
            iBeaconData beacon = null;
            foreach(var adv in Advertisment.ManufacturerData)
               if (adv.CompanyId==76) //Apple
                {
                    var bytes = adv.Data.ToArray();
                    if (bytes[0] == 0x02 && bytes[1] == 0x15 && bytes.Length==23)
                    {
                        //iBeacon Data
                        beacon = new iBeaconData();
                        beacon.UUID =new Guid(bytes.Skip(2).Take(16).ToArray());
                        beacon.Major = BitConverter.ToUInt16(bytes.Skip(18).Take(2).Reverse().ToArray(),0);
                        beacon.Minor = BitConverter.ToUInt16(bytes.Skip(20).Take(2).Reverse().ToArray(),0);
                        beacon.TxPower =(short) (sbyte) bytes[22];
                        beacon.Rssi = RawSignalStrengthInDBm;

                        //Estimated value
                        //Read this article http://developer.radiusnetworks.com/2014/12/04/fundamentals-of-beacon-ranging.html 
                        beacon.Distance = CalculateDistance(beacon.TxPower,RawSignalStrengthInDBm);

                        Debug.WriteLine("UUID: "+beacon.UUID.ToString()+" Major: "+beacon.Major+" Minor:"+beacon.Minor+" Power: "+ beacon.TxPower+ " Rssi: "+RawSignalStrengthInDBm+" Distance:"+ beacon.Distance);
                    }
                }
   
            return beacon;
        }

        internal static double CalculateDistance(int txPower, double rssi)
        {
            if (rssi == 0)
            {
                return -1.0; // if we cannot determine accuracy, return -1.
            }

            double ratio = rssi * 1.0 / txPower;
            if (ratio < 1.0)
            {
                return Math.Pow(ratio, 10);
            }
            else
            {
                double accuracy = (0.89976) * Math.Pow(ratio, 7.7095) + 0.111;
                return accuracy;
            }
        }

    }
}

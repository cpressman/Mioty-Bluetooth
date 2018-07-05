// ---------------------------------------------------------------------------------
//  
// The MIT License(MIT)
//  
// Permission is hereby granted, free of charge, to any person obtaining a copy 
// of this software and associated documentation files (the "Software"), to deal 
// in the Software without restriction, including without limitation the rights 
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell 
// copies of the Software, and to permit persons to whom the Software is 
// furnished to do so, subject to the following conditions: 
//  
// The above copyright notice and this permission notice shall be included in 
// all copies or substantial portions of the Software. 
//   
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR 
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, 
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE 
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER 
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, 
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN 
// THE SOFTWARE. 
// ---------------------------------------------------------------------------------
using System;
using System.Linq;
using System.Text;
using Windows.ApplicationModel.Background;
using Windows.System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using BioSensor;
using Windows.Devices.Sensors;
using Windows.Networking.Proximity;
using System.Runtime.InteropServices.WindowsRuntime;
using NdefLibrary.Ndef;


// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace BackgroundApplicationDebug
{
    public sealed class StartupTask : IBackgroundTask
    {
        ThreadPoolTimer timer;
        BackgroundTaskDeferral _deferral;

        //private BluetoothLEDevice device = null; 
        private DeviceInformation device = null;

        RateSensor bs;

        DeviceWatcher deviceWatcher;
        BluetoothLEDevice BLEdevice;
        GattCharacteristicsResult characteristics;
        GattCharacteristic character;

        bool sent;

        // Accelerometer
        private Windows.Devices.Sensors.Accelerometer _accelerometer;

        // NFC
        private Windows.Networking.Proximity.ProximityDevice proxDevice;
        string NFCText;
        bool bNFCText;

        // Adding UUIDs for the Nordic UART
        const string UUID_UART_SERV = "6e400001-b5a3-f393-e0a9-e50e24dcca9e";//Nordic UART service
        const string UUID_UART_TX = "6e400003-b5a3-f393-e0a9-e50e24dcca9e";//TX Read Notify
        const string UUID_UART_RX = "6e400002-b5a3-f393-e0a9-e50e24dcca9e";//RX, Write characteristic

        byte[] ATCMGS = { 0x41, 0x54, 0x2B, 0x43, 0x4D, 0x47, 0x53, 0x3D };
        byte[] AT_SUFFIX = { 0x1A, 0x0D };

        byte[] HEARTBEAT_ID = { 0x36, 0x34 };
        byte[] HEARTBEAT_SIZE_CR = { 0x34, 0x0D };

        byte[] NFC_ID = { 0x36, 0x35 };

        byte[] ACCEL_ID = { 0x36, 0x36 };
        byte[] ACCEL_SIZE_CR = { 0x31, 0x30, 0x0D };

        byte[] CR = { 0x0D };

        public async void Run(IBackgroundTaskInstance taskInstance)
        {
            // 
            // TODO: Insert code to perform background work
            //
            // If you start any asynchronous methods here, prevent the task
            // from closing prematurely by using BackgroundTaskDeferral as
            // described in http://aka.ms/backgroundtaskdeferral

            _deferral = taskInstance.GetDeferral();

            await Task.Delay(30000);

            bs = new RateSensor();
            bs.RateSensorInit();

            await Task.Delay(1000);

            bs.RateMonitorON();

            deviceWatcher = DeviceInformation.CreateWatcher(
            "System.ItemNameDisplay:~~\"Adafruit\"",
             new string[] {
                    "System.Devices.Aep.DeviceAddress",
                    "System.Devices.Aep.IsConnected"
                    },
                DeviceInformationKind.AssociationEndpoint);

            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Removed += DeviceWatcher_Removed;
            deviceWatcher.Start();

            sent = false;

            // NFC
            proxDevice = ProximityDevice.GetDefault();
            if (proxDevice != null)
            {
                proxDevice.SubscribeForMessage("NDEF", messagedReceived);
            }
            else
            {
                Debug.WriteLine("No proximity device found\n");
            }

            this.timer = ThreadPoolTimer.CreateTimer(Timer_Tick, TimeSpan.FromSeconds(2));

            try
            {
                await Task.Run(async () =>
                {
                    while (true)
                    {

                        await Task.Delay(100000);
                    }
                });
            }
            catch (Exception ex)
            {
            }
            deviceWatcher.Stop();
        }  

        // This functions takes in the UART characteristic and the byte array to transmit and sends it to the device
        private async Task MiotyTransmitter(GattCharacteristic character, byte[] data, string messageSent)
        {
            var writer = new DataWriter();
            GattCommunicationStatus s;
            for (int i = 0; i < data.Length; i++)
            {
                writer.WriteByte(data[i]);
                if ((i + 1) % 20 == 0)
                {
                    // Transmit 20 byte chunks because of BLE's limitations
                    s = await character.WriteValueAsync(writer.DetachBuffer());
                    Debug.WriteLine("Send " + messageSent + " Payload: " + s.ToString() + " part " + (i+1)/20);
                }
            }
            // Transmit any leftovers
            if (data.Length % 20 != 0)
            {
                s = await character.WriteValueAsync(writer.DetachBuffer());
                Debug.WriteLine("Send " + messageSent + " Payload: " + s.ToString() + " final part");
            }
            await Task.Delay(5000);
        }

        private void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            device = null;
        }

        private async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            device = args;

            BLEdevice = await BluetoothLEDevice.FromIdAsync(device.Id);

            GattDeviceServicesResult result = await BLEdevice.GetGattServicesAsync();

            if (result.Status == GattCommunicationStatus.Success)
            {
                var services = result.Services;
                foreach (var service in services)
                {
                    if (service.Uuid.ToString() == UUID_UART_SERV)
                    {
                        characteristics = await service.GetCharacteristicsAsync();

                        foreach (var characteristic in characteristics.Characteristics)
                        {
                            if (characteristic.Uuid.ToString() == UUID_UART_RX)
                            {
                                character = characteristic;
                            }
                        }
                    }
                }
            }

            Debug.WriteLine("Device Added");
        }

        // Connect to the BLE device and send the data
        private async Task UpdateAllData()
        {
            if (device == null)
            {
                Debug.WriteLine("Device NULL");
                return;
            }

           
            // Heartbeat Data
            var heartRateHex = bs.GetHeartRateHexForMioty();

            // Configure and send Heartbeat data using the AT command
            var HeartbeatData = ATCMGS.Concat(HEARTBEAT_SIZE_CR).Concat(HEARTBEAT_ID).Concat(heartRateHex).Concat(AT_SUFFIX).ToArray();
            await MiotyTransmitter(character, HeartbeatData, "Heartbeat");

            // Accelerometer Data
            _accelerometer = Windows.Devices.Sensors.Accelerometer.GetDefault();
            if (_accelerometer == null)
            {
                Debug.WriteLine("No accelerometer found");
            }

            // Configure and send Accelerometer data using the AT command
            AccelerometerReading reading = _accelerometer.GetCurrentReading();
            _accelerometer = null;

            var AccelerometerHex = ConvertAccelerometerForMioty(reading);
            var AccelData = ATCMGS.Concat(ACCEL_SIZE_CR).Concat(ACCEL_ID).Concat(AccelerometerHex).Concat(AT_SUFFIX).ToArray();

            await MiotyTransmitter(character, AccelData, "Accelerometer");

            // NFC Data
            if (bNFCText)
            {
                var NFCHex = StringIntoHexForMioty(NFCText);
                var Size = IntIntoHexForMioty(NFCHex.Count() + 2);

                var NFCData = ATCMGS.Concat(Size).Concat(CR).Concat(NFC_ID).Concat(NFCHex).Concat(AT_SUFFIX).ToArray();

                bNFCText = false;
                await MiotyTransmitter(character, NFCData, "NFC");
            }
            return;
        }
        private async void Timer_Tick(ThreadPoolTimer timer)
        {
            Debug.WriteLine("TICK");
            await UpdateAllData();
            this.timer = ThreadPoolTimer.CreateTimer(Timer_Tick, TimeSpan.FromSeconds(10));
        }

        // Using the packet format 
        private byte[] ConvertAccelerometerForMioty(AccelerometerReading reading)
        {
            double[] arr = { reading.AccelerationX, reading.AccelerationY, reading.AccelerationZ };
            double max = arr.Max();
            double min = arr.Min();
            double precision;
            if (Math.Abs(max) > Math.Abs(min))
            {
                precision = Math.Ceiling(max);
            }
            else
                precision = Math.Ceiling(Math.Abs(min));

            var AccelerometerHex = new byte[8];
            sbyte SBytePreceision = Convert.ToSByte(precision);
            var ret = SByteIntoHexForMioty(SBytePreceision);
            AccelerometerHex[0] = ret[0];
            AccelerometerHex[1] = ret[1];
            for (int i = 0; i < 3; i++)
            {
                var temp = arr[i];
                int factor = 127;

                sbyte SByteAccelerometer = Convert.ToSByte(factor * temp / precision);
                ret = SByteIntoHexForMioty(SByteAccelerometer);
                AccelerometerHex[2 + i * 2] = ret[0];
                AccelerometerHex[3 + i * 2] = ret[1];
            }
            return AccelerometerHex;
        }

        // Turns the int value into 2 nibbles
        private byte[] SByteIntoHexForMioty(sbyte input)
        {
            var output = new byte[2];
            string heartRateHexString = input.ToString("X2");
            int i = 0;
            foreach (char letter in heartRateHexString)
            {
                output[i] = (byte)letter;
                i++;
            }
            return output;
        }

        private byte[] StringIntoHexForMioty(string input)
        {
            byte[] data = Encoding.ASCII.GetBytes(input);

            string hex = BitConverter.ToString(data).Replace("-", string.Empty);

            var output = new byte[hex.Count()];
            int i = 0;
            foreach (char letter in hex)
            {
                output[i] = (byte)letter;
                i++;
            }
            return output;
        }

        private byte[] IntIntoHexForMioty(int input)
        {
            string heartRateHexString = input.ToString();
            var output = new byte[heartRateHexString.Count()];
            int i = 0;
            foreach (char letter in heartRateHexString)
            {
                output[i] = (byte)letter;
                i++;
            }
            return output;
        }

        // We have received a new NFC message, for now just see what it is.
        // TODO: send it to BLE device
        private void messagedReceived(ProximityDevice device, ProximityMessage m)
        {
            uint x = m.Data.Length;
            byte[] b = new byte[x];
            b = m.Data.ToArray();

            NdefMessage ndefMessage = NdefMessage.FromByteArray(b);

            foreach (NdefRecord record in ndefMessage)
            {
                if (record.CheckSpecializedType(false) == typeof(NdefTextRecord))
                {
                    var textRecord = new NdefTextRecord(record);
                    Debug.WriteLine("\nTEXT: " + textRecord.Text);
                    NFCText = textRecord.Text;
                    bNFCText = true;
                }
            }
        }
    }
}

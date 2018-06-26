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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using Windows.ApplicationModel.Background;
using Windows.System.Threading;
using System.Diagnostics;
using Windows.Devices.Power;
using System.Threading.Tasks;
using Windows.System.Power;
using Windows.Devices.Enumeration;
using System.Collections.ObjectModel;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using Windows.Devices.Gpio;
using Windows.Devices.I2c;
using BioSensor;


// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace BackgroundApplicationDebug
{
    public sealed class StartupTask : IBackgroundTask
    {
        ThreadPoolTimer timer;
        BackgroundTaskDeferral _deferral;

		RateSensor bs;

		DeviceWatcher deviceWatcher;
        ObservableCollection<DeviceInformation> deviceList = new ObservableCollection<DeviceInformation>();

        // Adding UUIDs for the Nordic UART
        const string UUID_UART_SERV = "6e400001-b5a3-f393-e0a9-e50e24dcca9e";//Nordic UART service
        const string UUID_UART_TX = "6e400003-b5a3-f393-e0a9-e50e24dcca9e";//TX Read Notify
        const string UUID_UART_RX = "6e400002-b5a3-f393-e0a9-e50e24dcca9e";//RX, Write characteristic

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
                    "System.Devices.Aep.IsConnected" },
                DeviceInformationKind.AssociationEndpoint);

            deviceWatcher.Added += DeviceWatcher_Added;
            deviceWatcher.Removed += DeviceWatcher_Removed;
            deviceWatcher.Start();

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

            //
            // Once the asynchronous method(s) are done, close the deferral.
            //
            //_deferral.Complete();
            //
        }

        

        private void DeviceWatcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            device = null; 
        }

        //private BluetoothLEDevice device = null; 
        private DeviceInformation device = null;


        private async void DeviceWatcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            device = args;

            Debug.WriteLine("Device Added");
        }

        private async Task UpdateAllData()
        {
            if (device == null)
            {
                Debug.WriteLine("Device NULL");
                return;
            }

            BluetoothLEDevice BLEdevice = await BluetoothLEDevice.FromIdAsync(device.Id);
            GattDeviceServicesResult result = await BLEdevice.GetGattServicesAsync();

            if (result.Status == GattCommunicationStatus.Success)
            {
                var services = result.Services;
                foreach (var service in services)
                {
                    if (service.Uuid.ToString() == UUID_UART_SERV)
                    {
                        var characteristics = await service.GetCharacteristicsAsync();
                        while (BLEdevice.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
                        {
                            Debug.WriteLine("Device in use, waiting...");
                            await Task.Delay(500);
                            characteristics = await service.GetCharacteristicsAsync();
                        }
                        Debug.WriteLine("Watch connected");

                        foreach (var character in characteristics.Characteristics)
                        {
                            if (character.Uuid.ToString() == UUID_UART_RX)
                            {
								var heartRateHex = bs.GetHeartRateHex();
								// This should never happen
								if (heartRateHex.Length != 2) {
									BLEdevice.Dispose();
									return;
								}
								var writer = new DataWriter();
								GattCommunicationStatus s;
								var data = new byte[] { 0x41, 0x54, 0x2B, 0x43, 0x4D, 0x47, 0x53, 0x3D, 0x34, 0x0D, 0x36, 0x34, heartRateHex[0], heartRateHex[1], 0x1A, 0x0D };
								string hex = BitConverter.ToString(data);

								for (int i = 0; i < data.Length; i++) {
									writer.WriteByte(data[i]);
									if ((i+1)%20 == 0) {
										// Transmit 20 byte chunks
										s = await character.WriteValueAsync(writer.DetachBuffer());
										Debug.WriteLine("Send New Payload:" + s.ToString());
									}
								}
								// Transmit any leftovers
								if (data.Length%20 != 0) {
									s = await character.WriteValueAsync(writer.DetachBuffer());
									Debug.WriteLine("Send New Payload:" + s.ToString());
								}
                            }
                        }

                    }
                    service.Dispose();
                }
            }
            Debug.WriteLine("Device Watcher Stopped");
            deviceWatcher.Stop();
            BLEdevice.Dispose();
            BLEdevice = null;
            GC.Collect();
            Debug.WriteLine("Garbage Collected");
            return;
        }
            private async void Timer_Tick(ThreadPoolTimer timer)
        {
            if (deviceWatcher.Status == DeviceWatcherStatus.Stopped)
            {
                deviceWatcher.Start();
                Debug.WriteLine("Device Watcher Restarted");
            }
            await UpdateAllData();
            this.timer = ThreadPoolTimer.CreateTimer(Timer_Tick, TimeSpan.FromSeconds(10));
        }

        private async Task<BatteryReport> GetBatteryStatus()
        {
            string batteryStatus;
            int batteryPercent;

            var deviceInfo = await DeviceInformation.FindAllAsync(Battery.GetDeviceSelector());
            BatteryReport br = null;
            foreach (DeviceInformation device in deviceInfo)
            {
                try
                {
                    // Create battery object
                    var battery = await Battery.FromIdAsync(device.Id);

                    // Get report
                    var report = battery.GetReport();
                    br = report;
                    batteryStatus = report.Status.ToString();
                    if (batteryStatus == "Idle")
                    {
                        batteryStatus = PowerManager.RemainingChargePercent.ToString() + "%";
                    }

                }
                catch (Exception e)
                {
                    /* Add error handling, as applicable */
                }
            }

            batteryPercent = PowerManager.RemainingChargePercent;
            return br;
        }
    }


}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Gpio;
using Windows.Devices.I2c;

namespace BioSensor
{
    public sealed class RateSensor
    {
        // For PCA6800 ES
        //const int AS7000_GPIO_POWER_PIN =    24;         // ES
        //const int AS7000_GPIO_INT_PIN =      96;         // ES
        //const string AS7000_I2C_PORT =        "I2C2";     // ES
        //const string BMC156_I2C_PORT =        "I2C1";     // ES

        // For PCA6800 EV
        const int AS7000_GPIO_POWER_PIN = 8;       // EV
        const int AS7000_GPIO_INT_PIN = 9;         // EV
        const string AS7000_I2C_PORT = "I2C6";      // EV
        const string BMC156_I2C_PORT = "I2C1";      // EV
        const int AS7000_GPIO_LEDEN_PIN = 93;      // EV3 New

        const int AS7000_I2C_ADDRESS = 0x30;
        const int BMC156_I2C_ADDRESS = 0x10;

        const int AS7000_REG_PROTOCOL_VERSION_MAJOR = 0x00;
        const int AS7000_REG_PROTOCOL_VERSION_MINOR = 0x01;
        const byte AS7000_REG_SW_VERSION_MAJOR = 0x02;
        const byte AS7000_REG_SW_VERSION_MINOR = 0x03;
        const byte AS7000_REG_APPLICATION_ID = 0x04;
        const byte AS7000_REG_HW_VERSION = 0x05;
        const byte AS7000_REG_RESERVED0 = 0x06;
        const byte AS7000_REG_RESERVED1 = 0x07;

        const byte AS7000_REG_ACC_SAMPLE_FREQUENCY_MSB = 8;
        const byte AS7000_REG_ACC_SAMPLE_FREQUENCY_LSB = 9;

        const byte AS7000_REG_HOST_ONE_SECOND_TIME_MSB = 10;
        const byte AS7000_REG_HOST_ONE_SECOND_TIME_LSB = 11;

        const byte AS7000_REG_ACC_SAMPLES = 14;

        const byte AS7000_REG_ACC_DATA_INDEX = 20;
        const byte AS7000_REG_ACC_DATA_X_MSB = 21;
        const byte AS7000_REG_ACC_DATA_X_LSB = 22;
        const byte AS7000_REG_ACC_DATA_Y_MSB = 23;
        const byte AS7000_REG_ACC_DATA_Y_LSB = 24;
        const byte AS7000_REG_ACC_DATA_Z_MSB = 25;
        const byte AS7000_REG_ACC_DATA_Z_LSB = 26;

        const byte AS7000_REG_HRM_STATUS = 54;
        const byte AS7000_REG_HRM_HEARTRATE = 55;
        const byte AS7000_REG_HRM_SQI = 56;
        const byte AS7000_REG_HRM_SYNC = 57;

        // AS7000_REG_APPLICATION_ID values
        const byte AS7000_APPID_IDLE = 0x00;
        const byte AS7000_APPID_LOADER = 0x01;
        const byte AS7000_APPID_HRM = 0x02;

        const byte BMC156_REG_CHIP_ID = 0x00;
        const byte BMC156_REG_FIFO_STATUS = 0x0E;
        const byte BMC156_REG_RANGE_SEL = 0x0F;
        const byte BMC156_REG_BW_SEL = 0x10;
        const byte BMC156_REG_FIFO_CONFIG_1 = 0x3E;
        const byte BMC156_REG_FIFO_DATA = 0x3F;

        // BMC156_REG_RANGE_SEL values
        const byte BMC156_RANGE_SEL_2G = 0x03;
        const byte BMC156_RANGE_SEL_4G = 0x05;
        const byte BMC156_RANGE_SEL_8G = 0x08;
        const byte BMC156_RANGE_SEL_16G = 0x0C;

        // BMC156_REG_BW_SEL values
        const byte BMC156_BW_SEL_7_81Hz = 0x08;
        const byte BMC156_BW_SEL_15_63Hz = 0x09;
        const byte BMC156_BW_SEL_31_25Hz = 0x0A;
        const byte BMC156_BW_SEL_62_50Hz = 0x0B;
        const byte BMC156_BW_SEL_125Hz = 0x0C;
        const byte BMC156_BW_SEL_250Hz = 0x0D;
        const byte BMC156_BW_SEL_500Hz = 0x0E;
        const byte BMC156_BW_SEL_1000Hz = 0x0F;

        // BMC156_REG_FIFO_CONFIG_1 values
        const byte BMC156_FIFO_CONFIG_1_MODE_BYPASS = 0x00;
        const byte BMC156_FIFO_CONFIG_1_MODE_FIFO = 0x40;
        const byte BMC156_FIFO_CONFIG_1_MODE_STREAM = 0x80;

        const byte BMC156_FIFO_CONFIG_1_DATA_SELECT_XYZ = 0x00;
        const byte BMC156_FIFO_CONFIG_1_DATA_SELECT_XONLY = 0x01;
        const byte BMC156_FIFO_CONFIG_1_DATA_SELECT_YONLY = 0x10;
        const byte BMC156_FIFO_CONFIG_1_DATA_SELECT_ZONLY = 0x11;

        GpioPin PwrPin = null;
        GpioPin IntPin = null;
        GpioPin LedEnPin = null;

        int AS7000_PWR_ON()
        {
            PwrPin.Write(GpioPinValue.Low);

            // EV3 New
            LedEnPin.Write(GpioPinValue.High);

            return 0;
        }


        int AS7000_PWR_OFF()
        {
            PwrPin.Write(GpioPinValue.High);

            // EV3 New
            LedEnPin.Write(GpioPinValue.Low);

            return 0;
        }

        int AS7000_GPIO_Init()
        {
            var gpio = GpioController.GetDefault();

            if (gpio == null)
            {
                return -1;
            }

            try
            {
                GpioOpenStatus openStatus = 0;
                gpio.TryOpenPin(AS7000_GPIO_POWER_PIN, GpioSharingMode.Exclusive, out PwrPin, out openStatus);
                PwrPin.SetDriveMode(GpioPinDriveMode.Output);

                gpio.TryOpenPin(AS7000_GPIO_INT_PIN, GpioSharingMode.Exclusive, out IntPin, out openStatus);
                IntPin.SetDriveMode(GpioPinDriveMode.Input);

                //// EV3 New
                gpio.TryOpenPin(AS7000_GPIO_LEDEN_PIN, GpioSharingMode.Exclusive, out LedEnPin, out openStatus);
                LedEnPin.SetDriveMode(GpioPinDriveMode.Output);
            }
            catch (Exception ex)
            {
                return -1;
            }

            return 1;
        }

        I2cDevice HrmSensor = null;
        async Task<bool> AS7000_I2C_Init()
        {
            var i2cSettings = new I2cConnectionSettings(AS7000_I2C_ADDRESS); // connect to default address; 
            i2cSettings.BusSpeed = I2cBusSpeed.FastMode;
            //string deviceSelector = I2cDevice.GetDeviceSelector("I2C5");
            string deviceSelector = I2cDevice.GetDeviceSelector(AS7000_I2C_PORT);
            var i2cDeviceControllers = await DeviceInformation.FindAllAsync(deviceSelector);
            HrmSensor = await I2cDevice.FromIdAsync(i2cDeviceControllers[0].Id, i2cSettings);
            return false;
        }


        int AS7000_INT_ON()
        {
            IntPin.ValueChanged += IntPin_ValueChanged;
            return 0;
        }

        int AS7000_INT_OFF()
        {
            IntPin.ValueChanged -= IntPin_ValueChanged;

            return 0;
        }

        bool flgStart = false;

        // AS7000_REG_HRM_STATUS - Reg Value
        //
        // 0   OK
        // 1   Illegal parameter handed to a function (e.g. a 0-pointer)
        // 2   Data was lost (e.g. execution of a function did take too long and ADC data was not read fast enough)
        // 4   Accelerometer not accessible
        //

        // AS7000_REG_HRM_HEARTRATE - Reg Value
        //
        // // The heartrate result – units = bpm
        //

        // AS7000_REG_HRM_SQI - Reg Value
        //
        // 0..2    Q++    Excellent signal quality
        // 3..5    Q+     Good signal quality
        // 6..8    Q0     Acceptable signal quality
        // 9..10   Q-     Poor signal quality
        // 11..20  Q--    Worst signal quality
        // 21..253       Reserved - currently not used
        // 254     No-P  No presence detected (no object in proximity)
        // 255     ???   No signal quality calculated yet
        //

        // AS7000_REG_HRM_SYNC - Reg Value
        //
        // [1...255] incrementing each new HRM-result (roll-over to 1)
        // 0 = no HRM-result
        //

        byte hrmStatus = 0;
        byte hrmHeartRate = 0;
        byte hrmSqi = 0;
        byte hrmSync = 0;

        bool blockI2C = false; 

        private async void IntPin_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            // Using dummy Acc Data
            if (args.Edge == GpioPinEdge.FallingEdge)
            {
                if(blockI2C)
                {
                    //Debug.Write("*B*");
                    return;
                }
                //Debug.Write("\nINT:   ");
                if (flgStart == false)
                {
                    return;
                }
                blockI2C = true;


                //Debug.Write("1");
                // Host one-second time - 0=not-determined
                I2C_WriteRegData(HrmSensor, AS7000_REG_HOST_ONE_SECOND_TIME_MSB, 0x00);
                await Task.Delay(1);
                I2C_WriteRegData(HrmSensor, AS7000_REG_HOST_ONE_SECOND_TIME_LSB, 0x00);
                await Task.Delay(1);

                //Debug.Write("2");

                for (int i = 0; i < 10; i++)
                {

                    I2C_WriteRegData(HrmSensor, AS7000_REG_ACC_DATA_INDEX, (byte)(i + 1));
                    await Task.Delay(1);
                    I2C_WriteRegData(HrmSensor, AS7000_REG_ACC_DATA_X_MSB, 0x00);
                    await Task.Delay(1);
                    I2C_WriteRegData(HrmSensor, AS7000_REG_ACC_DATA_X_LSB, (byte)(0x07 + i));
                    await Task.Delay(1);
                    I2C_WriteRegData(HrmSensor, AS7000_REG_ACC_DATA_Y_MSB, 0x00);
                    await Task.Delay(1);
                    I2C_WriteRegData(HrmSensor, AS7000_REG_ACC_DATA_Y_LSB, (byte)(0x11 + i));
                    await Task.Delay(1);
                    I2C_WriteRegData(HrmSensor, AS7000_REG_ACC_DATA_Z_MSB, 0x07);
                    await Task.Delay(1);
                    I2C_WriteRegData(HrmSensor, AS7000_REG_ACC_DATA_Z_LSB, 0xD0 + 1);
                    await Task.Delay(1);
                }

                //Debug.Write("3");

                hrmStatus = I2C_ReadRegData(HrmSensor, AS7000_REG_HRM_STATUS);
                await Task.Delay(1);
                hrmHeartRate = I2C_ReadRegData(HrmSensor, AS7000_REG_HRM_HEARTRATE);
                await Task.Delay(1);
                hrmSqi = I2C_ReadRegData(HrmSensor, AS7000_REG_HRM_SQI);
                await Task.Delay(1);
                //hrmSync = I2C_ReadRegData(HrmSensor, AS7000_REG_HRM_SYNC);
                await Task.Delay(1);
                //Debug.Write("\nHRM: Status="+hrmStatus.ToString()+" Rate="+hrmHeartRate.ToString()+" Sqi="+hrmSqi.ToString()+" sync="+hrmSync.ToString());
                blockI2C = false; 
            }
        }


        public string GetHeartRateString()
        {
            return hrmHeartRate.ToString();
        }

		public int GetHeartRateInt()
		{
			return hrmHeartRate;
		}
		public byte[] GetHeartRateHex()
		{
			int heartRateInt = GetHeartRateInt();
			if (heartRateInt > 255)
				return null;
			var heartRateHex = new byte[2];
			string heartRateHexString = heartRateInt.ToString("X2");
			int i = 0;
			foreach (char letter in heartRateHexString)
			{
				heartRateHex[i] = (byte)letter;
				i++;
			}
			return heartRateHex;
		}

		int protocolVersion;
        int softwareVersion;
        byte applicationId;
        byte hwRevision;

        int AS7000_Get_Protocol_Version()
        {
            byte pbMajor = I2C_ReadRegData(HrmSensor, AS7000_REG_PROTOCOL_VERSION_MAJOR);
            byte pbMinor = I2C_ReadRegData(HrmSensor, AS7000_REG_PROTOCOL_VERSION_MINOR);
            protocolVersion = (int)pbMajor*256 + (int)pbMinor;
            return 0; 
        }

        int AS7000_Get_SW_Version()
        {
            byte pbMajor = I2C_ReadRegData(HrmSensor, AS7000_REG_SW_VERSION_MAJOR);
            byte pbMinor = I2C_ReadRegData(HrmSensor, AS7000_REG_SW_VERSION_MINOR);
            softwareVersion = (int)pbMajor*256 + (int)pbMinor;
            return 0;
        }

        int AS7000_Get_HW_Version()
        {
            hwRevision = I2C_ReadRegData(HrmSensor, AS7000_REG_HW_VERSION);
            return 0;
        }

        int AS7000_Get_App_ID()
        {
            applicationId = I2C_ReadRegData(HrmSensor, AS7000_REG_APPLICATION_ID);
            return 0;
        }



        public async void RateSensorInit()
        {
            AS7000_GPIO_Init();
            AS7000_PWR_ON();

            await Task.Delay(200); // If we do not sleep, we will have the FallingEdge event.
            AS7000_INT_ON();

            await AS7000_I2C_Init();
            await Task.Delay(300);
            AS7000_Get_Protocol_Version();
            AS7000_Get_SW_Version();
            AS7000_Get_HW_Version();
            AS7000_Get_App_ID();
            return; 
        }

        
        public async void RateMonitorON()
        {
            I2C_WriteRegData(HrmSensor, AS7000_REG_ACC_SAMPLE_FREQUENCY_MSB, 0x4e);
            I2C_WriteRegData(HrmSensor, AS7000_REG_ACC_SAMPLE_FREQUENCY_LSB, 0x20);
            AS7000_INT_ON();
            I2C_WriteRegData(HrmSensor, AS7000_REG_APPLICATION_ID, AS7000_APPID_HRM);
            await Task.Delay(1000);
            AS7000_Get_App_ID();
            await Task.Delay(300);
            flgStart = true;
        }

        public async void RateMonitorOff()
        {
            flgStart = false;
            AS7000_INT_OFF();
            await Task.Delay(1000);
            I2C_WriteRegData(HrmSensor, AS7000_REG_APPLICATION_ID, AS7000_APPID_IDLE);
            await Task.Delay(300);
            AS7000_Get_App_ID();
        }


        //byte[] I2C_ReadRegData(I2cDevice device, byte bRegister)
        //{
        //    I2cTransferResult result;
        //    byte[] wBuf = new byte[1];
        //    wBuf[0] = bRegister;
        //    byte[] rBuf = new byte[1];
        //    result = device.WriteReadPartial(wBuf, rBuf);

        //    if (result.Status != I2cTransferStatus.FullTransfer)
        //    {
        //        return null; 
        //    }

        //    return rBuf;
        //}
        byte I2C_ReadRegData(I2cDevice device, byte reg)
        {
            try
            {
                if (device == null) return 0;
                device.Write(new byte[] { reg });
                byte[] i2cBuffer = new byte[1];
                device.Read(i2cBuffer);
                //UInt16 r = (UInt16)((i2cBuffer[0] << 8) | (i2cBuffer[1]));
                return i2cBuffer[0];
            }
            catch (Exception ex) 
            {
                Debug.WriteLine("Read Exception");
                return 0; 
            }
        }

        void I2C_WriteRegData(I2cDevice device, byte reg, byte value)
        {
            try
            {
                if (device == null) return;
                device.Write(new byte[] { reg, value });
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Write Exception");
            }
        }

    }
}

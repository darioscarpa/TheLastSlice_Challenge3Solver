using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

using Windows.Storage.Streams;
using Windows.Devices.Gpio;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;

namespace LastSliceOven
{
    class OvenManager
    {
        // straight from the challenge e-mail!
        private static readonly Dictionary<char, int> s_letterToPinMapping = new Dictionary<char, int>() {
            {'A', 2 }, 
            {'B', 3 }, 
            {'D', 4 }, 
            {'L', 5 }, 
            {'N', 6 }, 
            {'P', 7 }, 
            {'R', 8 }, 
            {'T', 9 }, 
            {'U', 10 } 
        }; 

        public enum eBtn
        {
            UP,
            DN,
            LT,
            RT,
            A,
            B
        };

        public enum eBtnState
        {
            PRESSED,
            UNPRESSED
        };

        private const GpioPinValue BTN_PRESSED_VALUE = GpioPinValue.Low;
        private const GpioPinValue BTN_UNPRESSED_VALUE = GpioPinValue.High;

        private GpioController m_rGpioController;
        private readonly Dictionary<eBtn, GpioPin[]> m_rBtnToPinsMapping;

        public OvenManager()
        {
            m_rGpioController = GpioController.GetDefault();
            if (m_rGpioController == null) {
                Debug.WriteLine("GPIO: cannot use controller! ");
                return;
            }

            Dictionary<char, GpioPin> s_rGpioPins = new Dictionary<char, GpioPin>();
            foreach (var kv in s_letterToPinMapping) {
                char cPinLetter = kv.Key;
                int iPinNumber = kv.Value;
                try {
                    GpioPin rPin = m_rGpioController.OpenPin(iPinNumber);
                    rPin.Write(BTN_UNPRESSED_VALUE);
                    rPin.SetDriveMode(GpioPinDriveMode.Output);
                    Debug.WriteLine("GPIO: opened pin " + iPinNumber + " in output mode");
                    s_rGpioPins.Add(cPinLetter, rPin);
                } catch (Exception e) {
                    Debug.WriteLine("GPIO: cannot open pin " + iPinNumber);
                    return;
                }
            }

            m_rBtnToPinsMapping = new Dictionary<eBtn, GpioPin[]>() {
                { eBtn.A, new GpioPin[] { s_rGpioPins['A'] }},
                { eBtn.B, new GpioPin[] { s_rGpioPins['B'] }},
                { eBtn.UP, new GpioPin[] { s_rGpioPins['U'], s_rGpioPins['P'] }},
                { eBtn.DN, new GpioPin[] { s_rGpioPins['D'], s_rGpioPins['N'] }},
                { eBtn.LT, new GpioPin[] { s_rGpioPins['L'], s_rGpioPins['T'] }},
                { eBtn.RT, new GpioPin[] { s_rGpioPins['R'], s_rGpioPins['T'] }},
             };

            // part of me likes this alternative generic initialization too
            //
            //s_btnToPinsMapping = new Dictionary<eBtn, GpioPin[]>();
            //foreach (eBtn btn in Enum.GetValues(typeof(eBtn))) {
            //    List<GpioPin> list = new List<GpioPin>();
            //    string btnName = btn.ToString();
            //    for (int i = 0; i < btnName.Length; ++i) {
            //        list.Add(s_rGpioPins[btnName[i]]);
            //    }
            //    s_btnToPinsMapping.Add(btn, list.ToArray());
            //}
        }


        public void setBtn(eBtn btn, eBtnState state)
        {
            Debug.WriteLine("GPIO -> " + btn + " " + state);
            GpioPinValue v = state == eBtnState.PRESSED ? BTN_PRESSED_VALUE : BTN_UNPRESSED_VALUE;
            foreach (GpioPin rPin in m_rBtnToPinsMapping[btn]) {
                rPin.Write(v);
            }
        }

        // straight from the docs, nothing fancy!
        public async Task<string> readFromUART()
        {
            string aqs = SerialDevice.GetDeviceSelector("UART0");
            var dis = await DeviceInformation.FindAllAsync(aqs);
            SerialDevice SerialPort = await SerialDevice.FromIdAsync(dis[0].Id);
            Debug.WriteLine("UART opened");

            SerialPort.WriteTimeout = TimeSpan.FromMilliseconds(1000);
            SerialPort.ReadTimeout = TimeSpan.FromMilliseconds(1000);
            SerialPort.BaudRate = 9600;
            SerialPort.Parity = SerialParity.None;
            SerialPort.StopBits = SerialStopBitCount.One;
            SerialPort.DataBits = 8;
            Debug.WriteLine("UART configured");

            const uint maxReadLength = 1024;
            DataReader dataReader = new DataReader(SerialPort.InputStream);
            dataReader.InputStreamOptions = InputStreamOptions.Partial;
            uint bytesToRead = await dataReader.LoadAsync(maxReadLength);
            string rxBuffer = dataReader.ReadString(bytesToRead);

            return rxBuffer;
        }
    }
}

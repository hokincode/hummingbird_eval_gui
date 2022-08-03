using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Ports;
using System.Windows.Forms;
using System.Threading;
using System.IO;

namespace hummingbird
{
    public class finger_poc
    {
        private static string _deviceVirtualComPort;

        public SerialPort VirtualComport = new SerialPort();

        public static string DeviceVirtualComport
        {
            get { return _deviceVirtualComPort; }
            set { _deviceVirtualComPort = value; }
        }

        public finger_poc()
        {
            //_serialNumber = "";
            //_hardwareVersion = "1.00";
        }

        public void Init()
        {
            //string fingerResponse = "";

            // If the port is open, close it.
            if (VirtualComport.IsOpen) VirtualComport.Close();
            else
            {
                // Set the port's settings
                VirtualComport.BaudRate = 115200;
                VirtualComport.DataBits = 8;
                VirtualComport.StopBits = (StopBits)Enum.Parse(typeof(StopBits), "1");
                VirtualComport.Parity = (System.IO.Ports.Parity)Enum.Parse(typeof(System.IO.Ports.Parity), "None");
                VirtualComport.PortName = finger_poc.DeviceVirtualComport;
                VirtualComport.ReadTimeout = 1000;
                VirtualComport.NewLine = "\r\n";

                // Open the port
                try
                {
                    VirtualComport.Open();
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message + "\n\nPlease ensure COM port is correct," + " the dispenser is connected," +
                                    " and turned on before continuing.\n",
                                    "Setup Failure", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    VirtualComport.Open();
                }

                //// Confirm device
                //fingerResponse = GetQuery("?ID");

                //if (fingerResponse != "FSC BREADBOARD")
                //    MessageBox.Show("Please ensure the device is connected" +
                //                    " and turned on before continuing.\n",
                //                    "Setup Failure", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        public void Open()
        {
            // Set the port's settings
            VirtualComport.BaudRate = 115200;
            VirtualComport.DataBits = 8;
            VirtualComport.StopBits = StopBits.One;
            VirtualComport.Parity = Parity.None;
            VirtualComport.PortName = finger_poc.DeviceVirtualComport;
            VirtualComport.Open();
        }

        public void Close()
        {
            VirtualComport.Close();
        }

        public string GetQuery(string query)
        {
            string queryResponse = String.Empty;

            VirtualComport.Write(query);
            VirtualComport.Write("\r");

            // Wait briefly for response
            Thread.Sleep(50);

            // Read response until newline or timeout
            queryResponse = VirtualComport.ReadExisting();
            int breakLoop = 100;
            while (queryResponse.EndsWith("\r\n") == false && breakLoop > 0)
            {
                queryResponse += VirtualComport.ReadExisting();
                breakLoop--;
                Thread.Sleep(10);
            }

            // Remove NPCs
            queryResponse = queryResponse.Replace(query, "");
            queryResponse = queryResponse.Replace("\r", "");
            queryResponse = queryResponse.Replace("\n", "");

            return queryResponse;
        }

        public void CloseVirtualComPort()
        {
            // If the port is open, close it.
            if (VirtualComport.IsOpen) VirtualComport.Close();
        }

    }
}

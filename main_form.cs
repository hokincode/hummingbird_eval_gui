using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Timers;
using System.IO;
using System.IO.Ports;
using System.Windows.Forms.DataVisualization.Charting;
using HidSharp;

namespace hummingbird
{
    public partial class main_form : Form
    {
        HidDeviceLoader loader;
        HidDevice device;
        HidStream stream;
        byte[] bytes;

        float[,] force = new float[5,3];
        float[,] torque = new float[5,2];
        //int[] forceOffset = new int[] { 0, 0, 0 };
        //int[] torqueOffset = new int[] { 0, 0 };
        int[] timeSamplesForce = new int[3];
        int[] timeSamplesTorque = new int[2];
        int[] forceAxisMinimum = { -2000, -2000, -2000, -2000, -2000 };
        int[] forceAxisMaximum = { 2000, 2000, 2000, 2000, 2000 };
        int[] torqueAxisMinimum = { -50, -50, -50, -50, -50 };
        int[] torqueAxisMaximum = { 50, 50, 50, 50, 50 };
        int numFingers = 5;
        int numDoF = 5;
        int forceDataScalar = 1;
        int torqueDataScalar = 1;
        float[,] vectorSum = new float[5,5];
        float[,] vectorAverage = new float[5,5];
        int fingerAvgIndex = 0;
        int stdev_array_index = 0;
        const int stdev_array_size = 100;
        float[] stdev_array = new float[stdev_array_size];

        enum Hand
        {
            None,
            Left,
            Right
        }
        Hand handedness = Hand.None;
        bool hand_present_left = false;
        bool hand_present_right = false;

        bool averaging = false;
        int averageCount = 0;

        public main_form()
        {
            InitializeComponent();

            // X axis labels
            chartForceF1.ChartAreas["ChartArea1"].AxisX.Interval = 1;
            chartForceF1.ChartAreas["ChartArea1"].AxisX.Maximum = 3;
            chartForceF1.ChartAreas["ChartArea1"].AxisX.CustomLabels.Add(-0.5, 0.5, "X(mN)");
            chartForceF1.ChartAreas["ChartArea1"].AxisX.CustomLabels.Add(0.5, 1.5, "Y(mN)");
            chartForceF1.ChartAreas["ChartArea1"].AxisX.CustomLabels.Add(1.5, 2.5, "Z(mN)");
            chartTorqueF1.ChartAreas["ChartArea1"].AxisX.Interval = 1;
            chartTorqueF1.ChartAreas["ChartArea1"].AxisX.Maximum = 2;
            chartTorqueF1.ChartAreas["ChartArea1"].AxisX.CustomLabels.Add(-0.5, 0.5, "R(mN*m)");
            chartTorqueF1.ChartAreas["ChartArea1"].AxisX.CustomLabels.Add(0.5, 1.5, "P(mN*m)");

            // initialize averaging values
            for (int finger_idx = 0; finger_idx < numFingers; finger_idx++)
            {
                for (int dof_idx = 0; dof_idx < numDoF; dof_idx++)
                {
                    vectorSum[finger_idx, dof_idx] = 0;
                    vectorAverage[finger_idx, dof_idx] = 0;
                }
            }

            loader = new HidDeviceLoader();

            // attempt to connect left hand
            device = loader.GetDevices(0x483, 0x5750).FirstOrDefault(); 
            if (device != null)
            {
                hand_present_left = true;
            }

            // attempt to connect right hand
            device = loader.GetDevices(0x483, 0x5751).FirstOrDefault();
            if (device != null)
            {
                hand_present_right = true;
            }

            // neither hand detected
            if ((hand_present_left == false) && (hand_present_right == false))
            {
                handedness = Hand.None;
                Console.WriteLine("No devices detected.");
                MessageBox.Show("No devices detected.", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(1);
            }

            // only left hand detected
            if ((hand_present_left == true) && (hand_present_right == false))
            {
                handedness = Hand.Left;
                device = loader.GetDevices(0x483, 0x5750).FirstOrDefault();
                radio_hand_left.Checked = true;
                radio_hand_right.Checked = false;
            }

            // only right hand detected
            if ((hand_present_left == false) && (hand_present_right == true))
            {
                handedness = Hand.Right;
                device = loader.GetDevices(0x483, 0x5751).FirstOrDefault();
                radio_hand_left.Checked = false;
                radio_hand_right.Checked = true;
            }

            // both hands detected
            if ((hand_present_left == true) && (hand_present_right == true))
            {
                radio_hand_left.Enabled = true;
                radio_hand_right.Enabled = true;
                btn_hand_select.Enabled = true;
            }

        }

        private void main_form_Load(object sender, EventArgs e)
        {

        }

        private void main_form_Paint(object sender, PaintEventArgs e)
        {


        }

       


        /* update status bar */
        private void UpdateStatus(bool ding, string text)
        {
            /* text */
            tsslStatus.Text = text;
            this.Update();
        }

        private void UpdateChartForceF1()
        {
            double chartValue = 0;

            chartForceF1.Series["Series1"].Points.Clear();

            // plot data
            for (int i = 0; i < 3; i++)
            {
                timeSamplesForce[i] = i;
                // calculation and convert
                chartValue = force[0,i];
                chartForceF1.Series["Series1"].Points.AddXY(timeSamplesForce[i], chartValue);
            }

            // update labels
            lblVectorXF1.Text = Convert.ToString(force[0,0]);
            lblVectorYF1.Text = Convert.ToString(force[0,1]);
            lblVectorZF1.Text = Convert.ToString(force[0,2]);


            // auto scale
            chartForceF1.ChartAreas["ChartArea1"].AxisY.IsStartedFromZero = false;
            chartForceF1.ChartAreas["ChartArea1"].AxisY.Minimum = forceAxisMinimum[0];
            chartForceF1.ChartAreas["ChartArea1"].AxisY.Maximum = forceAxisMaximum[0];
        }

        private void UpdateChartTorqueF1()
        {
            double chartValue = 0;

            chartTorqueF1.Series["Series1"].Points.Clear();

            // plot data
            for (int i = 0; i < 2; i++)
            {
                timeSamplesTorque[i] = i;
                // calculation and convert
                chartValue = torque[0,i];
                chartTorqueF1.Series["Series1"].Points.AddXY(timeSamplesTorque[i], chartValue);
            }

            // update labels
            lblVectorRollF1.Text = Convert.ToString(torque[0,0]);
            lblVectorPitchF1.Text = Convert.ToString(torque[0,1]);

            // auto scale
            chartTorqueF1.ChartAreas["ChartArea1"].AxisY.IsStartedFromZero = false;
            chartTorqueF1.ChartAreas["ChartArea1"].AxisY.Minimum = torqueAxisMinimum[0];
            chartTorqueF1.ChartAreas["ChartArea1"].AxisY.Maximum = torqueAxisMaximum[0];
        }

        private void UpdateChartForceF2()
        {
            double chartValue = 0;

            chartForceF2.Series["Series1"].Points.Clear();

            // plot data
            for (int i = 0; i < 3; i++)
            {
                timeSamplesForce[i] = i;
                // calculation and convert
                chartValue = force[1, i];
                chartForceF2.Series["Series1"].Points.AddXY(timeSamplesForce[i], chartValue);
            }

            // update labels
            lblVectorXF2.Text = Convert.ToString(force[1, 0]);
            lblVectorYF2.Text = Convert.ToString(force[1, 1]);
            lblVectorZF2.Text = Convert.ToString(force[1, 2]);


            // auto scale
            chartForceF2.ChartAreas["ChartArea1"].AxisY.IsStartedFromZero = false;
            chartForceF2.ChartAreas["ChartArea1"].AxisY.Minimum = forceAxisMinimum[1];
            chartForceF2.ChartAreas["ChartArea1"].AxisY.Maximum = forceAxisMaximum[1];
        }

        private void UpdateChartTorqueF2()
        {
            double chartValue = 0;

            chartTorqueF2.Series["Series1"].Points.Clear();

            // plot data
            for (int i = 0; i < 2; i++)
            {
                timeSamplesTorque[i] = i;
                // calculation and convert
                chartValue = torque[1, i];
                chartTorqueF2.Series["Series1"].Points.AddXY(timeSamplesTorque[i], chartValue);
            }

            // update labels
            lblVectorRollF2.Text = Convert.ToString(torque[1, 0]);
            lblVectorPitchF2.Text = Convert.ToString(torque[1, 1]);

            // auto scale
            chartTorqueF2.ChartAreas["ChartArea1"].AxisY.IsStartedFromZero = false;
            chartTorqueF2.ChartAreas["ChartArea1"].AxisY.Minimum = torqueAxisMinimum[1];
            chartTorqueF2.ChartAreas["ChartArea1"].AxisY.Maximum = torqueAxisMaximum[1];
        }

        private void UpdateChartForceF3()
        {
            double chartValue = 0;

            chartForceF3.Series["Series1"].Points.Clear();

            // plot data
            for (int i = 0; i < 3; i++)
            {
                timeSamplesForce[i] = i;
                // calculation and convert
                chartValue = force[2, i];
                chartForceF3.Series["Series1"].Points.AddXY(timeSamplesForce[i], chartValue);
            }

            // update labels
            lblVectorXF3.Text = Convert.ToString(force[2, 0]);
            lblVectorYF3.Text = Convert.ToString(force[2, 1]);
            lblVectorZF3.Text = Convert.ToString(force[2, 2]);


            // auto scale
            chartForceF3.ChartAreas["ChartArea1"].AxisY.IsStartedFromZero = false;
            chartForceF3.ChartAreas["ChartArea1"].AxisY.Minimum = forceAxisMinimum[2];
            chartForceF3.ChartAreas["ChartArea1"].AxisY.Maximum = forceAxisMaximum[2];
        }

        private void UpdateChartTorqueF3()
        {
            double chartValue = 0;

            chartTorqueF3.Series["Series1"].Points.Clear();

            // plot data
            for (int i = 0; i < 2; i++)
            {
                timeSamplesTorque[i] = i;
                // calculation and convert
                chartValue = torque[2, i];
                chartTorqueF3.Series["Series1"].Points.AddXY(timeSamplesTorque[i], chartValue);
            }

            // update labels
            lblVectorRollF3.Text = Convert.ToString(torque[2, 0]);
            lblVectorPitchF3.Text = Convert.ToString(torque[2, 1]);

            // auto scale
            chartTorqueF3.ChartAreas["ChartArea1"].AxisY.IsStartedFromZero = false;
            chartTorqueF3.ChartAreas["ChartArea1"].AxisY.Minimum = torqueAxisMinimum[2];
            chartTorqueF3.ChartAreas["ChartArea1"].AxisY.Maximum = torqueAxisMaximum[2];
        }

        private void UpdateChartForceF4()
        {
            double chartValue = 0;

            chartForceF4.Series["Series1"].Points.Clear();

            // plot data
            for (int i = 0; i < 3; i++)
            {
                timeSamplesForce[i] = i;
                // calculation and convert
                chartValue = force[3, i];
                chartForceF4.Series["Series1"].Points.AddXY(timeSamplesForce[i], chartValue);
            }

            // update labels
            lblVectorXF4.Text = Convert.ToString(force[3, 0]);
            lblVectorYF4.Text = Convert.ToString(force[3, 1]);
            lblVectorZF4.Text = Convert.ToString(force[3, 2]);


            // auto scale
            chartForceF4.ChartAreas["ChartArea1"].AxisY.IsStartedFromZero = false;
            chartForceF4.ChartAreas["ChartArea1"].AxisY.Minimum = forceAxisMinimum[3];
            chartForceF4.ChartAreas["ChartArea1"].AxisY.Maximum = forceAxisMaximum[3];
        }

        private void UpdateChartTorqueF4()
        {
            double chartValue = 0;

            chartTorqueF4.Series["Series1"].Points.Clear();

            // plot data
            for (int i = 0; i < 2; i++)
            {
                timeSamplesTorque[i] = i;
                // calculation and convert
                chartValue = torque[3, i];
                chartTorqueF4.Series["Series1"].Points.AddXY(timeSamplesTorque[i], chartValue);
            }

            // update labels
            lblVectorRollF4.Text = Convert.ToString(torque[3, 0]);
            lblVectorPitchF4.Text = Convert.ToString(torque[3, 1]);

            // auto scale
            chartTorqueF4.ChartAreas["ChartArea1"].AxisY.IsStartedFromZero = false;
            chartTorqueF4.ChartAreas["ChartArea1"].AxisY.Minimum = torqueAxisMinimum[3];
            chartTorqueF4.ChartAreas["ChartArea1"].AxisY.Maximum = torqueAxisMaximum[3];
        }

        private void UpdateChartForceF5()
        {
            double chartValue = 0;

            chartForceF5.Series["Series1"].Points.Clear();

            // plot data
            for (int i = 0; i < 3; i++)
            {
                timeSamplesForce[i] = i;
                // calculation and convert
                chartValue = force[4, i];
                chartForceF5.Series["Series1"].Points.AddXY(timeSamplesForce[i], chartValue);
            }

            // update labels
            lblVectorXF5.Text = Convert.ToString(force[4, 0]);
            lblVectorYF5.Text = Convert.ToString(force[4, 1]);
            lblVectorZF5.Text = Convert.ToString(force[4, 2]);


            // auto scale
            chartForceF5.ChartAreas["ChartArea1"].AxisY.IsStartedFromZero = false;
            chartForceF5.ChartAreas["ChartArea1"].AxisY.Minimum = forceAxisMinimum[4];
            chartForceF5.ChartAreas["ChartArea1"].AxisY.Maximum = forceAxisMaximum[4];
        }

        private void UpdateChartTorqueF5()
        {
            double chartValue = 0;

            chartTorqueF5.Series["Series1"].Points.Clear();

            // plot data
            for (int i = 0; i < 2; i++)
            {
                timeSamplesTorque[i] = i;
                // calculation and convert
                chartValue = torque[4, i];
                chartTorqueF5.Series["Series1"].Points.AddXY(timeSamplesTorque[i], chartValue);
            }

            // update labels
            lblVectorRollF5.Text = Convert.ToString(torque[4, 0]);
            lblVectorPitchF5.Text = Convert.ToString(torque[4, 1]);

            // auto scale
            chartTorqueF5.ChartAreas["ChartArea1"].AxisY.IsStartedFromZero = false;
            chartTorqueF5.ChartAreas["ChartArea1"].AxisY.Minimum = torqueAxisMinimum[4];
            chartTorqueF5.ChartAreas["ChartArea1"].AxisY.Maximum = torqueAxisMaximum[4];
        }


        private void buttonZero_Click(object sender, EventArgs e)
        {

            for (int finger_idx = 0; finger_idx < numFingers; finger_idx++)
            {
                forceAxisMinimum[finger_idx] = -2000;
                forceAxisMaximum[finger_idx] = 2000;
                torqueAxisMinimum[finger_idx] = -50;
                torqueAxisMaximum[finger_idx] = 50;
            }
            

            if (!device.TryOpen(out stream))
            {
                Console.WriteLine("Failed to open device."); MessageBox.Show("Failed to open device.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error); Environment.Exit(1);
            }

            using (stream)
            {
                bytes = new byte[device.MaxInputReportLength];
                string cmdString = "zero";
                var message = new byte[64];

                ASCIIEncoding.ASCII.GetBytes(cmdString, 0, cmdString.Length, message, 2);
                message[0] = 0;
                message[1] = (byte)cmdString.Length;

                stream.Write(message, 0, 2 + cmdString.Length);
            }

        }

        private void button_stream_start_Click(object sender, EventArgs e)
        {
            button_stream_start.Enabled = false;
            button_stream_stop.Enabled = true;
            buttonZero.Enabled = true;
            timer_poll.Enabled = true;

            if (!device.TryOpen(out stream))
            {
                Console.WriteLine("Failed to open device."); MessageBox.Show("Failed to open device.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error); Environment.Exit(1);
            }

            using (stream)
            {
                bytes = new byte[device.MaxInputReportLength];
                string cmdString = "";
                var message = new byte[64];

                if (radioVector.Checked == true)
                {
                    cmdString = "printmode v";
                }
                else
                {
                    cmdString = "printmode r";
                }

                ASCIIEncoding.ASCII.GetBytes(cmdString, 0, cmdString.Length, message, 2);
                message[0] = 0;
                message[1] = (byte)cmdString.Length;

                stream.Write(message, 0, 2 + cmdString.Length);

                radioVector.Enabled = false;
                radioRaw.Enabled = false;

            }
        }

        private void button_stream_stop_Click(object sender, EventArgs e)
        {
            button_stream_stop.Enabled = false;
            button_stream_start.Enabled = true;
            buttonZero.Enabled = false;
            timer_poll.Enabled = false;

            if (!device.TryOpen(out stream))
            {
                Console.WriteLine("Failed to open device."); MessageBox.Show("Failed to open device.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error); Environment.Exit(1);
            }

            using (stream)
            {
                bytes = new byte[device.MaxInputReportLength];
                string cmdString = "printmode n";
                var message = new byte[64];

                ASCIIEncoding.ASCII.GetBytes(cmdString, 0, cmdString.Length, message, 2);
                message[0] = 0;
                message[1] = (byte)cmdString.Length;

                stream.Write(message, 0, 2 + cmdString.Length);
            }

            radioVector.Enabled = true;
            radioRaw.Enabled = true;
        }

        private void timer_poll_Tick(object sender, EventArgs e)
        {
            int count = 0;

            if (!device.TryOpen(out stream))
            {
                Console.WriteLine("Failed to open device."); MessageBox.Show("Failed to open device.", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error); Environment.Exit(1);
            }

            using (stream)
            {

                try
                {
                    count = stream.Read(bytes);
                }
                catch (TimeoutException)
                {
                    Console.WriteLine("Read timed out.");
                }

                if (count > 0)
                {
                    this.BeginInvoke(new EventHandler(DoUpdateStreamResponse));
                }
            }
        }

        private void DoUpdateStreamResponse(object sender, System.EventArgs e)
        {
            int[,] dataInts = new int[5, 5];
            int i = 0;
            int dataMSB = 0;
            int dataLSB = 0;

            try
            {
                // parse/calculate force/torques from received string
                for (int finger_idx = 0; finger_idx < numFingers; finger_idx++)
                {
                    // parse convert received bytes to int's
                    for (int dof_idx = 0; dof_idx < numDoF; dof_idx++)
                    {
                        // parse bytes in packets
                        dataMSB = Convert.ToInt16(bytes[dof_idx * 2 + finger_idx * numDoF * 2 + 1]);
                        dataLSB = Convert.ToInt16(bytes[dof_idx * 2 + finger_idx * numDoF * 2 + 2]);
                        dataInts[finger_idx, dof_idx] = dataMSB * 256 + dataLSB;
                        // undo two's complement data
                        if (dataInts[finger_idx, dof_idx] > 32767)
                        {
                            dataInts[finger_idx, dof_idx] -= 65536;
                        }
                    }

                    // associate force x,y,z
                    for (int dof_idx = 0; dof_idx < 3; dof_idx++)
                    {
                        //force[finger_idx, dof_idx] = ((dataInts[finger_idx, dof_idx] - 32768) * forceDataScalar) / 32768;
                        force[finger_idx, dof_idx] = dataInts[finger_idx, dof_idx];
                        // stretch scale if necessary
                        if (force[finger_idx, dof_idx] < forceAxisMinimum[finger_idx])
                            forceAxisMinimum[finger_idx] = Convert.ToInt32(force[finger_idx, dof_idx]);
                        else if (force[finger_idx, dof_idx] > forceAxisMaximum[finger_idx])
                            forceAxisMaximum[finger_idx] = Convert.ToInt32(force[finger_idx, dof_idx]);
                    }

                    // associate torque
                    for (int dof_idx = 3; dof_idx < numDoF; dof_idx++)
                    {
                        // read torque and convert to nM*m
                        //torque[finger_idx, dof_idx-3] = ((dataInts[finger_idx, dof_idx] - 32768) * torqueDataScalar) / 32768;
                        torque[finger_idx, dof_idx - 3] = dataInts[finger_idx, dof_idx];
                        // stretch scale if necessary
                        if (torque[finger_idx, dof_idx - 3] < torqueAxisMinimum[finger_idx])
                            torqueAxisMinimum[finger_idx] = Convert.ToInt32(torque[finger_idx, dof_idx - 3]);
                        else if (torque[finger_idx, dof_idx-3] > torqueAxisMaximum[finger_idx])
                            torqueAxisMaximum[finger_idx] = Convert.ToInt32(torque[finger_idx, dof_idx - 3]);
                    }
                }



                // plot data
                // finger #1
                chartForceF1.Invoke(new Action(() => UpdateChartForceF1()));
                chartTorqueF1.Invoke(new Action(() => UpdateChartTorqueF1()));
                // finger #2
                chartForceF2.Invoke(new Action(() => UpdateChartForceF2()));
                chartTorqueF2.Invoke(new Action(() => UpdateChartTorqueF2()));
                // finger #3
                chartForceF3.Invoke(new Action(() => UpdateChartForceF3()));
                chartTorqueF3.Invoke(new Action(() => UpdateChartTorqueF3()));
                // finger #4
                chartForceF4.Invoke(new Action(() => UpdateChartForceF4()));
                chartTorqueF4.Invoke(new Action(() => UpdateChartTorqueF4()));
                // finger #5
                chartForceF5.Invoke(new Action(() => UpdateChartForceF5()));
                chartTorqueF5.Invoke(new Action(() => UpdateChartTorqueF5()));

                if (averaging == true)
                {
                    averageCount++;

                    // update average calc
                    for (i = 0; i < 3; i++)
                    {
                        vectorSum[fingerAvgIndex,i] += force[fingerAvgIndex, i];
                        vectorAverage[fingerAvgIndex, i] = vectorSum[fingerAvgIndex, i] / averageCount;
                    }

                    for (i = 0; i < 2; i++)
                    {
                        vectorSum[fingerAvgIndex, i+3] += torque[fingerAvgIndex, i];
                        vectorAverage[fingerAvgIndex, i+3] = vectorSum[fingerAvgIndex, i+3] / averageCount;
                    }

                    //// fill stdev array for one dof
                    //stdev_array[stdev_array_index] = force[0, 0];
                    //stdev_array_index++;
                    //if (stdev_array_index >= stdev_array_size)
                    //{
                    //    stdev_array_index = 0;
                    //}

                    // update values in gui
                    label_vector_x.Text = Convert.ToString(Math.Round(vectorAverage[fingerAvgIndex,0], 1)) + " mN";
                    label_vector_y.Text = Convert.ToString(Math.Round(vectorAverage[fingerAvgIndex, 1], 1)) + " mN";
                    label_vector_z.Text = Convert.ToString(Math.Round(vectorAverage[fingerAvgIndex, 2], 1)) + " mN";
                    label_vector_roll.Text = Convert.ToString(Math.Round(vectorAverage[fingerAvgIndex, 3], 3)) + " mN*m";
                    label_vector_pitch.Text = Convert.ToString(Math.Round(vectorAverage[fingerAvgIndex, 4], 3)) + " mN*m";

                }

            }
            catch (Exception)
            {
                //MessageBox.Show("Exception in DoUpdateStreamResponse");
            }

        }

        private void button_avg_start_Click(object sender, EventArgs e)
        {
            button_avg_start.Enabled = false;
            cbAvgFinger.Enabled = false;
            button_avg_stop.Enabled = true;
            averaging = true;


        }

        private void button_avg_stop_Click(object sender, EventArgs e)
        {
            button_avg_start.Enabled = true;
            button_avg_stop.Enabled = false;
            cbAvgFinger.Enabled = true;
            averaging = false;

            averageCount = 0;

            for (int i = 0; i < 5; i++)
            {
                vectorSum[fingerAvgIndex, i] = 0;
                vectorAverage[fingerAvgIndex, i] = 0;
            }
        }

        private void cbAvgFinger_SelectedIndexChanged(object sender, EventArgs e)
        {
            button_avg_start.Enabled = true;
            fingerAvgIndex = Convert.ToInt32(cbAvgFinger.Text) - 1;
        }

        private void btn_hand_select_Click(object sender, EventArgs e)
        {
            btn_hand_select.Enabled = false;
            radio_hand_left.Enabled = false;
            radio_hand_right.Enabled = false;

            if (radio_hand_left.Checked == true)
            {
                handedness = Hand.Left;
                device = loader.GetDevices(0x483, 0x5750).FirstOrDefault();
            }

            else
            {
                handedness = Hand.Right;
                device = loader.GetDevices(0x483, 0x5751).FirstOrDefault();
            }
        }
    }
}

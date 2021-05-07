using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace New_Communication_App.Properties
{
    public partial class Form1 : Form
    {
        private ushort[] image;
        private int picType = 0;
        private string fileName = "";
        private int imageNum = 0;
        private int temp = 10;
        static SerialPort serialPort = null;

      int xframe;
      int yframe;
      float xofs;
      float yofs;
      float xsize;
      float ysize;
      float xbin;
      float ybin;
      float xsec;
      float xmsec;
      int blen;


        public Form1()
        {
            InitializeComponent();

            string[] coms = SerialPort.GetPortNames();
            serPortList.DataSource = coms;
            int portSelect = -1;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            numericUpDown1.Value = 20;
            numericUpDown2.Value = 10;

            
            pictureBox1.ImageLocation = "../defaultImage.png";
            pictureBox1.SizeMode = PictureBoxSizeMode.AutoSize;
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {

        }

        private void radioButton1_CheckedChanged(object sender, EventArgs e)
        {
            picType = 0;
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            picType = 1;
        }

        private void radioButton3_CheckedChanged(object sender, EventArgs e)
        {
            picType = 2;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (picType == 0)
            {
                image = Program.getLightFrameImage();
            }
            else if (picType == 1)
            {
                image = Program.getDarkFrameImage(temp);
            }
            else if (picType == 2)
            {
                image = Program.getFlatFrameImage();
            }
            displayImage();
        }
        private void displayImage(bool from_fits = false)
        {
            {
                Bitmap bitmap;
                if (from_fits)
                {
                    bitmap = Program.ushortArrToBitmap(image,(int)Program.xframe, (int)Program.yframe);
                }
                else
                {
                    bitmap = Program.ushortArrToBitmap(Program.byteArrToUshort(Program.imgbuf, (int)Program.xframe, (int)Program.yframe), Program.xframe, Program.yframe);
                }
                pictureBox1.Image = bitmap;
            }
        }

        ///save
        private void button2_Click(object sender, EventArgs e) 
        {
            Program.saveFITS(image);
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            if (picType == 1)
            {
                temp = Convert.ToInt32(Math.Round(numericUpDown2.Value, 0));
            }
        }

        private void serPortList_SelectedIndexChanged(object sender, EventArgs e)
        {
        }

        private void doConnect_Click(object sender, EventArgs e)
        {
            string port = (string)serPortList.SelectedItem;
            serialPort = new SerialPort(port) { BaudRate = 115200 };
            try { serialPort.Open(); }
            catch (Exception ex)
            {
                MessageBox.Show("Unable to open serial port");
                return;
            }
            Program.setPortTest(serialPort);
            //Once serial port is open, lets get some info

            xframe = getParam("xframe?"); // get the dimensions of sensor 
            Program.xframe = getParam("xframe?"); // get the dimensions of sensor 
            statusTB.AppendText("xframe --> " + xframe + Environment.NewLine);

            yframe = getParam("yframe?");
            Program.yframe = getParam("yframe?");
            statusTB.AppendText("yframe --> " + yframe + Environment.NewLine);

            Program.xofs = getParam("xoffset?"); // get x,y,w,h of Region of Interest (ROI) 
            xofs = getParam("xoffset?");
            statusTB.AppendText("xoffset --> " + xofs + Environment.NewLine);

            Program.yofs = getParam("yoffset?");
            yofs = getParam("yoffset?");
            statusTB.AppendText("yoffset --> " + yofs + Environment.NewLine);

            Program.xsize = getParam("xsize?");
            xsize = getParam("xsize?");
            statusTB.AppendText("xsize --> " + xsize + Environment.NewLine);

            Program.ysize = getParam("ysize?");
            
            ysize = getParam("ysize?");
            statusTB.AppendText("ysize --> " + ysize + Environment.NewLine);

            Program.xbin = getParam("xbin?"); // get current bin 
            xbin = getParam("xbin?");
            statusTB.AppendText("xbin --> " + xbin + Environment.NewLine);

            Program.ybin = getParam("ybin?");
            ybin = getParam("ybin?");
            statusTB.AppendText("ybin --> " + ybin + Environment.NewLine);

            Program.xsec = getParam("xsec?"); // get exposure in seconds 
            xsec = getParam("xsec?");
            statusTB.AppendText("xsec --> " + xsec + Environment.NewLine);

            Program.xmsec = getParam("xmsec?");
            xmsec = getParam("xmsec?");
            statusTB.AppendText("xmsec --> " + xmsec + Environment.NewLine);


            Program.blen = 2 * xframe * yframe;
            blen = 2 * xframe * yframe;

            Console.WriteLine("blen --> " + blen);

            Program.imgbuf = new byte[blen];
        }

        bool setParam(string cmd, int val)
        {
            serialPort.Write(string.Format("{0} {1}\n", cmd, val));
            string xf = serialPort.ReadLine().Trim();
            return xf.StartsWith("OK");
        }

        int getParam(string cmd) // retrieves a integer parameter 
        {
            serialPort.Write(cmd + "\n");
            string xf = serialPort.ReadLine().Trim();
            string[] ary = xf.Split(' ');
            return Convert.ToInt32(ary[0]);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            //load the fits file open up file explorer
            OpenFileDialog fd = new OpenFileDialog();
            if(fd.ShowDialog() == DialogResult.OK)
            {
                image = Program.loadFits(fd.FileName);
                displayImage(from_fits: true);
            }
        }
    }
}

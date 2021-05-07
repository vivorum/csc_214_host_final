using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Imaging;
using New_Communication_App.Properties;

// MARCELO CONNOR ROWAN

namespace New_Communication_App
{
    class Program
    {
        static SerialPort serialPort = null;
        static string[] coms = SerialPort.GetPortNames();
        static int portSelect = -1;

        public static int xframe;
        public static int yframe;
        public static float xofs;
        public static float yofs;
        public static float xsize;
        public static float ysize;
        public static float xbin;
        public static float ybin;
        public static float xsec;
        public static float xmsec;
        public static double temp;

        public static int blen;

        public static byte[] imgbuf;

        [STAThread]
        static void Main()
        {
            System.Windows.Forms.Application.EnableVisualStyles();
            System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
            System.Windows.Forms.Application.Run(new Form1());

            return;
        }

        public static void setPortTest(SerialPort port_num)
        {
            serialPort = port_num;
            Console.WriteLine(serialPort);
            Console.WriteLine(xframe + " < xframe");
            Console.WriteLine(yframe + " < yframe");
        }
        
        static bool loadImg()
        {
            int nb = 0;
            while (nb < blen)
            {
                //Console.WriteLine(serialPort.PortName);
                int btr = serialPort.BytesToRead;
                //Console.WriteLine(btr);
                int bytes_left = blen - nb;
                // only read image bytes, not delimeter (” OK\n”) bytes 
                int nrd = Math.Min(btr, bytes_left);
                //if (btr > 0)
                if(nrd > 0)
                {
                    int value_to_add = serialPort.Read(imgbuf, nb, nrd);
                    nb += value_to_add;
                }
            }
            string isok = serialPort.ReadLine();
            return (isok.Contains("OK"));
        }

        static bool setParam(string cmd, int val)
        {
            serialPort.Write(string.Format("{0} {1}\n", cmd, val));
            string xf = serialPort.ReadLine().Trim();
            return xf.StartsWith("OK");
        }

        static int getParam(string cmd) // retrieves a integer parameter 
        {
            serialPort.Write(cmd + "\n");
            string xf = serialPort.ReadLine().Trim();
            string[] ary = xf.Split(' ');
            return Convert.ToInt32(ary[0]);
        }

        static double getDParam(string cmd) // retrieves a floating point parameter 
        { // for example, temperature 
            serialPort.Write(cmd + "\n");
            string xf = serialPort.ReadLine().Trim();
            string[] ary = xf.Split(' ');
            return Convert.ToDouble(ary[0]);
        }

        static bool sendCommand(string cmd, bool doWait = true)
        {
            serialPort.Write(string.Format("{0}\n", cmd));
            if (doWait)
            {
                string xf = serialPort.ReadLine().Trim();
                return xf.StartsWith("OK");
            }
            else
            {
                return true;
            }
        }

        // MARCELO's CODE STARTS HERE
        /// <summary>
        ///     Gets the current temperature of the camera.
        /// </summary>
        /// <returns>Returns the current temperature of the camera</returns>
        static double getTemp() {
            return getDParam("temp?");
        }


        public static Bitmap ushortArrToBitmap(ushort[] img, int w, int h)
        {
            // Useful variables
            int numpix = w * h;

            // First generate the histogram.
            SortedDictionary<int, int> histogram = new SortedDictionary<int,
                                                                        int>();
            for (int i = 0; i < numpix; i++)
            {
                int brightness = (int)img[i];
                if (histogram.ContainsKey(brightness))
                {
                    histogram[brightness] += 1;
                }
                else
                {
                    histogram[brightness] = 1;
                }
            }

            // With the new histogram, calculate the brightness with 3% of
            // pixels darker than that brightness
            int min = -1;
            int cMN = (int)((double)numpix * 0.03);
            int sum = 0;
            for (int k = 0; k < 65536; k++)
            {
                if (histogram.ContainsKey(k))
                {
                    sum += histogram[k];
                    if (sum < cMN)
                        min = k;
                }
            }

            // Calculate the pixel value where 99.7% of all pixel values are
            // darker than the value
            int max = -1;
            int cMX = (int)((double)numpix * 0.997);
            sum = 0;
            for (int k = 0; k < 65536; k++)
            {
                if (histogram.ContainsKey(k))
                {
                    sum += histogram[k];
                    if (sum < cMX)
                        max = k;
                }
            }

            // Create a lookup table for the pixel values
            byte[] trc = new byte[65536];
            double sf = (255.0) / (double)(max - min);
            for (int k = 0; k < min; k++)
                trc[k] = 0;
            for (int k = min; k <= max; k++)
                trc[k] = (byte)((double)(k - min) * sf);
            for (int k = max + 1; k < 65536; k++)
                trc[k] = 255;

            // Create a uint buffer to store the mapped pixel values
            uint[] mbuf = new uint[numpix];
            for (int i = 0; i < numpix; i++)
            {
                ushort bright = trc[(int)img[i]];
                mbuf[i] = (uint)(0xff << 24 | bright << 16 | bright << 8 | bright);
            }

            // Finally, create the image
            Bitmap dsp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            BitmapData bmd = dsp.LockBits(new Rectangle(0, 0, w, h),
                                    ImageLockMode.ReadWrite, dsp.PixelFormat);
            unsafe
            {
                uint* lptr = (uint*)bmd.Scan0;
                for (int k = 0; k < numpix; k++)
                    *lptr++ = mbuf[k];
            }
            dsp.UnlockBits(bmd);

            // Return the newly created bitmap
            return dsp;
        }

        public static ushort[] byteArrToUshort(byte[] bytes, int w, int h)
        {
            // Grab the number of pixels
            int numpix = w * h;

            // Make a change from bytes to ushort
            ushort[] img = new ushort[numpix];
            for (int i = 0; i < numpix; i++)
            {
                ushort curr = (ushort)(((ushort)bytes[i * 2] << 8) | ((ushort)bytes[i * 2 + 1]));
                img[i] = curr;
            }
            return img;
        }

        /// <summary>
        ///     Turns raw image bytes into a Bitmap.
        /// </summary>
        /// <param name="bytes">The raw bytes for the image.</param>
        /// <param name="w">The width of the image.</param>
        /// <param name="h">The height of the image.</param>
        /// <returns>Returns a Bitmap of the image.</returns>
        public static Bitmap byteArrToBitmap(byte[] bytes, int w, int h) {
            // Useful variables
            int numpix = w*h;

            // First generate the histogram.
            SortedDictionary<int, int> histogram = new SortedDictionary<int, 
                                                                        int>();
            for (int i = 0; i < numpix; i++) {
                int brightness = ((int)(bytes[i*2])<<8)|(int)(bytes[i*2+1]);
                if (histogram.ContainsKey(brightness)) {
                    histogram[brightness] += 1;
                } else {
                    histogram[brightness] = 1;
                }
            }

            // With the new histogram, calculate the brightness with 3% of
            // pixels darker than that brightness
            int min = -1;
            int cMN = (int)((double)numpix * 0.03);
            int sum = 0;
            for (int k = 0; k < 65536; k++) {
                if (histogram.ContainsKey(k)) {
                    sum += histogram[k];
                    if (sum < cMN)
                        min = k;
                }
            }

            // Calculate the pixel value where 99.7% of all pixel values are
            // darker than the value
            int max = -1;
            int cMX = (int)((double)numpix * 0.997);
            sum = 0;
            for (int k = 0; k < 65536; k++) {
                if (histogram.ContainsKey(k)) {
                    sum += histogram[k];
                    if (sum < cMX)
                        max = k;
                }
            }

            // Create a lookup table for the pixel values
            byte[] trc = new byte[65536];
            double sf = (255.0) / (double)(max-min);
            for (int k = 0; k < min; k++)
                trc[k] = 0;
            for (int k = min; k <= max; k++)
                trc[k] = (byte)((double)(k-min)*sf);
            for (int k = max+1; k < 65536; k++)
                trc[k] = 255;
            
            // Create a uint buffer to store the mapped pixel values
            uint[] mbuf = new uint[numpix];
            for (int i = 0; i < numpix; i++) {
                ushort bright = trc[((ushort)(bytes[i*2])<<8)|(ushort)(bytes[i*2+1])];
                mbuf[i] = (uint)(0xff<<24 | bright<<16 | bright<<8 | bright);
            }

            // Finally, create the image
            Bitmap dsp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
            BitmapData bmd = dsp.LockBits(new Rectangle(0, 0, w, h),
                                    ImageLockMode.ReadWrite, dsp.PixelFormat);
            unsafe {
                uint *lptr = (uint*)bmd.Scan0;
                for (int k = 0; k < numpix; k++)
                    *lptr++ = mbuf[k];
            }
            dsp.UnlockBits(bmd);

            // Return the newly created bitmap
            return dsp;
        }

        /// <summary>
        ///     Turns raw image bytes into a ushort[]
        /// </summary>
        /// <param name="bytes">The raw bytes for the image.</param>
        /// <param name="w">The width of the image</param>
        /// <param name="h">The height of the image</param>
        /// <returns>Returns a ushort[] of the image.</returns>
        static ushort[] byteArrToImage(byte[] bytes, int w, int h) {
            // Create a return array
            ushort[] img = new ushort[w*h];
            for (int i = 0; i < w*h; i++) {
                img[i] = (ushort)(((int)bytes[(i*2)] << 8) | ((int)bytes[(i*2) + 1]));
            }

            // Return the new image
            return img;
        }

        /// <summary>
        ///     Subtracts one image from another.
        /// </summary>
        /// <param name="a">The first image.</param>
        /// <param name="b">The image subtracted from a.</param>
        /// <param name="w">The width of the images.</param>
        /// <param name="h">The height of the images.</param>
        /// <returns>The difference between a and b.</returns>
        static ushort[] subtractImage(ushort[] a, ushort[] b, int w, int h) {
            ushort[] c = new ushort[a.Length];

            // Go pixel by pixel
            for (int x = 0; x < w; x++) {
                for (int y = 0; y < h; y++) {
                    // Grab the colors of the pixels for a and b
                    ushort aBright = a[y*w+x];
                    ushort bBright = b[y*w+x];
                    
                    // Create the new color
                    ushort cBright = (ushort)(aBright-bBright);
                    if (cBright > aBright) // Cap at 0 if overflow
                        cBright = 0;

                    // Set the new pixel color on c
                    c[y*w+x] = cBright;
                }
            }

            // Return c
            return c;
        }

        /// <summary>
        ///     Averages a list of images.
        /// </summary>
        /// <param name="imgs">The list of images to average.</param>
        /// <param name="w">The width of the images.</param>
        /// <param name="h">The height of the images.</param>
        /// <returns>The average of all images in imgs.</returns>
        static ushort[] averageImages(ushort[][] imgs, int w, int h) {
            // Store the length
            int l = imgs.Length;

            // Create an image to store data in
            ushort[] ret = new ushort[w*h];

            // Loop through each pixel
            for (int x = 0; x < w; x++) {
                for (int y = 0; y < h; y++) {
                    // Sums for the brightness
                    int sum = 0;
                    foreach (ushort[] img in imgs) {
                        sum += img[y*w+x];
                    }

                    // Set the pixel on the new image
                    ret[y*w+x] = (ushort)(sum/l);
                }
            }

            // Return the new image
            return ret;
        }

        /// <summary>
        ///     Gets a light frame image from the camera.
        /// </summary>
        /// <returns>Returns a light frame image in ushort[] form.</returns>
        public static ushort[] getLightFrameImage() {
            // Grab a light frame
            //sendCommand("open", doWait: false);
            sendCommand("capture", doWait: false);
            Console.WriteLine("Actually got here");
            loadImg();
            //imgbuf = Form1.
            Console.WriteLine("Got here");
            ushort[] frame = byteArrToImage(imgbuf, xframe, yframe);
            sendCommand("close", doWait: true);

            // Return the new light frame
            return frame;
        }

        /// <summary>
        ///     Gets a dark frame image from the camera.
        /// </summary>
        /// <param name="targetTemp">The target temperature of the cam.</param>
        /// <returns>Returns a captured dark frame from the camera.</returns>
        public static ushort[] getDarkFrameImage(double targetTemp) {
            // Set the temperature to the correct value
            double temp = getTemp();
            while (Math.Abs(targetTemp-temp) > 0.1) {
                if (temp < targetTemp) { // Turn off cooling
                    sendCommand("tecon", doWait: true);
                } else { // Turn on cooling
                    sendCommand("tecoff", doWait: true);
                }
            }

            // Take a dark frame image
            sendCommand("close", doWait: true);
            sendCommand("dcapture", doWait: false);
            loadImg();
            ushort[] dframe = byteArrToImage(imgbuf, xframe, yframe);

            // Return the image
            return dframe;
        }

        /// <summary>
        ///     Gets a flat frame image from the camera.
        /// </summary>
        /// <returns>Returns a flat frame image from the camera.</returns>
        public static ushort[] getFlatFrameImage() {
            // Grab a light frame
            sendCommand("open", doWait: true);
            sendCommand("capture", doWait: false);
            loadImg();
            ushort[] frame = byteArrToImage(imgbuf, xframe, yframe);
            sendCommand("close", doWait: true);

            // Return the new light frame
            return frame;
        }

        /// <summary>
        ///     Performs dark frame subtraction.
        /// </summary>
        /// <param name="targetTemp">The target temperature for the cam.</param>
        /// <returns>Returns a new image after dark frame subtraction.</returns>
        static ushort[] runDarkFrameSubtraction() {
            // Open up the dark captured dark frames and the single
            // light frame
            // TODO: Write code
            ushort[][] dframes = new ushort[3][];
            ushort[] lFrame = null;
            int w = 100;
            int h = 100;

            // Average the dark frames
            ushort[] master = averageImages(dframes, w, h);

            // Subtract the master dark frame from the light frame
            ushort[] difference = subtractImage(lFrame, master, w, h);

            // Return the difference
            return difference;
        }

        /// <summary>
        ///     Performs a flat field correction on an image.
        /// </summary>
        /// <param name="w">The width of the image.</param>
        /// <param name="h">The height of the image.</param>
        /// <returns>Returns a new flat field corrected image.</returns>
        static ushort[] runFlatFieldCorrection(int w, int h) {
            // Open the 3 flat frames
            // TODO: Write the code
            ushort[][] fframes = new ushort[3][];
            double temp = 0.0;

            // Open up the dark captured dark frames and the single
            // light frame
            // TODO: Write code
            ushort[][] dframes = new ushort[3][];
            ushort[] lframe = null;

            // Average the dark frames
            ushort[] master = averageImages(dframes, w, h);

            // Subtract the master dark frame from the flat frames
            ushort[][] differences = new ushort[3][];
            for (int i = 0; i < 3; i++) {
                differences[i] = subtractImage(fframes[i], master, w, h);
            }

            // Create the master flat field image
            ushort[] mFF = averageImages(differences, w, h);

            // Calculate the average pixel value of the middle 25%
            int mFFSum = 0;
            int mFFCount = 0;
            for (int x = w/4; x < w*3/4; x++) {
                for (int y = h/4; y < h*3/4; y++) {
                    mFFSum += mFF[y*w+x];
                    mFFCount += 1;
                }
            }
            double mFFAvg = ((double)mFFSum)/((double)mFFCount);

            // Perform the correction
            ushort[] corrected = new ushort[w*h];
            for (int x = 0; x < w; x++) {
                for (int y = 0; y < h; y++) {
                    ushort lb = lframe[y*w+x];
                    ushort mb = mFF[y*w+x];
                    ushort newB = (ushort)(((double)lb)*mFFAvg/((double)mb));
                    corrected[y*w+x] = newB;
                }
            }

            // Return the newly corrected image
            return corrected;
        }

        /// <summary>
        ///     Calculates the distance between two points.
        /// </summary>
        /// <param name="x1">The x coordinate of the first point.</param>
        /// <param name="y1">The y coordinate of the first point.</param>
        /// <param name="x2">The x coordinate of the second point.</param>
        /// <param name="y2">The y coordinate of the second point.</param>
        /// <returns>Returns the distance between (x1,y1) and (x2,y2)</returns>
        public double distance(int x1, int y1, int x2, int y2) {
            int xsq = (int)Math.Pow(x1-x2, 2);
            int ysq = (int)Math.Pow(y1-y2, 2);
            return Math.Sqrt(xsq+ysq);
        }

        /// <summary>
        ///     Finds the coordinates of the nearest star.
        /// </summary>
        /// <param name="img">The image to run the code on.</param>
        /// <param name="w">The width of the image.</param>
        /// <param name="h">The height of the image.</param>
        /// <param name="x">The x coordinate of the starting point.</param>
        /// <param name="y">The y coordinate of the starting point.</param>
        /// <returns>Returns the coordinates of (x,y)'s nearest star.</returns>
        public (double, double) getNearestStar(ushort[] img, int w, int h,
                                            int x, int y) {
            // Get ready for a lot of math!!!

            // The maximum radius in pixels to check for the star
            int maxRadius = 30;

            // First, calculate the average pixel value in the image
            int pixsum = 0;
            int pixcount = w*h;
            for (int i = 0; i < w; i++) {
                for (int j = 0; j < h; j++) {
                    pixsum += img[j*w+i];
                }
            }
            double avgbright = ((double)pixsum)/((double)pixcount);

            // Starting from the current value, spiral until a bright
            // pixel is found
            int cx = x; // Current x value
            int cy = y; // Current y value
            int dir = 0; // 0 is left, 1 is down, 2 is right, and 3 is up
            (int, int)[] corners = {(cx, cx), (cx, cx), (cx, cx), (cx, cx)};
            while (img[cy*w+cx] < avgbright && 
                        distance(x, y, cx, cy) < maxRadius) {
                // Make the next move
                (int, int) corner = corners[dir];
                if (dir == 0) { // Left
                    cx -= 1;
                    if (cx < corner.Item1) {
                        corners[dir] = (cx, cy);
                        dir = 1;
                    }
                } else if (dir == 1) { // Down
                    cy += 1;
                    if (cy > corner.Item2) {
                        corners[dir] = (cx, cy);
                        dir = 2;
                    }
                } else if (dir == 2) { // Right
                    cx += 1;
                    if (cx > corner.Item1) {
                        corners[dir] = (cx, cy);
                        dir = 3;
                    }
                } else { // Up
                    cy -= 1;
                    if (cy < corner.Item2) {
                        corners[dir] = (cx, cy);
                        dir = 0;
                    }
                }
            }

            // Now, a bright pixel should be found at cx, cy. We need
            // to find the center of the star in int pixel values.
            int lastBrightness = img[cy*w+cx];
            while (true) {
                if (img[cy*w+(cx-1)] > lastBrightness) { // Left
                    cx -= 1;
                    lastBrightness = img[cy*w+cx];
                    continue;
                } else if (img[(cy+1)*w+cx] > lastBrightness) { // Down
                    cy += 1;
                    lastBrightness = img[cy*w+cx];
                    continue;
                } else if (img[cy*w+(cx+1)] > lastBrightness) { // Right
                    cx += 1;
                    lastBrightness = img[cy*w+cx];
                    continue;
                }
                else if (img[(cy - 1) * w + cx] > lastBrightness)
                { // Up
                    cy -= 1;
                    lastBrightness = img[cy*w+cx];
                    continue;
                }
                break;
            }

            // Now (cx, cy) should be whole number representations of the center
            // of the star. Now we need to find the bounds of the star.
            int left = cx;
            int right = cx;
            int top = cy;
            int bottom = cy;
            while (img[cy*w+left] > avgbright)
                left -= 1;
            while (img[cy*w+right] > avgbright)
                right += 1;
            while (img[top*w+cx] > avgbright)
                top -= 1;
            while (img[bottom*w+cx] > avgbright)
                bottom += 1;
            
            // Perform the actual centroid analysis to find the absolute
            // center of the star
            int xnum = 0;
            int xden = 0;
            int ynum = 0;
            int yden = 0;
            for (int tx = left; tx <= right; tx++) {
                for (int ty = top; ty <= bottom; ty++) {
                    int brightness = (int)img[ty*w+tx];
                    xnum += (brightness*tx);
                    xden += brightness;
                    ynum += (brightness+ty);
                    yden += brightness;
                }
            }

            // Finally, calculate the absolute position
            double finx = ((double)xnum)/((double)xden);
            double finy = ((double)ynum)/((double)yden);

            // Return the final values
            return (finx, finy);
        }

        /// <summary>
        ///     Stacks a lot of images.
        /// </summary>
        /// <param name="w">The width of the images.</param>
        /// <param name="h">The height of the images.</param>
        /// <returns>Returns the final stacked image.</returns>
        static ushort[] runStacking(int w, int h) {
            // Pull out the images to be stacked along with the selected
            // star positions
            // TODO: Write code and integrate with GUI
            ushort[][] frames = new ushort[5][];
            (double, double, double, double)[] pos = 
                    new (double, double, double, double)[5];

            ushort[] fin = frames[0];
            double x1 = pos[0].Item1;
            double y1 = pos[0].Item2;
            double x2 = pos[0].Item3;
            double y2 = pos[0].Item4;
            double dx = x1-x2;
            double dy = y1-y2;

            // Stack each frame
            for (int i = 1; i < pos.Length; i++) {
                // Some helpful values
                double x1p = pos[i].Item1;
                double y1p = pos[i].Item2;
                double x2p = pos[i].Item3;
                double y2p = pos[i].Item4;
                double dxp = x1p-x2p;
                double dyp = y1p-y2p;
                
                // Calculate a, b, c, and d
                double b = ((dxp*dy)-(dyp*dx))/((dx*dx)+(dy*dy));
                double a = ((dxp*dx)-(dyp*dy))/((dx*dx)+(dy*dy));
                double c = x1p-(a*x1)-(b*y1);
                double d = y1p+(b*x1)-(a*y1);

                for (int x = 0; x < w; x++) {
                    for (int y = 0; y < h; y++) {
                        // Calculate the mirroring values on the second image
                        double xp = (a*x)+(b*y)+c;
                        double yp = (-1*b*x)+(a*y)+d;

                        // The corners of the new pixel box.
                        (double,double)[] corners = {(xp-0.5,yp-0.5), 
                                                    (xp+0.5,yp-0.5), 
                                                    (xp-0.5,yp+0.5), 
                                                    (xp+0.5,yp+0.5)};

                        // Find the border between the corners.
                        double bx = (double)(int)(xp)+0.5;
                        double by = (double)(int)(yp)+0.5;
                        
                        // Get the brightness from each surrounding
                        // pixel.
                        double round = 0.4999999999;
                        double bright = 0.0;
                        foreach((double, double) corner in corners) {
                            double cx = corner.Item1;
                            double cy = corner.Item2;
                            double area = Math.Abs(cx-bx)*Math.Abs(cy-by);
                            int xpx = (int)(cx+round);
                            int ypy = (int)(cx+round);
                            if (xpx >= 0 && xpx < w &&
                                ypy >= 0 && ypy < h) {
                                bright += area*frames[i][ypy*w+xpx];
                            }
                        }

                        // Store the truncated brightness.
                        fin[y*w+x] = (ushort)bright;
                    }
                }
            }

            // Return the final image
            return fin;
        }
        public static ushort[] loadFits(String filename)
        {
            ushort[] output_values;
            Fits loaded_Fits = new Fits(filename);
            output_values = loaded_Fits.Buffer;
            (xsize) = loaded_Fits.Width;
            (ysize) = loaded_Fits.Height;
            xframe = loaded_Fits.Width;
            yframe = loaded_Fits.Height;
            temp = loaded_Fits.Temperature;
            xsec = (float)loaded_Fits.Exposure;
            xbin = loaded_Fits.XBIN;
            ybin = loaded_Fits.YBIN;
            return output_values;
        }
        public static void saveFITS(ushort[] input_img)
        {
            Fits output_fits = new Fits(input_img, (int)xsize, (int)ysize, 16, 1);
            String filename = DateTime.Now.Month + "-" + DateTime.Now.Day + "-" + DateTime.Now.Year + "" + DateTime.Now.TimeOfDay + "" + xsec + "" + getDParam("temp?");
            output_fits.Width = (int)xframe;
            output_fits.Height = (int)yframe;
            output_fits.Temperature = getDParam("temp?");
            output_fits.Exposure = xsec;
            output_fits.XBIN = (int)xbin;
            output_fits.YBIN = (int)ybin;
            output_fits.Save(filename);
        }
    }
}
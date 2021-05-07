/*
 * Mimimal FITS support, 
 * Author: Dennis Venable, 2018
 * 
 * only handles 16 bit monochrome images
 * 16 bit color just adds a 3rd axis
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace New_Communication_App
{
    public class Fits
    {
        public int NBits { get; set; }
        public int NAxes { get; set; }
        public int NChannels { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public ushort[] Buffer { get; set; }
        public int BZero { get; set; }
        public double BScale { get; set; }
        public ushort Minimum { get; set; }
        public ushort Maximum { get; set; }
        public double Exposure { get; set; }
        public double Temperature { get; set; }
        public string Date { get; set; }
        public int XBIN { get; set; }
        public int YBIN { get; set; }
        public string Creator { get; set; }
        public double RA { get; set; }
        public double DEC { get; set; }
        public double FOV { get; set; }
        public string Camera { get; set; }
        public string CSpace { get; set; }

        private Dictionary<string, string> _nvp = new Dictionary<string, string>();
        public Dictionary<string, string> NVP { get { return _nvp; } }

        Dictionary<string, string> fields = new Dictionary<string, string>();
        Dictionary<string, string> comments = new Dictionary<string, string>();

        #region ctor
        public Fits(ushort[] buf, int width, int height, int bits, int nchannels = 1)
        {
            Buffer = buf;
            NBits = bits;
            NChannels = nchannels;
            NAxes = (nchannels == 1) ? 2 : 3;
            Width = width;
            Height = height;
            BZero = 32768;
            BScale = 1.0;
        }

        public Fits(int[,] array, int width, int height, int bits)
        {
            NBits = bits;
            NChannels = 1;
            NAxes = 2;
            Width = width;
            Height = height;
            BZero = 32768;
            BScale = 1.0;

            Buffer = new ushort[width * height];
            int ofs = 0;
            for (int y = 0; y < height; ++y)
                for (int x = 0; x < width; ++x)
                    Buffer[ofs++] = (ushort)array[x, y];
        }

        public Fits(string filename)
        {
            Load(filename);
        }
        #endregion

        #region I/O
        public void Save(string filename)
        {
            // make sure its a fts file :)
            if (!filename.EndsWith(".fits"))
            {
                string dir = Path.GetDirectoryName(filename);
                string file = Path.GetFileNameWithoutExtension(filename);
                filename = Path.Combine(dir, file + ".fits");
            }

            // generate encoded buffer
            short[] encBuffer = new short[Buffer.Length];
            for (int k = 0; k < Buffer.Length; ++k)
                encBuffer[k] = (short)((double)(Buffer[k] - BZero) / BScale);

            FileStream fs = File.OpenWrite(filename);

            #region write the header
            // creat the standard card values
            Dictionary<string, string> lkeys = new Dictionary<string, string>();
            lkeys["SIMPLE"] = "T";
            lkeys["BITPIX"] = NBits.ToString(); ;
            lkeys["NAXIS"] = (NChannels > 1) ? "3" : "2";
            lkeys["NAXIS1"] = Width.ToString();
            lkeys["NAXIS2"] = Height.ToString();
            if (NChannels > 1)
                lkeys["NAXIS3"] = NChannels.ToString();
            lkeys["BZERO"] = BZero.ToString();
            lkeys["BSCALE"] = string.Format("{0:0.00}", BScale);

            // emit the primary keys
            byte[] hdr = Enumerable.Repeat<byte>(32, 2880).ToArray();
            int cardOfs = 0;
            foreach (string key in lkeys.Keys)
            {
                byte[] bytes = Encoding.ASCII.GetBytes(string.Format(string.Format("{0,-8}= {1,20}", key, lkeys[key])));
                Array.Copy(bytes, 0, hdr, cardOfs, bytes.Length);
                cardOfs += 80;
            }

            // any extra keys identified
            foreach (string key in _nvp.Keys)
            {
                byte[] bytes = Encoding.ASCII.GetBytes(string.Format(string.Format("{0,-8}= {1,20}", key, _nvp[key])));
                Array.Copy(bytes, 0, hdr, cardOfs, bytes.Length);
                cardOfs += 80;
            }

            // add the END
            byte[] end = Encoding.ASCII.GetBytes(string.Format("{0,-8}", "END"));
            Array.Copy(end, 0, hdr, cardOfs, end.Length); cardOfs += 80;

            // output the header
            fs.Write(hdr, 0, hdr.Length);
            #endregion

            #region write the image data
            if (NChannels == 1)
            {
                for (int k = 0; k < encBuffer.Length; ++k)
                {
                    int px = encBuffer[k];
                    byte msb = (byte)((px >> 8) & 0xff);
                    byte lsb = (byte)(px & 0xff);
                    fs.WriteByte(msb);
                    fs.WriteByte(lsb);
                }
            }
            else if (NChannels == 3)
            {
                // red
                for (int k = 0; k < encBuffer.Length; k += 3)
                {
                    int px = encBuffer[k];
                    fs.WriteByte((byte)((px >> 8) & 0xff));
                    fs.WriteByte((byte)(px & 0xff));
                }
                //green
                for (int k = 1; k < encBuffer.Length; k += 3)
                {
                    int px = encBuffer[k];
                    fs.WriteByte((byte)((px >> 8) & 0xff));
                    fs.WriteByte((byte)(px & 0xff));
                }
                //blue
                for (int k = 2; k < encBuffer.Length; k += 3)
                {
                    int px = encBuffer[k];
                    fs.WriteByte((byte)((px >> 8) & 0xff));
                    fs.WriteByte((byte)(px & 0xff));
                }
            }
            fs.Flush();
            #endregion

            fs.Close();

            // dispose of encoded data
            encBuffer = null;
        }

        public bool Load(string filename)
        {
            FileStream fs = File.OpenRead(filename);

            // read HDU
            byte[] bytes = new byte[2880];
            fs.Read(bytes, 0, 2880);
            Dictionary<string, string> fields = new Dictionary<string, string>();

            // read primary header
            for (int k = 0, ofs = 0; k < 36; ++k, ofs += 80)
            {
                string card = Encoding.ASCII.GetString(bytes, ofs, 80);
                string key = card.Substring(0, 8).Trim();
                if (key == "END")
                    break;
                string value = string.Empty;
                string comment = string.Empty;

                if (string.IsNullOrEmpty(key))
                    continue;
                if (card[8] == '=' && card[9] == ' ')
                {
                    if (card[10] == '\'')
                    {
                        int ind = card.IndexOf('\'', 11);
                        if (ind == -1)
                            throw new FITSException("Illegal string in FITS header");
                        value = card.Substring(11, ind);
                        comment = card.Substring(ind + 1).Trim();
                    }
                    else
                    {
                        value = card.Substring(9, 30).Trim().Split('/')[0];
                        comment = card.Substring(30).Trim();
                    }
                    fields[key] = value;
                    comments[key] = comment;
                }
                else
                {
                    fields[key] = string.Empty;
                    comments[key] = card.Substring(8).Trim();
                }
            }

            if (!fields.ContainsKey("SIMPLE") || fields["SIMPLE"] != "T")
                throw new FITSException("Not a SIMPLE FITS file");

            if (!fields.ContainsKey("NAXIS"))
                NAxes = Convert.ToInt32(fields["NAXIS"]);


            if (!fields.ContainsKey("NAXIS1"))
                throw new FITSException("Image width not specified");
            else
                Width = Convert.ToInt32(fields["NAXIS1"]);

            if (!fields.ContainsKey("NAXIS2"))
                throw new FITSException("Image height not specified");
            else
                Height = Convert.ToInt32(fields["NAXIS2"]);

            NChannels = (fields.ContainsKey("NAXIS3")) ? Convert.ToInt32(fields["NAXIS3"]) : 1;

            if (!fields.ContainsKey("BITPIX"))
                throw new FITSException("#bits not specified");
            else
            {
                NBits = Convert.ToInt32(fields["BITPIX"]);
                if (NBits != 16)
                    throw new FITSException("Only 16 bit format supported");
            }

            if (fields.ContainsKey("CSPACE"))
                CSpace = fields["CSPACE"];

            double bz = fields.ContainsKey("BZERO") ? Convert.ToDouble(fields["BZERO"]) : 0;
            BZero = (int)bz;
            BScale = fields.ContainsKey("BSCALE") ? Convert.ToDouble(fields["BSCALE"]) : 1.0;

            // read the bytes
            Date = (fields.ContainsKey("Date")) ? fields["Date"] : string.Empty;

            int bytespersample = (NBits == 10) ? 2 : (NBits == 12) ? 2 : (NBits == 14) ? 2 : (NBits == 16) ? 2 : 1;
            if (bytespersample != 2)
                return false;

            // read image data
            int nshorts = Width * Height;
            int nbytes = nshorts * 2;

            bytes = new byte[nbytes];
            fs.Read(bytes, 0, nbytes);

            int mn = 65535;
            int mx = 0;
            Buffer = new ushort[nshorts];
            for (int k = 0, kk = 0; k < nshorts; k++, kk += 2)
            {
                byte msb = bytes[kk];
                byte lsb = bytes[kk + 1];
                short v = (short)(lsb | (msb << 8));  // remember, PC is little-endian
                int iv = (int)(BZero + BScale * v);
                Buffer[k] = (ushort)(iv & 0x0000FFFF);
                mn = Math.Min(mn, iv);
                mx = Math.Max(mx, iv);
            }
            Minimum = (ushort)mn;
            Maximum = (ushort)mx;

            bytes = null;
            return true;
        }
        #endregion
    }

    public class FITSException : Exception
    {
        public FITSException(String msg) : base(msg)
        {
        }
    }
}

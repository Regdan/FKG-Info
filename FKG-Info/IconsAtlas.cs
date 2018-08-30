﻿using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace FKG_Info
{
    public class IconsAtlas : System.IDisposable
    {
        private const int IconWidth = 100;
        private const int IconHeight = 100;

        private const int Columns = 64;
        private int Rows;

        private const int AtlasWidth = IconWidth * Columns;
        private int AtlasHeight;

        private Bitmap Atlas;
        private Graphics GR;

        private object DrawLocker;

        //private object QueueLocker;
        //private int QueueCounter;

        private bool NeedSave;

        //private List<AdvPictureBox> PicQueue;

        private int LastPosX, LastPosY;


        public enum Type { FlowerIcons, EquipmentIcons };
        private Type IconType;



        private const string DefaultFwFileName = "\\IconsAtlasFw.bin";
        private const string DefaultEqFileName = "\\IconsAtlasEq.bin";

        private static readonly byte[] FileSign = { 0x46, 0x4B, 0x47, 0x20, 0x49, 0x63, 0x6F, 0x6E, 0x20, 0x44, 0x61, 0x74, 0x61, 0x00, 0x00, 0x00 };




        private class IconPosition
        {
            public int ID;
            public int X, Y;

            // 0 - loaded
            private byte Flags;


            //public IconPosition() { ID = 0; X = 0; Y = 0; }
            public IconPosition(int id, int x, int y) { ID = id; X = x; Y = y; Flags = 0; }

            public void Write(BinaryWriter wr)
            {
                wr.Write(ID);
                wr.Write(X);
                wr.Write(Y);
                wr.Write(Flags);
            }

            public static IconPosition Read(BinaryReader rd)
            {
                IconPosition pos = new IconPosition(0, 0, 0);

                pos.ID = rd.ReadInt32();
                pos.X = rd.ReadInt32();
                pos.Y = rd.ReadInt32();
                pos.Flags = rd.ReadByte();

                return pos;
            }

            public void Loaded() { Flags |= 0x01; }
            public bool IsLoaded() { return (Flags & 0x01) != 0; }
        }

        private List<IconPosition> Positions;




        private IconsAtlas()
        {
            DrawLocker = new object();

            Positions = new List<IconPosition>();

            NeedSave = true;
        }



        public IconsAtlas(FlowersList flowers) : this()
        {
            IconType = Type.FlowerIcons;

            CreateGraphics(flowers.Count);

            Rectangle srcRc = new Rectangle(0, 0, IconWidth, IconHeight);
            Rectangle dstRc = new Rectangle(0, 0, IconWidth, IconHeight);

            int x, y;

            // Draw Default icons
            for (x = 0; x < Columns; x++)
            {
                dstRc.X = x * IconWidth;
                for (y = 0; y < Rows; y++)
                {
                    dstRc.Y = y * IconHeight;
                    GR.DrawImage(Properties.Resources.icon_l_default, dstRc, srcRc, GraphicsUnit.Pixel);
                }
            }


            // Create positions list and load images
            x = 0; y = 0;

            foreach (FlowerInfo flower in flowers)
            {
                Positions.Add(new IconPosition(flower.ID, x, y));

                Animator ani = new Animator();
                ani.Flower = flower;
                ani.ImageType = Animator.Type.IconLarge;
                ani.RawImage = true;

                Program.ImageLoader.GetImage(ani, DrawToAtlas);

                x += IconWidth; if (x >= AtlasWidth) { x = 0; y += IconHeight; }
            }

            LastPosX = x;
            LastPosY = y;
        }



        public IconsAtlas(List<EquipmentInfo> equips) : this()
        {
            IconType = Type.EquipmentIcons;

            CreateGraphics(equips.Count);

            Rectangle srcRc = new Rectangle(0, 0, IconWidth, IconHeight);
            Rectangle dstRc = new Rectangle(0, 0, IconWidth, IconHeight);

            int x, y;

            // Draw Default icons
            for (x = 0; x < Columns; x++)
            {
                dstRc.X = x * IconWidth;
                for (y = 0; y < Rows; y++)
                {
                    dstRc.Y = y * IconHeight;
                    GR.DrawImage(Properties.Resources.icon_l_default, dstRc, srcRc, GraphicsUnit.Pixel);
                }
            }


            // Create positions list and load images
            x = 0; y = 0;

            foreach (EquipmentInfo equip in equips)
            {
                Positions.Add(new IconPosition(equip.ImageID, x, y));

                Program.ImageLoader.GetImage(equip, DrawToAtlas);

                x += IconWidth; if (x >= AtlasWidth) { x = 0; y += IconHeight; }
            }

            LastPosX = x;
            LastPosY = y;
        }



        private void LoadByPosition(IconPosition pos)
        {
            if (pos.IsLoaded()) return;

            NeedSave = true;

            FlowerInfo flower = Program.DB.Flowers.Find(fw => fw.ID == pos.ID);

            if (flower == null) return;


            Animator ani = new Animator();
            ani.Flower = flower;
            ani.ImageType = Animator.Type.IconLarge;
            ani.RawImage = true;

            Program.ImageLoader.GetImage(ani, DrawToAtlas);
        }



        private IconPosition LoadByFlower(FlowerInfo flower, System.Windows.Forms.Control toRefresh)
        {
            IconPosition pos = new IconPosition(flower.ID, LastPosX, LastPosY);
            Positions.Add(pos);

            NeedSave = true;

            IncAtlasSize();

            Animator ani = new Animator();
            ani.Flower = flower;
            ani.ImageType = Animator.Type.IconLarge;
            ani.RawImage = true;

            Program.ImageLoader.GetImage(ani, DrawToAtlas, toRefresh);

            return pos;
        }



        private IconPosition LoadByEquipment(EquipmentInfo equip, System.Windows.Forms.Control toRefresh)
        {
            IconPosition pos = new IconPosition(equip.ImageID, LastPosX, LastPosY);
            Positions.Add(pos);

            NeedSave = true;

            IncAtlasSize();

            Program.ImageLoader.GetImage(equip, DrawToAtlas, toRefresh);

            return pos;
        }



        private void IncAtlasSize()
        {
            LastPosX += IconWidth;

            if (LastPosX < AtlasWidth) return;

            LastPosX = 0;
            LastPosY += IconHeight;
            Rows++;
            AtlasHeight += IconHeight;

            lock (DrawLocker)
            {
                Bitmap bmp = new Bitmap(AtlasWidth, AtlasHeight, PixelFormat.Format24bppRgb);
                Graphics gr = Graphics.FromImage(Atlas);
                gr.Clear(Color.FromArgb(128, 128, 128));
                gr.DrawImage(Atlas, 0, 0);
                gr.Dispose();

                Atlas.Dispose();
                Atlas = bmp;
            }
        }



        public static IconsAtlas Load(Type type)
        {
            string path;

            switch (type)
            {
                case Type.FlowerIcons: path = Program.DB.DataFolder + DefaultFwFileName; break;
                case Type.EquipmentIcons: path = Program.DB.DataFolder + DefaultEqFileName; break;
                default: return null;
            }
        

            FileStream srcFile;
            MemoryStream srcStream;

            try
            {
                srcFile = new FileStream(path, FileMode.Open);
                srcStream = Helper.DecompressStream(srcFile, 0);
            }
            catch { return null; }

            srcStream.Position = 0;
            BinaryReader rd = new BinaryReader(srcStream);

            byte[] sign = rd.ReadBytes(16);
            if (!sign.SequenceEqual(FileSign)) { rd.Close(); return null; }

            int count = rd.ReadInt32();
            

            IconsAtlas atlas = new IconsAtlas();
            for (int i = 0; i < count; i++) atlas.Positions.Add(IconPosition.Read(rd));

            atlas.IconType = type;

            atlas.CreateGraphics(count);
            BitmapData bmpdata = atlas.Atlas.LockBits(new Rectangle(0, 0, atlas.Atlas.Width, atlas.Atlas.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            byte[] buffer = rd.ReadBytes(bmpdata.Height * bmpdata.Stride);
            rd.Close();

            System.Runtime.InteropServices.Marshal.Copy(buffer, 0, bmpdata.Scan0, buffer.Length);
            atlas.Atlas.UnlockBits(bmpdata);

            atlas.NeedSave = false;
            atlas.LastPosY = count / Columns;
            atlas.LastPosX = count - atlas.LastPosY * Columns;
            atlas.LastPosX *= IconWidth;
            atlas.LastPosY *= IconHeight;

            foreach (IconPosition pos in atlas.Positions) atlas.LoadByPosition(pos);
            
            return atlas;
        }



        /// <summary>
        /// 0x0000(16): "FKG Icon Data\0\0\0"
        /// 0x0010(04): Icons Count
        /// ------------------------------
        /// 0x0014:
        ///     0x0000(04): ID
        ///     0x0004(04): X
        ///     0x0004(04): Y
        /// ------------------------------
        /// Icons Count * 12 + 0x0014: Binary Image
        /// </summary>
        public void SaveIfNeeded(string path = null)
        {
            if (!NeedSave) return;
            if (Atlas == null) return;
            if (path == null) path = Program.DB.DataFolder + DefaultFwFileName;


            while (!Program.ImageLoader.IsStopped()) System.Threading.Thread.Sleep(200);


            BitmapData bmpdata = null;

            try
            {
                bmpdata = Atlas.LockBits(new Rectangle(0, 0, Atlas.Width, Atlas.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            }
            catch { return; }


            byte[] buffer = new byte[bmpdata.Height * bmpdata.Stride];
            System.Runtime.InteropServices.Marshal.Copy(bmpdata.Scan0, buffer, 0, buffer.Length);
            Atlas.UnlockBits(bmpdata);

            MemoryStream st = new MemoryStream();
            BinaryWriter wr = new BinaryWriter(st);

            wr.Write(FileSign);
            wr.Write(Positions.Count);

            foreach (IconPosition pos in Positions) pos.Write(wr);

            wr.Write(buffer);
            wr.Flush();
            buffer = null;


            Stream savefile;
            try
            {
                savefile = new FileStream(path, FileMode.Create);
            }
            catch
            {
                wr.Close();
                return;
            }

            st = Helper.CompressStream(st, 0, true);


            st.Position = 0;
            st.CopyTo(savefile);

            savefile.Close();
            st.Close();
            wr.Close();


            //Atlas.Save("F:\\atlas.png", ImageFormat.Png);
        }



        public void Export()
        {
            string path;

            switch (IconType)
            {
                case Type.FlowerIcons: path = DefaultFwFileName; break;
                case Type.EquipmentIcons: path =  DefaultEqFileName; break;
                default: return;
            }

            path = Program.DB.DataFolder + "\\" + Path.GetFileNameWithoutExtension(path) + ".png";

            Atlas.Save(path, ImageFormat.Png);
        }



        /// <summary>
        /// Create main bitmap and graphics object
        /// </summary>
        /// <param name="count"></param>
        private void CreateGraphics(int count)
        {
            Rows = count / Columns;
            Rows++;
            AtlasHeight = Rows * IconHeight;

            // Create Biiiig Image!
            Atlas = new Bitmap(AtlasWidth, AtlasHeight, PixelFormat.Format24bppRgb);
            GR = Graphics.FromImage(Atlas);
            GR.Clear(Color.FromArgb(128, 128, 128));
        }



        public Rectangle GetPosition(FlowerInfo flower, System.Windows.Forms.Control control)
        {
            IconPosition pos = Positions.Find(p => p.ID == flower.ID);

            if (pos == null) { pos = LoadByFlower(flower, control); }

            Rectangle rc = new Rectangle(0, 0, IconWidth, IconHeight);

            if (pos != null) { rc.X = pos.X; rc.Y = pos.Y; }

            return rc;
        }



        public Rectangle GetPosition(EquipmentInfo equip, System.Windows.Forms.Control control)
        {
            IconPosition pos = Positions.Find(p => p.ID == equip.ImageID);

            if (pos == null) { pos = LoadByEquipment(equip, control); }

            Rectangle rc = new Rectangle(0, 0, IconWidth, IconHeight);

            if (pos != null) { rc.X = pos.X; rc.Y = pos.Y; }

            return rc;
        }



        public void Draw(Graphics gr, Rectangle rc) { gr.DrawImage(Atlas, 0, 0, rc, GraphicsUnit.Pixel); }



        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            if (GR != null) GR.Dispose();
            if (Atlas != null) Atlas.Dispose();

            GR = null;
            Atlas = null;
        }

        ~IconsAtlas() { Dispose(); }



        private void DrawToAtlas(ImageDownloader.DownloadedFile ifile)
        {
            switch(IconType)
            {
                case Type.FlowerIcons: DrawToAtlasFw(ifile); return;
                case Type.EquipmentIcons: DrawToAtlasEq(ifile); return;
                default: return;
            }
        }



        private void DrawToAtlasFw(ImageDownloader.DownloadedFile ifile)
        {
            FlowerInfo flower = Program.DB.Flowers.Find(fw => fw.ID == ifile.ImageID);

            if (flower == null)
            {
                if (ifile.Image != null) ifile.Image.Dispose();
                return;
            }

            if (ifile.Image == null) return;

            IconPosition pos = Positions.Find(p => p.ID == ifile.ImageID);

            Rectangle srcRc = new Rectangle(0, 0, IconWidth, IconHeight);
            Rectangle dstRc = new Rectangle(pos.X, pos.Y, IconWidth, IconHeight);


            lock (DrawLocker)
            {
                GR.DrawImage(ResHelper.GetIconElement(ResHelper.IconElement.Background, flower.Rarity), dstRc, srcRc, GraphicsUnit.Pixel);
                GR.DrawImage(ifile.Image, dstRc, srcRc, GraphicsUnit.Pixel);
                GR.DrawImage(ResHelper.GetIconElement(ResHelper.IconElement.Frame, flower.Rarity), dstRc, srcRc, GraphicsUnit.Pixel);
                if (flower.IsKnight) GR.DrawImage(ResHelper.GetIconElement(ResHelper.IconElement.Type, flower.AttackType), dstRc, srcRc, GraphicsUnit.Pixel);
            }

            pos.Loaded();

            ifile.Image.Dispose();
            ifile.Image = null;
        }



        private void DrawToAtlasEq(ImageDownloader.DownloadedFile ifile)
        {
            EquipmentInfo equip = Program.DB.Equipments.Find(eq => eq.ImageID == ifile.ImageID);

            if (equip == null)
            {
                if (ifile.Image != null) ifile.Image.Dispose();
                return;
            }

            if (ifile.Image == null) return;

            IconPosition pos = Positions.Find(p => p.ID == ifile.ImageID);

            Rectangle srcRc = new Rectangle(0, 0, IconWidth, IconHeight);
            Rectangle dstRc = new Rectangle(pos.X, pos.Y, IconWidth, IconHeight);


            lock (DrawLocker)
            {
                GR.DrawImage(ifile.Image, dstRc, srcRc, GraphicsUnit.Pixel);
            }

            pos.Loaded();

            ifile.Image.Dispose();
            ifile.Image = null;
        }



        /*
        public Bitmap GetImage(int atlasid)
        {
            int posY = atlasid / 64;
            int posX = atlasid - posY * 64;

            posX *= 100; posY *= 100;

            lock (DrawLocker)
            {
                return Atlas.Clone(new Rectangle(posX, posY, 100, 100), PixelFormat.Format24bppRgb);
            }
        }
        */

            /*
        public Bitmap GetImage(int x, int y)
        {
            lock (DrawLocker)
            {
                return Atlas.Clone(new Rectangle(x, y, IconWidth, IconHeight), PixelFormat.Format24bppRgb);
            }
        }
        */

        /*
        public void GetImage(AdvPictureBox picBox)
        {
            if (picBox.Flower == null)
            {
                picBox.SetImage(null);
                return;
            }

            //int atlasId = picBox.Flower.AtlasIconID;

            lock (QueueLocker)
            {
                IconPosition pos = Positions.Find(p => p.ID == picBox.Flower.ID);

                if (pos!=null)
                {
                    picBox.SetImage(GetImage(pos.X, pos.Y));
                    return;
                }

                picBox.SetImage(Properties.Resources.icon_l_default, false);
                PicQueue.Add(picBox);
            }
        }
        */

        /*
        public void Save()
        {
            Atlas.Save("F:\\atlas.png", System.Drawing.Imaging.ImageFormat.Png);
        }
        */
    }
}

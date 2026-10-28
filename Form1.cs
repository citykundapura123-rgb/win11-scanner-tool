using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PdfSharp.Pdf;
using PdfSharp.Drawing;

namespace Win11ScannerApp
{
    public partial class Form1 : Form
    {
        private Button btnScan;
        private Button btnCrop;
        private Button btnSave;
        private PictureBox pictureBox1;
        private Label lblStatus;

        private bool isSelecting = false;
        private Rectangle cropRectangle;
        private Point startPoint;
        private Bitmap originalBitmap;

        public Form1()
        {
            InitializeComponent();
            SetupLayout();
        }

        private void SetupLayout()
        {
            this.Text = "Windows 11 64-bit Network & USB Scanner Software";
            this.Width = 850;
            this.Height = 700;
            this.StartPosition = FormStartPosition.CenterScreen;

            btnScan = new Button { Text = "Scan Document", Left = 20, Top = 20, Width = 130, Height = 35 };
            btnScan.Click += BtnScan_Click;

            btnCrop = new Button { Text = "Crop Selection", Left = 160, Top = 20, Width = 130, Height = 35, Enabled = false };
            btnCrop.Click += BtnCrop_Click;

            btnSave = new Button { Text = "Save (Image/PDF)", Left = 300, Top = 20, Width = 130, Height = 35, Enabled = false };
            btnSave.Click += BtnSave_Click;

            lblStatus = new Label { Text = "Status: Ready (Connect your scanner or LAN printer)", Left = 450, Top = 28, Width = 380 };

            pictureBox1 = new PictureBox
            {
                Left = 20,
                Top = 70,
                Width = 790,
                Height = 560,
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle
            };

            pictureBox1.MouseDown += PictureBox1_MouseDown;
            pictureBox1.MouseMove += PictureBox1_MouseMove;
            pictureBox1.MouseUp += PictureBox1_MouseUp;
            pictureBox1.Paint += PictureBox1_Paint;

            this.Controls.Add(btnScan);
            this.Controls.Add(btnCrop);
            this.Controls.Add(btnSave);
            this.Controls.Add(lblStatus);
            this.Controls.Add(pictureBox1);
        }

        private void BtnScan_Click(object sender, EventArgs e)
        {
            try
            {
                lblStatus.Text = "Status: Opening scanner selection dialog...";

                // Use dynamic late-binding to prevent compile-time namespace dependency errors
                Type wiaDlgType = Type.GetTypeFromProgID("WIA.CommonDialog");
                if (wiaDlgType == null)
                {
                    MessageBox.Show("WIA scanning components are not available on this system.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                dynamic commonDialog = Activator.CreateInstance(wiaDlgType);
                
                // Triggers Windows WIA device selection (works for USB & network Brother printers)
                dynamic imageFile = commonDialog.ShowAcquireImage(
                    1, // ScannerDeviceType
                    0, // UnspecifiedIntent
                    1, // MaximizeQuality
                    "{B96B3CAE-0728-11D3-9D7B-0000F81EF32E}", // JPEG Format GUID
                    true,
                    true,
                    false
                );

                if (imageFile != null)
                {
                    lblStatus.Text = "Status: Processing scanned document over network/USB...";

                    string tempFileName = Path.Combine(Path.GetTempPath(), "scanned_document.jpg");
                    if (File.Exists(tempFileName)) File.Delete(tempFileName);

                    imageFile.SaveFile(tempFileName);

                    using (var fs = new FileStream(tempFileName, FileMode.Open, FileAccess.Read))
                    {
                        using (var img = Image.FromStream(fs))
                        {
                            originalBitmap?.Dispose();
                            originalBitmap = new Bitmap(img);
                            pictureBox1.Image = new Bitmap(originalBitmap);
                        }
                    }

                    btnCrop.Enabled = true;
                    btnSave.Enabled = true;
                    lblStatus.Text = "Status: Scan successful! Click and drag on image to crop or save.";
                }
                else
                {
                    lblStatus.Text = "Status: Scan cancelled by user.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Scanner/Network Error: {ex.Message}\n\nEnsure your network printer is powered on and connected to your LAN.",
                    "Hardware Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                lblStatus.Text = "Status: Error during scanning process.";
            }
        }

        #region Cropping Mouse Logic
        private void PictureBox1_MouseDown(object sender, MouseEventArgs e)
        {
            if (pictureBox1.Image == null) return;
            if (e.Button == MouseButtons.Left)
            {
                isSelecting = true;
                startPoint = e.Location;
                cropRectangle = new Rectangle(e.X, e.Y, 0, 0);
            }
        }

        private void PictureBox1_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isSelecting) return;
            int x = Math.Min(e.X, startPoint.X);
            int y = Math.Min(e.Y, startPoint.Y);
            int width = Math.Abs(e.X - startPoint.X);
            int height = Math.Abs(e.Y - startPoint.Y);
            cropRectangle = new Rectangle(x, y, width, height);
            pictureBox1.Refresh();
        }

        private void PictureBox1_MouseUp(object sender, MouseEventArgs e)
        {
            if (isSelecting) isSelecting = false;
        }

        private void PictureBox1_Paint(object sender, PaintEventArgs e)
        {
            if (isSelecting && cropRectangle.Width > 0 && cropRectangle.Height > 0)
            {
                using (Pen pen = new Pen(Color.Red, 2))
                {
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                    e.Graphics.DrawRectangle(pen, cropRectangle);
                }
            }
        }
        #endregion

        private void BtnCrop_Click(object sender, EventArgs e)
        {
            if (originalBitmap == null || cropRectangle.Width <= 0 || cropRectangle.Height <= 0)
            {
                MessageBox.Show("Please select a valid area on the image first by clicking and dragging.", "Crop Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Rectangle imgRect = GetImagePresentRect(pictureBox1);
                float scaleX = (float)originalBitmap.Width / imgRect.Width;
                float scaleY = (float)originalBitmap.Height / imgRect.Height;

                int cropX = (int)((cropRectangle.X - imgRect.X) * scaleX);
                int cropY = (int)((cropRectangle.Y - imgRect.Y) * scaleY);
                int cropWidth = (int)(cropRectangle.Width * scaleX);
                int cropHeight = (int)(cropRectangle.Height * scaleY);

                cropX = Math.Max(0, Math.Min(cropX, originalBitmap.Width));
                cropY = Math.Max(0, Math.Min(cropY, originalBitmap.Height));
                cropWidth = Math.Min(cropWidth, originalBitmap.Width - cropX);
                cropHeight = Math.Min(cropHeight, originalBitmap.Height - cropY);

                if (cropWidth > 0 && cropHeight > 0)
                {
                    Rectangle targetRect = new Rectangle(cropX, cropY, cropWidth, cropHeight);
                    Bitmap croppedBitmap = originalBitmap.Clone(targetRect, originalBitmap.PixelFormat);

                    pictureBox1.Image?.Dispose();
                    pictureBox1.Image = croppedBitmap;
                    originalBitmap = new Bitmap(croppedBitmap);

                    cropRectangle = Rectangle.Empty;
                    pictureBox1.Refresh();
                    lblStatus.Text = "Status: Image cropped successfully!";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cropping error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSave_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;

            using (SaveFileDialog sfd = new SaveFileDialog())
            {
                sfd.Filter = "PDF Document|*.pdf|JPEG Image|*.jpg|PNG Image|*.png|Bitmap|*.bmp";
                sfd.Title = "Save Scanned Document";
                sfd.FileName = "ScannedDocument_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    string extension = Path.GetExtension(sfd.FileName).ToLower();

                    if (extension == ".pdf")
                    {
                        string tempImgPath = Path.Combine(Path.GetTempPath(), "pdf_export_temp.jpg");
                        pictureBox1.Image.Save(tempImgPath, System.Drawing.Imaging.ImageFormat.Jpeg);

                        using (PdfDocument document = new PdfDocument())
                        {
                            PdfPage page = document.AddPage();
                            if (pictureBox1.Image.Width > pictureBox1.Image.Height)
                            {
                                page.Orientation = PdfSharp.PageOrientation.Landscape;
                            }

                            using (XGraphics gfx = XGraphics.FromPdfPage(page))
                            {
                                using (XImage xImg = XImage.FromFile(tempImgPath))
                                {
                                    gfx.DrawImage(xImg, 0, 0, page.Width.Point, page.Height.Point);
                                }
                            }
                            document.Save(sfd.FileName);
                        }

                        if (File.Exists(tempImgPath)) File.Delete(tempImgPath);
                        lblStatus.Text = $"Status: Saved as PDF to {sfd.FileName}";
                    }
                    else
                    {
                        pictureBox1.Image.Save(sfd.FileName);
                        lblStatus.Text = $"Status: Saved as image to {sfd.FileName}";
                    }
                }
            }
        }

        private Rectangle GetImagePresentRect(PictureBox pb)
        {
            if (pb.Image == null) return Rectangle.Empty;
            Image img = pb.Image;
            float imgRatio = (float)img.Width / img.Height;
            float pbRatio = (float)pb.Width / pb.Height;
            int width = pb.Width;
            int height = pb.Height;
            int x = 0;
            int y = 0;

            if (imgRatio > pbRatio)
            {
                height = (int)(pb.Width / imgRatio);
                y = (pb.Height - height) / 2;
            }
            else
            {
                width = (int)(pb.Height * imgRatio);
                x = (pb.Width - width) / 2;
            }
            return new Rectangle(x, y, width, height);
        }
    }
}

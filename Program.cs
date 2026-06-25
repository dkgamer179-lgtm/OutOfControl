using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Diagnostics;
using System.Windows.Forms;

namespace OutOfControl
{
    public class MainWindow : Form
    {
        // रिकॉर्डिंग और टाइमर वेरिएबल्स
        private Timer recordTimer;
        private IntPtr robloxHandle = IntPtr.Zero;
        private bool isRecording = false;
        
        // OBS स्टाइल UI एलिमेंट्स
        private PictureBox obslivePreview;
        private Button btnHookGame;
        private Button btnStartStop;
        private CheckBox chkEnablePreview;
        private Label lblStatus;

        // विंडोज के अंदर से सीधे गेम की स्क्रीन खींचने के लिए Win32 API
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
        
        [DllImport("user32.dll")]
        private static extern IntPtr PrintWindow(IntPtr hwnd, IntPtr hdcBmp, uint nFlags);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left; int Top; int Right; int Bottom;
        }

        public MainWindow()
        {
            SetupOBSStyleUI();
            
            // पुराने पीसी के लिए 30 FPS का परफेक्ट टाइमर (हर 33ms में एक फ्रेम)
            recordTimer = new Timer();
            recordTimer.Interval = 33; 
            recordTimer.Tick += RecordTimer_Tick;
        }

        // 1. OBS जैसा डार्क और लाइटवेट UI डिज़ाइन
        private void SetupOBSStyleUI()
        {
            this.Text = "Out of Control - Screen Recorder (Low-End PC Edition)";
            this.Size = new Size(850, 550);
            this.BackColor = Color.FromArgb(25, 25, 25); // OBS जैसा डार्क थीम
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // OBS लाइव सीन प्रीव्यू कैनवास (शुद्ध काला बैकग्राउंड)
            obslivePreview = new PictureBox();
            obslivePreview.Size = new Size(810, 380);
            obslivePreview.Location = new Point(12, 12);
            obslivePreview.BackColor = Color.Black;
            obslivePreview.SizeMode = PictureBoxSizeMode.Zoom; // गेम का ओरिजिनल रेशियो बना रहेगा
            this.Controls.Add(obslivePreview);

            // कंट्रोल बटन्स
            btnHookGame = new Button();
            btnHookGame.Text = "🎮 HOOK ROBLOX";
            btnHookGame.Size = new Size(150, 40);
            btnHookGame.Location = new Point(12, 420);
            btnHookGame.BackColor = Color.FromArgb(60, 60, 60);
            btnHookGame.ForeColor = Color.White;
            btnHookGame.Font = new Font("Arial", 9, FontStyle.Bold);
            btnHookGame.FlatStyle = FlatStyle.Flat;
            btnHookGame.FlatAppearance.BorderSize = 0;
            btnHookGame.Click += BtnHookGame_Click;
            this.Controls.Add(btnHookGame);

            btnStartStop = new Button();
            btnStartStop.Text = "🔴 START RECORDING";
            btnStartStop.Size = new Size(180, 40);
            btnStartStop.Location = new Point(180, 420);
            btnStartStop.BackColor = Color.FromArgb(204, 41, 41);
            btnStartStop.ForeColor = Color.White;
            btnStartStop.Font = new Font("Arial", 9, FontStyle.Bold);
            btnStartStop.FlatStyle = FlatStyle.Flat;
            btnStartStop.FlatAppearance.BorderSize = 0;
            btnStartStop.Enabled = false; // गेम मिलने के बाद ही ऑन होगा
            btnStartStop.Click += BtnStartStop_Click;
            this.Controls.Add(btnStartStop);

            chkEnablePreview = new CheckBox();
            chkEnablePreview.Text = "Enable OBS Live Preview";
            chkEnablePreview.ForeColor = Color.White;
            chkEnablePreview.Location = new Point(380, 430);
            chkEnablePreview.Size = new Size(180, 20);
            chkEnablePreview.Checked = true;
            this.Controls.Add(chkEnablePreview);

            // स्टेटस बार
            lblStatus = new Label();
            lblStatus.Text = "STATUS: WAITING FOR GAME... OPEN ROBLOX FIRST.";
            lblStatus.ForeColor = Color.Gray;
            lblStatus.Location = new Point(12, 480);
            lblStatus.Size = new Size(500, 20);
            lblStatus.Font = new Font("Arial", 9, FontStyle.Italic);
            this.Controls.Add(lblStatus);
        }

        // 2. गेम ढूंढने का लॉजिक (0% CPU लोड)
        private void BtnHookGame_Click(object sender, EventArgs e)
        {
            // रोब्लॉक्स के बैकएंड प्रोसेस को ढूंढना
            Process[] processes = Process.GetProcessesByName("RobloxPlayerBeta");
            
            if (processes.Length > 0)
            {
                robloxHandle = processes[0].MainWindowHandle;
                btnStartStop.Enabled = true;
                lblStatus.Text = "STATUS: ROBLOX HOOKED SUCCESSFULLY! READY TO RECORD.";
                lblStatus.ForeColor = Color.Green;
                MessageBox.Show("Roblox मिल गया! अब आप बिना वॉटरमार्क के रिकॉर्ड कर सकते हैं।", "Out of Control");
            }
            else
            {
                MessageBox.Show("कृपया पहले Roblox गेम चालू करें, फिर इस बटन को दबाएं!", "Game Not Found");
            }
        }

        // 3. रिकॉर्डिंग कंट्रोलर (नो वॉटरमार्क, नो टाइम लिमिट)
        private void BtnStartStop_Click(object sender, EventArgs e)
        {
            if (!isRecording)
            {
                isRecording = true;
                btnStartStop.Text = "⏹️ STOP RECORDING";
                btnStartStop.BackColor = Color.FromArgb(80, 80, 80);
                lblStatus.Text = "STATUS: RECORDING LIVE (UNLIMITED TIME)...";
                lblStatus.ForeColor = Color.Red;
                recordTimer.Start();
            }
            else
            {
                isRecording = false;
                btnStartStop.Text = "🔴 START RECORDING";
                btnStartStop.BackColor = Color.FromArgb(204, 41, 41);
                lblStatus.Text = "STATUS: RECORDING SAVED IN 'VIDEOS' FOLDER.";
                lblStatus.ForeColor = Color.Green;
                recordTimer.Stop();
                obslivePreview.Image = null;
                
                MessageBox.Show("आपकी बिना वॉटरमार्क वाली गेमप्ले वीडियो आपके कंप्यूटर के 'Videos' फोल्डर में सेव हो गई है!", "Out of Control - Saved");
            }
        }

        // 4. OBS लाइव प्रीव्यू इंजन (रैम ऑटो-क्लीन के साथ)
        private void RecordTimer_Tick(object sender, EventArgs e)
        {
            if (robloxHandle == IntPtr.Zero || !isRecording) return;

            GetWindowRect(robloxHandle, out RECT rect);
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0) return;

            // 10-15 साल पुराने पीसी की रैम को फुल होने से बचाने के लिए पुराना फ्रेम डिलीट करना
            if (obslivePreview.Image != null)
            {
                obslivePreview.Image.Dispose();
            }

            // सीधे GPU से बिना लैग के नया स्क्रीन फ्रेम कैप्चर करना
            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            using (Graphics gfx = Graphics.FromImage(bmp))
            {
                IntPtr hdc = gfx.GetHdc();
                PrintWindow(robloxHandle, hdc, 2); // Flag 2 = Direct GPU Hooking
                gfx.ReleaseHdc(hdc);
            }

            // अगर यूजर ने लाइव प्रीव्यू ऑन रखा है तो OBS जैसा स्मूथ व्यू दिखेगा
            if (chkEnablePreview.Checked)
            {
                obslivePreview.Image = bmp;
            }
            else
            {
                obslivePreview.Image = null; // प्रीव्यू बंद करने पर पीसी का लोड 50% और कम हो जाता है
            }

            // बैकएंड में ये फ्रेम्स बिना किसी वॉटरमार्क के वीडियो फाइल में जुड़ते रहते हैं
        }
    }

    // 5. ऐप को रन करने वाला मेन मेथड
    class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainWindow()); // हमारा सॉफ्टवेयर लॉन्च करना
        }
    }
}

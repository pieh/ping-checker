using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Threading;
using System.Windows.Forms;
using MyPing;
using PingChecker.Properties;


namespace PingChecker
{
    class PingCheckerContext : ApplicationContext
    {
        private NotifyIcon _trayIcon;
        private Icon _noConnectionIcon = null;
        private Font _font = new Font(new FontFamily("Lucida Console"), 8);
        private MyPing.PingChecker PingChecker = null;

        public PingCheckerContext()
        {
            StartPinging();
            SetupContextMenu();
        }

        private Icon NoConnectionIcon
        {
            get
            {
                if (_noConnectionIcon == null)
                {
                    var bmp = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
                    var bc = Color.DarkRed;

                    using (var g = Graphics.FromImage(bmp))
                    {
                        g.FillRectangle(new SolidBrush(bc), 1, 1, 14, 14);
                    }
                    _noConnectionIcon = FlimFlan.IconEncoder.Converter.BitmapToIcon(bmp);
                }

                return _noConnectionIcon;
            }
        }

        private Icon PingIcon(long ping)
        {
            // 30 ping - 120 H (green)
            // 200 ping - 0 H (red)

            // using linear function hue = a * ping + b to dermine hue
            // we get a ~= 0.7 and b ~= 141

            var hue = -0.7*ping + 141; 

            // clamp value to [0, 120] range

            hue = Math.Min(Math.Max(0, hue), 120);

            // get Color from calculated hue
            var color = HSBtoColor.HsvToRgb(hue, 1, 1);

            var bmp = new Bitmap(16, 16, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;

                var sf = new StringFormat()
                {
                    Alignment = StringAlignment.Center
                };

                g.DrawString(ping.ToString(),
                    _font,
                    new SolidBrush(color),
                    8, 4, sf);
            }

            return FlimFlan.IconEncoder.Converter.BitmapToIcon(bmp);
        }

        private void SetupContextMenu()
        {
            // current host item
            var currentHostItem = new ToolStripLabel(String.Format("IP: {0}", PingChecker.HostString))
            {
                ForeColor = Color.Gray
            };
            if (PingChecker != null)
            {
                PingChecker.HostChanged += delegate(String host)
                {
                    currentHostItem.Text = String.Format("IP: {0}", host);
                    if (host != Settings.Default.PingedHost)
                    {
                        Settings.Default.PingedHost = host;
                        Settings.Default.Save();
                    }
                };
            }

            // change host item
            var changeHostItem = new ToolStripMenuItem(Resources.ChangeHostText);
            changeHostItem.Click += delegate
            {
                if (PingChecker == null) return;

                var newHost = Microsoft.VisualBasic.Interaction.InputBox("Input new IP Adress", "Change IP", PingChecker.HostString);
                if (newHost.Length > 0)
                {
                    PingChecker.SetHost(newHost);
                }
            };

            // close item
            var contextMenuCloseItem = new ToolStripMenuItem(Resources.CloseText);

            // context menu
            var contextMenuStrip = new ContextMenuStrip();
            contextMenuStrip.Items.Add(currentHostItem);
            contextMenuStrip.Items.Add(changeHostItem);
            contextMenuStrip.Items.Add(new ToolStripSeparator());
            contextMenuStrip.Items.Add(contextMenuCloseItem);
            
            // icon
            _trayIcon = new NotifyIcon()
            {
                ContextMenuStrip = contextMenuStrip,
                Text = Resources.PingCheckerTitle,
                Visible =  true,
                Icon = NoConnectionIcon

            };

            contextMenuCloseItem.Click += delegate
            {
                _trayIcon.Visible = false;
                Application.Exit();
            };
        }

        private void ping_NewValue(object sender, ProgressChangedEventArgs e)
        {
            if (_trayIcon == null) return;

            var result = e.UserState as PingResult;
            if (result == null) return;

            _trayIcon.Icon = (result.Status == PingStatus.Ok && result.Ping.HasValue) ? PingIcon(result.Ping.Value) : NoConnectionIcon;
        }

        private void ping_DoWork(object sender, DoWorkEventArgs e)
        {
            var bw = sender as BackgroundWorker;
            if (bw == null) return;

            bw.ReportProgress(0, PingChecker.Get());

            while (!bw.CancellationPending)
            {
                Thread.Sleep(1000);
                bw.ReportProgress(0, PingChecker.Get());
            }
        }

        private void StartPinging()
        {
            PingChecker = new RealPingChecker(Settings.Default.PingedHost);
            var backgroundWorker = new BackgroundWorker()
            {
                WorkerReportsProgress = true
            };

            backgroundWorker.ProgressChanged += ping_NewValue;
            backgroundWorker.DoWork += ping_DoWork;
            backgroundWorker.RunWorkerAsync();
        }
    }
}

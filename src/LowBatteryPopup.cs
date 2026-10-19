using System;
using System.Drawing;
using System.Windows.Forms;

namespace GearBatt
{
    internal sealed class LowBatteryPopup : Form
    {
        private readonly PictureBox _icon;
        private readonly Label _device;
        private readonly Label _message;
        private readonly float _scale;
        private Image _iconImage;

        public LowBatteryPopup(WirelessDevice device)
        {
            using (Graphics screen = Graphics.FromHwnd(IntPtr.Zero))
                _scale = screen.DpiX / 96f;
            if (_scale <= 0.5f || _scale > 4f)
                _scale = 1f;

            Text = "低电量提醒";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            TopMost = true;
            StartPosition = FormStartPosition.Manual;
            AutoScaleMode = AutoScaleMode.None;
            BackColor = Color.FromArgb(30, 30, 34);
            ForeColor = Color.White;
            DoubleBuffered = true;
            ClientSize = new Size(S(360), S(116));

            Screen target = Screen.FromPoint(Cursor.Position);
            Rectangle area = target.WorkingArea;
            int x = Math.Max(area.Left + S(8),
                area.Right - Width - S(18));
            int y = Math.Max(area.Top + S(8),
                area.Bottom - Height - S(18));
            Location = new Point(x, y);

            Panel accent = new Panel();
            accent.BackColor = Color.FromArgb(255, 69, 58);
            accent.SetBounds(0, 0, S(4), ClientSize.Height);
            Controls.Add(accent);

            _icon = new PictureBox();
            _icon.BackColor = Color.Transparent;
            _icon.SetBounds(S(18), S(28), S(56), S(56));
            Controls.Add(_icon);

            Label title = new Label();
            title.Text = "低电量提醒";
            title.Font = new Font(Font.FontFamily, 11.5f, FontStyle.Bold);
            title.ForeColor = Color.FromArgb(255, 107, 107);
            title.BackColor = Color.Transparent;
            title.SetBounds(S(88), S(13), S(230), S(26));
            Controls.Add(title);

            _device = new Label();
            _device.AutoEllipsis = true;
            _device.AutoSize = false;
            _device.ForeColor = Color.FromArgb(242, 242, 244);
            _device.BackColor = Color.Transparent;
            _device.SetBounds(S(88), S(42), S(250), S(22));
            Controls.Add(_device);

            _message = new Label();
            _message.ForeColor = Color.FromArgb(202, 202, 206);
            _message.BackColor = Color.Transparent;
            _message.SetBounds(S(88), S(68), S(250), S(36));
            Controls.Add(_message);

            Label close = new Label();
            close.Text = "×";
            close.Font = new Font(Font.FontFamily, 12f, FontStyle.Bold);
            close.ForeColor = Color.FromArgb(150, 150, 154);
            close.TextAlign = ContentAlignment.MiddleCenter;
            close.BackColor = Color.Transparent;
            close.SetBounds(S(326), S(2), S(30), S(30));
            close.Cursor = Cursors.Hand;
            close.Click += delegate { Close(); };
            Controls.Add(close);

            Paint += delegate(object sender, PaintEventArgs e)
            {
                using (Pen pen = new Pen(Color.FromArgb(70, 70, 76),
                           Math.Max(1, S(1))))
                    e.Graphics.DrawRectangle(
                        pen, 0, 0, Width - 1, Height - 1);
            };

            UpdateDevice(device);
        }

        public void UpdateDevice(WirelessDevice device)
        {
            _device.Text = device.Name;
            _message.Text = "剩余 " + device.Percent + "%，请尽快充电。";
            if (_iconImage != null)
            {
                _icon.Image = null;
                _iconImage.Dispose();
            }
            _iconImage = IconRenderer.RenderBitmap(
                S(56), device.Percent, false, false);
            _icon.Image = _iconImage;
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                return cp;
            }
        }

        private int S(int value)
        {
            return (int)Math.Round(value * _scale);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_iconImage != null)
                {
                    _iconImage.Dispose();
                    _iconImage = null;
                }
            }
            base.Dispose(disposing);
        }
    }
}

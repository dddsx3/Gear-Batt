using System;
using System.Drawing;
using System.Windows.Forms;

namespace GearBatt
{
    internal sealed class StateEventForm : Form
    {
        public event EventHandler StateChanged;

        public StateEventForm()
        {
            Text = "GearBattEvents";
            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            Location = new Point(-32000, -32000);
            Size = new Size(1, 1);
            AutoScaleMode = AutoScaleMode.None;
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
                cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
                cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
                return cp;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0219) // WM_DEVICECHANGE
            {
                int evt = m.WParam.ToInt32();
                if (evt == 0x0007 || evt == 0x8000 || evt == 0x8003 ||
                    evt == 0x8004 || evt == 0x0018)
                    RaiseChanged();
            }
            else if (m.Msg == 0x0218) // WM_POWERBROADCAST
            {
                int evt = m.WParam.ToInt32();
                if (evt == 0x0006 || evt == 0x0012 || evt == 0x0007)
                    RaiseChanged();
            }
            base.WndProc(ref m);
        }

        private void RaiseChanged()
        {
            EventHandler handler = StateChanged;
            if (handler != null)
                handler(this, EventArgs.Empty);
        }
    }
}

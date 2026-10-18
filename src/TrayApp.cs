using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace GearBatt
{
    internal sealed class TrayApp : ApplicationContext
    {
        private readonly bool _mock;
        private readonly Scanner _scanner = new Scanner();
        private readonly Settings _settings;
        private readonly Control _ui;
        private readonly NotifyIcon _notifyIcon;
        private readonly ContextMenuStrip _menu;
        private readonly System.Windows.Forms.Timer _timer;

        private List<WirelessDevice> _devices = new List<WirelessDevice>();
        private string _selectedId;
        private bool _scanning;
        private bool _menuNeedsRebuild;
        private bool _disposed;
        private Icon _currentIcon;

        public TrayApp(bool mock)
        {
            _mock = mock;
            _settings = Settings.Load();
            _selectedId = _settings.SelectedId ?? "";

            Control ui = new Control();
            ui.CreateControl();
            if (!ui.IsHandleCreated)
            {
                ui.Dispose();
                Form hidden = new Form();
                hidden.ShowInTaskbar = false;
                hidden.FormBorderStyle = FormBorderStyle.None;
                hidden.StartPosition = FormStartPosition.Manual;
                hidden.Location = new Point(-32000, -32000);
                hidden.Size = new Size(1, 1);
                hidden.CreateControl();
                ui = hidden;
            }
            _ui = ui;

            _menu = new ContextMenuStrip();
            _menu.RenderMode = ToolStripRenderMode.System;
            _menu.Closed += delegate { OnMenuClosed(); };

            _notifyIcon = new NotifyIcon();
            _notifyIcon.ContextMenuStrip = _menu;
            _notifyIcon.MouseClick += OnTrayMouseClick;
            SetIcon(-1, false, true);
            _notifyIcon.Visible = true;

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 30000;
            _timer.Tick += delegate { BeginScan(false); };
            _timer.Start();

            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;

            RebuildMenu();
            BeginScan(false);
        }

        private void BeginScan(bool userInitiated)
        {
            if (_scanning || _disposed)
                return;

            _scanning = true;
            RebuildMenu();
            if (userInitiated)
                UpdateTray();

            ThreadPool.QueueUserWorkItem(delegate
            {
                ScanResult result = null;
                Exception failure = null;
                try
                {
                    result = _scanner.Scan(_mock, false);
                }
                catch (Exception ex)
                {
                    failure = ex;
                }

                try
                {
                    _ui.BeginInvoke(new Action(delegate
                    {
                        OnScanCompleted(result, failure);
                    }));
                }
                catch (Exception)
                {
                }
            });
        }

        private void OnScanCompleted(ScanResult result, Exception failure)
        {
            _scanning = false;
            if (_disposed)
                return;

            if (result == null)
                result = new ScanResult();
            if (failure != null)
                result.AddError(failure.Message);

            _devices = result.Devices;

            WirelessDevice selected = FindSelected();
            if (selected == null && string.IsNullOrEmpty(_selectedId) &&
                _devices.Count > 0)
            {
                selected = _devices[0];
                _selectedId = selected.Id;
                _settings.SelectedId = selected.Id;
                _settings.SelectedName = selected.Name;
                _settings.Save();
            }

            if (selected != null && _settings.SelectedName != selected.Name)
            {
                _settings.SelectedName = selected.Name;
                _settings.Save();
            }

            UpdateTray();
            RebuildMenu();
        }

        private void UpdateTray()
        {
            if (_disposed)
                return;

            WirelessDevice selected = FindSelected();
            if (selected != null)
            {
                bool unknown = selected.Percent < 0 && !selected.Charging;
                SetIcon(selected.Percent, selected.Charging, unknown);
                string text = selected.TooltipText;
                if (!string.IsNullOrEmpty(selected.Source))
                    text += " · " + selected.Source;
                SetTooltip(text);
            }
            else if (!string.IsNullOrEmpty(_selectedId))
            {
                SetIcon(-1, false, true);
                string name = _settings.SelectedName;
                if (string.IsNullOrEmpty(name))
                    SetTooltip("Gear-Batt · 所选设备未连接");
                else
                    SetTooltip("Gear-Batt · 所选设备未连接：" + name);
            }
            else
            {
                SetIcon(-1, false, true);
                if (_scanning)
                    SetTooltip("Gear-Batt · 正在扫描无线设备…");
                else
                    SetTooltip("Gear-Batt · 未检测到支持电量上报的设备");
            }
        }

        private void SetTooltip(string text)
        {
            try
            {
                _notifyIcon.Text = TextUtil.Truncate(text, 63);
            }
            catch (Exception)
            {
                _notifyIcon.Text = "Gear-Batt";
            }
        }

        private void SetIcon(int percent, bool charging, bool unknown)
        {
            Icon icon = IconRenderer.CreateIcon(GetTrayIconSize(), percent, charging, unknown);
            Icon old = _currentIcon;
            _notifyIcon.Icon = icon;
            _currentIcon = icon;
            if (old != null)
                old.Dispose();
        }

        private static int GetTrayIconSize()
        {
            int size = Native.GetSystemMetrics(Native.SM_CXSMICON);
            if (size < 16 || size > 64)
                size = 16;
            return size;
        }

        private WirelessDevice FindSelected()
        {
            if (string.IsNullOrEmpty(_selectedId))
                return null;
            for (int i = 0; i < _devices.Count; i++)
            {
                if (_devices[i].Id == _selectedId)
                    return _devices[i];
            }
            return null;
        }

        private WirelessDevice FindById(string id)
        {
            for (int i = 0; i < _devices.Count; i++)
            {
                if (_devices[i].Id == id)
                    return _devices[i];
            }
            return null;
        }

        private void RebuildMenu()
        {
            if (_disposed)
                return;
            if (_menu.Visible)
            {
                _menuNeedsRebuild = true;
                return;
            }

            while (_menu.Items.Count > 0)
            {
                ToolStripItem item = _menu.Items[0];
                _menu.Items.RemoveAt(0);
                item.Dispose();
            }

            ToolStripLabel header = new ToolStripLabel("选择要显示的设备");
            header.ForeColor = SystemColors.GrayText;
            _menu.Items.Add(header);

            if (_devices.Count == 0)
            {
                ToolStripMenuItem none = new ToolStripMenuItem(
                    _scanning ? "正在扫描无线设备…" : "未检测到支持电量上报的设备");
                none.Enabled = false;
                _menu.Items.Add(none);

                ToolStripMenuItem hint = new ToolStripMenuItem(
                    "需要设备向 Windows 或罗技 HID++ 上报电量");
                hint.Enabled = false;
                _menu.Items.Add(hint);
            }
            else
            {
                for (int i = 0; i < _devices.Count; i++)
                {
                    WirelessDevice device = _devices[i];
                    ToolStripMenuItem item = new ToolStripMenuItem(device.MenuText);
                    item.Checked = device.Id == _selectedId;
                    item.CheckOnClick = false;
                    string id = device.Id;
                    item.Click += delegate { SelectDevice(id); };
                    _menu.Items.Add(item);
                }

                if (!string.IsNullOrEmpty(_selectedId) && FindSelected() == null)
                {
                    string name = _settings.SelectedName;
                    ToolStripMenuItem missing = new ToolStripMenuItem(
                        string.IsNullOrEmpty(name)
                            ? "已选设备当前未连接"
                            : "已选设备当前未连接：" + name);
                    missing.Enabled = false;
                    _menu.Items.Add(missing);
                }
            }

            _menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem refresh = new ToolStripMenuItem(
                _scanning ? "正在刷新…" : "立即刷新");
            refresh.Enabled = !_scanning;
            refresh.Click += delegate { BeginScan(true); };
            _menu.Items.Add(refresh);

            ToolStripMenuItem autostart = new ToolStripMenuItem("开机启动");
            autostart.Checked = Settings.IsAutostartEnabled();
            autostart.Click += delegate { ToggleAutostart(); };
            _menu.Items.Add(autostart);

            ToolStripMenuItem about = new ToolStripMenuItem("使用说明");
            about.Click += delegate { ShowAbout(); };
            _menu.Items.Add(about);

            _menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem exit = new ToolStripMenuItem("退出");
            exit.Click += delegate { ExitApplication(); };
            _menu.Items.Add(exit);

            _menuNeedsRebuild = false;
        }

        private void OnMenuClosed()
        {
            if (_menuNeedsRebuild && !_disposed)
                RebuildMenu();
        }

        private void SelectDevice(string id)
        {
            WirelessDevice device = FindById(id);
            if (device == null)
                return;

            _selectedId = device.Id;
            _settings.SelectedId = device.Id;
            _settings.SelectedName = device.Name;
            _settings.Save();
            UpdateTray();

            _menuNeedsRebuild = true;
            if (!_menu.Visible)
                RebuildMenu();
        }

        private void ToggleAutostart()
        {
            bool enable = !Settings.IsAutostartEnabled();
            string error;
            if (!Settings.SetAutostart(enable, out error))
            {
                MessageBox.Show(
                    "无法修改开机启动设置：\n" + error,
                    "Gear-Batt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            RebuildMenu();
        }

        private void ShowAbout()
        {
            string text =
                "Gear-Batt 1.0\n\n" +
                "显示所选无线设备的电量与充电状态。\n\n" +
                "支持来源：\n" +
                "• Windows 电池接口（部分 2.4G 接收器）\n" +
                "• 罗技 HID++ 2.0（Unifying / Bolt / Lightspeed）\n" +
                "• Windows 蓝牙设备电量属性\n\n" +
                "设备本身必须上报电量。使用私有协议且不向系统上报电量的设备无法读取。\n\n" +
                "左键单击托盘图标可选择要显示的设备；悬停可查看精确电量。";
            MessageBox.Show(
                text, "关于 Gear-Batt",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void OnTrayMouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || _disposed)
                return;

            if (_menu.Visible)
            {
                _menu.Close();
                return;
            }

            _menu.Show(Cursor.Position);
        }

        private void OnUserPreferenceChanged(
            object sender, UserPreferenceChangedEventArgs e)
        {
            if (_disposed)
                return;
            try
            {
                _ui.BeginInvoke(new Action(delegate
                {
                    if (!_disposed)
                        UpdateTray();
                }));
            }
            catch (Exception)
            {
            }
        }

        private void ExitApplication()
        {
            Cleanup();
            ExitThread();
        }

        private void Cleanup()
        {
            if (_disposed)
                return;
            _disposed = true;

            SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;

            try
            {
                _timer.Stop();
                _timer.Dispose();
            }
            catch (Exception)
            {
            }

            try
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
            catch (Exception)
            {
            }

            if (_currentIcon != null)
            {
                _currentIcon.Dispose();
                _currentIcon = null;
            }

            try
            {
                _menu.Dispose();
            }
            catch (Exception)
            {
            }

            try
            {
                _ui.Dispose();
            }
            catch (Exception)
            {
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                Cleanup();
            base.Dispose(disposing);
        }
    }
}

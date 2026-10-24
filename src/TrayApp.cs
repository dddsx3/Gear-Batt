using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
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
        private DateTime _lastUpdated = DateTime.MinValue;
        private LowBatteryPopup _lowBatteryPopup;
        private string _lowBatteryAlertedDeviceId = "";
        private readonly StateEventForm _eventForm;
        private readonly System.Windows.Forms.Timer _eventDebounce;
        private bool _pendingScan;

        public TrayApp(bool mock)
        {
            _mock = mock;
            _settings = Settings.Load();
            _selectedId = _settings.SelectedId ?? "";

            _eventForm = new StateEventForm();
            _ui = _eventForm;
            _eventDebounce = new System.Windows.Forms.Timer();
            _eventDebounce.Interval = 450;
            _eventDebounce.Tick += delegate
            {
                _eventDebounce.Stop();
                BeginScan(false);
            };

            _menu = new ContextMenuStrip();
            _menu.RenderMode = ToolStripRenderMode.System;
            _menu.Closed += delegate { OnMenuClosed(); };

            _notifyIcon = new NotifyIcon();
            _notifyIcon.ContextMenuStrip = _menu;
            _notifyIcon.MouseClick += OnTrayMouseClick;
            SetIcon(-1, false, true);
            _notifyIcon.Visible = true;

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 60000;
            _timer.Tick += delegate { BeginScan(false); };
            _timer.Start();

            SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;

            _eventForm.StateChanged += delegate { PostStateChanged(); };
            _eventForm.Show();

            RebuildMenu();
            BeginScan(false);
        }

        private void BeginScan(bool userInitiated)
        {
            if (_disposed)
                return;
            if (_scanning)
            {
                _pendingScan = true;
                return;
            }

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

            _lastUpdated = DateTime.Now;
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
            CheckLowBattery(selected);
            UpdateRefreshInterval(selected);

            if (_pendingScan && !_disposed)
            {
                _pendingScan = false;
                BeginScan(false);
            }
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
                    text += " · " + L10n.Source(selected.Source);
                if (_lastUpdated != DateTime.MinValue)
                    text += " · " + _lastUpdated.ToString("HH:mm");
                SetTooltip(text);
            }
            else if (!string.IsNullOrEmpty(_selectedId))
            {
                SetIcon(-1, false, true);
                string name = _settings.SelectedName;
                if (string.IsNullOrEmpty(name))
                    SetTooltip(L10n.T(
                        "Gear-Batt · 所选设备未连接",
                        "Gear-Batt · Selected device is not connected"));
                else
                    SetTooltip(L10n.T(
                        "Gear-Batt · 所选设备未连接：" + name,
                        "Gear-Batt · Selected device is not connected: " + name));
            }
            else
            {
                SetIcon(-1, false, true);
                if (_scanning)
                    SetTooltip(L10n.T(
                        "Gear-Batt · 正在扫描无线设备…",
                        "Gear-Batt · Scanning wireless devices…"));
                else
                    SetTooltip(L10n.T(
                        "Gear-Batt · 未检测到支持电量上报的设备",
                        "Gear-Batt · No battery-reporting devices found"));
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

            ToolStripLabel header = new ToolStripLabel(L10n.T(
                "选择要显示的设备", "Select a device"));
            header.ForeColor = SystemColors.GrayText;
            _menu.Items.Add(header);

            if (_devices.Count == 0)
            {
                ToolStripMenuItem none = new ToolStripMenuItem(
                    _scanning
                        ? L10n.T("正在扫描无线设备…", "Scanning wireless devices…")
                        : L10n.T("未检测到支持电量上报的设备",
                            "No battery-reporting devices found"));
                none.Enabled = false;
                _menu.Items.Add(none);

                ToolStripMenuItem hint = new ToolStripMenuItem(L10n.T(
                    "需要设备向 Windows、HID++ 或已知私有协议上报电量",
                    "A device must report battery via Windows, HID++, or a supported protocol"));
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
                            ? L10n.T("已选设备当前未连接",
                                "Selected device is not connected")
                            : L10n.T("已选设备当前未连接：" + name,
                                "Selected device is not connected: " + name));
                    missing.Enabled = false;
                    _menu.Items.Add(missing);
                }
            }

            ToolStripLabel updated = new ToolStripLabel(
                _lastUpdated == DateTime.MinValue
                    ? L10n.T("上次更新：—", "Last update: —")
                    : L10n.T("上次更新：" + _lastUpdated.ToString("HH:mm:ss"),
                        "Last update: " + _lastUpdated.ToString("HH:mm:ss")));
            updated.ForeColor = SystemColors.GrayText;
            _menu.Items.Add(updated);

            _menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem refresh = new ToolStripMenuItem(
                _scanning
                    ? L10n.T("正在刷新…", "Refreshing…")
                    : L10n.T("立即刷新", "Refresh now"));
            refresh.Enabled = !_scanning;
            refresh.Click += delegate { BeginScan(true); };
            _menu.Items.Add(refresh);

            ToolStripMenuItem autostart = new ToolStripMenuItem(
                L10n.T("开机启动", "Start with Windows"));
            autostart.Checked = Settings.IsAutostartEnabled();
            autostart.Click += delegate { ToggleAutostart(); };
            _menu.Items.Add(autostart);

            ToolStripMenuItem about = new ToolStripMenuItem(
                L10n.T("使用说明", "Help"));
            about.Click += delegate { ShowAbout(); };
            _menu.Items.Add(about);

            ToolStripMenuItem adapt = new ToolStripMenuItem(
                L10n.T("帮助适配我的设备…", "Help adapt my device…"));
            adapt.Click += delegate { ShowAdapterHelp(); };
            _menu.Items.Add(adapt);

            _menu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem exit = new ToolStripMenuItem(
                L10n.T("退出", "Exit"));
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
            CheckLowBattery(device);
            UpdateRefreshInterval(device);

            _menuNeedsRebuild = true;
            if (!_menu.Visible)
                RebuildMenu();
        }

        private void CheckLowBattery(WirelessDevice device)
        {
            if (_disposed)
                return;
            if (device == null)
            {
                _lowBatteryAlertedDeviceId = "";
                return;
            }

            bool low = device.Percent >= 0 &&
                device.Percent < 5 && !device.Charging;
            if (!low)
            {
                _lowBatteryAlertedDeviceId = "";
                return;
            }
            if (_lowBatteryAlertedDeviceId == device.Id)
                return;

            _lowBatteryAlertedDeviceId = device.Id;
            ShowLowBatteryPopup(device);
        }

        private void ShowLowBatteryPopup(WirelessDevice device)
        {
            if (_lowBatteryPopup != null && !_lowBatteryPopup.IsDisposed)
            {
                _lowBatteryPopup.Close();
                _lowBatteryPopup = null;
            }

            LowBatteryPopup popup = new LowBatteryPopup(device);
            _lowBatteryPopup = popup;
            popup.FormClosed += delegate
            {
                if (ReferenceEquals(_lowBatteryPopup, popup))
                    _lowBatteryPopup = null;
            };
            popup.Show();
        }

        private void OnPowerModeChanged(
            object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.StatusChange ||
                e.Mode == PowerModes.Resume)
                PostStateChanged();
        }

        private void PostStateChanged()
        {
            if (_disposed)
                return;
            try
            {
                _ui.BeginInvoke(new Action(delegate
                {
                    if (_disposed)
                        return;
                    _eventDebounce.Stop();
                    _eventDebounce.Start();
                }));
            }
            catch (Exception)
            {
            }
        }

        private void UpdateRefreshInterval(WirelessDevice selected)
        {
            int interval = 60000;
            if (selected != null && (selected.Charging ||
                (selected.Percent >= 0 && selected.Percent <= 10)))
            {
                interval = 5000;
            }
            else if (selected == null && _scanner.CxReceiverPending)
            {
                // A CX receiver is present but the mouse is asleep; retry
                // more often so the device shows up right after it wakes.
                interval = 10000;
            }
            if (_timer.Interval != interval)
                _timer.Interval = interval;
        }

        private void ToggleAutostart()
        {
            bool enable = !Settings.IsAutostartEnabled();
            string error;
            if (!Settings.SetAutostart(enable, out error))
            {
                MessageBox.Show(
                    L10n.T("无法修改开机启动设置：\n" + error,
                        "Could not change the start-with-Windows setting:\n" + error),
                    "Gear-Batt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            RebuildMenu();
        }

        private void ShowAbout()
        {
            string text;
            if (L10n.Chinese)
            {
                text =
                    "Gear-Batt 1.4.1\n\n" +
                    "显示所选无线设备的电量与充电状态。\n\n" +
                "支持来源：\n" +
                "• Windows 电池接口（部分 2.4G 接收器）\n" +
                "• 罗技 HID++ 2.0（Unifying / Bolt / Lightspeed）\n" +
                "• CX 2.4G 接收器（如 260D:1163，含电压）\n" +
                "• ATK/VXE 373B:1216（只读查询）\n" +
                "• DARKRIM 3654:6901（只读查询）\n" +
                "• Windows 蓝牙设备电量属性\n\n" +
                    "状态刷新：设备接入/移除、电源状态变化会立即刷新；\n" +
                    "充电或电量 ≤10% 时每 5 秒快速检查，其余时间 60 秒。\n\n" +
                    "低电量提醒：当前显示的设备未充电且电量低于 5% 时，\n" +
                    "会静默弹出一个提示卡片（不播放声音、不抢焦点、不会自动关闭；\n" +
                    "点击右上角 × 关闭）。同一轮低电量只提醒一次。\n\n" +
                    "设备本身必须上报电量。使用私有协议且不向系统上报电量的设备无法读取；\n" +
                    "可以运行“帮助适配我的设备…”采集本机数据，帮助后续适配。\n\n" +
                    "左键单击托盘图标可选择要显示的设备；悬停可查看精确电量。";
            }
            else
            {
                text =
                    "Gear-Batt 1.4.1\n\n" +
                    "Shows the battery and charging state of the selected wireless device.\n\n" +
                    "Supported sources:\n" +
                    "• Windows battery interface (some 2.4 GHz receivers)\n" +
                    "• Logitech HID++ 2.0 (Unifying / Bolt / Lightspeed)\n" +
                    "• CX 2.4G receivers (e.g. 260D:1163, with voltage)\n" +
                    "• ATK/VXE 373B:1216 (read-only)\n" +
                    "• DARKRIM 3654:6901 (read-only)\n" +
                    "• Windows Bluetooth battery property\n\n" +
                    "Status refresh: device attach/remove and power-status changes refresh\n" +
                    "immediately; while charging or at ≤10% it checks every 5 seconds;\n" +
                    "otherwise every 60 seconds.\n\n" +
                    "Low-battery alert: when the selected device is discharging and below 5%,\n" +
                    "a silent card appears (no sound, no focus stealing, does not auto-close;\n" +
                    "click × to close). It is shown once per low-battery episode.\n\n" +
                    "The device must report battery. Devices that only use a private protocol\n" +
                    "and never report to Windows cannot be read. Use \"Help adapt my device…\"\n" +
                    "to collect local data for future support.\n\n" +
                    "Left-click the tray icon to select the displayed device; hover for the exact percentage.";
            }
            MessageBox.Show(
                text, L10n.T("关于 Gear-Batt", "About Gear-Batt"),
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ShowAdapterHelp()
        {
            string directory = Path.GetDirectoryName(Application.ExecutablePath);
            string collector = string.IsNullOrEmpty(directory)
                ? "Gear-Batt-Collector.exe"
                : Path.Combine(directory, "Gear-Batt-Collector.exe");
            if (File.Exists(collector))
            {
                try
                {
                    Process.Start(collector);
                    return;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        L10n.T("无法启动数据采集器：\n" + ex.Message,
                            "Could not start the data collector:\n" + ex.Message),
                        "Gear-Batt", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            MessageBox.Show(
                L10n.T(
                    "Gear-Batt 提供完全自愿的数据采集器，用于帮助适配更多 2.4G 设备。\n\n" +
                    "请下载并运行与主程序同目录的 Gear-Batt-Collector.exe。\n" +
                    "它会生成一份只保存在本机的报告，你可以在检查后手动附加到 GitHub Issue。\n" +
                    "本程序不会自动上传任何数据。\n\n" +
                    "项目地址：\nhttps://github.com/dddsx3/Gear-Batt",
                    "Gear-Batt provides a completely voluntary data collector to help\n" +
                    "add support for more 2.4G devices.\n\n" +
                    "Download and run Gear-Batt-Collector.exe from the same folder as the\n" +
                    "main app. It creates a report that stays on your machine; review it and\n" +
                    "attach it manually to a GitHub Issue. The program never uploads data\n" +
                    "automatically.\n\n" +
                    "Project:\nhttps://github.com/dddsx3/Gear-Batt"),
                L10n.T("帮助适配我的设备", "Help adapt my device"),
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
            SystemEvents.PowerModeChanged -= OnPowerModeChanged;
            _pendingScan = false;

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
                _eventDebounce.Stop();
                _eventDebounce.Dispose();
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

            if (_lowBatteryPopup != null)
            {
                try
                {
                    _lowBatteryPopup.Close();
                }
                catch (Exception)
                {
                }
                _lowBatteryPopup = null;
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
                _eventForm.Close();
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

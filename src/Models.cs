using System;
using System.Collections.Generic;
using System.Text;

namespace GearBatt
{
    internal sealed class WirelessDevice
    {
        public string Id = "";
        public string Name = "无线设备";
        public int Percent = -1;
        public bool PercentApproximate;
        public bool Charging;
        public int VoltageMv;
        public string Source = "无线";
        public string Provider = "";
        public string Detail = "";
        public string InstanceId = "";
        public int SortOrder = 50;

        public string PercentText
        {
            get
            {
                if (Percent < 0)
                    return "—";
                return (PercentApproximate ? "≈" : "") + Percent.ToString() + "%";
            }
        }

        public string StatusText
        {
            get
            {
                if (Charging)
                    return L10n.T("充电中", "Charging");
                if (Percent < 0)
                    return L10n.T("电量未知", "Unknown");
                return L10n.T("电池供电", "On battery");
            }
        }

        public string MenuText
        {
            get
            {
                string source = string.IsNullOrEmpty(Source)
                    ? ""
                    : "  ·  " + L10n.Source(Source);
                string voltage = VoltageMv > 0
                    ? "  ·  " + (VoltageMv / 1000.0).ToString("0.00",
                        System.Globalization.CultureInfo.InvariantCulture) + "V"
                    : "";
                return Name + "    " + PercentText + "  ·  " + StatusText +
                    source + voltage;
            }
        }

        public string TooltipText
        {
            get
            {
                string status;
                if (Charging)
                {
                    status = Percent < 0
                        ? L10n.T("充电中", "Charging")
                        : PercentText + L10n.T(" · 充电中", " · Charging");
                }
                else if (Percent < 0)
                {
                    status = L10n.T("电量未知", "Unknown");
                }
                else
                {
                    status = PercentText + L10n.T(" · 电池", " · Battery");
                }
                string suffix = VoltageMv > 0
                    ? " · " + (VoltageMv / 1000.0).ToString("0.00",
                        System.Globalization.CultureInfo.InvariantCulture) + "V"
                    : "";
                return Name + " · " + status + suffix;
            }
        }
    }

    internal sealed class ScanResult
    {
        public List<WirelessDevice> Devices = new List<WirelessDevice>();
        public List<string> Notes = new List<string>();
        public List<string> Errors = new List<string>();
        public DateTime Timestamp = DateTime.Now;

        public void AddNote(string text)
        {
            if (!string.IsNullOrEmpty(text))
                Notes.Add(text);
        }

        public void AddError(string text)
        {
            if (!string.IsNullOrEmpty(text))
                Errors.Add(text);
        }
    }

    internal static class TextUtil
    {
        public static string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            if (text.Length <= max)
                return text;
            if (max <= 1)
                return text.Substring(0, max);
            return text.Substring(0, max - 1) + "…";
        }

        public static string Hex(byte[] data, int max)
        {
            if (data == null || data.Length == 0)
                return "";
            int count = Math.Min(data.Length, max);
            StringBuilder sb = new StringBuilder(count * 3);
            for (int i = 0; i < count; i++)
            {
                if (i > 0)
                    sb.Append(' ');
                sb.Append(data[i].ToString("X2"));
            }
            if (data.Length > max)
                sb.Append(" ...");
            return sb.ToString();
        }

        public static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            StringBuilder sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\r' || c == '\n' || c == '\t')
                    c = ' ';
                if (c >= 32)
                    sb.Append(c);
            }
            string result = sb.ToString().Trim();
            while (result.Contains("  "))
                result = result.Replace("  ", " ");
            return result;
        }

        public static string NormalizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "";
            StringBuilder sb = new StringBuilder(name.Length);
            for (int i = 0; i < name.Length; i++)
            {
                char c = char.ToLowerInvariant(name[i]);
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c > 127)
                    sb.Append(c);
            }
            return sb.ToString();
        }

        public static bool IsGenericDeviceName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return true;
            string n = name.ToLowerInvariant();
            if (n.Contains("hid-compliant") || n.Contains("hid compliant"))
                return true;
            if (n.Contains("符合 hid 标准") || n.Contains("符合hid标准"))
                return true;
            if (n.Contains("usb input device") || n.Contains("usb 输入设备"))
                return true;
            if (n.Contains("usb composite device") || n.Contains("usb 复合设备"))
                return true;
            if (n.Contains("bluetooth le device") || n.Contains("蓝牙 le 设备"))
                return true;
            if (n.Contains("bluetooth device") || n.Contains("蓝牙设备"))
                return true;
            if (n.Contains("hid keyboard device") || n.Contains("hid 键盘设备"))
                return true;
            if (n.Contains("hid-compliant mouse") || n.Contains("hid 鼠标"))
                return true;
            if (n.Contains("usb root hub") || n.Contains("usb 根集线器"))
                return true;
            if (n.Contains("generic usb hub") || n.Contains("通用 usb 集线器"))
                return true;
            if (n.Contains("microsoft") && n.Contains("enumerator"))
                return true;
            if (n.Contains("蓝牙 le 枚举器") || n.Contains("bluetooth le enumerator"))
                return true;
            if (n.Contains("蓝牙枚举器") || n.Contains("bluetooth enumerator"))
                return true;
            if (n.Contains("usb 输入设备") || n.Contains("usb input device"))
                return true;
            return false;
        }

        public static bool LooksLikePeripheralCategory(string[] categories)
        {
            if (categories == null || categories.Length == 0)
                return true;
            for (int i = 0; i < categories.Length; i++)
            {
                string c = categories[i];
                if (string.IsNullOrEmpty(c))
                    continue;
                if (c.StartsWith("Input.", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (c.IndexOf("Mouse", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                if (c.IndexOf("Keyboard", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                if (c.IndexOf("Audio", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                if (c.IndexOf("Headset", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                if (c.IndexOf("Wearable", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        public static string SourceFromPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return "无线";
            string p = path.ToUpperInvariant();
            if (p.Contains("BTHLE") || p.Contains("BTHENUM") || p.Contains("BTH\\"))
                return "蓝牙";
            if (p.Contains("HID#") || p.Contains("USB#") || p.Contains("VID_"))
                return "2.4G";
            return "无线";
        }

        public static bool IsSystemBatteryPath(string path, string name)
        {
            string p = (path ?? "").ToUpperInvariant();
            if (p.Contains("ACPI") || p.Contains("PNP0C0A") || p.Contains("ACPI0003"))
                return true;
            string n = (name ?? "").ToLowerInvariant();
            if (n.Contains("internal battery") || n.Contains("内置电池") ||
                n.Contains("ac adapter") || n.Contains("ac 适配器") ||
                n.Contains("uninterruptible") || n.Contains("ups"))
                return true;
            return false;
        }

        public static bool IsLikelyLogitechPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            return path.IndexOf("VID_046D", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}

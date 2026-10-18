using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace GearBatt
{
    internal sealed class BatteryReading
    {
        public int Percent = -1;
        public bool Charging;
        public uint PowerState;
        public uint Capacity;
        public uint FullCapacity;
        public uint DesignedCapacity;
        public string DeviceName = "";
        public string Diagnostic = "";
    }

    internal static class DeviceNameResolver
    {
        public static string Resolve(
            string instanceId, string preferredName, string friendlyName,
            string busReportedName, string deviceDesc, List<string> log)
        {
            List<string> candidates = new List<string>();
            AddCandidate(candidates, preferredName);
            AddCandidate(candidates, friendlyName);
            AddCandidate(candidates, busReportedName);
            AddCandidate(candidates, deviceDesc);

            string current = instanceId;
            for (int level = 0; level < 8 && !string.IsNullOrEmpty(current); level++)
            {
                uint devInst;
                if (Native.CM_Locate_DevNodeW(out devInst, current, Native.CM_LOCATE_DEVNODE_NORMAL) != 0)
                    break;

                AddCandidate(candidates, Native.ReadStringPropertyCm(devInst, Native.DEVPKEY_Device_FriendlyName));
                AddCandidate(candidates, Native.ReadStringPropertyCm(devInst, Native.DEVPKEY_Device_BusReportedDeviceDesc));
                AddCandidate(candidates, Native.ReadStringPropertyCm(devInst, Native.DEVPKEY_Device_DeviceDesc));

                string parent = Native.ReadStringPropertyCm(devInst, Native.DEVPKEY_Device_Parent);
                if (string.IsNullOrEmpty(parent))
                    break;
                current = parent;
            }

            for (int i = 0; i < candidates.Count; i++)
            {
                if (!TextUtil.IsGenericDeviceName(candidates[i]))
                {
                    if (log != null && log.Count < 200)
                        log.Add("name: " + candidates[i] + " (from " + candidates.Count + " candidates)");
                    return candidates[i];
                }
            }

            if (candidates.Count > 0)
                return candidates[0];
            return "无线设备";
        }

        private static void AddCandidate(List<string> list, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;
            string cleaned = TextUtil.Clean(value);
            if (cleaned.Length == 0)
                return;
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], cleaned, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            list.Add(cleaned);
        }
    }

    internal sealed class WindowsBatteryProvider
    {
        public void Scan(ScanResult result, List<string> log)
        {
            Guid guid = Native.GUID_DEVINTERFACE_BATTERY;
            IntPtr set = Native.SetupDiGetClassDevsW(
                ref guid, IntPtr.Zero, IntPtr.Zero,
                Native.DIGCF_PRESENT | Native.DIGCF_DEVICEINTERFACE);
            if (set == new IntPtr(-1))
            {
                result.AddError("Windows 电池接口枚举失败，错误码 " + Marshal.GetLastWin32Error());
                return;
            }

            try
            {
                for (uint index = 0; ; index++)
                {
                    Native.SP_DEVICE_INTERFACE_DATA ifd = new Native.SP_DEVICE_INTERFACE_DATA();
                    ifd.cbSize = (uint)Marshal.SizeOf(typeof(Native.SP_DEVICE_INTERFACE_DATA));
                    if (!Native.SetupDiEnumDeviceInterfaces(
                            set, IntPtr.Zero, ref guid, index, ref ifd))
                        break;

                    Native.SP_DEVINFO_DATA did = new Native.SP_DEVINFO_DATA();
                    did.cbSize = (uint)Marshal.SizeOf(typeof(Native.SP_DEVINFO_DATA));
                    string path = Native.GetDeviceInterfacePath(set, ref ifd, ref did);
                    if (string.IsNullOrEmpty(path))
                        continue;

                    string friendly = Native.GetFriendlyName(set, ref did);
                    string instanceId = Native.GetDeviceInstanceId(set, ref did);
                    if (TextUtil.IsSystemBatteryPath(path, friendly))
                    {
                        if (log != null)
                            log.Add("windows: skipped system battery " + friendly);
                        continue;
                    }

                    string upper = path.ToUpperInvariant();
                    bool peripheralPath =
                        upper.Contains("HID#") || upper.Contains("BTH") ||
                        upper.Contains("USB#") || upper.Contains("VID_");
                    if (!peripheralPath)
                    {
                        if (log != null)
                            log.Add("windows: skipped non-peripheral " + path);
                        continue;
                    }

                    BatteryReading reading = Query(path);
                    if (reading == null)
                    {
                        if (log != null)
                            log.Add("windows: query failed " + path);
                        continue;
                    }

                    string bus = Native.GetBusReportedName(set, ref did);
                    string desc = Native.GetDeviceDesc(set, ref did);
                    string name = DeviceNameResolver.Resolve(
                        instanceId, reading.DeviceName, friendly, bus, desc, log);
                    if (TextUtil.IsGenericDeviceName(name))
                        name = "无线设备";

                    WirelessDevice device = new WirelessDevice();
                    device.Id = "win:" + (instanceId ?? path);
                    device.Name = name;
                    device.Percent = reading.Percent;
                    device.Charging = reading.Charging;
                    device.Source = TextUtil.SourceFromPath(path);
                    device.Provider = "Windows";
                    device.InstanceId = path;
                    device.Detail = reading.Diagnostic;
                    device.SortOrder = device.Source == "蓝牙" ? 31 : 21;
                    result.Devices.Add(device);

                    if (log != null)
                        log.Add("windows: " + name + " " + reading.Diagnostic + " path=" + path);
                }
            }
            finally
            {
                Native.SetupDiDestroyDeviceInfoList(set);
            }
        }

        private static BatteryReading Query(string path)
        {
            IntPtr handle = Native.CreateFileW(
                path, Native.GENERIC_READ | Native.GENERIC_WRITE,
                Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE,
                IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero);
            if (handle == new IntPtr(-1))
            {
                handle = Native.CreateFileW(
                    path, Native.GENERIC_READ,
                    Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE,
                    IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero);
            }
            if (handle == new IntPtr(-1))
                return null;

            try
            {
                uint tag;
                int bytes;
                uint timeout = 5000;
                if (!Native.DeviceIoControl(
                        handle, Native.IOCTL_BATTERY_QUERY_TAG,
                        ref timeout, 4, out tag, 4, out bytes, IntPtr.Zero))
                    return null;

                Native.BATTERY_QUERY_INFORMATION query =
                    new Native.BATTERY_QUERY_INFORMATION();
                query.BatteryTag = tag;
                query.InformationLevel = Native.BATTERY_INFORMATION_LEVEL;
                query.AtRate = 0;

                byte[] infoBuffer = new byte[Marshal.SizeOf(typeof(Native.BATTERY_INFORMATION))];
                if (!Native.DeviceIoControl(
                        handle, Native.IOCTL_BATTERY_QUERY_INFORMATION,
                        ref query, Marshal.SizeOf(typeof(Native.BATTERY_QUERY_INFORMATION)),
                        infoBuffer, infoBuffer.Length, out bytes, IntPtr.Zero))
                    return null;

                Native.BATTERY_INFORMATION info;
                GCHandle pin = GCHandle.Alloc(infoBuffer, GCHandleType.Pinned);
                try
                {
                    info = (Native.BATTERY_INFORMATION)Marshal.PtrToStructure(
                        pin.AddrOfPinnedObject(), typeof(Native.BATTERY_INFORMATION));
                }
                finally
                {
                    pin.Free();
                }

                Native.BATTERY_WAIT_STATUS wait = new Native.BATTERY_WAIT_STATUS();
                wait.BatteryTag = tag;
                wait.Timeout = 5000;
                Native.BATTERY_STATUS status;
                if (!Native.DeviceIoControl(
                        handle, Native.IOCTL_BATTERY_QUERY_STATUS,
                        ref wait, Marshal.SizeOf(typeof(Native.BATTERY_WAIT_STATUS)),
                        out status, Marshal.SizeOf(typeof(Native.BATTERY_STATUS)),
                        out bytes, IntPtr.Zero))
                    return null;

                uint full = info.FullChargedCapacity;
                if (full == 0)
                    full = info.DesignedCapacity;

                int percent = -1;
                if (full > 0 && status.Capacity != 0xFFFFFFFF)
                {
                    double value = status.Capacity * 100.0 / full;
                    if (value > 100.0)
                        value = 100.0;
                    if (value < 0.0)
                        value = 0.0;
                    percent = (int)Math.Round(value);
                }

                BatteryReading reading = new BatteryReading();
                reading.Percent = percent;
                reading.Charging = (status.PowerState & Native.BATTERY_CHARGING) != 0;
                reading.PowerState = status.PowerState;
                reading.Capacity = status.Capacity;
                reading.FullCapacity = full;
                reading.DesignedCapacity = info.DesignedCapacity;
                reading.DeviceName = QueryDeviceName(handle, tag);
                reading.Diagnostic = string.Format(
                    CultureInfo.InvariantCulture,
                    "percent={0} charging={1} power=0x{2:X} capacity={3}/{4}",
                    percent, reading.Charging, status.PowerState,
                    status.Capacity, full);
                return reading;
            }
            catch (Exception)
            {
                return null;
            }
            finally
            {
                Native.CloseHandle(handle);
            }
        }

        private static string QueryDeviceName(IntPtr handle, uint tag)
        {
            try
            {
                Native.BATTERY_QUERY_INFORMATION query =
                    new Native.BATTERY_QUERY_INFORMATION();
                query.BatteryTag = tag;
                query.InformationLevel = Native.BATTERY_DEVICE_NAME_LEVEL;
                query.AtRate = 0;

                byte[] buffer = new byte[512];
                int bytes;
                if (!Native.DeviceIoControl(
                        handle, Native.IOCTL_BATTERY_QUERY_INFORMATION,
                        ref query, Marshal.SizeOf(typeof(Native.BATTERY_QUERY_INFORMATION)),
                        buffer, buffer.Length, out bytes, IntPtr.Zero))
                    return "";
                if (bytes <= 1)
                    return "";
                string value = Encoding.Unicode.GetString(buffer, 0, Math.Min(bytes, buffer.Length));
                int nul = value.IndexOf('\0');
                if (nul >= 0)
                    value = value.Substring(0, nul);
                return TextUtil.Clean(value);
            }
            catch (Exception)
            {
                return "";
            }
        }
    }

    internal sealed class BluetoothBatteryProvider
    {
        public void Scan(ScanResult result, List<string> log)
        {
            HashSet<string> seen = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

            Guid guid = Native.GUID_DEVCLASS_BLUETOOTH;
            IntPtr set = Native.SetupDiGetClassDevsW(
                ref guid, IntPtr.Zero, IntPtr.Zero, Native.DIGCF_PRESENT);
            if (set == new IntPtr(-1))
            {
                result.AddError("蓝牙设备枚举失败，错误码 " +
                    Marshal.GetLastWin32Error());
            }
            else
            {
                try
                {
                    ScanSet(set, result, log, seen);
                }
                finally
                {
                    Native.SetupDiDestroyDeviceInfoList(set);
                }
            }

            // Some Hands-Free/AG Audio battery nodes are not in the Bluetooth
            // device class. A second all-classes pass catches those.
            IntPtr all = Native.SetupDiGetClassDevsW(
                IntPtr.Zero, IntPtr.Zero, IntPtr.Zero,
                Native.DIGCF_PRESENT | Native.DIGCF_ALLCLASSES);
            if (all != new IntPtr(-1))
            {
                try
                {
                    ScanSet(all, result, log, seen);
                }
                finally
                {
                    Native.SetupDiDestroyDeviceInfoList(all);
                }
            }
        }

        private static void ScanSet(
            IntPtr set, ScanResult result, List<string> log,
            HashSet<string> seen)
        {
            for (uint index = 0; ; index++)
            {
                Native.SP_DEVINFO_DATA did = new Native.SP_DEVINFO_DATA();
                did.cbSize = (uint)Marshal.SizeOf(typeof(Native.SP_DEVINFO_DATA));
                if (!Native.SetupDiEnumDeviceInfo(set, index, ref did))
                    break;

                string instanceId = Native.GetDeviceInstanceId(set, ref did);
                if (string.IsNullOrEmpty(instanceId))
                    continue;
                string upper = instanceId.ToUpperInvariant();
                if (!upper.StartsWith("BTHENUM") &&
                    !upper.StartsWith("BTHLE") &&
                    !upper.StartsWith("BTH\\"))
                    continue;
                if (seen.Contains(instanceId))
                    continue;

                byte percent;
                if (!Native.TryReadByteProperty(
                        set, ref did, Native.DEVPKEY_Bluetooth_Battery, out percent))
                    continue;

                bool charging;
                Native.TryReadBoolProperty(
                    set, ref did, Native.DEVPKEY_Bluetooth_BatteryCharging, out charging);

                string[] categories = Native.ReadStringListProperty(
                    set, ref did, Native.DEVPKEY_DeviceContainer_Category);
                if (!TextUtil.LooksLikePeripheralCategory(categories))
                    continue;

                string name = Native.GetFriendlyName(set, ref did);
                if (string.IsNullOrEmpty(name))
                    name = "蓝牙设备";
                name = TextUtil.Clean(name);

                int level = percent == 0 ? -1 : Math.Min(100, (int)percent);
                WirelessDevice device = new WirelessDevice();
                device.Id = "bt:" + instanceId;
                device.Name = name;
                device.Percent = level;
                device.Charging = charging;
                device.Source = "蓝牙";
                device.Provider = "蓝牙";
                device.InstanceId = instanceId;
                device.SortOrder = IsHandsFree(name) ? 25 : 30;
                result.Devices.Add(device);
                seen.Add(instanceId);

                if (log != null)
                    log.Add("bluetooth: " + name + " percent=" + percent +
                        (level < 0 ? " (unknown/not connected)" : "") +
                        " charging=" + charging +
                        " categories=" + string.Join(",", categories));
            }
        }

        private static bool IsHandsFree(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            string lower = name.ToLowerInvariant();
            return lower.Contains("hands-free") ||
                lower.Contains("ag audio") ||
                name.Contains("免提");
        }
    }

    internal sealed class Scanner
    {
        private readonly HidppProvider _hidpp = new HidppProvider();
        private readonly AtkVxeProvider _atk = new AtkVxeProvider();
        private readonly DarkrimProvider _darkrim = new DarkrimProvider();

        public ScanResult Scan(bool mock, bool verbose)
        {
            ScanResult result = new ScanResult();
            List<string> log = new List<string>();

            if (mock)
            {
                result.Devices.Add(MockDevice(
                    "mock:g304", "G304 无线鼠标", 87, true, "2.4G", "HID++ 模拟", 10));
                result.Devices.Add(MockDevice(
                    "mock:mxkeys", "MX Keys Mini", 42, false, "2.4G", "HID++ 模拟", 11));
                result.Devices.Add(MockDevice(
                    "mock:lm129", "LM129 BT", 100, false, "蓝牙", "蓝牙模拟", 30));
                result.Devices.Add(MockDevice(
                    "mock:low", "低电量测试鼠标", 8, false, "2.4G", "模拟", 12));
                result.AddNote("模拟模式：显示 4 个假设备。");
            }
            else
            {
                try
                {
                    new WindowsBatteryProvider().Scan(result, log);
                }
                catch (Exception ex)
                {
                    result.AddError("Windows 电池接口: " + ex.Message);
                }

                try
                {
                    new BluetoothBatteryProvider().Scan(result, log);
                }
                catch (Exception ex)
                {
                    result.AddError("蓝牙属性: " + ex.Message);
                }

                try
                {
                    _atk.Scan(result, log);
                }
                catch (Exception ex)
                {
                    result.AddError("ATK/VXE: " + ex.Message);
                }

                try
                {
                    _darkrim.Scan(result, log);
                }
                catch (Exception ex)
                {
                    result.AddError("DARKRIM: " + ex.Message);
                }

                try
                {
                    _hidpp.Scan(result, log, verbose);
                }
                catch (Exception ex)
                {
                    result.AddError("罗技 HID++: " + ex.Message);
                }
            }

            List<WirelessDevice> devices = Deduplicate(result.Devices);
            EnsureUniqueNames(devices);
            result.Devices = devices;

            if (verbose)
            {
                for (int i = 0; i < log.Count; i++)
                    result.AddNote(log[i]);
            }

            return result;
        }

        private static WirelessDevice MockDevice(
            string id, string name, int percent, bool charging,
            string source, string provider, int order)
        {
            WirelessDevice d = new WirelessDevice();
            d.Id = id;
            d.Name = name;
            d.Percent = percent;
            d.Charging = charging;
            d.Source = source;
            d.Provider = provider;
            d.InstanceId = id;
            d.SortOrder = order;
            return d;
        }

        private static List<WirelessDevice> Deduplicate(List<WirelessDevice> input)
        {
            List<WirelessDevice> sorted = new List<WirelessDevice>(input);
            sorted.Sort(delegate(WirelessDevice a, WirelessDevice b)
            {
                int pa = Priority(a);
                int pb = Priority(b);
                if (pa != pb)
                    return pa.CompareTo(pb);
                int so = a.SortOrder.CompareTo(b.SortOrder);
                if (so != 0)
                    return so;
                return string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase);
            });

            bool hasHidpp = sorted.Exists(delegate(WirelessDevice d)
            {
                return d.Provider == "HID++";
            });
            bool hasAtk = sorted.Exists(delegate(WirelessDevice d)
            {
                return d.Provider == "ATK/VXE";
            });
            bool hasDarkrim = sorted.Exists(delegate(WirelessDevice d)
            {
                return d.Provider == "DARKRIM";
            });

            List<WirelessDevice> output = new List<WirelessDevice>();
            for (int i = 0; i < sorted.Count; i++)
            {
                WirelessDevice d = sorted[i];

                if (hasHidpp && d.Provider == "Windows" &&
                    TextUtil.IsLikelyLogitechPath(d.InstanceId))
                    continue;
                if (hasAtk && d.Provider == "Windows" &&
                    PathHasVid(d.InstanceId, "VID_373B"))
                    continue;
                if (hasDarkrim && d.Provider == "Windows" &&
                    PathHasVid(d.InstanceId, "VID_3654"))
                    continue;

                if (d.Provider == "Windows" && d.Source == "蓝牙")
                {
                    bool covered = sorted.Exists(delegate(WirelessDevice b)
                    {
                        return b.Provider == "蓝牙" &&
                            (NamesSimilar(b.Name, d.Name) ||
                             SameBluetoothAddress(b.InstanceId, d.InstanceId));
                    });
                    if (covered)
                        continue;
                }

                bool duplicate = false;
                for (int j = 0; j < output.Count; j++)
                {
                    WirelessDevice existing = output[j];
                    if (existing.Id == d.Id)
                    {
                        duplicate = true;
                        break;
                    }
                    if (NamesSimilar(existing.Name, d.Name) &&
                        Math.Abs(existing.Percent - d.Percent) <= 3)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate)
                    output.Add(d);
            }
            return output;
        }

        private static int Priority(WirelessDevice d)
        {
            if (d.Provider == "HID++" || d.Provider == "ATK/VXE" ||
                d.Provider == "DARKRIM")
                return 0;
            if (d.Provider == "蓝牙")
                return 1;
            if (d.Provider == "Windows")
                return 2;
            return 3;
        }

        private static bool PathHasVid(string path, string vid)
        {
            return !string.IsNullOrEmpty(path) &&
                path.IndexOf(vid, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool NamesSimilar(string a, string b)
        {
            string na = TextUtil.NormalizeName(a);
            string nb = TextUtil.NormalizeName(b);
            if (na.Length == 0 || nb.Length == 0)
                return false;
            if (na == nb)
                return true;
            if (na.Length >= 5 && nb.Length >= 5)
            {
                if (na.Contains(nb) || nb.Contains(na))
                    return true;
            }
            return false;
        }

        private static bool SameBluetoothAddress(string a, string b)
        {
            string aa = ExtractBluetoothAddress(a);
            string bb = ExtractBluetoothAddress(b);
            return aa.Length > 0 && aa == bb;
        }

        private static string ExtractBluetoothAddress(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return "";
            Match m = Regex.Match(
                instanceId, "DEV_([0-9A-Fa-f]{12})", RegexOptions.IgnoreCase);
            if (m.Success)
                return m.Groups[1].Value.ToUpperInvariant();
            m = Regex.Match(
                instanceId, "([0-9A-Fa-f]{12})", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value.ToUpperInvariant() : "";
        }

        private static void EnsureUniqueNames(List<WirelessDevice> devices)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(
                StringComparer.CurrentCultureIgnoreCase);
            for (int i = 0; i < devices.Count; i++)
            {
                string name = devices[i].Name;
                int count;
                if (!counts.TryGetValue(name, out count))
                {
                    counts[name] = 1;
                    continue;
                }
                count++;
                counts[name] = count;
                devices[i].Name = name + " (" + count + ")";
            }
        }
    }
}

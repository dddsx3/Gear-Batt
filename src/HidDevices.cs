using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace GearBatt
{
    internal sealed class HidDeviceInfo
    {
        public string Path = "";
        public string InstanceId = "";
        public string FriendlyName = "";
        public string BusReportedName = "";
        public string DeviceDesc = "";
        public string Product = "";
        public string Manufacturer = "";
        public ushort VendorId;
        public ushort ProductId;
        public ushort UsagePage;
        public ushort Usage;
        public int InputReportLength;
        public int OutputReportLength;
        public int FeatureReportLength;
        public string CapsSummary = "";

        public string ResolveName(string fallback)
        {
            string name = DeviceNameResolver.Resolve(
                InstanceId, Product, FriendlyName, BusReportedName, DeviceDesc, null);
            if (string.IsNullOrEmpty(name) || TextUtil.IsGenericDeviceName(name))
                return fallback;
            return name;
        }
    }

    internal static class HidDevices
    {
        public static List<HidDeviceInfo> Enumerate(bool includeDescriptor)
        {
            return Enumerate(includeDescriptor, null);
        }

        public static List<HidDeviceInfo> Enumerate(
            bool includeDescriptor, List<string> log)
        {
            List<HidDeviceInfo> result = new List<HidDeviceInfo>();
            Guid guid = Native.GUID_DEVINTERFACE_HID;
            IntPtr set = Native.SetupDiGetClassDevsW(
                ref guid, IntPtr.Zero, IntPtr.Zero,
                Native.DIGCF_PRESENT | Native.DIGCF_DEVICEINTERFACE);
            if (set == new IntPtr(-1))
                return result;

            try
            {
                for (uint index = 0; ; index++)
                {
                    Native.SP_DEVICE_INTERFACE_DATA ifd =
                        new Native.SP_DEVICE_INTERFACE_DATA();
                    ifd.cbSize = (uint)Marshal.SizeOf(
                        typeof(Native.SP_DEVICE_INTERFACE_DATA));
                    if (!Native.SetupDiEnumDeviceInterfaces(
                            set, IntPtr.Zero, ref guid, index, ref ifd))
                        break;

                    Native.SP_DEVINFO_DATA did = new Native.SP_DEVINFO_DATA();
                    did.cbSize = (uint)Marshal.SizeOf(
                        typeof(Native.SP_DEVINFO_DATA));
                    string path = Native.GetDeviceInterfacePath(
                        set, ref ifd, ref did);
                    if (string.IsNullOrEmpty(path))
                    {
                        if (log != null)
                            log.Add("hid: interface detail path unavailable at index " +
                                index + " err=" + Marshal.GetLastWin32Error());
                        continue;
                    }

                    HidDeviceInfo info = Read(path, includeDescriptor, log);
                    if (info == null)
                        continue;

                    info.InstanceId = Native.GetDeviceInstanceId(set, ref did);
                    if (string.IsNullOrEmpty(info.InstanceId))
                        info.InstanceId = path;
                    info.FriendlyName = Native.GetFriendlyName(set, ref did) ?? "";
                    info.BusReportedName = Native.GetBusReportedName(set, ref did) ?? "";
                    info.DeviceDesc = Native.GetDeviceDesc(set, ref did) ?? "";
                    result.Add(info);
                    if (log != null)
                        log.Add("hid: ok VID=" + info.VendorId.ToString("X4") +
                            " PID=" + info.ProductId.ToString("X4") +
                            " usage=" + info.UsagePage.ToString("X4") +
                            "/" + info.Usage.ToString("X4") +
                            " path=" + path);
                }
            }
            finally
            {
                Native.SetupDiDestroyDeviceInfoList(set);
            }

            result.Sort(delegate(HidDeviceInfo a, HidDeviceInfo b)
            {
                int cmp = a.VendorId.CompareTo(b.VendorId);
                if (cmp != 0) return cmp;
                cmp = a.ProductId.CompareTo(b.ProductId);
                if (cmp != 0) return cmp;
                cmp = a.UsagePage.CompareTo(b.UsagePage);
                if (cmp != 0) return cmp;
                cmp = a.Usage.CompareTo(b.Usage);
                if (cmp != 0) return cmp;
                return string.Compare(a.InstanceId, b.InstanceId,
                    StringComparison.OrdinalIgnoreCase);
            });
            return result;
        }

        private static HidDeviceInfo Read(
            string path, bool includeDescriptor, List<string> log)
        {
            IntPtr handle = Native.CreateFileW(
                path, Native.GENERIC_READ,
                Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE,
                IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero);
            if (handle == new IntPtr(-1))
            {
                handle = Native.CreateFileW(
                    path, 0,
                    Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE,
                    IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero);
            }
            if (handle == new IntPtr(-1))
            {
                if (log != null)
                    log.Add("hid: open failed err=" +
                        Marshal.GetLastWin32Error() + " path=" + path);
                return null;
            }

            try
            {
                Native.HIDD_ATTRIBUTES attributes = new Native.HIDD_ATTRIBUTES();
                attributes.Size = Marshal.SizeOf(typeof(Native.HIDD_ATTRIBUTES));
                if (!Native.HidD_GetAttributes(handle, ref attributes))
                {
                    if (log != null)
                        log.Add("hid: HidD_GetAttributes failed err=" +
                            Marshal.GetLastWin32Error() + " path=" + path);
                    return null;
                }

                HidDeviceInfo info = new HidDeviceInfo();
                info.Path = path;
                info.VendorId = attributes.VendorID;
                info.ProductId = attributes.ProductID;

                IntPtr preparsed = IntPtr.Zero;
                IntPtr caps = IntPtr.Zero;
                try
                {
                    if (!Native.HidD_GetPreparsedData(handle, out preparsed))
                    {
                        if (log != null)
                            log.Add("hid: HidD_GetPreparsedData failed err=" +
                                Marshal.GetLastWin32Error() + " path=" + path);
                        return null;
                    }
                    caps = Marshal.AllocHGlobal(256);
                    int capsStatus = Native.HidP_GetCaps(preparsed, caps);
                    if (capsStatus != Native.HIDP_STATUS_SUCCESS)
                    {
                        if (log != null)
                            log.Add("hid: HidP_GetCaps failed status=" +
                                capsStatus + " path=" + path);
                        return null;
                    }
                    info.Usage = (ushort)Marshal.ReadInt16(caps, 0);
                    info.UsagePage = (ushort)Marshal.ReadInt16(caps, 2);
                    info.InputReportLength = (ushort)Marshal.ReadInt16(caps, 4);
                    info.OutputReportLength = (ushort)Marshal.ReadInt16(caps, 6);
                    info.FeatureReportLength = (ushort)Marshal.ReadInt16(caps, 8);
                    if (includeDescriptor)
                        info.CapsSummary = BuildCapsSummary(preparsed, caps);
                }
                finally
                {
                    if (caps != IntPtr.Zero)
                        Marshal.FreeHGlobal(caps);
                    if (preparsed != IntPtr.Zero)
                        Native.HidD_FreePreparsedData(preparsed);
                }

                info.Product = ReadWideString(handle, true);
                info.Manufacturer = ReadWideString(handle, false);

                return info;
            }
            finally
            {
                Native.CloseHandle(handle);
            }
        }

        private static string ReadWideString(IntPtr handle, bool product)
        {
            byte[] buffer = new byte[512];
            bool ok = product
                ? Native.HidD_GetProductString(handle, buffer, buffer.Length)
                : Native.HidD_GetManufacturerString(handle, buffer, buffer.Length);
            if (!ok)
                return "";
            string value = Encoding.Unicode.GetString(buffer);
            int nul = value.IndexOf('\0');
            if (nul >= 0)
                value = value.Substring(0, nul);
            return TextUtil.Clean(value);
        }

        private static string BuildCapsSummary(IntPtr preparsed, IntPtr caps)
        {
            StringBuilder sb = new StringBuilder();
            AppendValueCaps(sb, preparsed, 0, "Input",
                (ushort)Marshal.ReadInt16(caps, 48));
            AppendValueCaps(sb, preparsed, 1, "Output",
                (ushort)Marshal.ReadInt16(caps, 54));
            AppendValueCaps(sb, preparsed, 2, "Feature",
                (ushort)Marshal.ReadInt16(caps, 60));
            return sb.ToString();
        }

        private static void AppendValueCaps(
            StringBuilder sb, IntPtr preparsed, int reportType,
            string label, ushort count)
        {
            if (count == 0)
                return;

            int capSize = Marshal.SizeOf(typeof(Native.HIDP_VALUE_CAPS));
            if (capSize <= 0)
                capSize = 96;
            IntPtr buffer = Marshal.AllocHGlobal(count * capSize);
            try
            {
                ushort length = count;
                int status = Native.HidP_GetValueCaps(
                    reportType, buffer, ref length, preparsed);
                if (status != Native.HIDP_STATUS_SUCCESS)
                {
                    sb.AppendLine("  " + label + " value caps: query failed 0x" +
                        status.ToString("X8"));
                    return;
                }

                sb.AppendLine("  " + label + " value caps (" + length + "):");
                for (int i = 0; i < length; i++)
                {
                    IntPtr item = IntPtr.Add(buffer, i * capSize);
                    Native.HIDP_VALUE_CAPS cap =
                        (Native.HIDP_VALUE_CAPS)Marshal.PtrToStructure(
                            item, typeof(Native.HIDP_VALUE_CAPS));
                    string usage = cap.IsRange
                        ? "0x" + cap.UsageMin.ToString("X4") +
                          "..0x" + cap.UsageMax.ToString("X4")
                        : "0x" + cap.UsageMin.ToString("X4");
                    sb.AppendLine("    report 0x" + cap.ReportID.ToString("X2") +
                        " usagePage 0x" + cap.UsagePage.ToString("X4") +
                        " usage " + usage +
                        " logical " + cap.LogicalMin + ".." + cap.LogicalMax +
                        " bits " + cap.BitSize +
                        " count " + cap.ReportCount);
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
    }
}

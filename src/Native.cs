using System;
using System.Runtime.InteropServices;
using System.Text;

namespace GearBatt
{
    internal static class Native
    {
        public const int DIGCF_PRESENT = 0x02;
        public const int DIGCF_DEVICEINTERFACE = 0x10;
        public const int DIGCF_ALLCLASSES = 0x04;
        public const int ERROR_INSUFFICIENT_BUFFER = 122;
        public const int ERROR_NO_MORE_ITEMS = 259;
        public const int ERROR_IO_PENDING = 997;

        public const uint GENERIC_READ = 0x80000000;
        public const uint GENERIC_WRITE = 0x40000000;
        public const uint FILE_SHARE_READ = 0x01;
        public const uint FILE_SHARE_WRITE = 0x02;
        public const uint OPEN_EXISTING = 3;
        public const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        public const uint FILE_FLAG_OVERLAPPED = 0x40000000;

        public const uint WAIT_OBJECT_0 = 0;
        public const uint WAIT_TIMEOUT = 258;
        public const uint INFINITE = 0xFFFFFFFF;

        public const uint IOCTL_HID_GET_REPORT_DESCRIPTOR = 0x000B0007;
        public const uint IOCTL_BATTERY_QUERY_TAG = 0x294040;
        public const uint IOCTL_BATTERY_QUERY_INFORMATION = 0x294044;
        public const uint IOCTL_BATTERY_QUERY_STATUS = 0x29404C;

        public const uint BATTERY_POWER_ON_LINE = 0x00000001;
        public const uint BATTERY_DISCHARGING = 0x00000002;
        public const uint BATTERY_CHARGING = 0x00000004;
        public const uint BATTERY_CRITICAL = 0x00000008;

        public const uint BATTERY_INFORMATION_LEVEL = 0;
        public const uint BATTERY_DEVICE_NAME_LEVEL = 4;

        public const int SM_CXSMICON = 49;
        public const int SM_CYSMICON = 50;
        public const uint ATTACH_PARENT_PROCESS = 0xFFFFFFFF;
        public const uint CM_LOCATE_DEVNODE_NORMAL = 0;
        public const int HIDP_STATUS_SUCCESS = 0x00110000;

        public const uint SPDRP_DEVICEDESC = 0x00000000;
        public const uint SPDRP_FRIENDLYNAME = 0x0000000C;

        public static readonly Guid GUID_DEVINTERFACE_BATTERY =
            new Guid("72631E54-78A4-11D0-BCF7-00AA00B7B32A");
        public static readonly Guid GUID_DEVINTERFACE_HID =
            new Guid("4D1E55B2-F16F-11CF-88CB-001111000030");
        public static readonly Guid GUID_DEVCLASS_BLUETOOTH =
            new Guid("E0CBF06C-CD8B-4647-BB8A-263B43F0F974");

        public static readonly DEVPROPKEY DEVPKEY_Device_FriendlyName =
            new DEVPROPKEY(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 14);
        public static readonly DEVPROPKEY DEVPKEY_Device_DeviceDesc =
            new DEVPROPKEY(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), 2);
        public static readonly DEVPROPKEY DEVPKEY_Device_BusReportedDeviceDesc =
            new DEVPROPKEY(new Guid("540B947E-8B40-45BC-A8A2-6A0B894CBDA2"), 4);
        public static readonly DEVPROPKEY DEVPKEY_Device_Parent =
            new DEVPROPKEY(new Guid("4340A6C5-93FA-4706-972C-7B648008A5A7"), 8);
        public static readonly DEVPROPKEY DEVPKEY_Device_InstanceId =
            new DEVPROPKEY(new Guid("78C34FC8-104A-4ACA-9EA4-524D52996E57"), 256);
        public static readonly DEVPROPKEY DEVPKEY_DeviceContainer_Category =
            new DEVPROPKEY(new Guid("78C34FC8-104A-4ACA-9EA4-524D52996E57"), 90);

        // Windows' private-but-stable Bluetooth battery property set.
        public static readonly DEVPROPKEY DEVPKEY_Bluetooth_Battery =
            new DEVPROPKEY(new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"), 2);
        public static readonly DEVPROPKEY DEVPKEY_Bluetooth_BatteryCharging =
            new DEVPROPKEY(new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"), 3);

        public const uint DEVPROP_TYPE_STRING = 0x00000012;
        public const uint DEVPROP_TYPE_STRING_LIST = 0x00002012;
        public const uint DEVPROP_TYPE_BYTE = 0x00000003;
        public const uint DEVPROP_TYPE_BOOLEAN = 0x00000011;
        public const uint DEVPROP_TYPE_FILETIME = 0x00000010;

        public static readonly DEVPROPKEY DEVPKEY_Bluetooth_BatteryUpdated =
            new DEVPROPKEY(new Guid("104EA319-6EE2-4701-BD47-8DDBF425BBE5"), 7);

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVINFO_DATA
        {
            public uint cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SP_DEVICE_INTERFACE_DATA
        {
            public uint cbSize;
            public Guid InterfaceClassGuid;
            public uint Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DEVPROPKEY
        {
            public Guid fmtid;
            public uint pid;

            public DEVPROPKEY(Guid fmtid, uint pid)
            {
                this.fmtid = fmtid;
                this.pid = pid;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BATTERY_QUERY_INFORMATION
        {
            public uint BatteryTag;
            public uint InformationLevel;
            public int AtRate;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BATTERY_WAIT_STATUS
        {
            public uint BatteryTag;
            public uint Timeout;
            public uint PowerState;
            public uint LowCapacity;
            public uint HighCapacity;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BATTERY_STATUS
        {
            public uint PowerState;
            public uint Capacity;
            public uint Voltage;
            public int Rate;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct BATTERY_INFORMATION
        {
            public uint Capabilities;
            public byte Technology;
            public byte Reserved1;
            public byte Reserved2;
            public byte Reserved3;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] Chemistry;
            public uint DesignedCapacity;
            public uint FullChargedCapacity;
            public uint DefaultAlert1;
            public uint DefaultAlert2;
            public uint CriticalBias;
            public uint CycleCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HIDD_ATTRIBUTES
        {
            public int Size;
            public ushort VendorID;
            public ushort ProductID;
            public ushort VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HIDP_VALUE_CAPS
        {
            public ushort UsagePage;
            public byte ReportID;
            [MarshalAs(UnmanagedType.U1)] public bool IsAlias;
            public ushort BitField;
            public ushort LinkCollection;
            public ushort LinkUsage;
            public ushort LinkUsagePage;
            [MarshalAs(UnmanagedType.U1)] public bool IsRange;
            [MarshalAs(UnmanagedType.U1)] public bool IsStringRange;
            [MarshalAs(UnmanagedType.U1)] public bool IsDesignatorRange;
            [MarshalAs(UnmanagedType.U1)] public bool IsAbsolute;
            [MarshalAs(UnmanagedType.U1)] public bool HasNull;
            public byte Reserved;
            public ushort BitSize;
            public ushort ReportCount;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 5)]
            public ushort[] Reserved2;
            public uint UnitsExp;
            public uint Units;
            public int LogicalMin;
            public int LogicalMax;
            public int PhysicalMin;
            public int PhysicalMax;
            public ushort UsageMin;
            public ushort UsageMax;
            public ushort StringMin;
            public ushort StringMax;
            public ushort DesignatorMin;
            public ushort DesignatorMax;
            public ushort DataIndexMin;
            public ushort DataIndexMax;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct OVERLAPPED
        {
            public IntPtr Internal;
            public IntPtr InternalHigh;
            public uint Offset;
            public uint OffsetHigh;
            public IntPtr hEvent;
        }

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevsW(
            ref Guid ClassGuid, IntPtr Enumerator, IntPtr hwndParent, uint Flags);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr SetupDiGetClassDevsW(
            IntPtr ClassGuid, IntPtr Enumerator, IntPtr hwndParent, uint Flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiDestroyDeviceInfoList(IntPtr DeviceInfoSet);

        [DllImport("setupapi.dll", SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInfo(
            IntPtr DeviceInfoSet, uint MemberIndex, ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiEnumDeviceInterfaces(
            IntPtr DeviceInfoSet, IntPtr DeviceInfoData, ref Guid InterfaceClassGuid,
            uint MemberIndex, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiGetDeviceInterfaceDetailW(
            IntPtr DeviceInfoSet, ref SP_DEVICE_INTERFACE_DATA DeviceInterfaceData,
            IntPtr DeviceInterfaceDetailData, int DeviceInterfaceDetailDataSize,
            out int RequiredSize, ref SP_DEVINFO_DATA DeviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiGetDeviceInstanceIdW(
            IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData,
            StringBuilder DeviceInstanceId, int DeviceInstanceIdSize, out int RequiredSize);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiGetDevicePropertyW(
            IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData,
            ref DEVPROPKEY PropertyKey, out uint PropertyType,
            byte[] PropertyBuffer, int PropertyBufferSize, out int RequiredSize, int Flags);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool SetupDiGetDeviceRegistryPropertyW(
            IntPtr DeviceInfoSet, ref SP_DEVINFO_DATA DeviceInfoData, uint Property,
            out uint PropertyRegDataType, byte[] PropertyBuffer,
            int PropertyBufferSize, out int RequiredSize);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        public static extern uint CM_Locate_DevNodeW(
            out uint pdnDevInst, string pDeviceID, uint ulFlags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        public static extern uint CM_Get_DevNode_PropertyW(
            uint dnDevInst, ref DEVPROPKEY PropertyKey, out uint PropertyType,
            byte[] PropertyBuffer, ref uint PropertyBufferSize, uint ulFlags);

        [DllImport("cfgmgr32.dll")]
        public static extern uint CM_Get_Parent(
            out uint pdnDevInst, uint dnDevInst, uint ulFlags);

        [DllImport("cfgmgr32.dll", CharSet = CharSet.Unicode)]
        public static extern uint CM_Get_Device_IDW(
            uint dnDevInst, StringBuilder Buffer, uint BufferLen, uint ulFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern IntPtr CreateFileW(
            string lpFileName, uint dwDesiredAccess, uint dwShareMode,
            IntPtr lpSecurityAttributes, uint dwCreationDisposition,
            uint dwFlagsAndAttributes, IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool DeviceIoControl(
            IntPtr hDevice, uint dwIoControlCode, ref uint lpInBuffer, int nInBufferSize,
            out uint lpOutBuffer, int nOutBufferSize, out int lpBytesReturned,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool DeviceIoControl(
            IntPtr hDevice, uint dwIoControlCode,
            ref BATTERY_QUERY_INFORMATION lpInBuffer, int nInBufferSize,
            byte[] lpOutBuffer, int nOutBufferSize, out int lpBytesReturned,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool DeviceIoControl(
            IntPtr hDevice, uint dwIoControlCode,
            ref BATTERY_WAIT_STATUS lpInBuffer, int nInBufferSize,
            out BATTERY_STATUS lpOutBuffer, int nOutBufferSize,
            out int lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool DeviceIoControl(
            IntPtr hDevice, uint dwIoControlCode,
            IntPtr lpInBuffer, int nInBufferSize,
            byte[] lpOutBuffer, int nOutBufferSize,
            out int lpBytesReturned, IntPtr lpOverlapped);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetAttributes(
            IntPtr HidDeviceObject, ref HIDD_ATTRIBUTES Attributes);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_GetPreparsedData(
            IntPtr HidDeviceObject, out IntPtr PreparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern bool HidD_FreePreparsedData(IntPtr PreparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        public static extern int HidP_GetCaps(IntPtr PreparsedData, IntPtr Capabilities);

        [DllImport("hid.dll")]
        public static extern int HidP_GetValueCaps(
            int ReportType, IntPtr ValueCaps, ref ushort ValueCapsLength,
            IntPtr PreparsedData);

        [DllImport("hid.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool HidD_GetProductString(
            IntPtr HidDeviceObject, byte[] Buffer, int BufferLength);

        [DllImport("hid.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        public static extern bool HidD_GetManufacturerString(
            IntPtr HidDeviceObject, byte[] Buffer, int BufferLength);


        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern IntPtr CreateEventW(
            IntPtr lpEventAttributes, bool bManualReset, bool bInitialState, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ResetEvent(IntPtr hEvent);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool GetOverlappedResult(
            IntPtr hFile, ref OVERLAPPED lpOverlapped,
            out int lpNumberOfBytesTransferred, bool bWait);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool CancelIoEx(IntPtr hFile, ref OVERLAPPED lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool WriteFile(
            IntPtr hFile, byte[] lpBuffer, int nNumberOfBytesToWrite,
            out int lpNumberOfBytesWritten, ref OVERLAPPED lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool ReadFile(
            IntPtr hFile, byte[] lpBuffer, int nNumberOfBytesToRead,
            out int lpNumberOfBytesRead, ref OVERLAPPED lpOverlapped);

        [DllImport("kernel32.dll")]
        public static extern ushort GetUserDefaultUILanguage();

        [DllImport("user32.dll")]
        public static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetProcessDpiAwarenessContext(IntPtr value);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AttachConsole(uint dwProcessId);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool AllocConsole();

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern bool FreeConsole();

        public static IntPtr DpiAwarenessPerMonitorV2
        {
            get { return new IntPtr(-4); }
        }

        public static void EnableDpiAwareness()
        {
            try
            {
                if (SetProcessDpiAwarenessContext(DpiAwarenessPerMonitorV2))
                    return;
            }
            catch (Exception)
            {
            }
            try
            {
                SetProcessDPIAware();
            }
            catch (Exception)
            {
            }
        }

        public static string ReadStringProperty(
            IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData,
            DEVPROPKEY key)
        {
            byte[] buffer = new byte[2048];
            uint type;
            int required;
            if (!SetupDiGetDevicePropertyW(
                    deviceInfoSet, ref deviceInfoData, ref key, out type,
                    buffer, buffer.Length, out required, 0))
                return null;
            if (type != DEVPROP_TYPE_STRING || required <= 2)
                return null;
            string value = Encoding.Unicode.GetString(buffer, 0, Math.Min(required, buffer.Length));
            int nul = value.IndexOf('\0');
            if (nul >= 0)
                value = value.Substring(0, nul);
            return value.Trim();
        }

        public static string[] ReadStringListProperty(
            IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData,
            DEVPROPKEY key)
        {
            byte[] buffer = new byte[4096];
            uint type;
            int required;
            if (!SetupDiGetDevicePropertyW(
                    deviceInfoSet, ref deviceInfoData, ref key, out type,
                    buffer, buffer.Length, out required, 0))
                return new string[0];
            if (type != DEVPROP_TYPE_STRING_LIST || required <= 2)
                return new string[0];
            string raw = Encoding.Unicode.GetString(buffer, 0, Math.Min(required, buffer.Length));
            return raw.Split(new char[] { '\0' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static bool TryReadByteProperty(
            IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData,
            DEVPROPKEY key, out byte value)
        {
            byte[] buffer = new byte[16];
            uint type;
            int required;
            value = 0;
            if (!SetupDiGetDevicePropertyW(
                    deviceInfoSet, ref deviceInfoData, ref key, out type,
                    buffer, buffer.Length, out required, 0))
                return false;
            if (type != DEVPROP_TYPE_BYTE || required < 1)
                return false;
            value = buffer[0];
            return true;
        }

        public static bool TryReadBoolProperty(
            IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData,
            DEVPROPKEY key, out bool value)
        {
            byte[] buffer = new byte[16];
            uint type;
            int required;
            value = false;
            if (!SetupDiGetDevicePropertyW(
                    deviceInfoSet, ref deviceInfoData, ref key, out type,
                    buffer, buffer.Length, out required, 0))
                return false;
            if (type != DEVPROP_TYPE_BOOLEAN || required < 1)
                return false;
            value = buffer[0] != 0;
            return true;
        }

        public static bool TryReadFileTimeProperty(
            IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData,
            DEVPROPKEY key, out DateTime value)
        {
            byte[] buffer = new byte[16];
            uint type;
            int required;
            value = DateTime.MinValue;
            if (!SetupDiGetDevicePropertyW(
                    deviceInfoSet, ref deviceInfoData, ref key, out type,
                    buffer, buffer.Length, out required, 0))
                return false;
            if (type != DEVPROP_TYPE_FILETIME || required < 8)
                return false;
            long fileTime = BitConverter.ToInt64(buffer, 0);
            if (fileTime <= 0)
                return false;
            try
            {
                value = DateTime.FromFileTimeUtc(fileTime).ToLocalTime();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static string ReadStringPropertyCm(uint devInst, DEVPROPKEY key)
        {
            byte[] buffer = new byte[2048];
            uint type;
            uint size = (uint)buffer.Length;
            uint cr = CM_Get_DevNode_PropertyW(devInst, ref key, out type, buffer, ref size, 0);
            if (cr != 0 || type != DEVPROP_TYPE_STRING || size <= 2)
                return null;
            string value = Encoding.Unicode.GetString(buffer, 0, (int)Math.Min(size, (uint)buffer.Length));
            int nul = value.IndexOf('\0');
            if (nul >= 0)
                value = value.Substring(0, nul);
            return value.Trim();
        }

        public static string GetDeviceInstanceId(
            IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData)
        {
            StringBuilder sb = new StringBuilder(512);
            int required;
            if (SetupDiGetDeviceInstanceIdW(
                    deviceInfoSet, ref deviceInfoData, sb, sb.Capacity, out required))
                return sb.ToString();
            return null;
        }

        public static string GetDeviceInterfacePath(
            IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA interfaceData,
            ref SP_DEVINFO_DATA deviceInfoData)
        {
            int required;
            SetupDiGetDeviceInterfaceDetailW(
                deviceInfoSet, ref interfaceData, IntPtr.Zero, 0, out required, ref deviceInfoData);
            if (required <= 0)
                return null;

            IntPtr detail = Marshal.AllocHGlobal(required);
            try
            {
                int detailSize = IntPtr.Size == 8 ? 8 : 6;
                Marshal.WriteInt32(detail, 0, detailSize);
                if (!SetupDiGetDeviceInterfaceDetailW(
                        deviceInfoSet, ref interfaceData, detail, required, out required, ref deviceInfoData))
                    return null;
                // DevicePath follows the DWORD cbSize directly. The documented
                // x64 cbSize is 8, but the string itself starts at offset 4.
                const int pathOffset = 4;
                return Marshal.PtrToStringUni(IntPtr.Add(detail, pathOffset));
            }
            finally
            {
                Marshal.FreeHGlobal(detail);
            }
        }

        public static string GetFriendlyName(
            IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData)
        {
            string value = ReadStringProperty(deviceInfoSet, ref deviceInfoData, DEVPKEY_Device_FriendlyName);
            if (!string.IsNullOrEmpty(value))
                return value;

            byte[] buffer = new byte[2048];
            uint type;
            int required;
            if (SetupDiGetDeviceRegistryPropertyW(
                    deviceInfoSet, ref deviceInfoData, SPDRP_FRIENDLYNAME,
                    out type, buffer, buffer.Length, out required)
                && required > 2)
            {
                value = Encoding.Unicode.GetString(buffer, 0, Math.Min(required, buffer.Length));
                int nul = value.IndexOf('\0');
                if (nul >= 0)
                    value = value.Substring(0, nul);
                return value.Trim();
            }
            return null;
        }

        public static string GetBusReportedName(
            IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData)
        {
            return ReadStringProperty(
                deviceInfoSet, ref deviceInfoData, DEVPKEY_Device_BusReportedDeviceDesc);
        }

        public static string GetDeviceDesc(
            IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData)
        {
            return ReadStringProperty(
                deviceInfoSet, ref deviceInfoData, DEVPKEY_Device_DeviceDesc);
        }

        public static string GetParentInstanceId(
            IntPtr deviceInfoSet, ref SP_DEVINFO_DATA deviceInfoData)
        {
            return ReadStringProperty(deviceInfoSet, ref deviceInfoData, DEVPKEY_Device_Parent);
        }
    }
}

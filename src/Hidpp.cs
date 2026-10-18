using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace GearBatt
{
    internal sealed class HidppBattery
    {
        public int Percent = -1;
        public bool Charging;
        public bool Approximate;
        public string Detail = "";
    }

    internal sealed class HidppResponse
    {
        public bool IsError;
        public byte ErrorCode;
        public byte[] Parameters = new byte[0];
        public byte[] RawRequest = new byte[0];
        public byte[] RawResponse = new byte[0];
    }

    internal sealed class HidppDeviceRef
    {
        public string Path = "";
        public byte DeviceIndex;
        public ushort FeatureId;
        public byte FeatureIndex;
        public string Name = "";
        public string ReceiverKey = "";
        public string ReceiverName = "";
        public string Source = "2.4G";
    }

    internal sealed class HidppConnection : IDisposable
    {
        private byte _softwareId = 0x08;

        private IntPtr _handle = new IntPtr(-1);
        private IntPtr _event = IntPtr.Zero;
        private int _inputLength;
        private int _outputLength;

        public string Path = "";
        public ushort VendorId;
        public ushort ProductId;
        public ushort UsagePage;
        public ushort Usage;

        public bool IsOpen
        {
            get { return _handle != new IntPtr(-1); }
        }

        public int InputReportLength
        {
            get { return _inputLength; }
        }

        public int OutputReportLength
        {
            get { return _outputLength; }
        }

        public static HidppConnection Open(string path)
        {
            HidppConnection connection = new HidppConnection();
            connection.Path = path;
            connection._handle = Native.CreateFileW(
                path,
                Native.GENERIC_READ | Native.GENERIC_WRITE,
                Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE,
                IntPtr.Zero, Native.OPEN_EXISTING,
                Native.FILE_FLAG_OVERLAPPED, IntPtr.Zero);
            if (connection._handle == new IntPtr(-1))
                return null;

            connection._event = Native.CreateEventW(
                IntPtr.Zero, true, false, null);
            if (connection._event == IntPtr.Zero)
            {
                connection.Dispose();
                return null;
            }

            Native.HIDD_ATTRIBUTES attributes = new Native.HIDD_ATTRIBUTES();
            attributes.Size = Marshal.SizeOf(typeof(Native.HIDD_ATTRIBUTES));
            if (!Native.HidD_GetAttributes(connection._handle, ref attributes))
            {
                connection.Dispose();
                return null;
            }
            connection.VendorId = attributes.VendorID;
            connection.ProductId = attributes.ProductID;

            IntPtr preparsed = IntPtr.Zero;
            IntPtr caps = IntPtr.Zero;
            try
            {
                if (!Native.HidD_GetPreparsedData(connection._handle, out preparsed))
                {
                    connection.Dispose();
                    return null;
                }
                caps = Marshal.AllocHGlobal(256);
                int status = Native.HidP_GetCaps(preparsed, caps);
                if (status != Native.HIDP_STATUS_SUCCESS)
                {
                    connection.Dispose();
                    return null;
                }
                connection.Usage = (ushort)Marshal.ReadInt16(caps, 0);
                connection.UsagePage = (ushort)Marshal.ReadInt16(caps, 2);
                connection._inputLength = (ushort)Marshal.ReadInt16(caps, 4);
                connection._outputLength = (ushort)Marshal.ReadInt16(caps, 6);
                if (connection._inputLength <= 0 || connection._outputLength <= 0)
                {
                    connection.Dispose();
                    return null;
                }
                return connection;
            }
            finally
            {
                if (caps != IntPtr.Zero)
                    Marshal.FreeHGlobal(caps);
                if (preparsed != IntPtr.Zero)
                    Native.HidD_FreePreparsedData(preparsed);
            }
        }

        public HidppResponse Request(
            byte deviceIndex, byte featureIndex, byte function,
            byte[] parameters, int timeoutMs)
        {
            if (!IsOpen)
                return null;

            bool primaryLong = _outputLength >= 20;
            int firstTimeout = Math.Max(80, timeoutMs * 2 / 3);
            int secondTimeout = Math.Max(60, timeoutMs - firstTimeout);

            HidppResponse response = RequestOnce(
                deviceIndex, featureIndex, function, parameters,
                primaryLong, firstTimeout);
            if (response != null)
                return response;

            if (_outputLength >= 20)
            {
                // A few receivers answer only the short report form.
                response = RequestOnce(
                    deviceIndex, featureIndex, function, parameters,
                    false, secondTimeout);
            }
            return response;
        }

        private HidppResponse RequestOnce(
            byte deviceIndex, byte featureIndex, byte function,
            byte[] parameters, bool useLong, int timeoutMs)
        {
            _softwareId++;
            if (_softwareId < 0x08 || _softwareId > 0x0F)
                _softwareId = 0x08;
            byte softwareId = _softwareId;

            int length = useLong ? 20 : 7;
            if (_outputLength < length)
                length = _outputLength;
            if (length < 4)
                return null;

            byte[] report = new byte[length];
            report[0] = useLong ? (byte)0x11 : (byte)0x10;
            report[1] = deviceIndex;
            report[2] = featureIndex;
            report[3] = (byte)(((function & 0x0F) << 4) | softwareId);
            if (parameters != null && parameters.Length > 0)
            {
                int copy = Math.Min(parameters.Length, length - 4);
                Array.Copy(parameters, 0, report, 4, copy);
            }

            if (!WriteReport(report, timeoutMs))
                return null;

            long deadline = Environment.TickCount + timeoutMs;
            byte[] buffer = new byte[Math.Max(64, _inputLength + 8)];
            while (true)
            {
                int remaining = (int)(deadline - Environment.TickCount);
                if (remaining <= 0)
                    return null;

                int read = ReadReport(buffer, remaining);
                if (read < 4)
                    continue;

                byte reportId = buffer[0];
                if (reportId != 0x10 && reportId != 0x11)
                    continue;

                byte responseDevice = buffer[1];
                byte responseFeature = buffer[2];
                byte functionAndSoftware = buffer[3];
                byte software = (byte)(functionAndSoftware & 0x0F);
                byte responseFunction = (byte)((functionAndSoftware >> 4) & 0x0F);

                if (responseDevice != deviceIndex || software != softwareId)
                    continue;

                byte[] rawResponse = new byte[read];
                Array.Copy(buffer, 0, rawResponse, 0, read);

                if (responseFeature == 0xFF)
                {
                    HidppResponse error = new HidppResponse();
                    error.IsError = true;
                    error.ErrorCode = read > 4 ? buffer[4] : (byte)0;
                    error.RawRequest = report;
                    error.RawResponse = rawResponse;
                    return error;
                }

                if (responseFeature != featureIndex || responseFunction != function)
                    continue;

                HidppResponse response = new HidppResponse();
                response.IsError = false;
                int parameterLength = Math.Min(read - 4, buffer.Length - 4);
                if (parameterLength < 0)
                    parameterLength = 0;
                response.Parameters = new byte[parameterLength];
                Array.Copy(buffer, 4, response.Parameters, 0, parameterLength);
                response.RawRequest = report;
                response.RawResponse = rawResponse;
                return response;
            }
        }

        public bool WriteRaw(byte[] report, int timeoutMs)
        {
            return WriteReport(report, timeoutMs);
        }

        public int ReadRaw(byte[] buffer, int timeoutMs)
        {
            return ReadReport(buffer, timeoutMs);
        }

        private bool WriteReport(byte[] report, int timeoutMs)
        {
            Native.OVERLAPPED overlapped = new Native.OVERLAPPED();
            overlapped.hEvent = _event;
            Native.ResetEvent(_event);

            int written;
            bool ok = Native.WriteFile(
                _handle, report, report.Length, out written, ref overlapped);
            if (!ok)
            {
                int error = Marshal.GetLastWin32Error();
                if (error != Native.ERROR_IO_PENDING)
                    return false;
                uint wait = Native.WaitForSingleObject(_event, (uint)Math.Max(1, timeoutMs));
                if (wait != Native.WAIT_OBJECT_0)
                {
                    Native.CancelIoEx(_handle, ref overlapped);
                    Native.WaitForSingleObject(_event, 100);
                    int ignored;
                    Native.GetOverlappedResult(_handle, ref overlapped, out ignored, false);
                    return false;
                }
                if (!Native.GetOverlappedResult(_handle, ref overlapped, out written, false))
                    return false;
            }
            return written > 0;
        }

        private int ReadReport(byte[] buffer, int timeoutMs)
        {
            Native.OVERLAPPED overlapped = new Native.OVERLAPPED();
            overlapped.hEvent = _event;
            Native.ResetEvent(_event);

            int read;
            bool ok = Native.ReadFile(
                _handle, buffer, buffer.Length, out read, ref overlapped);
            if (!ok)
            {
                int error = Marshal.GetLastWin32Error();
                if (error != Native.ERROR_IO_PENDING)
                    return -1;
                uint wait = Native.WaitForSingleObject(_event, (uint)Math.Max(1, timeoutMs));
                if (wait != Native.WAIT_OBJECT_0)
                {
                    Native.CancelIoEx(_handle, ref overlapped);
                    Native.WaitForSingleObject(_event, 100);
                    int ignored;
                    Native.GetOverlappedResult(_handle, ref overlapped, out ignored, false);
                    return -1;
                }
                if (!Native.GetOverlappedResult(_handle, ref overlapped, out read, false))
                    return -1;
            }
            return read;
        }

        public void Dispose()
        {
            if (_handle != new IntPtr(-1))
            {
                Native.CloseHandle(_handle);
                _handle = new IntPtr(-1);
            }
            if (_event != IntPtr.Zero)
            {
                Native.CloseHandle(_event);
                _event = IntPtr.Zero;
            }
        }
    }

    internal sealed class HidppProvider
    {
        private readonly object _sync = new object();
        private List<HidppDeviceRef> _cache = new List<HidppDeviceRef>();
        private DateTime _lastDiscovery = DateTime.MinValue;

        public void Scan(ScanResult result, List<string> log, bool verbose)
        {
            lock (_sync)
            {
                if (_cache.Count == 0 ||
                    (DateTime.UtcNow - _lastDiscovery).TotalSeconds >= 60)
                {
                    Discover(log, verbose);
                }

                Dictionary<string, List<HidppDeviceRef>> byPath =
                    new Dictionary<string, List<HidppDeviceRef>>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < _cache.Count; i++)
                {
                    HidppDeviceRef reference = _cache[i];
                    List<HidppDeviceRef> group;
                    if (!byPath.TryGetValue(reference.Path, out group))
                    {
                        group = new List<HidppDeviceRef>();
                        byPath[reference.Path] = group;
                    }
                    group.Add(reference);
                }

                List<HidppDeviceRef> dead = new List<HidppDeviceRef>();
                foreach (KeyValuePair<string, List<HidppDeviceRef>> pair in byPath)
                {
                    using (HidppConnection connection = HidppConnection.Open(pair.Key))
                    {
                        if (connection == null)
                        {
                            dead.AddRange(pair.Value);
                            if (verbose)
                                log.Add("hidpp: open failed " + pair.Key);
                            continue;
                        }

                        for (int i = 0; i < pair.Value.Count; i++)
                        {
                            HidppDeviceRef reference = pair.Value[i];
                            HidppBattery battery = ReadBattery(connection, reference, 350);
                            if (battery == null)
                            {
                                dead.Add(reference);
                                if (verbose)
                                    log.Add("hidpp: battery read failed " +
                                        reference.Name + " #" + reference.DeviceIndex);
                                continue;
                            }

                            WirelessDevice device = new WirelessDevice();
                            device.Id = "hidpp:" + reference.ReceiverKey + ":" + reference.DeviceIndex;
                            device.Name = reference.Name;
                            device.Percent = battery.Percent;
                            device.PercentApproximate = battery.Approximate;
                            device.Charging = battery.Charging;
                            device.Source = reference.Source;
                            device.Provider = "HID++";
                            device.InstanceId = reference.Path;
                            device.Detail = battery.Detail;
                            device.SortOrder = reference.Source == "蓝牙" ? 29 : 10;
                            result.Devices.Add(device);

                            if (verbose)
                                log.Add("hidpp: " + reference.Name + " " + battery.Detail);
                        }
                    }
                }

                if (dead.Count > 0)
                {
                    for (int i = 0; i < dead.Count; i++)
                        _cache.Remove(dead[i]);
                    if (_cache.Count == 0)
                        _lastDiscovery = DateTime.MinValue;
                }
            }
        }

        private void Discover(List<string> log, bool verbose)
        {
            List<HidppDeviceRef> found = new List<HidppDeviceRef>();
            Guid guid = Native.GUID_DEVINTERFACE_HID;
            IntPtr set = Native.SetupDiGetClassDevsW(
                ref guid, IntPtr.Zero, IntPtr.Zero,
                Native.DIGCF_PRESENT | Native.DIGCF_DEVICEINTERFACE);
            if (set == new IntPtr(-1))
            {
                if (verbose)
                    log.Add("hidpp: HID enumeration failed");
                _lastDiscovery = DateTime.UtcNow;
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

                    using (HidppConnection connection = HidppConnection.Open(path))
                    {
                        if (connection == null)
                            continue;
                        if (connection.VendorId != 0x046D)
                            continue;
                        if (connection.UsagePage != 0xFF00)
                            continue;

                        if (verbose)
                            log.Add("hidpp: candidate usage=" + connection.Usage +
                                " vid=" + connection.VendorId.ToString("X4") +
                                " pid=" + connection.ProductId.ToString("X4"));

                        string instanceId = Native.GetDeviceInstanceId(set, ref did);
                        string receiverKey = GetReceiverKey(instanceId);
                        string transport = IsBluetoothKey(receiverKey) ? "蓝牙" : "2.4G";
                        string receiverName = ResolveReceiverName(
                            set, ref did, instanceId, log);

                        List<byte> indexes = new List<byte>();
                        if (transport == "蓝牙")
                        {
                            indexes.Add(0xFF);
                            indexes.Add(1);
                        }
                        else
                        {
                            for (byte i = 1; i <= 6; i++)
                                indexes.Add(i);
                        }

                        bool foundAny = false;
                        for (int i = 0; i < indexes.Count; i++)
                        {
                            byte deviceIndex = indexes[i];
                            HidppDeviceRef reference = ProbeDevice(
                                connection, deviceIndex, path, receiverKey,
                                receiverName, transport, log, verbose);
                            if (reference == null)
                                continue;

                            bool duplicate = false;
                            for (int j = 0; j < found.Count; j++)
                            {
                                if (found[j].ReceiverKey == reference.ReceiverKey &&
                                    found[j].DeviceIndex == reference.DeviceIndex)
                                {
                                    duplicate = true;
                                    break;
                                }
                            }
                            if (!duplicate)
                                found.Add(reference);
                            foundAny = true;
                        }

                        if (!foundAny && transport != "蓝牙")
                        {
                            HidppDeviceRef reference = ProbeDevice(
                                connection, 0xFF, path, receiverKey,
                                receiverName, transport, log, verbose);
                            if (reference != null)
                                found.Add(reference);
                        }
                    }
                }
            }
            finally
            {
                Native.SetupDiDestroyDeviceInfoList(set);
            }

            _cache = found;
            _lastDiscovery = DateTime.UtcNow;

            if (verbose)
                log.Add("hidpp: discovered " + found.Count + " device(s)");
        }

        private static HidppDeviceRef ProbeDevice(
            HidppConnection connection, byte deviceIndex, string path,
            string receiverKey, string receiverName, string transport,
            List<string> log, bool verbose)
        {
            byte featureIndex = GetFeature(connection, deviceIndex, 0x1000, 220);
            ushort featureId = 0x1000;
            if (featureIndex == 0)
            {
                featureIndex = GetFeature(connection, deviceIndex, 0x1004, 220);
                featureId = 0x1004;
            }
            if (featureIndex == 0)
            {
                featureIndex = GetFeature(connection, deviceIndex, 0x1001, 220);
                featureId = 0x1001;
            }
            if (featureIndex == 0)
            {
                if (verbose)
                    log.Add("hidpp: index " + deviceIndex + " has no battery feature");
                return null;
            }

            string name = "";
            byte nameFeature = GetFeature(connection, deviceIndex, 0x0005, 220);
            if (nameFeature != 0)
                name = ReadDeviceName(connection, deviceIndex, nameFeature, 250);
            if (string.IsNullOrEmpty(name))
            {
                if (string.IsNullOrEmpty(receiverName) ||
                    TextUtil.IsGenericDeviceName(receiverName))
                    name = "罗技设备 #" + deviceIndex;
                else
                    name = receiverName + " #" + deviceIndex;
            }

            HidppDeviceRef reference = new HidppDeviceRef();
            reference.Path = path;
            reference.DeviceIndex = deviceIndex;
            reference.FeatureId = featureId;
            reference.FeatureIndex = featureIndex;
            reference.Name = TextUtil.Clean(name);
            reference.ReceiverKey = receiverKey;
            reference.ReceiverName = receiverName;
            reference.Source = transport;

            if (verbose)
                log.Add("hidpp: found " + reference.Name + " idx=" + deviceIndex +
                    " feature=0x" + featureId.ToString("X4") +
                    " featureIndex=" + featureIndex);
            return reference;
        }

        private static byte GetFeature(
            HidppConnection connection, byte deviceIndex, ushort featureId, int timeout)
        {
            byte[] parameters = new byte[2];
            parameters[0] = (byte)(featureId >> 8);
            parameters[1] = (byte)featureId;
            HidppResponse response = connection.Request(
                deviceIndex, 0x00, 0x00, parameters, timeout);
            if (response == null || response.IsError || response.Parameters.Length < 1)
                return 0;
            return response.Parameters[0];
        }

        private static HidppBattery ReadBattery(
            HidppConnection connection, HidppDeviceRef reference, int timeout)
        {
            HidppResponse response = connection.Request(
                reference.DeviceIndex, reference.FeatureIndex, 0x00,
                new byte[0], timeout);
            if (response == null || response.IsError)
                return null;

            if (reference.FeatureId == 0x1000)
                return ParseBatteryStatus(response.Parameters);
            if (reference.FeatureId == 0x1004)
                return ParseUnifiedBattery(response.Parameters);
            if (reference.FeatureId == 0x1001)
                return ParseBatteryVoltage(response.Parameters);
            return null;
        }

        internal static HidppBattery ParseBatteryStatus(byte[] p)
        {
            if (p == null || p.Length < 3)
                return null;
            HidppBattery battery = new HidppBattery();
            int level = p[0];
            byte status = p[2];
            battery.Charging = status == 1 || status == 2 ||
                status == 3 || status == 4;
            if (level == 0)
                battery.Percent = -1;
            else
                battery.Percent = Math.Min(100, level);
            battery.Detail = string.Format(
                CultureInfo.InvariantCulture,
                "0x1000 level={0} next={1} status={2}",
                p[0], p[1], status);
            return battery;
        }

        internal static HidppBattery ParseUnifiedBattery(byte[] p)
        {
            if (p == null || p.Length < 1)
                return null;
            HidppBattery battery = new HidppBattery();
            int discharge = p[0];
            byte level = p.Length > 1 ? p[1] : (byte)0;
            byte status = p.Length > 2 ? p[2] : (byte)0;
            byte ignored = p.Length > 3 ? p[3] : (byte)0;
            battery.Charging = status >= 1 && status <= 4;
            if (discharge > 0 && discharge <= 100)
            {
                battery.Percent = discharge;
            }
            else
            {
                battery.Approximate = true;
                switch (level)
                {
                    case 8: battery.Percent = 90; break;
                    case 4: battery.Percent = 50; break;
                    case 2: battery.Percent = 20; break;
                    case 1: battery.Percent = 5; break;
                    case 0: battery.Percent = 0; break;
                    default: battery.Percent = -1; break;
                }
            }
            battery.Detail = string.Format(
                CultureInfo.InvariantCulture,
                "0x1004 discharge={0} level={1} status={2} ignored={3}",
                p[0], level, status, ignored);
            return battery;
        }

        internal static HidppBattery ParseBatteryVoltage(byte[] p)
        {
            if (p == null || p.Length < 2)
                return null;
            HidppBattery battery = new HidppBattery();
            int bigEndian = (p[0] << 8) | p[1];
            int littleEndian = (p[1] << 8) | p[0];
            int millivolts = bigEndian;
            if (millivolts < 500 || millivolts > 6000)
                millivolts = littleEndian;
            byte flags = p.Length > 2 ? p[2] : (byte)0;
            battery.Charging = (flags & 0x80) != 0 ||
                (flags & 0x03) == 0x01;
            battery.Approximate = true;
            battery.Percent = VoltageToPercent(millivolts);
            battery.Detail = string.Format(
                CultureInfo.InvariantCulture,
                "0x1001 voltage={0}mV flags=0x{1:X2}", millivolts, flags);
            return battery;
        }

        internal static int VoltageToPercent(int millivolts)
        {
            if (millivolts <= 0)
                return -1;
            double percent;
            if (millivolts >= 2500)
                percent = (millivolts - 3300.0) * 100.0 / 900.0;
            else
                percent = (millivolts - 1000.0) * 100.0 / 600.0;
            if (percent < 0)
                percent = 0;
            if (percent > 100)
                percent = 100;
            return (int)Math.Round(percent);
        }

        private static string ReadDeviceName(
            HidppConnection connection, byte deviceIndex, byte featureIndex, int timeout)
        {
            HidppResponse countResponse = connection.Request(
                deviceIndex, featureIndex, 0x00, new byte[0], timeout);
            if (countResponse == null || countResponse.IsError ||
                countResponse.Parameters.Length < 1)
                return "";

            int count = countResponse.Parameters[0];
            if (count <= 0 || count > 96)
                return "";

            List<byte> bytes = new List<byte>();
            bool chunkIndexMode = false;
            for (int offset = 0; offset < count; offset += 16)
            {
                byte parameter = (byte)offset;
                if (chunkIndexMode)
                    parameter = (byte)(offset / 16);

                HidppResponse chunk = connection.Request(
                    deviceIndex, featureIndex, 0x01, new byte[] { parameter }, timeout);
                if ((chunk == null || chunk.IsError) && offset > 0 && !chunkIndexMode)
                {
                    chunk = connection.Request(
                        deviceIndex, featureIndex, 0x01,
                        new byte[] { (byte)(offset / 16) }, timeout);
                    if (chunk != null && !chunk.IsError)
                        chunkIndexMode = true;
                }
                if (chunk == null || chunk.IsError || chunk.Parameters.Length < 1)
                    break;

                int chunkLength = chunk.Parameters[0];
                int available = chunk.Parameters.Length - 1;
                if (chunkLength <= 0 || chunkLength > available)
                    chunkLength = available;
                for (int i = 0; i < chunkLength; i++)
                {
                    byte value = chunk.Parameters[1 + i];
                    if (value == 0)
                    {
                        offset = count;
                        break;
                    }
                    bytes.Add(value);
                }
            }

            if (bytes.Count == 0)
                return "";
            string name = Encoding.UTF8.GetString(bytes.ToArray()).Trim();
            return TextUtil.Clean(name);
        }

        private static string ResolveReceiverName(
            IntPtr deviceInfoSet, ref Native.SP_DEVINFO_DATA deviceInfoData,
            string instanceId, List<string> log)
        {
            string friendly = Native.GetFriendlyName(deviceInfoSet, ref deviceInfoData);
            string bus = Native.GetBusReportedName(deviceInfoSet, ref deviceInfoData);
            string desc = Native.GetDeviceDesc(deviceInfoSet, ref deviceInfoData);
            string name = DeviceNameResolver.Resolve(
                instanceId, "", friendly, bus, desc, log);
            if (TextUtil.IsGenericDeviceName(name))
                return "罗技设备";
            return name;
        }

        private static bool IsBluetoothKey(string key)
        {
            if (string.IsNullOrEmpty(key))
                return false;
            return key.StartsWith("BTH", StringComparison.OrdinalIgnoreCase) ||
                key.IndexOf("BTHENUM", StringComparison.OrdinalIgnoreCase) >= 0 ||
                key.IndexOf("BTHLE", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetReceiverKey(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return "";
            string current = instanceId;
            string fallback = instanceId;
            for (int i = 0; i < 8 && !string.IsNullOrEmpty(current); i++)
            {
                uint devInst;
                if (Native.CM_Locate_DevNodeW(
                        out devInst, current, Native.CM_LOCATE_DEVNODE_NORMAL) != 0)
                    break;
                string parent = Native.ReadStringPropertyCm(
                    devInst, Native.DEVPKEY_Device_Parent);
                if (string.IsNullOrEmpty(parent))
                    break;

                if (parent.StartsWith("USB\\", StringComparison.OrdinalIgnoreCase))
                {
                    if (parent.IndexOf("&MI_", StringComparison.OrdinalIgnoreCase) < 0)
                        return parent;
                    fallback = parent;
                }
                else if (parent.StartsWith("BTHLE\\DEV_", StringComparison.OrdinalIgnoreCase) ||
                         parent.StartsWith("BTHENUM\\DEV_", StringComparison.OrdinalIgnoreCase) ||
                         parent.StartsWith("BTH\\", StringComparison.OrdinalIgnoreCase))
                {
                    return parent;
                }

                current = parent;
            }
            return fallback;
        }
    }
}

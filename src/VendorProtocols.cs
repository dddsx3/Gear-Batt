using System;
using System.Collections.Generic;

namespace GearBatt
{
    internal sealed class DarkrimTlv
    {
        public byte Type;
        public byte[] Value = new byte[0];
    }

    internal static class DarkrimProtocol
    {
        public const ushort VendorId = 0x3654;
        public const ushort UsagePage = 0xFF02;
        public const ushort Usage = 0x0002;
        public const int ReportLength = 65;
        public const byte ChannelLocal = 0x10;

        public static ushort Crc16(byte[] data, int offset, int count)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < count; i++)
            {
                crc ^= (ushort)(data[offset + i] << 8);
                for (int bit = 0; bit < 8; bit++)
                {
                    if ((crc & 0x8000) != 0)
                        crc = (ushort)((crc << 1) ^ 0x1021);
                    else
                        crc = (ushort)(crc << 1);
                }
            }
            return crc;
        }

        public static byte[] BuildFrame(
            byte seq, byte[] payload, byte channel, byte sub,
            byte frag, byte more)
        {
            if (payload == null)
                payload = new byte[0];
            if (payload.Length > ReportLength - 12)
                throw new ArgumentException("DARKRIM payload too large");

            byte[] frame = new byte[ReportLength];
            frame[0] = 0x00;
            frame[1] = 0xFE;
            frame[2] = seq;
            frame[3] = channel;
            frame[4] = 0x00;
            frame[5] = sub;
            frame[6] = frag;
            frame[7] = more;
            frame[8] = (byte)(payload.Length & 0xFF);
            frame[9] = (byte)((payload.Length >> 8) & 0xFF);
            Array.Copy(payload, 0, frame, 10, payload.Length);
            ushort crc = Crc16(frame, 1, 9 + payload.Length);
            frame[10 + payload.Length] = (byte)(crc >> 8);
            frame[11 + payload.Length] = (byte)(crc & 0xFF);
            return frame;
        }

        public static byte[] BuildBatteryQuery()
        {
            return BuildFrame(0, new byte[] { 0x01, 0x00 }, ChannelLocal, 0x02, 0, 0);
        }

        public static bool ParseFrame(
            byte[] report, out byte seq, out List<DarkrimTlv> tlvs)
        {
            seq = 0;
            tlvs = new List<DarkrimTlv>();
            if (report == null || report.Length < 12 || report[1] != 0xFE)
                return false;

            int length = report[8] | (report[9] << 8);
            if (length < 2 || 10 + length + 2 > report.Length)
                return false;

            ushort stored = (ushort)((report[10 + length] << 8) |
                report[11 + length]);
            ushort calculated = Crc16(report, 1, 9 + length);
            if (stored != calculated)
                return false;

            seq = report[2];
            int offset = 10;
            int end = 10 + length;
            while (offset + 2 <= end)
            {
                byte type = report[offset];
                int valueLength = report[offset + 1];
                if (offset + 2 + valueLength > end)
                    break;
                DarkrimTlv tlv = new DarkrimTlv();
                tlv.Type = type;
                tlv.Value = new byte[valueLength];
                Array.Copy(report, offset + 2, tlv.Value, 0, valueLength);
                tlvs.Add(tlv);
                offset += 2 + valueLength;
            }
            return true;
        }

        public static bool TryGetBattery(
            byte[] report, out int percent, out byte seq, out List<DarkrimTlv> tlvs)
        {
            percent = -1;
            if (!ParseFrame(report, out seq, out tlvs))
                return false;
            for (int i = 0; i < tlvs.Count; i++)
            {
                if (tlvs[i].Type == 0x01 && tlvs[i].Value.Length >= 1)
                {
                    int value = tlvs[i].Value[0];
                    if (value >= 0 && value <= 100)
                    {
                        percent = value;
                        return true;
                    }
                }
            }
            return false;
        }
    }

    internal sealed class CxBattery
    {
        public int Percent = -1;
        public bool Wired;
        public int VoltageMv;
        public string Detail = "";
    }

    internal sealed class CxReceiverProvider
    {
        // CX 2.4G receiver (for example VID 260D PID 1163, product string
        // "2.4G Wireless Receiver"). Vendor interface is usage page 0xFF02 /
        // usage 0x0002 with 17-byte input and output reports. The battery
        // query is the 17-byte report 0x08 command 0x04 with trailing 0x49
        // (the same framing as ATK/VXE "protocol 1"); the reply echoes
        // 08 04 and carries percent at byte 6, a wired flag at byte 7 and
        // the battery voltage in millivolts at bytes 8-9 (big endian).
        // The mouse only answers while its radio link is active; when it
        // sleeps the query times out, so the last successful reading is
        // cached and reused for a while.
        public const ushort VendorId = 0x260D;
        public const ushort UsagePage = 0xFF02;
        public const ushort Usage = 0x0002;
        public const int ReportLength = 17;

        private sealed class CachedReading
        {
            public CxBattery Battery;
            public DateTime Time;
            public string Name;
        }

        private readonly object _cacheLock = new object();
        private readonly Dictionary<string, CachedReading> _cache =
            new Dictionary<string, CachedReading>(
                StringComparer.OrdinalIgnoreCase);

        // True when the last scan saw a CX receiver but could not read it
        // (mouse asleep and no cached value). The tray app polls faster
        // while this is set so the device appears soon after the mouse
        // wakes up.
        public bool PendingUnread { get; private set; }

        public void Scan(ScanResult result, List<string> log)
        {
            PendingUnread = false;
            List<HidDeviceInfo> interfaces = HidDevices.Enumerate(false);
            for (int i = 0; i < interfaces.Count; i++)
            {
                HidDeviceInfo info = interfaces[i];
                if (info.VendorId != VendorId ||
                    info.UsagePage != UsagePage ||
                    info.Usage != Usage ||
                    info.OutputReportLength < ReportLength ||
                    info.InputReportLength < ReportLength)
                    continue;
                PendingUnread = true;

                byte[] request;
                byte[] response;
                string detail;
                CxBattery battery = Query(info, out request, out response,
                    out detail);
                string name = info.ResolveName(L10n.T(
                    "CX 2.4G 无线接收器", "CX 2.4G receiver"));
                bool cached = false;

                if (battery == null)
                {
                    lock (_cacheLock)
                    {
                        CachedReading previous;
                        if (_cache.TryGetValue(info.InstanceId, out previous) &&
                            (DateTime.Now - previous.Time).TotalHours < 12.0)
                        {
                            battery = previous.Battery;
                            name = previous.Name;
                            cached = true;
                        }
                    }
                    if (battery == null)
                    {
                        if (log != null)
                            log.Add("cx: " + info.InstanceId + " " + detail);
                        continue;
                    }
                }
                else
                {
                    PendingUnread = false;
                    lock (_cacheLock)
                    {
                        CachedReading entry = new CachedReading();
                        entry.Battery = battery;
                        entry.Time = DateTime.Now;
                        entry.Name = name;
                        _cache[info.InstanceId] = entry;
                    }
                }

                WirelessDevice device = new WirelessDevice();
                device.Id = "cx:" + info.InstanceId;
                device.Name = name;
                device.Percent = battery.Percent;
                device.Charging = battery.Wired;
                device.VoltageMv = battery.VoltageMv;
                device.Source = "2.4G";
                device.Provider = "CX";
                device.InstanceId = info.InstanceId;
                device.Detail = "CX: " + battery.Detail +
                    (cached
                        ? " (cached " + DateTime.Now.ToString("HH:mm") + ")"
                        : "");
                device.SortOrder = 4;
                result.Devices.Add(device);

                if (log != null)
                    log.Add("cx: " + device.Name + " " + battery.Detail +
                        (cached ? " [cached]" : ""));
            }
        }

        internal static CxBattery Query(
            HidDeviceInfo info, out byte[] request,
            out byte[] response, out string detail)
        {
            request = null;
            response = null;
            detail = "";

            using (HidppConnection connection = HidppConnection.Open(info.Path))
            {
                if (connection == null)
                {
                    detail = "cannot open HID interface";
                    return null;
                }

                int reportLength = Math.Max(
                    ReportLength, connection.OutputReportLength);
                byte[] report = new byte[reportLength];
                report[0] = 0x08;
                report[1] = 0x04;
                if (reportLength >= ReportLength)
                    report[ReportLength - 1] = 0x49;
                request = report;

                byte[] buffer = new byte[Math.Max(
                    ReportLength, connection.InputReportLength)];
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    if (!connection.WriteRaw(report, 250))
                    {
                        detail = "write failed";
                        continue;
                    }

                    int deadline = Environment.TickCount + 800;
                    while (true)
                    {
                        int remaining = deadline - Environment.TickCount;
                        if (remaining <= 0)
                            break;
                        int read = connection.ReadRaw(
                            buffer, Math.Min(250, remaining));
                        if (read < 10)
                            continue;

                        byte[] candidate = new byte[read];
                        Array.Copy(buffer, 0, candidate, 0, read);
                        CxBattery battery = ParseResponse(candidate);
                        if (battery != null)
                        {
                            response = candidate;
                            detail = battery.Detail;
                            return battery;
                        }
                    }
                }

                detail = "no response (mouse asleep or off)";
                return null;
            }
        }

        internal static CxBattery ParseResponse(byte[] report)
        {
            if (report == null || report.Length < 10)
                return null;
            if (report[0] != 0x08 || report[1] != 0x04)
                return null;

            int percent = report[6];
            if (percent < 0 || percent > 100)
                percent = -1;
            bool wired = report[7] != 0;

            int bigEndian = (report[8] << 8) | report[9];
            int littleEndian = (report[9] << 8) | report[8];
            int millivolts = bigEndian;
            if (millivolts < 2500 || millivolts > 5500)
                millivolts = littleEndian;
            if (millivolts < 2500 || millivolts > 5500)
                millivolts = 0;

            CxBattery battery = new CxBattery();
            battery.Percent = percent;
            battery.Wired = wired;
            battery.VoltageMv = millivolts;
            battery.Detail = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "level={0} wired={1} voltage={2}mV mode={3}",
                percent, wired, millivolts, report[5]);
            return battery;
        }
    }

    internal sealed class AtkVxeProvider
    {
        public const ushort VendorId = 0x373B;
        public const ushort ProductId = 0x1216;
        public const ushort UsagePage = 0xFF05;
        private static readonly byte[] Command =
            new byte[] { 0x7D, 0x72, 0x02, 0x00, 0x01, 0x07, 0x01 };

        public void Scan(ScanResult result, List<string> log)
        {
            List<HidDeviceInfo> interfaces = HidDevices.Enumerate(false);
            for (int i = 0; i < interfaces.Count; i++)
            {
                HidDeviceInfo info = interfaces[i];
                if (info.VendorId != VendorId ||
                    info.ProductId != ProductId ||
                    info.UsagePage != UsagePage ||
                    info.OutputReportLength < 64)
                    continue;

                byte[] request;
                byte[] response;
                string detail;
                int percent = Query(info, out request, out response, out detail);
                if (percent < 0)
                {
                    if (log != null)
                        log.Add("atk: " + info.InstanceId + " " + detail);
                    continue;
                }

                WirelessDevice device = new WirelessDevice();
                device.Id = "atk:" + info.InstanceId;
                device.Name = info.ResolveName("ATK/VXE 无线鼠标");
                device.Percent = percent;
                device.Charging = false;
                device.Source = "2.4G";
                device.Provider = "ATK/VXE";
                device.InstanceId = info.InstanceId;
                device.Detail = "ATK protocol 2: " + detail;
                device.SortOrder = 5;
                result.Devices.Add(device);

                if (log != null)
                    log.Add("atk: " + device.Name + " percent=" + percent);
            }
        }

        internal static bool TryParseResponse(byte[] response, out int percent)
        {
            percent = -1;
            if (response == null || response.Length < 8)
                return false;
            if (response[1] != 0x72 || response[5] != 0x07)
                return false;
            int value = response[7];
            if (value < 0 || value > 100)
                return false;
            percent = value;
            return true;
        }

        internal static int Query(
            HidDeviceInfo info, out byte[] request,
            out byte[] response, out string detail)
        {
            request = null;
            response = null;
            detail = "";

            using (HidppConnection connection = HidppConnection.Open(info.Path))
            {
                if (connection == null)
                {
                    detail = "cannot open HID interface";
                    return -1;
                }

                int reportLength = Math.Max(64, connection.OutputReportLength);
                byte[] report = new byte[reportLength];
                report[0] = 0x08;
                Array.Copy(Command, 0, report, 1, Command.Length);
                request = report;

                byte[] buffer = new byte[Math.Max(64, connection.InputReportLength)];
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    if (!connection.WriteRaw(report, 250))
                    {
                        detail = "write failed";
                        continue;
                    }

                    int deadline = Environment.TickCount + 650;
                    while (true)
                    {
                        int remaining = deadline - Environment.TickCount;
                        if (remaining <= 0)
                            break;
                        int read = connection.ReadRaw(
                            buffer, Math.Min(250, remaining));
                        if (read < 8)
                            continue;

                        byte[] candidate = new byte[read];
                        Array.Copy(buffer, 0, candidate, 0, read);
                        int percent;
                        if (TryParseResponse(candidate, out percent))
                        {
                            response = candidate;
                            detail = "level=" + percent +
                                " raw=" + TextUtil.Hex(candidate, 16);
                            return percent;
                        }
                    }
                }

                detail = "no response (device may be asleep; move the mouse and refresh)";
                return -1;
            }
        }
    }

    internal sealed class DarkrimProvider
    {
        public void Scan(ScanResult result, List<string> log)
        {
            List<HidDeviceInfo> interfaces = HidDevices.Enumerate(false);
            for (int i = 0; i < interfaces.Count; i++)
            {
                HidDeviceInfo info = interfaces[i];
                if (info.VendorId != DarkrimProtocol.VendorId ||
                    info.UsagePage != DarkrimProtocol.UsagePage ||
                    info.Usage != DarkrimProtocol.Usage ||
                    info.OutputReportLength < DarkrimProtocol.ReportLength)
                    continue;

                byte[] request;
                byte[] response;
                string detail;
                int percent = Query(info, out request, out response, out detail);
                if (percent < 0)
                {
                    if (log != null)
                        log.Add("darkrim: " + info.InstanceId + " " + detail);
                    continue;
                }

                WirelessDevice device = new WirelessDevice();
                device.Id = "darkrim:" + info.InstanceId;
                device.Name = info.ResolveName("DARKRIM 无线耳机");
                device.Percent = percent;
                device.Charging = false;
                device.Source = "2.4G";
                device.Provider = "DARKRIM";
                device.InstanceId = info.InstanceId;
                device.Detail = "DARKRIM TLV: " + detail;
                device.SortOrder = 6;
                result.Devices.Add(device);

                if (log != null)
                    log.Add("darkrim: " + device.Name + " percent=" + percent);
            }
        }

        internal static int Query(
            HidDeviceInfo info, out byte[] request,
            out byte[] response, out string detail)
        {
            request = null;
            response = null;
            detail = "";

            using (HidppConnection connection = HidppConnection.Open(info.Path))
            {
                if (connection == null)
                {
                    detail = "cannot open HID interface";
                    return -1;
                }

                byte[] frame = DarkrimProtocol.BuildBatteryQuery();
                int reportLength = Math.Max(
                    DarkrimProtocol.ReportLength, connection.OutputReportLength);
                byte[] report = new byte[reportLength];
                Array.Copy(frame, 0, report, 0,
                    Math.Min(frame.Length, report.Length));
                request = report;

                byte[] buffer = new byte[Math.Max(
                    DarkrimProtocol.ReportLength, connection.InputReportLength)];
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    if (!connection.WriteRaw(report, 250))
                    {
                        detail = "write failed";
                        continue;
                    }

                    int deadline = Environment.TickCount + 500;
                    while (true)
                    {
                        int remaining = deadline - Environment.TickCount;
                        if (remaining <= 0)
                            break;
                        int read = connection.ReadRaw(
                            buffer, Math.Min(250, remaining));
                        if (read < 12)
                            continue;

                        byte[] candidate = new byte[read];
                        Array.Copy(buffer, 0, candidate, 0, read);
                        int percent;
                        byte seq;
                        List<DarkrimTlv> tlvs;
                        if (DarkrimProtocol.TryGetBattery(
                                candidate, out percent, out seq, out tlvs) &&
                            seq == 0)
                        {
                            response = candidate;
                            detail = "level=" + percent +
                                " seq=" + seq +
                                " raw=" + TextUtil.Hex(candidate, 16);
                            return percent;
                        }
                    }
                }

                detail = "no response (headset may be off or not connected)";
                return -1;
            }
        }
    }
}

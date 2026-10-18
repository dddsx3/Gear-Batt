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

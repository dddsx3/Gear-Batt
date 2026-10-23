// Dev-only probe for CX 2.4G receivers (VID 260D) and other HID capture.
// Not part of build.cmd. Compile manually:
//   csc -target:exe -out:dist\GearBattProbe.exe -r:System.dll -r:System.Core.dll ^
//       src\Native.cs src\L10n.cs src\Models.cs src\Providers.cs src\Hidpp.cs ^
//       src\HidDevices.cs src\VendorProtocols.cs tools\ProbeHid.cs
// Usage:
//   GearBattProbe.exe --seconds 20 --out probe.txt     (passive input + features)
//   GearBattProbe.exe --query --out probe.txt          (vendor command probing)
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace GearBatt
{
    internal static class ProbeHid
    {
        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_GetFeature(
            IntPtr h, byte[] buffer, int length);

        [DllImport("hid.dll", SetLastError = true)]
        private static extern bool HidD_SetFeature(
            IntPtr h, byte[] buffer, int length);

        private sealed class ReportStat
        {
            public int Count;
            public long FirstTicks;
            public long LastTicks;
        }

        private static void Main(string[] args)
        {
            int duration = 15;
            string outPath = "probe-hid.txt";
            bool query = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "--seconds" && i + 1 < args.Length)
                    int.TryParse(args[++i], out duration);
                else if (args[i] == "--out" && i + 1 < args.Length)
                    outPath = args[++i];
                else if (args[i] == "--query")
                    query = true;
            }

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("probe start " + DateTime.Now.ToString("HH:mm:ss"));

            List<HidDeviceInfo> all = HidDevices.Enumerate(false);
            foreach (HidDeviceInfo info in all)
            {
                if (info.VendorId != 0x260D)
                    continue;
                if (query)
                {
                    if (info.UsagePage == 0xFF02 &&
                        info.OutputReportLength >= 17)
                        RunAtkStyleQuery(sb, info);
                    if (info.UsagePage == 0xFF04)
                        PollFeature(sb, info, 6, 5, 700);
                }
                else
                {
                    DumpInterface(sb, info, duration, false);
                }
            }

            System.IO.File.WriteAllText(outPath, sb.ToString(),
                new UTF8Encoding(false));
            Console.WriteLine("written " + outPath);
        }

        private static void RunAtkStyleQuery(
            StringBuilder sb, HidDeviceInfo info)
        {
            sb.AppendLine();
            sb.AppendLine("=== query on FF02 " + info.InstanceId);
            IntPtr handle = Native.CreateFileW(
                info.Path, Native.GENERIC_READ | Native.GENERIC_WRITE,
                Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE,
                IntPtr.Zero, Native.OPEN_EXISTING,
                Native.FILE_FLAG_OVERLAPPED, IntPtr.Zero);
            if (handle == new IntPtr(-1))
            {
                sb.AppendLine("open failed err=" + Marshal.GetLastWin32Error());
                return;
            }
            IntPtr evt = Native.CreateEventW(IntPtr.Zero, true, false, null);

            byte[][] commands = new byte[][]
            {
                BuildReport(0x08, new byte[] { 0x7D, 0x72, 0x02, 0x00, 0x01, 0x07, 0x01 }, 0x00),
                BuildReport(0x08, new byte[] { 0x04 }, 0x49),
                BuildReport(0x08, new byte[] { 0x04 }, 0x49)
            };
            System.Threading.Thread.Sleep(400);

            for (int c = 0; c < commands.Length; c++)
            {
                byte[] command = commands[c];
                sb.AppendLine("cmd" + c + " > " + TextUtil.Hex(command, 32));
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    Native.ResetEvent(evt);
                    Native.OVERLAPPED ov = new Native.OVERLAPPED();
                    ov.hEvent = evt;
                    int written;
                    bool ok = Native.WriteFile(
                        handle, command, command.Length, out written, ref ov);
                    if (!ok)
                    {
                        int err = Marshal.GetLastWin32Error();
                        if (err != Native.ERROR_IO_PENDING)
                        {
                            sb.AppendLine("write failed err=" + err);
                            break;
                        }
                        uint wait = Native.WaitForSingleObject(evt, 400);
                        if (wait != Native.WAIT_OBJECT_0)
                        {
                            Native.CancelIoEx(handle, ref ov);
                            Native.WaitForSingleObject(evt, 100);
                            int ignored;
                            Native.GetOverlappedResult(
                                handle, ref ov, out ignored, false);
                            sb.AppendLine("write timeout");
                            break;
                        }
                        if (!Native.GetOverlappedResult(
                                handle, ref ov, out written, false))
                        {
                            sb.AppendLine("write result failed");
                            break;
                        }
                    }

                    long deadline = Environment.TickCount + 900;
                    byte[] buffer = new byte[Math.Max(64,
                        info.InputReportLength + 8)];
                    while (Environment.TickCount < deadline)
                    {
                        int remaining = (int)(deadline - Environment.TickCount);
                        int read = ReadWithTimeout(
                            handle, evt, buffer, remaining);
                        if (read <= 0)
                            break;
                        byte[] copy = new byte[read];
                        Array.Copy(buffer, 0, copy, 0, read);
                        sb.AppendLine("  attempt" + attempt + " <[" + read + "] " +
                            DateTime.Now.ToString("HH:mm:ss.fff") + " " +
                            TextUtil.Hex(copy, 32));
                    }
                }
            }

            Native.CloseHandle(handle);
            Native.CloseHandle(evt);
        }

        private static byte[] BuildReport(
            byte reportId, byte[] body, byte trailing)
        {
            byte[] report = new byte[17];
            report[0] = reportId;
            Array.Copy(body, 0, report, 1, Math.Min(body.Length, 15));
            report[16] = trailing;
            return report;
        }

        private static int ReadWithTimeout(
            IntPtr handle, IntPtr evt, byte[] buffer, int timeoutMs)
        {
            Native.ResetEvent(evt);
            Native.OVERLAPPED ov = new Native.OVERLAPPED();
            ov.hEvent = evt;
            int read;
            bool ok = Native.ReadFile(
                handle, buffer, buffer.Length, out read, ref ov);
            if (!ok)
            {
                int err = Marshal.GetLastWin32Error();
                if (err != Native.ERROR_IO_PENDING)
                    return -1;
                uint wait = Native.WaitForSingleObject(
                    evt, (uint)Math.Max(1, timeoutMs));
                if (wait != Native.WAIT_OBJECT_0)
                {
                    Native.CancelIoEx(handle, ref ov);
                    Native.WaitForSingleObject(evt, 100);
                    int ignored;
                    Native.GetOverlappedResult(handle, ref ov, out ignored, false);
                    return -1;
                }
                if (!Native.GetOverlappedResult(handle, ref ov, out read, false))
                    return -1;
            }
            return read;
        }

        private static void PollFeature(
            StringBuilder sb, HidDeviceInfo info, byte id, int times, int gap)
        {
            sb.AppendLine();
            sb.AppendLine("=== poll feature 0x" + id.ToString("X2") +
                " on " + info.InstanceId);
            IntPtr handle = Native.CreateFileW(
                info.Path, 0,
                Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE,
                IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero);
            if (handle == new IntPtr(-1))
            {
                sb.AppendLine("open failed err=" + Marshal.GetLastWin32Error());
                return;
            }
            int length = info.FeatureReportLength;
            for (int i = 0; i < times; i++)
            {
                byte[] buffer = new byte[length];
                buffer[0] = id;
                if (HidD_GetFeature(handle, buffer, length))
                    sb.AppendLine(DateTime.Now.ToString("HH:mm:ss.fff") +
                        " " + TextUtil.Hex(buffer, 32));
                else
                    sb.AppendLine("get failed err=" + Marshal.GetLastWin32Error());
                System.Threading.Thread.Sleep(gap);
            }
            Native.CloseHandle(handle);
        }

        private static void DumpInterface(
            StringBuilder sb, HidDeviceInfo info, int duration, bool doWrite)
        {
            sb.AppendLine();
            sb.AppendLine("=== " + info.InstanceId);
            sb.AppendLine("usagePage=0x" + info.UsagePage.ToString("X4") +
                " usage=0x" + info.Usage.ToString("X4") +
                " in=" + info.InputReportLength +
                " out=" + info.OutputReportLength +
                " feature=" + info.FeatureReportLength);

            // 1) passive input monitoring
            if (info.InputReportLength > 0)
            {
                IntPtr handle = Native.CreateFileW(
                    info.Path, Native.GENERIC_READ,
                    Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE,
                    IntPtr.Zero, Native.OPEN_EXISTING,
                    Native.FILE_FLAG_OVERLAPPED, IntPtr.Zero);
                if (handle == new IntPtr(-1))
                {
                    sb.AppendLine("input: open failed err=" +
                        Marshal.GetLastWin32Error());
                }
                else
                {
                    IntPtr evt = Native.CreateEventW(
                        IntPtr.Zero, true, false, null);
                    Dictionary<string, ReportStat> stats =
                        new Dictionary<string, ReportStat>();
                    long start = Environment.TickCount;
                    byte[] buffer = new byte[Math.Max(
                        info.InputReportLength, 64)];
                    while (Environment.TickCount - start < duration * 1000)
                    {
                        Native.OVERLAPPED ov = new Native.OVERLAPPED();
                        ov.hEvent = evt;
                        Native.ResetEvent(evt);
                        int read;
                        bool ok = Native.ReadFile(
                            handle, buffer, buffer.Length, out read, ref ov);
                        if (!ok)
                        {
                            int err = Marshal.GetLastWin32Error();
                            if (err != Native.ERROR_IO_PENDING)
                                break;
                            uint wait = Native.WaitForSingleObject(
                                evt, (uint)Math.Max(200,
                                    duration * 1000 -
                                    (Environment.TickCount - start)));
                            if (wait != Native.WAIT_OBJECT_0)
                            {
                                Native.CancelIoEx(handle, ref ov);
                                Native.WaitForSingleObject(evt, 100);
                                int ignored;
                                Native.GetOverlappedResult(
                                    handle, ref ov, out ignored, false);
                                continue;
                            }
                            if (!Native.GetOverlappedResult(
                                    handle, ref ov, out read, false))
                                continue;
                        }
                        if (read <= 0)
                            continue;
                        byte[] copy = new byte[read];
                        Array.Copy(buffer, 0, copy, 0, read);
                        string key = TextUtil.Hex(copy, 32);
                        ReportStat stat;
                        if (!stats.TryGetValue(key, out stat))
                        {
                            stat = new ReportStat();
                            stat.FirstTicks = Environment.TickCount;
                            stats[key] = stat;
                            if (stats.Count <= 400)
                                sb.AppendLine("input[" + read + "] " +
                                    DateTime.Now.ToString("HH:mm:ss.fff") +
                                    " " + key);
                        }
                        stat.Count++;
                        stat.LastTicks = Environment.TickCount;
                    }
                    Native.CloseHandle(handle);
                    Native.CloseHandle(evt);
                    sb.AppendLine("input summary: " + stats.Count +
                        " distinct reports in " + duration + "s");
                }
            }

            // 2) feature report reads (passive)
            if (info.FeatureReportLength > 0)
            {
                IntPtr handle = Native.CreateFileW(
                    info.Path, 0,
                    Native.FILE_SHARE_READ | Native.FILE_SHARE_WRITE,
                    IntPtr.Zero, Native.OPEN_EXISTING, 0, IntPtr.Zero);
                if (handle == new IntPtr(-1))
                {
                    sb.AppendLine("feature: open failed err=" +
                        Marshal.GetLastWin32Error());
                    return;
                }
                int length = info.FeatureReportLength;
                for (byte id = 0; id < 32; id++)
                {
                    byte[] buffer = new byte[length];
                    buffer[0] = id;
                    if (HidD_GetFeature(handle, buffer, length))
                        sb.AppendLine("feature[0x" + id.ToString("X2") +
                            "] " + TextUtil.Hex(buffer, 32));
                }
                Native.CloseHandle(handle);
            }
        }
    }
}

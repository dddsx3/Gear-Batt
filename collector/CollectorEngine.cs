using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace GearBatt
{
    internal sealed class CollectorResult
    {
        public string ReportPath = "";
        public string Summary = "";
    }

    internal static class CollectorEngine
    {
        public static CollectorResult Collect(
            string userModel, string outputPath, bool fromUi)
        {
            StringBuilder report = new StringBuilder();
            string generated = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            report.AppendLine("Gear-Batt Voluntary Compatibility Report");
            report.AppendLine("========================================");
            report.AppendLine("Generated: " + generated);
            report.AppendLine("Collector: " + GetVersion());
            report.AppendLine("OS: " + Environment.OSVersion.VersionString);
            report.AppendLine("Process: " + (IntPtr.Size * 8) + "-bit");
            report.AppendLine("Mode: " + (fromUi
                ? "UI (voluntary consent checked)"
                : "CLI --collect (explicit request)"));
            report.AppendLine("User-provided model: " +
                TextUtil.Truncate(TextUtil.Clean(userModel ?? ""), 100));
            report.AppendLine();
            report.AppendLine("Privacy and consent");
            report.AppendLine("-------------------");
            report.AppendLine(fromUi
                ? "The user read the notice and checked the voluntary consent checkbox."
                : "The user explicitly ran the collector with --collect.");
            report.AppendLine("This report is stored locally. The program does not upload it.");
            report.AppendLine("Included: OS/app version, HID metadata (VID/PID, usage, report");
            report.AppendLine("lengths, product/manufacturer names, parsed HID value caps),");
            report.AppendLine("read-only battery protocol responses, and Windows/Bluetooth");
            report.AppendLine("battery values. Raw HID report descriptors are not exposed by");
            report.AppendLine("the Windows user-mode HID API; parsed usages are included instead.");
            report.AppendLine("Excluded: keystrokes, mouse movement, raw input reports,");
            report.AppendLine("usernames, computer name, IP addresses, USB serial numbers,");
            report.AppendLine("and Bluetooth addresses. Instance-specific path suffixes are");
            report.AppendLine("redacted. Device names may be user-customized. Vendor-specific");
            report.AppendLine("raw protocol responses may contain model/ID bytes; review this");
            report.AppendLine("file before sharing.");
            report.AppendLine();

            ScanResult scan = new Scanner().Scan(false, false);
            report.AppendLine("Device summary (" + scan.Devices.Count + ")");
            report.AppendLine("-------------------------");
            if (scan.Devices.Count == 0)
            {
                report.AppendLine("(none)");
            }
            else
            {
                for (int i = 0; i < scan.Devices.Count; i++)
                {
                    WirelessDevice d = scan.Devices[i];
                    report.AppendLine("[" + d.Source + "] [" + d.Provider + "] " +
                        d.Name + " - " + d.PercentText + " - " + d.StatusText);
                }
            }
            if (scan.Errors.Count > 0)
            {
                report.AppendLine();
                report.AppendLine("Provider errors:");
                for (int i = 0; i < scan.Errors.Count; i++)
                    report.AppendLine("  " + SanitizeText(scan.Errors[i]));
            }
            report.AppendLine();

            List<string> hidLog = new List<string>();
            List<HidDeviceInfo> hid = HidDevices.Enumerate(true, hidLog);
            report.AppendLine("HID interfaces (" + hid.Count + ")");
            report.AppendLine("-----------------------");
            for (int i = 0; i < hid.Count; i++)
            {
                HidDeviceInfo info = hid[i];
                report.AppendLine("Interface #" + (i + 1));
                report.AppendLine("  Instance: " + SanitizeInstanceId(info.InstanceId));
                report.AppendLine("  VID/PID: 0x" + info.VendorId.ToString("X4") +
                    " / 0x" + info.ProductId.ToString("X4"));
                report.AppendLine("  UsagePage/Usage: 0x" +
                    info.UsagePage.ToString("X4") + " / 0x" +
                    info.Usage.ToString("X4"));
                report.AppendLine("  Report lengths: in=" + info.InputReportLength +
                    " out=" + info.OutputReportLength +
                    " feature=" + info.FeatureReportLength);
                if (!string.IsNullOrEmpty(info.Product))
                    report.AppendLine("  Product: " + info.Product);
                if (!string.IsNullOrEmpty(info.Manufacturer))
                    report.AppendLine("  Manufacturer: " + info.Manufacturer);
                if (!string.IsNullOrEmpty(info.FriendlyName))
                    report.AppendLine("  FriendlyName: " + info.FriendlyName);
                if (!string.IsNullOrEmpty(info.BusReportedName))
                    report.AppendLine("  BusReportedName: " + info.BusReportedName);
                if (!string.IsNullOrEmpty(info.DeviceDesc))
                    report.AppendLine("  DeviceDesc: " + info.DeviceDesc);
                string note = ClassifyHid(info);
                if (!string.IsNullOrEmpty(note))
                    report.AppendLine("  Note: " + note);
                report.AppendLine("  Parsed HID value caps:");
                if (!string.IsNullOrEmpty(info.CapsSummary))
                    report.Append(info.CapsSummary);
                else
                    report.AppendLine("    (none)");
                report.AppendLine();
            }

            if (hid.Count == 0 && hidLog.Count > 0)
            {
                report.AppendLine("HID enumeration diagnostics");
                report.AppendLine("---------------------------");
                for (int i = 0; i < hidLog.Count && i < 40; i++)
                    report.AppendLine("  " + SanitizeHidLog(hidLog[i]));
                report.AppendLine();
            }

            int hidppInterfaces = AppendHidppSection(report, hid);
            int vendorInterfaces = AppendVendorSection(report, hid);

            report.AppendLine("How to share");
            report.AppendLine("------------");
            report.AppendLine("1. Review this file and remove anything you do not want to share.");
            report.AppendLine("2. Open https://github.com/dddsx3/Gear-Batt/issues/new");
            report.AppendLine("3. Attach this report and describe the device model.");
            report.AppendLine("This file was generated locally. Nothing was uploaded automatically.");

            string path = ResolveReportPath(outputPath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, report.ToString(), new UTF8Encoding(false));

            CollectorResult result = new CollectorResult();
            result.ReportPath = path;
            result.Summary = L10n.T(
                "检测到设备：" + scan.Devices.Count + "\r\n" +
                "HID 接口：" + hid.Count + "\r\n" +
                "罗技 HID++ 接口：" + hidppInterfaces + "\r\n" +
                "已知厂商接口：" + vendorInterfaces,
                "Devices found: " + scan.Devices.Count + "\r\n" +
                "HID interfaces: " + hid.Count + "\r\n" +
                "Logitech HID++ interfaces: " + hidppInterfaces + "\r\n" +
                "Known vendor interfaces: " + vendorInterfaces);
            return result;
        }

        private static int AppendHidppSection(
            StringBuilder report, List<HidDeviceInfo> hid)
        {
            report.AppendLine("Logitech HID++ 2.0 probes");
            report.AppendLine("-------------------------");
            int interfaceCount = 0;
            int deviceCount = 0;

            for (int i = 0; i < hid.Count; i++)
            {
                HidDeviceInfo info = hid[i];
                if (info.VendorId != 0x046D || info.UsagePage != 0xFF00)
                    continue;

                interfaceCount++;
                report.AppendLine("Interface: " + SanitizeInstanceId(info.InstanceId));
                report.AppendLine("  VID/PID: 0x" + info.VendorId.ToString("X4") +
                    " / 0x" + info.ProductId.ToString("X4"));
                report.AppendLine("  UsagePage/Usage: 0x" +
                    info.UsagePage.ToString("X4") + " / 0x" +
                    info.Usage.ToString("X4"));
                report.AppendLine("  Report lengths: in=" + info.InputReportLength +
                    " out=" + info.OutputReportLength);

                using (HidppConnection connection = HidppConnection.Open(info.Path))
                {
                    if (connection == null)
                    {
                        report.AppendLine("  Could not open this HID interface.");
                        report.AppendLine();
                        continue;
                    }

                    byte[] indices = new byte[] { 0xFF, 1, 2, 3, 4, 5, 6 };
                    for (int j = 0; j < indices.Length; j++)
                    {
                        byte index = indices[j];
                        byte batteryFeature = 0;
                        ushort batteryId = 0;
                        HidppResponse batteryDiscovery = null;

                        if (TryDiscoverFeature(connection, index, 0x1000,
                                out batteryFeature, out batteryDiscovery))
                        {
                            batteryId = 0x1000;
                        }
                        else if (TryDiscoverFeature(connection, index, 0x1004,
                                     out batteryFeature, out batteryDiscovery))
                        {
                            batteryId = 0x1004;
                        }
                        else if (TryDiscoverFeature(connection, index, 0x1001,
                                     out batteryFeature, out batteryDiscovery))
                        {
                            batteryId = 0x1001;
                        }

                        if (batteryFeature == 0)
                        {
                            report.AppendLine("  Index " + index +
                                ": no battery feature");
                            continue;
                        }

                        deviceCount++;
                        report.AppendLine("  Index " + index + ":");
                        report.AppendLine("    Battery feature 0x" +
                            batteryId.ToString("X4") + " -> index " +
                            batteryFeature);
                        AppendResponse(report, "    ", batteryDiscovery);

                        HidppResponse battery = connection.Request(
                            index, batteryFeature, 0x00, new byte[0], 350);
                        report.AppendLine("    Battery read:");
                        AppendResponse(report, "      ", battery);

                        HidppResponse nameDiscovery;
                        byte nameFeature;
                        if (TryDiscoverFeature(connection, index, 0x0005,
                                out nameFeature, out nameDiscovery))
                        {
                            report.AppendLine("    Device name feature -> index " +
                                nameFeature);
                            AppendResponse(report, "      ", nameDiscovery);
                            HidppResponse name = connection.Request(
                                index, nameFeature, 0x00, new byte[0], 300);
                            AppendResponse(report, "      ", name);
                        }

                        HidppResponse fwDiscovery;
                        byte fwFeature;
                        if (TryDiscoverFeature(connection, index, 0x0003,
                                out fwFeature, out fwDiscovery))
                        {
                            report.AppendLine("    Firmware feature -> index " +
                                fwFeature);
                            AppendResponse(report, "      ", fwDiscovery);
                            HidppResponse fw = connection.Request(
                                index, fwFeature, 0x00, new byte[0], 300);
                            AppendResponse(report, "      ", fw);
                        }

                        HidppResponse fsDiscovery;
                        byte fsFeature;
                        if (TryDiscoverFeature(connection, index, 0x0001,
                                out fsFeature, out fsDiscovery))
                        {
                            report.AppendLine("    Feature set -> index " + fsFeature);
                            AppendResponse(report, "      ", fsDiscovery);
                            HidppResponse count = connection.Request(
                                index, fsFeature, 0x00, new byte[0], 300);
                            report.AppendLine("      Feature count:");
                            AppendResponse(report, "        ", count);
                            int featureCount = 0;
                            if (count != null && !count.IsError &&
                                count.Parameters.Length > 0)
                                featureCount = count.Parameters[0];
                            int limit = Math.Min(featureCount, 16);
                            for (int k = 0; k < limit; k++)
                            {
                                HidppResponse feature = connection.Request(
                                    index, fsFeature, 0x01,
                                    new byte[] { (byte)k }, 250);
                                report.AppendLine("      Feature #" + k + ":");
                                AppendResponse(report, "        ", feature);
                            }
                        }
                    }
                }
                report.AppendLine();
            }

            if (interfaceCount == 0)
                report.AppendLine("(no Logitech HID++ interface found)");
            report.AppendLine("HID++ interfaces: " + interfaceCount +
                ", responding device indices: " + deviceCount);
            report.AppendLine();
            return interfaceCount;
        }

        private static int AppendVendorSection(
            StringBuilder report, List<HidDeviceInfo> hid)
        {
            report.AppendLine("Known vendor protocol probes");
            report.AppendLine("----------------------------");
            int count = 0;

            for (int i = 0; i < hid.Count; i++)
            {
                HidDeviceInfo info = hid[i];

                if (info.VendorId == AtkVxeProvider.VendorId &&
                    info.ProductId == AtkVxeProvider.ProductId &&
                    info.UsagePage == AtkVxeProvider.UsagePage &&
                    info.OutputReportLength >= 64)
                {
                    count++;
                    report.AppendLine("ATK/VXE 373B:1216");
                    report.AppendLine("  Instance: " + SanitizeInstanceId(info.InstanceId));
                    byte[] request;
                    byte[] response;
                    string detail;
                    int percent = AtkVxeProvider.Query(
                        info, out request, out response, out detail);
                    report.AppendLine("  Request: " + TextUtil.Hex(request, 64));
                    report.AppendLine("  Response: " + TextUtil.Hex(response, 64));
                    report.AppendLine("  Result: " + (percent >= 0
                        ? percent + "%"
                        : detail));
                    report.AppendLine();
                }

                if (info.VendorId == DarkrimProtocol.VendorId &&
                    info.UsagePage == DarkrimProtocol.UsagePage &&
                    info.Usage == DarkrimProtocol.Usage &&
                    info.OutputReportLength >= DarkrimProtocol.ReportLength)
                {
                    count++;
                    report.AppendLine("DARKRIM 3654:6901");
                    report.AppendLine("  Instance: " + SanitizeInstanceId(info.InstanceId));
                    byte[] request;
                    byte[] response;
                    string detail;
                    int percent = DarkrimProvider.Query(
                        info, out request, out response, out detail);
                    report.AppendLine("  Request: " + TextUtil.Hex(request, 65));
                    report.AppendLine("  Response: " + TextUtil.Hex(response, 65));
                    report.AppendLine("  Result: " + (percent >= 0
                        ? percent + "%"
                        : detail));
                    report.AppendLine();
                }
            }

            if (count == 0)
                report.AppendLine("(no ATK/VXE or DARKRIM interface found)");
            report.AppendLine();
            return count;
        }

        private static bool TryDiscoverFeature(
            HidppConnection connection, byte index, ushort featureId,
            out byte featureIndex, out HidppResponse response)
        {
            featureIndex = 0;
            byte[] parameters = new byte[2];
            parameters[0] = (byte)(featureId >> 8);
            parameters[1] = (byte)featureId;
            response = connection.Request(
                index, 0x00, 0x00, parameters, 220);
            if (response == null || response.IsError ||
                response.Parameters.Length < 1)
                return false;
            featureIndex = response.Parameters[0];
            return featureIndex != 0;
        }

        private static void AppendResponse(
            StringBuilder report, string indent, HidppResponse response)
        {
            if (response == null)
            {
                report.AppendLine(indent + "(no response)");
                return;
            }
            if (response.RawRequest != null && response.RawRequest.Length > 0)
                report.AppendLine(indent + "req: " +
                    TextUtil.Hex(response.RawRequest, 32));
            if (response.RawResponse != null && response.RawResponse.Length > 0)
                report.AppendLine(indent + "rsp: " +
                    TextUtil.Hex(response.RawResponse, 32));
            if (response.IsError)
                report.AppendLine(indent + "error: " + response.ErrorCode);
            else if (response.Parameters != null && response.Parameters.Length > 0)
                report.AppendLine(indent + "params: " +
                    TextUtil.Hex(response.Parameters, 32));
        }

        private static string ClassifyHid(HidDeviceInfo info)
        {
            if (info.VendorId == 0x046D && info.UsagePage == 0xFF00)
                return "Logitech HID++ 2.0 candidate";
            if (info.VendorId == AtkVxeProvider.VendorId &&
                info.ProductId == AtkVxeProvider.ProductId &&
                info.UsagePage == AtkVxeProvider.UsagePage)
                return "ATK/VXE 373B:1216 candidate";
            if (info.VendorId == DarkrimProtocol.VendorId &&
                info.UsagePage == DarkrimProtocol.UsagePage &&
                info.Usage == DarkrimProtocol.Usage)
                return "DARKRIM 3654:6901 candidate";
            if (info.UsagePage == 0x0006 && info.Usage == 0x0020)
                return "standard HID Battery Strength";
            if (info.UsagePage >= 0xFF00)
                return "vendor-defined usage";
            return "";
        }

        private static string SanitizeInstanceId(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId))
                return "";
            string clean = TextUtil.Clean(instanceId);
            string[] parts = clean.Split('\\');
            if (parts.Length >= 3 &&
                (parts[0].Equals("HID", StringComparison.OrdinalIgnoreCase) ||
                 parts[0].Equals("USB", StringComparison.OrdinalIgnoreCase)))
            {
                return parts[0] + "\\" + parts[1] + "\\<redacted>";
            }
            if (clean.StartsWith("BTHLE\\DEV_", StringComparison.OrdinalIgnoreCase) ||
                clean.StartsWith("BTHENUM\\DEV_", StringComparison.OrdinalIgnoreCase))
            {
                return Regex.Replace(
                    clean, "(?i)(DEV_)[0-9A-F]{12}", "$1<redacted>");
            }
            return Regex.Replace(
                clean, "(?i)\\b[0-9A-F]{12}\\b", "<12HEX-REDACTED>");
        }

        private static string SanitizeHidLog(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            string clean = TextUtil.Clean(text);
            clean = Regex.Replace(clean, @" path=\S+", " path=<redacted>");
            clean = Regex.Replace(
                clean, "(?i)\\b[0-9A-F]{12}\\b", "<12HEX-REDACTED>");
            return TextUtil.Truncate(clean, 300);
        }

        private static string SanitizeText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            string clean = TextUtil.Clean(text);
            clean = Regex.Replace(
                clean, "(?i)\\b[0-9A-F]{12}\\b", "<12HEX-REDACTED>");
            return TextUtil.Truncate(clean, 500);
        }

        private static string ResolveReportPath(string outputPath)
        {
            if (!string.IsNullOrEmpty(outputPath))
                return Path.GetFullPath(outputPath);

            string directory = Environment.GetFolderPath(
                Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                directory = Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments);
            }
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                directory = Environment.CurrentDirectory;
            return Path.Combine(directory,
                "Gear-Batt-Report-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".txt");
        }

        private static string GetVersion()
        {
            try
            {
                Version version = Assembly.GetExecutingAssembly()
                    .GetName().Version;
                return version.ToString(3);
            }
            catch (Exception)
            {
                return "1.0.0";
            }
        }
    }
}

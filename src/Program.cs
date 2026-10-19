using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace GearBatt
{
    internal static class Program
    {
        private static Mutex _mutex;

        [STAThread]
        private static void Main(string[] args)
        {
            Native.EnableDpiAwareness();
            L10n.Initialize(GetArgValue(args, "--lang"));

            bool mock = HasArg(args, "--mock");
            string outPath = GetArgValue(args, "--out");
            string selectId = GetArgValue(args, "--select");

            if (HasArg(args, "--help") || HasArg(args, "-h") || HasArg(args, "/?"))
            {
                ShowHelp();
                return;
            }

            if (HasArg(args, "--selftest"))
            {
                int failures = RunSelfTest(outPath);
                Environment.ExitCode = failures == 0 ? 0 : 1;
                return;
            }

            if (HasArg(args, "--render-icons"))
            {
                string directory = GetArgValue(args, "--render-icons");
                if (string.IsNullOrEmpty(directory))
                    directory = Path.Combine(Environment.CurrentDirectory, "preview");
                RenderIconPreviews(directory);
                return;
            }

            if (HasArg(args, "--diag"))
            {
                RunDiagnostics(mock, HasArg(args, "--verbose"), outPath);
                return;
            }

            if (!string.IsNullOrEmpty(selectId))
            {
                Settings settings = Settings.Load();
                settings.SelectedId = selectId;
                settings.SelectedName = "";
                settings.Save();
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                MessageBox.Show(
                    L10n.T("Gear-Batt 发生未处理的错误：\n\n",
                        "Gear-Batt encountered an unhandled error:\n\n") +
                    e.Exception.Message,
                    "Gear-Batt", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };

            bool createdNew;
            _mutex = new Mutex(
                true,
                "Local\\GearBatt.SingleInstance" + (mock ? ".Mock" : ""),
                out createdNew);
            if (!createdNew)
            {
                MessageBox.Show(
                    L10n.T(
                        "Gear-Batt 已在运行。\n请查看任务栏通知区域（可能需要展开隐藏图标）。",
                        "Gear-Batt is already running.\nCheck the notification area (you may need to expand hidden icons)."),
                    "Gear-Batt", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                Application.Run(new TrayApp(mock));
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    L10n.T("Gear-Batt 启动失败：\n\n",
                        "Gear-Batt failed to start:\n\n") + ex.Message,
                    "Gear-Batt", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch (Exception)
                {
                }
                _mutex.Dispose();
                _mutex = null;
            }
        }

        private static void RunDiagnostics(bool mock, bool verbose, string outPath)
        {
            Scanner scanner = new Scanner();
            ScanResult result;
            try
            {
                result = scanner.Scan(mock, verbose);
            }
            catch (Exception ex)
            {
                result = new ScanResult();
                result.AddError(ex.ToString());
            }

            Settings settings = Settings.Load();
            StringBuilder report = new StringBuilder();
            report.AppendLine("Gear-Batt " + GetVersion());
            report.AppendLine("Generated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            report.AppendLine("OS: " + Environment.OSVersion.VersionString);
            report.AppendLine("Process: " + (IntPtr.Size * 8) + "-bit");
            report.AppendLine("Mode: " + (mock ? "mock" : "real"));
            report.AppendLine("Tray icon size: " + GetTrayIconSize() + " px");
            report.AppendLine("Selected id: " + settings.SelectedId);
            report.AppendLine("Selected name: " + settings.SelectedName);
            report.AppendLine();
            report.AppendLine("Devices (" + result.Devices.Count + "):");
            if (result.Devices.Count == 0)
            {
                report.AppendLine("  (none)");
            }
            else
            {
                for (int i = 0; i < result.Devices.Count; i++)
                {
                    WirelessDevice d = result.Devices[i];
                    report.AppendLine("  [" + d.Source + "] [" + d.Provider + "] " +
                        d.Name + " - " + d.PercentText + " - " + d.StatusText);
                    report.AppendLine("    id: " + d.Id);
                    if (!string.IsNullOrEmpty(d.Detail))
                        report.AppendLine("    detail: " + d.Detail);
                }
            }

            if (result.Notes.Count > 0)
            {
                report.AppendLine();
                report.AppendLine("Notes:");
                for (int i = 0; i < result.Notes.Count; i++)
                    report.AppendLine("  " + result.Notes[i]);
            }

            if (result.Errors.Count > 0)
            {
                report.AppendLine();
                report.AppendLine("Errors:");
                for (int i = 0; i < result.Errors.Count; i++)
                    report.AppendLine("  " + result.Errors[i]);
            }

            report.AppendLine();
            report.AppendLine("Hint: only devices that report battery to Windows, Logitech HID++, ATK/VXE, DARKRIM, or the Bluetooth battery property can be listed.");

            string path = string.IsNullOrEmpty(outPath)
                ? Path.Combine(Path.GetTempPath(), "GearBatt-diag.txt")
                : Path.GetFullPath(outPath);
            WriteReportFile(path, report.ToString());

            bool attached = Native.AttachConsole(Native.ATTACH_PARENT_PROCESS);
            if (attached)
            {
                try
                {
                    Console.WriteLine(report.ToString());
                    Console.WriteLine("Report written to: " + path);
                    Console.Out.Flush();
                }
                catch (Exception)
                {
                }
            }
            else if (string.IsNullOrEmpty(outPath))
            {
                MessageBox.Show(
                    report.ToString() + L10n.T(
                        "\n\n报告已保存到：\n", "\n\nReport saved to:\n") + path,
                    L10n.T("Gear-Batt 诊断", "Gear-Batt diagnostics"),
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private static int RunSelfTest(string outPath)
        {
            StringBuilder sb = new StringBuilder();
            int failures = 0;
            sb.AppendLine("Gear-Batt self-test");

            HidppBattery battery = HidppProvider.ParseBatteryStatus(
                new byte[] { 87, 80, 1 });
            Check(sb, ref failures, "0x1000 charging",
                battery != null && battery.Percent == 87 && battery.Charging);

            battery = HidppProvider.ParseBatteryStatus(
                new byte[] { 42, 40, 0 });
            Check(sb, ref failures, "0x1000 battery",
                battery != null && battery.Percent == 42 && !battery.Charging);

            battery = HidppProvider.ParseBatteryStatus(
                new byte[] { 0, 0, 0 });
            Check(sb, ref failures, "0x1000 zero is unknown",
                battery != null && battery.Percent == -1);

            battery = HidppProvider.ParseUnifiedBattery(
                new byte[] { 55, 0, 1, 0 });
            Check(sb, ref failures, "0x1004 charging",
                battery != null && battery.Percent == 55 && battery.Charging);

            battery = HidppProvider.ParseUnifiedBattery(
                new byte[] { 0, 8, 3, 0 });
            Check(sb, ref failures, "0x1004 approximate full",
                battery != null && battery.Percent == 90 &&
                battery.Approximate && battery.Charging);

            battery = HidppProvider.ParseBatteryVoltage(
                new byte[] { 0x0E, 0x74, 0x80 });
            Check(sb, ref failures, "0x1001 voltage charging",
                battery != null && battery.Approximate && battery.Charging &&
                battery.Percent >= 40 && battery.Percent <= 50);

            Check(sb, ref failures, "voltage 4200",
                HidppProvider.VoltageToPercent(4200) == 100);
            Check(sb, ref failures, "voltage 3300",
                HidppProvider.VoltageToPercent(3300) == 0);
            Check(sb, ref failures, "generic HID name",
                TextUtil.IsGenericDeviceName("HID-compliant mouse"));
            Check(sb, ref failures, "real device name",
                !TextUtil.IsGenericDeviceName("LM129 BT"));

            int atkPercent;
            Check(sb, ref failures, "ATK response parser",
                AtkVxeProvider.TryParseResponse(
                    new byte[] { 0x08, 0x72, 0, 0, 0, 0x07, 0, 87 },
                    out atkPercent) && atkPercent == 87);

            byte[] darkrimQuery = DarkrimProtocol.BuildBatteryQuery();
            Check(sb, ref failures, "DARKRIM query CRC",
                darkrimQuery.Length == 65 &&
                darkrimQuery[12] == 0x58 && darkrimQuery[13] == 0x09);

            byte[] darkrimResponse = DarkrimProtocol.BuildFrame(
                0, new byte[] { 0x01, 0x01, 0x46 }, 0x11, 0x02, 0, 0);
            int darkrimPercent;
            byte darkrimSeq;
            List<DarkrimTlv> darkrimTlvs;
            Check(sb, ref failures, "DARKRIM TLV battery",
                DarkrimProtocol.TryGetBattery(
                    darkrimResponse, out darkrimPercent,
                    out darkrimSeq, out darkrimTlvs) &&
                darkrimPercent == 70);

            using (Bitmap bitmap = IconRenderer.RenderBitmap(16, 90, false, false))
            {
                Check(sb, ref failures, "icon has transparent corner",
                    bitmap.GetPixel(0, 0).A == 0);
                Check(sb, ref failures, "icon has battery pixels",
                    bitmap.GetPixel(6, 8).A > 0);
            }

            sb.AppendLine(failures == 0 ? "RESULT: PASS" : "RESULT: FAIL");
            string path = string.IsNullOrEmpty(outPath)
                ? Path.Combine(Path.GetTempPath(), "GearBatt-selftest.txt")
                : Path.GetFullPath(outPath);
            WriteReportFile(path, sb.ToString());

            bool attached = Native.AttachConsole(Native.ATTACH_PARENT_PROCESS);
            if (attached)
            {
                try
                {
                    Console.WriteLine(sb.ToString());
                    Console.Out.Flush();
                }
                catch (Exception)
                {
                }
            }
            else if (string.IsNullOrEmpty(outPath))
            {
                MessageBox.Show(sb.ToString(), "Gear-Batt self-test",
                    MessageBoxButtons.OK, failures == 0
                        ? MessageBoxIcon.Information
                        : MessageBoxIcon.Error);
            }
            return failures;
        }

        private static void Check(
            StringBuilder sb, ref int failures, string name, bool condition)
        {
            if (condition)
            {
                sb.AppendLine("PASS: " + name);
            }
            else
            {
                failures++;
                sb.AppendLine("FAIL: " + name);
            }
        }

        private static void RenderIconPreviews(string directory)
        {
            Directory.CreateDirectory(directory);
            int[] sizes = new int[] { 16, 24, 32, 48 };
            for (int i = 0; i < sizes.Length; i++)
            {
                string path = Path.Combine(
                    directory, "icons-" + sizes[i] + ".png");
                RenderIconSheet(sizes[i], path);
                Console.WriteLine(path);
            }
        }

        private static void RenderIconSheet(int size, string path)
        {
            int[] percents = new int[] { -1, 5, 20, 50, 90, 100, 5, 60 };
            bool[] charging = new bool[]
            {
                false, false, false, false, false, false, true, true
            };
            bool[] unknown = new bool[]
            {
                true, false, false, false, false, false, false, false
            };
            string[] labels = new string[]
            {
                "unknown", "5%", "20%", "50%", "90%", "100%",
                "charging 5%", "charging 60%"
            };

            int cellWidth = 160;
            int panelHeight = 220;
            int width = cellWidth * percents.Length;
            int height = panelHeight * 2 + 30;
            using (Bitmap sheet = new Bitmap(width, height, PixelFormat.Format32bppArgb))
            using (Graphics graphics = Graphics.FromImage(sheet))
            using (Font labelFont = new Font("Segoe UI", 8.5f))
            using (Font titleFont = new Font("Segoe UI", 10f, FontStyle.Bold))
            {
                graphics.SmoothingMode = SmoothingMode.None;
                graphics.PixelOffsetMode = PixelOffsetMode.Half;
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;

                Color[] backgrounds = new Color[]
                {
                    Color.FromArgb(255, 32, 32, 34),
                    Color.FromArgb(255, 243, 243, 245)
                };
                string[] titles = new string[]
                {
                    "Dark taskbar", "Light taskbar"
                };

                for (int panel = 0; panel < 2; panel++)
                {
                    int panelY = 30 + panel * panelHeight;
                    using (SolidBrush backgroundBrush = new SolidBrush(backgrounds[panel]))
                        graphics.FillRectangle(backgroundBrush, 0, panelY, width, panelHeight);
                    Color textColor = panel == 0 ? Color.White : Color.Black;
                    using (SolidBrush textBrush = new SolidBrush(textColor))
                    {
                        graphics.DrawString(
                            titles[panel] + "  —  " + size + " px (actual and zoomed)",
                            titleFont, textBrush, 10, panelY + 6);
                    }

                    int zoom = Math.Max(1, 96 / size);
                    for (int i = 0; i < percents.Length; i++)
                    {
                        int cellX = i * cellWidth;
                        int cellY = panelY + 32;
                        using (Bitmap icon = IconRenderer.RenderBitmap(
                                   size, percents[i], charging[i], unknown[i]))
                        {
                            graphics.DrawImage(
                                icon, cellX + (cellWidth - size) / 2, cellY,
                                size, size);
                            graphics.DrawImage(
                                icon,
                                new Rectangle(
                                    cellX + (cellWidth - 96) / 2,
                                    cellY + size + 12, 96, 96),
                                0, 0, size, size, GraphicsUnit.Pixel);
                        }
                        using (SolidBrush textBrush = new SolidBrush(textColor))
                        {
                            SizeF textSize = graphics.MeasureString(labels[i], labelFont);
                            graphics.DrawString(
                                labels[i], labelFont, textBrush,
                                cellX + (cellWidth - textSize.Width) / 2,
                                cellY + size + 114);
                        }
                    }
                }

                sheet.Save(path, ImageFormat.Png);
            }
        }

        private static void ShowHelp()
        {
            string text;
            if (L10n.Chinese)
            {
                text =
                    "Gear-Batt " + GetVersion() + "\n\n" +
                    "用法：\n" +
                    "  Gear-Batt.exe                     启动托盘图标\n" +
                    "  Gear-Batt.exe --lang zh|en        强制界面语言\n" +
                    "  Gear-Batt.exe --mock              使用模拟设备\n" +
                    "  Gear-Batt.exe --select <设备id>   指定要显示的设备\n" +
                    "  Gear-Batt.exe --diag [--out 文件] [--verbose]\n" +
                    "  Gear-Batt.exe --selftest [--out 文件]\n" +
                    "  Gear-Batt.exe --render-icons <目录>\n\n" +
                    "诊断模式会列出当前能读取到的设备和原始错误信息。";
            }
            else
            {
                text =
                    "Gear-Batt " + GetVersion() + "\n\n" +
                    "Usage:\n" +
                    "  Gear-Batt.exe                     Start the tray icon\n" +
                    "  Gear-Batt.exe --lang zh|en        Force UI language\n" +
                    "  Gear-Batt.exe --mock              Use mock devices\n" +
                    "  Gear-Batt.exe --select <device-id> Select the displayed device\n" +
                    "  Gear-Batt.exe --diag [--out file] [--verbose]\n" +
                    "  Gear-Batt.exe --selftest [--out file]\n" +
                    "  Gear-Batt.exe --render-icons <dir>\n\n" +
                    "Diagnostics lists readable devices and raw error details.";
            }
            bool attached = Native.AttachConsole(Native.ATTACH_PARENT_PROCESS);
            if (attached)
            {
                try
                {
                    Console.WriteLine(text);
                    Console.Out.Flush();
                    return;
                }
                catch (Exception)
                {
                }
            }
            MessageBox.Show(text, "Gear-Batt", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        private static void WriteReportFile(string path, string text)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(path, text, new UTF8Encoding(false));
            }
            catch (Exception)
            {
            }
        }

        private static int GetTrayIconSize()
        {
            int size = Native.GetSystemMetrics(Native.SM_CXSMICON);
            if (size < 16 || size > 64)
                size = 16;
            return size;
        }

        private static string GetVersion()
        {
            try
            {
                Version version = Assembly.GetExecutingAssembly().GetName().Version;
                return version.ToString(3);
            }
            catch (Exception)
            {
                return "1.0.0";
            }
        }

        private static bool HasArg(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string GetArgValue(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase))
                {
                    if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                        return args[i + 1];
                    return "";
                }
                string prefix = name + "=";
                if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return arg.Substring(prefix.Length);
            }
            return null;
        }
    }
}

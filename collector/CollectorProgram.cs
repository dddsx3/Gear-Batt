using System;
using System.Windows.Forms;

namespace GearBatt
{
    internal static class CollectorProgram
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Native.EnableDpiAwareness();
            L10n.Initialize(GetArg(args, "--lang"));

            if (HasArg(args, "--collect"))
            {
                string output = GetArg(args, "--out");
                string model = GetArg(args, "--model");
                CollectorResult result = CollectorEngine.Collect(model, output, false);
                bool attached = Native.AttachConsole(Native.ATTACH_PARENT_PROCESS);
                if (attached)
                {
                    try
                    {
                        Console.WriteLine(result.Summary);
                        Console.WriteLine("Report: " + result.ReportPath);
                        Console.Out.Flush();
                    }
                    catch (Exception)
                    {
                    }
                }
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new CollectorForm());
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

        private static string GetArg(string[] args, string name)
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

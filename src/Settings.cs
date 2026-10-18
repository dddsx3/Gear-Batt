using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Win32;

namespace GearBatt
{
    internal sealed class Settings
    {
        public string SelectedId = "";
        public string SelectedName = "";

        private const string RunKeyPath =
            @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "Gear-Batt";

        public static string DirectoryPath
        {
            get
            {
                string root = Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);
                return Path.Combine(root, "Gear-Batt");
            }
        }

        public static string FilePath
        {
            get { return Path.Combine(DirectoryPath, "settings.ini"); }
        }

        public static Settings Load()
        {
            Settings settings = new Settings();
            try
            {
                if (!File.Exists(FilePath))
                    return settings;
                string[] lines = File.ReadAllLines(FilePath, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrEmpty(line))
                        continue;
                    int equals = line.IndexOf('=');
                    if (equals <= 0)
                        continue;
                    string key = line.Substring(0, equals).Trim();
                    string value = line.Substring(equals + 1).Trim();
                    if (key == "selectedId")
                        settings.SelectedId = value;
                    else if (key == "selectedName")
                        settings.SelectedName = TextUtil.Clean(value);
                }
            }
            catch (Exception)
            {
            }
            return settings;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("selectedId=" + SelectedId);
                sb.AppendLine("selectedName=" + SelectedName);
                string temporary = FilePath + ".tmp";
                File.WriteAllText(temporary, sb.ToString(), new UTF8Encoding(false));
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
                File.Move(temporary, FilePath);
            }
            catch (Exception)
            {
            }
        }

        public static bool IsAutostartEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false))
                {
                    if (key == null)
                        return false;
                    object value = key.GetValue(RunValueName);
                    return value != null;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static bool SetAutostart(bool enabled, out string error)
        {
            error = "";
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath))
                {
                    if (key == null)
                    {
                        error = "无法打开开机启动注册表项。";
                        return false;
                    }
                    if (enabled)
                    {
                        string exe = Assembly.GetEntryAssembly().Location;
                        key.SetValue(RunValueName, "\"" + exe + "\"",
                            RegistryValueKind.String);
                    }
                    else
                    {
                        key.DeleteValue(RunValueName, false);
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }
    }
}

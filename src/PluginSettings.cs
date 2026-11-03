using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BetterGroundWindow
{
    /// <summary>
    /// Key=value settings persisted to %APPDATA%\BetterGroundWindow\settings.txt.
    /// </summary>
    internal static class PluginSettings
    {
        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BetterGroundWindow",
            "settings.txt");

        private static readonly Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static bool UnlimitedZoom
        {
            get => values.TryGetValue(nameof(UnlimitedZoom), out string v) && bool.TryParse(v, out bool b) && b;
            set
            {
                values[nameof(UnlimitedZoom)] = value.ToString();
                Save();
            }
        }

        public static void Load()
        {
            values.Clear();
            try
            {
                if (!File.Exists(FilePath))
                {
                    return;
                }

                foreach (string line in File.ReadAllLines(FilePath))
                {
                    int eq = line.IndexOf('=');
                    if (eq > 0)
                    {
                        values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                    }
                }
            }
            catch (Exception)
            {
                // Fall back to defaults if the file can't be read.
            }
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllLines(FilePath, values.Select(kv => kv.Key + "=" + kv.Value));
            }
            catch (Exception)
            {
                // Settings are a convenience; ignore I/O failures.
            }
        }
    }
}

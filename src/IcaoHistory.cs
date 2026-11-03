using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BetterGroundWindow
{
    /// <summary>
    /// Most-recently-used list of ICAO codes, shared by every Ground window and
    /// persisted to %APPDATA%\BetterGroundWindow\history.txt.
    /// </summary>
    internal static class IcaoHistory
    {
        private const int MaxEntries = 15;

        private static readonly List<string> entries = new List<string>();

        private static readonly string FilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "BetterGroundWindow",
            "history.txt");

        public static event EventHandler Changed;

        public static IReadOnlyList<string> Entries => entries;

        public static void Load()
        {
            entries.Clear();
            try
            {
                if (File.Exists(FilePath))
                {
                    entries.AddRange(File.ReadAllLines(FilePath)
                        .Select(Normalise)
                        .Where(IsValidIcao)
                        .Distinct()
                        .Take(MaxEntries));
                }
            }
            catch (Exception)
            {
                // A broken history file must never stop vatSys from starting.
            }
        }

        public static void Add(string icao)
        {
            icao = Normalise(icao);
            if (!IsValidIcao(icao))
            {
                return;
            }

            entries.Remove(icao);
            entries.Insert(0, icao);
            if (entries.Count > MaxEntries)
            {
                entries.RemoveRange(MaxEntries, entries.Count - MaxEntries);
            }

            Save();
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static void Clear()
        {
            entries.Clear();
            Save();
            Changed?.Invoke(null, EventArgs.Empty);
        }

        public static string Normalise(string text)
        {
            return (text ?? "").Trim().ToUpperInvariant();
        }

        public static bool IsValidIcao(string text)
        {
            return text.Length == 4 && text.All(char.IsLetterOrDigit);
        }

        private static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllLines(FilePath, entries);
            }
            catch (Exception)
            {
                // History is a convenience; ignore I/O failures.
            }
        }
    }
}

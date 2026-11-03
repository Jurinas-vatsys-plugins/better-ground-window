using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace BetterGroundWindow
{
    /// <summary>
    /// Supplies 0Harmony.dll, which is embedded in this DLL so the plugin can be
    /// installed as a single file. If another plugin (e.g. VATPRCPlugin) has
    /// already loaded a compatible Harmony 2.x, that copy is reused instead.
    /// </summary>
    internal static class EmbeddedAssemblies
    {
        private const string HarmonyName = "0Harmony";

        private const string HarmonyResource = "BetterGroundWindow.0Harmony.dll";

        private static readonly object sync = new object();

        private static Assembly harmony;

        private static bool registered;

        public static void Register()
        {
            lock (sync)
            {
                if (!registered)
                {
                    AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
                    registered = true;
                }
            }
        }

        private static Assembly OnAssemblyResolve(object sender, ResolveEventArgs args)
        {
            AssemblyName requested = new AssemblyName(args.Name);
            if (!string.Equals(requested.Name, HarmonyName, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            lock (sync)
            {
                if (harmony != null)
                {
                    return harmony;
                }

                Version wanted = requested.Version ?? new Version(2, 0);
                harmony = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => string.Equals(a.GetName().Name, HarmonyName, StringComparison.OrdinalIgnoreCase))
                    .Where(a => a.GetName().Version.Major == wanted.Major && a.GetName().Version >= wanted)
                    .OrderByDescending(a => a.GetName().Version)
                    .FirstOrDefault();

                if (harmony == null)
                {
                    using (Stream stream = typeof(EmbeddedAssemblies).Assembly.GetManifestResourceStream(HarmonyResource))
                    {
                        if (stream == null)
                        {
                            return null;
                        }

                        byte[] bytes = new byte[stream.Length];
                        stream.Read(bytes, 0, bytes.Length);
                        harmony = Assembly.Load(bytes);
                    }
                }

                return harmony;
            }
        }
    }
}

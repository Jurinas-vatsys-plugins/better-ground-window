using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using vatsys;

namespace BetterGroundWindow
{
    /// <summary>
    /// The "Unlimited Zoom" option. vatSys clamps the Ground display range to
    /// 0.1-30 NM inside ASDControlDX.SetZoom; <see cref="ZoomPatch"/> widens that
    /// clamp for Ground displays while this option is on.
    /// </summary>
    internal static class ZoomUnlock
    {
        /// <summary>Closest Ground range allowed when unlocked, in NM (vatSys: 0.1).</summary>
        public const float UnlockedMinRange = 0.01f;

        /// <summary>Widest Ground range allowed when unlocked, in NM (vatSys: 30, radar ASDs: 9999).</summary>
        public const float UnlockedMaxRange = 9999f;

        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        public static event EventHandler EnabledChanged;

        public static bool Enabled { get; private set; }

        /// <summary>Why the patch could not be installed, or null if it is active.</summary>
        public static string Unavailable { get; private set; } = "Not initialised.";

        public static void Initialize()
        {
            Enabled = PluginSettings.UnlimitedZoom;
            try
            {
                InstallPatch();
                Unavailable = null;
            }
            catch (Exception ex)
            {
                while ((ex is TargetInvocationException || ex is TypeInitializationException) && ex.InnerException != null)
                {
                    ex = ex.InnerException;
                }

                Unavailable = ex.Message;
            }
        }

        public static void SetEnabled(bool enabled)
        {
            if (enabled == Enabled)
            {
                return;
            }

            Enabled = enabled;
            PluginSettings.UnlimitedZoom = enabled;

            if (!enabled)
            {
                ReclampOpenGroundDisplays();
            }

            EnabledChanged?.Invoke(null, EventArgs.Empty);
        }

        // Kept in its own non-inlined method so Harmony is only resolved once
        // EmbeddedAssemblies is ready, and a missing Harmony is caught above.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InstallPatch()
        {
            ZoomPatch.Install();
        }

        /// <summary>
        /// After switching the option off, push any display that is zoomed past
        /// the normal limits back inside them.
        /// </summary>
        private static void ReclampOpenGroundDisplays()
        {
            foreach (Form form in Application.OpenForms.Cast<Form>().ToList())
            {
                if (form.IsDisposed || form.GetType().FullName != BetterGroundWindowPlugin.GroundWindowTypeName)
                {
                    continue;
                }

                object display = form.GetType().GetField("asmgcsControlDX1", PrivateInstance)?.GetValue(form);
                if (display == null)
                {
                    continue;
                }

                Type type = display.GetType();
                object range = type.GetField("range", PrivateInstance)?.GetValue(display);
                MethodInfo setZoom = type.GetMethod("SetZoom", new[] { typeof(double), typeof(bool), typeof(bool), typeof(bool) });
                if (range is double current && setZoom != null)
                {
                    setZoom.Invoke(display, new object[] { current, true, true, true });
                }
            }
        }
    }
}

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using vatsys;

namespace BetterGroundWindow
{
    /// <summary>
    /// "Top-Down Mode" (internally still EasyMode; the saved hotkey key is EasyModeHotkey): Ground displays show the network callsign of aircraft that are
    /// on the ground without Mode C (and without a coupled flight plan), which
    /// vatSys otherwise draws as a bare symbol.
    ///
    /// It is display-only. Nothing is written to flight plans, coupling, radar
    /// tracks or strips; the extra labels are not clickable. Aircraft with Mode C,
    /// Mode S/ADS-B or a coupled flight plan are labelled by vatSys as usual.
    ///
    /// Off at every vatSys start; toggled with a hotkey (default Ctrl+T).
    /// </summary>
    internal static class EasyMode
    {
        public static readonly Keys DefaultHotkey = Keys.Control | Keys.T;

        private static HotkeyFilter filter;

        public static event EventHandler Changed;

        public static bool Enabled { get; private set; }

        public static Keys Hotkey { get; private set; } = DefaultHotkey;

        /// <summary>Why the patch could not be installed, or null if it is active.</summary>
        public static string Unavailable { get; private set; } = "Not initialised.";

        public static void Initialize()
        {
            Hotkey = PluginSettings.EasyModeHotkey ?? DefaultHotkey;

            try
            {
                InstallPatch();
                Unavailable = null;
            }
            catch (Exception ex)
            {
                Unavailable = Describe(ex);
                return;
            }

            // The main-ASD ground view is an extra: if it can't be installed,
            // Easy Mode still works on Ground windows.
            try
            {
                InstallTopDownPatch();
                TopDownUnavailable = null;
            }
            catch (Exception ex)
            {
                TopDownUnavailable = Describe(ex);
            }

            filter = new HotkeyFilter();
            Application.AddMessageFilter(filter);
        }

        /// <summary>Why the main-ASD ground view could not be installed, or null if it is active.</summary>
        public static string TopDownUnavailable { get; private set; } = "Not initialised.";

        public static void SetEnabled(bool enabled)
        {
            if (Unavailable != null || enabled == Enabled)
            {
                return;
            }

            Enabled = enabled;
            Changed?.Invoke(null, EventArgs.Empty);

            // Ground windows are redrawn by GroundWindowBar; the main ASD here.
            if (TopDownUnavailable == null)
            {
                RedrawMainAsd();
            }
        }

        private static string Describe(Exception ex)
        {
            while ((ex is TargetInvocationException || ex is TypeInitializationException) && ex.InnerException != null)
            {
                ex = ex.InnerException;
            }

            return ex.Message;
        }

        private static void RedrawMainAsd()
        {
            BindingFlags any = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic;
            object main = typeof(MMI).GetField("MainForm", any)?.GetValue(null);
            (main?.GetType().GetField("ASD", any)?.GetValue(main) as Control)?.Invalidate();
        }

        public static void SetHotkey(Keys keys)
        {
            Hotkey = keys;
            PluginSettings.EasyModeHotkey = keys;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>"Ctrl+T", "Ctrl+Shift+F5", or "None".</summary>
        public static string Describe(Keys keys)
        {
            Keys code = keys & Keys.KeyCode;
            if (code == Keys.None)
            {
                return "None";
            }

            string text = "";
            if ((keys & Keys.Control) != 0)
            {
                text += "Ctrl+";
            }

            if ((keys & Keys.Shift) != 0)
            {
                text += "Shift+";
            }

            if ((keys & Keys.Alt) != 0)
            {
                text += "Alt+";
            }

            string name = code.ToString();
            if (code >= Keys.D0 && code <= Keys.D9)
            {
                name = name.Substring(1);
            }

            return text + name;
        }

        /// <summary>Set while the settings window is recording a new hotkey.</summary>
        internal static bool CapturingHotkey { get; set; }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InstallPatch()
        {
            EasyModePatch.Install();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void InstallTopDownPatch()
        {
            TopDownPatch.Install();
        }

        /// <summary>
        /// Sees key presses for every vatSys window on the GUI thread, so the
        /// hotkey works whichever window has focus.
        /// </summary>
        private sealed class HotkeyFilter : IMessageFilter
        {
            private const int WmKeyDown = 0x0100;

            private const int WmSysKeyDown = 0x0104;

            public bool PreFilterMessage(ref Message m)
            {
                if ((m.Msg != WmKeyDown && m.Msg != WmSysKeyDown) || CapturingHotkey)
                {
                    return false;
                }

                Keys code = (Keys)(m.WParam.ToInt64() & 0xFFFF);
                Keys hotkey = Hotkey;
                if ((hotkey & Keys.KeyCode) == Keys.None || code != (hotkey & Keys.KeyCode) || Control.ModifierKeys != (hotkey & Keys.Modifiers))
                {
                    return false;
                }

                // Ignore auto-repeat while the keys are held down.
                if ((m.LParam.ToInt64() & 0x40000000) != 0)
                {
                    return true;
                }

                SetEnabled(!Enabled);
                return true;
            }
        }
    }
}

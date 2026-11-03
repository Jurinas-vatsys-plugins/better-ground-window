using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Windows.Forms;
using vatsys;
using vatsys.Plugin;

namespace BetterGroundWindow
{
    /// <summary>
    /// Entry point loaded by vatSys through MEF. vatSys does not expose the Ground
    /// (ASMGCS) window to plugins, so we watch the open forms and attach an
    /// <see cref="GroundWindowBar"/> to every Ground window that appears.
    /// </summary>
    [Export(typeof(IPlugin))]
    public class BetterGroundWindowPlugin : IPlugin
    {
        internal const string GroundWindowTypeName = "vatsys.ASMGCSWindow";

        private const int ScanIntervalMs = 500;

        private const string ActiveInstanceKey = "BetterGroundWindow.ActiveInstance";

        private readonly Dictionary<Form, GroundWindowBar> bars = new Dictionary<Form, GroundWindowBar>();

        private Timer scanTimer;

        public string Name => "Better Ground Window";

        public BetterGroundWindowPlugin()
        {
            // The DLL may be installed in both <profile>\Plugins and <vatSys>\bin\Plugins.
            // Two different builds would load as two instances, so only the first one
            // (vatSys scans the profile folder first) does any work.
            if (AppDomain.CurrentDomain.GetData(ActiveInstanceKey) != null)
            {
                return;
            }

            AppDomain.CurrentDomain.SetData(ActiveInstanceKey, typeof(BetterGroundWindowPlugin).Assembly.Location);

            IcaoHistory.Load();

            // vatSys constructs plugins on the GUI thread during MainForm load, but
            // fall back to marshalling onto it in case that ever changes.
            if (SynchronizationContextIsWinForms())
            {
                StartScanning();
            }
            else
            {
                MMI.InvokeOnGUI(StartScanning);
            }
        }

        public void OnFDRUpdate(FDP2.FDR updated)
        {
        }

        public void OnRadarTrackUpdate(RDP.RadarTrack updated)
        {
        }

        private static bool SynchronizationContextIsWinForms()
        {
            return System.Threading.SynchronizationContext.Current is WindowsFormsSynchronizationContext;
        }

        private void StartScanning()
        {
            if (scanTimer != null)
            {
                return;
            }

            scanTimer = new Timer { Interval = ScanIntervalMs };
            scanTimer.Tick += (s, e) => ScanForGroundWindows();
            scanTimer.Start();
        }

        private void ScanForGroundWindows()
        {
            foreach (Form form in Application.OpenForms.Cast<Form>().ToList())
            {
                if (form.IsDisposed || bars.ContainsKey(form) || form.GetType().FullName != GroundWindowTypeName)
                {
                    continue;
                }

                GroundWindowBar bar = GroundWindowBar.TryAttach(form);
                if (bar == null)
                {
                    continue;
                }

                bars[form] = bar;
                form.FormClosed += (s, e) =>
                {
                    if (bars.TryGetValue(form, out GroundWindowBar closing))
                    {
                        closing.Detach();
                        bars.Remove(form);
                    }
                };
            }
        }
    }
}

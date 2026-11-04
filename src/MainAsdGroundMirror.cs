using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using vatsys;

namespace BetterGroundWindow
{
    /// <summary>
    /// While the top-down view is active (Easy Mode on, main ASD at 30 NM or less),
    /// the main ASD also shows the ground maps (runways, taxiways, taxilines,
    /// labels, ...) that the open Ground windows are showing. Kept in sync from the
    /// plugin's scan timer. Only maps this class added are removed again, so ground
    /// maps ticked by hand in Maps > Ground Maps stay as the user left them.
    /// </summary>
    internal static class MainAsdGroundMirror
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>Maps this class made visible on the main ASD.</summary>
        private static readonly HashSet<DisplayMaps.Map> mirrored = new HashSet<DisplayMaps.Map>();

        private static Control mainAsd;

        private static MethodInfo isMapVisible;

        private static MethodInfo setMapVisible;

        public static void Update()
        {
            Control asd = MainAsd();
            if (asd == null)
            {
                return;
            }

            bool active = EasyMode.Enabled && EasyMode.TopDownUnavailable == null && TopDownPatch.Active(asd);
            Sync(asd, active ? GroundDisplays() : new List<Control>());
        }

        /// <summary>
        /// Makes <paramref name="asd"/> show exactly the ground maps visible in
        /// <paramref name="groundDisplays"/>, plus whatever it showed on its own.
        /// </summary>
        internal static void Sync(Control asd, List<Control> groundDisplays)
        {
            if (isMapVisible == null || setMapVisible == null)
            {
                isMapVisible = asd.GetType().GetMethod("IsMapVisible", AnyInstance, null, new[] { typeof(DisplayMaps.Map) }, null);
                setMapVisible = asd.GetType().GetMethod("SetMapVisible", AnyInstance, null, new[] { typeof(DisplayMaps.Map), typeof(bool) }, null);
            }

            HashSet<DisplayMaps.Map> wanted = GroundMaps(groundDisplays);

            foreach (DisplayMaps.Map map in mirrored.Where(m => !wanted.Contains(m)).ToList())
            {
                SetVisible(asd, map, false);
                mirrored.Remove(map);
            }

            foreach (DisplayMaps.Map map in wanted)
            {
                // Also re-adds maps after a main ASD position change cleared them.
                if (!IsVisible(asd, map))
                {
                    SetVisible(asd, map, true);
                    mirrored.Add(map);
                }
            }
        }

        /// <summary>Number of maps currently added to the main ASD by the mirror.</summary>
        internal static int MirroredCount => mirrored.Count;

        /// <summary>The ground displays of all open Ground windows.</summary>
        private static List<Control> GroundDisplays()
        {
            return Application.OpenForms.Cast<Form>()
                .Where(f => !f.IsDisposed && f.GetType().FullName == BetterGroundWindowPlugin.GroundWindowTypeName)
                .Select(f => f.GetType().GetField("asmgcsControlDX1", AnyInstance)?.GetValue(f) as Control)
                .Where(c => c != null && !c.IsDisposed)
                .ToList();
        }

        /// <summary>Ground-category maps visible in any of the given displays.</summary>
        private static HashSet<DisplayMaps.Map> GroundMaps(List<Control> displays)
        {
            HashSet<DisplayMaps.Map> maps = new HashSet<DisplayMaps.Map>();
            if (displays.Count == 0)
            {
                return maps;
            }

            foreach (DisplayMaps.Map map in DisplayMaps.Maps)
            {
                if (map.Category == DisplayMaps.MapCategories.Ground && displays.Any(d => IsVisible(d, map)))
                {
                    maps.Add(map);
                }
            }

            return maps;
        }

        private static Control MainAsd()
        {
            if (mainAsd != null && !mainAsd.IsDisposed)
            {
                return mainAsd;
            }

            object main = typeof(MMI).GetField("MainForm", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
            mainAsd = main?.GetType().GetField("ASD", AnyInstance)?.GetValue(main) as Control;
            if (mainAsd == null)
            {
                return null;
            }

            isMapVisible = mainAsd.GetType().GetMethod("IsMapVisible", AnyInstance, null, new[] { typeof(DisplayMaps.Map) }, null);
            setMapVisible = mainAsd.GetType().GetMethod("SetMapVisible", AnyInstance, null, new[] { typeof(DisplayMaps.Map), typeof(bool) }, null);
            if (isMapVisible == null || setMapVisible == null)
            {
                mainAsd = null;
            }

            return mainAsd;
        }

        private static bool IsVisible(Control display, DisplayMaps.Map map)
        {
            MethodInfo method = isMapVisible ?? display.GetType().GetMethod("IsMapVisible", AnyInstance, null, new[] { typeof(DisplayMaps.Map) }, null);
            return method != null && (bool)method.Invoke(display, new object[] { map });
        }

        private static void SetVisible(Control display, DisplayMaps.Map map, bool visible)
        {
            setMapVisible.Invoke(display, new object[] { map, visible });
        }
    }
}

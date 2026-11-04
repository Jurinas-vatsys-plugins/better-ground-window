using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using vatsys;

namespace BetterGroundWindow
{
    /// <summary>
    /// While the top-down view is active (Top-Down Mode on, main ASD at
    /// <see cref="PluginSettings.TopDownRangeNm"/> or less), the main ASD shows every
    /// ground map (runways, taxiways, taxilines, aprons, buildings, labels) of the
    /// airports in view. Airports outside the view are not loaded, which keeps the
    /// map redraws cheap. No Ground window is needed.
    ///
    /// Kept in sync from the plugin's scan timer, so panning and zooming bring
    /// airports in and out. Only maps this class added are removed again, so
    /// ground maps ticked by hand in Maps > Ground Maps stay as the user left them.
    /// </summary>
    internal static class MainAsdGroundMaps
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        /// <summary>Margin added to the view radius so an airport appears before its centre is on screen.</summary>
        private const double AirportMarginNm = 5.0;

        /// <summary>Maps this class made visible on the main ASD.</summary>
        private static readonly HashSet<DisplayMaps.Map> added = new HashSet<DisplayMaps.Map>();

        /// <summary>Map folder (normally the airport ICAO) to airport, or null if it isn't one.</summary>
        private static readonly Dictionary<string, Airspace2.Airport> folderAirports = new Dictionary<string, Airspace2.Airport>(StringComparer.OrdinalIgnoreCase);

        private static Control mainAsd;

        private static MethodInfo isMapVisible;

        private static MethodInfo setMapVisible;

        private static FieldInfo rangeField;

        public static void Update()
        {
            Control asd = MainAsd();
            if (asd == null)
            {
                return;
            }

            bool active = EasyMode.Enabled && EasyMode.TopDownUnavailable == null && TopDownPatch.Active(asd);
            double range = rangeField.GetValue(asd) is double r ? r : 0;
            Sync(asd, MMI.MainASDCentre, range, active);
        }

        /// <summary>
        /// Shows the ground maps of the airports within view of <paramref name="centre"/>
        /// at <paramref name="range"/> when <paramref name="active"/>, and removes the
        /// maps this class added otherwise.
        /// </summary>
        internal static void Sync(Control asd, Coordinate centre, double range, bool active)
        {
            if (isMapVisible == null || setMapVisible == null)
            {
                isMapVisible = asd.GetType().GetMethod("IsMapVisible", AnyInstance, null, new[] { typeof(DisplayMaps.Map) }, null);
                setMapVisible = asd.GetType().GetMethod("SetMapVisible", AnyInstance, null, new[] { typeof(DisplayMaps.Map), typeof(bool) }, null);
            }

            HashSet<DisplayMaps.Map> wanted = active && centre != null
                ? MapsInView(centre, range)
                : new HashSet<DisplayMaps.Map>();

            foreach (DisplayMaps.Map map in added.Where(m => !wanted.Contains(m)).ToList())
            {
                SetVisible(asd, map, false);
                added.Remove(map);
            }

            foreach (DisplayMaps.Map map in wanted)
            {
                // Also re-adds maps after a main ASD position change cleared them.
                if (!IsVisible(asd, map))
                {
                    SetVisible(asd, map, true);
                    added.Add(map);
                }
            }
        }

        /// <summary>Number of maps currently added to the main ASD by this class.</summary>
        internal static int AddedCount => added.Count;

        /// <summary>
        /// How far from the display centre an airport counts as in view: a bit more
        /// than half the screen diagonal, never more than the top-down range.
        /// </summary>
        internal static double ViewRadiusNm(double range) => Math.Min(range * 0.6 + AirportMarginNm, PluginSettings.TopDownRangeNm);

        /// <summary>Ground maps whose airport is within view.</summary>
        internal static HashSet<DisplayMaps.Map> MapsInView(Coordinate centre, double range)
        {
            double radius = ViewRadiusNm(range);
            HashSet<DisplayMaps.Map> maps = new HashSet<DisplayMaps.Map>();
            foreach (DisplayMaps.Map map in DisplayMaps.Maps)
            {
                if (map.Category != DisplayMaps.MapCategories.Ground)
                {
                    continue;
                }

                Airspace2.Airport airport = AirportOf(map);
                if (airport?.LatLong != null && Conversions.CalculateDistance(centre, airport.LatLong) <= radius)
                {
                    maps.Add(map);
                }
            }

            return maps;
        }

        private static Airspace2.Airport AirportOf(DisplayMaps.Map map)
        {
            string folder = map.Folder ?? "";
            if (folder == "")
            {
                return null;
            }

            if (!folderAirports.TryGetValue(folder, out Airspace2.Airport airport))
            {
                airport = Airspace2.GetAirport(folder.ToUpperInvariant());
                folderAirports[folder] = airport;
            }

            return airport;
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
            rangeField = mainAsd.GetType().GetField("range", AnyInstance);
            if (isMapVisible == null || setMapVisible == null || rangeField == null)
            {
                mainAsd = null;
            }

            return mainAsd;
        }

        private static bool IsVisible(Control display, DisplayMaps.Map map)
        {
            return (bool)isMapVisible.Invoke(display, new object[] { map });
        }

        private static void SetVisible(Control display, DisplayMaps.Map map, bool visible)
        {
            setMapVisible.Invoke(display, new object[] { map, visible });
        }
    }
}

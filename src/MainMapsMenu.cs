using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using vatsys;

namespace BetterGroundWindow
{
    /// <summary>
    /// Adds a "Ground Maps" submenu to the main window's Maps menu, listing the
    /// profile's ground maps (runways, taxiways, aprons, ...) that vatSys only
    /// offers in Ground windows. Items use MainForm's own map click handler, so
    /// toggling and check marks behave exactly like the other main ASD maps.
    /// </summary>
    internal static class MainMapsMenu
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private const string MenuName = "betterGroundMainGroundMaps";

        public static bool Attached { get; private set; }

        /// <summary>
        /// Builds the submenu once MainForm and its Maps menu exist. vatSys loads
        /// plugins before it fills the Maps menu, so this is retried from the scan
        /// timer until it succeeds.
        /// </summary>
        public static void TryAttach()
        {
            if (Attached)
            {
                return;
            }

            Form main = typeof(MMI).GetField("MainForm", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null) as Form;
            if (main == null || main.IsDisposed || !main.IsHandleCreated)
            {
                return;
            }

            Attach(main);
        }

        /// <summary>Adds the submenu to a form shaped like vatSys' MainForm.</summary>
        internal static void Attach(Form main)
        {
            Type type = main.GetType();
            ToolStripMenuItem maps = type.GetField("mapsToolStripMenuItem", PrivateInstance)?.GetValue(main) as ToolStripMenuItem;
            MethodInfo mapClick = type.GetMethod("mapItem_Click", PrivateInstance, null, new[] { typeof(object), typeof(EventArgs) }, null);
            MethodInfo keepOpen = type.GetMethod("MapsDropDown_Closing", PrivateInstance, null, new[] { typeof(object), typeof(ToolStripDropDownClosingEventArgs) }, null);
            if (maps == null || mapClick == null || maps.DropDownItems.Count == 0)
            {
                return; // Maps menu not built yet (or not recognised).
            }

            Attached = true;
            if (maps.DropDownItems.ContainsKey(MenuName))
            {
                return;
            }

            DisplayMaps.Map[] groundMaps = DisplayMaps.Maps.Where(m => m.Category == DisplayMaps.MapCategories.Ground).ToArray();
            if (groundMaps.Length == 0)
            {
                return;
            }

            EventHandler click = (EventHandler)Delegate.CreateDelegate(typeof(EventHandler), main, mapClick);
            ToolStripDropDownClosingEventHandler closing = keepOpen == null
                ? null
                : (ToolStripDropDownClosingEventHandler)Delegate.CreateDelegate(typeof(ToolStripDropDownClosingEventHandler), main, keepOpen);

            // The main ASD itself, to refresh check marks: maps can also be made
            // visible by MainAsdGroundMaps or reset by a position change.
            Control asd = type.GetField("ASD", PrivateInstance)?.GetValue(main) as Control;
            MethodInfo isVisible = asd?.GetType().GetMethod("IsMapVisible", BindingFlags.Instance | BindingFlags.Public, null, new[] { typeof(DisplayMaps.Map) }, null);
            EventHandler syncChecks = (s, e) =>
            {
                if (asd == null || isVisible == null || asd.IsDisposed)
                {
                    return;
                }

                foreach (ToolStripMenuItem item in ((ToolStripMenuItem)s).DropDownItems.OfType<ToolStripMenuItem>())
                {
                    if (item.Tag is DisplayMaps.Map map)
                    {
                        item.Checked = (bool)isVisible.Invoke(asd, new object[] { map });
                    }
                }
            };

            ToolStripMenuItem root = new ToolStripMenuItem("Ground Maps") { Name = MenuName };
            Prepare(root, maps, closing);
            root.DropDownOpening += syncChecks;

            // Same layout as the Ground window's Maps menu: one submenu per folder
            // (usually the airport). No separators: vatSys' map menu code casts
            // every child of a folder to ToolStripMenuItem.
            foreach (string folder in groundMaps.Select(m => m.Folder ?? "").Distinct().OrderBy(f => f))
            {
                ToolStripItemCollection target = root.DropDownItems;
                if (folder != "")
                {
                    ToolStripMenuItem sub = new ToolStripMenuItem(folder);
                    Prepare(sub, maps, closing);
                    sub.DropDownOpening += syncChecks;
                    root.DropDownItems.Add(sub);
                    target = sub.DropDownItems;
                }

                foreach (DisplayMaps.Map map in groundMaps.Where(m => (m.Folder ?? "") == folder).OrderBy(m => m.Name))
                {
                    ToolStripMenuItem item = new ToolStripMenuItem(map.Name) { Tag = map };
                    item.Click += click;
                    target.Add(item);
                }
            }

            maps.DropDownItems.Add(root);
        }

        /// <summary>Match the Maps menu's check-mark margin and keep it open on click.</summary>
        private static void Prepare(ToolStripMenuItem item, ToolStripMenuItem maps, ToolStripDropDownClosingEventHandler closing)
        {
            if (maps.DropDown is ToolStripDropDownMenu source && item.DropDown is ToolStripDropDownMenu target)
            {
                target.ShowImageMargin = source.ShowImageMargin;
                target.ShowCheckMargin = source.ShowCheckMargin;
            }

            if (closing != null)
            {
                item.DropDown.Closing += closing;
            }
        }
    }
}

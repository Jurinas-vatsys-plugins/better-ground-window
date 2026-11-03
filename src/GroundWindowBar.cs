using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using vatsys;

namespace BetterGroundWindow
{
    /// <summary>
    /// The controls added to the menu bar of one Ground window:
    /// <code>[ICAO][▼]  Position  Maps  Tools</code>
    /// The ICAO field is vatSys' own <see cref="TextField"/> and the ▼ history menu
    /// is drawn by the window's own menu renderer, so both match the rest of vatSys.
    /// Typing a code and pressing Enter loads that airport's ground position,
    /// exactly as if it had been chosen from the Position menu.
    /// </summary>
    internal sealed class GroundWindowBar
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private const string FieldName = "betterGroundIcaoField";

        private const int ErrorFlashMs = 1500;

        private readonly Form window;

        private readonly ToolStripMenuItem positionsMenu;

        private readonly Control groundDisplay;

        private readonly TextField field;

        private readonly ToolStripControlHost fieldHost;

        private readonly ToolStripMenuItem historyMenu;

        private readonly Timer errorTimer = new Timer { Interval = ErrorFlashMs };

        private readonly VatSysToolTip toolTip = new VatSysToolTip();

        private readonly ToolStripSeparator zoomSeparator = new ToolStripSeparator();

        private readonly ToolStripMenuItem unlimitedZoomItem = new ToolStripMenuItem("Unlimited Zoom");

        private readonly ToolStripMenuItem easyModeItem = new ToolStripMenuItem("Easy Mode");

        private readonly ToolStripMenuItem settingsItem = new ToolStripMenuItem("Better Ground Settings");

        private string currentIcao;

        private GroundWindowBar(Form window, MenuStrip menu, ToolStripMenuItem positionsMenu, ToolStripMenuItem toolsMenu, Control groundDisplay)
        {
            this.window = window;
            this.positionsMenu = positionsMenu;
            this.groundDisplay = groundDisplay;

            field = new TextField
            {
                Name = FieldName,
                Font = MMI.eurofont_winsml,
                CharacterCasing = CharacterCasing.Upper,
                MaxLength = 4,
                AutoSize = false,
                Size = new Size(58, 21),
                TextAlign = HorizontalAlignment.Center,
            };
            field.OnReturn += (s, e) => Open(field.Text);
            field.Escape += (s, e) =>
            {
                field.Text = "";
                ClearError();
                FocusDisplay();
            };
            field.KeyPress += Field_KeyPress;
            field.TextChanged += (s, e) => ClearError();

            fieldHost = new ToolStripControlHost(field)
            {
                Name = FieldName + "Host",
                AutoSize = false,
                Size = field.Size,
                Margin = new Padding(0, 1, 0, 1),
            };

            historyMenu = new ToolStripMenuItem
            {
                Name = "betterGroundHistoryMenu",
                DisplayStyle = ToolStripItemDisplayStyle.Image,
                Image = DrawDropDownArrow(Colours.GetColour(Colours.Identities.InteractiveText)),
                ImageScaling = ToolStripItemImageScaling.None,
                Margin = new Padding(0, 0, 4, 0),
            };
            historyMenu.DropDownOpening += (s, e) => RebuildHistoryMenu();
            RebuildHistoryMenu();

            errorTimer.Tick += (s, e) => ClearError();

            menu.Items.Insert(0, fieldHost);
            menu.Items.Insert(1, historyMenu);
            MatchCheckMargin(historyMenu);

            if (toolsMenu != null)
            {
                unlimitedZoomItem.Name = "betterGroundUnlimitedZoom";
                unlimitedZoomItem.Click += (s, e) => ZoomUnlock.SetEnabled(!ZoomUnlock.Enabled);
                ZoomUnlock.EnabledChanged += OnFeatureChanged;

                easyModeItem.Name = "betterGroundEasyMode";
                easyModeItem.Click += (s, e) => EasyMode.SetEnabled(!EasyMode.Enabled);
                EasyMode.Changed += OnFeatureChanged;

                settingsItem.Name = "betterGroundSettings";
                settingsItem.Click += (s, e) => SettingsWindow.ShowSettings(window);

                UpdateToolItems();
                toolsMenu.DropDownItems.Add(zoomSeparator);
                toolsMenu.DropDownItems.Add(unlimitedZoomItem);
                toolsMenu.DropDownItems.Add(easyModeItem);
                toolsMenu.DropDownItems.Add(settingsItem);
            }
        }

        /// <summary>
        /// Adds the ICAO field to a Ground window. Returns null if the window's
        /// internals don't look like the ones this plugin was written against.
        /// </summary>
        public static GroundWindowBar TryAttach(Form window)
        {
            Type type = window.GetType();
            MenuStrip menu = type.GetField("mainMenu", PrivateInstance)?.GetValue(window) as MenuStrip;
            ToolStripMenuItem positions = type.GetField("positionsToolStripMenuItem", PrivateInstance)?.GetValue(window) as ToolStripMenuItem;
            ToolStripMenuItem tools = type.GetField("toolsToolStripMenuItem", PrivateInstance)?.GetValue(window) as ToolStripMenuItem;
            Control display = type.GetField("asmgcsControlDX1", PrivateInstance)?.GetValue(window) as Control;

            if (menu == null || positions == null || menu.Items.ContainsKey(FieldName + "Host"))
            {
                return null;
            }

            return new GroundWindowBar(window, menu, positions, tools, display);
        }

        public void Detach()
        {
            ZoomUnlock.EnabledChanged -= OnFeatureChanged;
            EasyMode.Changed -= OnFeatureChanged;
            errorTimer.Dispose();
            toolTip.Dispose();
        }

        private void Field_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsLetterOrDigit(e.KeyChar) && !char.IsControl(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        private void RebuildHistoryMenu()
        {
            historyMenu.DropDownItems.Clear();

            // History items deliberately carry no Tag: vatSys' MenuRenderer flashes
            // any top-level menu whose children have a Tag.
            foreach (string icao in IcaoHistory.Entries)
            {
                ToolStripMenuItem item = new ToolStripMenuItem(icao)
                {
                    Checked = icao == currentIcao,
                };
                item.Click += (s, e) => Open(icao);
                historyMenu.DropDownItems.Add(item);
            }

            if (IcaoHistory.Entries.Count == 0)
            {
                historyMenu.DropDownItems.Add(new ToolStripMenuItem("No Recent Airports") { Enabled = false });
                return;
            }

            ToolStripMenuItem clear = new ToolStripMenuItem("Clear History");
            clear.Click += (s, e) => IcaoHistory.Clear();
            historyMenu.DropDownItems.Add(new ToolStripSeparator());
            historyMenu.DropDownItems.Add(clear);
        }

        /// <summary>
        /// Use the same check-mark margin as the window's own menus, which vatSys
        /// sets from the user's check mark style preference.
        /// </summary>
        private void MatchCheckMargin(ToolStripMenuItem item)
        {
            if (positionsMenu.DropDown is ToolStripDropDownMenu source && item.DropDown is ToolStripDropDownMenu target)
            {
                target.ShowImageMargin = source.ShowImageMargin;
                target.ShowCheckMargin = source.ShowCheckMargin;
            }
        }

        private void OnFeatureChanged(object sender, EventArgs e)
        {
            if (!window.IsDisposed)
            {
                UpdateToolItems();
            }
        }

        private void UpdateToolItems()
        {
            UpdateToggleItem(unlimitedZoomItem, "Unlimited Zoom", ZoomUnlock.Enabled, ZoomUnlock.Unavailable);
            UpdateToggleItem(easyModeItem, "Easy Mode", EasyMode.Enabled, EasyMode.Unavailable);

            // Redraw now so a hotkey toggle shows straight away.
            groundDisplay?.Invalidate();
        }

        private static void UpdateToggleItem(ToolStripMenuItem item, string caption, bool on, string unavailable)
        {
            item.Text = unavailable != null ? caption + " (Unavailable)" : caption;
            item.Enabled = unavailable == null;
            item.Checked = unavailable == null && on;
        }

        private void Open(string text)
        {
            string icao = IcaoHistory.Normalise(text);
            if (!IcaoHistory.IsValidIcao(icao))
            {
                ShowError("ENTER A 4 LETTER ICAO CODE");
                return;
            }

            LogicalPositions.Position position = FindPosition(icao);
            if (position == null)
            {
                ShowError("NO GROUND POSITION FOR " + icao);
                return;
            }

            LoadPosition(position);

            currentIcao = icao;
            field.Text = icao;
            IcaoHistory.Add(icao);
            FocusDisplay();
        }

        /// <summary>
        /// Picks the ASMGCS position for an airport. When an airport has several,
        /// prefer one named exactly after the ICAO code, then one starting with it.
        /// </summary>
        private static LogicalPositions.Position FindPosition(string icao)
        {
            return LogicalPositions.Positions
                .Where(p => p.Type == LogicalPositions.PositionTypes.ASMGCS
                            && string.Equals(p.ASMGCSAirport?.ICAOName, icao, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => string.Equals(p.Name, icao, StringComparison.OrdinalIgnoreCase) ? 0
                              : p.Name.StartsWith(icao, StringComparison.OrdinalIgnoreCase) ? 1 : 2)
                .ThenBy(p => p.Name)
                .FirstOrDefault();
        }

        private void LoadPosition(LogicalPositions.Position position)
        {
            // Clicking the existing menu item keeps vatSys' own bookkeeping
            // (check marks, visible maps, window title) consistent.
            ToolStripMenuItem item = FindMenuItem(positionsMenu.DropDownItems, position);
            if (item != null)
            {
                item.PerformClick();
                return;
            }

            MethodInfo handler = window.GetType().GetMethod("positionItem_Click", PrivateInstance);
            handler?.Invoke(window, new object[] { new ToolStripMenuItem { Tag = position }, EventArgs.Empty });
        }

        private static ToolStripMenuItem FindMenuItem(ToolStripItemCollection items, LogicalPositions.Position position)
        {
            foreach (ToolStripMenuItem item in items.OfType<ToolStripMenuItem>())
            {
                if (item.Tag == position)
                {
                    return item;
                }

                ToolStripMenuItem child = FindMenuItem(item.DropDownItems, position);
                if (child != null)
                {
                    return child;
                }
            }

            return null;
        }

        private void FocusDisplay()
        {
            if (groundDisplay != null && !groundDisplay.IsDisposed && groundDisplay.CanFocus)
            {
                groundDisplay.Focus();
            }
        }

        private void ShowError(string message)
        {
            // Same warning style vatSys uses for invalid entries in its own fields.
            field.ErrorMode(true);
            field.SelectAll();
            field.Focus();
            toolTip.Show(message, field, 0, field.Height + 2, ErrorFlashMs);
            errorTimer.Stop();
            errorTimer.Start();
        }

        private void ClearError()
        {
            if (!errorTimer.Enabled)
            {
                return;
            }

            errorTimer.Stop();
            toolTip.Hide(field);
            field.ErrorMode(false);
        }

        private static Bitmap DrawDropDownArrow(Color colour)
        {
            Bitmap bitmap = new Bitmap(9, 9);
            using (Graphics g = Graphics.FromImage(bitmap))
            using (Brush brush = new SolidBrush(colour))
            {
                g.SmoothingMode = SmoothingMode.None;
                g.FillPolygon(brush, new[] { new Point(0, 2), new Point(9, 2), new Point(4, 7) });
            }

            return bitmap;
        }
    }
}

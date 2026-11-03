using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using vatsys;

namespace BetterGroundWindow
{
    /// <summary>
    /// The ICAO box added to the menu bar of one Ground window. Typing a four-letter
    /// ICAO code (or picking one from the ▼ history list) loads that airport's
    /// ground position, exactly as if it had been chosen from the Position menu.
    /// </summary>
    internal sealed class GroundWindowBar
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

        private const int ErrorFlashMs = 1200;

        private readonly Form window;

        private readonly ToolStripMenuItem positionsMenu;

        private readonly Control groundDisplay;

        private readonly ToolStripComboBox box;

        private readonly Timer errorTimer = new Timer { Interval = ErrorFlashMs };

        private readonly Color normalBack;

        private readonly Color normalFore;

        private bool updatingBox;

        private GroundWindowBar(Form window, MenuStrip menu, ToolStripMenuItem positionsMenu, Control groundDisplay)
        {
            this.window = window;
            this.positionsMenu = positionsMenu;
            this.groundDisplay = groundDisplay;

            normalBack = Colours.GetColour(Colours.Identities.WindowBackground);
            normalFore = Colours.GetColour(Colours.Identities.InteractiveText);

            box = new ToolStripComboBox
            {
                Name = "betterGroundIcaoBox",
                DropDownStyle = ComboBoxStyle.DropDown,
                FlatStyle = FlatStyle.Flat,
                MaxLength = 4,
                AutoSize = false,
                Width = 78,
                Font = MMI.eurofont_winsml,
                BackColor = normalBack,
                ForeColor = normalFore,
                ToolTipText = "Type an ICAO code to open its ground position. ▼ shows recent airports.",
                Margin = new Padding(0, 0, 6, 0),
            };
            box.ComboBox.DropDownWidth = 110;

            box.KeyDown += Box_KeyDown;
            box.KeyPress += Box_KeyPress;
            box.TextUpdate += Box_TextUpdate;
            box.SelectedIndexChanged += Box_SelectedIndexChanged;
            errorTimer.Tick += (s, e) => ClearError();
            IcaoHistory.Changed += IcaoHistory_Changed;

            RefreshItems();
            menu.Items.Insert(0, box);
        }

        /// <summary>
        /// Adds the ICAO box to a Ground window. Returns null if the window's
        /// internals don't look like the ones this plugin was written against.
        /// </summary>
        public static GroundWindowBar TryAttach(Form window)
        {
            Type type = window.GetType();
            MenuStrip menu = type.GetField("mainMenu", PrivateInstance)?.GetValue(window) as MenuStrip;
            ToolStripMenuItem positions = type.GetField("positionsToolStripMenuItem", PrivateInstance)?.GetValue(window) as ToolStripMenuItem;
            Control display = type.GetField("asmgcsControlDX1", PrivateInstance)?.GetValue(window) as Control;

            if (menu == null || positions == null || menu.Items.ContainsKey("betterGroundIcaoBox"))
            {
                return null;
            }

            return new GroundWindowBar(window, menu, positions, display);
        }

        public void Detach()
        {
            IcaoHistory.Changed -= IcaoHistory_Changed;
            errorTimer.Dispose();
        }

        private void Box_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (char.IsLetter(e.KeyChar))
            {
                e.KeyChar = char.ToUpperInvariant(e.KeyChar);
            }
            else if (!char.IsDigit(e.KeyChar) && !char.IsControl(e.KeyChar))
            {
                e.Handled = true;
            }
        }

        private void Box_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                Open(box.Text);
            }
            else if (e.KeyCode == Keys.Escape)
            {
                e.SuppressKeyPress = true;
                box.Text = "";
                FocusDisplay();
            }
        }

        private void Box_TextUpdate(object sender, EventArgs e)
        {
            ClearError();

            // Auto-open as soon as a complete ICAO code has been typed.
            if (IcaoHistory.IsValidIcao(IcaoHistory.Normalise(box.Text)))
            {
                Open(box.Text);
            }
        }

        private void Box_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (updatingBox || box.SelectedItem == null)
            {
                return;
            }

            object picked = box.SelectedItem;

            // Defer: changing the item list or focus while the combo is still
            // processing its own selection change is not safe.
            window.BeginInvoke((MethodInvoker)delegate
            {
                if (picked is ClearHistoryEntry)
                {
                    box.Text = "";
                    IcaoHistory.Clear();
                }
                else
                {
                    Open(picked.ToString());
                }
            });
        }

        private void IcaoHistory_Changed(object sender, EventArgs e)
        {
            if (window.IsDisposed)
            {
                return;
            }

            RefreshItems();
        }

        private void RefreshItems()
        {
            updatingBox = true;
            try
            {
                string text = box.Text;
                box.Items.Clear();
                foreach (string icao in IcaoHistory.Entries)
                {
                    box.Items.Add(icao);
                }

                if (IcaoHistory.Entries.Count > 0)
                {
                    box.Items.Add(ClearHistoryEntry.Instance);
                }

                box.Text = text;
            }
            finally
            {
                updatingBox = false;
            }
        }

        private void Open(string text)
        {
            string icao = IcaoHistory.Normalise(text);
            if (!IcaoHistory.IsValidIcao(icao))
            {
                ShowError("Enter a 4-character ICAO code.");
                return;
            }

            LogicalPositions.Position position = FindPosition(icao);
            if (position == null)
            {
                ShowError("No ground position for " + icao + " in this profile.");
                return;
            }

            LoadPosition(position);

            updatingBox = true;
            try
            {
                box.Text = icao;
            }
            finally
            {
                updatingBox = false;
            }

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
            box.BackColor = Colours.GetColour(Colours.Identities.Warning);
            box.ToolTipText = message;
            box.ComboBox.SelectAll();
            errorTimer.Stop();
            errorTimer.Start();
        }

        private void ClearError()
        {
            errorTimer.Stop();
            box.BackColor = normalBack;
            box.ToolTipText = "Type an ICAO code to open its ground position. ▼ shows recent airports.";
        }

        private sealed class ClearHistoryEntry
        {
            public static readonly ClearHistoryEntry Instance = new ClearHistoryEntry();

            public override string ToString() => "Clear history";
        }
    }
}

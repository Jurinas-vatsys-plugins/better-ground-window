using System;
using System.Drawing;
using System.Windows.Forms;
using vatsys;

namespace BetterGroundWindow
{
    /// <summary>
    /// Plugin settings, built from vatSys' own window frame (BaseForm), labels
    /// and buttons so it looks like any other vatSys window. Changes apply
    /// immediately.
    /// </summary>
    internal sealed class SettingsWindow : BaseForm
    {
        private const int RowHeight = 30;

        private const int LabelWidth = 200;

        private const int ButtonWidth = 120;

        private static SettingsWindow instance;

        private readonly GenericButton easyModeButton;

        private readonly GenericButton hotkeyButton;

        private readonly GenericButton hotkeyDefaultButton;

        private readonly GenericButton hotkeyClearButton;

        private readonly GenericButton zoomButton;

        private bool capturing;

        private SettingsWindow()
        {
            Name = "BetterGroundWindowSettings";
            Text = "Better Ground Window";
            HasMinimizeButton = false;
            HasMaximizeButton = false;
            Resizeable = false;
            MiddleClickClose = true;
            HideOnClose = true;
            KeyPreview = true;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(12 + LabelWidth + ButtonWidth + 12, 0);

            int y = 10;
            easyModeButton = AddRow("Easy Mode", ref y, (s, e) => EasyMode.SetEnabled(!EasyMode.Enabled));
            AddNote("Callsign for aircraft without Mode C", ref y);
            hotkeyButton = AddRow("Easy Mode Key", ref y, (s, e) => BeginCapture());
            (hotkeyDefaultButton, hotkeyClearButton) = AddHalfButtons("Default", "Clear", ref y);
            hotkeyDefaultButton.Click += (s, e) => { EndCapture(); EasyMode.SetHotkey(EasyMode.DefaultHotkey); };
            hotkeyClearButton.Click += (s, e) => { EndCapture(); EasyMode.SetHotkey(Keys.None); };
            AddNote("Click, then press keys. Del clears.", ref y);
            zoomButton = AddRow("Unlimited Zoom", ref y, (s, e) => ZoomUnlock.SetEnabled(!ZoomUnlock.Enabled));
            AddNote("Ground range 0.01 - 9999 NM", ref y);

            y += 8;
            GenericButton close = new GenericButton
            {
                Text = "Close",
                Size = new Size(ButtonWidth, RowHeight),
                Location = new Point(ClientSize.Width - 12 - ButtonWidth, y),
            };
            close.Click += (s, e) => Close();
            Controls.Add(close);
            ClientSize = new Size(ClientSize.Width, y + RowHeight + 10);

            EasyMode.Changed += OnSettingsChanged;
            ZoomUnlock.EnabledChanged += OnSettingsChanged;
            RefreshButtons();
        }

        public static void ShowSettings(IWin32Window owner)
        {
            if (instance == null || instance.IsDisposed)
            {
                instance = new SettingsWindow();
            }

            instance.Show(owner);
            instance.Activate();
        }

        private GenericButton AddRow(string caption, ref int y, EventHandler click)
        {
            Controls.Add(new TextLabel
            {
                Text = caption,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(12, y),
                Size = new Size(LabelWidth, RowHeight),
            });

            GenericButton button = new GenericButton
            {
                Location = new Point(12 + LabelWidth, y),
                Size = new Size(ButtonWidth, RowHeight),
            };
            button.Click += click;
            Controls.Add(button);

            y += RowHeight + 2;
            return button;
        }

        /// <summary>Two buttons splitting the button column, under the previous row.</summary>
        private (GenericButton, GenericButton) AddHalfButtons(string left, string right, ref int y)
        {
            int width = (ButtonWidth - 2) / 2;
            GenericButton a = new GenericButton
            {
                Text = left,
                Location = new Point(12 + LabelWidth, y),
                Size = new Size(width, RowHeight),
            };
            GenericButton b = new GenericButton
            {
                Text = right,
                Location = new Point(12 + LabelWidth + ButtonWidth - width, y),
                Size = new Size(width, RowHeight),
            };
            Controls.Add(a);
            Controls.Add(b);

            y += RowHeight + 2;
            return (a, b);
        }

        private void AddNote(string text, ref int y)
        {
            Controls.Add(new TextLabel
            {
                Text = text,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(12, y),
                Size = new Size(LabelWidth + ButtonWidth, 20),
                ForeColor = Colours.GetColour(Colours.Identities.NonInteractiveText),
            });
            y += 26;
        }

        private void OnSettingsChanged(object sender, EventArgs e)
        {
            if (!IsDisposed)
            {
                RefreshButtons();
            }
        }

        private void RefreshButtons()
        {
            SetToggle(easyModeButton, EasyMode.Enabled, EasyMode.Unavailable);
            SetToggle(zoomButton, ZoomUnlock.Enabled, ZoomUnlock.Unavailable);

            hotkeyButton.Enabled = EasyMode.Unavailable == null;
            hotkeyButton.Text = capturing ? "Press Keys" : EasyMode.Describe(EasyMode.Hotkey);
            hotkeyButton.BackColor = Colours.GetColour(capturing ? Colours.Identities.WindowWarning : Colours.Identities.WindowBackground);
            hotkeyButton.ForeColor = Colours.GetColour(Colours.Identities.InteractiveText);
            hotkeyButton.Invalidate();
            hotkeyDefaultButton.Enabled = hotkeyClearButton.Enabled = EasyMode.Unavailable == null;
        }

        /// <summary>Same colours vatSys uses for a checked menu item.</summary>
        private static void SetToggle(GenericButton button, bool on, string unavailable)
        {
            button.Enabled = unavailable == null;
            button.Text = unavailable != null ? "Unavailable" : on ? "On" : "Off";
            button.BackColor = Colours.GetColour(on ? Colours.Identities.WindowButtonSelected : Colours.Identities.WindowBackground);
            button.ForeColor = Colours.GetColour(on ? Colours.Identities.WindowBackground : Colours.Identities.InteractiveText);
            button.Invalidate();
        }

        private void BeginCapture()
        {
            capturing = true;
            EasyMode.CapturingHotkey = true;
            RefreshButtons();
        }

        private void EndCapture()
        {
            capturing = false;
            EasyMode.CapturingHotkey = false;
            RefreshButtons();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (!capturing)
            {
                return base.ProcessCmdKey(ref msg, keyData);
            }

            Keys code = keyData & Keys.KeyCode;
            switch (code)
            {
                case Keys.ControlKey:
                case Keys.ShiftKey:
                case Keys.Menu:
                case Keys.LWin:
                case Keys.RWin:
                    return true; // wait for the actual key
                case Keys.Escape:
                    EndCapture();
                    return true;
                case Keys.Delete:
                case Keys.Back:
                    EasyMode.SetHotkey(Keys.None);
                    EndCapture();
                    return true;
                default:
                    EasyMode.SetHotkey(keyData);
                    EndCapture();
                    return true;
            }
        }

        protected override void OnDeactivate(EventArgs e)
        {
            if (capturing)
            {
                EndCapture();
            }

            base.OnDeactivate(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                EasyMode.Changed -= OnSettingsChanged;
                ZoomUnlock.EnabledChanged -= OnSettingsChanged;
                EasyMode.CapturingHotkey = false;
            }

            base.Dispose(disposing);
        }
    }
}

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

        private readonly GenericButton scaleButton;

        private readonly TextField rangeField;

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
            easyModeButton = AddRow("Top-Down Mode", ref y, (s, e) => EasyMode.SetEnabled(!EasyMode.Enabled));
            AddNote("Callsign for aircraft without Mode C", ref y);
            rangeField = AddFieldRow("Top-Down Range NM", ref y);
            rangeField.OnReturn += (s, e) => ApplyRange(leaving: false);
            rangeField.LostFocus += (s, e) => ApplyRange(leaving: true);
            rangeField.Escape += (s, e) => { rangeField.ErrorMode(false); rangeField.Text = ""; ActiveControl = null; RefreshButtons(); };
            AddNote("Main ASD ground view + airports", ref y);
            hotkeyButton = AddRow("Top-Down Key", ref y, (s, e) => BeginCapture());
            (hotkeyDefaultButton, hotkeyClearButton) = AddHalfButtons("Reset", "Clear", ref y);
            hotkeyDefaultButton.Click += (s, e) => { EndCapture(); EasyMode.SetHotkey(EasyMode.DefaultHotkey); };
            hotkeyClearButton.Click += (s, e) => { EndCapture(); EasyMode.SetHotkey(Keys.None); };
            AddNote("Click, then press keys. Del clears.", ref y);
            zoomButton = AddRow("Unlimited Zoom", ref y, (s, e) => ZoomUnlock.SetEnabled(!ZoomUnlock.Enabled));
            AddNote("Ground + main ASD: 0.01 - 9999 NM", ref y);
            scaleButton = AddRow("Scale Marker", ref y, (s, e) => SetScaleMarker(!MMI.ScaleMarker));
            AddNote("Range scale on radar screens", ref y);

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

        /// <summary>A caption with a vatSys number field in the button column.</summary>
        private TextField AddFieldRow(string caption, ref int y)
        {
            Controls.Add(new TextLabel
            {
                Text = caption,
                TextAlign = ContentAlignment.MiddleLeft,
                Location = new Point(12, y),
                Size = new Size(LabelWidth, RowHeight),
            });

            TextField field = new TextField
            {
                NumericCharOnly = true,
                MaxLength = 3,
                Font = MMI.eurofont_winsml,
                TextAlign = HorizontalAlignment.Center,
                AutoSize = false,
                Location = new Point(12 + LabelWidth, y + 3),
                Size = new Size(ButtonWidth, RowHeight - 6),
            };
            Controls.Add(field);

            y += RowHeight + 2;
            return field;
        }

        /// <summary>
        /// Saves the typed range (1-500 NM). An invalid entry shows vatSys' warning
        /// colour while typing, and reverts to the saved value when leaving the field.
        /// </summary>
        private void ApplyRange(bool leaving)
        {
            bool valid = double.TryParse(rangeField.Text, out double nm)
                         && nm >= PluginSettings.MinTopDownRangeNm && nm <= PluginSettings.MaxTopDownRangeNm;
            if (valid && nm != PluginSettings.TopDownRangeNm)
            {
                PluginSettings.TopDownRangeNm = nm;
            }

            rangeField.ErrorMode(!valid && !leaving);
            if (valid || leaving)
            {
                rangeField.Text = PluginSettings.TopDownRangeNm.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
            }

            RefreshButtons();
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
            SetToggle(scaleButton, MMI.ScaleMarker, null);

            if (!rangeField.Focused)
            {
                rangeField.Text = PluginSettings.TopDownRangeNm.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
            }

            rangeField.Enabled = EasyMode.TopDownUnavailable == null;

            hotkeyButton.Enabled = EasyMode.Unavailable == null;
            hotkeyButton.Text = capturing ? "Press Keys" : EasyMode.Describe(EasyMode.Hotkey);
            hotkeyButton.BackColor = Colours.GetColour(capturing ? Colours.Identities.WindowWarning : Colours.Identities.WindowBackground);
            hotkeyButton.ForeColor = Colours.GetColour(Colours.Identities.InteractiveText);
            hotkeyButton.Invalidate();
            hotkeyDefaultButton.Enabled = hotkeyClearButton.Enabled = EasyMode.Unavailable == null;
        }

        /// <summary>
        /// The scale marker is vatSys' own setting (Display Settings > Scale
        /// Marker), which vatSys saves on exit. This only flips it.
        /// </summary>
        private void SetScaleMarker(bool on)
        {
            MMI.ScaleMarker = on;
            foreach (Form form in Application.OpenForms)
            {
                if (!form.IsDisposed)
                {
                    form.Invalidate(true);
                }
            }

            RefreshButtons();
        }

        protected override void OnActivated(EventArgs e)
        {
            // Pick up changes made elsewhere, e.g. in vatSys' Display Settings.
            RefreshButtons();
            base.OnActivated(e);
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

using System.Drawing;
using System.Windows.Forms;
using vatsys;

namespace BetterGroundWindow
{
    /// <summary>
    /// A tooltip drawn with the vatSys palette, font and raised 3D border instead
    /// of the yellow Windows default.
    /// </summary>
    internal sealed class VatSysToolTip : ToolTip
    {
        private const int Padding = 4;

        public VatSysToolTip()
        {
            OwnerDraw = true;
            UseAnimation = false;
            UseFading = false;
            Popup += OnPopup;
            Draw += OnDraw;
        }

        private static void OnPopup(object sender, PopupEventArgs e)
        {
            string text = ((ToolTip)sender).GetToolTip(e.AssociatedControl);
            Size size = TextRenderer.MeasureText(string.IsNullOrEmpty(text) ? " " : text, MMI.eurofont_winsml);
            e.ToolTipSize = new Size(size.Width + Padding * 2, size.Height + Padding * 2);
        }

        private static void OnDraw(object sender, DrawToolTipEventArgs e)
        {
            Rectangle bounds = e.Bounds;
            using (Brush back = new SolidBrush(Colours.GetColour(Colours.Identities.WindowBackground)))
            {
                e.Graphics.FillRectangle(back, bounds);
            }

            ControlPaint.DrawBorder3D(e.Graphics, bounds, Border3DStyle.Raised);
            TextRenderer.DrawText(e.Graphics, e.ToolTipText, MMI.eurofont_winsml, bounds,
                Colours.GetColour(Colours.Identities.InteractiveText),
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }
}

using System.Reflection;
using System.Windows.Forms;
using vatsys;

namespace BetterGroundWindow
{
    /// <summary>vatSys' main window and its ASD, looked up once by reflection (MMI.MainForm is internal).</summary>
    internal static class VatSysMain
    {
        private const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly FieldInfo MainFormField = typeof(MMI).GetField("MainForm", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        private static FieldInfo asdField;

        /// <summary>The main window, or null before vatSys has created it.</summary>
        public static Form Form => MainFormField?.GetValue(null) as Form;

        /// <summary>The main ASD control, or null.</summary>
        public static Control Asd
        {
            get
            {
                Form main = Form;
                if (main == null)
                {
                    return null;
                }

                if (asdField == null)
                {
                    asdField = main.GetType().GetField("ASD", AnyInstance);
                }

                return asdField?.GetValue(main) as Control;
            }
        }
    }
}

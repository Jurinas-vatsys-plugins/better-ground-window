using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace BetterGroundWindow
{
    /// <summary>
    /// Closes drop-down menus that WinForms leaves open. vatSys opens some
    /// submenus itself (ShowDropDown on the Ground window's Positions / Maps
    /// menus) and keeps the Maps menus open on click, so after switching to
    /// another vatSys window those drop-downs can stay on screen and keep the
    /// menu bar in menu mode.
    ///
    /// <see cref="CloseAll"/> is the manual "Close All Menus" command. While
    /// <see cref="PluginSettings.AutoCloseMenus"/> is on, activating a vatSys
    /// window also closes the drop-downs that belong to other windows.
    /// </summary>
    internal static class DropDownCloser
    {
        private static readonly HashSet<Form> watched = new HashSet<Form>();

        private static readonly List<ToolStripDropDown> found = new List<ToolStripDropDown>();

        private static readonly EnumThreadWindowsProc collect = Collect;

        private static readonly EventHandler onActivated = OnFormActivated;

        private static readonly FormClosedEventHandler onClosed = OnFormClosed;

        private static MethodInfo exitMenuMode;

        private static bool exitMenuModeLooked;

        private delegate bool EnumThreadWindowsProc(IntPtr hwnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumThreadWindows(uint threadId, EnumThreadWindowsProc callback, IntPtr lParam);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        /// <summary>Watches the activation of every open vatSys window. Called from the scan timer.</summary>
        public static void WatchOpenForms()
        {
            FormCollection forms = Application.OpenForms;
            for (int i = 0; i < forms.Count; i++)
            {
                Form form = forms[i];
                if (!form.IsDisposed && watched.Add(form))
                {
                    form.Activated += onActivated;
                    form.FormClosed += onClosed;
                }
            }
        }

        /// <summary>Closes every open drop-down menu. Returns how many were closed.</summary>
        public static int CloseAll()
        {
            return Close(null);
        }

        private static void OnFormActivated(object sender, EventArgs e)
        {
            if (PluginSettings.AutoCloseMenus)
            {
                Close((Form)sender);
            }
        }

        private static void OnFormClosed(object sender, FormClosedEventArgs e)
        {
            Form form = (Form)sender;
            form.Activated -= onActivated;
            form.FormClosed -= onClosed;
            watched.Remove(form);
        }

        /// <summary>Closes the open drop-downs that don't belong to <paramref name="keep"/> (all if null).</summary>
        private static int Close(Form keep)
        {
            found.Clear();
            try
            {
                EnumThreadWindows(GetCurrentThreadId(), collect, IntPtr.Zero);
            }
            catch (Exception)
            {
                return 0;
            }

            int closed = 0;
            foreach (ToolStripDropDown dropDown in found)
            {
                if (dropDown.IsDisposed || !dropDown.Visible || (keep != null && RootForm(dropDown) == keep))
                {
                    continue;
                }

                // Closing a parent also closes its submenus, which are then
                // skipped as no longer visible. CloseCalled is not cancelled by
                // vatSys' keep-open handlers (they only cancel ItemClicked).
                dropDown.Close(ToolStripDropDownCloseReason.CloseCalled);
                closed++;
            }

            found.Clear();
            if (keep == null || closed > 0)
            {
                ExitMenuMode();
            }

            return closed;
        }

        private static bool Collect(IntPtr hwnd, IntPtr lParam)
        {
            if (Control.FromHandle(hwnd) is ToolStripDropDown dropDown && dropDown.Visible)
            {
                found.Add(dropDown);
            }

            return true;
        }

        /// <summary>The window whose menu bar (or control) a drop-down was opened from.</summary>
        private static Form RootForm(ToolStripDropDown dropDown)
        {
            ToolStrip strip = dropDown;
            for (int depth = 0; depth < 32 && strip is ToolStripDropDown dd; depth++)
            {
                if (dd.OwnerItem?.Owner != null)
                {
                    strip = dd.OwnerItem.Owner;
                }
                else
                {
                    return (dd as ContextMenuStrip)?.SourceControl?.FindForm();
                }
            }

            return strip?.FindForm();
        }

        /// <summary>
        /// Leaves WinForms' menu mode (menu bar item still highlighted, keys going
        /// to the menu). ToolStripManager.ModalMenuFilter.ExitMenuMode is internal.
        /// </summary>
        private static void ExitMenuMode()
        {
            if (!exitMenuModeLooked)
            {
                exitMenuModeLooked = true;
                exitMenuMode = typeof(ToolStripManager)
                    .GetNestedType("ModalMenuFilter", BindingFlags.NonPublic)
                    ?.GetMethod("ExitMenuMode", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            }

            try
            {
                exitMenuMode?.Invoke(null, null);
            }
            catch (Exception)
            {
                // Best effort: the drop-downs themselves are already closed.
            }
        }
    }
}

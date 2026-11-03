using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Windows.Forms;
using HarmonyLib;
using vatsys;

namespace BetterGroundWindow
{
    /// <summary>
    /// Harmony transpiler for ASDControlDX.SetZoom. The original clamps look like:
    /// <code>
    /// nRange = Math.Min(Math.Max(MMI.MinimumASDRange(asdType), nRange), MMI.MaximumASDRange(asdType));
    /// ...
    /// val = Math.Min(Math.Max(0.5, val), 16000.0);
    /// </code>
    /// After each of the four limit values is pushed, we insert a call that gets
    /// the display and may widen the value. Original behaviour is untouched
    /// unless Unlimited Zoom is on and the display is a Ground display.
    /// </summary>
    internal static class ZoomPatch
    {
        private const string HarmonyId = "BetterGroundWindow.UnlimitedZoom";

        private const double OriginalMinZoom = 0.5;

        private const double OriginalMaxZoom = 16000.0;

        /// <summary>Zoom factor bounds when unlocked; wide enough for the unlocked range limits.</summary>
        private const double UnlockedMinZoom = 1e-4;

        private const double UnlockedMaxZoom = 1e7;

        private const int GroundAsdType = 2; // ASDControlDX.ASDTypes.Ground

        private static readonly Type AsdControlType = typeof(MMI).Assembly.GetType("vatsys.ASDControlDX", throwOnError: true);

        private static readonly FieldInfo AsdTypeField = AccessTools.Field(AsdControlType, "asdType");

        private static int patchedSites;

        public static void Install()
        {
            if (AsdTypeField == null)
            {
                throw new MissingFieldException("ASDControlDX.asdType not found.");
            }

            MethodInfo target = AccessTools.Method(AsdControlType, "SetZoom", new[] { typeof(double), typeof(bool), typeof(bool), typeof(bool) })
                                ?? throw new MissingMethodException("ASDControlDX.SetZoom not found.");

            patchedSites = 0;
            new Harmony(HarmonyId).Patch(target, transpiler: new HarmonyMethod(typeof(ZoomPatch), nameof(Transpiler)));

            if (patchedSites != 4)
            {
                new Harmony(HarmonyId).Unpatch(target, HarmonyPatchType.Transpiler, HarmonyId);
                throw new InvalidOperationException("This vatSys version's zoom code is not recognised (" + patchedSites + "/4 limits found).");
            }

            PatchWheelZoomPlugins();
        }

        /// <summary>Type names of wheel-zoom plugins whose ZoomWithWheel is handled.</summary>
        public static readonly List<string> WheelPluginsPatched = new List<string>();

        private const double WheelZoomInFactor = 0.8;

        private const double WheelZoomOutFactor = 1.25;

        private static readonly FieldInfo RangeField = AccessTools.Field(AsdControlType, "range");

        private static readonly MethodInfo SetZoomMethod = AccessTools.Method(AsdControlType, "SetZoom", new[] { typeof(double), typeof(bool), typeof(bool), typeof(bool) });

        /// <summary>
        /// Wheel-zoom plugins (Persistent Pan / Drag And Zoom) step the range in
        /// whole NM using the int GetRange(), so they stall around 2 NM:
        /// round(2 * 0.8) == 2. While Unlimited Zoom is on, their ZoomWithWheel is
        /// replaced for Ground displays with fractional steps on the real range.
        /// vatSys loads every plugin assembly before constructing any plugin, so
        /// they can all be found here.
        /// </summary>
        private static void PatchWheelZoomPlugins()
        {
            Harmony harmony = new Harmony(HarmonyId);
            HarmonyMethod prefix = new HarmonyMethod(typeof(ZoomPatch), nameof(WheelZoomPrefix));

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (assembly == typeof(ZoomPatch).Assembly || assembly.IsDynamic)
                {
                    continue;
                }

                Type[] types;
                try
                {
                    types = assembly.GetTypes();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    types = ex.Types;
                }
                catch (Exception)
                {
                    continue;
                }

                foreach (Type type in types)
                {
                    MethodInfo wheel = type?.GetMethod("ZoomWithWheel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null, new[] { typeof(Control), typeof(int) }, null);
                    if (wheel == null || wheel.ReturnType != typeof(void))
                    {
                        continue;
                    }

                    try
                    {
                        harmony.Patch(wheel, prefix: prefix);
                        WheelPluginsPatched.Add(type.FullName);
                    }
                    catch (Exception)
                    {
                        // Leave that plugin's wheel zoom as it is.
                    }
                }
            }
        }

        private static bool WheelZoomPrefix(Control __0, int __1)
        {
            Control display = __0;
            if (display == null || __1 == 0 || !AsdControlType.IsInstanceOfType(display) || !Unlocked(display)
                || !(RangeField?.GetValue(display) is double range))
            {
                return true;
            }

            double next = range * (__1 > 0 ? WheelZoomInFactor : WheelZoomOutFactor);
            SetZoomMethod.Invoke(display, new object[] { next, true, true, false });
            display.Invalidate();
            return false;
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo minRange = AccessTools.Method(typeof(MMI), "MinimumASDRange");
            MethodInfo maxRange = AccessTools.Method(typeof(MMI), "MaximumASDRange");

            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;

                string adjust = null;
                if (minRange != null && instruction.Calls(minRange))
                {
                    adjust = nameof(AdjustMinRange);
                }
                else if (maxRange != null && instruction.Calls(maxRange))
                {
                    adjust = nameof(AdjustMaxRange);
                }
                else if (instruction.opcode == OpCodes.Ldc_R8 && instruction.operand is double d)
                {
                    if (d == OriginalMinZoom)
                    {
                        adjust = nameof(AdjustMinZoom);
                    }
                    else if (d == OriginalMaxZoom)
                    {
                        adjust = nameof(AdjustMaxZoom);
                    }
                }

                if (adjust != null)
                {
                    patchedSites++;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ZoomPatch), adjust));
                }
            }
        }

        private static bool Unlocked(object display)
        {
            return ZoomUnlock.Enabled && Convert.ToInt32(AsdTypeField.GetValue(display)) == GroundAsdType;
        }

        public static float AdjustMinRange(float original, object display) =>
            Unlocked(display) ? Math.Min(original, ZoomUnlock.UnlockedMinRange) : original;

        public static float AdjustMaxRange(float original, object display) =>
            Unlocked(display) ? Math.Max(original, ZoomUnlock.UnlockedMaxRange) : original;

        public static double AdjustMinZoom(double original, object display) =>
            Unlocked(display) ? Math.Min(original, UnlockedMinZoom) : original;

        public static double AdjustMaxZoom(double original, object display) =>
            Unlocked(display) ? Math.Max(original, UnlockedMaxZoom) : original;
    }
}

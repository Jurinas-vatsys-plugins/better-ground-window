using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using vatsys;

namespace BetterGroundWindow
{
    /// <summary>
    /// Harmony patches behind <see cref="EasyMode"/>. All of them act on drawing
    /// only:
    /// <list type="number">
    /// <item>ASDControlDX.PaintGroundTracks: the branch for an on-ground track with
    /// no flight plan and no Mode C/S/ADS-B stores <c>track.HasASMGCSLabel = false</c>.
    /// That value is replaced with <see cref="IsForced"/>, so such tracks get the
    /// GroundLimited label. HasASMGCSLabel is only read by the painter and by
    /// label dragging.</item>
    /// <item>ASDControlDX.PaintLabel: the LABEL_ITEM_ACID text is empty for those
    /// tracks. Just before vatSys tests it with string.IsNullOrEmpty, the network
    /// callsign is substituted.</item>
    /// <item>ASDControlDX.PaintLabel prefix/postfix: clickspots added while painting
    /// a forced label are removed again, so the label cannot open menus.</item>
    /// </list>
    /// </summary>
    internal static class EasyModePatch
    {
        private const string HarmonyId = "BetterGroundWindow.EasyMode";

        private const RDP.RadarTypes LabelledRadarTypes =
            RDP.RadarTypes.SSR_ModeC | RDP.RadarTypes.SSR_ModeS | RDP.RadarTypes.ADSB | RDP.RadarTypes.SMR_ADSB;

        private static readonly Type AsdControlType = typeof(MMI).Assembly.GetType("vatsys.ASDControlDX", throwOnError: true);

        private static readonly FieldInfo HasLabelField = AccessTools.Field(typeof(Track), nameof(Track.HasASMGCSLabel));

        private static int labelSites;

        private static int acidSites;

        public static void Install()
        {
            MethodInfo paintGroundTracks = AccessTools.Method(AsdControlType, "PaintGroundTracks")
                                           ?? throw new MissingMethodException("ASDControlDX.PaintGroundTracks not found.");
            MethodInfo paintLabel = AccessTools.Method(AsdControlType, "PaintLabel")
                                    ?? throw new MissingMethodException("ASDControlDX.PaintLabel not found.");

            string[] needed = { "isASMGCS", "tl", "clickspots" };
            string[] names = paintLabel.GetParameters().Select(p => p.Name).ToArray();
            if (needed.Any(n => !names.Contains(n)))
            {
                throw new InvalidOperationException("ASDControlDX.PaintLabel has an unexpected signature.");
            }

            Harmony harmony = new Harmony(HarmonyId);
            labelSites = 0;
            acidSites = 0;
            try
            {
                harmony.Patch(paintGroundTracks, transpiler: new HarmonyMethod(typeof(EasyModePatch), nameof(GroundTracksTranspiler)));
                harmony.Patch(paintLabel,
                    prefix: new HarmonyMethod(typeof(EasyModePatch), nameof(PaintLabelPrefix)),
                    postfix: new HarmonyMethod(typeof(EasyModePatch), nameof(PaintLabelPostfix)),
                    transpiler: new HarmonyMethod(typeof(EasyModePatch), nameof(PaintLabelTranspiler)));

                if (labelSites != 1 || acidSites != 1)
                {
                    throw new InvalidOperationException("This vatSys version's ground label code is not recognised ("
                                                        + labelSites + "/1, " + acidSites + "/1).");
                }
            }
            catch
            {
                harmony.UnpatchAll(HarmonyId);
                throw;
            }
        }

        /// <summary>
        /// True for an on-ground track with a network pilot, no Mode C/S/ADS-B and
        /// no coupled flight plan, while Easy Mode is on.
        /// </summary>
        public static bool IsForced(Track track)
        {
            if (!EasyMode.Enabled || track == null)
            {
                return false;
            }

            RDP.RadarTrack radar = track.GetRadarTrack();
            return radar != null
                   && radar.OnGround
                   && (radar.RadarTypes & LabelledRadarTypes) == 0
                   && radar.ASMGCSCoupledFDR == null
                   && radar.CoupledFDR == null
                   && !string.IsNullOrEmpty(track.GetPilot()?.Callsign);
        }

        public static string AcidText(string text, bool isASMGCS, Track track)
        {
            if (!isASMGCS || !string.IsNullOrEmpty(text) || !IsForced(track))
            {
                return text;
            }

            return track.GetPilot()?.Callsign ?? text;
        }

        private static IEnumerable<CodeInstruction> GroundTracksTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            List<CodeInstruction> code = instructions.ToList();
            for (int i = 0; i < code.Count - 1; i++)
            {
                // track.HasASMGCSLabel = false;  ->  track.HasASMGCSLabel = IsForced(track);
                if (code[i].opcode == OpCodes.Ldc_I4_0 && code[i + 1].StoresField(HasLabelField))
                {
                    code[i].opcode = OpCodes.Dup; // keeps any labels on this instruction
                    code[i].operand = null;
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(EasyModePatch), nameof(IsForced))));
                    labelSites++;
                    i++;
                }
            }

            return code;
        }

        private static IEnumerable<CodeInstruction> PaintLabelTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            List<CodeInstruction> code = instructions.ToList();
            FieldInfo callsign = AccessTools.Field(typeof(NetworkPilot), nameof(NetworkPilot.Callsign));
            MethodInfo isNullOrEmpty = AccessTools.Method(typeof(string), nameof(string.IsNullOrEmpty));

            ParameterInfo[] parameters = original.GetParameters();
            int isAsmgcsArg = Array.FindIndex(parameters, p => p.Name == "isASMGCS") + (original.IsStatic ? 0 : 1);
            int trackArg = Array.FindIndex(parameters, p => p.Name == "tl") + (original.IsStatic ? 0 : 1);

            int read = code.FindIndex(c => c.LoadsField(callsign));
            if (read < 0)
            {
                return code;
            }

            // The ACID text local is the one the callsign is stored into.
            int store = code.FindIndex(read, c => c.IsStloc());
            if (store < 0)
            {
                return code;
            }

            CodeInstruction storeText = code[store];
            for (int i = store + 1; i < code.Count - 1; i++)
            {
                if (code[i].IsLdloc() && SameLocal(code[i], storeText) && code[i + 1].Calls(isNullOrEmpty))
                {
                    // text = AcidText(text, isASMGCS, tl);  then vatSys' own IsNullOrEmpty(text)
                    code.InsertRange(i + 1, new[]
                    {
                        CodeInstruction.LoadArgument(isAsmgcsArg),
                        CodeInstruction.LoadArgument(trackArg),
                        new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(EasyModePatch), nameof(AcidText))),
                        new CodeInstruction(OpCodes.Dup),
                        new CodeInstruction(storeText.opcode, storeText.operand),
                    });
                    acidSites++;
                    break;
                }
            }

            return code;
        }

        private static bool SameLocal(CodeInstruction load, CodeInstruction store)
        {
            int Index(CodeInstruction c)
            {
                if (c.operand is LocalBuilder builder)
                {
                    return builder.LocalIndex;
                }

                if (c.operand is int index)
                {
                    return index;
                }

                if (c.operand is byte small)
                {
                    return small;
                }

                OpCode op = c.opcode;
                if (op == OpCodes.Ldloc_0 || op == OpCodes.Stloc_0) return 0;
                if (op == OpCodes.Ldloc_1 || op == OpCodes.Stloc_1) return 1;
                if (op == OpCodes.Ldloc_2 || op == OpCodes.Stloc_2) return 2;
                if (op == OpCodes.Ldloc_3 || op == OpCodes.Stloc_3) return 3;
                return -1;
            }

            return Index(load) >= 0 && Index(load) == Index(store);
        }

        private static void PaintLabelPrefix(bool isASMGCS, Track tl, object clickspots, out int __state)
        {
            __state = isASMGCS && clickspots is IList list && IsForced(tl) ? list.Count : -1;
        }

        private static void PaintLabelPostfix(object clickspots, int __state)
        {
            if (__state >= 0 && clickspots is IList list)
            {
                while (list.Count > __state)
                {
                    list.RemoveAt(list.Count - 1);
                }
            }
        }
    }
}

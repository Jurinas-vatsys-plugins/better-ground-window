using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using vatsys;

namespace BetterGroundWindow
{
    /// <summary>
    /// Easy Mode "top-down" view: while Easy Mode is on and the main ASD range is
    /// at most <see cref="MaxRangeNm"/>, aircraft on the ground are drawn on the
    /// main ASD by vatSys' own Ground painter (ground symbols, Ground labels,
    /// ground colours) instead of as radar tracks. Display-only, like Easy Mode.
    /// <list type="number">
    /// <item>Prefix on ASDControlDX.PaintTracksLabels: for the main ASD, paint the
    /// on-ground tracks with PaintGroundTracks and the rest with PaintASDTracks.</item>
    /// <item>SelectBrush / SelectGroundBrush: their asdType reads go through
    /// <see cref="EffectiveAsdType"/>, which reports Ground while the prefix is
    /// painting ground tracks, so vatSys picks its ground colours.</item>
    /// <item>PaintGroundTracks / SelectGroundBrush: the Ground window's airport
    /// (DisplayPosition.ASMGCSAirport) is read through
    /// <see cref="ResolveGroundAirport"/>. On a Ground window that returns the same
    /// airport as before. On the main ASD, which has no such airport, it uses the
    /// track's departure or destination airport when the aircraft is there.</item>
    /// </list>
    /// No type here may derive from or implement a Harmony type (see ZoomPatch).
    /// </summary>
    internal static class TopDownPatch
    {
        /// <summary>Top-down range in NM; set in the settings window (default 80).</summary>
        public static double MaxRangeNm => PluginSettings.TopDownRangeNm;

        /// <summary>How close an aircraft must be to its dep/dest airport to count as "at" it.</summary>
        private const double AtAirportNm = 5.0;

        private const string HarmonyId = "BetterGroundWindow.TopDown";

        private const int MainAsdType = 0; // ASDControlDX.ASDTypes.MainASD

        private const int GroundAsdType = 2; // ASDControlDX.ASDTypes.Ground

        private static readonly Type AsdControlType = typeof(MMI).Assembly.GetType("vatsys.ASDControlDX", throwOnError: true);

        private static readonly FieldInfo AsdTypeField = AccessTools.Field(AsdControlType, "asdType");

        private static readonly FieldInfo RangeField = AccessTools.Field(AsdControlType, "range");

        private static readonly FieldInfo AirportField = AccessTools.Field(typeof(LogicalPositions.Position), nameof(LogicalPositions.Position.ASMGCSAirport));

        private static readonly MethodInfo PaintGroundTracks = AccessTools.Method(AsdControlType, "PaintGroundTracks");

        private static readonly MethodInfo PaintAsdTracks = AccessTools.Method(AsdControlType, "PaintASDTracks");

        private static readonly Airspace2.Airport NoAirport = new Airspace2.Airport { ICAOName = "" };

        [ThreadStatic]
        private static bool groundScope;

        private static Dictionary<string, int> sites;

        public static void Install()
        {
            if (AsdTypeField == null || RangeField == null || AirportField == null || PaintGroundTracks == null || PaintAsdTracks == null)
            {
                throw new MissingMemberException("ASDControlDX members for the top-down view were not found.");
            }

            MethodInfo paintTracksLabels = AccessTools.Method(AsdControlType, "PaintTracksLabels")
                                           ?? throw new MissingMethodException("ASDControlDX.PaintTracksLabels not found.");
            MethodInfo selectBrush = AccessTools.Method(AsdControlType, "SelectBrush")
                                     ?? throw new MissingMethodException("ASDControlDX.SelectBrush not found.");
            MethodInfo selectGroundBrush = AccessTools.Method(AsdControlType, "SelectGroundBrush")
                                           ?? throw new MissingMethodException("ASDControlDX.SelectGroundBrush not found.");

            sites = new Dictionary<string, int>();
            Harmony harmony = new Harmony(HarmonyId);
            try
            {
                HarmonyMethod transpiler = new HarmonyMethod(typeof(TopDownPatch), nameof(Transpiler));
                harmony.Patch(selectBrush, transpiler: transpiler);
                harmony.Patch(selectGroundBrush, transpiler: transpiler);
                harmony.Patch(PaintGroundTracks, transpiler: transpiler);
                harmony.Patch(paintTracksLabels, prefix: new HarmonyMethod(typeof(TopDownPatch), nameof(PaintTracksLabelsPrefix)));

                // asdType reads / ASMGCSAirport reads expected in each method.
                Expect("SelectBrush", 1, 0);
                Expect("SelectGroundBrush", 1, 3);
                Expect("PaintGroundTracks", 0, 2);

                // Dragging ground labels on the main ASD. Optional: without it the
                // view still works, labels just keep their Ground window position.
                MethodInfo mouseDown = AccessTools.Method(AsdControlType, "ASD_MouseDown", new[] { typeof(object), typeof(System.Windows.Forms.MouseEventArgs) });
                if (mouseDown != null && AccessTools.Field(typeof(Track), nameof(Track.HasASDLabel)) != null)
                {
                    harmony.Patch(mouseDown,
                        prefix: new HarmonyMethod(typeof(TopDownPatch), nameof(MouseDownPrefix)),
                        postfix: new HarmonyMethod(typeof(TopDownPatch), nameof(MouseDownPostfix)));
                    sites["ASD_MouseDown:labelDrag"] = 1;
                }
            }
            catch
            {
                harmony.UnpatchAll(HarmonyId);
                throw;
            }
        }

        /// <summary>Patch-site counts, for diagnostics: "Method:asdType" / "Method:airport".</summary>
        public static IReadOnlyDictionary<string, int> Sites => sites;

        private static void Expect(string method, int asdTypeReads, int airportReads)
        {
            sites.TryGetValue(method + ":asdType", out int a);
            sites.TryGetValue(method + ":airport", out int b);
            if (a != asdTypeReads || b != airportReads)
            {
                throw new InvalidOperationException("This vatSys version's " + method + " is not recognised ("
                                                    + a + "/" + asdTypeReads + ", " + b + "/" + airportReads + ").");
            }
        }

        public static int EffectiveAsdType(int asdType) => groundScope ? GroundAsdType : asdType;

        public static Airspace2.Airport ResolveGroundAirport(LogicalPositions.Position position, Track track)
        {
            if (position?.ASMGCSAirport != null)
            {
                return position.ASMGCSAirport;
            }

            return AirportOf(track) ?? NoAirport;
        }

        /// <summary>The track's departure or destination airport, if the aircraft is at it.</summary>
        public static Airspace2.Airport AirportOf(Track track)
        {
            FDP2.FDR fdr = track?.GetFDR(includeASMGCS: true);
            Coordinate location = track?.GetRadarTrack()?.LatLong;
            if (fdr == null || location == null)
            {
                return null;
            }

            foreach (string icao in new[] { fdr.DepAirport, fdr.DesAirport })
            {
                Airspace2.Airport airport = string.IsNullOrEmpty(icao) ? null : Airspace2.GetAirport(icao);
                if (airport?.LatLong != null && Conversions.CalculateDistance(airport.LatLong, location) <= AtAirportNm)
                {
                    return airport;
                }
            }

            return null;
        }

        /// <summary>True when an on-ground track should be drawn Ground-style on the main ASD.</summary>
        public static bool IsGroundTrack(Track track)
        {
            RDP.RadarTrack radar = track?.GetRadarTrack();
            return radar != null && !radar.Cancelled && radar.OnGround && radar.ActualAircraft != null;
        }

        /// <summary>True when the main ASD should currently use the top-down view.</summary>
        public static bool Active(object display)
        {
            return EasyMode.Enabled
                   && Convert.ToInt32(AsdTypeField.GetValue(display)) == MainAsdType
                   && RangeField.GetValue(display) is double range
                   && range > 0
                   && range <= MaxRangeNm;
        }

        private static bool PaintTracksLabelsPrefix(object __instance, object renderParams, IList<Track> tracks, object renderClickSpots)
        {
            if (tracks == null || !Active(__instance))
            {
                if (Convert.ToInt32(AsdTypeField.GetValue(__instance)) == MainAsdType)
                {
                    groundPainted = new Track[0];
                }

                return true;
            }

            List<Track> ground = new List<Track>();
            List<Track> rest = new List<Track>();
            foreach (Track track in tracks)
            {
                (IsGroundTrack(track) ? ground : rest).Add(track);
            }

            groundPainted = ground.ToArray();

            // Ground first, so airborne traffic and its labels are drawn on top.
            groundScope = true;
            try
            {
                Call(PaintGroundTracks, __instance, renderParams, ground, renderClickSpots);
            }
            finally
            {
                groundScope = false;
            }

            Call(PaintAsdTracks, __instance, renderParams, rest, renderClickSpots);
            return false;
        }

        /// <summary>Tracks the main ASD last drew Ground-style (written by the render, read on clicks).</summary>
        private static volatile Track[] groundPainted = new Track[0];

        /// <summary>A ground-painted track's radar-label fields, saved around a click.</summary>
        private sealed class SavedLabel
        {
            public Track Track;
            public bool HasAsdLabel;
            public int AsdDirection;
            public int AsdLength;
            public int SetDirection;
            public int SetLength;
        }

        /// <summary>
        /// Label dragging on the main ASD goes through vatSys' radar-label path
        /// (HasASDLabel / ASDLabelDirection / ASDLabelLeaderLength), because the
        /// display is not a Ground display. For the click, give every track drawn
        /// Ground-style its ground label position in those fields, so vatSys picks
        /// up, previews and drops the ground label. The postfix stores any new
        /// position as the ground label position and restores the radar label.
        /// </summary>
        private static void MouseDownPrefix(object __instance, out List<SavedLabel> __state)
        {
            __state = null;
            Track[] ground = groundPainted;
            if (ground.Length == 0 || !Active(__instance))
            {
                return;
            }

            __state = new List<SavedLabel>(ground.Length);
            foreach (Track track in ground)
            {
                __state.Add(new SavedLabel
                {
                    Track = track,
                    HasAsdLabel = track.HasASDLabel,
                    AsdDirection = track.ASDLabelDirection,
                    AsdLength = track.ASDLabelLeaderLength,
                    SetDirection = track.ASMGCSLabelDirection,
                    SetLength = track.ASMGCSLabelLeaderLength,
                });
                track.HasASDLabel = track.HasASMGCSLabel;
                track.ASDLabelDirection = track.ASMGCSLabelDirection;
                track.ASDLabelLeaderLength = track.ASMGCSLabelLeaderLength;
            }
        }

        private static void MouseDownPostfix(List<SavedLabel> __state)
        {
            if (__state == null)
            {
                return;
            }

            foreach (SavedLabel saved in __state)
            {
                Track track = saved.Track;
                if (track.ASDLabelDirection != saved.SetDirection || track.ASDLabelLeaderLength != saved.SetLength)
                {
                    // vatSys dropped the label: that is the new ground label position.
                    track.ASMGCSLabelDirection = track.ASDLabelDirection;
                    track.ASMGCSLabelLeaderLength = track.ASDLabelLeaderLength;
                }

                track.HasASDLabel = saved.HasAsdLabel;
                track.ASDLabelDirection = saved.AsdDirection;
                track.ASDLabelLeaderLength = saved.AsdLength;
            }
        }

        /// <summary>
        /// Invoke without wrapping exceptions: vatSys' render loop catches
        /// SharpDXException itself to recover from a lost device.
        /// </summary>
        private static void Call(MethodInfo method, object instance, params object[] args)
        {
            try
            {
                method.Invoke(instance, args);
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            List<CodeInstruction> code = instructions.ToList();
            MethodInfo getRadarTrack = AccessTools.Method(typeof(Track), nameof(Track.GetRadarTrack));
            MethodInfo effectiveAsdType = AccessTools.Method(typeof(TopDownPatch), nameof(EffectiveAsdType));
            MethodInfo resolveAirport = AccessTools.Method(typeof(TopDownPatch), nameof(ResolveGroundAirport));

            // How to push the current Track: the "tl" argument, or in PaintGroundTracks
            // the loop local that GetRadarTrack() is called on.
            CodeInstruction loadTrack = null;
            int trackArg = Array.FindIndex(original.GetParameters(), p => p.ParameterType == typeof(Track));
            if (trackArg >= 0)
            {
                loadTrack = CodeInstruction.LoadArgument(trackArg + 1);
            }
            else
            {
                int call = code.FindIndex(c => c.Calls(getRadarTrack));
                if (call > 0 && code[call - 1].IsLdloc())
                {
                    loadTrack = new CodeInstruction(code[call - 1].opcode, code[call - 1].operand);
                }
            }

            int asdTypeReads = 0;
            int airportReads = 0;
            for (int i = 0; i < code.Count; i++)
            {
                if (code[i].LoadsField(AsdTypeField))
                {
                    // ... asdType  ->  EffectiveAsdType(asdType)
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Call, effectiveAsdType));
                    asdTypeReads++;
                    i++;
                }
                else if (loadTrack != null && code[i].LoadsField(AirportField))
                {
                    // position.ASMGCSAirport  ->  ResolveGroundAirport(position, track)
                    // Rewrite the ldfld in place so branch labels on it are kept.
                    code[i].opcode = loadTrack.opcode;
                    code[i].operand = loadTrack.operand;
                    code.Insert(i + 1, new CodeInstruction(OpCodes.Call, resolveAirport));
                    airportReads++;
                    i++;
                }
            }

            sites[original.Name + ":asdType"] = asdTypeReads;
            sites[original.Name + ":airport"] = airportReads;
            return code;
        }
    }
}

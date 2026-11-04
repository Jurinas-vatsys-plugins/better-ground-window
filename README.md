# Better Ground Window

A [vatSys](https://virtualairtrafficsystem.com/) plugin for the **Ground** (ASMGCS) window. It adds an ICAO quick-jump box to the window's menu bar, an Unlimited Zoom option for Ground windows and the main ASD, and a Top-Down Mode that shows callsigns for aircraft without Mode C and puts the ground picture on the main ASD.

```
┌──────────────────────────────────────────────────────┐
│ Ground: YSSY                                         │
├──────────────────────────────────────────────────────┤
│ [YSSY] ▼  Position  Maps  Tools                      │
│                                                      │
```

## Features

- **Type an ICAO code and press Enter** to open that airport's ground position. Input is upper-cased automatically and limited to four letters or digits. **Esc** clears the field and moves focus back to the ground display.
- **▼ Recent airports.** Airports you opened successfully are saved, newest first, up to 15 of them. Pick one to jump back to it. The airport currently shown is checked. *Clear History* empties the list.
- History is shared between all Ground windows and kept across vatSys restarts in `%APPDATA%\BetterGroundWindow\history.txt`.
- If the code is invalid, or the profile has no ground position for it, the field turns the vatSys warning colour and a short message appears below it.
- **It looks like vatSys.** The ICAO field is vatSys' own `TextField` control: the sunken border, the highlight outline when focused, and the warning colour. The ▼ menu and the Unlimited Zoom item are drawn by the window's own menu renderer. Everything uses the profile's `Colours.xml` and the vatSys Terminus font, so it follows whatever colour scheme the profile uses.

When an airport has more than one ground position, the plugin picks a position named exactly after the ICAO code first. If there is none, it picks one whose name starts with the code, and after that the first one alphabetically. The other positions are still available from the normal **Position** menu.

Loading a position goes through the window's existing Position menu item. Check marks, visible maps and the window title therefore stay the same as when you choose the position by hand.

### Unlimited Zoom

vatSys normally limits a Ground window to a range of **0.1 to 30 NM**, and the main ASD to no closer than **10 NM**. Turn on **Unlimited Zoom** to widen both to **0.01 to 9999 NM**. You can switch it from **Tools → Unlimited Zoom** in any Ground window or from the settings window. Turn it off the same way.

- It is one switch for the Ground windows and the main ASD. Extra ASD windows keep vatSys' limits.
- The option is off by default and is remembered across restarts in `%APPDATA%\BetterGroundWindow\settings.txt`.
- It works for every way of zooming: range keys, the zoom box, *Default*/*Restore*, and the mouse wheel.
- **Mouse wheel:** vatSys has no wheel zoom of its own; the wheel comes from plugins such as *Persistent Pan* / *Drag And Zoom*. Those step the range in whole nautical miles, so on their own they stop at about 2 NM (`round(2 × 0.8) = 2`). While Unlimited Zoom is on, this plugin takes over their `ZoomWithWheel` on Ground windows and the main ASD, and zooms in fractional ×0.8 / ×1.25 steps, all the way down to 0.01 NM. Elsewhere, and with the option off, the other plugin's own wheel code runs.
- Zooming the main ASD in close does not shrink your network visibility range. vatSys only uses the main ASD range to place extra visibility centres when it is wider than your visibility range.
- When you turn it off, any display zoomed past the normal limits snaps back inside them.
- If the plugin cannot patch your vatSys version, the menu item reads *Unlimited Zoom (Unavailable)* and is greyed out.

### Top-Down Mode

For working top-down (covering TWR/GND from a radar position). Turn it on with **Ctrl+T** in any vatSys window, from **Tools → Top-Down Mode** in a Ground window, or from the settings window. It is **off** every time vatSys starts. While it is on:

- **Callsigns for aircraft without Mode C.** vatSys draws an aircraft on the ground that has no Mode C (transponder on standby) and no coupled flight plan as a bare symbol, with no label. Top-Down Mode gives those aircraft a small Ground label with their network callsign, on Ground windows and on the main ASD. Aircraft with Mode C, Mode S/ADS-B or a coupled flight plan are labelled by vatSys exactly as before. Once a pilot turns Mode C on, vatSys' own label takes over automatically.
- **Ground view on the main screen within the Top-Down Range.** The range is set in the settings window, 1 to 500 NM, 80 NM by default. While the main ASD range is at or below it, aircraft on the ground are drawn on the main ASD the way a Ground window draws them: ground symbols, Ground labels and ground colours. Departure, arrival and local colours are worked out from the flight plan when the aircraft is within 5 NM of its departure or destination airport. Airborne traffic is drawn as normal radar tracks on top.
- **Ground maps on the main screen, for the airports in view.** Within the same Top-Down Range, the main ASD also shows every ground map of each airport in view: runways, taxiways, taxilines, aprons, buildings and labels. No Ground window is needed. Airports outside the view are not loaded, so performance stays reasonable. Pan or zoom and airports come and go within half a second. "In view" means within about 0.6 × the range, plus 5 NM, of the screen centre, and never more than the Top-Down Range.
- Zoom out past the Top-Down Range or turn Top-Down Mode off, and the main ASD goes back to normal. Extra ASD windows are not affected.
- **Ground windows are not affected.** The main ASD picks its airports from its own view only. Ground windows keep their own position and maps; Top-Down Mode never adds or removes maps there. The only Top-Down Mode effect inside a Ground window is the callsign label for aircraft without Mode C.
- **Display only.** Top-Down Mode does not touch flight plans, coupling, strips, radar data or any profile XML (`Labels.xml` is not changed). The extra callsign labels cannot be clicked, so they cannot open menus or couple anything.
- **Ground maps by hand.** The main window's **Maps** menu also has a **Ground Maps** submenu with every airport's ground maps, so you can show any of them yourself, with or without Top-Down Mode. Top-Down Mode leaves maps you ticked there alone. Like other ASD maps, they reset when you change position. While Top-Down Mode is showing an airport, its maps come back even if you untick them.
- **Limitation:** on the main ASD, dragging a ground label with the right mouse button uses vatSys' radar-label drag, so ground labels there keep the position set in a Ground window.
- **Custom key:** in the settings window, click the key button and press the new combination. *Reset* restores Ctrl+T, *Clear*, *Del* or *Backspace* removes the hotkey, and *Esc* cancels. The key is saved in `%APPDATA%\BetterGroundWindow\settings.txt`.

### Scale Marker

The settings window has a **Scale Marker** switch that shows or hides the range scale vatSys draws on radar screens (top left by default). It is vatSys' own setting, the same as **Display Settings → Scale Marker**, so vatSys remembers it across restarts.

### Settings window

Open it from **Settings → Better Ground Window** in the main window, or **Tools → Better Ground Settings** in a Ground window. It is a normal vatSys window: it uses vatSys' window frame, buttons, colours and font, and every change applies immediately.

```
┌ Better Ground Window ─────────────────── × ┐
│ Top-Down Mode                 [    On    ] │
│ Top-Down Range NM             [    80    ] │
│ Top-Down Key                  [  Ctrl+T  ] │
│                               [Reset][Clr] │
│ Unlimited Zoom                [   Off    ] │
│ Scale Marker                  [    On    ] │
│                               [  Close   ] │
└────────────────────────────────────────────┘
```

## How it works

The vatSys SDK only exposes `vatsys.Plugin.IPlugin` and does not give plugins access to the Ground window. The plugin is exported through MEF, as the SDK requires. Every 500 ms it looks through `Application.OpenForms` for `vatsys.ASMGCSWindow` instances. When it finds a new one, it uses reflection to insert a vatSys `TextField` (hosted in a `ToolStripControlHost`) and a ▼ menu item into that window's private `mainMenu`.

Private members it relies on (verified against vatSys 0.4.9305):

| Member | Use |
| --- | --- |
| `ASMGCSWindow.mainMenu` | The top menu bar the box is inserted into |
| `ASMGCSWindow.positionsToolStripMenuItem` | Finding the menu item for a position |
| `ASMGCSWindow.asmgcsControlDX1` | Returning focus to the ground display |
| `ASMGCSWindow.positionItem_Click` | Fallback when a position has no menu item |
| `ASMGCSWindow.toolsToolStripMenuItem` | Where the Unlimited Zoom item goes |
| `ASDControlDX.SetZoom`, `asdType`, `range` | The zoom limit patch |
| `ASDControlDX.PaintGroundTracks`, `PaintLabel` | The Top-Down Mode patch |
| `ASDControlDX.PaintTracksLabels`, `PaintASDTracks`, `SelectBrush`, `SelectGroundBrush` | The main-ASD top-down view |
| `MMI.MainForm`, `MainForm.mapsToolStripMenuItem`, `mapItem_Click`, `MapsDropDown_Closing` | The Ground Maps submenu |
| `MainForm.ASD`, `ASDControlDX.IsMapVisible`/`SetMapVisible`, `range`, `MMI.MainASDCentre` | Showing in-view airports' ground maps on the main ASD |

If a future vatSys release renames these, the plugin does nothing to that window. It does not crash vatSys.

The zoom limit is hard-coded in `ASDControlDX.SetZoom`, which clamps the range to `MMI.MinimumASDRange`/`MaximumASDRange` and then clamps the internal zoom factor to 0.5–16000. A [Harmony](https://github.com/pardeike/Harmony) transpiler runs at startup. After each of those four limits it inserts a call to the plugin, which widens the limit only when Unlimited Zoom is on and the display is a Ground window or the main ASD. If the plugin cannot find exactly those four limits, it removes its patch and disables the option.

Top-Down Mode uses three Harmony patches, all of them in the paint code:

1. In `PaintGroundTracks`, the branch for an on-ground track with no flight plan and no Mode C/S/ADS-B stores `track.HasASMGCSLabel = false`. That value is replaced with "Top-Down Mode is on and this aircraft has a pilot", so the track gets vatSys' GroundLimited label. `HasASMGCSLabel` is only read by the painter and by label dragging.
2. In `PaintLabel`, the `LABEL_ITEM_ACID` text is empty for such a track. Just before vatSys checks it, the pilot's network callsign is put in.
3. A prefix and postfix on `PaintLabel` remove the clickspots that were added while painting such a label.

If either code pattern isn't found exactly once, all three patches are removed and Top-Down Mode shows as *(Unavailable)*.

The top-down view adds three more patches:

1. A prefix on `PaintTracksLabels` does the work on the main ASD while Top-Down Mode is on and the range is within the Top-Down Range. It sends on-ground tracks to vatSys' own `PaintGroundTracks` and everything else to `PaintASDTracks`.
2. `SelectBrush` and `SelectGroundBrush` read `asdType` through the plugin, which reports "Ground" while the ground tracks are being painted. vatSys then picks its ground colours.
3. `PaintGroundTracks` and `SelectGroundBrush` read the Ground window's airport (`DisplayPosition.ASMGCSAirport`) through the plugin. On a Ground window this returns the same airport as before. On the main ASD it returns the track's departure or destination airport if the aircraft is within 5 NM of it.

If these patterns aren't found exactly as expected, only the top-down view is switched off. Top-Down Mode on Ground windows keeps working.

Harmony 2.3.3 is embedded inside `BetterGroundWindow.dll`, so the plugin is still a single file. If another plugin has already loaded a compatible Harmony 2.x (VATPRCPlugin ships 2.3.3, for example), the plugin uses that copy.

The embedded copy only becomes available once the plugin is constructed. vatSys, through MEF, loads every type in every plugin DLL before it constructs any plugin. So no type in this assembly may derive from or implement a Harmony type, and that includes compiler-generated iterator classes (`yield return` over `CodeInstruction`). Otherwise `GetTypes()` fails and vatSys cannot start any profile that does not ship its own `0Harmony.dll`. Version 1.3.0 had this bug, and 1.3.1 fixes it.

## Building

Requirements: the .NET SDK (any recent version) and a local vatSys install.

```bash
dotnet build -c Release
```

The project references `vatSys.exe` from `G:\vatSys\vatSys\bin` by default. To use a different install, point the build at it:

```bash
dotnet build -c Release -p:VatSysBin="C:\Program Files (x86)\vatSys\bin"
```

Output:

- `bin\Release\net472\BetterGroundWindow.dll`, the plugin itself
- `dist\BetterGroundWindow-<version>.zip`, a release package with the DLL, this README and the install script inside a single `BetterGroundWindow` folder

## Installing

vatSys loads plugins from two places. You can install into either one, or both:

| Location | Path | Loaded for |
| --- | --- | --- |
| Profile | `Documents\vatSys Files\Profiles\<profile>\Plugins\` | That profile only |
| vatSys program | `<vatSys install>\bin\Plugins\` | Every profile |

vatSys searches both folders recursively, so the plugin can sit in its own `BetterGroundWindow` sub-folder. If it is installed in both places, only one copy runs. vatSys scans the profile folder first, so the profile copy is the one that runs.

### With the install script

Run this from the repository after building, or from an extracted release zip:

```powershell
# Into a profile, by name (from Documents\vatSys Files\Profiles)
.\Install-BetterGroundWindow.ps1 -ProfileName "ZBPE FIR Beijing"

# Into a profile, by full path
.\Install-BetterGroundWindow.ps1 -ProfilePath "D:\vatSys Profiles\Australia"

# Into the vatSys program folder (applies to all profiles)
.\Install-BetterGroundWindow.ps1 -VatSysBin "C:\Program Files (x86)\vatSys\bin"

# Remove it again: add -Uninstall to any of the above
.\Install-BetterGroundWindow.ps1 -ProfileName "ZBPE FIR Beijing" -Uninstall
```

The script copies the DLL to `<target>\Plugins\BetterGroundWindow\BetterGroundWindow.dll`. It also removes any older copy placed loose in `Plugins\`. Installing under `Program Files` needs an elevated PowerShell. Close vatSys before installing.

### By hand

Extract the release zip into either `Plugins` folder above, so you end up with `Plugins\BetterGroundWindow\BetterGroundWindow.dll`. Then restart vatSys and open a Ground window from the main menu.

### Including it in a profile you distribute

Profile maintainers can ship the plugin with their profile. Put the `BetterGroundWindow` folder from the release zip into the profile's `Plugins` folder. The plugin needs no other files and no profile XML changes. It keeps its history per Windows user in `%APPDATA%`, not in the profile, so the profile folder stays clean.

## License

GNU General Public License v3.0. See [LICENSE](LICENSE).

The embedded Harmony library is MIT-licensed. See [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

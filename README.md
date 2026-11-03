# Better Ground Window

A [vatSys](https://virtualairtrafficsystem.com/) plugin for the **Ground** (ASMGCS) window. It adds an ICAO quick-jump box to the window's menu bar, an option to remove the Ground zoom limit, and an Easy Mode that shows callsigns for aircraft without Mode C.

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

vatSys normally limits a Ground display to a range of **0.1 to 30 NM**. Turn on **Tools → Unlimited Zoom** in any Ground window to widen that to **0.01 to 9999 NM**. Click it again to turn it off.

- The option is off by default. It applies to all Ground windows at once and is remembered across restarts in `%APPDATA%\BetterGroundWindow\settings.txt`.
- It works for every way of zooming: range keys, the zoom box, *Default*/*Restore*, and the mouse wheel.
- **Mouse wheel:** vatSys has no wheel zoom of its own; the wheel comes from plugins such as *Persistent Pan* / *Drag And Zoom*. Those step the range in whole nautical miles, so on their own they stop at about 2 NM (`round(2 × 0.8) = 2`). While Unlimited Zoom is on, this plugin takes over their `ZoomWithWheel` on Ground displays and zooms in fractional ×0.8 / ×1.25 steps, all the way down to 0.01 NM. Radar windows, and Ground windows with the option off, still use the other plugin's own wheel code.
- Only Ground displays are affected. Radar ASD windows keep their normal limits.
- When you turn it off, any Ground window zoomed past the normal limits snaps back inside them.
- If the plugin cannot patch your vatSys version, the menu item reads *Unlimited Zoom (Unavailable)* and is greyed out.

### Easy Mode

vatSys draws an aircraft on the ground that has no Mode C (transponder on standby) and no coupled flight plan as a bare symbol, with no label. **Easy Mode** gives those aircraft a small Ground label with their network callsign.

- **Toggle:** press **Ctrl+T** in any vatSys window, or use **Tools → Easy Mode** in a Ground window. Easy Mode is **off** every time vatSys starts.
- **Only for aircraft without Mode C.** Aircraft with Mode C, Mode S/ADS-B or a coupled flight plan are labelled by vatSys exactly as before. Once a pilot turns Mode C on, vatSys' own label takes over automatically.
- **Display only.** Easy Mode does not touch flight plans, coupling, strips, radar data or any profile XML (`Labels.xml` is not changed). The extra labels cannot be clicked, so they cannot open menus or couple anything. Only drawing is affected, and only on Ground windows. Radar ASD labels are unchanged.
- **Custom key:** open **Settings → Better Ground Window** in the main window, or **Tools → Better Ground Settings** in a Ground window. Click the key button and press the new combination. *Del* or *Backspace* removes the hotkey, and *Esc* cancels. The key is saved in `%APPDATA%\BetterGroundWindow\settings.txt`.

### Settings window

The settings window is a normal vatSys window. It uses vatSys' window frame, buttons, colours and font, and every change applies immediately:

```
┌ Better Ground Window ─────────────────── × ┐
│ Easy Mode                     [    On    ] │
│ Easy Mode Key                 [  Ctrl+T  ] │
│ Unlimited Zoom                [   Off    ] │
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
| `ASDControlDX.PaintGroundTracks`, `PaintLabel` | The Easy Mode patch |

If a future vatSys release renames these, the plugin does nothing to that window. It does not crash vatSys.

The zoom limit is hard-coded in `ASDControlDX.SetZoom`, which clamps the range to `MMI.MinimumASDRange`/`MaximumASDRange` and then clamps the internal zoom factor to 0.5–16000. A [Harmony](https://github.com/pardeike/Harmony) transpiler runs at startup. After each of those four limits it inserts a call to the plugin, which widens the limit only when Unlimited Zoom is on and the display is a Ground display. If the plugin cannot find exactly those four limits, it removes its patch and disables the option.

Easy Mode uses three Harmony patches, all of them in the paint code:

1. In `PaintGroundTracks`, the branch for an on-ground track with no flight plan and no Mode C/S/ADS-B stores `track.HasASMGCSLabel = false`. That value is replaced with "Easy Mode is on and this aircraft has a pilot", so the track gets vatSys' GroundLimited label. `HasASMGCSLabel` is only read by the painter and by label dragging.
2. In `PaintLabel`, the `LABEL_ITEM_ACID` text is empty for such a track. Just before vatSys checks it, the pilot's network callsign is put in.
3. A prefix and postfix on `PaintLabel` remove the clickspots that were added while painting such a label.

If either code pattern isn't found exactly once, all three patches are removed and Easy Mode shows as *(Unavailable)*.

Harmony 2.3.3 is embedded inside `BetterGroundWindow.dll`, so the plugin is still a single file. If another plugin has already loaded a compatible Harmony 2.x (VATPRCPlugin ships 2.3.3, for example), the plugin uses that copy.

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

MIT

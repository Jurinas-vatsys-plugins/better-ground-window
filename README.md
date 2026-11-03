# Better Ground Window

A [vatSys](https://virtualairtrafficsystem.com/) plugin for the **Ground** (ASMGCS) window. It adds an ICAO quick-jump box to the window's menu bar, plus an option to remove the Ground zoom limit.

```
┌──────────────────────────────────────────────────────┐
│ Ground: YSSY                                         │
├──────────────────────────────────────────────────────┤
│ [YSSY ▼]  Position  Maps  Tools                      │
│                                                      │
```

## Features

- **Type an ICAO code to open it.** Once four characters are in the box, the airport's ground position loads. You don't need to press Enter. Input is upper-cased automatically.
- **Enter** reloads whatever code is in the box. **Esc** clears the box and moves focus back to the ground display.
- **▼ history dropdown.** Airports you opened successfully are saved, newest first, up to 15 of them. Pick one to jump back to it. The last entry, *Clear history*, empties the list.
- History is shared between all Ground windows and kept across vatSys restarts in `%APPDATA%\BetterGroundWindow\history.txt`.
- If the profile has no ground position for the code, the box flashes in the warning colour, and its tooltip gives the reason.

When an airport has more than one ground position, the plugin picks a position named exactly after the ICAO code first. If there is none, it picks one whose name starts with the code, and after that the first one alphabetically. The other positions are still available from the normal **Position** menu.

Loading a position goes through the window's existing Position menu item. Check marks, visible maps and the window title therefore stay the same as when you choose the position by hand.

### Unlimited Zoom

vatSys normally limits a Ground display to a range of **0.1 to 30 NM**. Turn on **Tools → Unlimited Zoom** in any Ground window to widen that to **0.01 to 9999 NM**. Click it again to turn it off.

- The option is off by default. It applies to all Ground windows at once and is remembered across restarts in `%APPDATA%\BetterGroundWindow\settings.txt`.
- It works for every way of zooming: range keys, the zoom box, *Default*/*Restore*, and wheel-zoom plugins such as Persistent Pan.
- Only Ground displays are affected. Radar ASD windows keep their normal limits.
- When you turn it off, any Ground window zoomed past the normal limits snaps back inside them.
- If the plugin cannot patch your vatSys version, the menu item is greyed out and its tooltip says why.

## How it works

The vatSys SDK only exposes `vatsys.Plugin.IPlugin` and does not give plugins access to the Ground window. The plugin is exported through MEF, as the SDK requires. Every 500 ms it looks through `Application.OpenForms` for `vatsys.ASMGCSWindow` instances. When it finds a new one, it uses reflection to insert a `ToolStripComboBox` into that window's private `mainMenu`.

Private members it relies on (verified against vatSys 0.4.9305):

| Member | Use |
| --- | --- |
| `ASMGCSWindow.mainMenu` | The top menu bar the box is inserted into |
| `ASMGCSWindow.positionsToolStripMenuItem` | Finding the menu item for a position |
| `ASMGCSWindow.asmgcsControlDX1` | Returning focus to the ground display |
| `ASMGCSWindow.positionItem_Click` | Fallback when a position has no menu item |
| `ASMGCSWindow.toolsToolStripMenuItem` | Where the Unlimited Zoom item goes |
| `ASDControlDX.SetZoom`, `asdType`, `range` | The zoom limit patch |

If a future vatSys release renames these, the plugin does nothing to that window. It does not crash vatSys.

The zoom limit is hard-coded in `ASDControlDX.SetZoom`, which clamps the range to `MMI.MinimumASDRange`/`MaximumASDRange` and then clamps the internal zoom factor to 0.5–16000. A [Harmony](https://github.com/pardeike/Harmony) transpiler runs at startup. After each of those four limits it inserts a call to the plugin, which widens the limit only when Unlimited Zoom is on and the display is a Ground display. If the plugin cannot find exactly those four limits, it removes its patch and disables the option.

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

# Better Ground Window

A [vatSys](https://virtualairtrafficsystem.com/) plugin that adds an ICAO quick-jump box to the menu bar of every **Ground** (ASMGCS) window.

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

## How it works

The vatSys SDK only exposes `vatsys.Plugin.IPlugin` and does not give plugins access to the Ground window. The plugin is exported through MEF, as the SDK requires. Every 500 ms it looks through `Application.OpenForms` for `vatsys.ASMGCSWindow` instances. When it finds a new one, it uses reflection to insert a `ToolStripComboBox` into that window's private `mainMenu`.

Private members it relies on (verified against vatSys 0.4.9305):

| Member | Use |
| --- | --- |
| `ASMGCSWindow.mainMenu` | The top menu bar the box is inserted into |
| `ASMGCSWindow.positionsToolStripMenuItem` | Finding the menu item for a position |
| `ASMGCSWindow.asmgcsControlDX1` | Returning focus to the ground display |
| `ASMGCSWindow.positionItem_Click` | Fallback when a position has no menu item |

If a future vatSys release renames these, the plugin does nothing to that window. It does not crash vatSys.

## Building

Requirements: the .NET SDK (any recent version) and a local vatSys install.

```bash
dotnet build -c Release
```

The project references `vatSys.exe` from `G:\vatSys\vatSys\bin` by default. To use a different install, point the build at it:

```bash
dotnet build -c Release -p:VatSysBin="C:\Program Files (x86)\vatSys\bin"
```

Output: `bin\Release\net472\BetterGroundWindow.dll`

## Installing

Copy `BetterGroundWindow.dll` into one of the folders vatSys scans for plugins:

- `<vatSys install>\bin\Plugins\`
- `<your profile folder>\Plugins\`

Then restart vatSys and open a Ground window from the main menu.

## License

MIT

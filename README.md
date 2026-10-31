# Blasting Layout

A Windows desktop prototype for manually plotting blast holes and connection paths on a graph-paper canvas. Built with C# and WPF on .NET 8.

> **Prototype notice:** This application is a visual layout tool, not an engineering or safety system. It does not calculate blast designs, charges, or operational parameters. Verify any real-world work with qualified professionals and approved procedures.

## Features

- Add rows of holes in square or staggered patterns.
- Choose stagger offsets and insert rows above or below existing rows.
- Automatically label rows (`A`, `B`, `C`, …, `Z`, `AA`, …), or provide custom row names.
- Select, move, copy, paste, and delete holes; hole positions snap to the graph grid when moved.
- Edit a hole's ID, depth, burden, and spacing.
- Draw connector paths between holes, and select or delete individual connector segments or whole chains.
- Zoom and pan around the canvas, toggle the grid and side panels, and undo layout changes.
- View live totals for holes and connectors.

The current prototype keeps the layout in memory; it does not save or export layouts.

## Requirements

- Windows
- .NET 8 SDK with Windows desktop/WPF support

## Build and run

From the repository root in PowerShell:

```powershell
dotnet restore ".\Blasting Layout\Blasting Layout.csproj"
dotnet build ".\Blasting Layout\Blasting Layout.csproj"
dotnet run --project ".\Blasting Layout\Blasting Layout.csproj"
```

Alternatively, open `Blasting Layout\Blasting Layout.csproj` in Visual Studio and run the project.

## Basic use

1. Choose a pattern, stagger direction, and row insertion direction in the **Toolbox**.
2. Enter a hole count and optionally a row name, then select **Add Row**.
3. Select holes on the canvas to edit their properties. Drag a hole to move it; select multiple holes to move them together.
4. Select **Draw Connector** (or press `C` while the canvas has focus), then select a start hole and an end hole. Press `Esc` to cancel.
5. Use the canvas controls to zoom, pan, and adjust the view.

## Keyboard and mouse controls

| Input | Action |
|---|---|
| `Ctrl+Z` | Undo |
| `Ctrl+C` | Copy selected holes |
| Click canvas, then `Ctrl+V` | Paste copied holes at the clicked location |
| `C` | Start or stop connector drawing |
| `Esc` | Cancel connector drawing |
| Drag empty canvas | Select holes in an area |
| `Ctrl`+click | Add/remove a hole from selection; select a connector segment individually |
| Drag selected hole | Move the selected hole(s) |
| Mouse wheel | Zoom |
| Middle-button drag | Pan |

Keyboard shortcuts that involve `Ctrl` are available when focus is not in a text box or combo box.

## Revision history and development steps

The following history summarizes the requirements and follow-up changes recorded during development. The original notes were not dated, so the steps are listed in the order they were recorded rather than assigned dates.

1. **Added canvas navigation and undo.** Added `Ctrl+Z` undo, zoom controls, and middle-button canvas panning.
2. **Added connector drawing controls.** Added a connector-drawing shortcut and toggle, selectable connector colors, and arrowheads.
3. **Improved the plotting canvas.** Expanded the canvas for large layouts, centered the initial plotting area, and aligned hole positions with the graph grid.
4. **Added staggered pattern options.** Made staggered the default pattern and added right, left, and alternating right/left row offsets.
5. **Added layout editing tools.** Added canvas recentering, multi-hole selection and group dragging, hole renaming, and editable hole properties.
6. **Corrected stagger direction behavior.** Updated row positioning so the selected left/right offset applies as intended; alternating offsets are used only for the alternating options.
7. **Added row insertion direction.** Added controls for inserting rows above or below existing rows, with automatic row labels and support for custom names.
8. **Fixed labels for rows inserted above.** Reversed automatic label assignment so rows appear top-to-bottom as `D`, `C`, `B`, `A` after inserting above, rather than `A`, `B`, `C`, `D`.
9. **Documented the project for GitHub.** Added this README with project overview, setup, usage, controls, and development history.

## Project layout

```text
Blasting Layout/
  Models/       Blast holes and connectors
  Services/     Layout positioning and connector path logic
  ViewModels/   UI state and commands
  Views/        WPF interface
```

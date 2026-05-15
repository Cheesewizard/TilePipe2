# TilePipe Unity Bridge

This package imports TilePipe2 generated Unity manifests as Unity 2D Tilemap Extras `RuleTile` assets.

## Requirements

- Unity 2021.3 or newer.
- `com.unity.2d.tilemap.extras` installed from Package Manager.
- A Godot executable available to Unity. The bridge runs TilePipe2 headless for generation.

## Setup

1. In Unity, open Package Manager.
2. Choose `Add package from disk...`.
3. Select this package's `package.json`.
4. Install `2D Tilemap Extras` from Package Manager.
5. Open `Tools > TilePipe > Unity Bridge`.

## Use

Fill in:

- `Godot Executable`: `godot` if it is on `PATH`, otherwise the full executable path.
- `TilePipe2 Root`: the folder containing TilePipe2's `project.godot`.
- `TilePipe Project Dir`: the folder containing your `.tptile` project files.
- `Tile File`: the `.tptile` file name relative to `TilePipe Project Dir`.
- `Atlas Asset`, `Manifest Asset`, and `RuleTile Asset`: Unity `Assets/...` output paths.

Press `Generate And Import` to create or update the atlas, manifest, and `RuleTile`.

Enable `Live Reimport` to watch the `.tptile`, source texture, ruleset JSON, and template PNG. When one changes, Unity waits briefly, runs TilePipe2 headless, reimports the atlas sprites, and updates the existing `RuleTile` asset.

The first version intentionally keeps TilePipe2/Godot as the generator. A later C# generation path can remove that dependency while preserving this manifest shape.

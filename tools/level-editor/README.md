# Cannon level editor

Local visual editor for level records consumed by the Unity game.

## Run

From `tools/level-editor`:

```powershell
npm start
```

Open <http://127.0.0.1:4173>.

The server binds only to localhost. `Save records` validates and writes:

- `Assets/Resources/LevelEditor/levels.json`
- `Assets/Resources/LevelEditor/objects.json`

Unity loads these same files through `Resources`, including in player builds.

## Edit

- Choose, create, duplicate, or delete a level from the top bar.
- Drag a reusable asset from the left palette onto the map.
- Drag placed objects to reposition them. Building edges snap to the selected 0.25, 0.5, or 1-unit grid.
- Select an object to resize it or enter exact position, rotation, scale, gravity, collision, and orbit metrics.
- Use `+ Asset` to create reusable planet, sun, moon, cannon, target, or structure definitions.
- Use `Reset unsaved` to restore the last loaded or saved catalog without writing files.
- Save only when every level has exactly one cannon, at least one target, and at least one gravity body.

Castle pieces ship as aligned 2x1 bricks, 4x0.5 beams, 0.5x4 columns, and 2x3 towers. They use the same runtime block behavior as existing stone blocks.

## Unity representation

Assets are JSON records, not Unity prefabs:

- `objects.json` contains reusable `ObjectDefinitionRecord` values such as behavior, dimensions, color, mass, and durability.
- `levels.json` contains `LevelObjectRecord` instances referencing a definition ID plus position and per-instance overrides.
- `LevelCatalogLoader` imports both files as Unity `TextAsset` resources. `GameManager` creates runtime primitives and components from those records.

Unity's standard TextAsset Inspector previews JSON but does not provide safe structured editing. Use `Cannon > Level Editor > Open Web Editor` inside Unity for visual editing. `Select JSON Records` locates the source records; `Validate JSON Records` checks every reference through the runtime loader.

## Test

```powershell
npm test
npm run test:browser
```

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
- Drag a definition from the left palette onto the map.
- Drag placed objects to reposition them.
- Select an object to resize it or enter exact position, rotation, scale, gravity, collision, and orbit metrics.
- Use `+ Definition` to create reusable planet, sun, moon, cannon, target, or structure definitions.
- Save only when every level has exactly one cannon, at least one target, and at least one gravity body.

## Test

```powershell
npm test
```

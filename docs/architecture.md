# Architecture

How the code is put together, where new things go, and lessons learned the hard way.
Gameplay rules are in [mechanics/](mechanics/).

## The shape of the code

- **`core/Character.cs`** is the shared body of the player and every goon: walking legs, the
  gun in the hand, one-hit death (with or without blood), knockdown, being held down (`Pinned`),
  flinching under blows, alive and dead looks. Everything that happens *to* a body lives here.
- **`player/Player.cs`** and **`enemies/Enemy.cs`** build on `Character`. The player adds
  keyboard/mouse control, the camera and the takedowns (executions, stealth kills); the goon adds
  the AI (behaviours, sight, hearing, chasing, route-finding).
- **`weapons/`**: `WeaponData` is one gun's settings, saved as a data file per gun (`*.tres`).
  `Gun` is the gun in a hand and does the shooting. `Bullet` flies and hits. `WeaponPickup` is
  a gun on the floor or in the air (dropped or thrown).
- **`props/Door.cs`**: swinging doors, pushed by walking into them or by bullets (`Shove`); they block
  bullets and sight and slam goons. Rules, including how to place doors, in [mechanics/doors.md](mechanics/doors.md).
- **`effects/`**: goo, particles, screen shake (`ShakeCamera.Shake`), hit stop
  (`HitStop.Freeze`), and `Effects.Spawn` for dropping an effect into the level (it returns the effect,
  to resize it).
- **`audio/`**: sounds. A sound that should vary is an `AudioStreamRandomizer` file (`*.tres`)
  listing its takes; it picks one and shifts the pitch a little on every play.
- **`core/Noise.cs`**: gunshots alert goons in earshot. **`core/LevelNavigation.cs`** builds the
  walkable map goons route on, when the level starts.
- **`Main.cs`**: the top of the game: HUD, restart, camera limits, starts the navigation map.
- **`debug/`**: testing switches (`DevSettings.Current`), active in editor runs only.

## Where new things go

- Affects both you and goons: `Character`. Goon-only: `Enemy`. Player-only: `Player`.
- A new gun: a new `WeaponData` file in `weapons/`, no code.
- A new visual effect: its own scene, spawned with `Effects.Spawn` into the level (never as a
  child of a character, or it moves and dies with them).
- A new testing switch: a field in `DevSettings`, an `if` where it matters, and a line in
  `ActiveSummary()`.

## Conventions

- Scripts find each other through groups set in the scenes: `player`, `enemies`, `pickups`, `doors`.
- Physics layers (named in Project Settings): world, player, enemies, bullets, furniture, doors.
  `core/Layers.cs` mirrors them in code. Furniture blocks walking only; doors block bullets and
  sight only.
- Draw order, bottom to top: floor, goo, corpses and floor guns, the living, bullets.
- Tuning values live in the Inspector (scene and data files), not in code.
- Every goon's defaults are in `enemies/enemy.tscn`; set a single goon's values in the level.

## Lessons learned

- **Close a scene or data file in Godot before editing it outside Godot.** The editor keeps its
  own copy in memory and can save it over the outside edit; this has wiped files before. If in
  doubt, close Godot without saving and reopen.
- **C# changes need a build** before the editor or the game sees them.
- **Fast things skip thin things.** A bullet moves further each frame than a door is thick, so
  bullets check the whole stretch they fly, not just where they land.
- **The game starts from `main.tscn`.** F6 runs just the open scene, for testing one piece.
- **Testing switches stay local:** git ignores local changes to `debug/dev_settings.tres`, so the
  repository always holds a normal game.
- **No other game's name anywhere in the repository** (code, comments, commits, docs).

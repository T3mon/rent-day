# Architecture

How the code is put together, where new things go, and lessons learned the hard way.
Gameplay rules are in [mechanics/](mechanics/).

## The shape of the code

- **`core/Character.cs`** is the shared body of the player and every goon: walking legs, the
  weapon in the hand, one-hit death (with or without blood), knockdown, being held down (`Pinned`),
  flinching under blows, alive and dead looks. Everything that happens *to* a body lives here.
  Its melee half is **`core/CharacterMelee.cs`**: swings (`TrySwing`, hits checked each physics tick
  in `UpdateSwing`) and the arms, drawn in code while a bat, knife or bare fists are in hand (the
  body picture then has its hands removed; arm colours are read from the armed look).
- **`player/Player.cs`** and **`enemies/Enemy.cs`** build on `Character`. The player adds
  keyboard/mouse control, the camera and the takedowns (executions, stealth kills); the goon adds
  the AI (behaviours, sight, hearing, chasing, route-finding).
- **`weapons/`**: `WeaponData` is one weapon's settings, saved as a data file per weapon (`*.tres`);
  melee weapons (bat, knife, and the player's fists) tick `Melee`. `Gun` is the weapon in a hand and
  does the shooting. `Bullet` flies and hits. `WeaponPickup` is
  a gun on the floor or in the air (dropped or thrown).
- **`props/Stairs.cs`**: stairs. Stairs up are the floor's exit: they open once every goon is dead and
  call `Main.NextFloor`. Rules in [mechanics/floors.md](mechanics/floors.md).
- **`props/Door.cs`**: swinging doors, pushed by walking into them or by bullets (`Shove`); they block
  bullets and sight and slam goons. Rules, including how to place doors, in [mechanics/doors.md](mechanics/doors.md).
- **`effects/`**: particles, screen shake (`ShakeCamera.Shake`), hit stop (`HitStop.Freeze`), and
  `Effects.Spawn` for dropping an effect into the level.
- **`effects/GooCanvas.cs`**: all goo on a level's floor, painted once into one canvas that never
  clears (a hidden `SubViewport`), so splats cost nothing once they've landed. Splats are clipped
  to the floor they can reach without crossing a wall. Anything that bleeds calls the static
  `GooCanvas.Splat` / `Spray` / `Bleed`; `Main` builds one per level, like the level grid.
- **`audio/`**: sounds. A sound that should vary is an `AudioStreamRandomizer` file (`*.tres`)
  listing its takes; it picks one and shifts the pitch a little on every play.
- **`core/Noise.cs`**: gunshots alert goons in earshot. **`core/LevelGrid.cs`** is the map goons
  route on: 8 px squares, closed where a goon's body (plus a margin) would touch a wall, furniture or
  the edge of the floor. Routes come from Godot's `AStarGrid2D` and are pulled straight wherever a
  goon fits all the way. It checks the squares on first use, once the walls are solid to physics queries.
- **`Main.cs`**: the top of the game: loads the current floor from its `Floors` list, carries your gun
  up the stairs (static fields outlive the scene reload), HUD, restart, camera limits, builds the level grid.
- **`debug/`**: testing switches (`DevSettings.Current`), active in editor runs only.

## Where new things go

- Affects both you and goons: `Character`. Goon-only: `Enemy`. Player-only: `Player`.
- A new gun or melee weapon: a new `WeaponData` file in `weapons/`, no code.
- A new visual effect: its own scene, spawned with `Effects.Spawn` into the level (never as a
  child of a character, or it moves and dies with them).
- A new floor: a level scene in `levels/` (floor, walls, props, doors, guns, goons, the player at the
  arrival stairs), added to Main's `Floors` in `main.tscn`.
- A new testing switch: a field in `DevSettings`, an `if` where it matters, and a line in
  `ActiveSummary()`.

## Conventions

- Scripts find each other through groups set in the scenes: `player`, `enemies`, `pickups`, `doors`.
- Physics layers (named in Project Settings): world, player, enemies, bullets, furniture, doors.
  `core/Layers.cs` mirrors them in code. Furniture blocks walking only; doors block bullets and
  sight only.
- Draw order, bottom to top: floor, goo, corpses and floor guns, furniture and doors (bodies and goo
  go under them), the living, walls (over everyone, so brushing a wall never looks like being stuck
  in it), bullets and effects. Set with each scene's z index.
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

# Architecture

How the code is put together, where new things go, and lessons learned the hard way.
Gameplay rules are in [mechanics/](mechanics/).

## The shape of the code

- **`core/Character.cs`** is the shared body of the player and every goon: walking legs, the
  weapon in the hand, one-hit death (with or without blood), knockdown, being held down (`Pinned`),
  flinching under blows, the lying layer (falling, knocked down, slumped against a wall, getting up,
  dead). Everything that happens *to* a body lives here.
  Its melee half is **`core/CharacterMelee.cs`**: swings (`TrySwing`, hits checked each physics tick
  in `UpdateSwing`).
  **`core/CharacterMotion.cs`** is the upper body's look, which never changes timing: which animation
  plays (walking in step with the legs or standing, per thing in hand, a goon's idle pose, and
  one-offs over that via `Act`: swings, throws, pickups, a hand on a door, finishers), plus small
  motions on top: leaning into a run and settling after a stop (with dust), a dip on pickups, a
  shoulder into doors, a throw's follow-through, a goon's startle when he spots you.
- **Animations** are generated, not drawn: **`tools/anim/`** (Python, no packages) builds every
  character's frames from body parts posed per frame, taking heads, hands and colours from the
  hand-drawn sprites in `art/anim/<name>/` (`<name>_armed`, `_unarmed`, `_legs`, `_dead*`). It writes
  `art/anim/<name>/<animation>.png` and one `SpriteFrames` per character, `art/anim/<name>.tres`,
  which is the character's `Look`. Poses and timings are in `tools/anim/anims.py`; rebuild with
  `python tools/anim/build.py`. A new outfit: its hand-drawn sprites in `art/` (or a recolour of an
  existing goon in `tools/anim/outfits.py`), its name in `build.py`. A melee weapon's animations are named after it (`bat_swing`), so a new one needs
  poses there too.
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
- **`effects/`**: particles, screen shake (`ShakeCamera.Shake(pixels)`), hit stop (`HitStop.Freeze`),
  `Effects.Spawn` for dropping an effect into the level, and spent casings (`Shell.Eject`, one per shot).
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
- **`ui/TapeMenu.cs`** (scene `ui/menu.tscn`, where the game starts): the main menu and the Esc menu,
  drawn in code like a VCR's on-screen display (VT323 and Silkscreen fonts in `ui/fonts/`, licences
  beside them). Settings are placeholders: they keep their values while the game runs, nothing more.
  It is also the loading screen: `TapeMenu.LoadScene` reads a scene (with every floor in it, and all
  the music) a few pieces a frame while the game waits paused under it, then switches. It only shows
  if that takes a moment (over 0.25 s), then stays at least 0.6 s. The first load of a run also draws
  every shader once, hidden under it, so no effect stutters the first time it shows up mid-play (the
  graphics driver builds a shader the first time it's drawn). Its pictures are in `ui/loading/`,
  one at random each time, filling the screen (cropped to fit). Make them 400 x 225, the game screen's
  size in pixels, so each picture pixel is one game pixel; a bigger one is shrunk on screen and loses
  its crisp pixels.
- **`core/Music.cs`**: all music, one player that outlives scene changes (menu song, floor songs, the
  clear song, skip, shuffle, loop, volume). Rules in [mechanics/music.md](mechanics/music.md).
- **`core/SaveGame.cs`**: saves as JSON files in `Documents/My Games/Rent Day/Saves` (the game's own
  storage on other systems). `Main.Begin` starts a run (a new game or a save); `Main.Checkpoint` is
  what a save holds. Rules in [mechanics/saves.md](mechanics/saves.md).
- **`Main.cs`**: the top of a run: loads the current floor from its `Floors` list, carries your gun
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
- A new shader: its path in `TapeMenu.Shaders` too, so the loading screen builds it up front (else it
  may stutter the first time it shows).

## Conventions

- Scripts find each other through groups set in the scenes: `player`, `enemies`, `pickups`, `doors`.
- Physics layers (named in Project Settings): world, player, enemies, bullets, furniture, doors.
  `core/Layers.cs` mirrors them in code. Furniture blocks walking only; doors block bullets and
  sight only.
- Draw order, bottom to top: floor, goo, corpses and floor guns, furniture and doors (bodies and goo
  go under them), the living, walls (over everyone, so brushing a wall never looks like being stuck
  in it), bullets and effects. Set with each scene's z index.
- Tuning values live in the Inspector (scene and data files), not in code.
- **Green belongs to the crosshair** (`ui/crosshair.png`, the mouse cursor in Project Settings): no green in
  levels (floors, walls, props, goons, goo), so the pointer always stands out.
- Every goon's defaults are in `enemies/enemy.tscn`; set a single goon's values in the level.
- **Change scenes with `TapeMenu.LoadScene`** (or `Main.Begin` for a run), never `ChangeSceneToFile`:
  that reads the scene on the spot and freezes the game. The scene it loaded stays in memory, so the
  stairs and restarts (which reload the same scene) never touch the disk.

## Icon and builds

- **Icon:** `icon.png` is the game's icon (window, taskbar); `icon.ico` is the exe's icon on Windows,
  with a simplified 16 px version for small views. Both are set in Project Settings → Application → Config.
- **A Windows build:** Project → Export → "Windows Desktop" (`export_presets.cfg`). It writes
  `RentDay.exe`, `RentDay.pck` and a `data_RentDay_windows_x86_64` folder; send all three together.
  From the command line, Godot's console exe with `--headless --export-release "Windows Desktop" <path to RentDay.exe>`
  does the same, but may not exit on its own after it's done.

## Lessons learned

- **Close a scene or data file in Godot before editing it outside Godot.** The editor keeps its
  own copy in memory and can save it over the outside edit; this has wiped files before. A tab
  in the background counts too: running the game saves open scenes. If in doubt, close Godot
  without saving and reopen.
- **C# changes need a build** before the editor or the game sees them.
- **Fast things skip thin things.** A bullet moves further each frame than a door is thick, so
  bullets check the whole stretch they fly, not just where they land.
- **The game starts from the main menu (`ui/menu.tscn`);** New game or Continue loads `main.tscn`.
  F6 runs just the open scene, for testing one piece (running `main.tscn` that way skips the menu).
- **Disk reads can stall the game.** A first read of a file off a slow drive (an SD card) took up
  to half a second, so anything loaded mid-play (songs) is read up front, on the loading screen.
- **Don't use Godot's background loading (`ResourceLoader.LoadThreadedRequest`).** In 4.7 it crashed
  the game outright (no error, no log) handing over a scene with C# in it, and Godot's tracker has
  open bugs about it. The loading screen reads on the main thread instead, a few pieces a frame.
- **Testing switches stay local:** git ignores local changes to `debug/dev_settings.tres`, so the
  repository always holds a normal game.
- **No other game's name anywhere in the repository** (code, comments, commits, docs).

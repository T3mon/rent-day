# Architecture

How the code is put together, where new things go, and lessons learned the hard way.
Gameplay rules are in [mechanics/](mechanics/).

## The shape of the code

- **`core/Character.cs`** is the shared body of the player and every enemy: walking legs, the
  weapon in the hand, one-hit death (with or without blood), knockdown, being held down (`Pinned`),
  flinching under blows, the lying layer (falling, knocked down, slumped against a wall, getting up,
  dead). Everything that happens *to* a body lives here.
  Its melee half is **`core/CharacterMelee.cs`**: swings (`TrySwing`, hits checked each physics tick
  in `UpdateSwing`).
  **`core/CharacterMotion.cs`** is the upper body's look, which never changes timing: which animation
  plays (walking in step with the legs, standing or looking around, per thing in hand, an enemy's idle pose, and
  one-offs over that via `Act`: swings, throws, pickups, a hand on a door, finishers), plus small
  motions on top: leaning into a run and settling after a stop (with dust), a dip on pickups, a
  shoulder into doors, a throw's follow-through, an enemy's startle when he spots you.
- **Animations** are generated, not drawn: **`tools/anim/`** (Python, no packages) builds every
  character's frames from body parts posed per frame, taking heads, hands and colours from the
  hand-drawn sprites in `art/anim/<name>/` (`<name>_armed`, `_unarmed`, `_legs`, `_dead*`). It writes
  `art/anim/<name>/<animation>.png` and one `SpriteFrames` per character, `art/anim/<name>.tres`,
  which is the character's `Look`. Poses and timings are in `tools/anim/anims.py`; rebuild with
  `python tools/anim/build.py`. A new outfit: its hand-drawn sprites in `art/` (or a recolour of an
  existing enemy in `tools/anim/outfits.py`), its name in `build.py`. A melee weapon's animations are named after it (`bat_swing`), so a new one needs
  poses there too.
- **Walking:** the legs (and the arms swinging in step) run a 12-frame walk cycle driven by distance
  moved, not time: one full cycle (left foot, then right) per `Stride` pixels, set per character
  (player 133: about 180 steps a minute at top speed, a running rhythm; enemies 80: a stroll on patrol,
  a run when chasing). Shorter strides look like scurrying. Goo footprints keep their own spacing (20 px).
- **`player/Player.cs`** and **`enemies/Enemy.cs`** build on `Character`. The player adds
  keyboard/mouse control, the camera and the takedowns (executions, stealth kills); the enemy adds
  the AI (behaviours, sight, hearing, chasing, route-finding).
- **`weapons/`**: `WeaponData` is one weapon's settings, saved as a data file per weapon (`*.tres`);
  melee weapons (bat, knife, and the player's fists) tick `Melee`. `Gun` is the weapon in a hand and
  does the shooting. `Bullet` flies and hits. `WeaponPickup` is
  a gun on the floor or in the air (dropped or thrown). On the floor a weapon is drawn the size it is
  in hand, with no outline, over its shadow. Its floor picture (`FloorLook`) is seen from the side, so
  a pistol shows its grip.
- **Props, walls and floors** are generated too: **`tools/props/build.py`** (Python with Pillow) draws
  every `props/<name>.png`, the wall tiles `levels/walls3.png` and the floor tiles `levels/floors3.png`;
  rebuild with `python tools/props/build.py` (or name some props: `... build.py sofa bed`).
  - The look: real shapes and materials (dark wood with grain, velvet, leather, porcelain, chrome) lit by
	the room's neon, a faint cyan on top and left edges and pink on bottom and right, glowing only where
	something gives off light (lamps, screens, LED strips). Glowing sprites are padded with see-through
	pixels, centred on the body, so swapping art never moves a prop.
  - What stands and what lies flat must read at a glance: furniture (`props/Prop.cs`, a `StaticBody2D`
	on the furniture layer with collision boxes) casts a shadow down-right on screen however it's
    turned, as long as its `Height` (3 low, like beds and tables; 4 counters and safes; 6 tall, like
    wardrobes, fridges and arcades). Rugs (`rug_*`, z 0: under the goo and everyone) and floor clutter
    (z 2: the keytar, pizza boxes, the duffel bag) cast none, and are walked over. Windows sit on the outer
    walls (z 5, no collision); the concrete pillar is on the world layer like a wall (stops bullets and sight).
  - Walls are drawn as seen cut through from above: a dark outline, the two faces, and the core between
    them, with a square post on every corner, junction and wall end. The sheet: 16 columns, one per
    neighbour mask (a wall north 1, east 2, south 4, west 8). Rows: 0 drywall (white board, yellow
    insulation, studs), 1 outer walls (brick), 2 the drop shadow, 3 concrete block, 4 white painted brick,
	5 timber. Each inner wall's material is picked by the floors beside it: block next to bathrooms and
	kitchens (tiled floors), drywall along hallways, timber next to wooden floors, painted brick
	otherwise; corners and junctions take what most of the walls running into them are. Changing a wall's
    material is just painting its cells with the same column on another row.
  - Floors: rows 0 and 1 the street and sidewalk; rows 2 and 3 sixteen room floors (oak, honey and cherry
    planks, parquet, red, purple, blue and teal carpet, pink and pool tiles, black and white checker, terracotta,
    cream stone, lavender lino, purple velvet, polished concrete). Every room gets its own, and rooms side
    by side never share one, so the floor plan reads by colour.
- **`props/Stairs.cs`**: stairs. Stairs up are the floor's exit: they open once every enemy is dead and
  call `Main.NextFloor`. Rules in [mechanics/floors.md](mechanics/floors.md).
- **`props/Door.cs`**: swinging doors, pushed by walking into them or by bullets (`Shove`); they block
  bullets and sight and slam enemies. Rules, including how to place doors, in [mechanics/doors.md](mechanics/doors.md).
- **`effects/`**: particles, screen shake (`ShakeCamera.Shake(pixels)`), hit stop (`HitStop.Freeze`), slow
  motion (`SlowMotion.Set`: game speed, sound speed and the music in one place; the player runs its meter),
  and its look: `slow_motion.gdshader` on a rect `Main` lays over the level at z 7 (over walls 5 and stairs 7);
  bullets and afterimages (`Afterimage.Leave`) sit at 8 and the player is lifted to 9 while slowed, so they keep their colours,
  `Effects.Spawn` for dropping an effect into the level, and spent casings (`Shell.Eject`, one per shot).
- **`effects/GooCanvas.cs`**: all goo on a level's floor, painted once into one canvas that never
  clears (a hidden `SubViewport`, redrawn only on frames where a splat lands), so splats cost nothing once they've landed. Splats are clipped
  to the floor they can reach without crossing a wall. Anything that bleeds calls the static
  `GooCanvas.Splat` / `Spray` / `Bleed`; `Main` builds one per level, like the level grid.
- **`audio/`**: sounds: effects as WAV (cheapest to play; Godot packs them small), music as MP3 (a WAV
  song would be ten times the size). A sound that should vary is an `AudioStreamRandomizer` file (`*.tres`)
  listing its takes; it picks one and shifts the pitch a little on every play.
- **`core/Noise.cs`**: gunshots alert enemies in earshot (the three with the shortest walk come).
  **`core/LevelGrid.cs`** is the map enemies
  route on: 8 px squares, closed where an enemy's body (plus a margin) would touch a wall, furniture or
  the edge of the floor. Routes come from Godot's `AStarGrid2D` and are pulled straight wherever an
  enemy fits all the way. It checks the squares on first use, once the walls are solid to physics queries
  (a 20 ms frame), and keeps them when the same floor restarts: walls and furniture never move.
  A route may cut across the corner of a closed square (the line was checked), so "closed" is not
  "stuck": an enemy only counts as wedged when his body actually overlaps something (`Wedged`), and then
  steps straight out (`StepOut`) with nothing stopping him before routing on.
- **`ui/Menu.cs`** (scene `ui/menu.tscn`, where the game starts): the main menu and the Esc menu,
  drawn in code like a VCR's on-screen display (VT323 and Silkscreen fonts in `ui/fonts/`, licences
  beside them). Settings are placeholders: they keep their values while the game runs, nothing more.
  It is also the loading screen (`ui/MenuLoading.cs`): `Menu.LoadScene` reads a scene (with every floor in it, and all
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
  up the stairs (static fields outlive the scene reload), HUD, restart, camera limits, builds the level grid, and fences the map's edge (on the furniture layer: nobody walks or is thrown off the map, bullets fly on).
- **`debug/`**: testing switches (`DevSettings.Current`) and the admin panel (`AdminPanel`, F1), in editor runs only.

## Where new things go

- Affects both you and enemies: `Character`. Enemy-only: `Enemy`. Player-only: `Player`.
- A new gun or melee weapon: a new `WeaponData` file in `weapons/`, no code.
- A new visual effect: its own scene, spawned with `Effects.Spawn` into the level (never as a
  child of a character, or it moves and dies with them).
- A new floor: a level scene in `levels/` (floor, walls, props, doors, guns, enemies, the player at the
  arrival stairs), added to Main's `Floors` in `main.tscn`. Keep a door's whole swing, doorways and the
  stairs clear of furniture, and leave enemies a way round it: a square of their route map closes
  when furniture comes within 10 px of its middle, so gaps under about 24 px are walls to them.
- A new prop: a drawing function and a line in `PROPS` in `tools/props/build.py`, then its scene in
  `props/` (copy one of the same kind).
- A new testing switch: a field in `DevSettings`, an `if` where it matters, a line in
  `ActiveSummary()`, and a switch in `AdminPanel`.
- A new shader: its path in `Menu.Shaders` (`ui/MenuLoading.cs`) too, so the loading screen builds it up front (else it
  may stutter the first time it shows).

## Conventions

- **Plain names** for code, files, art and docs: what the thing is in everyday game terms (`Enemy`,
  `Player`, `Menu`, `vignette`), never a theme word or nickname for it.
- Scripts find each other through groups set in the scenes: `player`, `enemies`, `pickups`, `doors`.
- Physics layers (named in Project Settings): world, player, enemies, bullets, furniture, doors.
  `core/Layers.cs` mirrors them in code. Furniture blocks walking only; doors block bullets and
  sight only.
- Draw order, bottom to top (z index): floor and rugs 0, goo 1, corpses, floor guns and clutter 2,
  furniture and doors 3 (bodies and goo go under them), the living 4, walls 5 (over everyone, so
  brushing a wall never looks like being stuck in it), sparks, smoke and flying goo 6, stairs and
  slow motion's tint 7, bullets and afterimages 8, and you while slowed 9. Set with each scene's
  z index, or in code where the thing is made.
- Tuning values live in the Inspector (scene and data files), not in code.
- **Green belongs to the crosshair** (`ui/crosshair.png`, the mouse cursor in Project Settings): no green in
  levels (floors, walls, props, enemies, goo), so the pointer always stands out.
- Every enemy's defaults are in `enemies/enemy.tscn`; set a single enemy's values in the level.
- **Change scenes with `Menu.LoadScene`** (or `Main.Begin` for a run), never `ChangeSceneToFile`:
  that reads the scene on the spot and freezes the game. The scene it loaded stays in memory, so the
  stairs and restarts (which reload the same scene) never touch the disk.

## Icon and builds

- **Icon:** `icon.png` is the game's icon (window, taskbar); `icon.ico` is the exe's icon on Windows,
  with a simplified 16 px version for small views. Both are set in Project Settings → Application → Config.
- **A Windows build:** Project → Export → "Windows Desktop" (`export_presets.cfg`). It writes
  `RentDay.exe`, `RentDay.pck` and a `data_RentDay_windows_x86_64` folder; send all three together.
  The editor's MCP plugin (`addons/godot_ai/`) is left out by the preset's exclude filter; the plugin
  takes its game helper out of the build itself.
  From the command line, Godot's console exe with `--headless --export-release "Windows Desktop" <path to RentDay.exe>`
  does the same, but may not exit on its own after it's done.

## Lessons learned

- **Close a scene or data file in Godot before editing it outside Godot.** The editor keeps its
  own copy in memory and can save it over the outside edit; this has wiped files before. A tab
  in the background counts too: running the game saves open scenes. If in doubt, close Godot
  without saving and reopen.
- **C# changes need a build** before the editor or the game sees them.
- **Renaming an exported field drops its saved values.** Scenes and data files store each value under
  the field's name; after a rename Godot quietly ignores the old name and uses the default. Rename it
  in the `.tscn` and `.tres` files too (with those files closed in Godot).
- **Let Godot finish importing before running the game** after rebuilding animations or replacing art.
  An import that overlapped a game start once failed silently: the new image was left as a `.tmp` in
  `.godot/imported`, yet Godot counted it as done and kept the old, shorter strip, so a 12-frame walk
  showed 4 blank frames (the player vanished walking with a bat). A forced reimport doesn't fix it,
  because Godot thinks it's current: send that image's `.ctex` and `.md5` in `.godot/imported` to the
  Recycle Bin and rescan.
- **A build that adds new animations: build, let Godot import, then build again.** The editor reloads a
  character's frames file (`art/anim/<name>.tres`) when it changes, but if the new strips aren't imported
  yet the reload fails and it keeps the old copy, which the next game start saves over the build (the
  new animations vanish, and a missing one left the gun hidden). The second build, once the strips are
  imported, reloads cleanly.
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

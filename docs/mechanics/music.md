# Music

- **Main menu:** `audio/music/menu.mp3`, looping.
- **A floor:** one floor song from `audio/music/floor/`. It keeps playing through deaths and restarts (R).
- **A new floor** (stairs, new game, loading a save) starts a new song: the next one in order, a random other one with **shuffle**, or the same one from the start with **loop**.
- **Floor cleared:** the floor song cuts out and `audio/music/win.mp3` plays (looping) until the next floor.
- When a floor song ends: the next one (shuffle: a random one; loop: the same again).
- **N** skips to the next floor song at any time, without pausing. "NOW PLAYING" shows in the top left when a floor song starts.
- **Esc menu → MUSIC:** on the left, every floor song: pick one to play it now. On the right, what's playing, back / pause / next, shuffle, loop, volume. The menu and clear songs aren't in the player.
- **Volume:** click a block of the bar to set it there (left of the first block: silent), or drag along it; left/right on the keyboard.
- Volume, shuffle and loop are remembered in `Documents/My Games/Rent Day/settings.json`.
- A new floor song: drop the mp3 in `audio/music/floor/`. Every mp3 there is a floor song, in name order; the name shown is the file name.
- The floor songs and the clear song are read on the loading screen and stay in memory (about 3 MB each), like the menu song, so N, a cleared floor and going back to the menu never wait on the disk.
- The music playing goes on through the loading screen: New game keeps the menu song until the floor starts.
- Dying is silent (the player has no death sound), so the music carries on.
- **Slow motion** (Alt) slows the game's sounds down, but the music keeps its speed.

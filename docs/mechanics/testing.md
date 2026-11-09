# Automatic tests

`bash test.sh` builds the game and checks it without opening a window, in under half a minute. In PowerShell, plain `bash` may open WSL instead: use `& "C:\Program Files\Git\bin\bash.exe" test.sh`.

The last lines tell you the result: `all passed` and `exit=0 error_lines=0` is a pass. Otherwise look for `FAIL`, `ERROR:` or `error CS` lines, which say what broke and where.

- **Warnings:** a compiler warning stops the build.
- **Files:** every scene and resource loads, and everything they need exists.
- **Rules:** animation frames lie inside their pictures, characters have every animation the code plays, weapons have their pictures and sound, every floor has one player, a Floor and enemies with looks, every shader is in the warm-up list, and the export leaves out the plugin, the graph cache and the tests.
- **Running:** the menu and each floor run for 10 game seconds with nobody playing. Any error logged fails the run.
- **Dash:** a bullet flies on through a dashing player, kills a standing one, and the dash waits before the next.

The tests don't check that the game plays right: if bullets stopped killing enemies, they would still pass. They use normal switch values in memory, and never write saves, settings or `debug/dev_settings.tres`.

They run by themselves before every `git push`, and a failure stops the push. `git push --no-verify` skips them. The hook is switched on once per copy of the project: `git config core.hooksPath .githooks`.

# Testing switches

Switches and tools for testing the game: in the admin panel (F1 while playing), or in `debug/dev_settings.tres`.

- They only work in runs started from the editor. Exported builds ignore them and have no admin panel.
- **Admin panel (F1):** pauses the game; F1 or Esc closes it. Switches flip live and are saved to `debug/dev_settings.tres`, so the next run starts with them too.
- Or set them in the Inspector, then press play and pick New game on the menu (or run `main.tscn` with F6 to skip the menu).
- The repository stores the normal (default) values. Changing them locally does not change the repository.
- Active switches are listed in the top-right corner while playing, so a cheat is never mistaken for a bug.

Available switches:
- **Enemies see you:** off makes enemies blind to you (they still patrol, wander and hear shots).
- **Enemies hear shots:** off makes your gunshots alert nobody.
- **Enemies shoot:** off lets enemies spot and aim at you but never fire.
- **Start weapon:** start the level holding a chosen gun.
- **Start floor:** New game starts on a later floor (1 = the first, as normal). Loading a save always goes to the save's floor.
- **Infinite ammo:** your guns never run dry.
- **God mode:** you can't die.
- **Infinite slow motion:** the slow-motion meter never runs dry.
- **Show enemy info:** draws what each enemy sees and where he is heading, and the noise radius of each shot.
- **Red goo:** all goo is plain red blood instead of mixed colours.

Tools, in the admin panel only:
- **Give:** any weapon in your hands, a gun with a full load.
- **Go to:** any floor, empty-handed.
- **Restart floor**, and **Kill all enemies** (to see a cleared floor).
- **Reset all to normal:** every switch back to its normal value, saved to the file too.

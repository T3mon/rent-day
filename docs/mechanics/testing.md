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

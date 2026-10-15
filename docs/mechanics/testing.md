# Testing switches

Switches for testing the game, in `debug/dev_settings.tres`.

- They only work in runs started from the editor. Exported builds ignore them.
- Set them in the Inspector, then press play.
- The repository stores the normal (default) values. Changing them locally does not change the repository.
- Active switches are listed in the top-right corner while playing, so a cheat is never mistaken for a bug.

Available switches:
- **Goons see you:** off makes goons blind to you (they still patrol, wander and hear shots).
- **Goons hear shots:** off makes your gunshots alert nobody.
- **Goons shoot:** off lets goons spot and aim at you but never fire.
- **Start weapon:** start the level holding a chosen gun.
- **Infinite ammo:** your guns never run dry.
- **Show goon info:** draws what each goon sees and where he is heading, and the noise radius of each shot.
- **Red goo:** all goo is plain red blood instead of mixed colours.

Planned: an in-game admin panel, tracked as issue #1.

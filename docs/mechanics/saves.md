# Saves

- **A save is a playthrough,** with a name. As many as you like; there are no slots to fill or overwrite.
- It's saved every time a floor starts (also on a restart): the floor you're on and the gun you came onto it with. Loading it starts that floor over, on purpose: floors are short, and there's no saving mid-floor.
- **New game** makes a new playthrough. It opens a page with a made-up two-word name (NEON TIGER, CHROME PANTHER...): keep it with START, type over it (letters, digits and spaces, up to 16, shown in capitals; Backspace deletes), or roll another with NEW NAME. With a gamepad, START keeps the made-up one. A new game never touches another playthrough.
- **Continue** (main menu) loads the newest save, the playthrough played last; the line shows its name and floor.
- **Load** (main menu, or LOAD GAME in the Esc menu): every playthrough, newest first, with its name, floor and when it was saved. Each line has a DELETE box (right arrow, or click it): it asks first, then removes the save and its backup. The playthrough being played can't be deleted from the Esc menu.
- New game, Continue and Load go through the loading screen (see [architecture](../architecture.md)); it only shows if loading takes a moment.
- **Files:** `Documents/My Games/Rent Day/Saves/save_N.json`, one per playthrough, on a PC: readable in Notepad, easy to find and share (the game's own storage on other systems). The name is inside the file, so renaming the file changes nothing.
- **Backups:** each save keeps the one before it as `save_N.json.bak`. A save that gets damaged loads that copy instead. A save from a newer version of the game, or one whose copy is broken too, is left out of the list.

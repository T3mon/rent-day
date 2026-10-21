# Saves

- A save is a checkpoint: the floor you're on and the gun you came onto it with. Loading one starts that floor over.
- **Autosave:** written every time a floor starts (also on a restart), always over the same file.
- **Quick save:** F5 saves, F9 loads. One quick save, overwritten each time.
  In a run started from the Godot editor, F9 is also Godot's own key for suspending the game: the save loads but the game sits frozen until you press resume in the editor (or change that shortcut in Editor Settings). Exported builds aren't affected.
- **Save game** (Esc menu): a new numbered save every time (SAVE 1, SAVE 2...).
- **Load** (main menu or Esc menu): every save, newest first. **Continue** on the main menu loads the newest.
- **New game** starts the first floor straight away, no question asked.
- New game, Continue, Load and quick load go through the loading screen (see [architecture](../architecture.md)); it only shows if loading takes a moment.
- Files: `Documents/My Games/Rent Day/Saves/*.json` on a PC, readable in Notepad (the game's own storage on other systems). A save from a newer version of the game, or a broken file, is left out of the list.

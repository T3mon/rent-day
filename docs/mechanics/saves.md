# Saves

- A save is a checkpoint: the floor you're on and the gun you came onto it with. Loading one starts that floor over.
- **Autosave:** written every time a floor starts (also on a restart), always over the same file.
- **Quick save:** F5 saves, F9 loads. One quick save, overwritten each time.
- **Save game** (Esc menu): a new numbered save every time (SAVE 1, SAVE 2...).
- **Load** (main menu or Esc menu): every save, newest first. **Continue** on the main menu loads the newest.
- **New game** starts the first floor straight away, no question asked.
- Files: `Documents/My Games/Rent Day/Saves/*.json` on a PC, readable in Notepad (the game's own storage on other systems). A save from a newer version of the game, or a broken file, is left out of the list.

# Settings

The Settings page, on the main menu and the Esc menu. A change applies at once and is saved; the game starts with them.

- **File:** `Documents/My Games/Rent Day/settings.json` on a PC (next to the Saves folder; the game's own storage on other systems), readable in Notepad. A broken file, or a key the game can't read, falls back to the normal values.
- Read before anything else when the game starts (`core/Settings.cs` is an autoload).

## Display

- **Screen mode:** WINDOWED; FULLSCREEN WINDOW, a borderless window covering the screen (switching to other programs is quick); or FULLSCREEN, exclusive (the screen is the game's alone). Back to WINDOWED goes back to the resolution below.
- Covering the screen (either fullscreen), the mouse stays inside it, so a click at the edge mid-fight never lands on another window, and the game goes silent while another window has the focus. Windowed, neither: the mouse is free and the sound plays on.
- **Resolution:** the window's size, centred on the screen: 1024 x 576 up to 3840 x 2160, all 16:9, the ones that fit your screen. Covering the screen, the game uses the screen's own size; the line shows it, greyed out.
- **VSync:** on or off.
- The 400 x 225 picture stretches to fill the window or the screen, so a 16:9 screen has no black frame (a screen of another shape gets thin bars on two sides). Stretched by a fraction (4.8x on 1920 x 1080), some art pixels come out one screen pixel wider than others (Project Settings: stretch scale mode fractional, aspect keep).
- In a run from the Godot editor, with the game inside the editor's Game tab, the editor sizes it: screen mode and resolution are locked there (the menu says why). To try them from the editor, untick **Embed Game on Next Play** in the drop-down at the top right of the Game tab.

## Audio

- **Master, Music, Effects:** 0 (silent) to 10.
- **Bars** (volumes, screen shake) work the same everywhere, on this page and the MUSIC page: click a block to set it there (left of the first block: 0), drag along it, or left and right. Enter doesn't change a bar.
- Music plays on the Music bus, every sound effect on the Effects bus, both under Master. A new sound needs nothing: any 2D sound left on Master is moved to Effects when it appears.
- **Music** is the same volume as the Esc menu's MUSIC page. Shuffle and loop are kept in the same file.

## Controls

- Every action has a main and a second key box. Pick one (click, or Enter), then press the new key or mouse button. **Esc** cancels.
- A key another action already uses asks first: yes moves it here, and the other action loses it.
- **Pause stays on Esc**, so the menus can always be opened and left.
- Switching to another window (alt-tab) during a floor pauses the game the way Esc does.
- **Reset to default keys** (last row): every key back to the Input Map in Project Settings.
- Only keys and mouse buttons change here; gamepad buttons keep theirs. The mouse wheel isn't a key.
- Only keys moved off their defaults are saved, so a key added to the game later works for everyone.

## Accessibility

- **Screen shake:** 0 (off) to 10 (full), scaling every shake.
- **Flashes:** off hides the muzzle flash, and calms the menus: no rolling tracking band, still static on the main menu, the title never slips, and the loading screen's FF doesn't blink.

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using static RentDay.Style;

namespace RentDay;

// The main menu and the Esc menu, drawn like a VCR's on-screen display: PLAY or PAUSE in the
// corner, a running timecode, scanlines and a rolling tracking band, the picked line inverted.
// Mouse, keyboard and gamepad all work. Paused = true makes it the Esc menu: it sits over the
// frozen floor, and the game stays paused until it closes. It is also the loading screen (MenuLoading.cs).
public partial class Menu : Control
{
	public const string ScenePath = "res://ui/menu.tscn";

	[Export] public bool Paused;

	private const int W = 400, H = 225;

	private enum View { Main, Pause, Load, Settings, Confirm, Music, Loading }

	private static readonly string[] PauseItems = { "RESUME", "SAVE GAME", "LOAD GAME", "MUSIC", "SETTINGS", "QUIT TO MENU" };
	private static readonly string[] MusicRows = { "PLAYER", "SHUFFLE", "LOOP", "VOLUME", "BACK" }; // PLAYER: back, pause, next

	// The Settings page: each row's value is in Settings.Values, under its key.
	private record Row(string Label, string Key, string Kind, params string[] Choices);
	private static readonly string[] Tabs = { "DISPLAY", "AUDIO", "CONTROLS", "ACCESS" };
	private const int ControlsTab = 2;
	private static readonly Row[][] Rows =
	{
		new Row[] { new("SCREEN MODE", "screen", "choice", "WINDOWED", "FULLSCREEN WINDOW", "FULLSCREEN"), new("RESOLUTION", "resolution", "choice", Settings.Resolutions), new("VSYNC", "vsync", "toggle") },
		new Row[] { new("MASTER", "master", "bar"), new("MUSIC", "music", "bar"), new("EFFECTS", "sfx", "bar") },
		null, // the controls tab lists Keys instead
		new Row[] { new("SCREEN SHAKE", "shake", "bar"), new("FLASHES", "flash", "toggle") },
	};

	// The controls tab: actions in groups, each with its main and second key, read from the input map. Picking
	// a key box waits for the new key (_rebinding). PAUSE stays on Esc; the row with no action resets every key.
	private static readonly (string Group, string Label, string Action)[] Keys =
	{
		("COMBAT", null, null), (null, "SHOOT, SWING", "shoot"), (null, "GRAB, THROW", "grab"), (null, "EXECUTE", "execute"), (null, "HUMAN SHIELD", "shield"),
		(null, "SLOW MOTION", "slow_motion"),
		("MOVE", null, null), (null, "MOVE UP", "move_up"), (null, "MOVE DOWN", "move_down"), (null, "MOVE LEFT", "move_left"),
		(null, "MOVE RIGHT", "move_right"), (null, "LOOK AHEAD", "look"),
		("GAME", null, null), (null, "RESTART FLOOR", "restart"), (null, "PAUSE", "ui_cancel"), (null, "QUICK SAVE", "quick_save"),
		(null, "QUICK LOAD", "quick_load"), (null, "NEXT SONG", "next_song"), (null, "RESET TO DEFAULT KEYS", ""),
	};
	private static readonly (string Label, string Action)[] KeyRows = Keys.Where(k => k.Group == null).Select(k => (k.Label, k.Action)).ToArray();

	private View _view;
	private readonly Stack<(View View, int Sel)> _back = new();
	private int _sel, _tab, _col, _scroll; // _sel -1 = the settings tabs
	private bool _rebinding; // the picked key box is waiting for its new key
	private string _askLine, _askSub;
	private Action _onYes;
	private string _toast;
	private ulong _toastUntil, _opened;
	private List<SaveGame.Entry> _saves = new();
	private readonly List<(Rect2 Rect, int Index, int Tab, int Col)> _hits = new(); // rebuilt every draw

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always; // keeps running while the game is paused
		_opened = Time.GetTicksMsec();
		if (_loading != null)
		{
			ReadyLoading();
			return;
		}
		_view = Paused ? View.Pause : View.Main;
		_saves = SaveGame.All();
		if (!Paused)
			Music.Menu(this);
		if (Paused) // the frozen floor behind, washed out
		{
			var wash = new ColorRect { ShowBehindParent = true, MouseFilter = MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://ui/pause.gdshader") } };
			wash.SetAnchorsPreset(LayoutPreset.FullRect);
			AddChild(wash);
		}
	}

	public override void _Process(double delta)
	{
		QueueRedraw();
		if (_loading != null)
			PollLoading();
	}

	// ---------------------------------------------------------------- what's on each page
	private string[] MainItems() => _saves.Count > 0
		? new[] { "CONTINUE", "NEW GAME", "LOAD", "SETTINGS", "QUIT" }
		: new[] { "NEW GAME", "LOAD", "SETTINGS", "QUIT" };

	private int Count() => _view switch
	{
		View.Main => MainItems().Length,
		View.Pause => PauseItems.Length,
		View.Load => _saves.Count + 1,
		View.Settings => (_tab == ControlsTab ? KeyRows.Length : Rows[_tab].Length) + 1,
		View.Music => Music.Songs.Length + MusicRows.Length, // the song list, then the player's rows
		_ => 2,
	};

	private bool IsBack(int i) => _view is View.Load or View.Settings or View.Music && i == Count() - 1;

	private void Activate()
	{
		if (_sel < 0)
			return;
		if (IsBack(_sel))
		{
			Back();
			return;
		}
		switch (_view)
		{
			case View.Main or View.Pause:
				switch (_view == View.Main ? MainItems()[_sel] : PauseItems[_sel])
				{
					case "CONTINUE": Main.Begin(GetTree(), _saves[0].Data); break;
					case "NEW GAME": Main.Begin(GetTree()); break;
					case "LOAD" or "LOAD GAME": _saves = SaveGame.All(); Go(View.Load); break;
					case "SETTINGS": Go(View.Settings); break;
					case "MUSIC":
						Go(View.Music);
						_sel = Math.Max(Music.Current, 0); // starts on the song playing
						break;
					case "QUIT": GetTree().Quit(); break;
					case "RESUME": Resume(); break;
					case "SAVE GAME":
						string slot = SaveGame.NextSlot();
						Toast(SaveGame.Write(slot, Main.Checkpoint()) ? "SAVED AS " + SaveGame.Title(slot) : "COULD NOT SAVE");
						break;
					case "QUIT TO MENU":
						Ask("EJECT THE TAPE?", "THE FLOOR STARTS OVER NEXT TIME", () => LoadScene(GetTree(), ScenePath));
						break;
				}
				break;
			case View.Load:
				Main.Begin(GetTree(), _saves[_sel].Data);
				break;
			case View.Settings:
				if (_tab != ControlsTab)
				{
					if (Rows[_tab][_sel].Kind != "bar") // a bar: left and right, or the mouse (SetBarAt)
						Change(Rows[_tab][_sel], 1);
				}
				else if (KeyRows[_sel].Action == "")
					Ask("RESET EVERY KEY?", "BACK TO THE KEYS THE GAME CAME WITH", () => { Settings.ResetKeys(); Back(); });
				else if (KeyRows[_sel].Action == "ui_cancel")
					Toast("PAUSE IS ALWAYS ESC");
				else
					_rebinding = true;
				break;
			case View.Music:
				if (_sel < Music.Songs.Length)
					Music.PlaySong(_sel);
				else
					MusicRow(_sel - Music.Songs.Length, 1, enter: true);
				break;
			case View.Confirm:
				if (_sel == 1)
					_onYes();
				else
					Back();
				break;
		}
	}

	private void Change(Row row, int dir)
	{
		if (Settings.Locked(row.Key) is string why)
		{
			Toast(why);
			return;
		}
		int v = Settings.Values[row.Key];
		Settings.Set(row.Key, row.Kind switch
		{
			"toggle" => v == 1 ? 0 : 1,
			"choice" => (Math.Min(v, row.Choices.Length - 1) + dir + row.Choices.Length) % row.Choices.Length,
			_ => Math.Clamp(v + dir, 0, 10),
		});
	}

	// The picked key box gets `key`. If another action has it, that's asked first, and it loses it there.
	private void Rebind(InputEvent key)
	{
		_rebinding = false;
		string action = KeyRows[_sel].Action;
		int slot = _col;
		var (label, other) = KeyRows.FirstOrDefault(row => row.Action != action && row.Action != "" && Settings.KeysOf(row.Action).Any(k => Settings.Same(k, key)));
		if (other == null)
			Settings.Bind(action, slot, key);
		else
			Ask($"{KeyLabel(key)} IS ALREADY {label}", "USE IT HERE INSTEAD?", () => { Settings.Bind(action, slot, key, other); Back(); });
	}

	private void Go(View view)
	{
		_back.Push((_view, _sel));
		_view = view;
		_sel = 0;
		_scroll = 0;
	}

	private void Back()
	{
		if (_back.Count > 0)
			(_view, _sel) = _back.Pop();
		else if (Paused)
			Resume();
	}

	private void Ask(string line, string sub, Action yes)
	{
		(_askLine, _askSub, _onYes) = (line, sub, yes);
		Go(View.Confirm);
	}

	private void Toast(string text)
	{
		_toast = text;
		_toastUntil = Time.GetTicksMsec() + 1700;
	}

	private void Resume()
	{
		GetTree().Paused = false;
		QueueFree();
	}

	// ---------------------------------------------------------------- input
	public override void _UnhandledInput(InputEvent e)
	{
		if (_rebinding) // the next key goes in the key box; Esc changes nothing
		{
			if (e.IsActionPressed("ui_cancel"))
				_rebinding = false;
			else if (e is InputEventKey { Pressed: true, Echo: false } key && key.Keycode != Key.None)
				Rebind(new InputEventKey { Keycode = key.Keycode });
			GetViewport().SetInputAsHandled();
			return;
		}
		if (e is not (InputEventKey or InputEventJoypadButton or InputEventJoypadMotion))
			return; // the mouse comes through _GuiInput
		Viewport viewport = GetViewport(); // picking New game or Load takes this menu out of the tree
		bool settings = _view == View.Settings;
		int n = Count();
		if (Pressed(e, "ui_up", "move_up"))
			_sel = settings ? Math.Max(-1, _sel - 1) : (_sel - 1 + n) % n;
		else if (Pressed(e, "ui_down", "move_down"))
			_sel = settings ? Math.Min(n - 1, _sel + 1) : (_sel + 1) % n;
		else if (Pressed(e, "ui_left", "move_left"))
			Sideways(-1);
		else if (Pressed(e, "ui_right", "move_right"))
			Sideways(1);
		else if (e.IsActionPressed("ui_accept"))
			Activate();
		else if (e.IsActionPressed("ui_cancel"))
			Back();
		else
			return;
		viewport.SetInputAsHandled();
	}

	private static bool Pressed(InputEvent e, string action, string alsoAction) =>
		e.IsActionPressed(action, allowEcho: true) || e.IsActionPressed(alsoAction, allowEcho: true);

	// Left and right: the tab, a setting's value, the key box, or yes and no.
	private void Sideways(int dir)
	{
		if (_view == View.Confirm)
			_sel = 1 - _sel;
		else if (_view == View.Music && !IsBack(_sel) && _sel >= Music.Songs.Length)
			MusicRow(_sel - Music.Songs.Length, dir, enter: false);
		else if (_view != View.Settings || IsBack(_sel))
			return;
		else if (_sel == -1)
			_tab = (_tab + dir + Tabs.Length) % Tabs.Length;
		else if (_tab == ControlsTab)
			_col = dir > 0 ? 1 : 0;
		else
			Change(Rows[_tab][_sel], dir);
	}

	public override void _GuiInput(InputEvent e)
	{
		if (_rebinding) // a mouse button goes in the key box (not the wheel)
		{
			if (e is InputEventMouseButton { Pressed: true } click && click.ButtonIndex is not (MouseButton.WheelUp or MouseButton.WheelDown or MouseButton.WheelLeft or MouseButton.WheelRight))
				Rebind(new InputEventMouseButton { ButtonIndex = click.ButtonIndex });
			if (e is InputEventMouseButton)
				AcceptEvent();
			return;
		}
		if (e is InputEventMouseMotion motion)
		{
			if (HitAt(motion.Position) is { } hover)
			{
				_sel = hover.Tab >= 0 ? -1 : hover.Index;
				if (hover.Col >= 0)
					_col = hover.Col;
				if (motion.ButtonMask.HasFlag(MouseButtonMask.Left)) // dragging along a bar
					SetBarAt(hover.Index, motion.Position.X);
			}
			return;
		}
		if (e is not InputEventMouseButton { Pressed: true } button)
			return;
		AcceptEvent(); // before acting: picking New game or Load takes this menu out of the tree
		if (button.ButtonIndex == MouseButton.Right)
			Back();
		else if (button.ButtonIndex is MouseButton.WheelUp or MouseButton.WheelDown && _view == View.Settings && _tab == ControlsTab)
			_sel = Math.Clamp(Math.Max(_sel, 0) + (button.ButtonIndex == MouseButton.WheelDown ? 1 : -1), 0, Count() - 1);
		else if (button.ButtonIndex == MouseButton.Left && HitAt(button.Position) is { } hit)
		{
			if (hit.Tab >= 0)
			{
				_tab = hit.Tab;
				_sel = -1;
				return;
			}
			_sel = hit.Index;
			if (hit.Col >= 0)
				_col = hit.Col;
			if (BarOn(hit.Index) != null)
				SetBarAt(hit.Index, button.Position.X);
			else if (_view == View.Music && hit.Index == Music.Songs.Length) // the player row: back, pause or next by where you click
				MusicRow(0, button.Position.X < 260 ? -1 : button.Position.X < 300 ? 0 : 1, enter: false);
			else
				Activate();
		}
	}

	private (Rect2 Rect, int Index, int Tab, int Col)? HitAt(Vector2 at)
	{
		foreach (var hit in _hits)
		{
			if (hit.Rect.HasPoint(at))
				return hit;
		}
		return null;
	}

	// ---------------------------------------------------------------- drawing
	public override void _Draw()
	{
		_hits.Clear();
		if (_view == View.Loading && _shownAt == 0)
			return; // a quick load never shows the loading screen
		ulong now = Time.GetTicksMsec();
		if (!Paused)
			Backdrop(now);
		switch (_view)
		{
			case View.Main: DrawMain(now); break;
			case View.Pause: DrawPause(now); break;
			case View.Load: DrawLoad(now); break;
			case View.Settings: DrawSettings(now); break;
			case View.Music: DrawMusic(now); break;
			case View.Loading: DrawLoading(now); break;
			default: DrawConfirm(now); break;
		}
		Scanlines(now);
		if (_toast != null && now < _toastUntil)
		{
			float w = Width(_toast);
			R(200 - w / 2 - 6, 176, w + 12, 15, new Color(0f, 0f, 0f, 0.75f));
			Osd(_toast, 200, 188, Colors.White, HorizontalAlignment.Center);
		}
	}

	// The main menu's ground: dark, a purple glow from the top, static.
	private void Backdrop(ulong now)
	{
		R(0, 0, W, H, Ink);
		for (int y = 0; y < H; y += 3)
			R(0, y, W, 3, new Color(60 / 255f, 14 / 255f, 120 / 255f, 0.55f * (1f - (float)y / H)));
		uint s = (uint)(Settings.Flashes ? now / 50 : 0) * 9301u + 49297u; // with flashes off, still
		for (uint k = 0; k < 280; k++)
		{
			s = Hash(s + k);
			float v = (90 + s % 130) / 255f;
			R(s % W, (s >> 9) % H, 1, 1, new Color(v, v, Math.Min(v + 0.1f, 1f), 0.35f));
		}
	}

	// Over everything: a tracking band rolling down (held still on the settings, to keep them easy
	// to read) and scanlines.
	private void Scanlines(ulong now)
	{
		if (_view != View.Settings && Settings.Flashes)
		{
			int y = (int)(now / 38 % (H + 40)) - 20;
			for (int k = 0; k < 7; k++)
				R(0, y + k, W, 1, new Color(220 / 255f, 220 / 255f, 1f, 0.05f + 0.04f * Mathf.Sin(k + now / 90f)));
			uint s = (uint)(now / 60);
			for (uint k = 0; k < 26; k++)
			{
				s = Hash(s + k * 7);
				R(s % W, y + (s >> 8) % 7, 2 + (s >> 4) % 10, 1, new Color(1f, 1f, 1f, 0.35f));
			}
		}
		for (int y = 0; y < H; y += 2)
			R(0, y, W, 1, new Color(0f, 0f, 0f, 0.26f));
	}

	// PLAY or PAUSE in the top left, the timecode in the bottom right (on the Esc menu: time on this floor).
	private void Corner(ulong now)
	{
		float w = Osd(Paused ? "PAUSE" : "PLAY", 16, 26, Colors.White);
		if (Paused)
			PauseSign(16 + w + 7, 15, Colors.White);
		else
			Tri(16 + w + 7, 15, Colors.White);
		ulong sec = (Paused ? _opened - Main.FloorStarted : now - _opened) / 1000;
		Osd($"SP {sec / 3600}:{sec / 60 % 60:00}:{sec % 60:00}", W - 16, H - 12, Colors.White, HorizontalAlignment.Right);
	}

	// ---------------------------------------------------------------- the pieces every page is made of
	// A page under the corner: its dark band and its title.
	private void Page(ulong now, string title)
	{
		Corner(now);
		Panel(34, 186);
		Osd(title, 34, 56, Colors.White);
	}

	// The dark band a page or a question sits on.
	private void Panel(float top, float height) => R(0, top, W, height, new Color(0f, 0f, 0f, 0.6f));

	// One line of a list; the picked one inverted, like a VCR menu: a white bar with a pointer. A `width`
	// sets the bar across the page; 0 fits it to the label. `off`: the label's colour when not picked.
	private void Line(string label, int index, float x, float y, float width = 0, Color? off = null)
	{
		float box = width > 0 ? width : Width(label) + 26;
		if (_sel == index)
		{
			R(x - 6, y - 12, box, 15, Colors.White);
			Tri(x - 1, y - 10, Ink);
			Text(label, x + 12, y, OsdFont, 16, Ink);
		}
		else
			Osd(label, x + 12, y, off ?? Soft);
		_hits.Add((new Rect2(x - 6, y - 12, width > 0 ? width : Math.Max(240, box), 15), index, -1, -1));
	}

	// A value at the right end of its line (ON, OFF, 1600 X 900...); on the picked line between arrows: left
	// and right change it. A locked one is greyed out, with no arrows.
	private void Value(string text, float y, bool on, bool locked = false)
	{
		const float right = 354;
		Color ink = on ? Ink : locked ? Dim : Colors.White;
		float w = on ? Text(text, right, y, OsdFont, 16, ink, HorizontalAlignment.Right) : Osd(text, right, y, ink, HorizontalAlignment.Right);
		if (!on || locked)
			return;
		Tri(right - w - 9, y - 9, ink, left: true, size: 5);
		Tri(right + 5, y - 9, ink, size: 5);
	}

	// A value from 0 to 10 as ten blocks, the same on every page (volumes, screen shake). Left and right
	// step it; a click or drag sets the block under the mouse (left of the first block: 0).
	private const float BarLeft = 284, BarStep = 8;

	private void Bar(float y, int value, bool on)
	{
		for (int k = 0; k < 10; k++)
			R(BarLeft + k * BarStep, y - 10, BarStep - 2, 9, k < value ? (on ? Ink : Colors.White) : on ? new Color("c9c3d8") : Track);
	}

	// What the bar on row `index` of this page sets; null when that row has none.
	private Action<int> BarOn(int index)
	{
		if (_view == View.Music && index == Music.Songs.Length + Array.IndexOf(MusicRows, "VOLUME"))
			return Music.SetVolume;
		if (_view == View.Settings && _tab != ControlsTab && index >= 0 && index < Rows[_tab].Length && Rows[_tab][index].Kind == "bar")
		{
			string key = Rows[_tab][index].Key;
			return value => Settings.Set(key, value);
		}
		return null;
	}

	private void SetBarAt(int index, float x)
	{
		if (x >= BarLeft - 6 && BarOn(index) is { } set)
			set(Math.Clamp((int)Mathf.Floor((x - BarLeft) / BarStep) + 1, 0, 10));
	}

	// A thin bar filling up from the left: how much is done, 0 to 1 (the loading screen, a song).
	private void Progress(float x, float y, float width, float done)
	{
		R(x, y, width, 3, Track);
		R(x, y, Mathf.Round(width * done), 3, Colors.White);
	}

	// Scrolls a list of `count` lines, `shown` at a time, to keep the picked line in view.
	private void KeepInView(int count, int shown)
	{
		if (_sel >= 0 && _sel < count)
			_scroll = Math.Clamp(_scroll, _sel - shown + 1, _sel);
		_scroll = Math.Clamp(_scroll, 0, Math.Max(0, count - shown));
	}

	// Where you are in a list longer than its view (counted in lines or pixels): a thin track and a thumb.
	private void Scrollbar(float x, int top, int height, int total, int shown, int scroll)
	{
		if (total <= shown)
			return;
		int thumb = Math.Max(10, height * shown / total);
		R(x + 1, top, 1, height, Track);
		R(x, top + (height - thumb) * scroll / (total - shown), 3, thumb, Colors.White);
	}

	// ---------------------------------------------------------------- the pages
	private void DrawMain(ulong now)
	{
		Corner(now);
		int jx = Settings.Flashes && now / 2700 % 3 == 0 && now % 2700 < 140 ? 3 : 0; // now and then the title slips
		Osd("RENT DAY", 34 + jx, 74, Colors.White, font: TitleFont, size: 32, split: 2);
		string[] items = MainItems();
		float y = 106;
		for (int i = 0; i < items.Length; i++)
		{
			if (items[i] == "QUIT")
				y += 6;
			Line(items[i] == "CONTINUE" ? $"CONTINUE  FLOOR {_saves[0].Data.Floor}" : items[i], i, 34, y);
			y += 17;
		}
	}

	private void DrawPause(ulong now)
	{
		Corner(now);
		Panel(76, 108);
		float y = 100;
		for (int i = 0; i < PauseItems.Length; i++)
		{
			if (i == PauseItems.Length - 1)
				y += 6;
			Line(PauseItems[i], i, 34, y);
			y += 17;
		}
	}

	private void DrawLoad(ulong now)
	{
		const int shown = 5, top = 80;
		Page(now, "TAPE INDEX");
		if (_saves.Count == 0)
			Osd("NO SAVES YET", 46, top, Dim);
		KeepInView(_saves.Count, shown);
		for (int i = _scroll; i < Math.Min(_saves.Count, _scroll + shown); i++)
		{
			SaveGame.Entry save = _saves[i];
			Line($"{i + 1:00}  {SaveGame.Title(save.Name),-10} FL.{save.Data.Floor:00}   {save.Data.Saved:MM/dd HH:mm}", i, 34, top + (i - _scroll) * 18);
		}
		Scrollbar(372, top - 12, shown * 18 - 3, _saves.Count, shown, _scroll);
		Line("BACK", _saves.Count, 34, top + Math.Min(Math.Max(_saves.Count, 1), shown) * 18 + 10);
	}

	private void DrawSettings(ulong now)
	{
		Page(now, "SETTINGS");
		float x = 34;
		for (int i = 0; i < Tabs.Length; i++)
		{
			float w = Width(Tabs[i]);
			if (_tab == i)
			{
				R(x - 4, 64, w + 8, 15, Colors.White);
				Text(Tabs[i], x, 76, OsdFont, 16, Ink);
			}
			else
				Osd(Tabs[i], x, 76, Dim);
			_hits.Add((new Rect2(x - 4, 64, w + 8, 15), -1, i, -1));
			x += w + 16;
		}
		if (_sel == -1)
			Tri(22, 67, Colors.White);
		R(30, 84, 340, 1, Faint);
		if (_tab == ControlsTab)
		{
			DrawKeys();
			return;
		}
		Row[] rows = Rows[_tab];
		for (int i = 0; i < rows.Length; i++)
		{
			float y = 100 + i * 15;
			int v = Settings.Values[rows[i].Key];
			Line(rows[i].Label, i, 34, y, 344);
			if (rows[i].Kind == "bar")
				Bar(y, v, _sel == i);
			else
				Value(Settings.Shown(rows[i].Key) ?? (rows[i].Kind == "toggle" ? (v == 1 ? "ON" : "OFF") : rows[i].Choices[Math.Min(v, rows[i].Choices.Length - 1)]),
					y, _sel == i, Settings.Locked(rows[i].Key) != null);
		}
		Line("BACK", rows.Length, 34, 100 + rows.Length * 15 + 5, 344);
	}

	// The MUSIC page: the floor song playing, and back / pause / next, shuffle, loop, volume.
	private static void MusicRow(int row, int dir, bool enter)
	{
		switch (MusicRows[row])
		{
			case "PLAYER":
				if (enter || dir == 0)
					Music.TogglePause();
				else
					Music.Skip(dir);
				break;
			case "SHUFFLE": Music.SetShuffle(!Music.Shuffle); break;
			case "LOOP": Music.SetLoop(!Music.Loop); break;
			case "VOLUME":
				if (!enter) // left and right; a click or drag sets it on the bar (SetBarAt)
					Music.SetVolume(Music.Volume + dir);
				break;
		}
	}

	private void DrawMusic(ulong now)
	{
		const int shown = 8, top = 80;
		int songs = Music.Songs.Length;
		Page(now, "MUSIC");

		// left: the floor songs; pick one to play it
		KeepInView(songs, shown);
		for (int i = _scroll; i < Math.Min(songs, _scroll + shown); i++)
		{
			float y = top + (i - _scroll) * 15;
			bool on = _sel == i, playing = Music.OnFloorSong && Music.Current == i;
			Line(Music.Songs[i].Replace('_', ' ').ToUpperInvariant(), i, 34, y, 172, playing ? Colors.White : Soft);
			if (playing) // what's playing: a little bar, or a pause sign
			{
				Color mark = on ? Ink : Magenta;
				if (Music.Paused)
					PauseSign(186, y - 10, mark);
				else
				{
					for (int b = 0; b < 3; b++)
						R(184 + b * 3, y - 2 - (int)(3 + 3 * Mathf.Sin(now / 120f + b * 2)), 2, (int)(3 + 3 * Mathf.Sin(now / 120f + b * 2)) + 1, mark);
				}
			}
		}
		Scrollbar(202, top - 12, shown * 15, songs, shown, _scroll);
		Text("N  NEXT SONG, ANY TIME", 34, 212, SmallFont, 8, Dim);

		// right: what's playing, then the controls
		const float x = 214, right = 370;
		if (Music.OnFloorSong)
		{
			Text("NOW PLAYING", x, 58, SmallFont, 8, Dim);
			Osd(Music.Title, x, 76, Colors.White);
			float len = Mathf.Max(Music.Length, 1f), at = Mathf.Min(Music.Position, len);
			Progress(x, 84, right - x, at / len);
			Text(Clock(at), x, 99, SmallFont, 8, Dim);
			Text(Clock(len), right, 99, SmallFont, 8, Dim, HorizontalAlignment.Right);
		}
		else
			Osd("FLOOR CLEARED", x, 76, Dim);
		for (int i = 0; i < MusicRows.Length; i++)
		{
			string row = MusicRows[i];
			int index = songs + i;
			bool on = _sel == index;
			float y = 126 + i * 15 + (row == "BACK" ? 6 : 0);
			Line(row == "PLAYER" ? "" : row, index, x, y, right - x + 8);
			Color ink = on ? Ink : Soft;
			switch (row)
			{
				case "PLAYER": // back, pause or play, next
					Tri(x + 18, y - 10, ink, left: true);
					Tri(x + 23, y - 10, ink, left: true);
					if (Music.Paused)
						Tri(x + 58, y - 10, ink);
					else
						PauseSign(x + 58, y - 10, ink);
					Tri(x + 98, y - 10, ink);
					Tri(x + 103, y - 10, ink);
					break;
				case "SHUFFLE": Value(Music.Shuffle ? "ON" : "OFF", y, on); break;
				case "LOOP": Value(Music.Loop ? "ON" : "OFF", y, on); break;
				case "VOLUME": Bar(y, Music.Volume, on); break;
			}
		}
	}

	private static string Clock(float seconds) => $"{(int)seconds / 60}:{(int)seconds % 60:00}";

	// The controls tab: a scrolling list of groups, each action with its main and second key.
	private void DrawKeys()
	{
		const int top = 88, bottom = 200, view = bottom - top, rowH = 14, headH = 16;
		var lines = new List<(int Y, int H, int Key, string Group)>();
		int y = 0, key = 0;
		foreach (var line in Keys)
		{
			lines.Add(line.Group != null ? (y, headH, -1, line.Group) : (y, rowH, key++, null));
			y += line.Group != null ? headH : rowH;
		}
		int total = y, cur = lines.FindIndex(l => l.Key == _sel);
		if (cur >= 0) // keep the picked line in view, with its group's heading when it's the first in the group
		{
			bool first = cur > 0 && lines[cur - 1].Group != null;
			_scroll = Math.Min(_scroll, first ? lines[cur].Y - headH : lines[cur].Y);
			_scroll = Math.Max(_scroll, lines[cur].Y + rowH - view);
		}
		else if (_sel == -1)
			_scroll = 0;
		_scroll = Math.Clamp(_scroll, 0, total - view);

		foreach (var l in lines)
		{
			int ly = top + l.Y - _scroll;
			if (ly < top || ly + l.H > bottom)
				continue; // only whole lines
			if (l.Group != null)
			{
				float w = Width(l.Group);
				R(46, ly + 8, 200 - w / 2 - 54, 1, Faint);
				R(208 + w / 2, ly + 8, 154 - w / 2, 1, Faint);
				Osd(l.Group, 200, ly + 13, Colors.White, HorizontalAlignment.Center);
				continue;
			}
			bool on = _sel == l.Key;
			if (on)
			{
				R(28, ly, 344, rowH - 1, new Color(1f, 1f, 1f, 0.16f));
				Tri(33, ly + 2, Colors.White);
			}
			Osd(KeyRows[l.Key].Label, 46, ly + 11, on ? Colors.White : Soft);
			bool reset = KeyRows[l.Key].Action == "";
			_hits.Add((new Rect2(28, ly, reset ? 344 : 166, rowH), l.Key, -1, -1));
			for (int col = 0; col < 2 && !reset; col++)
			{
				float cx = col == 1 ? 282 : 196, cw = 80;
				R(cx, ly + 1, cw, rowH - 3, new Color(0f, 0f, 0f, 0.4f));
				if (on && _col == col)
					Outline(cx - 1, ly, cw + 2, rowH - 1, Colors.White);
				if (on && _col == col && _rebinding)
					Text("PRESS A KEY", cx + cw / 2, ly + 9, SmallFont, 8, Colors.White, HorizontalAlignment.Center);
				else
					Keycap(KeyName(KeyRows[l.Key].Action, col), cx + cw / 2, ly + 6);
				_hits.Add((new Rect2(cx - 1, ly, cw + 2, rowH), l.Key, -1, col));
			}
		}
		Scrollbar(372, top, view, total, view, _scroll);
		Line("BACK", KeyRows.Length, 34, 212);
		if (_rebinding)
			Text("A KEY OR MOUSE BUTTON  ESC CANCELS", 372, 211, SmallFont, 8, Dim, HorizontalAlignment.Right);
	}

	// A key as a little white keycap; a mouse button as a mouse with that button lit.
	private void Keycap(string key, float cx, float cy)
	{
		if (key == "")
			return;
		if (key is "LMB" or "RMB")
		{
			float x = Mathf.Round(cx) - 3, y = cy - 5;
			R(x + 1, y, 5, 1, Colors.White);
			R(x + 1, y + 10, 5, 1, Colors.White);
			R(x, y + 1, 1, 9, Colors.White);
			R(x + 6, y + 1, 1, 9, Colors.White);
			R(x + 1, y + 1, 5, 9, Ink);
			R(x + 1, y + 4, 5, 1, Colors.White);
			R(x + 3, y + 1, 1, 3, Colors.White);
			R(key == "RMB" ? x + 4 : x + 1, y + 1, 2, 3, Pink);
			return;
		}
		float w = Math.Max(11, Width(key, SmallFont, 8) + 6), left = Mathf.Round(cx - w / 2);
		R(left, cy - 5, w, 10, Colors.White);
		R(left, cy + 5, w, 1, Dim);
		Text(key, cx, cy + 3, SmallFont, 8, Ink, HorizontalAlignment.Center);
	}

	private static string KeyName(string action, int index)
	{
		List<InputEvent> keys = Settings.KeysOf(action);
		return index < keys.Count ? KeyLabel(keys[index]) : "";
	}

	private static string KeyLabel(InputEvent key) => key switch
	{
		InputEventMouseButton { ButtonIndex: MouseButton.Left } => "LMB",
		InputEventMouseButton { ButtonIndex: MouseButton.Right } => "RMB",
		InputEventMouseButton mouse => "MOUSE " + (int)mouse.ButtonIndex,
		InputEventKey k => OS.GetKeycodeString(k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode).ToUpperInvariant().Replace("ESCAPE", "ESC"),
		_ => "",
	};

	private void DrawConfirm(ulong now)
	{
		Corner(now);
		Panel(72, 88);
		Osd(_askLine, 200, 102, Colors.White, HorizontalAlignment.Center);
		Osd(_askSub, 200, 118, Dim, HorizontalAlignment.Center);
		string[] answers = { "NO", "YES" };
		for (int i = 0; i < answers.Length; i++)
		{
			float x = 170 + i * 60;
			if (_sel == i)
				R(x - 18, 130, 36, 15, Colors.White);
			Text(answers[i], x, 142, OsdFont, 16, _sel == i ? Ink : Soft, HorizontalAlignment.Center);
			_hits.Add((new Rect2(x - 18, 130, 36, 15), i, -1, -1));
		}
	}

	// ---------------------------------------------------------------- small helpers
	private float Width(string text, Font font = null, int size = 16) => (font ?? OsdFont).GetStringSize(text, HorizontalAlignment.Left, -1, size).X;

	// y is the baseline. Returns the width.
	private float Text(string text, float x, float y, Font font, int size, Color color, HorizontalAlignment align = HorizontalAlignment.Left)
	{
		float w = Width(text, font, size);
		float left = align == HorizontalAlignment.Center ? x - w / 2 : align == HorizontalAlignment.Right ? x - w : x;
		DrawString(font, new Vector2(Mathf.Round(left), Mathf.Round(y)), text, HorizontalAlignment.Left, -1, size, color);
		return w;
	}

	// Text with the slight red and cyan colour split of an on-screen display, `split` pixels each way.
	private float Osd(string text, float x, float y, Color color, HorizontalAlignment align = HorizontalAlignment.Left, Font font = null, int size = 16, int split = 1)
	{
		font ??= OsdFont;
		Text(text, x - split, y, font, size, Pink, align);
		Text(text, x + split, y, font, size, Cyan, align);
		return Text(text, x, y, font, size, color, align);
	}

	private void R(float x, float y, float w, float h, Color color) => DrawRect(new Rect2(x, y, w, h), color);

	private void Outline(float x, float y, float w, float h, Color color)
	{
		R(x, y, w, 1, color);
		R(x, y + h - 1, w, 1, color);
		R(x, y, 1, h, color);
		R(x + w - 1, y, 1, h, color);
	}

	// A solid triangle `size` rows tall, pointing right (or left).
	private void Tri(float x, float y, Color color, bool left = false, int size = 9)
	{
		for (int r = 0; r < size; r++)
		{
			int len = r <= size / 2 ? r + 1 : size - r;
			R(left ? x + size / 2 + 1 - len : x, y + r, len, 1, color);
		}
	}

	private void PauseSign(float x, float y, Color color)
	{
		R(x, y, 3, 9, color);
		R(x + 5, y, 3, 9, color);
	}

	private static uint Hash(uint n)
	{
		n = (n ^ 61) ^ (n >> 16);
		n += n << 3;
		n ^= n >> 4;
		n *= 0x27d4eb2d;
		return n ^ (n >> 15);
	}
}

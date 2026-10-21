using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RentDay;

// The main menu and the Esc menu, drawn like a VCR's on-screen display: PLAY or PAUSE in the
// corner, a running timecode, scanlines and a rolling tracking band, the picked line inverted.
// Mouse, keyboard and gamepad all work. Paused = true makes it the Esc menu: it sits over the
// frozen floor, and the game stays paused until it closes. It is also the loading screen (LoadScene).
public partial class TapeMenu : Control
{
	public const string ScenePath = "res://ui/menu.tscn";

	[Export] public bool Paused;

	private const int W = 400, H = 225;
	private static readonly Color Ink = new("07030f"), Soft = new("d8d2e8"), Dim = new("8f88a6"), Red = new("ff2a6d"), Cyan = new("2de2e6");
	private static readonly Color Faint = new(1f, 1f, 1f, 0.3f);

	private enum View { Main, Pause, Load, Settings, Confirm, Music, Loading }

	private static readonly string[] PauseItems = { "RESUME", "SAVE GAME", "LOAD GAME", "MUSIC", "SETTINGS", "QUIT TO MENU" };
	private static readonly string[] MusicRows = { "PLAYER", "SHUFFLE", "LOOP", "VOLUME", "BACK" }; // PLAYER: back, pause, next

	// Settings are placeholders for now: they keep their values while the game runs, nothing more.
	private record Row(string Label, string Key, string Kind, params string[] Choices);
	private static readonly string[] Tabs = { "DISPLAY", "AUDIO", "CONTROLS", "ACCESS" };
	private const int ControlsTab = 2;
	private static readonly Row[][] Rows =
	{
		new Row[] { new("FULLSCREEN", "full", "toggle"), new("WINDOW SIZE", "scale", "choice", "2X", "3X", "4X", "5X"), new("VSYNC", "vsync", "toggle") },
		new Row[] { new("MASTER", "master", "bar"), new("MUSIC", "music", "bar"), new("EFFECTS", "sfx", "bar") },
		null, // the controls tab lists Keys instead
		new Row[] { new("SCREEN SHAKE", "shake", "bar"), new("FLASHES", "flash", "toggle"), new("COLOR DRIFT", "drift", "bar") },
	};
	private static readonly Dictionary<string, int> Values = new()
	{
		["full"] = 0, ["scale"] = 2, ["vsync"] = 1, ["master"] = 8, ["music"] = 6, ["sfx"] = 8, ["shake"] = 10, ["flash"] = 1, ["drift"] = 5,
	};

	// The controls tab: actions in groups, each with its main and second key, read from the input map.
	private static readonly (string Group, string Label, string Action)[] Keys =
	{
		("COMBAT", null, null), (null, "SHOOT, SWING", "shoot"), (null, "GRAB, THROW", "grab"), (null, "EXECUTE", "execute"),
		("MOVE", null, null), (null, "MOVE UP", "move_up"), (null, "MOVE DOWN", "move_down"), (null, "MOVE LEFT", "move_left"),
		(null, "MOVE RIGHT", "move_right"), (null, "LOOK AHEAD", "look"),
		("GAME", null, null), (null, "RESTART FLOOR", "restart"), (null, "PAUSE", "ui_cancel"), (null, "QUICK SAVE", "quick_save"),
		(null, "QUICK LOAD", "quick_load"), (null, "NEXT SONG", "next_song"),
	};
	private static readonly (string Label, string Action)[] KeyRows = Keys.Where(k => k.Group == null).Select(k => (k.Label, k.Action)).ToArray();

	private Font _osd, _small, _title;
	private View _view;
	private readonly Stack<(View View, int Sel)> _back = new();
	private int _sel, _tab, _col, _scroll; // _sel -1 = the settings tabs
	private string _askLine, _askSub;
	private Action _onYes;
	private string _toast;
	private ulong _toastUntil, _opened;
	private List<SaveGame.Entry> _saves = new();
	private readonly List<(Rect2 Rect, int Index, int Tab, int Col)> _hits = new(); // rebuilt every draw

	public override void _Ready()
	{
		_osd = PixelFont("res://ui/fonts/VT323-Regular.ttf");
		_small = PixelFont("res://ui/fonts/Silkscreen-Regular.ttf");
		_title = PixelFont("res://ui/Blox2.ttf");
		ProcessMode = ProcessModeEnum.Always; // keeps running while the game is paused
		_opened = Time.GetTicksMsec();
		if (_loading != null)
		{
			_view = View.Loading;
			string[] art = ResourceLoader.ListDirectory(ArtFolder).Where(f => f.EndsWith(".png")).ToArray();
			_art = art.Length > 0 ? GD.Load<Texture2D>($"{ArtFolder}/{art[GD.Randi() % art.Length]}") : null;
			return;
		}
		_view = Paused ? View.Pause : View.Main;
		_saves = SaveGame.All();
		if (!Paused)
			Music.Menu(this);
		if (Paused) // the frozen floor behind, washed out
		{
			var wash = new ColorRect { ShowBehindParent = true, MouseFilter = MouseFilterEnum.Ignore, Material = new ShaderMaterial { Shader = GD.Load<Shader>("res://ui/tape_pause.gdshader") } };
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
				if (_tab == ControlsTab)
					Toast("KEY CHANGES COME LATER");
				else
					Change(Rows[_tab][_sel], 1, wrap: true);
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

	private static void Change(Row row, int dir, bool wrap = false)
	{
		int v = Values[row.Key];
		Values[row.Key] = row.Kind switch
		{
			"toggle" => 1 - v,
			"choice" => (v + dir + row.Choices.Length) % row.Choices.Length,
			_ => wrap ? (v + 1) % 11 : Math.Clamp(v + dir, 0, 10),
		};
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

	// ---------------------------------------------------------------- loading
	// The loading screen. It covers whatever is on (the game stays paused under it) and reads the scene,
	// everything in it (every floor) and all the music, a few pieces a frame so it keeps moving, then
	// switches to the scene. A quick load never shows it; once up, it stays a moment so it never just
	// flashes. The scene stays in memory until the next one, so the stairs and restarts never touch the disk.
	// Not Godot's background loading (LoadThreadedRequest): in 4.7 it crashed the game handing over a
	// scene with C# in it.
	private const string ArtFolder = "res://ui/loading"; // its pictures: one at random each time, best at 400 x 225
	private const ulong ShowAfter = 250, ShowAtLeast = 600, FrameBudget = 12; // milliseconds
	private static PackedScene _held;
	private string _loading; // the scene being read; null on a menu
	private ulong _shownAt;  // when the loading screen came up; 0 = not yet
	private readonly Stack<(string Path, bool Opened)> _todo = new(); // Opened: what it needs is queued above it
	private readonly HashSet<string> _seen = new();
	private readonly List<Resource> _read = new(); // held until the scene is in
	private int _done, _total;
	private float _progress;
	private Texture2D _art;

	public static void LoadScene(SceneTree tree, string path)
	{
		var screen = new TapeMenu { _loading = path, _total = 1 + Music.Unread };
		screen._todo.Push((path, false));
		screen._seen.Add(path);
		screen.SetAnchorsPreset(LayoutPreset.FullRect);
		var layer = new CanvasLayer { Layer = 100 }; // over the HUD and the Esc menu
		layer.AddChild(screen);
		tree.Root.AddChild(layer);
		tree.Paused = true; // nothing moves under it
	}

	private void PollLoading()
	{
		ulong now = Time.GetTicksMsec();
		if (_shownAt == 0 && now - _opened >= ShowAfter)
			_shownAt = now;
		bool done = ReadSome(now + FrameBudget);
		_progress = Math.Max(_progress, (float)_done / _total);
		if (!done || _shownAt > 0 && Time.GetTicksMsec() - _shownAt < ShowAtLeast)
			return;
		SceneTree tree = GetTree();
		tree.Paused = false;
		_held = GD.Load<PackedScene>(_loading); // already in memory
		if (_held != null)
			tree.ChangeSceneToPacked(_held);
		else
			tree.ChangeSceneToFile(_loading); // the read failed: the plain load says why
		_loading = null;
		GetParent().QueueFree();
	}

	// Reads pieces until the time is up (at least one); true once the scene and all the music are in.
	private bool ReadSome(ulong until)
	{
		do
		{
			if (_todo.Count > 0)
				ReadNext();
			else if (!Music.ReadNext())
				return true;
			_done++;
		} while (Time.GetTicksMsec() < until);
		return false;
	}

	// One piece of the scene: what it needs goes on top of it first, so by the time it is read, that is
	// all in memory and reading it is quick.
	private void ReadNext()
	{
		var (path, opened) = _todo.Pop();
		if (opened || ResourceLoader.HasCached(path))
		{
			_read.Add(GD.Load(path));
			return;
		}
		_todo.Push((path, true));
		_done--; // not read yet
		foreach (string dependency in ResourceLoader.GetDependencies(path))
		{
			string[] parts = dependency.Split("::"); // uid, type, path
			string need = parts[^1] != "" ? parts[^1] : parts[0];
			if (_seen.Add(need))
			{
				_todo.Push((need, false));
				_total++;
			}
		}
	}

	public override void _Input(InputEvent e)
	{
		if (_view == View.Loading)
			GetViewport().SetInputAsHandled(); // nothing under the loading screen reacts
	}

	// ---------------------------------------------------------------- input
	public override void _UnhandledInput(InputEvent e)
	{
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
		if (e is InputEventMouseMotion motion)
		{
			if (HitAt(motion.Position) is { } hover)
			{
				_sel = hover.Tab >= 0 ? -1 : hover.Index;
				if (hover.Col >= 0)
					_col = hover.Col;
				if (motion.ButtonMask.HasFlag(MouseButtonMask.Left) && IsVolumeRow(hover.Index)) // dragging along the volume bar
					VolumeAt(motion.Position.X);
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
			if (IsVolumeRow(hit.Index))
				VolumeAt(button.Position.X);
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
		Tape(now);
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
		uint s = (uint)(now / 50) * 9301u + 49297u;
		for (uint k = 0; k < 280; k++)
		{
			s = Hash(s + k);
			float v = (90 + s % 130) / 255f;
			R(s % W, (s >> 9) % H, 1, 1, new Color(v, v, Math.Min(v + 0.1f, 1f), 0.35f));
		}
	}

	// Over everything: a tracking band rolling down (held still on the settings, to keep them easy
	// to read) and scanlines.
	private void Tape(ulong now)
	{
		if (_view != View.Settings)
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
		{
			R(16 + w + 7, 15, 3, 10, Colors.White);
			R(16 + w + 12, 15, 3, 10, Colors.White);
		}
		else
			Tri(16 + w + 7, 15, Colors.White);
		ulong sec = (Paused ? _opened - Main.FloorStarted : now - _opened) / 1000;
		Osd($"SP {sec / 3600}:{sec / 60 % 60:00}:{sec % 60:00}", W - 16, H - 12, Colors.White, HorizontalAlignment.Right);
	}

	// The loading screen: the picture, the tape fast-forwarding, how far the read has got.
	private void DrawLoading(ulong now)
	{
		if (_art != null) // filling the screen, cropped to it (a 400 x 225 picture fits pixel for pixel)
		{
			Vector2 size = _art.GetSize() * Math.Max(W / _art.GetSize().X, H / _art.GetSize().Y);
			DrawTextureRect(_art, new Rect2((new Vector2(W, H) - size) / 2, size), false);
		}
		float w = Osd("FF", 16, 26, Colors.White);
		if (now / 300 % 2 == 0)
		{
			Tri(16 + w + 7, 15, Colors.White);
			Tri(16 + w + 13, 15, Colors.White);
		}
		Osd("LOADING", 16, H - 12, Colors.White);
		R(W - 96, H - 17, 80, 3, Faint); // how far the read has got
		R(W - 96, H - 17, Mathf.Round(80 * _progress), 3, Colors.White);
	}

	// One line of a list; the picked one inverted, like a VCR menu.
	private void Item(string label, int i, float x, float y)
	{
		float w = Width(label);
		if (_sel == i)
		{
			R(x - 6, y - 12, w + 26, 15, Colors.White);
			Tri(x - 1, y - 10, Ink);
			Text(label, x + 12, y, _osd, 16, Ink);
		}
		else
			Osd(label, x + 12, y, Soft);
		_hits.Add((new Rect2(x - 6, y - 13, Math.Max(240, w + 26), 16), i, -1, -1));
	}

	private void DrawMain(ulong now)
	{
		Corner(now);
		int jx = now / 2700 % 3 == 0 && now % 2700 < 140 ? 3 : 0; // now and then the title slips
		Text("RENT DAY", 32 + jx, 74, _title, 32, Red);
		Text("RENT DAY", 36 + jx, 74, _title, 32, Cyan);
		Text("RENT DAY", 34 + jx, 74, _title, 32, Colors.White);
		string[] items = MainItems();
		float y = 106;
		for (int i = 0; i < items.Length; i++)
		{
			if (items[i] == "QUIT")
				y += 6;
			Item(items[i] == "CONTINUE" ? $"CONTINUE  FLOOR {_saves[0].Data.Floor}" : items[i], i, 34, y);
			y += 17;
		}
	}

	private void DrawPause(ulong now)
	{
		Corner(now);
		R(0, 76, W, 108, new Color(0f, 0f, 0f, 0.55f));
		float y = 100;
		for (int i = 0; i < PauseItems.Length; i++)
		{
			if (i == PauseItems.Length - 1)
				y += 6;
			Item(PauseItems[i], i, 34, y);
			y += 17;
		}
	}

	private void DrawLoad(ulong now)
	{
		const int shown = 5;
		Corner(now);
		R(0, 46, W, 156, new Color(0f, 0f, 0f, 0.5f));
		Osd("TAPE INDEX", 34, 68, Colors.White);
		if (_saves.Count == 0)
			Osd("NO SAVES YET", 46, 96, Dim);
		if (_sel < _saves.Count) // keep the picked save in view
			_scroll = Math.Clamp(_scroll, _sel - shown + 1, _sel);
		_scroll = Math.Clamp(_scroll, 0, Math.Max(0, _saves.Count - shown));
		for (int i = _scroll; i < Math.Min(_saves.Count, _scroll + shown); i++)
		{
			SaveGame.Entry save = _saves[i];
			Item($"{i + 1:00}  {SaveGame.Title(save.Name),-10} FL.{save.Data.Floor:00}   {save.Data.Saved:MM/dd HH:mm}", i, 34, 96 + (i - _scroll) * 18);
		}
		Item("BACK", _saves.Count, 34, 96 + Math.Min(Math.Max(_saves.Count, 1), shown) * 18 + 10);
	}

	private void DrawSettings(ulong now)
	{
		Corner(now);
		R(0, 34, W, 186, new Color(0f, 0f, 0f, 0.58f));
		Osd("SETTINGS", 34, 56, Colors.White);
		float x = 34;
		for (int i = 0; i < Tabs.Length; i++)
		{
			float w = Width(Tabs[i]);
			if (_tab == i)
			{
				R(x - 4, 64, w + 8, 15, Colors.White);
				Text(Tabs[i], x, 76, _osd, 16, Ink);
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
		for (int i = 0; i <= rows.Length; i++)
		{
			bool on = _sel == i, back = i == rows.Length;
			float y = 100 + i * 15 + (back ? 5 : 0);
			string label = back ? "BACK" : rows[i].Label;
			if (on)
			{
				R(28, y - 12, 344, 15, Colors.White);
				Tri(33, y - 10, Ink);
				Text(label, 46, y, _osd, 16, Ink);
			}
			else
				Osd(label, 46, y, Soft);
			if (!back)
				DrawValue(rows[i], 362, y, on);
			_hits.Add((new Rect2(28, y - 12, 344, 15), i, -1, -1));
		}
	}

	private void DrawValue(Row row, float right, float y, bool on)
	{
		Color ink = on ? Ink : Colors.White;
		int v = Values[row.Key];
		if (row.Kind == "bar")
		{
			for (int k = 0; k < 10; k++)
				R(right - 88 + k * 9, y - 10, 7, 9, k < v ? ink : on ? new Color("c9c3d8") : new Color(1f, 1f, 1f, 0.2f));
			return;
		}
		string text = row.Kind == "toggle" ? (v == 1 ? "ON" : "OFF") : row.Choices[v];
		float w = on ? Text(text, right - 8, y, _osd, 16, ink, HorizontalAlignment.Right) : Osd(text, right - 8, y, ink, HorizontalAlignment.Right);
		if (!on)
			return;
		for (int r = 0; r < 5; r++) // arrows: left and right change it
		{
			int len = r <= 2 ? r + 1 : 5 - r;
			R(right - 8 - w - 6 - len, y - 9 + r, len, 1, ink);
			R(right - 3, y - 9 + r, len, 1, ink);
		}
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
				if (!enter) // left and right; a click or drag sets it on the bar (VolumeAt)
					Music.SetVolume(Music.Volume + dir);
				break;
		}
	}

	private void DrawMusic(ulong now)
	{
		const int shown = 8, top = 80;
		int songs = Music.Songs.Length;
		Corner(now);
		R(0, 34, W, 186, new Color(0f, 0f, 0f, 0.62f));
		Osd("MUSIC", 34, 56, Colors.White);

		// left: the floor songs; pick one to play it
		if (_sel < songs) // keep the picked song in view
			_scroll = Math.Clamp(_scroll, _sel - shown + 1, _sel);
		_scroll = Math.Clamp(_scroll, 0, Math.Max(0, songs - shown));
		for (int i = _scroll; i < Math.Min(songs, _scroll + shown); i++)
		{
			float y = top + (i - _scroll) * 15;
			string title = Music.Songs[i].Replace('_', ' ').ToUpperInvariant();
			bool on = _sel == i, playing = Music.OnFloorSong && Music.Current == i;
			if (on)
			{
				R(28, y - 12, 172, 15, Colors.White);
				Tri(33, y - 10, Ink);
				Text(title, 46, y, _osd, 16, Ink);
			}
			else
				Osd(title, 46, y, playing ? Colors.White : Soft);
			if (playing) // what's playing: a little bar, or a pause sign
			{
				Color mark = on ? Ink : new Color("ff2bd6");
				if (Music.Paused)
				{
					R(186, y - 10, 2, 8, mark);
					R(190, y - 10, 2, 8, mark);
				}
				else
				{
					for (int b = 0; b < 3; b++)
						R(184 + b * 3, y - 2 - (int)(3 + 3 * Mathf.Sin(now / 120f + b * 2)), 2, (int)(3 + 3 * Mathf.Sin(now / 120f + b * 2)) + 1, mark);
				}
			}
			_hits.Add((new Rect2(28, y - 12, 172, 15), i, -1, -1));
		}
		Text("N  NEXT SONG, ANY TIME", 34, 212, _small, 8, Dim);

		// right: what's playing, then the controls
		const float x = 214, right = 370;
		if (Music.OnFloorSong)
		{
			Text("NOW PLAYING", x, 58, _small, 8, Dim);
			Osd(Music.Title, x, 76, Colors.White);
			float len = Mathf.Max(Music.Length, 1f), at = Mathf.Min(Music.Position, len);
			R(x, 84, right - x, 3, new Color(1f, 1f, 1f, 0.2f));
			R(x, 84, Mathf.Round((right - x) * at / len), 3, Colors.White);
			Text(Clock(at), x, 99, _small, 8, Dim);
			Text(Clock(len), right, 99, _small, 8, Dim, HorizontalAlignment.Right);
		}
		else
			Osd("FLOOR CLEARED", x, 76, Dim);
		for (int i = 0; i < MusicRows.Length; i++)
		{
			string row = MusicRows[i];
			int index = songs + i;
			bool on = _sel == index;
			float y = 126 + i * 15 + (row == "BACK" ? 6 : 0);
			Color ink = on ? Ink : Soft;
			if (on)
			{
				R(x - 6, y - 12, right - x + 8, 15, Colors.White);
				Tri(x - 1, y - 10, Ink);
			}
			if (row == "PLAYER")
			{
				TriLeft(x + 18, y - 10, ink); TriLeft(x + 23, y - 10, ink); // back
				if (Music.Paused)
					Tri(x + 58, y - 10, ink);
				else
				{
					R(x + 58, y - 10, 3, 9, ink);
					R(x + 63, y - 10, 3, 9, ink);
				}
				Tri(x + 98, y - 10, ink); Tri(x + 103, y - 10, ink); // next
			}
			else
			{
				if (on) Text(row, x + 12, y, _osd, 16, Ink); else Osd(row, x + 12, y, Soft);
				string value = row switch { "SHUFFLE" => Music.Shuffle ? "ON" : "OFF", "LOOP" => Music.Loop ? "ON" : "OFF", _ => "" };
				if (value != "")
				{
					if (on) Text(value, right, y, _osd, 16, Ink, HorizontalAlignment.Right); else Osd(value, right, y, Colors.White, HorizontalAlignment.Right);
				}
				if (row == "VOLUME")
				{
					for (int k = 0; k < 10; k++)
						R(VolumeBar + k * 8, y - 10, 6, 9, k < Music.Volume ? (on ? Ink : Colors.White) : on ? new Color("c9c3d8") : new Color(1f, 1f, 1f, 0.2f));
				}
			}
			_hits.Add((new Rect2(x - 6, y - 12, right - x + 8, 15), index, -1, -1));
		}
	}

	private const float VolumeBar = 292; // where the volume bar's first block starts; each block is 8 wide

	private bool IsVolumeRow(int index) => _view == View.Music && index == Music.Songs.Length + Array.IndexOf(MusicRows, "VOLUME");

	// The block clicked becomes the volume (left of the first block: silent).
	private static void VolumeAt(float x)
	{
		if (x >= VolumeBar - 6)
			Music.SetVolume((int)Mathf.Floor((x - VolumeBar) / 8f) + 1);
	}

	private static string Clock(float seconds) => $"{(int)seconds / 60}:{(int)seconds % 60:00}";

	private void TriLeft(float x, float y, Color color)
	{
		for (int r = 0; r < 9; r++)
		{
			int len = r <= 4 ? r + 1 : 9 - r;
			R(x + 5 - len, y + r, len, 1, color);
		}
	}

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
			_hits.Add((new Rect2(28, ly, 166, rowH), l.Key, -1, -1));
			for (int col = 0; col < 2; col++)
			{
				float cx = col == 1 ? 282 : 196, cw = 80;
				R(cx, ly + 1, cw, rowH - 3, new Color(0f, 0f, 0f, 0.4f));
				if (on && _col == col)
					Outline(cx - 1, ly, cw + 2, rowH - 1, Colors.White);
				Keycap(KeyName(KeyRows[l.Key].Action, col), cx + cw / 2, ly + 6);
				_hits.Add((new Rect2(cx - 1, ly, cw + 2, rowH), l.Key, -1, col));
			}
		}
		if (total > view) // where you are in the list
		{
			int bar = Math.Max(10, view * view / total);
			R(373, top, 1, view, new Color(1f, 1f, 1f, 0.2f));
			R(372, top + (view - bar) * _scroll / (total - view), 3, bar, Colors.White);
		}
		int backIndex = KeyRows.Length;
		if (_sel == backIndex)
		{
			R(28, 200, 60, 15, Colors.White);
			Tri(33, 202, Ink);
			Text("BACK", 46, 212, _osd, 16, Ink);
		}
		else
			Osd("BACK", 46, 212, Soft);
		_hits.Add((new Rect2(28, 200, 120, 15), backIndex, -1, -1));
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
			R(key == "RMB" ? x + 4 : x + 1, y + 1, 2, 3, Red);
			return;
		}
		float w = Math.Max(11, Width(key, _small, 8) + 6), left = Mathf.Round(cx - w / 2);
		R(left, cy - 5, w, 10, Colors.White);
		R(left, cy + 5, w, 1, Dim);
		Text(key, cx, cy + 3, _small, 8, Ink, HorizontalAlignment.Center);
	}

	private static string KeyName(string action, int index)
	{
		if (!InputMap.HasAction(action))
			return "";
		var events = InputMap.ActionGetEvents(action).Where(e => e is InputEventKey or InputEventMouseButton).ToList();
		if (index >= events.Count)
			return "";
		return events[index] switch
		{
			InputEventMouseButton { ButtonIndex: MouseButton.Left } => "LMB",
			InputEventMouseButton { ButtonIndex: MouseButton.Right } => "RMB",
			InputEventMouseButton mouse => "MOUSE " + (int)mouse.ButtonIndex,
			InputEventKey k => OS.GetKeycodeString(k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode).ToUpperInvariant().Replace("ESCAPE", "ESC"),
			_ => "",
		};
	}

	private void DrawConfirm(ulong now)
	{
		Corner(now);
		R(0, 72, W, 88, new Color(0f, 0f, 0f, 0.7f));
		Osd(_askLine, 200, 102, Colors.White, HorizontalAlignment.Center);
		Osd(_askSub, 200, 118, Dim, HorizontalAlignment.Center);
		string[] answers = { "NO", "YES" };
		for (int i = 0; i < answers.Length; i++)
		{
			float x = 170 + i * 60;
			if (_sel == i)
				R(x - 18, 130, 36, 15, Colors.White);
			Text(answers[i], x, 142, _osd, 16, _sel == i ? Ink : Soft, HorizontalAlignment.Center);
			_hits.Add((new Rect2(x - 18, 130, 36, 15), i, -1, -1));
		}
	}

	// ---------------------------------------------------------------- small helpers
	private static FontFile PixelFont(string path)
	{
		var font = GD.Load<FontFile>(path);
		font.Antialiasing = TextServer.FontAntialiasing.None;
		font.Hinting = TextServer.Hinting.None;
		font.SubpixelPositioning = TextServer.SubpixelPositioning.Disabled;
		return font;
	}

	private float Width(string text, Font font = null, int size = 16) => (font ?? _osd).GetStringSize(text, HorizontalAlignment.Left, -1, size).X;

	// y is the baseline. Returns the width.
	private float Text(string text, float x, float y, Font font, int size, Color color, HorizontalAlignment align = HorizontalAlignment.Left)
	{
		float w = Width(text, font, size);
		float left = align == HorizontalAlignment.Center ? x - w / 2 : align == HorizontalAlignment.Right ? x - w : x;
		DrawString(font, new Vector2(Mathf.Round(left), Mathf.Round(y)), text, HorizontalAlignment.Left, -1, size, color);
		return w;
	}

	// On-screen text with the slight colour split of a worn tape.
	private float Osd(string text, float x, float y, Color color, HorizontalAlignment align = HorizontalAlignment.Left)
	{
		Text(text, x - 1, y, _osd, 16, Red, align);
		Text(text, x + 1, y, _osd, 16, Cyan, align);
		return Text(text, x, y, _osd, 16, color, align);
	}

	private void R(float x, float y, float w, float h, Color color) => DrawRect(new Rect2(x, y, w, h), color);

	private void Outline(float x, float y, float w, float h, Color color)
	{
		R(x, y, w, 1, color);
		R(x, y + h - 1, w, 1, color);
		R(x, y, 1, h, color);
		R(x + w - 1, y, 1, h, color);
	}

	private void Tri(float x, float y, Color color)
	{
		for (int r = 0; r < 9; r++)
			R(x, y + r, r <= 4 ? r + 1 : 9 - r, 1, color);
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

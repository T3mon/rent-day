using System;
using System.Linq;
using Godot;

namespace RentDay;

// The top of a run. Plays the floors in order: kill every enemy on a floor and its stairs
// up open; walk onto them and you're on the next floor, holding the gun you came with.
// Holds the on-screen messages and ammo counter, and handles restarting. Every floor start saves the
// playthrough (SaveGame); Esc (or switching to another window) opens the Esc menu (Menu).
// A run starts from the main menu: NewGame, or Continue a saved playthrough.
public partial class Main : Node2D
{
	[Export] public PackedScene[] Floors = [];

	// These outlive the scene reload between floors and on restart.
	private static int _floor = -1;
	private static WeaponData _carried;  // the gun you came up the stairs with, and its rounds
	private static int _carriedAmmo;
	private static bool _arriving;
	private static string _save, _name; // the playthrough: its save's file (null in a test run of main.tscn) and name
	private static readonly PackedScene MenuScene = GD.Load<PackedScene>(Menu.ScenePath);

	public static ulong FloorStarted { get; private set; } // engine ticks when this floor began
	public static string CurrentSave => _save;

	// Every enemy on this floor is dead (the stairs up open).
	public bool Cleared { get; private set; }

	private Label _message;
	private Label _ammo;
	private ColorRect _fade;
	private Label _note;
	private Control _battery;
	private float _shownMeter = 1f;
	private Control _dash;
	private float _dashGlow; // seconds the dash tile stays up, fully lit, after it recharges
	private static readonly Texture2D DashIcon = GD.Load<Texture2D>("res://art/icons/dash.png"); // 14 px
	private ColorRect _slowLook;
	private ShaderMaterial _slowShader;
	private float _slowFade; // 0..1, how far slow motion's look is in
	private int _songShown;
	private Player _player;
	private int _shownAmmo = int.MinValue;
	private bool _leaving;
	private bool _won;

	public override void _Ready()
	{
		if (_floor < 0)
			_floor = DevSettings.Current.StartFloor - 1;
		_floor = Mathf.Clamp(_floor, 0, Floors.Length - 1); // a save may name a floor that's gone
		var level = Floors[_floor].Instantiate<Node2D>();
		AddChild(level);
		MoveChild(level, 0);

		_message = GetNode<Label>("HUD/Message");
		_message.Visible = false;
		_ammo = GetNode<Label>("HUD/Ammo");
		_fade = new ColorRect { Color = Colors.Black, MouseFilter = Control.MouseFilterEnum.Ignore };
		_fade.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_fade.Modulate = _arriving ? Colors.White : Colors.Transparent;
		GetNode("HUD").AddChild(_fade);
		if (_arriving)
			CreateTween().TweenProperty(_fade, "modulate:a", 0f, 0.3);
		_arriving = false;

		// Short notes in the top left corner, in the menu's lettering: "NOW PLAYING  ...".
		_note = new Label { Position = new Vector2(16, 6), Visible = false };
		_note.AddThemeFontOverride("font", Style.OsdFont);
		_note.AddThemeFontSizeOverride("font_size", 16);
		GetNode("HUD").AddChild(_note);

		// Above them, while they recharge: the dash (DrawDash), and the slow-motion meter, a battery (DrawBattery).
		_dash = new Control { Name = "Dash", Position = new Vector2(8, 8), Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
		_dash.Draw += DrawDash;
		GetNode("HUD").AddChild(_dash);
		_battery = new Control { Name = "SlowMeter", Position = new Vector2(8, 8), Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
		_battery.Draw += DrawBattery;
		GetNode("HUD").AddChild(_battery);

		ShowDevInfo();

		// Groups are tags set in the scenes: everyone tagged "player" / "enemies".
		_player = (Player)GetTree().GetFirstNodeInGroup("player");
		_player.Died += () => ShowMessage("YOU'RE DEAD\npress R");
		if (_carried != null)
			_player.Equip(_carried, _carriedAmmo);
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
			((Enemy)node).Died += OnEnemyDied;

		// The level's floor covers the whole map, so its size is the camera's limit,
		// and (minus walls and furniture) the area enemies can walk.
		if (level.FindChild("Floor", true, false) is TileMapLayer floor)
		{
			Rect2I cells = floor.GetUsedRect();
			Vector2I tile = floor.TileSet.TileSize;
			var area = new Rect2I(cells.Position * tile, cells.Size * tile);
			var map = new Rect2(floor.Position + area.Position, area.Size);
			_player.SetCameraLimits(new Rect2I((Vector2I)floor.GlobalPosition + area.Position, area.Size));
			LevelGrid.Build(level, map);
			GooCanvas.Build(level, map);
			level.AddChild(Fence(map));
		}

		// Slow motion's look over the level (effects/slow_motion.gdshader), faded in and out in _Process. At z 7
		// it covers the walls and the stairs; you (lifted to 9 while slowed), bullets and afterimages (8) draw over it.
		_slowShader = new ShaderMaterial { Shader = GD.Load<Shader>("res://effects/slow_motion.gdshader") };
		_slowLook = new ColorRect { Material = _slowShader, ZIndex = 7, ZAsRelative = false, Visible = false,
			MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(-4096, -4096), Size = new Vector2(8192, 8192) };
		AddChild(_slowLook);

		FloorStarted = Time.GetTicksMsec();
		if (_save != null)
			SaveGame.Write(_save, Checkpoint());
		_songShown = Music.Started;
		Music.Floor(this, _floor); // keeps playing through a death or a restart
	}

	// The edge of the map stops everyone, you and anyone thrown: four thick sides just outside it, on the
	// furniture layer, so bullets and eyesight still pass.
	private static StaticBody2D Fence(Rect2 map)
	{
		const float Thick = 64f;
		var fence = new StaticBody2D { Name = "MapEdge", CollisionLayer = Layers.Furniture, CollisionMask = 0 };
		foreach (Rect2 side in new[]
		{
			new Rect2(map.Position.X - Thick, map.Position.Y - Thick, Thick, map.Size.Y + 2 * Thick), // left
			new Rect2(map.End.X, map.Position.Y - Thick, Thick, map.Size.Y + 2 * Thick),                // right
			new Rect2(map.Position.X, map.Position.Y - Thick, map.Size.X, Thick),                       // top
			new Rect2(map.Position.X, map.End.Y, map.Size.X, Thick),                                    // bottom
		})
			fence.AddChild(new CollisionShape2D { Position = side.GetCenter(), Shape = new RectangleShape2D { Size = side.Size } });
		return fence;
	}

	// A new playthrough: the first floor (or the testing start floor), empty-handed.
	public static void NewGame(SceneTree tree, string name)
	{
		(_save, _name) = (SaveGame.NewId(), name);
		Begin(tree, null);
	}

	// A saved playthrough, from the floor it was saved on, holding the gun it was saved with.
	public static void Continue(SceneTree tree, SaveGame.Entry entry)
	{
		(_save, _name) = (entry.Id, entry.Name);
		Begin(tree, entry.Data);
	}

	// Testing (the admin panel): another floor of this playthrough, empty-handed.
	public static void GoTo(SceneTree tree, int floor) => Begin(tree, new SaveGame.Data(SaveGame.Version, floor, null, 0, DateTime.Now, _name));

	private static void Begin(SceneTree tree, SaveGame.Data save)
	{
		_floor = save != null ? save.Floor - 1 : -1;
		_carried = save != null ? SaveGame.WeaponOf(save) : null;
		_carriedAmmo = _carried != null ? save.Ammo : 0;
		_arriving = true;
		Menu.LoadScene(tree, "res://main.tscn");
	}

	// What a save holds: this floor, and the gun you came onto it with (loading starts the floor over).
	private static SaveGame.Data Checkpoint() =>
		new(SaveGame.Version, _floor + 1, _carried?.ResourcePath, _carried != null ? _carriedAmmo : 0, DateTime.Now, _name);

	public override void _UnhandledInput(InputEvent e)
	{
		if (e.IsActionPressed("ui_cancel"))
		{
			GetViewport().SetInputAsHandled();
			OpenMenu();
		}
		else if (e.IsActionPressed("admin_panel") && OS.IsDebugBuild())
		{
			GetViewport().SetInputAsHandled();
			GetNode("HUD").AddChild(new AdminPanel { FloorCount = Floors.Length, Changed = ShowDevInfo });
			GetTree().Paused = true;
		}
		else if (e.IsActionPressed("next_song"))
			Music.Skip(1);
	}

	// Switching to another window (alt-tab) pauses the game the way Esc does.
	public override void _Notification(int what)
	{
		if (what == NotificationApplicationFocusOut && !GetTree().Paused)
			OpenMenu();
	}

	private void OpenMenu()
	{
		if (SlowMotion.On)
			SlowMotion.Set(1f); // the menu runs at full speed
		var menu = MenuScene.Instantiate<Menu>();
		menu.Paused = true;
		GetNode("HUD").AddChild(menu);
		GetTree().Paused = true;
	}

	// Testing switches that are on (debug/dev_settings.tres, the admin panel), so they're never mistaken for bugs.
	private void ShowDevInfo() => GetNode<Label>("HUD/DevInfo").Text = DevSettings.Current.ActiveSummary();

	private void Note(string text)
	{
		_note.Text = text;
		_note.Visible = true;
		GetTree().CreateTimer(1.5).Timeout += () =>
		{
			if (IsInstanceValid(_note) && _note.Text == text) // a newer note stays up
				_note.Visible = false;
		};
	}

	public override void _Process(double delta)
	{
		// R restarts the floor instantly, with the gun you came up with (after the last floor: the whole building).
		if (Input.IsActionJustPressed("restart"))
		{
			if (_won)
			{
				_floor = 0;
				_carried = null;
			}
			GetTree().ReloadCurrentScene();
		}

		UpdateAmmo();
		UpdateBattery();
		UpdateDash(delta);
		UpdateSlowLook(delta);
		if (Music.Started != _songShown) // a new floor song: say what's playing
		{
			_songShown = Music.Started;
			Note("NOW PLAYING  " + Music.Title);
		}
	}

	// Up the stairs: fade out and load the next floor.
	public void NextFloor()
	{
		if (_leaving)
			return;
		_leaving = true;
		_carried = _player.Gun.Data;
		_carriedAmmo = _player.Gun.Ammo;
		_floor++;
		_arriving = true;
		Tween fade = CreateTween();
		fade.TweenProperty(_fade, "modulate:a", 1f, 0.3);
		fade.TweenCallback(Callable.From(() => GetTree().ReloadCurrentScene()));
	}

	// Bottom-left corner: rounds left in the gun you hold. The label is only
	// touched when the number changes, not every frame.
	private void UpdateAmmo()
	{
		int ammo = _player.Gun.Data is null or { Melee: true } || _player.IsDead ? -1 : _player.Gun.Ammo;
		if (ammo == _shownAmmo)
			return;
		_shownAmmo = ammo;
		_ammo.Text = ammo < 0 ? "" : ammo > 0 ? ammo.ToString() : "empty";
		_ammo.Modulate = ammo == 0 ? new Color(1f, 0.35f, 0.35f) : Colors.White;
	}

	// Top-left corner: the slow-motion meter, only while it isn't full (and you're alive). Redrawn
	// only when it changes.
	private void UpdateBattery()
	{
		float meter = _player.IsDead ? 1f : _player.SlowMeter;
		if (meter == _shownMeter)
			return;
		_shownMeter = meter;
		_battery.Visible = meter < 1f;
		_battery.QueueRedraw();
	}

	// Top-left corner, while it recharges (and you're alive): the dash, lit for a moment more once it's ready.
	// The battery moves over beside it, notes go below.
	private void UpdateDash(double delta)
	{
		bool waiting = !_player.IsDead && _player.DashWait > 0f;
		_dashGlow = waiting ? 0.3f : _player.IsDead ? 0f : Mathf.Max(_dashGlow - (float)delta, 0f);
		_dash.Visible = waiting || _dashGlow > 0f;
		if (_dash.Visible)
			_dash.QueueRedraw(); // the seconds count down
		_battery.Position = _dash.Visible ? new Vector2(8 + Tile + 6, 11) : new Vector2(8, 8);
		_note.Position = new Vector2(16, _dash.Visible ? 8 + Tile + 18 : _battery.Visible ? 20 : 6);
	}

	// The look every ability's recharge will share: a square with its shadow, like the battery, the icon under a
	// dark wedge that sweeps away clockwise as it recharges, the seconds left over it, and below it the key it's on,
	// a keycap as in the controls menu, never wider than the square.
	private const int Tile = 16;

	private void DrawDash()
	{
		_dash.DrawRect(new Rect2(2, 2, Tile, Tile), Style.Ink);
		_dash.DrawRect(new Rect2(0, 0, Tile, Tile), Style.Soft);
		_dash.DrawRect(new Rect2(1, 1, Tile - 2, Tile - 2), Style.Ink);
		_dash.DrawTextureRect(DashIcon, new Rect2(1, 1, Tile - 2, Tile - 2), false);
		float wait = _player.DashWait;
		if (wait > 0f)
			_dash.DrawColoredPolygon(Wedge(wait / _player.DashCooldown), Style.Ink with { A = 0.7f });
		DrawKeycap(FitKey(Menu.KeyName("dash", 0)));
		if (wait <= 0f)
			return;
		string seconds = Mathf.CeilToInt(wait).ToString();
		_dash.DrawStringOutline(Style.OsdFont, new Vector2(0, 13), seconds, HorizontalAlignment.Center, Tile, 16, 3, Style.Ink);
		_dash.DrawString(Style.OsdFont, new Vector2(0, 13), seconds, HorizontalAlignment.Center, Tile, 16, Style.Soft);
	}

	// The share still to recharge (1 down to 0) as a pie over the icon, from 12 o'clock round to where the sweep
	// has got to, its edge running along the square's border.
	private static Vector2[] Wedge(float left)
	{
		const int Steps = 64;
		Vector2 centre = new(Tile / 2f, Tile / 2f);
		float half = Tile / 2f - 1f;
		var points = new Vector2[Steps + 2];
		points[0] = centre;
		for (int i = 0; i <= Steps; i++)
		{
			Vector2 way = Vector2.FromAngle(-Mathf.Pi / 2f + Mathf.Tau * (1f - left * (1f - (float)i / Steps)));
			points[i + 1] = centre + way * (half / Mathf.Max(Mathf.Abs(way.X), Mathf.Abs(way.Y)));
		}
		return points;
	}

	// Under the square: a white keycap with its shadow. Space shows as the space bar's bracket, since no word fits.
	private void DrawKeycap(string key)
	{
		if (key == "")
			return;
		float w = key == "SPACE" ? 12 : KeyWidth(key), x = Mathf.Round((Tile - w) / 2);
		_dash.DrawRect(new Rect2(x + 1, Tile + 3, w, 10), Style.Ink);
		_dash.DrawRect(new Rect2(x, Tile + 2, w, 10), Colors.White);
		if (key != "SPACE")
		{
			_dash.DrawString(Style.SmallFont, new Vector2(x, Tile + 10), key, HorizontalAlignment.Center, w, 8, Style.Ink);
			return;
		}
		_dash.DrawRect(new Rect2(x + 3, Tile + 5, 1, 4), Style.Ink);
		_dash.DrawRect(new Rect2(x + 3, Tile + 8, 6, 1), Style.Ink);
		_dash.DrawRect(new Rect2(x + 8, Tile + 5, 1, 4), Style.Ink);
	}

	private static float KeyWidth(string key) => Mathf.Round(Style.SmallFont.GetStringSize(key, HorizontalAlignment.Left, -1, 8).X) + 2;

	// A key's name short enough for its keycap to fit under the square: long names shortened, then cut.
	private static string FitKey(string key)
	{
		if (key == "SPACE")
			return key; // drawn as a symbol
		key = key switch
		{
			"BACKSPACE" => "BKSP", "CAPSLOCK" => "CAPS", "PAGEUP" => "PGUP", "PAGEDOWN" => "PGDN",
			"DELETE" => "DEL", "INSERT" => "INS", "ESCAPE" => "ESC", _ => key.Replace("MOUSE ", "M"),
		};
		while (key.Length > 1 && KeyWidth(key) > Tile)
			key = key[..^1];
		return key;
	}

	// Slow motion's look comes in and goes out over a quarter of a second of real time.
	private void UpdateSlowLook(double delta)
	{
		_slowFade = Mathf.MoveToward(_slowFade, SlowMotion.On ? 1f : 0f, (float)(delta / Engine.TimeScale) / 0.25f);
		_slowLook.Visible = _slowFade > 0f;
		if (!_slowLook.Visible)
			return;
		_slowShader.SetShaderParameter("amount", _slowFade);
		_slowShader.SetShaderParameter("game_pixel", Vector2.One / GetViewport().GetVisibleRect().Size);
	}

	// Ten cells in a battery with a nub on the right, and its shadow. Lit cyan, red while there's too
	// little to start; the cell filling up is dimmer.
	private void DrawBattery()
	{
		const int Cells = 10;
		Color lit = _player.SlowReady ? Style.Cyan : new Color(1f, 0.19f, 0.28f);
		foreach (var (at, colour) in new[] { (new Vector2(2, 2), Style.Ink), (Vector2.Zero, Style.Soft) })
		{
			_battery.DrawRect(new Rect2(at, new Vector2(33, 9)), colour);
			_battery.DrawRect(new Rect2(at + new Vector2(33, 3), new Vector2(2, 3)), colour);
		}
		_battery.DrawRect(new Rect2(1, 1, 31, 7), Style.Ink);
		float cells = _shownMeter * Cells;
		for (int i = 0; i < cells; i++)
			_battery.DrawRect(new Rect2(2 + i * 3, 2, 2, 5), i + 1 <= cells ? lit : lit.Darkened(0.55f));
	}

	private void OnEnemyDied()
	{
		if (_player.IsDead || !GetTree().GetNodesInGroup("enemies").All(node => ((Enemy)node).IsDead))
			return;
		Cleared = true;
		Music.Clear();
		if (_floor == Floors.Length - 1)
		{
			_won = true;
			ShowMessage("RENT COLLECTED\npress R");
			return;
		}
		ShowMessage("FLOOR CLEAR");
		GetTree().CreateTimer(1.5).Timeout += () => _message.Visible = false; // out of the way on the walk to the stairs
	}

	private void ShowMessage(string text)
	{
		_message.Text = text;
		_message.Visible = true;
	}
}

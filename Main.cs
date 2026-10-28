using System;
using System.Linq;
using Godot;

namespace RentDay;

// The top of a run. Plays the floors in order: kill every enemy on a floor and its stairs
// up open; walk onto them and you're on the next floor, holding the gun you came with.
// Holds the on-screen messages and ammo counter, and handles restarting. Every floor start is
// autosaved; Esc opens the Esc menu (Menu), F5 quick-saves, F9 loads the quick save.
// A run starts from the main menu through Begin (a new game or a save).
public partial class Main : Node2D
{
	[Export] public PackedScene[] Floors = [];

	// These outlive the scene reload between floors and on restart.
	private static int _floor = -1;
	private static WeaponData _carried;  // the gun you came up the stairs with, and its rounds
	private static int _carriedAmmo;
	private static bool _arriving;
	private static readonly PackedScene MenuScene = GD.Load<PackedScene>(Menu.ScenePath);

	public static ulong FloorStarted { get; private set; } // engine ticks when this floor began

	// Every enemy on this floor is dead (the stairs up open).
	public bool Cleared { get; private set; }

	private Label _message;
	private Label _ammo;
	private ColorRect _fade;
	private Label _note;
	private Control _battery;
	private float _shownMeter = 1f;
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

		// Short notes in the top left corner, in the menu's lettering: "QUICK SAVED".
		_note = new Label { Position = new Vector2(16, 6), Visible = false };
		_note.AddThemeFontOverride("font", GD.Load<Font>("res://ui/fonts/VT323-Regular.ttf"));
		_note.AddThemeFontSizeOverride("font_size", 16);
		GetNode("HUD").AddChild(_note);

		// Above them, while it isn't full: the slow-motion meter, a battery (DrawBattery).
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
		SaveGame.Write(SaveGame.Auto, Checkpoint());
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

	// Starts a run: a new game (the first floor, or the testing start floor), or the floor a save
	// was made on, holding the gun it was made with.
	public static void Begin(SceneTree tree, SaveGame.Data save = null)
	{
		_floor = save != null ? save.Floor - 1 : -1;
		_carried = save != null ? SaveGame.WeaponOf(save) : null;
		_carriedAmmo = _carried != null ? save.Ammo : 0;
		_arriving = true;
		Menu.LoadScene(tree, "res://main.tscn");
	}

	// What a save holds: this floor, and the gun you came onto it with (loading starts the floor over).
	public static SaveGame.Data Checkpoint() =>
		new(SaveGame.Version, _floor + 1, _carried?.ResourcePath, _carried != null ? _carriedAmmo : 0, DateTime.Now);

	public override void _UnhandledInput(InputEvent e)
	{
		if (e.IsActionPressed("ui_cancel"))
		{
			GetViewport().SetInputAsHandled();
			if (SlowMotion.On)
				SlowMotion.Set(1f); // the menu runs at full speed
			var menu = MenuScene.Instantiate<Menu>();
			menu.Paused = true;
			GetNode("HUD").AddChild(menu);
			GetTree().Paused = true;
		}
		else if (e.IsActionPressed("admin_panel") && OS.IsDebugBuild())
		{
			GetViewport().SetInputAsHandled();
			GetNode("HUD").AddChild(new AdminPanel { FloorCount = Floors.Length, Changed = ShowDevInfo });
			GetTree().Paused = true;
		}
		else if (e.IsActionPressed("next_song"))
			Music.Skip(1);
		else if (e.IsActionPressed("quick_save"))
			Note(SaveGame.Write(SaveGame.Quick, Checkpoint()) ? "QUICK SAVED" : "COULD NOT SAVE");
		else if (e.IsActionPressed("quick_load"))
		{
			if (SaveGame.Read(SaveGame.Quick) is { } save)
				Begin(GetTree(), save);
			else
				Note("NO QUICK SAVE YET");
		}
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
	// only when it changes. Notes move down out of its way.
	private void UpdateBattery()
	{
		float meter = _player.IsDead ? 1f : _player.SlowMeter;
		if (meter == _shownMeter)
			return;
		_shownMeter = meter;
		_battery.Visible = meter < 1f;
		_battery.QueueRedraw();
		_note.Position = new Vector2(16, _battery.Visible ? 20 : 6);
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
		var ink = new Color(0.04f, 0.02f, 0.06f);
		var rim = new Color(0.85f, 0.82f, 0.91f);
		Color lit = _player.SlowReady ? new Color(0.18f, 0.89f, 0.9f) : new Color(1f, 0.19f, 0.28f);
		foreach (var (at, colour) in new[] { (new Vector2(2, 2), ink), (Vector2.Zero, rim) })
		{
			_battery.DrawRect(new Rect2(at, new Vector2(33, 9)), colour);
			_battery.DrawRect(new Rect2(at + new Vector2(33, 3), new Vector2(2, 3)), colour);
		}
		_battery.DrawRect(new Rect2(1, 1, 31, 7), ink);
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

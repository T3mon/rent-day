using System.Linq;
using Godot;

namespace RentDay;

// The top of the game. Plays the floors in order: kill every goon on a floor and its stairs
// up open; walk onto them and you're on the next floor, holding the gun you came with.
// Holds the on-screen messages and ammo counter, and handles restarting.
public partial class Main : Node2D
{
	[Export] public PackedScene[] Floors = [];

	// These outlive the scene reload between floors and on restart.
	private static int _floor = -1;
	private static WeaponData _carried;  // the gun you came up the stairs with, and its rounds
	private static int _carriedAmmo;
	private static bool _arriving;

	private Label _message;
	private Label _ammo;
	private ColorRect _fade;
	private Player _player;
	private int _shownAmmo = int.MinValue;
	private bool _leaving;
	private bool _won;

	public override void _Ready()
	{
		if (_floor < 0)
			_floor = Mathf.Clamp(DevSettings.Current.StartFloor - 1, 0, Floors.Length - 1);
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

		// Testing switches that are on (debug/dev_settings.tres), so they're never mistaken for bugs.
		GetNode<Label>("HUD/DevInfo").Text = DevSettings.Current.ActiveSummary();

		// Groups are tags set in the scenes: everyone tagged "player" / "enemies".
		_player = (Player)GetTree().GetFirstNodeInGroup("player");
		_player.Died += () => ShowMessage("YOU'RE DEAD\npress R");
		if (_carried != null)
			_player.Equip(_carried, _carriedAmmo);
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
			((Enemy)node).Died += OnEnemyDied;

		// The level's floor covers the whole map, so its size is the camera's limit,
		// and (minus walls and furniture) the area goons can walk.
		if (level.FindChild("Floor", true, false) is TileMapLayer floor)
		{
			Rect2I cells = floor.GetUsedRect();
			Vector2I tile = floor.TileSet.TileSize;
			var area = new Rect2I(cells.Position * tile, cells.Size * tile);
			_player.SetCameraLimits(new Rect2I((Vector2I)floor.GlobalPosition + area.Position, area.Size));
			LevelGrid.Build(level, new Rect2(floor.Position + area.Position, area.Size));
			GooCanvas.Build(level, new Rect2(floor.Position + area.Position, area.Size));
		}
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

	private void OnEnemyDied()
	{
		if (_player.IsDead || !GetTree().GetNodesInGroup("enemies").All(node => ((Enemy)node).IsDead))
			return;
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

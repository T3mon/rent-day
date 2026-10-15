using System.Linq;
using Godot;

namespace RentDay;

// The top of the game. Holds the current level and the on-screen messages
// and ammo counter, and handles restarting.
public partial class Main : Node2D
{
	private Label _message;
	private Label _ammo;
	private Player _player;
	private int _shownAmmo = int.MinValue;

	public override void _Ready()
	{
		_message = GetNode<Label>("HUD/Message");
		_message.Visible = false;
		_ammo = GetNode<Label>("HUD/Ammo");

		// Testing switches that are on (debug/dev_settings.tres), so they're never mistaken for bugs.
		GetNode<Label>("HUD/DevInfo").Text = DevSettings.Current.ActiveSummary();

		// Groups are tags set in the scenes: everyone tagged "player" / "enemies".
		_player = (Player)GetTree().GetFirstNodeInGroup("player");
		_player.Died += () => ShowMessage("YOU'RE DEAD\npress R");
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
			((Enemy)node).Died += OnEnemyDied;

		// The level's floor covers the whole map, so its size is the camera's limit,
		// and (minus walls and furniture) the area goons can walk.
		if (FindChild("Floor", true, false) is TileMapLayer floor)
		{
			Rect2I cells = floor.GetUsedRect();
			Vector2I tile = floor.TileSet.TileSize;
			var area = new Rect2I(cells.Position * tile, cells.Size * tile);
			_player.SetCameraLimits(new Rect2I((Vector2I)floor.GlobalPosition + area.Position, area.Size));
			LevelNavigation.Build(floor.GetParent<Node2D>(), new Rect2(floor.Position + area.Position, area.Size));
			GooCanvas.Build(floor.GetParent<Node2D>(), new Rect2(floor.Position + area.Position, area.Size));
		}
	}

	public override void _Process(double delta)
	{
		// R restarts the whole floor instantly.
		if (Input.IsActionJustPressed("restart"))
			GetTree().ReloadCurrentScene();

		UpdateAmmo();
	}

	// Bottom-left corner: rounds left in the gun you hold. The label is only
	// touched when the number changes, not every frame.
	private void UpdateAmmo()
	{
		int ammo = _player.Gun.Data == null || _player.IsDead ? -1 : _player.Gun.Ammo;
		if (ammo == _shownAmmo)
			return;
		_shownAmmo = ammo;
		_ammo.Text = ammo < 0 ? "" : ammo > 0 ? ammo.ToString() : "empty";
		_ammo.Modulate = ammo == 0 ? new Color(1f, 0.35f, 0.35f) : Colors.White;
	}

	private void OnEnemyDied()
	{
		if (GetTree().GetNodesInGroup("enemies").All(node => ((Enemy)node).IsDead))
			ShowMessage("FLOOR CLEAR\nrent collected");
	}

	private void ShowMessage(string text)
	{
		_message.Text = text;
		_message.Visible = true;
	}
}

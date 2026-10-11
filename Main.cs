using Godot;

namespace RentDay;

// The top of the game. Holds the current level and the on-screen messages,
// and handles restarting.
public partial class Main : Node2D
{
	private Label _message;

	public override void _Ready()
	{
		_message = GetNode<Label>("HUD/Message");
		_message.Visible = false;

		// Groups are tags set in the scenes: everyone tagged "player" / "enemies".
		var player = (Player)GetTree().GetFirstNodeInGroup("player");
		player.Died += OnPlayerDied;

		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
			((Enemy)node).Died += OnEnemyDied;
	}

	public override void _Process(double delta)
	{
		// R restarts the whole floor instantly, Hotline Miami style.
		if (Input.IsActionJustPressed("restart"))
			GetTree().ReloadCurrentScene();
	}

	private void OnPlayerDied()
	{
		ShowMessage("YOU'RE DEAD\npress R");
	}

	private void OnEnemyDied()
	{
		foreach (Node node in GetTree().GetNodesInGroup("enemies"))
		{
			if (!((Enemy)node).IsDead)
				return;
		}
		ShowMessage("FLOOR CLEAR\nrent collected");
	}

	private void ShowMessage(string text)
	{
		_message.Text = text;
		_message.Visible = true;
	}
}

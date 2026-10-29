using Godot;

namespace RentDay;

// A spent casing: flies out of the right side of the gun, spins, skids to a stop and stays on the floor.
public partial class Shell : Node2D
{
	private const float Friction = 600f; // pixels per second, per second

	private Vector2 _velocity;
	private float _spin;
	private Color _color;

	public static void Eject(Node from, Vector2 at, float aim, Color color)
	{
		var shell = new Shell
		{
			ZIndex = 2, // on the floor, under the living
			Rotation = GD.Randf() * Mathf.Tau,
			_color = color,
			_velocity = Vector2.Right.Rotated(aim + Mathf.Pi / 2f + (float)GD.RandRange(-0.4, 0.4)) * (float)GD.RandRange(70.0, 140.0),
			_spin = (float)GD.RandRange(-25.0, 25.0),
		};
		from.GetTree().CurrentScene.AddChild(shell);
		shell.GlobalPosition = at;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		Position += _velocity * dt;
		Rotation += _spin * dt;
		_velocity = _velocity.MoveToward(Vector2.Zero, Friction * dt);
		_spin = Mathf.MoveToward(_spin, 0f, 60f * dt);
		if (_velocity == Vector2.Zero)
			SetProcess(false); // lying still from now on
	}

	public override void _Draw()
	{
		DrawRect(new Rect2(-2f, -1f, 4f, 2f), Style.Ink);
		DrawRect(new Rect2(-1f, -0.5f, 2f, 1f), _color);
	}
}

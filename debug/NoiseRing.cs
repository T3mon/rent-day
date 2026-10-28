using Godot;

namespace RentDay;

// Dev settings, "show enemy info": a ring that briefly shows how far a gunshot carried.
public partial class NoiseRing : Node2D
{
	private float _radius;
	private float _life = 0.6f;

	public static void Show(Node from, Vector2 where, float radius)
	{
		var ring = new NoiseRing { _radius = radius, ZIndex = 7 };
		from.GetTree().CurrentScene.AddChild(ring);
		ring.GlobalPosition = where;
	}

	public override void _Process(double delta)
	{
		_life -= (float)delta;
		if (_life <= 0f)
			QueueFree();
		QueueRedraw();
	}

	public override void _Draw()
	{
		DrawArc(Vector2.Zero, _radius, 0f, Mathf.Tau, 64, new Color(0.4f, 0.8f, 1f, _life / 0.6f), 1.5f);
	}
}

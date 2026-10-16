using System.Linq;
using Godot;

namespace RentDay;

// Stairs; the way up is the scene's up. Stairs that lead up are the floor's exit: shut while
// any goon on the floor is alive (knocked-down ones too), then an arrow bobs over them, and
// walking onto them takes you to the next floor (Main.NextFloor).
public partial class Stairs : Area2D
{
	[Export] public bool LeadsUp = true; // false: the stairs you came up by, just for show

	private static readonly Color Pink = new(1f, 0.17f, 0.84f);
	private static readonly Color Ink = new(0.04f, 0.02f, 0.06f);
	private static readonly Vector2[] Arrow = [new(0, -10), new(7, -3), new(3, -3), new(3, 6), new(-3, 6), new(-3, -3), new(-7, -3)];

	private bool _open;
	private float _time;

	public override void _PhysicsProcess(double delta)
	{
		if (!LeadsUp)
			return;
		_open = GetTree().GetNodesInGroup("enemies").OfType<Enemy>().All(goon => goon.IsDead);
		if (!_open)
			return;
		_time += (float)delta;
		QueueRedraw();
		if (GetOverlappingBodies().Any(body => body is Player { IsDead: false }) && GetTree().CurrentScene is Main main)
			main.NextFloor();
	}

	public override void _Draw()
	{
		if (!_open)
			return;
		Vector2 bob = new(0f, -3f * Mathf.Sin(_time * 8f));
		Vector2[] arrow = Arrow.Select(point => point + bob).ToArray();
		DrawColoredPolygon(arrow, Pink);
		DrawPolyline([.. arrow, arrow[0]], Ink, 1f);
	}
}

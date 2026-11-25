using Godot;

namespace RentDay;

// A drinker's beer bottle, thrown at you when he spots you: it flies a short way, spinning, and smashes on the
// floor (or on a wall or door in its way), leaving amber glass shards. Only a show: it hurts nobody.
public partial class Bottle : Node2D
{
	private const float Speed = 260f;     // pixels per second
	private const float FlightTime = 0.3f; // seconds before it hits the floor (so about 80 px)
	// The same amber beer bottle he holds (tools/anim/anims.py, 'bottle').
	private static readonly Color Amber = Color.Color8(168, 88, 22), Dark = Color.Color8(104, 52, 14),
		Shine = Color.Color8(240, 182, 92), Label = Color.Color8(232, 214, 168), Cap = Color.Color8(200, 200, 196);
	private static AudioStream _smash;

	private Vector2 _velocity;
	private float _left = FlightTime;

	public static void Throw(Node from, Vector2 at, Vector2 toward)
	{
		var bottle = new Bottle
		{
			ZIndex = 5, // in the air, over everyone
			Rotation = GD.Randf() * Mathf.Tau,
			_velocity = at.DirectionTo(toward).Rotated((float)GD.RandRange(-0.15, 0.15)) * Speed,
		};
		from.GetTree().CurrentScene.AddChild(bottle);
		bottle.GlobalPosition = at;
	}

	public override void _Process(double delta)
	{
		float dt = (float)delta;
		Vector2 step = _velocity * dt;
		var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, GlobalPosition + step, Layers.World | Layers.Doors);
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		_left -= dt;
		if (hit.Count > 0)
		{
			GlobalPosition = (Vector2)hit["position"] - _velocity.Normalized() * 2f; // smashed on the wall, this side of it
			Smash();
			return;
		}
		GlobalPosition += step;
		Rotation += 20f * dt;
		Scale = Vector2.One * (1f + 0.3f * Mathf.Sin(Mathf.Pi * (1f - _left / FlightTime))); // up in an arc and back down
		if (_left <= 0f)
			Smash();
	}

	private void Smash()
	{
		_smash ??= GD.Load<AudioStream>("res://audio/glass_shatter.tres");
		var sound = new AudioStreamPlayer2D { Stream = _smash, VolumeDb = -8f, PitchScale = (float)GD.RandRange(1.3, 1.5) }; // a small bottle, not a pane
		GetParent().AddChild(sound);
		sound.GlobalPosition = GlobalPosition;
		sound.Finished += sound.QueueFree;
		sound.Play();
		for (int i = 0; i < 5; i++)
			Shell.Eject(this, GlobalPosition, GD.Randf() * Mathf.Tau, i < 3 ? Amber : Shine);
		QueueFree();
	}

	public override void _Draw()
	{
		// 11 px from its bottom (-5) to its cap, outlined: body three wide, label, shoulder, neck one wide.
		DrawRect(new Rect2(-6f, -2.5f, 9f, 5f), Style.Ink);
		DrawRect(new Rect2(3f, -1.5f, 4f, 3f), Style.Ink);
		DrawRect(new Rect2(-5f, -1.5f, 6f, 3f), Amber);
		DrawRect(new Rect2(-5f, 0.5f, 6f, 1f), Dark);
		DrawRect(new Rect2(-4f, -1.5f, 1f, 1f), Shine);
		DrawRect(new Rect2(-1f, -1.5f, 3f, 1f), Shine);
		DrawRect(new Rect2(-3f, -1.5f, 2f, 3f), Label);
		DrawRect(new Rect2(1f, -1.5f, 1f, 2f), Amber);
		DrawRect(new Rect2(2f, -0.5f, 3f, 1f), Amber);
		DrawRect(new Rect2(4f, -0.5f, 1f, 1f), Dark);
		DrawRect(new Rect2(5f, -0.5f, 1f, 1f), Cap);
	}
}

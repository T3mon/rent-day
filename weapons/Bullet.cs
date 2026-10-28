using Godot;

namespace RentDay;

// A single bullet. It flies straight in the direction it was fired and
// disappears when it hits something, or after a while if it never does.
// An Area2D is not solid itself; it just reports what it overlaps.
// Who it can hit is decided by its CollisionMask, which the Gun sets.
public partial class Bullet : Area2D
{
	[Export] public float Speed = 700f;    // pixels per second
	[Export] public float Lifetime = 1.5f; // seconds before it removes itself
	[Export] public PackedScene SparkScene; // sparks when it hits a wall
	[Export] public float DoorShove = 190f; // degrees per second it adds to a door's swing; two quick shots slam
	public bool Blast; // one pellet of a shotgun blast: a human shield can't stop it

	private const int Tracer = 24;          // slow motion: pixels of fading tail behind it
	private static readonly Color TracerHot = new(1f, 0.62f, 0.11f), TracerCool = new(1f, 0.35f, 0.16f);
	private float _flown;                   // pixels so far: the tail never reaches back past the muzzle
	private bool _traced;                   // the tail is drawn (cleared again once slow motion ends)

	public override void _Ready()
	{
		// Signal: "call Hit whenever a solid body touches me".
		BodyEntered += Hit;
	}

	public override void _PhysicsProcess(double delta)
	{
		// Transform.X is the direction the bullet faces (rotation 0 = right).
		Vector2 step = GlobalTransform.X * Speed * (float)delta;

		// Check the whole stretch it flies this frame, or it could skip right over a thin door.
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(
			PhysicsRayQueryParameters2D.Create(GlobalPosition, GlobalPosition + step, CollisionMask));
		if (hit.Count > 0)
		{
			GlobalPosition = (Vector2)hit["position"];
			Hit((Node2D)hit["collider"]);
			return;
		}
		GlobalPosition += step;
		_flown += step.Length();
		if (SlowMotion.On || _traced)
			QueueRedraw();

		Lifetime -= (float)delta;
		if (Lifetime <= 0)
			QueueFree();
	}

	// Slow motion: a warm tracer behind it, orange to red, fading out (the picture is 8 px long, centred).
	public override void _Draw()
	{
		_traced = SlowMotion.On;
		if (!_traced)
			return;
		int tail = Mathf.Min(Tracer, (int)_flown);
		for (int i = 0; i < tail; i++)
		{
			Color colour = i < 10 ? TracerHot : TracerCool;
			colour.A = 0.8f * Mathf.Pow(1f - (float)i / Tracer, 1.5f);
			DrawRect(new Rect2(-5 - i, -1, 1, 2), colour);
		}
	}

	public void Hit(Node2D body)
	{
		if (IsQueuedForDeletion())
			return; // already hit something this frame

		// Anyone killable (player or enemy) dies, unless an enemy held up in front takes it; a wall just stops
		// the bullet, a door also gets pushed.
		if (body is Character target)
		{
			if (!(target is Player holder && holder.ShieldTakes(Transform.X, Blast)))
				target.Die(Transform.X);
		}
		else
			Effects.Spawn(SparkScene, this, GlobalPosition, Rotation + Mathf.Pi); // sparks fly back off the wall

		if (body.GetParent() is Door door)
			door.Shove(Transform.X, DoorShove, byPlayer: (CollisionMask & Layers.Enemies) != 0); // only your bullets hit enemies

		// QueueFree removes the node from the game at the end of this frame.
		QueueFree();
	}
}

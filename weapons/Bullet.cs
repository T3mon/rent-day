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

	public override void _Ready()
	{
		// Signal: "call Hit whenever a solid body touches me".
		BodyEntered += Hit;
	}

	public override void _PhysicsProcess(double delta)
	{
		// Transform.X is the direction the bullet faces (rotation 0 = right).
		// shortcut: only checks where the bullet lands each frame, so a bullet moving more than ~14 px
		// per frame could skip an 8 px wall; sweep a ray from the last position if bullets get faster.
		Position += Transform.X * Speed * (float)delta;

		Lifetime -= (float)delta;
		if (Lifetime <= 0)
			QueueFree();
	}

	public void Hit(Node2D body)
	{
		// Anyone killable (player or goon) dies; a wall just stops the bullet.
		if (body is Character target)
			target.Die(Transform.X);
		else
			Effects.Spawn(SparkScene, this, GlobalPosition, Rotation + Mathf.Pi); // sparks fly back off the wall

		// QueueFree removes the node from the game at the end of this frame.
		QueueFree();
	}
}

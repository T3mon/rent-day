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

	public override void _Ready()
	{
		// Signal: "call OnBodyEntered whenever a solid body touches me".
		BodyEntered += OnBodyEntered;
	}

	public override void _PhysicsProcess(double delta)
	{
		// Transform.X is the direction the bullet faces (rotation 0 = right).
		Position += Transform.X * Speed * (float)delta;

		Lifetime -= (float)delta;
		if (Lifetime <= 0)
			QueueFree();
	}

	private void OnBodyEntered(Node2D body)
	{
		// Anyone killable (player or goon) dies; a wall just stops the bullet.
		if (body is IKillable target)
			target.Die(Transform.X);

		// QueueFree removes the node from the game at the end of this frame.
		QueueFree();
	}
}

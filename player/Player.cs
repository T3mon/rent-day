using Godot;

namespace RentDay;

// The rent collector. A CharacterBody2D is a body you move from code;
// it stops at walls instead of passing through them.
public partial class Player : CharacterBody2D
{
	// [Export] shows these in the Inspector, so you can tune them without touching code.
	[Export] public float MaxSpeed = 160f;      // pixels per second
	[Export] public float Acceleration = 1600f; // how quickly we reach MaxSpeed
	[Export] public float Friction = 2000f;     // how quickly we stop when no key is held

	// Godot calls this 60 times per second, in step with the physics engine.
	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta; // seconds since the last call (about 0.0167)

		// WASD -> a direction. Right = (1, 0), up-left = (-0.7, -0.7), nothing held = (0, 0).
		Vector2 direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");

		// Ease toward full speed in that direction, or ease to a stop.
		Vector2 target = direction * MaxSpeed;
		float rate = direction == Vector2.Zero ? Friction : Acceleration;
		Velocity = Velocity.MoveToward(target, rate * dt);

		// Move by Velocity, sliding along anything solid instead of sticking to it.
		MoveAndSlide();
	}
}

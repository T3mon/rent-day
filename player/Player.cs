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

	// The bullet blueprint to copy each time we fire. Set in the Inspector.
	[Export] public PackedScene BulletScene;
	[Export] public float FireCooldown = 0.15f; // minimum seconds between shots

	private float _cooldownLeft;

	// The child node holding everything we can see. Only this part rotates;
	// the physics body itself never spins.
	private Node2D _visual;

	// The point at the tip of the nose where bullets appear.
	private Marker2D _muzzle;

	// Godot calls this once, when the player enters the game.
	public override void _Ready()
	{
		_visual = GetNode<Node2D>("Visual");
		_muzzle = GetNode<Marker2D>("Visual/Muzzle");
	}

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

		// Turn the visuals to face the mouse cursor. Rotation 0 means facing right (+X),
		// so everything on the player that "points" should point right.
		_visual.LookAt(GetGlobalMousePosition());

		// Fire on click, but not faster than FireCooldown allows.
		_cooldownLeft -= dt;
		if (Input.IsActionJustPressed("shoot") && _cooldownLeft <= 0)
		{
			Shoot();
			_cooldownLeft = FireCooldown;
		}
	}

	private void Shoot()
	{
		// Make a fresh copy of the bullet scene...
		var bullet = BulletScene.Instantiate<Bullet>();

		// ...and add it to the level, NOT to the player. As a child of the player,
		// it would be dragged along whenever the player moves.
		GetParent().AddChild(bullet);

		// Start at the muzzle, facing where the player is aiming.
		bullet.GlobalPosition = _muzzle.GlobalPosition;
		bullet.GlobalRotation = _visual.GlobalRotation;
	}
}

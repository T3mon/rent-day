using Godot;

namespace RentDay;

// The rent collector, driven by WASD and the mouse.
// Dying, the gun and the alive/corpse looks come from Character.
public partial class Player : Character
{
	// [Export] shows these in the Inspector, so you can tune them without touching code.
	[Export] public float MaxSpeed = 200f;      // pixels per second
	[Export] public float Acceleration = 2600f; // how quickly we reach MaxSpeed
	[Export] public float Friction = 3000f;     // how quickly we stop when no key is held

	// The camera leans toward the mouse, so you see further where you're aiming.
	[Export] public float LookAhead = 0.35f;    // 0 = camera stays on you, 0.5 = halfway to the mouse
	[Export] public float MaxLookAhead = 90f;   // pixels

	[Export] public float PickupRange = 20f;    // how close a gun on the floor must be to grab it
	[Export] public float ThrowSpeed = 600f;    // pixels per second

	private Camera2D _camera;

	public override void _Ready()
	{
		StartWeapon = DevSettings.Current.StartWeapon ?? StartWeapon; // testing: start holding a gun
		base._Ready();
		_camera = GetNode<Camera2D>("Camera");
	}

	// Keep the camera inside the level, so you never see past its edges.
	public void SetCameraLimits(Rect2I area)
	{
		_camera.LimitLeft = area.Position.X;
		_camera.LimitTop = area.Position.Y;
		_camera.LimitRight = area.End.X;
		_camera.LimitBottom = area.End.Y;
	}

	// Godot calls this 60 times per second, in step with the physics engine.
	public override void _PhysicsProcess(double delta)
	{
		if (IsDead)
			return;

		float dt = (float)delta;

		// WASD -> a direction. Right = (1, 0), up-left = (-0.7, -0.7), nothing held = (0, 0).
		Vector2 direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");

		// Ease toward full speed in that direction, or ease to a stop.
		Vector2 target = direction * MaxSpeed;
		float rate = direction == Vector2.Zero ? Friction : Acceleration;
		Velocity = Velocity.MoveToward(target, rate * dt);

		// Move by Velocity, sliding along walls instead of sticking to them.
		MoveAndSlide();

		// Only the visuals turn toward the mouse; the physics body never spins.
		Vector2 mouse = GetGlobalMousePosition();
		Vector2 aim = (mouse - GlobalPosition).Normalized();
		Visual.LookAt(mouse);
		_camera.Position = ((mouse - GlobalPosition) * LookAhead).LimitLength(MaxLookAhead);

		// Single-shot guns fire once per click; automatic ones keep firing while the button is held.
		bool trigger = Gun.Data is { Automatic: true }
			? Input.IsActionPressed("shoot")
			: Input.IsActionJustPressed("shoot");
		if (HasWeapon && trigger && Gun.TryFire())
			Velocity -= aim * Gun.Data.Kickback;

		// Grab: pick up the gun at your feet (dropping yours there), or throw yours if there's none.
		if (Input.IsActionJustPressed("grab"))
		{
			WeaponPickup gunAtFeet = WeaponPickup.Nearest(GetTree(), GlobalPosition, PickupRange);
			if (gunAtFeet != null)
			{
				DropWeapon(Vector2.Zero);
				PickUp(gunAtFeet);
			}
			else if (HasWeapon)
			{
				DropWeapon(aim * ThrowSpeed + Velocity, thrown: true);
			}
		}
	}
}

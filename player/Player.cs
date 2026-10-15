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
	// 0 = camera stays on you, 1 = it moves as far as the cursor is from the screen center.
	[Export] public float LookAhead = 0.5f;
	[Export] public float ShiftLookAhead = 1f;  // while holding "look" (Shift)

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

		// Lean by the cursor's spot on the screen, not in the world: the world spot moves with
		// the camera, so the lean would feed on itself.
		Vector2 screen = GetViewportRect().Size;
		Vector2 fromCenter = GetViewport().GetMousePosition() - screen / 2;
		float lean = Input.IsActionPressed("look") ? ShiftLookAhead : LookAhead;
		Vector2 limit = screen / 2 - new Vector2(24, 24); // you stay at least 24 px inside the screen
		_camera.Position = (fromCenter * lean).Clamp(-limit, limit);

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

using System.Linq;
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

	// Execute: finish off a goon lying on the floor. Bare fists (or holding a gun, which you drop):
	// a few blows, one click each (a goon slumped against a wall: kicks to the head). A bat: its own
	// blows, one click each, and you keep it. A knife: one slow cut across the throat, no clicks,
	// and you keep it.
	[Export] public float ExecuteRange = 32f;   // how close a downed goon must be
	[Export] public float BlowTime = 0.2f;      // seconds per blow; clicks during a blow don't count
	[Export] public AudioStream[] BlowCracks = []; // fists: lightest first, every blow cracks harder, the last one before the kill hardest
	[Export] public float CutTime = 0.78f;      // knife: seconds the whole cut takes
	[Export] public float CutAt = 0.44f;        // knife: when it cuts

	// Sneak kill: the same key on a goon standing right in front of you takes him out, no clicks.
	[Export] public float SneakKillReach = 28f; // how far in front of you he may stand
	[Export] public float SneakKillTime = 0.7f; // seconds it takes; you're stuck meanwhile

	// Human shield ("shield", E): pick a downed goon up and hold him in front of you, whatever is in your
	// hand. He takes goon bullets from the front; guns still fire past him, melee can't swing.
	// E again: a neck snap, or, once he's shot up, you just drop him. Right-click: shove him away (Enemy.Throw).
	[Export] public int ShieldHits = 5;         // bullets he takes before he dies
	[Export] public int ShieldDropAfter = 3;    // hits after which letting go just drops him
	[Export] public float ShieldSpeed = 0.67f;  // your top speed holding him, part of MaxSpeed
	[Export] public float ShieldTurn = 360f;    // degrees per second you can turn holding him
	[Export] public float ShieldFront = 120f;   // the angle he covers, centered on where you face
	[Export] public float SnapTime = 0.3f;      // seconds the neck snap takes; you stand still meanwhile
	[Export] public float LiftTime = 0.25f;     // seconds picking him up off the floor; you stand still meanwhile
	[Export] public float ShieldThrowSpeed = 450f; // shoving him away: how fast he flies, pixels per second (about 7 tiles)

	private const float ShieldAhead = 12f;     // how far in front of you he's held
	private static readonly Vector2 ShieldGunHand = new(15f, 11f); // the gun out past his right shoulder, not over his head
	private static readonly AudioStream NeckSnap = GD.Load<AudioStream>("res://audio/neck_snap.wav");
	private static readonly AudioStream Whoosh = GD.Load<AudioStream>("res://audio/melee/swoosh.tres");

	private Camera2D _camera;
	private AudioStreamPlayer2D _blowSound, _finalBlowSound, _grabSound, _struggleSound, _bodyFallSound;
	private Enemy _victim; // the goon we're finishing off, if any
	private WeaponData _finisher; // the bat or knife finishing him; null = fists
	private float _cutAge;
	private int _blowsLeft;
	private float _blowLeft;
	private float _sneakKillLeft;
	private float _nextStruggle;
	private Enemy _shield;        // the goon held up in front, if any
	private int _shieldHits;
	private float _snapLeft, _liftLeft;
	private Vector2 _liftFrom;
	private Vector2 _gunRest;      // the gun hand's usual spot
	private ulong _shieldGoneFrame; // the physics frame a blast killed him in: the rest of that blast is his too

	public override void _Ready()
	{
		StartWeapon = DevSettings.Current.StartWeapon ?? StartWeapon; // testing: start holding a gun
		base._Ready();
		_camera = GetNode<Camera2D>("Camera");
		_blowSound = GetNode<AudioStreamPlayer2D>("BlowSound");
		_finalBlowSound = GetNode<AudioStreamPlayer2D>("FinalBlowSound");
		_grabSound = GetNode<AudioStreamPlayer2D>("GrabSound");
		_struggleSound = GetNode<AudioStreamPlayer2D>("StruggleSound");
		_bodyFallSound = GetNode<AudioStreamPlayer2D>("BodyFallSound");
		_gunRest = Gun.Position;
		Died += () => DropShield(Visual.Transform.X); // shot from behind: he goes down with you
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
		UpdateSwing(dt);
		if (_victim != null)
		{
			Execute(dt);
			return;
		}
		if (_snapLeft > 0f)
		{
			Snap(dt);
			return;
		}
		if (_liftLeft > 0f)
		{
			Lift(dt);
			return;
		}

		// WASD -> a direction. Right = (1, 0), up-left = (-0.7, -0.7), nothing held = (0, 0).
		Vector2 direction = Input.GetVector("move_left", "move_right", "move_up", "move_down");

		// Ease toward full speed in that direction, or ease to a stop.
		Vector2 target = direction * MaxSpeed * (_shield != null ? ShieldSpeed : 1f);
		float rate = direction == Vector2.Zero ? Friction : Acceleration;
		Velocity = Velocity.MoveToward(target, rate * dt);

		// Move by Velocity, sliding along walls instead of sticking to them.
		MoveAndSlide();

		// Only the visuals turn toward the mouse; the physics body never spins. Holding a shield, slowly.
		float toMouse = (GetGlobalMousePosition() - GlobalPosition).Angle();
		Visual.Rotation = _shield != null ? Mathf.RotateToward(Visual.Rotation, toMouse, Mathf.DegToRad(ShieldTurn) * dt) : toMouse;
		Vector2 aim = Vector2.Right.Rotated(Visual.Rotation);
		_shield?.Carry(GlobalPosition + aim * ShieldAhead, Visual.Rotation, Velocity);

		// Lean by the cursor's spot on the screen, not in the world: the world spot moves with
		// the camera, so the lean would feed on itself.
		Vector2 screen = GetViewportRect().Size;
		Vector2 fromCenter = GetViewport().GetMousePosition() - screen / 2;
		float lean = Input.IsActionPressed("look") ? ShiftLookAhead : LookAhead;
		Vector2 limit = screen / 2 - new Vector2(24, 24); // you stay at least 24 px inside the screen
		_camera.Position = (fromCenter * lean).Clamp(-limit, limit);

		// Guns shoot, bats and knives swing, empty hands punch. Single-shot guns fire once per
		// click; automatic guns and swings keep going while the button is held. Holding a shield,
		// guns still fire, melee can't swing.
		bool hands = _shield == null;
		bool swings = Gun.Data is null or { Melee: true };
		bool trigger = Gun.Data is { Automatic: true } || swings
			? Input.IsActionPressed("shoot")
			: Input.IsActionJustPressed("shoot");
		if (trigger && swings && hands)
			TrySwing();
		else if (trigger && !swings && Gun.TryFire())
			Velocity -= aim * Gun.Data.Kickback;

		if (Input.IsActionJustPressed("shield"))
		{
			if (hands)
				GrabShield();
			else
				LetGoOfShield();
		}
		if (!hands)
		{
			if (Input.IsActionJustPressed("grab"))
				ThrowShield();
			return; // no picking up or executing with him in your arms
		}

		// Grab: throw yours toward the mouse, and pick up the gun at your feet if there is one.
		if (Input.IsActionJustPressed("grab"))
		{
			WeaponPickup gunAtFeet = WeaponPickup.Nearest(GetTree(), GlobalPosition, PickupRange);
			if (HasWeapon)
				DropWeapon(aim * ThrowSpeed + Velocity, thrown: true);
			if (gunAtFeet != null)
				PickUp(gunAtFeet);
		}

		if (Input.IsActionJustPressed("execute"))
			StartExecution();
	}

	// Grab a goon standing right in front of you, or else kneel on the nearest downed goon in reach.
	// A bat or knife finishes a downed goon itself; anything else in your hand is dropped beside you.
	private void StartExecution()
	{
		var goons = GetTree().GetNodesInGroup("enemies").OfType<Enemy>().Where(goon => !goon.IsDead).ToList();
		_victim = goons.FirstOrDefault(goon => !goon.IsDown && InSneakKillReach(goon))
			?? goons.Where(goon => goon.IsDown && goon.GlobalPosition.DistanceTo(GlobalPosition) <= ExecuteRange)
				.MinBy(goon => goon.GlobalPosition.DistanceTo(GlobalPosition));
		if (_victim == null)
			return;

		if (_victim.GlobalPosition != GlobalPosition)
			Visual.Rotation = (_victim.GlobalPosition - GlobalPosition).Angle();
		Vector2 facing = Visual.GlobalTransform.X;
		Vector2 aside = facing.Rotated(Mathf.Pi / 2f);
		_finisher = _victim.IsDown && Gun.Data is { Melee: true } ? Gun.Data : null;
		if (_finisher == null)
			DropWeapon(aside * 120f);
		_victim.Pinned = true;
		_victim.Velocity = Vector2.Zero;
		Velocity = Vector2.Zero;
		_blowLeft = 0f;

		if (_victim.Slumped)
		{
			// Step up to him where he sits against the wall.
			Vector2 front = _victim.GlobalPosition - facing * 14f;
			CreateTween().TweenProperty(this, "global_position", front, 0.08);
			_blowsLeft = _finisher?.FinishBlows ?? GD.RandRange(2, 5);
			_cutAge = 0f;
			_sneakKillLeft = 0f;
		}
		else if (_victim.IsDown)
		{
			// Hop onto him and crouch over him.
			CreateTween().TweenProperty(this, "global_position", _victim.GlobalPosition, 0.08);
			Visual.Scale = Vector2.One * 0.9f;
			_bodyFallSound.Play();
			ShakeCamera.Shake(1f);
			_blowsLeft = _finisher?.FinishBlows ?? GD.RandRange(2, 5);
			_cutAge = 0f;
			_sneakKillLeft = 0f;
		}
		else
		{
			// Grab him and pull him in tight; his gun flies out of his hands.
			_victim.DropWeapon(-aside * 150f);
			CreateTween().TweenProperty(_victim, "global_position", GlobalPosition + facing * 12f, 0.08);
			_grabSound.Play();
			ShakeCamera.Shake(1f);
			HitStop.Freeze(this, 0.04f);
			_sneakKillLeft = SneakKillTime;
			_nextStruggle = 0.08f;
			Act("choke");
		}
		if (_finisher is { FinishBlows: 0 })
			Act("fin_knife"); // the whole cut, timed to CutAt and CutTime
	}

	// Pick the nearest downed goon in reach up off the floor and hold him in front of you.
	private void GrabShield()
	{
		_shield = GetTree().GetNodesInGroup("enemies").OfType<Enemy>()
			.Where(goon => goon.IsDown && !goon.IsDead && goon.GlobalPosition.DistanceTo(GlobalPosition) <= ExecuteRange)
			.MinBy(goon => goon.GlobalPosition.DistanceTo(GlobalPosition));
		if (_shield == null)
			return;
		_shield.Pinned = true;
		_shield.Velocity = Vector2.Zero;
		_shieldHits = 0;
		_liftLeft = LiftTime;
		_liftFrom = _shield.GlobalPosition;
		_shield.LiftUp(LiftTime, Visual.Rotation);
		Act("shield_grab");
		_grabSound.Play();
	}

	// Reach down, grip, pull: he comes up off the floor into your arms.
	private void Lift(float dt)
	{
		Velocity = Vector2.Zero;
		_liftLeft = Mathf.Max(_liftLeft - dt, 0f);
		Vector2 held = GlobalPosition + Visual.Transform.X * ShieldAhead;
		_shield.Carry(_liftFrom.Lerp(held, 1f - _liftLeft / LiftTime), Visual.Rotation, Vector2.Zero);
		if (_liftLeft > 0f)
			return;
		_shield.HoldUp();
		HoldPose = ShieldPose;
		Gun.Position = ShieldGunHand;
		ShakeCamera.Shake(1f);
	}

	// E again: snap his neck, or, once he's shot up, just let the body drop.
	private void LetGoOfShield()
	{
		if (_shieldHits >= ShieldDropAfter)
		{
			DropShield(Visual.Transform.X);
			return;
		}
		_snapLeft = SnapTime;
		HoldPose = null;
		Gun.Position = _gunRest;
		Act("snap");
		_shield.Flinch(Visual.Transform.Y, 1.5f, bleed: false);
		_blowSound.Stream = NeckSnap; // its cracks run through the twist
		_blowSound.PitchScale = 1f;
		_blowSound.Play();
	}

	// Right-click holding him: shove him the way you face.
	private void ThrowShield()
	{
		Release().Throw(Visual.Transform.X * ShieldThrowSpeed);
		Act("shove");
		_blowSound.Stream = Whoosh;
		_blowSound.PitchScale = 1f;
		_blowSound.Play();
		ShakeCamera.Shake(1f);
	}

	// Standing still for the snap; he drops at the end.
	private void Snap(float dt)
	{
		Velocity = Vector2.Zero;
		_snapLeft -= dt;
		if (_snapLeft > 0f)
			return;
		Act(null);
		ShakeCamera.Shake(1f);
		DropShield(Visual.Transform.X);
	}

	// He's dead either way: falls the way you face, face down, no goo.
	private void DropShield(Vector2 facing)
	{
		if (_shield != null)
			Release().Die(facing, lethal: false);
	}

	// You're no longer holding him, whatever happens to him next.
	private Enemy Release()
	{
		Enemy body = _shield;
		_shield = null;
		_snapLeft = _liftLeft = 0f;
		HoldPose = null;
		Gun.Position = _gunRest;
		return body;
	}

	// A goon's bullet reached you. From the front, the goon you hold takes it: he jolts and bleeds, and
	// dies after ShieldHits (a shotgun blast at once, and he takes all of it). Returns whether he took it.
	public bool ShieldTakes(Vector2 bulletDirection, bool blast)
	{
		bool front = Mathf.Abs(Mathf.AngleDifference(Visual.Rotation, (-bulletDirection).Angle())) <= Mathf.DegToRad(ShieldFront / 2f);
		if (_shield == null || IsDead)
			return front && blast && Engine.GetPhysicsFrames() == _shieldGoneFrame;
		if (!front)
			return false;
		if (blast || ++_shieldHits >= ShieldHits)
		{
			Act(null);
			_shieldGoneFrame = Engine.GetPhysicsFrames();
			Release().Die(bulletDirection);
		}
		else
			_shield.Flinch(bulletDirection, 2f, bleed: true);
		return true;
	}

	// Measured along where you aim, like a reach in front of your chest.
	private bool InSneakKillReach(Enemy goon)
	{
		Vector2 ahead = (goon.GlobalPosition - GlobalPosition).Rotated(-Visual.Rotation);
		return ahead.X > 0f && ahead.X <= SneakKillReach && Mathf.Abs(ahead.Y) <= 16f;
	}

	// Stuck on him until he's done, and goons can still shoot you meanwhile.
	private void Execute(float dt)
	{
		_blowLeft = Mathf.Max(_blowLeft - dt, 0f);
		Vector2 facing = Visual.Transform.X;

		if (_sneakKillLeft > 0f)
		{
			// Choking him out, no clicks needed: he struggles, weaker and weaker, then goes limp. No blood.
			Visual.Position = facing * 3f;
			_sneakKillLeft -= dt;
			_nextStruggle -= dt;
			if (_nextStruggle <= 0f && _sneakKillLeft > 0f)
			{
				_nextStruggle = 0.1f;
				_victim.Flinch(Vector2.Right.Rotated(GD.Randf() * Mathf.Tau), 0.5f + 2.5f * _sneakKillLeft / SneakKillTime, bleed: false);
				if (!_struggleSound.Playing)
					_struggleSound.Play();
			}
			if (_sneakKillLeft > 0f)
				return;
			_victim.Die(facing, lethal: false);
			Act(null);
			_bodyFallSound.Play();
			ShakeCamera.Shake(1f);
		}
		else if (_finisher is { FinishBlows: 0 })
		{
			// A knife, no clicks: a moment on him, then one cut across the throat, blood squirting
			// out to the side.
			bool cuts = _cutAge < CutAt && _cutAge + dt >= CutAt;
			_cutAge += dt;
			if (cuts)
			{
				_blowSound.Stream = _finisher.HitSound;
				_blowSound.PitchScale = 1f;
				_blowSound.Play();
				ShakeCamera.Shake(1f);
				_victim.Flinch(facing.Rotated(-Mathf.Pi / 2f), 2f, bleed: true);
			}
			if (_cutAge < CutTime)
				return;
			_victim.Die(facing);
			HoldSwings(0.5f);
		}
		else
		{
			// Each click lands a blow, harder every time: a punch, a kick to the head of a goon
			// slumped against a wall, or the bat swung down on him.
			if (_blowLeft > 0f || !Input.IsActionJustPressed("shoot"))
				return;
			_blowLeft = BlowTime;
			Act(_finisher != null ? "fin_bat" : _victim.Slumped ? "kick" : "fin_fists");
			if (--_blowsLeft > 0)
			{
				if (_finisher != null)
					_blowSound.Stream = _finisher.HitSound;
				else if (BlowCracks.Length > 0)
					_blowSound.Stream = BlowCracks[Mathf.Max(BlowCracks.Length - _blowsLeft, 0)]; // climbs to the hardest right before the kill
				_blowSound.PitchScale = (float)GD.RandRange(0.93, 1.07);
				_blowSound.Play();
				ShakeCamera.Shake(_finisher != null ? 1.5f : 1f + 0.3f * (5 - _blowsLeft));
				HitStop.Freeze(this, 0.04f);
				_victim.Flinch(facing, 2f, bleed: true);
				return;
			}
			if (_finisher != null)
			{
				_blowSound.Stream = _finisher.HitSound;
				_blowSound.Play();
				HoldSwings(0.17f);
			}
			else
				_finalBlowSound.Play();
			_victim.Die(facing); // the big splat, shake and freeze
		}
		Visual.Position = Vector2.Zero;
		Visual.Scale = Vector2.One;
		_victim = null;
	}
}

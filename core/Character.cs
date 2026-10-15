using System.Linq;
using Godot;

namespace RentDay;

// Everything the player and the goons have in common: a body that faces where
// it aims, legs that walk where it moves, a gun (or empty hands), and death
// from a single hit. Player and Enemy build on this.
//
// Expected children:
//   Legs              - walking legs, turn toward the direction of movement
//   Visual            - rotates to face where we aim
//     Alive/Gun       - the gun in the hand, drawn under the body
//     Alive/Sprite    - the standing body
//     Corpse/Body     - the dead look, hidden until we die
//   CollisionShape2D
//   DeathSound
//
// Draw order (z index): floor 0, goo 1, corpses and guns on the floor 2, walls and
// furniture and doors 3 (so bodies slide under them), the living 4, bullets and effects 5.
public partial class Character : CharacterBody2D
{
	// Fired once, at the moment we die. Main listens to this.
	[Signal] public delegate void DiedEventHandler();

	// Looks. Leave a slot empty to keep what the scene shows; fill them on a
	// placed goon to give him a different outfit.
	[Export] public Texture2D ArmedLook;         // one arm out, holding whatever gun
	[Export] public Texture2D UnarmedLook;
	[Export] public Texture2D LegsLook;

	// Death poses: one is picked at random each time, so bodies don't all look alike.
	[Export] public Texture2D DeadLook;          // on the back
	[Export] public Texture2D DeadLookFaceDown;
	[Export] public Texture2D DeadLookSide;      // curled up on the side

	[Export] public WeaponData StartWeapon;      // empty = starts empty-handed

	// Seconds on the floor after a knock-down, picked at random in this range.
	[Export] public float DownTimeMin = 1.7f;
	[Export] public float DownTimeMax = 3.7f;

	// What a dropped or thrown gun becomes: a gun lying on the floor.
	[Export] public PackedScene PickupScene;

	public bool IsDead { get; private set; }

	// Knocked to the floor (by a door or a thrown gun): lying there for a moment, then gets back up.
	public bool IsDown { get; private set; }

	// Held by someone finishing him off: he can't act or get up.
	public bool Pinned { get; set; }

	public Gun Gun { get; private set; }
	public bool HasWeapon => Gun.Data != null;

	protected Node2D Visual { get; private set; }

	private const int CorpseZ = 2;
	private const int GooSteps = 8; // footprints left after stepping in goo

	// One step of the walk cycle per this many pixels travelled.
	private const float StepLength = 7f;
	private static readonly int[] WalkCycle = { 1, 0, 2, 0 };

	private Sprite2D _bodySprite;
	private Sprite2D _corpseBody;
	private Sprite2D _legs;
	private Node2D _alive;
	private Node2D _corpse;
	private CollisionShape2D _collision;
	private AudioStreamPlayer2D _deathSound;
	private float _walked;
	private int _step;
	private int _gooSteps;
	private bool _leftFoot;
	private float _downLeft;
	private int _livingZ;
	private Tween _flinch;

	public override void _Ready()
	{
		Visual = GetNode<Node2D>("Visual");
		Gun = GetNode<Gun>("Visual/Alive/Gun");
		_bodySprite = GetNode<Sprite2D>("Visual/Alive/Sprite");
		_legs = GetNode<Sprite2D>("Legs");
		_alive = GetNode<Node2D>("Visual/Alive");
		_corpse = GetNode<Node2D>("Visual/Corpse");
		_collision = GetNode<CollisionShape2D>("CollisionShape2D");
		_deathSound = GetNode<AudioStreamPlayer2D>("DeathSound");
		_corpseBody = GetNode<Sprite2D>("Visual/Corpse/Body");
		_livingZ = ZIndex;

		if (LegsLook != null)
			_legs.Texture = LegsLook;
		Equip(StartWeapon, StartWeapon?.Ammo ?? 0);
	}

	// Puts a gun in our hand (or empties it, with null) and swaps the body look to match.
	public void Equip(WeaponData data, int ammo, bool playSound = false)
	{
		Gun.Equip(data, ammo, playSound);
		Texture2D look = data != null ? ArmedLook : UnarmedLook;
		if (look != null)
			_bodySprite.Texture = look;
	}

	// Picks up a gun lying on the floor.
	public void PickUp(WeaponPickup pickup)
	{
		Equip(pickup.Data, pickup.Ammo, playSound: true);
		pickup.QueueFree();
	}

	// Lets go of our gun: it becomes a gun on the floor, sliding at `velocity`.
	// thrown = true makes it knock down the goon it hits.
	public void DropWeapon(Vector2 velocity, bool thrown = false)
	{
		if (!HasWeapon || PickupScene == null)
			return;

		var pickup = PickupScene.Instantiate<WeaponPickup>();
		pickup.Data = Gun.Data;
		pickup.Ammo = Gun.Ammo;
		pickup.AddCollisionExceptionWith(this); // don't trip over our own gun
		pickup.Position = GlobalPosition;       // the level's root sits at (0, 0), so this is the same spot
		pickup.Launch(velocity, thrown);

		// Added "deferred" (at the end of this frame): we may be in the middle of a
		// physics step here (a bullet just hit us), and new bodies can't join mid-step.
		GetTree().CurrentScene.CallDeferred(Node.MethodName.AddChild, pickup);

		Equip(null, 0);
	}

	// Runs every drawn frame: purely visual, so it doesn't need to be in step with physics.
	public override void _Process(double delta)
	{
		if (IsDead)
			return;

		if (IsDown)
		{
			// Squirming on the floor, so it's clear he isn't dead.
			if (!Pinned)
				_downLeft -= (float)delta;
			_corpseBody.Rotation = Mathf.Sin(_downLeft * 14f) * 0.08f;
			if (_downLeft <= 0)
				GetUp();
			return;
		}

		float speed = Velocity.Length();
		if (speed < 10f)
		{
			_legs.Frame = 0; // standing
			_walked = 0;
			_step = 0;
			return;
		}

		// Legs point where we're going, which may differ from where we aim.
		_legs.Rotation = Velocity.Angle();
		_walked += speed * (float)delta;
		int step = (int)(_walked / StepLength);
		_legs.Frame = WalkCycle[step % WalkCycle.Length];
		for (; _step < step; _step++) // a slow frame can cover several steps: each gets its print, back along the path
			Footstep(GlobalPosition - Velocity.Normalized() * (step - _step - 1) * StepLength);
	}

	// Walking through goo leaves a few footprints, fading out.
	private void Footstep(Vector2 at)
	{
		if (GooCanvas.IsWet(at))
			_gooSteps = GooSteps;
		if (_gooSteps <= 0)
			return;
		_leftFoot = !_leftFoot;
		Vector2 side = Vector2.Down.Rotated(_legs.Rotation) * (_leftFoot ? 2.5f : -2.5f);
		GooCanvas.Footprint(at + side, _legs.Rotation, (float)_gooSteps / GooSteps);
		_gooSteps--;
	}

	// hitDirection is the way the bullet was flying. Not lethal: knocked out cold for good
	// (choked out), so no goo and no blood, he just slumps.
	public void Die(Vector2 hitDirection, bool lethal = true)
	{
		if (IsDead)
			return;
		IsDead = true;
		Velocity = Vector2.Zero;

		// The gun falls out of our hand, a little way off in the direction of the shot.
		DropWeapon(hitDirection.Rotated((float)GD.RandRange(-1.0, 1.0)) * 90f);

		// Stop being solid, so bullets and people pass over the body.
		// "Deferred" means "after the current physics step": Godot doesn't allow
		// changing collisions in the middle of checking them.
		_collision.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);

		// Pick a random death pose (sometimes mirrored), fall the way the bullet
		// was going, and swap the standing look for the corpse.
		Texture2D[] poses = (lethal ? new[] { DeadLook, DeadLookFaceDown, DeadLookSide } : new[] { DeadLookFaceDown })
			.Where(t => t != null).ToArray();
		if (poses.Length > 0)
			_corpseBody.Texture = poses[GD.RandRange(0, poses.Length - 1)];
		_corpseBody.FlipV = GD.Randf() < 0.5f;
		_corpseBody.Rotation = 0f;
		Visual.Rotation = hitDirection.Angle();
		IsDown = false;
		ShowLying(true);

		// Goo bursts out the way the bullet went, splats, and pools under the body.
		if (lethal)
			GooCanvas.Bleed(GlobalPosition, hitDirection);

		// The body is knocked back a little in the direction of the shot.
		CreateTween()
			.TweenProperty(this, "global_position", GlobalPosition + hitDirection * 8f, 0.2)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);

		// Make the kill land: kick the screen and freeze the game for a split second.
		// A quiet takedown stays quiet: whoever did it plays its sounds.
		if (lethal)
		{
			ShakeCamera.Shake(0.6f);
			HitStop.Freeze(this);
			_deathSound.Play();
		}
		EmitSignal(SignalName.Died);
	}

	// A blow lands on us while we're held: the body jolts the way it was hit, and maybe bleeds.
	public void Flinch(Vector2 direction, float strength, bool bleed)
	{
		_flinch?.Kill();
		_flinch = CreateTween();
		_flinch.TweenProperty(Visual, "position", direction * strength, 0.03);
		_flinch.TweenProperty(Visual, "position", Vector2.Zero, 0.08);
		if (bleed)
		{
			GooCanvas.Splat(GlobalPosition, direction * 4f, (float)GD.RandRange(22.0, 32.0), GD.Randf() * Mathf.Tau);
			GooCanvas.Spray(GlobalPosition, direction, 3, 0.9f, 40f, 160f);
		}
	}

	// Knocked flat, sliding the way he was hit, and the gun flies out of his hand.
	// Still alive: he gets up after a while, unless someone finishes him off first.
	public void KnockDown(Vector2 direction)
	{
		if (IsDead || IsDown)
			return;
		IsDown = true;
		_downLeft = (float)GD.RandRange(DownTimeMin, DownTimeMax);
		Velocity = direction * 220f; // the subclass's physics slides him to a stop

		DropWeapon(Vector2.Right.Rotated(GD.Randf() * Mathf.Tau) * 160f);

		_corpseBody.Texture = DeadLookFaceDown ?? DeadLook;
		_corpseBody.FlipV = GD.Randf() < 0.5f;
		Visual.Rotation = direction.Angle();
		ShowLying(true);
	}

	private void GetUp()
	{
		IsDown = false;
		_corpseBody.Rotation = 0f;
		ShowLying(false);
	}

	// Lying on the floor (dead or knocked down) vs. standing.
	private void ShowLying(bool lying)
	{
		_alive.Visible = !lying;
		_legs.Visible = !lying;
		_corpse.Visible = lying;
		ZIndex = lying ? CorpseZ : _livingZ;
	}
}

using Godot;

namespace RentDay;

// Everything the player and the goons have in common: a body that faces where
// it aims, legs that walk where it moves, a weapon (or empty hands), melee swings,
// and death from a single hit. Player and Enemy build on this.
//
// Expected children (the three sprites all play this character's Look):
//   Legs              - walking legs, turn toward the direction of movement
//   Visual            - rotates to face where we aim
//     Alive/Gun       - the gun in the hand, drawn under the body
//     Alive/Sprite    - the standing body (see CharacterMotion for which animation plays when)
//     Corpse/Body     - lying on the floor: falling, knocked down, slumped, getting up, dead
//   CollisionShape2D
//   DeathSound
// Knocked down, the body hits the floor with a body fall, played from a player made in code.
//
// Draw order (z index): floor 0, goo 1, corpses and guns on the floor 2, furniture and
// doors 3 (so bodies slide under them), the living 4, walls 5 (over everyone, so a body
// brushing a wall never looks stuck in it), bullets and effects 6.
public partial class Character : CharacterBody2D
{
	// Fired once, at the moment we die. Main listens to this.
	[Signal] public delegate void DiedEventHandler();

	// Every animation of this character (art/anim/<name>.tres, made by tools/anim/build.py).
	// Set it on a placed goon to give him a different outfit.
	[Export] public SpriteFrames Look;

	[Export] public WeaponData StartWeapon;      // empty = starts empty-handed

	// Seconds on the floor after a knock-down, picked at random in this range.
	[Export] public float DownTimeMin = 1.7f;
	[Export] public float DownTimeMax = 3.7f;

	// Pixels travelled in one full walk cycle (left foot, then right). Longer = slower, heavier steps:
	// about 180 steps a minute at the player's top speed (133), a stroll to a run for goons (80).
	[Export] public float Stride = 80f;

	// What a dropped or thrown gun becomes: a gun lying on the floor.
	[Export] public PackedScene PickupScene;

	public bool IsDead { get; private set; }

	// Knocked to the floor (by a door or a thrown gun): lying there for a moment, then gets back up.
	public bool IsDown { get; private set; }

	// Held by someone finishing him off: he can't act or get up.
	public bool Pinned { get; set; }

	// Thrown by whoever held him up, and still flying (see Enemy: he bowls over a goon or bursts on a wall).
	public bool Thrown { get; protected set; }

	public Gun Gun { get; private set; }
	public bool HasWeapon => Gun.Data != null;

	protected Node2D Visual { get; private set; }

	private const int CorpseZ = 2;
	private const int GooSteps = 8; // footprints left after stepping in goo

	// One footprint per this many pixels travelled (set apart from Stride, so goo trails stay dense).
	private const float StepLength = 20f;
	private const int DeadFaceDown = 1; // lie_dead's frames: on the back, face down, on the side

	private AnimatedSprite2D _bodySprite;
	private AnimatedSprite2D _corpseBody;
	private AnimatedSprite2D _legs;
	private Node2D _alive;
	private Node2D _corpse;
	private CollisionShape2D _collision;
	private static readonly AudioStream BodyFall = GD.Load<AudioStream>("res://audio/body_fall.tres");
	private AudioStreamPlayer2D _deathSound, _fallSound;
	private float _walked;
	private int _step;
	private int _gooSteps;
	private bool _leftFoot;
	private float _downLeft;
	private bool _slumped;       // knocked into a wall: sitting against it
	private int _deadPose;       // lie_dead's frame shown once the fall is over
	private int _livingZ;
	private int _legFrames;
	private Tween _flinch;

	public override void _Ready()
	{
		Visual = GetNode<Node2D>("Visual");
		Gun = GetNode<Gun>("Visual/Alive/Gun");
		_bodySprite = GetNode<AnimatedSprite2D>("Visual/Alive/Sprite");
		_legs = GetNode<AnimatedSprite2D>("Legs");
		_alive = GetNode<Node2D>("Visual/Alive");
		_corpse = GetNode<Node2D>("Visual/Corpse");
		_collision = GetNode<CollisionShape2D>("CollisionShape2D");
		_deathSound = GetNode<AudioStreamPlayer2D>("DeathSound");
		_fallSound = new AudioStreamPlayer2D { Stream = BodyFall };
		AddChild(_fallSound);
		_corpseBody = GetNode<AnimatedSprite2D>("Visual/Corpse/Body");
		_livingZ = ZIndex;
		ReadyMelee();

		if (Look != null)
			_bodySprite.SpriteFrames = _legs.SpriteFrames = _corpseBody.SpriteFrames = Look;
		_legs.Animation = "legs";
		_legFrames = _legs.SpriteFrames.GetFrameCount("legs");
		_corpseBody.AnimationFinished += () =>
		{
			if (_corpseBody.Animation == "lie_fall") // landed: the pose he stays in
				ShowOnFloor("lie_dead", _deadPose);
		};
		Equip(StartWeapon, StartWeapon?.Ammo ?? 0);
	}

	// Puts a gun in our hand (or empties it, with null) and swaps the body look to match.
	public void Equip(WeaponData data, int ammo, bool playSound = false)
	{
		Gun.Equip(data, ammo, playSound);
		FitAnimations();
		ResetSwing();
	}

	// Picks up a gun lying on the floor.
	public void PickUp(WeaponPickup pickup)
	{
		Equip(pickup.Data, pickup.Ammo, playSound: true);
		pickup.QueueFree();
		_dip = 1f; // reaching down for it
		Act("pickup");
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
		if (thrown)
		{
			_throwFollow = 1f; // the arm follows through
			Act("throw");
		}
	}

	// Runs every drawn frame: purely visual, so it doesn't need to be in step with physics.
	public override void _Process(double delta)
	{
		if (IsDead)
			return;

		if (IsDown)
		{
			if (!Pinned)
				_downLeft -= (float)delta;
			// Getting up takes as long as its animation, finishing right when he's back on his feet.
			string getUp = _slumped ? "lie_slump_getup" : "lie_getup";
			if (!Pinned && _downLeft <= Seconds(getUp) && _corpseBody.Animation != getUp)
				ShowOnFloor(getUp);
			else if (!_slumped && _corpseBody.Animation == "lie_dead")
				_corpseBody.Rotation = Mathf.Sin(_downLeft * 14f) * 0.08f; // squirming, so it's clear he isn't dead
			if (_downLeft <= 0)
				GetUp();
			return;
		}
		MoveBody((float)delta);

		float speed = Velocity.Length();
		if (speed < 10f)
		{
			_legs.Frame = 0; // standing
			_walked = 0;
			_step = 0;
		}
		else
		{
			// Legs point where we're going, which may differ from where we aim.
			_legs.Rotation = Velocity.Angle();
			_walked += speed * (float)delta;
			_legs.Frame = (int)(_walked * _legFrames / Stride) % _legFrames;
			int step = (int)(_walked / StepLength);
			for (; _step < step; _step++) // a slow frame can cover several steps: each gets its print, back along the path
				Footstep(GlobalPosition - Velocity.Normalized() * (step - _step - 1) * StepLength);
		}
		AnimateBody(speed >= 10f);
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

		// Already on the floor: he stays as he lies (slumped against a wall, he slumps over).
		// Standing: he falls the way the bullet was going, into a random death pose (sometimes
		// mirrored); choked out, face down.
		_corpseBody.Rotation = 0f;
		if (_slumped)
			ShowOnFloor("lie_dead_lean");
		else if (IsDown)
			ShowOnFloor("lie_dead", DeadFaceDown);
		else
		{
			_deadPose = lethal ? GD.RandRange(0, _corpseBody.SpriteFrames.GetFrameCount("lie_dead") - 1) : DeadFaceDown;
			_corpseBody.FlipV = GD.Randf() < 0.5f;
			Visual.Rotation = hitDirection.Angle();
			ShowOnFloor("lie_fall");
		}
		IsDown = false;
		ShowLying(true);

		// Goo bursts out the way the bullet went, splats, and pools under the body.
		if (lethal)
			GooCanvas.Bleed(GlobalPosition, hitDirection);

		// The body is knocked back a little in the direction of the shot (not into the wall he sits against).
		if (!_slumped)
		{
			CreateTween()
				.TweenProperty(this, "global_position", GlobalPosition + hitDirection * 8f, 0.2)
				.SetTrans(Tween.TransitionType.Cubic)
				.SetEase(Tween.EaseType.Out);
		}

		// Make the kill land: kick the screen and freeze the game for a split second.
		// A quiet takedown stays quiet: whoever did it plays its sounds.
		if (lethal)
		{
			ShakeCamera.Shake(this is Player ? 3.3f : 2.3f); // your own death kicks hardest
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

		_deadPose = DeadFaceDown;
		_corpseBody.FlipV = GD.Randf() < 0.5f;
		Visual.Rotation = direction.Angle();
		ShowOnFloor("lie_fall");
		ShowLying(true);
		_fallSound.Play();
	}

	// Being picked up as a human shield: his get-up plays sped up to last `seconds`, turned to `facing`.
	public void LiftUp(float seconds, float facing)
	{
		string getUp = _slumped ? "lie_slump_getup" : "lie_getup";
		Visual.Rotation = facing;
		_corpseBody.Rotation = 0f;
		ShowOnFloor(getUp);
		_corpseBody.SpeedScale = Seconds(getUp) / seconds;
	}

	// Picked up off the floor as a human shield: on his feet, not solid (the holder's shots pass him; shots
	// at the holder are checked by the holder), drawn just under the holder's arms.
	public void HoldUp()
	{
		_corpseBody.SpeedScale = 1f;
		IsDown = false;
		_slumped = false;
		_corpseBody.Rotation = 0f;
		ShowLying(false);
		ZIndex = _livingZ - 1;
		_collision.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
		HoldPose = "held";
	}

	// Thrown by whoever held him up: knocked down, flying at `velocity`.
	public void Throw(Vector2 velocity)
	{
		Pinned = false;
		HoldPose = null;
		_collision.SetDeferred(CollisionShape2D.PropertyName.Disabled, false);
		KnockDown(velocity.Normalized());
		Velocity = velocity;
		Thrown = true;
	}

	// Carried by whoever holds him: where he stands, which way he faces, and how fast (for his legs).
	public void Carry(Vector2 at, float facing, Vector2 velocity)
	{
		GlobalPosition = at;
		Visual.Rotation = facing;
		Velocity = velocity;
	}

	// Knocked down into a wall (`away` points out of it): he sits slumped against it, dazed,
	// instead of lying flat. He gets up the same as from the floor.
	public bool Slumped => IsDown && _slumped;

	protected void Slump(Vector2 away)
	{
		_slumped = true;
		Velocity = Vector2.Zero;
		Visual.Rotation = away.Angle();
		_corpseBody.Rotation = 0f;
		ShowOnFloor("lie_slump");
	}

	// Thrown into a wall hard (`away` points out of it): he splats flat against it, dead, slides
	// down it into a heap, and goo splashes back off the wall.
	protected void Splat(Vector2 away)
	{
		Slump(away);
		Die(away);
		_corpseBody.FlipV = GD.Randf() < 0.5f; // two ways to crumple
		ShowOnFloor("lie_splat");
		GooCanvas.Spray(GlobalPosition, away, 14, Mathf.Pi, 80f, 260f);
		ShakeCamera.Shake(3f);
	}

	private void GetUp()
	{
		IsDown = false;
		Thrown = false;
		_slumped = false;
		_corpseBody.Rotation = 0f;
		ShowLying(false);
	}

	// The lying layer plays `animation` from the start, or shows just its frame `frame`.
	private void ShowOnFloor(string animation, int frame = -1)
	{
		_corpseBody.Play(animation);
		if (frame < 0)
			return;
		_corpseBody.Pause();
		_corpseBody.Frame = frame;
	}

	// How long one run of an animation takes.
	private float Seconds(string animation)
	{
		SpriteFrames frames = _corpseBody.SpriteFrames;
		float total = 0f;
		for (int i = 0; i < frames.GetFrameCount(animation); i++)
			total += (float)frames.GetFrameDuration(animation, i);
		return total / (float)frames.GetAnimationSpeed(animation);
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

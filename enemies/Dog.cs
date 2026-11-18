using Godot;

namespace RentDay;

// A dog. It walks along the walls (Behaviour FollowWall) and sits down for a moment every few seconds;
// a dog left Static sits at its post. It sees and hears like anyone (see Enemy).
// Spotting you, it stops and barks: that's your warning, and walking enemies nearby hear it like a shot.
// Then it charges, speeding up straight at you and bouncing off walls instead of going round them, so it
// can overshoot. Running into you, it bites, and you're dead (a dash goes through it, like through a
// bullet). If it loses you, it runs to where it last saw you, and barks again when it finds you.
// A bullet, a bat or a knife kills it. Punches, door slams and thrown things do nothing, and it can't be
// grabbed (KnockProof): it's the one enemy you can't take on bare-handed.
public partial class Dog : Enemy
{
	[ExportGroup("Dog")]
	[Export] public float SitEvery = 7f;        // following the walls, it sits down about this often (seconds); 0 = never
	[Export] public float SitTime = 3f;         // for about this long
	[Export] public float BarkTime = 0.4f;      // seconds from its bark to its charge
	[Export] public float ForgetTime = 0.5f;    // out of sight this long, it barks again when it sees you
	[Export] public float RunSpeed = 240f;      // charging, top speed (pixels per second): faster than you, and than a dash
	[Export] public float Acceleration = 1500f; // charging, speed gained per second
	[Export] public float BiteRange = 12f;      // it bites you this close, while running
	[Export] public float BarkNoise = 240f;     // walking enemies this close hear the bark, through walls, and come to the dog (as loud as a pistol)
	[Export] public AudioStream BarkSound;
	[Export] public AudioStream BiteSound;

	private static readonly StringName Sit = "sit";

	private AudioStreamPlayer2D _barkSound;
	private AudioStreamPlayer2D _biteSound;
	private float _barkLeft;
	private float _unseen = float.MaxValue; // seconds since it last saw you
	private float _untilSit;
	private float _sitLeft;

	public override bool KnockProof => true;
	protected override bool CarriesWeapons => false;
	protected override StringName CalmPose => _sitLeft > 0f || Behaviour == Routine.Static ? Sit : null;

	public override void _Ready()
	{
		base._Ready();
		_untilSit = GD.Randf() * SitEvery; // dogs don't all sit at once
		_barkSound = new AudioStreamPlayer2D { Stream = BarkSound };
		_biteSound = new AudioStreamPlayer2D { Stream = BiteSound };
		AddChild(_barkSound);
		AddChild(_biteSound);
	}

	public override void _PhysicsProcess(double delta)
	{
		_unseen += (float)delta;
		base._PhysicsProcess(delta);
	}

	protected override void Attack(float dt)
	{
		_sitLeft = 0f;
		Vector2 toYou = Target.GlobalPosition - GlobalPosition;
		if (_unseen > ForgetTime)
		{
			_barkLeft = BarkTime;
			Startle();
			Act("bark");
			_barkSound.Play();
			Noise.Make(this, GlobalPosition, BarkNoise);
		}
		_unseen = 0f;

		if (_barkLeft > 0f)
		{
			_barkLeft -= dt;
			Velocity = Vector2.Zero;
			Visual.Rotation = toYou.Angle();
			return;
		}

		Velocity = (Velocity + toYou.Normalized() * Acceleration * dt).LimitLength(RunSpeed);
		var bump = new KinematicCollision2D();
		if (TestMove(GlobalTransform, Velocity * dt, bump))
			Velocity = Velocity.Bounce(bump.GetNormal());
		Visual.Rotation = Velocity.Angle();
		if (toYou.Length() <= BiteRange && Velocity.Length() > 60f && DevSettings.Current.EnemiesShoot && !Target.IsDead && !Target.Dodging)
		{
			_biteSound.Play();
			Target.Die(toYou.Normalized());
		}
	}

	// Following the walls: every so often it sits down for a while.
	protected override void FollowRoutine(float dt)
	{
		if (Behaviour == Routine.FollowWall && SitEvery > 0f)
		{
			if (_sitLeft > 0f)
			{
				_sitLeft -= dt;
				Velocity = Vector2.Zero;
				return;
			}
			_untilSit -= dt;
			if (_untilSit <= 0f)
			{
				_untilSit = SitEvery * (float)GD.RandRange(0.7, 1.3);
				_sitLeft = SitTime * (float)GD.RandRange(0.7, 1.3);
			}
		}
		base.FollowRoutine(dt);
	}
}

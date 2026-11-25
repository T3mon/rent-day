using Godot;

namespace RentDay;

// A heavy enemy: a big man with bare fists. He patrols like anyone (Behaviour Patrol) and sees and hears
// like anyone (see Enemy). Spotting you, he walks straight at you; once you're within Swing
// Range he swings (his Fists): it kills you.
// Only bullets hurt him. A bat, a knife, punches, door slams and thrown things do nothing, and he can't be
// grabbed or stealth-killed (KnockProof, MeleeProof); blows and thrown things clang off his vest (Shrug). Each bullet is a hit and slows him a little. At Max
// Hits he dies at once; before that he bleeds out, sooner the more hits he has, and keeps coming meanwhile:
// 1 hit about 6.4 s, 2 hits 4 s, 3 hits 2.3 s, 4 hits 1 s. So a close shotgun blast kills him at once.
public partial class Heavy : Enemy
{
	[ExportGroup("Heavy")]
	[Export] public int MaxHits = 5;          // this many bullets kill him at once
	[Export] public float Blood = 120f;       // what he has to bleed out (the bleeding rate is in _PhysicsProcess)
	[Export] public float SlowPerHit = 12f;   // each bullet takes this much off his chasing and searching speeds (pixels per second)
	[Export] public AudioStream HurtSound;    // a bullet hits him and he lives
	[Export] public AudioStream ClangSound;   // a blow or a thrown thing hits his vest

	private int _hits;
	private float _blood;
	private float _dripWait;
	private bool _startled;
	private AudioStreamPlayer2D _hurt;
	private AudioStreamPlayer2D _clang;
	private Vector2 _lastShot = Vector2.Right;

	public override bool KnockProof => true;
	public override bool MeleeProof => true;
	protected override bool CarriesWeapons => false;

	public override void _Ready()
	{
		base._Ready();
		_blood = Blood;
		_hurt = new AudioStreamPlayer2D { Stream = HurtSound, VolumeDb = -6f }; // as loud as his swing grunts
		AddChild(_hurt);
		_clang = new AudioStreamPlayer2D { Stream = ClangSound };
		AddChild(_clang);
	}

	public override void Shrug()
	{
		_clang.PitchScale = (float)GD.RandRange(0.9, 1.1);
		_clang.Play();
		ShakeCamera.Shake(1f);
	}

	public override void Shot(Vector2 direction)
	{
		if (IsDead)
			return;
		_hits++;
		_lastShot = direction;
		if (_hits >= MaxHits)
		{
			Die(direction);
			return;
		}
		ChaseSpeed = Mathf.Max(ChaseSpeed - SlowPerHit, 0f);
		SearchSpeed = Mathf.Max(SearchSpeed - SlowPerHit, 0f);
		Flinch(direction, 2f, bleed: true);
		_hurt.PitchScale = (float)GD.RandRange(0.9, 1.1);
		_hurt.Play();
		ShakeCamera.Shake(1.5f);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_hits > 0 && !IsDead)
		{
			float dt = (float)delta;
			// With h hits he loses 1 + h/4 blood every 5 - h ticks (every tick from 4 hits), at 60 ticks a second.
			_blood -= (1f + _hits / 4f) * 60f / Mathf.Max(5 - _hits, 1) * dt;
			if (_blood <= 0f)
			{
				Die(_lastShot);
				return;
			}
			_dripWait -= dt;
			if (_dripWait <= 0f) // goo drips behind him, faster the more hits he has
			{
				_dripWait = 0.35f / _hits;
				GooCanvas.Splat(GlobalPosition, Vector2.Right.Rotated(GD.Randf() * Mathf.Tau) * 6f, (float)GD.RandRange(5.0, 9.0), GD.Randf() * Mathf.Tau);
			}
		}
		base._PhysicsProcess(delta);
	}

	protected override void Attack(float dt)
	{
		if (!_startled)
		{
			_startled = true;
			Startle();
		}
		Vector2 toYou = Target.GlobalPosition - GlobalPosition;
		if (toYou.Length() > SwingRange)
			RunTo(Target.GlobalPosition, ChaseSpeed, dt);
		else
			Velocity = Vector2.Zero;
		Visual.Rotation = toYou.Angle();
		if (toYou.Length() <= SwingRange && DevSettings.Current.EnemiesShoot)
			TrySwing();
	}
}

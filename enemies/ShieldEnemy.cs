using Godot;

namespace RentDay;

// A shield enemy: an ordinary enemy in a helmet (the riot look), with a pistol, behind a ballistic shield
// (Visual/Alive/Shield). The shield covers his front (Front degrees): bullets bounce off it and can kill you or
// other enemies, and blows, thrown things, door slams and grabs from there do nothing to him. He turns slowly
// (Turn Speed), so you get round to his side or back, where he dies like anyone.
// A shotgun blast on it knocks him down (its pellets still bounce off). It opens up: for Peek Time after each of his
// shots he holds it aside, and a door slam or a thrown thing knocks it aside for Lower Time. A bat or a door on it
// shoves him back. He bangs on it when he spots you.
// Lying down or dead, the shield lies beside him (Visual/Corpse/Shield).
public partial class ShieldEnemy : Enemy
{
	[ExportGroup("Shield")]
	[Export] public float Front = 120f;         // degrees the shield covers, centred on where he faces
	[Export] public float PeekTime = 0.35f;     // seconds it's held aside after each of his shots
	[Export] public float LowerTime = 1f;       // seconds it's knocked aside by a door slam or a thrown thing
	[Export] public AudioStream BlockSound;     // a blow lands on the shield, or he bangs on it
	[Export] public AudioStream RicochetSound;  // a bullet bounces off it

	private static readonly Vector2 Held = new(9f, -3f), Aside = new(1f, -11f); // the shield up in front, or swung aside to his left
	private const float AsideAngle = -1.2f;
	private const float StillBlocking = 0.15f;  // after a hit the shield keeps taking the same blow (every pellet, a door still sweeping)
	private const float Bang = 4f;              // spotting you, he jabs the shield this far forward

	private Node2D _shield;
	private AudioStreamPlayer2D _sound;
	private float _open;        // seconds left with the shield aside
	private float _blocking;    // seconds left of StillBlocking
	private float _blockFacing; // where the shield faced when that hit landed
	private int _shots;
	private Vector2 _push;
	private Vector2 _knock;     // a shotgun blast landed: knocked down this way on the next tick (after every pellet bounced)
	private ulong _attackedFrame;

	public override void _Ready()
	{
		base._Ready();
		_shield = GetNode<Node2D>("Visual/Alive/Shield");
		_sound = new AudioStreamPlayer2D { VolumeDb = -4f };
		AddChild(_sound);
	}

	public override Vector2 Facing => _blocking > 0f ? Vector2.FromAngle(_blockFacing) : base.Facing;

	public override bool Blocks(Vector2 direction) =>
		!IsDead && !Pinned && (_blocking > 0f || (!IsDown && _open <= 0f))
		&& Mathf.Abs(Mathf.AngleDifference(Facing.Angle(), (-direction).Angle())) <= Mathf.DegToRad(Front / 2f);

	public override void Fend(Vector2 direction, float push = 0f, bool lower = false, bool knock = false, bool bounced = false)
	{
		if (_blocking <= 0f)
			_blockFacing = Visual.GlobalRotation;
		_blocking = StillBlocking;
		Play(bounced ? RicochetSound : BlockSound, 1f);
		_push += direction * push;
		if (knock)
			_knock = direction;
		if (lower)
			_open = Mathf.Max(_open, LowerTime);
		ShakeCamera.Shake(push > 0f || lower || knock ? 1.3f : 0.6f);
	}

	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		if (_knock != Vector2.Zero)
		{
			KnockDown(_knock);
			_knock = Vector2.Zero;
			_push = Vector2.Zero;
		}
		if (Gun.Shots != _shots)
		{
			_shots = Gun.Shots;
			_open = Mathf.Max(_open, PeekTime);
		}
		_open -= dt;
		_blocking -= dt;
		base._PhysicsProcess(delta);
		if (_push == Vector2.Zero || IsDead || IsDown || Pinned)
			return;
		MoveAndCollide(_push * dt); // shoved back, sliding to a stop
		_push = _push.MoveToward(Vector2.Zero, 900f * dt);
	}

	// Spotting you (after a second of not seeing you), he bangs on his shield: a clang and a jab forward.
	protected override void Attack(float dt)
	{
		ulong frame = Engine.GetPhysicsFrames();
		if (frame - _attackedFrame > (ulong)Engine.PhysicsTicksPerSecond)
		{
			Play(BlockSound, 0.75f);
			_shield.Position += Vector2.Right * Bang;
		}
		_attackedFrame = frame;
		base.Attack(dt);
	}

	public override void _Process(double delta)
	{
		base._Process(delta);
		float dt = (float)delta;
		bool aside = _open > 0f;
		_shield.Position = _shield.Position.MoveToward(aside ? Aside : Held, 160f * dt);
		_shield.Rotation = Mathf.MoveToward(_shield.Rotation, aside ? AsideAngle : 0f, 16f * dt);
	}

	private void Play(AudioStream sound, float pitch)
	{
		_sound.Stream = sound;
		_sound.PitchScale = pitch * (float)GD.RandRange(0.9, 1.1);
		_sound.Play();
	}
}

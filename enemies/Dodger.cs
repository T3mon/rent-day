using Godot;

namespace RentDay;

// A dodger: an ordinary enemy in a red headband, with a knife. On his feet, he slides out of the way of the bullets
// and thrown things he sees or hears coming: anything while he's onto you and has you in sight, and anything for a
// moment (GuardTime) after a bang within earshot (a gunshot; a silenced one makes none). One coming at him, he slides
// aside across its path, angled toward you (Dodge Angle), and it flies on past; one slide covers a whole shotgun blast,
// and he slides again as often as it takes, to the other side each time if there's room there. With no room either side
// he ducks on the spot. Then he knows you're around.
// Caught unaware (calm and he hasn't noticed you, and no bang he could hear), a shot or a throw hits him like anyone.
// Up close he's like anyone, even mid-slide: a bat or a knife kills him, a punch or a door slam knocks him down, and
// grabs, stealth kills and finishing off work as usual. Knocked down, he can't slide. Disarmed, he only goes for a
// bat or a knife.
// The slide follows its animation (tools/anim/anims.py spin, which copies Dodge Time, Dodge Speed, Dodge Angle and
// Pace): loading up, a burst, a feet-first slide along the floor, a brake and up. He keeps facing the way he was.
public partial class Dodger : Enemy
{
	[ExportGroup("Dodge")]
	[Export] public float DodgeSpeed = 225f;  // pixels per second at a full burst (your dash's speed)
	[Export] public float DodgeTime = 0.3f;   // seconds a slide lasts
	[Export] public float DodgeAngle = 35f;   // degrees off straight across the shot, toward you: each slide brings him closer
	[Export] public AudioStream DodgeSound;   // the whoosh of a slide

	// How fast he goes through a slide (0 to 1), as a share of Dodge Speed, straight between its keys (when, share).
	private static readonly (float At, float Share)[] Pace =
		{ (0f, 0.3f), (0.12f, 0.3f), (0.2f, 1.4f), (0.6f, 1f), (0.8f, 0.5f), (0.92f, 0.1f), (1f, 0f) };
	// Squash and stretch through it: he goes mostly sideways (y), so loading up and the skid squash him that way,
	// and the burst and the slide stretch him that way.
	private static readonly (float At, Vector2 Size)[] SpinSmush = { (0f, Vector2.One), (0.12f, new(1.1f, 0.88f)),
		(0.22f, new(0.92f, 1.1f)), (0.5f, new(0.96f, 1.05f)), (0.76f, new(0.98f, 1.02f)), (0.86f, new(1.08f, 0.9f)),
		(0.95f, new(0.97f, 1.03f)), (1f, Vector2.One) };
	private const float Room = 20f; // pixels clear he needs to a side to slide that way
	private const float GuardTime = 1f; // seconds a bang keeps him ready (a bullet from within earshot is there sooner)

	private float _guardLeft;
	private float _dodgeLeft;
	private Vector2 _dodgeDirection; // zero: on the spot
	private float _lastSide;
	private AudioStreamPlayer2D _sound;

	public override bool Dodging => _dodgeLeft > 0f;

	public override bool Dodges(Vector2 direction)
	{
		if (Dodging)
			return true;
		if (IsDead || IsDown || Pinned || (_guardLeft <= 0f && !Watching))
			return false; // down, or caught unaware: it hits him like anyone
		Spin(direction);
		return true;
	}

	public override void HearBang() => _guardLeft = GuardTime;

	protected override bool Wants(WeaponData weapon) => weapon.Melee;

	public override void _Ready()
	{
		base._Ready();
		_sound = new AudioStreamPlayer2D { Stream = DodgeSound, VolumeDb = 6f }; // the recordings are quiet
		AddChild(_sound);
		_lastSide = GD.Randf() < 0.5f ? 1f : -1f;
	}

	// Something flying `shot` way: off across its path, leaning toward you, to the other side than last time if
	// there's room there, else this side, else on the spot.
	private void Spin(Vector2 shot)
	{
		Vector2 toYou = Target != null ? GlobalPosition.DirectionTo(Target.GlobalPosition) : -shot;
		float lean = Mathf.Tan(Mathf.DegToRad(DodgeAngle));
		_dodgeDirection = Vector2.Zero;
		foreach (float side in new[] { -_lastSide, _lastSide })
		{
			Vector2 way = (shot.Orthogonal() * side + toYou * lean).Normalized();
			if (!TestMove(GlobalTransform, way * Room))
			{
				_dodgeDirection = way;
				_lastSide = side;
				break;
			}
		}
		bool right = _dodgeDirection == Vector2.Zero ? GD.Randf() < 0.5f : _dodgeDirection.Rotated(-Visual.Rotation).Y > 0f;
		_dodgeLeft = DodgeTime;
		ResetSwing(); // a swing that was out ends
		IdlePose = null;
		ShotAt();
		_sound.Play();
		Act(DashPose(right ? "spin_r" : "spin_l"));
	}

	public override void _PhysicsProcess(double delta)
	{
		_guardLeft -= (float)delta;
		if (Dodging && (IsDead || IsDown || Pinned))
			StopSpin(); // a blow or a grab landed mid-slide
		if (!Dodging)
		{
			base._PhysicsProcess(delta);
			return;
		}

		float dt = (float)delta;
		_dodgeLeft -= dt;
		float done = Mathf.Min(1f - _dodgeLeft / DodgeTime, 1f);
		Velocity = _dodgeDirection * DodgeSpeed * PaceAt(done);
		MoveAndSlide();
		Smush = SmushAt(SpinSmush, done);
		if (Engine.GetPhysicsFrames() % 2 == 0)
			Afterimage.Leave(this);
		if (!Dodging)
			StopSpin();
	}

	private void StopSpin()
	{
		_dodgeLeft = 0f;
		Smush = Vector2.One;
	}

	private static float PaceAt(float through)
	{
		for (int i = 1; i < Pace.Length; i++)
			if (through <= Pace[i].At)
				return Mathf.Lerp(Pace[i - 1].Share, Pace[i].Share, (through - Pace[i - 1].At) / (Pace[i].At - Pace[i - 1].At));
		return 0f;
	}
}

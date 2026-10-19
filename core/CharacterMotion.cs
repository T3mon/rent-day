using Godot;

namespace RentDay;

// The upper body's look, which never changes what anyone can do or when. Its animation (from
// Look): walking in step with the legs or standing, with whatever is in hand, and one-offs
// over that (a swing, a throw, a pickup, a hand on a door, a finisher). And small motions on
// top: leaning into a run and settling after a stop, with dust at the feet; a dip to pick
// something up; a shoulder into a door; the body turning with a throw; a startle.
public partial class Character
{
	private static readonly PackedScene Dust = GD.Load<PackedScene>("res://effects/dust.tscn");

	private const float LeanPerSpeed = 2.5f / 200f; // pixels the upper body leads by, per pixel per second of speed
	private const float DustKick = 1800f;           // speed change (pixels per second, per second) that kicks up dust

	// Standing still, this plays instead of the usual standing animation (a goon's smoke, phone...); null = none.
	protected string IdlePose;

	private string _action;               // the one-off animation playing, if any
	private Vector2 _lean;                // where the upper body sits over the legs, in the level's frame
	private Vector2 _leanSpeed;
	private Vector2 _lastVelocity;
	private float _dustWait;
	private float _dip;                   // these run from 1 (just happened) down to 0
	private float _push;
	private float _startle;
	private float _throwFollow;
	private Vector2 _pushDirection;

	// Which set of animations fits what's in hand: "" bare fists, "gun_", "bat_", "knife_".
	private string Kind => Gun.Data == null ? "" : Gun.Data.Melee ? Gun.Data.Name.ToLowerInvariant() + "_" : "gun_";

	// Plays a one-off animation of the upper body from the start; walking or standing takes over
	// when it ends. A looping one (choke) plays until Act(null).
	protected void Act(string animation)
	{
		_action = animation;
		if (animation == null)
			return;
		_bodySprite.Play(animation);
		_bodySprite.Frame = 0;
	}

	private void AnimateBody(bool moving)
	{
		if (_action != null && _bodySprite.IsPlaying())
		{
			Gun.Visible = false; // the one-offs show the hands, not a gun
			return;
		}
		_action = null;
		string pose = moving ? null : IdlePose;
		Gun.Visible = Kind == "gun_" && pose == null; // bats and knives are part of the animation
		_legs.Visible = pose != "sit";
		if (moving)
		{
			_bodySprite.Animation = Kind + "walk";
			_bodySprite.Pause();
			_bodySprite.Frame = _legs.Frame; // the arms swing in step with the feet
		}
		else if (!_bodySprite.IsPlaying() || _bodySprite.Animation != (pose ?? Kind + "idle"))
			_bodySprite.Play(pose ?? Kind + "idle");
	}

	// Walked into a door: a shoulder against it, toward `direction`, and an empty hand on it.
	public void PushDoor(Vector2 direction)
	{
		_push = 1f;
		_pushDirection = direction;
		string hand = _pushDirection.Rotated(-Visual.GlobalRotation).Y >= 0f ? "push_r" : "push_l";
		if (Kind == "" && _action != hand)
			Act(hand);
	}

	// Just spotted someone: a jolt.
	protected void Startle() => _startle = 1f;

	private void MoveBody(float dt)
	{
		dt = Mathf.Min(dt, 1f / 30f); // a long frame (a level loading) would make the spring below blow up
		// A spring pulls the upper body a little ahead in the direction we move: starting off it
		// swings forward, stopping it overshoots and settles back.
		_leanSpeed += ((Velocity * LeanPerSpeed - _lean) * 400f - _leanSpeed * 14f) * dt;
		_lean += _leanSpeed * dt;

		// Dust where the feet push off hard: running off, stopping, turning sharply.
		Vector2 change = (Velocity - _lastVelocity) / Mathf.Max(dt, 0.001f);
		bool running = Mathf.Max(Velocity.Length(), _lastVelocity.Length()) > 100f;
		_lastVelocity = Velocity;
		_dustWait -= dt;
		if (running && change.Length() > DustKick && _dustWait <= 0f)
		{
			Effects.Spawn(Dust, this, GlobalPosition, (-change).Angle());
			_dustWait = 0.2f;
		}

		_dip = Mathf.MoveToward(_dip, 0f, dt / 0.18f);
		_push = Mathf.MoveToward(_push, 0f, dt / 0.3f);
		_startle = Mathf.MoveToward(_startle, 0f, dt / 0.2f);
		_throwFollow = Mathf.MoveToward(_throwFollow, 0f, dt / 0.25f);

		float turn = -Visual.GlobalRotation;
		float dip = Bump(_dip);
		float push = Bump(_push);
		float startle = Pop(_startle);
		Vector2 door = _pushDirection.Rotated(turn);
		_alive.Position = _lean.Rotated(turn) + Vector2.Right * (2f * dip) + door * (3.5f * push)
			- Vector2.Right * (1f * startle);
		_alive.Rotation = -0.35f * _throwFollow * _throwFollow
			- 0.35f * push * (door.Y < 0f ? -1f : 1f); // the shoulder on the door's side leads
		_alive.Scale = Vector2.One * (1f - 0.08f * dip + 0.08f * startle);
	}

	// 0 -> 1 -> 0 while `left` runs from 1 down to 0.
	private static float Bump(float left) => Mathf.Sin(Mathf.Pi * (1f - left)) * (left > 0f ? 1f : 0f);

	// The same, but snapping up in the first fifth and easing back down: a jolt.
	private static float Pop(float left) => left > 0.8f ? (1f - left) / 0.2f : left / 0.8f;
}

using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RentDay;

// An enemy. What he does until he notices you depends on his Behaviour:
//   Static - stands guard facing one way. Reacts only to what he sees, never to gunshots.
//   Patrol - walks straight ahead; when something blocks his way he turns left, bit by bit, until
//            the way is clear and he faces straight along the walls again.
//   Wander - walks a few steps in a random direction, stops, looks around, repeats.
// Patrolling and wandering enemies hear your gunshots and run to where the shot came from.
// When an enemy sees you he stops, turns to aim, and fires after a short reaction time: longer
// when you're far away or behind him, with a little randomness. That delay is your window.
// After that he keeps firing at his gun's enemy pace for as long as he sees you, walking at you
// until he's close. An enemy with a bat or knife instead runs at you and swings once you're in reach.
// He notices you all around him, but only as far as about the screen reaches to your side. Calm, he
// only looks around every half second and takes a moment to react, so you can sneak up on him.
// If he loses you, he runs to around where he last saw or heard you (each enemy to a different spot,
// so they don't pile up), on a route around walls (see LevelGrid). If you're not there, he
// looks around for a moment and goes back to his routine (a static enemy returns to his post).
// Enemies shoulder each other apart rather than stand inside one another. One standing where he
// doesn't fit (sitting in a couch, slumped against a wall) first steps out onto open floor.
// An enemy whose weapon got knocked away runs for the nearest weapon he can see.
// Knocked down into a wall, he sits slumped against it instead of lying flat.
// Pose is only the look: what he does while calm and standing still (smoking, on the phone...).
//
// Change the defaults for every enemy in enemies/enemy.tscn; change one enemy in the level.
public partial class Enemy : Character
{
	public enum Routine { Static, Patrol, Wander }
	public enum Idle { None, Smoke, Phone, Hide, Sit, Lean, Talk }

	[ExportGroup("Routine")]
	[Export] public Routine Behaviour = Routine.Static;
	[Export] public float FacingDegrees = 0f;   // where he looks at the start (0 = right, 90 = down); a patroller sets off this way
	[Export] public bool StayInRoom;            // patrol and wander only: never walks out through a doorway on his own
	[Export] public float WalkSpeed = 55f;      // patrolling and wandering, pixels per second
	[Export] public Idle Pose = Idle.None;      // while calm and standing still: smoking, on the phone, hiding, sitting, leaning on a wall, talking

	[ExportGroup("Senses")]
	[Export] public float SightRange = 183f;    // pixels, all around him; the screen reaches 200 to your side, 112 up and down
	[Export] public float LookEvery = 0.5f;     // calm, he only looks around this often (seconds), so you can sneak up between looks
	[Export] public float NoticeTime = 0.25f;   // calm, seconds from spotting you to reacting; he carries on until then

	[ExportGroup("Reaction")]
	[Export] public float ReactionTime = 0.3f;    // seconds before his first shot when you're close and in front of him
	[Export] public float FarReaction = 0.3f;     // extra seconds when you're at the edge of his sight range
	[Export] public float BehindReaction = 0.4f;  // extra seconds when you're right behind him
	[Export] public float RandomReaction = 0.15f; // up to this many extra seconds, at random
	[Export] public float TurnSpeed = 540f;       // degrees per second he can turn while aiming at you

	[ExportGroup("Thrown")]
	[Export] public float SplatSpeed = 250f;    // thrown straight into a wall at least this fast (pixels per second): he dies on it
	[Export] public float BowlSpeed = 150f;     // thrown and still this fast: the enemy he slides into goes down too

	[ExportGroup("Chase")]
	[Export] public float ChaseSpeed = 130f;    // running after you
	[Export] public float ChargeSpeed = 180f;   // with a bat or knife, running at you once he's seen you
	[Export] public float SwingRange = 26f;     // he swings once you're this close
	[Export] public float StandOff = 67f;       // with a gun, he walks at you while shooting until this close
	[Export] public float SearchTime = 1.5f;    // seconds he looks around an empty spot before giving up
	[Export] public float SearchSpread = 32f;   // runs to a random spot this close to where he last saw you
	[Export] public float NoiseSpread = 64f;    // runs to a random spot this close to a gunshot he heard

	private const float Shoulder = 16f;          // two enemies' bodies side by side: closer than this they push apart
	private const float ShoveSpeed = 90f;        // how hard, pixels per second, when right on top of each other

	private static readonly AudioStream WallHit = GD.Load<AudioStream>("res://audio/wall_hit.wav");
	private static readonly List<Enemy> All = new();

	private Player _player;
	private AudioStreamPlayer2D _wallSound;
	private StringName _poseName;   // Pose's animation ("smoke"...); null = none
	private bool _alerted;          // knows you're around
	private bool _seesYou;
	private float _reactionLeft = -1f; // counting down to his first shot; below zero = not counting yet
	private float _lookLeft = -1f;  // calm: seconds to his next look around (below zero at the start: a random first look)
	private float _noticeLeft = -1f; // calm and spotted you: seconds until he reacts; below zero = hasn't
	private Vector2? _checkSpot;    // where he last saw or heard you
	private float _checkSpread;     // and how far from it he may end up
	private float _searchLeft;
	private bool _goingBack;        // stays in his room: walking back to it after a chase
	private Vector2 _post;          // where he started, and which way he faced
	private float _postAngle;
	private Vector2 _heading;       // walking direction while patrolling or wandering
	private float _walkLeft;
	private float _waitLeft;
	private bool _lookedAround;
	private float _stuckTime;
	private List<Vector2> _route;   // corners left on his way to _goal; null = plan a new route
	private Vector2 _goal;
	private Vector2? _stepOut;      // walking straight onto open floor first (through furniture if need be)
	private bool _turning;          // patrol: turning left until the way is clear

	public override void _EnterTree() => All.Add(this);
	public override void _ExitTree() => All.Remove(this);

	// Would a gunshot bring him running? (Static enemies stay put; one who sees you is busy with you.)
	public bool CanHear => Behaviour != Routine.Static && !IsDead && !IsDown && !_seesYou;

	public override void _Ready()
	{
		base._Ready();
		Gun.HeldByEnemy = true;
		Visual.RotationDegrees = FacingDegrees;
		_post = GlobalPosition;
		_postAngle = Visual.Rotation;
		_heading = Vector2.Right.Rotated(_postAngle);
		_player = GetTree().GetFirstNodeInGroup("player") as Player;
		_poseName = Pose == Idle.None ? null : (StringName)Pose.ToString().ToLowerInvariant(); // a null string would become an empty name: a "pose"
		_wallSound = new AudioStreamPlayer2D { Stream = WallHit };
		AddChild(_wallSound);
	}

	// A gunshot went off within earshot, at `where`.
	public void Hear(Vector2 where)
	{
		if (!CanHear)
			return;
		_alerted = true;
		_checkSpot = where;
		_checkSpread = NoiseSpread;
		_searchLeft = SearchTime;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (DevSettings.Current.ShowEnemyInfo || _drawnInfo)
			QueueRedraw();
		if (IsDead || Pinned)
		{
			IdlePose = null;
			return;
		}

		float dt = (float)delta;
		UpdateSwing(dt);
		_seesYou = false;
		LookAround = false; // Search and Wander turn it on while he stands there

		if (IsDown)
		{
			// Knocked flat: slide to a stop. When he gets up, he knows you're around.
			_stepOut = null;
			CollisionMask = Layers.World | Layers.Furniture;
			Velocity = Velocity.MoveToward(Vector2.Zero, 900f * dt);
			Vector2 sliding = Velocity;
			MoveAndSlide();
			if (!Slumped && GetLastSlideCollision() is { } bump && sliding.Dot(bump.GetNormal()) < -60f
				&& (PhysicsServer2D.BodyGetCollisionLayer(bump.GetColliderRid()) & Layers.World) != 0)
			{
				bool hard = Thrown && sliding.Dot(bump.GetNormal()) < -SplatSpeed;
				if (Thrown) // the crash; softer and lighter when he only slumps
				{
					_wallSound.VolumeDb = hard ? -3f : -12f;
					_wallSound.PitchScale = hard ? 1f : 1.25f;
					_wallSound.Play();
				}
				if (hard)
				{
					Splat(bump.GetNormal());
					return;
				}
				Slump(bump.GetNormal());
			}
			if (Thrown)
				Bowl(sliding);
			_alerted = true;
			_reactionLeft = -1f;
			_route = null; // he slid off his route
			return;
		}

		if (_player == null || _player.IsDead)
			Velocity = Vector2.Zero;
		else if (!HasWeapon)
			GoForAGun(dt);
		else if (Notices(dt))
			Fire(dt);
		else if (_checkSpot is Vector2 spot)
			Search(spot, dt);
		else
			FollowRoutine(dt);

		StringName pose = _alerted || Velocity != Vector2.Zero ? null : _poseName;
		// Furniture doesn't stop him while he's in it (sitting in a couch); elsewhere it does.
		// Stepping out onto open floor, nothing does.
		bool throughFurniture = (Pose == Idle.Sit && !_alerted) || LevelGrid.Wedged(GlobalPosition, Layers.Furniture);
		CollisionMask = _stepOut != null ? 0 : Layers.World | (throughFurniture ? 0 : Layers.Furniture);
		if (Pose == Idle.Smoke && IdlePose != null && pose == null && _alerted) // flicks the cigarette away
			Shell.Eject(this, GlobalPosition + Vector2.Down.Rotated(Visual.GlobalRotation) * 12f, Visual.GlobalRotation, Colors.White);
		if (IdlePose != null && pose == null && GetUpAnimation() is string getUp)
			Act(getUp);
		IdlePose = pose;

		if (_alerted || Velocity != Vector2.Zero)
			Velocity += Shove();
		MoveAndSlide();
	}

	// Leaving a pose that has him sitting, crouched or against a wall: the look of getting up (it fits
	// inside his reaction time, so it never slows him down).
	private string GetUpAnimation() => Pose switch
	{
		Idle.Sit => "sit_up",
		Idle.Hide => "hide_up",
		Idle.Lean => "lean_off",
		_ => null,
	};

	// Closer to another enemy than their two bodies side by side: a push apart, harder the deeper in.
	private Vector2 Shove()
	{
		Vector2 push = Vector2.Zero;
		foreach (Enemy other in All)
		{
			if (other == this || other.IsDead || other.IsDown || other.Pinned)
				continue;
			Vector2 away = GlobalPosition - other.GlobalPosition;
			float distance = away.Length();
			if (distance >= Shoulder)
				continue;
			Vector2 direction = distance > 0.01f ? away / distance : Vector2.Right.Rotated(GetIndex()); // right on top: any way, but not both the same
			push += direction * (1f - distance / Shoulder);
		}
		return push * ShoveSpeed;
	}

	// Thrown and still flying fast: the first enemy he slides into goes down too, and he stops there.
	private void Bowl(Vector2 sliding)
	{
		if (sliding.Length() < BowlSpeed)
		{
			Thrown = false; // slowed down: just sliding now
			return;
		}
		Enemy hit = GetTree().GetNodesInGroup("enemies").OfType<Enemy>()
			.FirstOrDefault(enemy => enemy != this && !enemy.IsDead && !enemy.IsDown && !enemy.Pinned && enemy.GlobalPosition.DistanceTo(GlobalPosition) < 16f);
		if (hit == null)
			return;
		hit.KnockDown(sliding.Normalized());
		Velocity = Vector2.Zero;
		Thrown = false;
		ShakeCamera.Shake(2f);
		HitStop.Freeze(this, 0.05f);
	}

	// He sees you: stand still, turn to aim, and shoot once his reaction time has passed.
	private void Fire(float dt)
	{
		_seesYou = true;
		if (_reactionLeft < 0f)
		{
			_reactionLeft = ReactionFor(_player.GlobalPosition);
			Startle(); // just the look: the reaction time stays the same
		}
		_alerted = true;
		_checkSpot = _player.GlobalPosition;
		_checkSpread = SearchSpread;
		_searchLeft = SearchTime;
		_goingBack = false;

		Velocity = Vector2.Zero;
		Vector2 toYou = _player.GlobalPosition - GlobalPosition;
		float aim = toYou.Angle();
		Visual.Rotation = Mathf.RotateToward(Visual.Rotation, aim, Mathf.DegToRad(TurnSpeed) * dt);
		_reactionLeft = Mathf.Max(_reactionLeft - dt, 0f); // one reaction: then he keeps at it at his weapon's pace while he sees you

		if (Gun.Data.Melee)
		{
			if (_reactionLeft > 0f)
				return;
			if (toYou.Length() > SwingRange)
				RunTo(_player.GlobalPosition, ChargeSpeed, dt);
			Visual.Rotation = aim;
			if (toYou.Length() <= SwingRange && DevSettings.Current.EnemiesShoot)
				TrySwing();
			return;
		}

		// Then he walks at you while he shoots, around walls and furniture, still aiming at you.
		if (_reactionLeft <= 0f && toYou.Length() > StandOff)
		{
			float facing = Visual.Rotation;
			RunTo(_player.GlobalPosition, ChaseSpeed, dt);
			Visual.Rotation = facing;
		}

		bool onTarget = Mathf.Abs(Mathf.AngleDifference(Visual.Rotation, aim)) < Mathf.DegToRad(10f);
		if (_reactionLeft <= 0f && onTarget && DevSettings.Current.EnemiesShoot)
			Gun.TryFire();
	}

	// How long he takes to fire after spotting you at `spot`: quickest close up and in
	// front of him, slower far away or behind him, plus a little randomness.
	private float ReactionFor(Vector2 spot)
	{
		Vector2 toSpot = spot - GlobalPosition;
		float far = Mathf.Clamp(toSpot.Length() / SightRange, 0f, 1f);
		float behind = Mathf.Abs(Visual.GlobalTransform.X.AngleTo(toSpot)) / Mathf.Pi;
		return ReactionTime + FarReaction * far + BehindReaction * behind + GD.Randf() * RandomReaction;
	}

	// Run to where he last saw or heard you; if you're not there, give up after a moment.
	private void Search(Vector2 spot, float dt)
	{
		_reactionLeft = -1f; // when he sees you again, he needs a moment again
		if (RunTo(spot, ChaseSpeed, dt, _checkSpread))
			return;

		Velocity = Vector2.Zero;
		LookAround = true;
		_searchLeft -= dt;
		if (_searchLeft <= 0)
		{
			// Nobody here. Back to the routine, walking the way he faces now
			// (or first back to his own room, if he stays in it).
			_checkSpot = null;
			_alerted = false;
			_goingBack = StayInRoom && Behaviour != Routine.Static;
			_heading = Vector2.Right.Rotated(Mathf.Snapped(Visual.Rotation, Mathf.Pi / 2));
			_walkLeft = 0;
			_waitLeft = 0;
		}
	}

	private void FollowRoutine(float dt)
	{
		if (_goingBack)
		{
			if (!RunTo(_post, WalkSpeed, dt))
				_goingBack = false;
			return;
		}

		switch (Behaviour)
		{
			case Routine.Static:
				// Back to his post, then face the way he was facing. (At it, no route: a post on furniture he sits on has none.)
				if (GlobalPosition.DistanceTo(_post) < 4f || !RunTo(_post, WalkSpeed, dt))
				{
					Velocity = Vector2.Zero;
					Visual.Rotation = _postAngle;
				}
				break;

			case Routine.Patrol:
				Patrol(dt);
				break;

			case Routine.Wander:
				Wander(dt);
				break;
		}
	}

	// Straight ahead until something's in the way, then turn left 10 degrees a tick (standing
	// still while it's blocked) until the way is clear and he faces straight along the walls again.
	private void Patrol(float dt)
	{
		if (SteppingOut(WalkSpeed))
			return;
		bool blocked = Blocked(_heading) || Stuck(dt);
		if (blocked || _turning)
		{
			_heading = _heading.Rotated(-Mathf.DegToRad(10f));
			float squared = Mathf.Snapped(_heading.Angle(), Mathf.Pi / 2);
			_turning = blocked || Mathf.Abs(Mathf.AngleDifference(_heading.Angle(), squared)) > Mathf.DegToRad(5f);
			if (!_turning)
				_heading = Vector2.Right.Rotated(squared);
		}
		if (Blocked(_heading))
			Velocity = Vector2.Zero;
		else
			Walk(_heading);
	}

	// Walk a few steps in a random direction, stop, look around, repeat.
	private void Wander(float dt)
	{
		if (_walkLeft > 0)
		{
			_walkLeft -= dt;
			if (SteppingOut(WalkSpeed))
				return;
			if (Blocked(_heading) || Stuck(dt))
				_heading = OpenDirection(); // off walls, furniture (and doorways, if he stays in his room)
			Walk(_heading);
			if (_walkLeft <= 0)
			{
				_waitLeft = (float)GD.RandRange(1.0, 2.5);
				_lookedAround = false;
			}
			return;
		}

		Velocity = Vector2.Zero;
		_waitLeft -= dt;
		if (!_lookedAround && _waitLeft < 0.6f)
		{
			_lookedAround = true;
			Visual.Rotation = GD.Randf() * Mathf.Tau;
		}
		LookAround = !_lookedAround; // glancing about, until he turns to face some way
		if (_waitLeft <= 0)
		{
			_heading = OpenDirection();
			_walkLeft = (float)GD.RandRange(1.0, 3.0);
		}
	}

	// A random direction he can walk in, if he finds one in a few tries.
	private Vector2 OpenDirection()
	{
		Vector2 direction = Vector2.Right.Rotated(GD.Randf() * Mathf.Tau);
		for (int tries = 0; tries < 8 && Blocked(direction); tries++)
			direction = Vector2.Right.Rotated(GD.Randf() * Mathf.Tau);
		return direction;
	}

	// Empty-handed: run to the nearest gun he can see (never one behind glass) and pick it up.
	private void GoForAGun(float dt)
	{
		WeaponPickup nearest = WeaponPickup.Nearest(GetTree(), GlobalPosition, SightRange, spot => ClearView(spot, Layers.Glass));

		if (nearest == null)
		{
			// Nothing to grab: just watch you (enemies never punch).
			Velocity = Vector2.Zero;
			if (CanSeePlayer())
				Visual.LookAt(_player.GlobalPosition);
			return;
		}

		if (RunTo(nearest.GlobalPosition, ChaseSpeed, dt))
			return;
		Velocity = Vector2.Zero;
		if (GlobalPosition.DistanceTo(nearest.GlobalPosition) <= 32f) // a gun can land where nobody fits, but never grab one from across the room
		{
			PickUp(nearest);
			_reactionLeft = -1f;
		}
	}

	private void Walk(Vector2 direction)
	{
		Velocity = direction * WalkSpeed;
		Visual.Rotation = direction.Angle();
	}

	// Heads for a spot on a route around walls and furniture (see LevelGrid), planning a new
	// route when the spot moves or he gets stuck. spread: aims for a random open spot this close to it
	// (and not behind a wall from it) instead. Returns false once he has arrived, or if there's no way there.
	private bool RunTo(Vector2 spot, float speed, float dt, float spread = 0f)
	{
		if (SteppingOut(speed))
			return true;
		if (_route == null || _goal.DistanceTo(spot) > 8f || (_route.Count > 0 && Stuck(dt)))
		{
			_goal = spot;
			Vector2 near = spot;
			for (int tries = 0; tries < 6 && spread > 0f; tries++)
			{
				Vector2 pick = spot + Vector2.Right.Rotated(GD.Randf() * Mathf.Tau) * spread * Mathf.Sqrt(GD.Randf());
				if (LevelGrid.IsOpen(pick) && !LevelGrid.WallBetween(spot, pick))
				{
					near = pick;
					break;
				}
			}
			_route = LevelGrid.Path(GlobalPosition, near);
			_stuckTime = 0f;
			if (_route == null) // the map isn't ready yet (the first frames)
			{
				Velocity = Vector2.Zero;
				return true;
			}
		}

		while (_route.Count > 0 && GlobalPosition.DistanceTo(_route[0]) < 3f)
			_route.RemoveAt(0);
		if (_route.Count == 0)
			return false;

		Vector2 toNext = _route[0] - GlobalPosition;
		Velocity = toNext.Normalized() * speed;
		Visual.Rotation = toNext.Angle();
		return true;
	}

	// Body in something (a couch he sat in, pressed into a wall, shoved into a corner):
	// walk straight onto the nearest open floor first, nothing stopping him (the way there crosses no
	// wall, so at most he brushes one), so he can never wedge himself. True while at it.
	private bool SteppingOut(float speed)
	{
		if (_stepOut == null)
		{
			if (!LevelGrid.Wedged(GlobalPosition, Layers.World | Layers.Furniture) || LevelGrid.StepOut(GlobalPosition) is not Vector2 step)
				return false;
			_stepOut = step;
		}
		Vector2 toStep = _stepOut.Value - GlobalPosition;
		if (toStep.Length() < 2f)
		{
			_stepOut = null;
			_route = null; // plan from here
			return false;
		}
		Velocity = toStep.Normalized() * speed;
		Visual.Rotation = toStep.Angle();
		return true;
	}

	// Trying to walk, but hardly moving for a quarter of a second: he's caught on something.
	// Call it only while he's meant to be walking.
	private bool Stuck(float dt)
	{
		bool barelyMoving = GetRealVelocity().Length() < WalkSpeed * 0.3f;
		_stuckTime = barelyMoving ? _stuckTime + dt : 0f;
		if (_stuckTime < 0.25f)
			return false;
		_stuckTime = 0f;
		return true;
	}

	// Would a step this way bring his body up against a wall, furniture or the edge of the
	// floor (or take him through a doorway, if he stays in his room)?
	private bool Blocked(Vector2 direction)
	{
		if (!LevelGrid.IsOpen(GlobalPosition + direction * 8f))
			return true;

		if (StayInRoom)
		{
			foreach (Node node in GetTree().GetNodesInGroup("doors"))
			{
				if (node is Door door && door.Crosses(GlobalPosition, GlobalPosition + direction * 14f, out _))
					return true;
			}
		}
		return false;
	}

	// Once he knows you're around he watches all the time. Calm, he only looks around now and then
	// (enemies not all at once), and after spotting you carries on for a moment before he reacts.
	private bool Notices(float dt)
	{
		if (_alerted)
		{
			_noticeLeft = -1f;
			return CanSeePlayer();
		}
		if (_noticeLeft < 0f)
		{
			if (_lookLeft < 0f)
				_lookLeft = GD.Randf() * LookEvery;
			_lookLeft -= dt;
			if (_lookLeft > 0f)
				return false;
			_lookLeft = LookEvery;
			if (!CanSeePlayer())
				return false;
			_noticeLeft = NoticeTime;
		}
		_noticeLeft -= dt;
		if (_noticeLeft > 0f)
			return false;
		// He's onto you: if you've slipped out of sight meanwhile, he goes to where you are now.
		_noticeLeft = -1f;
		_alerted = true;
		_checkSpot = _player.GlobalPosition;
		_checkSpread = SearchSpread;
		_searchLeft = SearchTime;
		return CanSeePlayer();
	}

	private bool CanSeePlayer()
	{
		if (!DevSettings.Current.EnemiesSeeYou)
			return false;

		Vector2 toPlayer = _player.GlobalPosition - GlobalPosition;
		return toPlayer.Length() <= SightRange && ClearView(_player.GlobalPosition);
	}

	// A ray from him to the spot that only notices walls and doors (and `alsoStops`). If it hits nothing,
	// nothing's in the way.
	private bool ClearView(Vector2 spot, uint alsoStops = 0)
	{
		var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, spot, Layers.World | Layers.Doors | alsoStops);
		return GetWorld2D().DirectSpaceState.IntersectRay(query).Count == 0;
	}

	// ---------------------------------------------------------------- dev settings: "show enemy info"
	// Draws what he can see (white = calm, yellow = searching, red = sees you) and where he's headed.
	private bool _drawnInfo;

	public override void _Draw()
	{
		_drawnInfo = DevSettings.Current.ShowEnemyInfo && !IsDead;
		if (!_drawnInfo)
			return;

		Color color = _seesYou ? new Color(1f, 0.2f, 0.2f, 0.5f)
			: _checkSpot != null ? new Color(1f, 0.85f, 0.2f, 0.45f)
			: new Color(1f, 1f, 1f, 0.3f);
		DrawArc(Vector2.Zero, SightRange, 0f, Mathf.Tau, 48, color, 1f);
		if (_checkSpot is Vector2 spot)
		{
			DrawLine(Vector2.Zero, ToLocal(spot), color, 1f);
			DrawCircle(ToLocal(spot), 3f, color);
		}
	}
}

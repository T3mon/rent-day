using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RentDay;

// A goon. What he does until he notices you depends on his Behaviour:
//   Static - stands guard facing one way. Reacts only to what he sees, never to gunshots.
//   Patrol - walks straight ahead; when something blocks his way he turns left, bit by bit, until
//            the way is clear and he faces straight along the walls again.
//   Wander - walks a few steps in a random direction, stops, looks around, repeats.
// Patrolling and wandering goons hear your gunshots and run to where the shot came from.
// When a goon sees you he stops, turns to aim, and fires after a short reaction time: longer
// when you're far away or behind him, with a little randomness. That delay is your window.
// After that he keeps firing at his gun's goon pace for as long as he sees you.
// A goon with a bat or knife instead runs at you after that delay and swings once you're in reach.
// If he loses you, he runs to around where he last saw or heard you (each goon to a slightly
// different spot, so they don't pile up), on a route around walls (see LevelGrid). If you're not there, he
// looks around for a moment and goes back to his routine (a static goon returns to his post).
// A goon whose weapon got knocked away runs for the nearest weapon he can see.
// Knocked down into a wall, he sits slumped against it instead of lying flat.
// Pose is only the look: what he does while calm and standing still (smoking, on the phone...).
//
// Change the defaults for every goon in enemies/enemy.tscn; change one goon in the level.
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
	[Export] public float SightRange = 300f;    // pixels
	[Export] public float FieldOfView = 140f;   // degrees he can see while unaware

	[ExportGroup("Reaction")]
	[Export] public float ReactionTime = 0.3f;    // seconds before his first shot when you're close and in front of him
	[Export] public float FarReaction = 0.3f;     // extra seconds when you're at the edge of his sight range
	[Export] public float BehindReaction = 0.4f;  // extra seconds when you're right behind him
	[Export] public float RandomReaction = 0.15f; // up to this many extra seconds, at random
	[Export] public float TurnSpeed = 540f;       // degrees per second he can turn while aiming at you

	[ExportGroup("Thrown")]
	[Export] public float SplatSpeed = 250f;    // thrown straight into a wall at least this fast (pixels per second): he dies on it
	[Export] public float BowlSpeed = 150f;     // thrown and still this fast: the goon he slides into goes down too

	[ExportGroup("Chase")]
	[Export] public float ChaseSpeed = 130f;    // running after you
	[Export] public float ChargeSpeed = 180f;   // with a bat or knife, running at you once he's seen you
	[Export] public float SwingRange = 26f;     // he swings once you're this close
	[Export] public float SearchTime = 1.5f;    // seconds he looks around an empty spot before giving up
	[Export] public float SearchSpread = 12f;   // runs to a random spot this close to where he last saw or heard you

	private static readonly AudioStream WallHit = GD.Load<AudioStream>("res://audio/wall_hit.wav");

	private Player _player;
	private AudioStreamPlayer2D _wallSound;
	private bool _alerted;          // knows you're around: sees all around him, not just in front
	private bool _seesYou;
	private float _reactionLeft = -1f; // counting down to his first shot; below zero = not counting yet
	private Vector2? _checkSpot;    // where he last saw or heard you
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
	private bool _turning;          // patrol: turning left until the way is clear

	public override void _Ready()
	{
		base._Ready();
		Gun.HeldByGoon = true;
		Visual.RotationDegrees = FacingDegrees;
		_post = GlobalPosition;
		_postAngle = Visual.Rotation;
		_heading = Vector2.Right.Rotated(_postAngle);
		_player = GetTree().GetFirstNodeInGroup("player") as Player;
		_wallSound = new AudioStreamPlayer2D { Stream = WallHit };
		AddChild(_wallSound);
	}

	// A gunshot went off within earshot, at `where`.
	public void Hear(Vector2 where)
	{
		if (Behaviour == Routine.Static || IsDead || IsDown || _seesYou)
			return;
		_alerted = true;
		_checkSpot = where;
		_searchLeft = SearchTime;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (DevSettings.Current.ShowGoonInfo || _drawnInfo)
			QueueRedraw();
		if (IsDead || Pinned)
		{
			IdlePose = null;
			return;
		}

		float dt = (float)delta;
		UpdateSwing(dt);
		_seesYou = false;

		if (IsDown)
		{
			// Knocked flat: slide to a stop. When he gets up, he knows you're around.
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
		else if (CanSeePlayer())
			Fire(dt);
		else if (_checkSpot is Vector2 spot)
			Search(spot, dt);
		else
			FollowRoutine(dt);

		string pose = Pose == Idle.None || _alerted || Velocity != Vector2.Zero ? null : Pose.ToString().ToLowerInvariant();
		if (Pose == Idle.Sit) // sitting on furniture: he can be in it until something alerts him, then he gets up off it
			CollisionMask = _alerted ? CollisionMask | Layers.Furniture : CollisionMask & ~Layers.Furniture;
		if (IdlePose == "smoke" && pose == null && _alerted) // flicks the cigarette away
			Shell.Eject(this, GlobalPosition + Vector2.Down.Rotated(Visual.GlobalRotation) * 12f, Visual.GlobalRotation, Colors.White);
		IdlePose = pose;

		MoveAndSlide();
	}

	// Thrown and still flying fast: the first goon he slides into goes down too, and he stops there.
	private void Bowl(Vector2 sliding)
	{
		if (sliding.Length() < BowlSpeed)
		{
			Thrown = false; // slowed down: just sliding now
			return;
		}
		Enemy hit = GetTree().GetNodesInGroup("enemies").OfType<Enemy>()
			.FirstOrDefault(goon => goon != this && !goon.IsDead && !goon.IsDown && !goon.Pinned && goon.GlobalPosition.DistanceTo(GlobalPosition) < 16f);
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
			if (toYou.Length() <= SwingRange && DevSettings.Current.GoonsShoot)
				TrySwing();
			return;
		}

		bool onTarget = Mathf.Abs(Mathf.AngleDifference(Visual.Rotation, aim)) < Mathf.DegToRad(10f);
		if (_reactionLeft <= 0f && onTarget && DevSettings.Current.GoonsShoot)
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
		if (RunTo(spot, ChaseSpeed, dt, SearchSpread))
			return;

		Velocity = Vector2.Zero;
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

	// Empty-handed: run to the nearest gun he can see and pick it up.
	private void GoForAGun(float dt)
	{
		WeaponPickup nearest = WeaponPickup.Nearest(GetTree(), GlobalPosition, SightRange, ClearView);

		if (nearest == null)
		{
			// Nothing to grab: just watch you (goons never punch).
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
	// route when the spot moves or he gets stuck. spread: aims for a random spot this close to it
	// instead. Returns false once he has arrived, or if there's no way there.
	private bool RunTo(Vector2 spot, float speed, float dt, float spread = 0f)
	{
		if (_route == null || _goal.DistanceTo(spot) > 8f || (_route.Count > 0 && Stuck(dt)))
		{
			_goal = spot;
			Vector2 near = spot + Vector2.Right.Rotated(GD.Randf() * Mathf.Tau) * spread * Mathf.Sqrt(GD.Randf());
			_route = LevelGrid.Path(GlobalPosition, LevelGrid.IsOpen(near) ? near : spot);
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

	private bool CanSeePlayer()
	{
		if (!DevSettings.Current.GoonsSeeYou)
			return false;

		Vector2 toPlayer = _player.GlobalPosition - GlobalPosition;
		if (toPlayer.Length() > SightRange)
			return false;

		// Until something alerts him, he only sees what's in front of him.
		if (!_alerted)
		{
			float offAxis = Mathf.RadToDeg(Mathf.Abs(Visual.GlobalTransform.X.AngleTo(toPlayer)));
			if (offAxis > FieldOfView / 2f)
				return false;
		}

		return ClearView(_player.GlobalPosition);
	}

	// A ray from him to the spot that only notices walls and doors. If it hits nothing, nothing's in the way.
	private bool ClearView(Vector2 spot)
	{
		var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, spot, Layers.World | Layers.Doors);
		return GetWorld2D().DirectSpaceState.IntersectRay(query).Count == 0;
	}

	// ---------------------------------------------------------------- dev settings: "show goon info"
	// Draws what he can see (white = calm, yellow = searching, red = sees you) and where he's headed.
	private bool _drawnInfo;

	public override void _Draw()
	{
		_drawnInfo = DevSettings.Current.ShowGoonInfo && !IsDead;
		if (!_drawnInfo)
			return;

		Color color = _seesYou ? new Color(1f, 0.2f, 0.2f, 0.5f)
			: _checkSpot != null ? new Color(1f, 0.85f, 0.2f, 0.45f)
			: new Color(1f, 1f, 1f, 0.3f);
		float facing = Visual.Rotation;
		float half = _alerted ? Mathf.Pi : Mathf.DegToRad(FieldOfView / 2f);
		DrawArc(Vector2.Zero, SightRange, facing - half, facing + half, 48, color, 1f);
		if (!_alerted)
		{
			DrawLine(Vector2.Zero, Vector2.Right.Rotated(facing - half) * SightRange, color, 1f);
			DrawLine(Vector2.Zero, Vector2.Right.Rotated(facing + half) * SightRange, color, 1f);
		}
		if (_checkSpot is Vector2 spot)
		{
			DrawLine(Vector2.Zero, ToLocal(spot), color, 1f);
			DrawCircle(ToLocal(spot), 3f, color);
		}
	}
}

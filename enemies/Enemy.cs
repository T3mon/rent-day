using Godot;

namespace RentDay;

// A goon. What he does until he notices you depends on his Behaviour:
//   Static - stands guard facing one way. Reacts only to what he sees, never to gunshots.
//   Patrol - walks straight ahead and turns 90 degrees left whenever something blocks his way.
//   Wander - walks a few steps in a random direction, stops, looks around, repeats.
// Patrolling and wandering goons hear your gunshots and run to where the shot came from.
// When a goon sees you he stops, turns to aim, and fires after a short reaction time: longer
// when you're far away or behind him, with a little randomness. That delay is your window.
// If he loses you, he runs to where he last saw or heard you. If you're not there, he
// looks around for a moment and goes back to his routine (a static goon returns to his post).
// A goon whose gun got knocked away runs for the nearest gun he can see.
//
// Change the defaults for every goon in enemies/enemy.tscn; change one goon in the level.
public partial class Enemy : Character
{
	public enum Routine { Static, Patrol, Wander }

	[ExportGroup("Routine")]
	[Export] public Routine Behaviour = Routine.Static;
	[Export] public float FacingDegrees = 0f;   // where he looks at the start (0 = right, 90 = down); a patroller sets off this way
	[Export] public bool StayInRoom;            // patrol and wander only: never walks out through a doorway on his own
	[Export] public float WalkSpeed = 55f;      // patrolling and wandering, pixels per second

	[ExportGroup("Senses")]
	[Export] public float SightRange = 300f;    // pixels
	[Export] public float FieldOfView = 140f;   // degrees he can see while unaware

	[ExportGroup("Reaction")]
	[Export] public float ReactionTime = 0.3f;    // seconds before his first shot when you're close and in front of him
	[Export] public float FarReaction = 0.3f;     // extra seconds when you're at the edge of his sight range
	[Export] public float BehindReaction = 0.4f;  // extra seconds when you're right behind him
	[Export] public float RandomReaction = 0.15f; // up to this many extra seconds, at random
	[Export] public float TurnSpeed = 540f;       // degrees per second he can turn while aiming at you

	[ExportGroup("Chase")]
	[Export] public float ChaseSpeed = 130f;    // running after you
	[Export] public float SearchTime = 1.5f;    // seconds he looks around an empty spot before giving up

	private Player _player;
	private NavigationAgent2D _nav;
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

	public override void _Ready()
	{
		base._Ready();
		Gun.HeldByGoon = true;
		Visual.RotationDegrees = FacingDegrees;
		_post = GlobalPosition;
		_postAngle = Visual.Rotation;
		_heading = Vector2.Right.Rotated(_postAngle);
		_nav = GetNode<NavigationAgent2D>("Nav");
		_player = GetTree().GetFirstNodeInGroup("player") as Player;
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
		if (IsDead)
			return;

		float dt = (float)delta;
		_seesYou = false;

		if (IsDown)
		{
			// Knocked flat: slide to a stop. When he gets up, he knows you're around.
			Velocity = Velocity.MoveToward(Vector2.Zero, 900f * dt);
			MoveAndSlide();
			_alerted = true;
			_reactionLeft = -1f;
			return;
		}

		if (_player == null || _player.IsDead)
			Velocity = Vector2.Zero;
		else if (!HasWeapon)
			GoForAGun();
		else if (CanSeePlayer())
			Fire(dt);
		else if (_checkSpot is Vector2 spot)
			Search(spot, dt);
		else
			FollowRoutine(dt);

		MoveAndSlide();
	}

	// He sees you: stand still, turn to aim, and shoot once his reaction time has passed.
	private void Fire(float dt)
	{
		_seesYou = true;
		if (_reactionLeft < 0f)
			_reactionLeft = ReactionFor(_player.GlobalPosition);
		_alerted = true;
		_checkSpot = _player.GlobalPosition;
		_searchLeft = SearchTime;
		_goingBack = false;

		Velocity = Vector2.Zero;
		float aim = (_player.GlobalPosition - GlobalPosition).Angle();
		Visual.Rotation = Mathf.RotateToward(Visual.Rotation, aim, Mathf.DegToRad(TurnSpeed) * dt);
		_reactionLeft -= dt;

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
		if (RunTo(spot, ChaseSpeed))
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
			if (!RunTo(_post, WalkSpeed))
				_goingBack = false;
			return;
		}

		switch (Behaviour)
		{
			case Routine.Static:
				// Back to his post, then face the way he was facing.
				if (!RunTo(_post, WalkSpeed))
				{
					Velocity = Vector2.Zero;
					Visual.Rotation = _postAngle;
				}
				break;

			case Routine.Patrol:
				if (Blocked(_heading, out _) || Stuck(dt))
					_heading = _heading.Rotated(-Mathf.Pi / 2); // turn left
				Walk(_heading);
				break;

			case Routine.Wander:
				Wander(dt);
				break;
		}
	}

	// Walk a few steps in a random direction, stop, look around, repeat.
	private void Wander(float dt)
	{
		if (_walkLeft > 0)
		{
			_walkLeft -= dt;
			if (Blocked(_heading, out Vector2 wall))
				_heading = _heading.Bounce(wall); // bounce off walls, furniture (and doorways, if he stays in his room)
			else if (Stuck(dt))
				_heading = _heading.Rotated(Mathf.Pi * (float)GD.RandRange(0.5, 1.5)); // wedged: try another way
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
			_heading = Vector2.Right.Rotated(GD.Randf() * Mathf.Tau);
			_walkLeft = (float)GD.RandRange(1.0, 3.0);
		}
	}

	// Empty-handed: run to the nearest gun he can see and pick it up.
	private void GoForAGun()
	{
		WeaponPickup nearest = WeaponPickup.Nearest(GetTree(), GlobalPosition, SightRange, ClearView);

		if (nearest == null)
		{
			// Nothing to grab: just watch you (fists come later).
			Velocity = Vector2.Zero;
			if (CanSeePlayer())
				Visual.LookAt(_player.GlobalPosition);
			return;
		}

		if (!RunTo(nearest.GlobalPosition, ChaseSpeed))
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

	// Heads for a spot along a route around walls and furniture (the level's navigation map).
	// Returns false once he has arrived.
	private bool RunTo(Vector2 spot, float speed)
	{
		if (_nav.TargetPosition.DistanceTo(spot) > 2f)
			_nav.TargetPosition = spot;
		if (_nav.IsNavigationFinished())
			return false;

		Vector2 toNext = _nav.GetNextPathPosition() - GlobalPosition;
		if (toNext.Length() < 0.5f)
			toNext = spot - GlobalPosition; // no route yet (first frame): head straight for it
		Velocity = toNext.Normalized() * speed;
		Visual.Rotation = toNext.Angle();
		return true;
	}

	// Trying to walk, but hardly moving for a quarter of a second: he's caught on a corner
	// the look-ahead below missed (it only checks straight ahead of his middle).
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

	// Is a wall or piece of furniture right in front of him (or a doorway, if he stays in
	// his room)? `normal` is the side it faces.
	private bool Blocked(Vector2 direction, out Vector2 normal)
	{
		Vector2 ahead = GlobalPosition + direction * 14f;
		var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, ahead, Layers.World | Layers.Furniture);
		var hit = GetWorld2D().DirectSpaceState.IntersectRay(query);
		if (hit.Count > 0)
		{
			normal = (Vector2)hit["normal"];
			return true;
		}

		if (StayInRoom)
		{
			foreach (Node node in GetTree().GetNodesInGroup("doors"))
			{
				if (node is Door door && door.Crosses(GlobalPosition, ahead, out normal))
					return true;
			}
		}

		normal = Vector2.Zero;
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

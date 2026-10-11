using Godot;

namespace RentDay;

// A mushroom-headed goon. He stands guard facing one way. When he spots you
// he turns, hesitates for a moment (your window to act), then opens fire.
// If you break line of sight, he walks to where he last saw you.
public partial class Enemy : Character
{
	[Export] public float FacingDegrees = 0f;   // where he looks before noticing you (0 = right, 90 = down)
	[Export] public float SightRange = 300f;    // pixels
	[Export] public float FieldOfView = 140f;   // degrees he can see while unaware
	[Export] public float ReactionTime = 0.35f; // seconds from spotting you to the first shot
	[Export] public float ChaseSpeed = 90f;     // pixels per second

	private Player _player;
	private bool _alerted;
	private float _reactionLeft;
	private Vector2? _lastSeenAt;

	public override void _Ready()
	{
		base._Ready();
		Visual.RotationDegrees = FacingDegrees;
		_player = GetTree().GetFirstNodeInGroup("player") as Player;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (IsDead || _player == null || _player.IsDead)
			return;

		float dt = (float)delta;

		if (CanSeePlayer())
		{
			if (!_alerted)
			{
				_alerted = true;
				_reactionLeft = ReactionTime;
			}
			_lastSeenAt = _player.GlobalPosition;

			// Stand still, aim, and shoot once the reaction time has passed.
			Velocity = Vector2.Zero;
			Visual.LookAt(_player.GlobalPosition);
			_reactionLeft -= dt;
			if (_reactionLeft <= 0)
				Gun.TryFire();
		}
		else if (_lastSeenAt is Vector2 target)
		{
			// Lost you: go to where you were last seen.
			Vector2 toTarget = target - GlobalPosition;
			if (toTarget.Length() < 6f)
			{
				_lastSeenAt = null;
				Velocity = Vector2.Zero;
			}
			else
			{
				Velocity = toTarget.Normalized() * ChaseSpeed;
				Visual.Rotation = toTarget.Angle();
			}

			// When he sees you again, he needs a moment again.
			_reactionLeft = ReactionTime;
		}
		else
		{
			Velocity = Vector2.Zero;
		}

		MoveAndSlide();
	}

	private bool CanSeePlayer()
	{
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

		// A ray from him to you that only notices walls. If it hits nothing, nothing's in the way.
		var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, _player.GlobalPosition, Layers.World);
		return GetWorld2D().DirectSpaceState.IntersectRay(query).Count == 0;
	}
}

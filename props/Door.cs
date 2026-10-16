using Godot;

namespace RentDay;

// A swinging door. Walk into it and it flies open, away from you; nobody is
// ever blocked by a door. It swings at a fixed speed, slows down and stays
// where it stops: doors don't close by themselves. A goon caught by a door
// you pushed, while it swings hard, goes down. Bullets push doors too.
// Closed doors stop bullets and block the goons' eyesight.
//
// Place the Door node on the hinge and rotate it so the closed door spans the
// doorway; set Length to the doorway's width. Keep its whole swing (MaxAngle
// both ways) clear of walls and furniture. [Tool] makes this script also run
// inside the editor, so changing Length there redraws the door right away.
//
// Children:
//   Panel (AnimatableBody2D, Doors layer) - the solid part bullets and eyesight hit
//     Sweep (Area2D)                     - notices anyone touching the door
//   SlamSound
[Tool]
public partial class Door : Node2D
{
	[Export] public Texture2D Look;

	[Export] public float Length // pixels from hinge to tip
	{
		get => _length;
		set { _length = value; QueueRedraw(); }
	}

	[Export] public float SwingSpeed = 480f;  // how fast it flies open when pushed (degrees per second)
	[Export] public float PlayerSwingSpeedLeftRight = 420f; // your push on doors in left-right walls is softer
	[Export] public float Slowdown = 900f;    // degrees per second lost every second
	[Export] public float SlamSpeed = 210f;   // faster than this it floors goons and ignores new pushes
	[Export] public float MaxAngle = 135f;    // how far it opens either way (degrees); it bounces back off this

	private float _length = 40f;
	private float _angle; // degrees, 0 = closed
	private float _spin;  // degrees per second, + swings toward the door's "down" side
	private bool _swungByPlayer; // only doors you push knock goons down; goons just open them
	private bool _leftRight;     // hangs in a left-right wall
	private Node2D _panel;
	private Area2D _sweep;
	private AudioStreamPlayer2D _slamSound;

	public override void _Ready()
	{
		if (Engine.IsEditorHint())
			return;

		AddToGroup("doors");
		_panel = GetNode<Node2D>("Panel");
		_sweep = GetNode<Area2D>("Panel/Sweep");
		_slamSound = GetNode<AudioStreamPlayer2D>("SlamSound");
		_leftRight = Mathf.Abs(GlobalTransform.X.Normalized().X) > 0.7f;

		// Size the solid part and the touch zone to this door's length.
		FitShape(GetNode<CollisionShape2D>("Panel/Shape"), 4f);
		FitShape(GetNode<CollisionShape2D>("Panel/Sweep/Shape"), 6f);
	}

	// Does the line from `from` to `to` cross this doorway (where the door hangs when closed)?
	// `normal` points out of the doorway, back toward `from`.
	public bool Crosses(Vector2 from, Vector2 to, out Vector2 normal)
	{
		Vector2 hinge = GlobalPosition;
		Vector2 tip = hinge + GlobalTransform.X.Normalized() * Length;
		Vector2 across = (tip - hinge).Orthogonal().Normalized();
		normal = across.Dot(from - hinge) >= 0f ? across : -across;
		return Geometry2D.SegmentIntersectsSegment(from, to, hinge, tip).VariantType != Variant.Type.Nil;
	}

	// A bullet (or a shot fired right against the door) pushes it the way it flies.
	public void Shove(Vector2 direction, float speed, bool byPlayer)
	{
		if (Mathf.Abs(_spin) >= SwingSpeed)
			return;
		float across = direction.Dot(_panel.GlobalTransform.Y.Normalized());
		_spin = Mathf.Clamp(_spin + speed * across, -SwingSpeed, SwingSpeed);
		if (Mathf.Abs(_spin) > SlamSpeed)
			_swungByPlayer = byPlayer;
	}

	private void FitShape(CollisionShape2D node, float thickness)
	{
		node.Shape = new RectangleShape2D { Size = new Vector2(Length, thickness) };
		node.Position = new Vector2(Length / 2f, 0f);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Engine.IsEditorHint())
			return;

		// At rest and nobody near it: nothing to do this frame.
		if (_spin == 0f && !_sweep.HasOverlappingBodies())
			return;

		foreach (Node2D body in _sweep.GetOverlappingBodies())
		{
			if (body is not Character who || who.IsDead || who.IsDown)
				continue;

			if (Mathf.Abs(_spin) > SlamSpeed)
			{
				// Swinging hard: it passes through people, and floors a goon if you pushed it.
				if (who is Enemy && _swungByPlayer)
				{
					who.KnockDown(_panel.GlobalTransform.Y.Normalized() * Mathf.Sign(_spin));
					_slamSound.Play();
					ShakeCamera.Shake(0.45f);
					HitStop.Freeze(this, 0.05f);
				}
				continue;
			}

			// Slow or still: whoever walks into it pushes it open, away from them. Standing
			// still pushes nothing, or a door bouncing back would ping-pong off you forever.
			float side = _panel.ToLocal(who.GlobalPosition).Y >= 0 ? 1f : -1f;
			if (who.Velocity.Dot(_panel.GlobalTransform.Y.Normalized()) * side < -20f)
			{
				_spin = -side * (who is Player && _leftRight ? PlayerSwingSpeedLeftRight : SwingSpeed);
				_swungByPlayer = who is Player;
				who.PushDoor(_panel.GlobalTransform.Y.Normalized() * -side);
			}
			else if (Mathf.Sign(_spin) == side)
				_spin = 0f; // drifting into someone standing still: it comes to rest against them
		}

		if (_spin != 0f)
			Swing((float)delta);
	}

	private void Swing(float dt)
	{
		float step = _spin * dt;
		_angle += step;

		// Walls and furniture bounce it back at half speed. The probe starts a bit out
		// from the hinge, which always sits against the wall the door hangs in.
		Vector2 along = Vector2.Right.Rotated(GlobalRotation + Mathf.DegToRad(_angle));
		var probe = PhysicsRayQueryParameters2D.Create(
			GlobalPosition + along * 10f, GlobalPosition + along * (Length - 2f), Layers.World | Layers.Furniture);
		probe.HitFromInside = true; // it may already be poking into a wall
		if (GetWorld2D().DirectSpaceState.IntersectRay(probe).Count > 0)
		{
			_angle -= step;
			_spin *= -0.5f;
		}

		// The stop throws it back just as fast.
		if (Mathf.Abs(_angle) > MaxAngle)
		{
			_angle = Mathf.Sign(_angle) * MaxAngle;
			_spin = -Mathf.Sign(_angle) * Mathf.Abs(_spin);
		}

		// Drifting slowly past the doorway, it clicks shut; otherwise it slows down.
		if (Mathf.Abs(_spin) < SlamSpeed && Mathf.Abs(_angle) < 6f)
			_angle = _spin = 0f;
		else
			_spin = Mathf.MoveToward(_spin, 0f, Slowdown * dt);

		if (_spin == 0f)
			_swungByPlayer = false;

		_panel.RotationDegrees = _angle;
		QueueRedraw();
	}

	// The door draws itself (with its shadow) instead of using a Sprite2D, so it
	// can be cropped to any Length, in the game and in the editor.
	public override void _Draw()
	{
		if (Look == null)
			return;

		// The art is long enough for the widest doorway; crop it from the hinge end
		// so the knob end always shows.
		Vector2 size = Look.GetSize();
		float width = Mathf.Min(Length, size.X);
		var source = new Rect2(size.X - width, 0f, width, size.Y);
		var target = new Rect2(0f, -size.Y / 2f, Length, size.Y);
		float angle = Mathf.DegToRad(_angle);

		// The shadow falls 2px down-right on screen, whichever way the door is turned.
		Vector2 shadowOffset = GlobalTransform.BasisXformInv(new Vector2(2f, 2f));
		DrawSetTransform(shadowOffset, angle);
		DrawTextureRectRegion(Look, target, source, new Color(0f, 0f, 0f, 0.45f));

		DrawSetTransform(Vector2.Zero, angle);
		DrawTextureRectRegion(Look, target, source);
	}
}

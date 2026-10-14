using System.Collections.Generic;
using Godot;

namespace RentDay;

// A swinging door. Walk into it and it flies open, away
// from you; nobody is ever blocked by a door. A goon standing on the other
// side when it swings open hard gets knocked to the floor.
// Closed doors stop bullets and block the goons' eyesight.
//
// Place the Door node on the hinge and rotate it so the closed door spans the
// doorway; set Length to the doorway's width. [Tool] makes this script also
// run inside the editor, so changing Length there redraws the door right away.
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

	[Export] public float SwingSpeed = 10f;     // how fast it flies open when pushed (radians per second)
	[Export] public float Friction = 4f;        // how quickly a swinging door slows down
	[Export] public float MaxAngle = 100f;      // how far it opens either way (degrees)
	[Export] public float KnockSpeed = 4f;      // swinging at least this fast floors a goon
	[Export] public float KnockDownTime = 2.5f; // seconds he stays down

	private float _length = 40f;
	private float _angle; // radians, 0 = closed
	private float _spin;  // radians per second, + swings toward the door's "down" side
	private bool _swungByPlayer; // only doors you push knock goons down; goons just open them
	private Node2D _panel;
	private Area2D _sweep;
	private AudioStreamPlayer2D _slamSound;
	private Dictionary<Character, float> _touching = new(); // who touches the door, and from which side
	private Dictionary<Character, float> _stillTouching = new();

	public override void _Ready()
	{
		if (Engine.IsEditorHint())
			return;

		AddToGroup("doors");
		_panel = GetNode<Node2D>("Panel");
		_sweep = GetNode<Area2D>("Panel/Sweep");
		_slamSound = GetNode<AudioStreamPlayer2D>("SlamSound");

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
		{
			_touching.Clear();
			return;
		}

		float dt = (float)delta;
		_stillTouching.Clear();
		foreach (Node2D body in _sweep.GetOverlappingBodies())
		{
			if (body is not Character who || who.IsDead || who.IsDown)
				continue;

			// Which side of the door he came from: +1 or -1. Remembered while he
			// touches it, so the door keeps swinging the same way as he walks through.
			if (!_touching.TryGetValue(who, out float side))
				side = _panel.ToLocal(who.GlobalPosition).Y >= 0 ? 1f : -1f;
			_stillTouching[who] = side;

			if (who is Enemy && _swungByPlayer && Mathf.Sign(_spin) == side && Mathf.Abs(_spin) >= KnockSpeed)
			{
				// The door is swinging into him, hard: he goes down.
				Vector2 away = _panel.GlobalTransform.Y.Normalized() * side;
				who.KnockDown(away, KnockDownTime);
				_slamSound.Play();
				ShakeCamera.Shake(0.45f);
				HitStop.Freeze(this, 0.05f);
			}
			else if (Mathf.Sign(_spin) != -side || Mathf.Abs(_spin) < SwingSpeed)
			{
				// Someone walked into the door: it flies open, away from them.
				_spin = -side * SwingSpeed;
				_swungByPlayer = who is Player;
			}
		}
		(_touching, _stillTouching) = (_stillTouching, _touching);

		// Swing, slow down, and bounce a little off the stop.
		_spin *= Mathf.Exp(-Friction * dt);
		if (Mathf.Abs(_spin) < 0.05f)
			_spin = 0f;
		_angle += _spin * dt;

		float max = Mathf.DegToRad(MaxAngle);
		if (Mathf.Abs(_angle) > max)
		{
			_angle = Mathf.Clamp(_angle, -max, max);
			_spin = -_spin * 0.3f;
		}

		if (!Mathf.IsEqualApprox(_panel.Rotation, _angle))
		{
			_panel.Rotation = _angle;
			QueueRedraw();
		}
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

		// The shadow falls 2px down-right on screen, whichever way the door is turned.
		Vector2 shadowOffset = GlobalTransform.BasisXformInv(new Vector2(2f, 2f));
		DrawSetTransform(shadowOffset, _angle);
		DrawTextureRectRegion(Look, target, source, new Color(0f, 0f, 0f, 0.45f));

		DrawSetTransform(Vector2.Zero, _angle);
		DrawTextureRectRegion(Look, target, source);
	}
}

using System.Collections.Generic;
using Godot;

namespace RentDay;

// Melee, for the player and the goons: swinging a bat or knife, or punching with bare fists.
// While one is in hand the arms are drawn here, from the shoulders to the hands, so they can
// carry it through a swing. Everything is held on the right: a bat rests back over the right
// shoulder in both hands and sweeps around the front; the right hand slashes a knife across
// blade first, or jabs. After every swing it all comes back to the same resting place.
public partial class Character
{
	[Export] public WeaponData Fists;            // swung with empty hands; empty = can't punch

	private const float GripRadius = 11f;        // the hands sweep around the body this far out
	private static readonly Vector2 Shoulder = new(0f, 7f);    // the right shoulder (facing +x); the left one is mirrored
	private static readonly Vector2 RestHand = new(1f, 10.5f); // where the right hand hangs

	// All bodies share one picture layout: these rows hold the hands at the sides, and the
	// armed look's arm gives the sleeve and skin colours.
	private static readonly int[] HandRows = { 16, 17, 18, 19, 36, 37, 38, 39 };
	private static readonly Dictionary<Texture2D, Texture2D> BareLooks = new();
	private static readonly Color Ink = new(0.04f, 0.02f, 0.07f);

	private WeaponData _swung;                   // the weapon (or fists) of the swing that's out
	private float _swingLeft;
	private float _swingWait;
	private float _swingAge = float.MaxValue;   // seconds since the last swing started, for the look
	private readonly HashSet<Character> _struck = new(); // hit by this swing already
	private readonly RectangleShape2D _reach = new();
	private AudioStreamPlayer2D _swingSound;
	private AudioStreamPlayer2D _strikeSound;
	private Node2D _arms;
	private Color _sleeve = new(0.9f, 0.3f, 0.55f);
	private Color _skin = new(0.73f, 0.5f, 0.38f);
	private Vector2 _grip;                       // the weapon hand (fists: the punching hand)
	private Vector2 _offHand;
	private float _gripAngle;                    // where the weapon points

	// Bare fists, or a bat or knife, in hand: then the arms are drawn, not part of the body picture.
	private WeaponData HeldMelee => Gun.Data is { Melee: true } ? Gun.Data : Gun.Data == null ? Fists : null;

	private void ReadyMelee()
	{
		_swingSound = new AudioStreamPlayer2D { VolumeDb = -6f };
		_strikeSound = new AudioStreamPlayer2D();
		AddChild(_swingSound);
		AddChild(_strikeSound);
		_arms = new Node2D { Name = "Arms" };
		_alive.AddChild(_arms); // drawn over the body
		_arms.Draw += DrawArms;
		if (ArmedLook?.GetImage() is { } armed)
		{
			_sleeve = armed.GetPixel(38, 30);
			_skin = armed.GetPixel(43, 29);
		}
	}

	// The look for what's in hand, and a fresh start for the swings. Returns the body picture.
	private Texture2D ArmsFor(WeaponData data)
	{
		_swingLeft = 0f;
		_swingAge = float.MaxValue;
		_alive.Rotation = 0f;
		bool arms = HeldMelee != null;
		Gun.Visible = data != null && !arms;     // a bat or knife is drawn in the hands instead
		_arms.Visible = arms;
		PoseArms(0f);
		return arms ? BareLook() : data != null ? ArmedLook : UnarmedLook;
	}

	// The body without the hands at its sides: the empty-handed look, with the rows where its
	// hands stick out taken from the armed look, which has nothing there.
	private Texture2D BareLook()
	{
		if (UnarmedLook == null || ArmedLook == null)
			return UnarmedLook;
		if (!BareLooks.TryGetValue(UnarmedLook, out Texture2D bare))
		{
			Image body = UnarmedLook.GetImage();
			Image armed = ArmedLook.GetImage();
			foreach (int y in HandRows)
			{
				for (int x = 0; x < body.GetWidth(); x++)
					body.SetPixel(x, y, armed.GetPixel(x, y));
			}
			BareLooks[UnarmedLook] = bare = ImageTexture.CreateFromImage(body);
		}
		return bare;
	}

	// Swings what's in hand for hitting (or bare fists) if ready. Returns true if a swing started.
	public bool TrySwing()
	{
		WeaponData weapon = HeldMelee;
		if (weapon == null || _swingWait > 0f || IsDown || IsDead)
			return false;
		_swung = weapon;
		_swingLeft = weapon.SwingTime;
		_swingAge = 0f;
		_swingWait = Gun.HeldByGoon ? weapon.GoonCooldown : weapon.Cooldown;
		_struck.Clear();
		_swingSound.Stream = weapon.ShotSound;
		_swingSound.PitchScale = (float)GD.RandRange(0.9, 1.1);
		_swingSound.Play();
		return true;
	}

	// Call every physics tick: while a swing is out, everyone in reach in front of us is hit,
	// once per swing, unless a wall or door is in between.
	protected void UpdateSwing(float dt)
	{
		_swingWait -= dt;
		if (_swingLeft <= 0f)
			return;
		_swingLeft -= dt;

		Vector2 facing = Vector2.Right.Rotated(Visual.GlobalRotation);
		_reach.Size = new Vector2(_swung.Reach, 22f);
		var query = new PhysicsShapeQueryParameters2D
		{
			Shape = _reach,
			Transform = new Transform2D(facing.Angle(), GlobalPosition + facing * (_swung.Reach / 2f + 1f)),
			CollisionMask = Gun.HeldByGoon ? Layers.Player : Layers.Enemies,
		};
		PhysicsDirectSpaceState2D space = GetWorld2D().DirectSpaceState;
		foreach (var hit in space.IntersectShape(query, 8))
		{
			if (hit["collider"].As<GodotObject>() is not Character target || target.IsDead || (target.IsDown && !_swung.Lethal) || !_struck.Add(target))
				continue;
			var between = PhysicsRayQueryParameters2D.Create(GlobalPosition, target.GlobalPosition, Layers.World | Layers.Doors);
			if (space.IntersectRay(between).Count > 0)
				continue;

			Vector2 direction = (target.GlobalPosition - GlobalPosition).Normalized();
			_strikeSound.Stream = _swung.HitSound;
			_strikeSound.Play();
			if (!Gun.HeldByGoon)
				ShakeCamera.Shake(_swung.Shake);
			if (_swung.Lethal)
				target.Die(direction);
			else
				target.KnockDown(direction);
		}
	}

	// Where the hands and the weapon are this frame, in the upper body's own frame (facing +x).
	private void PoseArms(float dt)
	{
		WeaponData weapon = HeldMelee;
		if (weapon == null)
			return;
		_swingAge += dt;
		float swing = weapon.SwingTime;
		float back = Mathf.Max((Gun.HeldByGoon ? weapon.GoonCooldown : weapon.Cooldown) - swing, 0.06f);

		if (weapon.SwingArc <= 0f)
		{
			// Fists: the right hand jabs out and back, the left stays by the shoulder.
			float t = Mathf.Clamp(_swingAge / swing, 0f, 1f);
			float jab = t < 0.3f ? t / 0.3f : 1f - (t - 0.3f) / 0.7f;
			_grip = RestHand.Lerp(new Vector2(14f, 3f), jab);
			_offHand = RestHand * new Vector2(1f, -1f);
			_alive.Rotation = -0.15f * jab; // a little turn into the punch
		}
		else
		{
			// e runs 0 -> 1 through the swing (fast, then easing to a stop), then back to 0 while the
			// weapon returns to rest. The hands sweep around the front from right to left; the weapon
			// trails behind them (bat) or leads (knife), and points straight out at the moment of impact.
			float e = _swingAge < swing ? 1f - Mathf.Pow(1f - _swingAge / swing, 3f)
				: _swingAge < swing + back ? 1f - Mathf.SmoothStep(0f, 1f, (_swingAge - swing) / back)
				: 0f;
			float hands = Mathf.DegToRad(weapon.SwingArc / 2f) * (2f * e - 1f);
			float lead = Mathf.DegToRad(weapon.SwingLead) * (1f - 2f * e);
			_grip = Vector2.Right.Rotated(-hands) * GripRadius;
			_gripAngle = -(hands + lead);
			_offHand = weapon.TwoHanded ? _grip - Vector2.Right.Rotated(_gripAngle) * 3f : RestHand * new Vector2(1f, -1f);
			_alive.Rotation = -0.25f * Mathf.Sin(Mathf.Pi * e); // the body turns into the swing
		}
		_arms.QueueRedraw();
	}

	private void DrawArms()
	{
		WeaponData weapon = HeldMelee;
		if (weapon == null)
			return;
		if (weapon.Look != null)
		{
			_arms.DrawSetTransform(_grip, _gripAngle);
			_arms.DrawTexture(weapon.Look, weapon.HoldOffset - weapon.Look.GetSize() / 2f);
			_arms.DrawSetTransform(Vector2.Zero, 0f);
		}

		if (weapon.SwingArc <= 0f)
			DrawArm(Shoulder, _grip);
		else
		{
			DrawArm(Shoulder, _grip);
			if (weapon.TwoHanded)
				DrawArm(-Shoulder, _offHand);
		}
		DrawHand(_offHand);
		DrawHand(_grip);
	}

	// A sleeve from the shoulder to the hand; none when the hand is right at the shoulder.
	private void DrawArm(Vector2 from, Vector2 to)
	{
		if (from.DistanceTo(to) < 5f)
			return;
		_arms.DrawLine(from, to, Ink, 5f);
		_arms.DrawLine(from, to, _sleeve, 3f);
	}

	private void DrawHand(Vector2 at)
	{
		at = at.Round();
		_arms.DrawRect(new Rect2(at - new Vector2(2f, 2f), new Vector2(5f, 5f)), Ink);
		_arms.DrawRect(new Rect2(at - new Vector2(1f, 1f), new Vector2(3f, 3f)), _skin);
	}
}

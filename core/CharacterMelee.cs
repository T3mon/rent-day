using System.Collections.Generic;
using Godot;

namespace RentDay;

// Melee, for the player and the enemies: swinging a bat or knife, or punching with bare fists.
// Here is when a swing may start and whom it hits; the swing itself is an animation of the
// body (see CharacterMotion).
public partial class Character
{
	[Export] public WeaponData Fists;            // swung with empty hands; empty = can't punch

	private WeaponData _swung;                   // the weapon (or fists) of the swing that's out
	private float _swingLeft;
	private float _swingWait;
	private readonly HashSet<Character> _struck = new(); // hit by this swing already
	private readonly RectangleShape2D _reach = new();
	private AudioStreamPlayer2D _swingSound;
	private AudioStreamPlayer2D _strikeSound;

	// Bare fists, or a bat or knife, in hand.
	private WeaponData HeldMelee => Gun.Data is { Melee: true } ? Gun.Data : Gun.Data == null ? Fists : null;

	private void ReadyMelee()
	{
		_swingSound = new AudioStreamPlayer2D { VolumeDb = -6f };
		_strikeSound = new AudioStreamPlayer2D();
		AddChild(_swingSound);
		AddChild(_strikeSound);
	}

	// Something else in hand: a swing that's out ends.
	private void ResetSwing() => _swingLeft = 0f;

	// Swings what's in hand for hitting (or bare fists) if ready. Returns true if a swing started.
	public bool TrySwing()
	{
		WeaponData weapon = HeldMelee;
		if (weapon == null || _swingWait > 0f || IsDown || IsDead)
			return false;
		_swung = weapon;
		_swingLeft = weapon.SwingTime;
		_swingWait = Gun.HeldByEnemy ? weapon.EnemyCooldown : weapon.Cooldown;
		_struck.Clear();
		_swingSound.Stream = weapon.ShotSound;
		_swingSound.PitchScale = (float)GD.RandRange(0.9, 1.1);
		_swingSound.Play();
		Act(Kind == "" ? "punch" : Kind + "swing");
		return true;
	}

	// No swings for this many seconds.
	protected void HoldSwings(float seconds) => _swingWait = Mathf.Max(_swingWait, seconds);

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
			CollisionMask = Gun.HeldByEnemy ? Layers.Player : Layers.Enemies,
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
			if (!Gun.HeldByEnemy)
				ShakeCamera.Shake(_swung.Shake);
			if (_swung.Lethal)
				target.Die(direction);
			else
				target.KnockDown(direction);
		}
	}
}

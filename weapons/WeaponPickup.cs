using System;
using Godot;

namespace RentDay;

// A gun lying on the floor, or flying through the air after being thrown or
// dropped. Stand on it and press "grab" to pick it up. A thrown gun knocks down
// the first goon it hits, then clatters to the floor.
public partial class WeaponPickup : CharacterBody2D
{
	[Export] public WeaponData Data;
	[Export] public int Ammo = -1;              // -1 = a fresh gun, full of ammo
	[Export] public float Friction = 700f;      // how quickly a sliding gun stops
	[Export] public float KnockDownTime = 2.5f; // seconds a goon stays down after being hit

	// Moving at least this fast (pixels per second), a thrown gun still hurts.
	private const float DangerousSpeed = 150f;

	private Sprite2D _look;
	private Sprite2D _shadow;
	private AudioStreamPlayer2D _hitSound;
	private float _spin;
	private bool _thrown;

	// Lying still (or nearly), so it can be picked up.
	public bool IsLying => Velocity.Length() < 60f && !IsQueuedForDeletion();

	// The closest gun lying within `range` of `spot` (and that `canReach` agrees to), or null.
	public static WeaponPickup Nearest(SceneTree tree, Vector2 spot, float range, Func<Vector2, bool> canReach = null)
	{
		WeaponPickup nearest = null;
		foreach (Node node in tree.GetNodesInGroup("pickups"))
		{
			if (node is not WeaponPickup gun || !gun.IsLying)
				continue;
			float distance = spot.DistanceTo(gun.GlobalPosition);
			if (distance <= range && (canReach == null || canReach(gun.GlobalPosition)))
			{
				range = distance;
				nearest = gun;
			}
		}
		return nearest;
	}

	public override void _Ready()
	{
		_look = GetNode<Sprite2D>("Look");
		_shadow = GetNode<Sprite2D>("Shadow");
		_hitSound = GetNode<AudioStreamPlayer2D>("HitSound");

		if (Ammo < 0 && Data != null)
			Ammo = Data.Ammo;
		_look.Texture = Data?.FloorLook;
		_shadow.Texture = Data?.FloorLook;
		_look.Rotation = _shadow.Rotation = GD.Randf() * Mathf.Tau;
	}

	// Sends the gun sliding. thrown = true makes it dangerous to goons.
	public void Launch(Vector2 velocity, bool thrown)
	{
		Velocity = velocity;
		_thrown = thrown;
		_spin = thrown ? 28f : (float)GD.RandRange(-10.0, 10.0);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (Velocity == Vector2.Zero)
			return;

		float dt = (float)delta;
		KinematicCollision2D hit = MoveAndCollide(Velocity * dt);
		if (hit != null)
		{
			if (hit.GetCollider() is Enemy goon && _thrown && !goon.IsDown)
			{
				// A thrown gun to the head: he goes down, the gun bounces off him.
				goon.KnockDown(Velocity.Normalized(), KnockDownTime);
				_hitSound.Play();
				ShakeCamera.Shake(0.35f);
				_thrown = false;
				Velocity = -Velocity * 0.2f;
			}
			else if (hit.GetCollider() is Character someone)
			{
				AddCollisionExceptionWith(someone); // a slow gun just slides past people
			}
			else
			{
				Velocity = Velocity.Bounce(hit.GetNormal()) * 0.4f; // off walls and doors
			}
		}

		_look.Rotation = _shadow.Rotation = _look.Rotation + _spin * dt;
		_spin = Mathf.MoveToward(_spin, 0f, 40f * dt);
		Velocity = Velocity.MoveToward(Vector2.Zero, Friction * dt);
		if (Velocity.Length() < DangerousSpeed)
			_thrown = false;
	}
}

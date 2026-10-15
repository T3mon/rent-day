using Godot;

namespace RentDay;

// The gun in someone's hand. Which gun it is (or none, for empty hands) comes
// from a WeaponData resource; this node does the shooting. It sits at the hand
// and is drawn under the body, so the hand covers the grip.
public partial class Gun : Node2D
{
	[Export] public PackedScene BulletScene;
	[Export] public PackedScene SmokeScene;      // puff of smoke at the muzzle on every shot

	public WeaponData Data { get; private set; } // null = empty hands
	public int Ammo { get; private set; }

	// A goon's gun never runs dry, fires at the goon pace, only hurts the player and makes no noise.
	public bool HeldByGoon;

	private float _cooldownLeft;
	private float _flashLeft;
	private Sprite2D _look;
	private Node2D _muzzle;
	private Sprite2D _flash;
	private AudioStreamPlayer2D _shotSound;
	private AudioStreamPlayer2D _emptySound;
	private AudioStreamPlayer2D _pickupSound;

	public override void _Ready()
	{
		_look = GetNode<Sprite2D>("Look");
		_muzzle = GetNode<Node2D>("Muzzle");
		_flash = GetNode<Sprite2D>("Muzzle/Flash");
		_shotSound = GetNode<AudioStreamPlayer2D>("ShotSound");
		_emptySound = GetNode<AudioStreamPlayer2D>("EmptySound");
		_pickupSound = GetNode<AudioStreamPlayer2D>("PickupSound");
	}

	// Puts a gun in the hand (or empties the hand, with null).
	public void Equip(WeaponData data, int ammo, bool playSound = false)
	{
		Data = data;
		Ammo = ammo;
		Visible = data != null;
		_cooldownLeft = 0;
		if (data == null)
			return;

		_look.Texture = data.Look;
		_look.Position = data.HoldOffset;
		_muzzle.Position = data.Muzzle;
		_shotSound.Stream = data.ShotSound;
		if (playSound)
		{
			_pickupSound.Stream = data.PickupSound;
			_pickupSound.Play();
		}
	}

	public override void _Process(double delta)
	{
		_cooldownLeft -= (float)delta;

		// The muzzle flash only shows for a split second after each shot.
		_flashLeft -= (float)delta;
		_flash.Visible = _flashLeft > 0;
	}

	// Fires if the gun is ready. Returns true if bullets came out.
	public bool TryFire()
	{
		if (Data == null || _cooldownLeft > 0)
			return false;

		if (Ammo <= 0 && !HeldByGoon)
		{
			// Out of ammo: just a dry click. Time to throw it.
			_cooldownLeft = 0.25f;
			_emptySound.Play();
			return false;
		}

		_cooldownLeft = HeldByGoon ? Data.GoonCooldown : Data.Cooldown;
		if (!HeldByGoon && !DevSettings.Current.InfiniteAmmo)
			Ammo--;

		// What the bullets can hit. Goons never shoot each other.
		uint mask = Layers.World | Layers.Doors | (HeldByGoon ? Layers.Player : Layers.Enemies);

		// The barrel can poke through a wall the shooter is touching. Check the stretch from the
		// shooter's center (Owner, never inside a wall) to the muzzle: whatever is in the way takes the shot.
		var blocked = GetWorld2D().DirectSpaceState.IntersectRay(
			PhysicsRayQueryParameters2D.Create(((Node2D)Owner).GlobalPosition, _muzzle.GlobalPosition, mask));

		// A shot fired right against a door shoves it at full swing.
		if (blocked.Count > 0 && ((Node)blocked["collider"]).GetParent() is Door door)
			door.Shove(GlobalTransform.X, door.SwingSpeed, !HeldByGoon);

		for (int i = 0; i < Data.Pellets; i++)
		{
			var bullet = BulletScene.Instantiate<Bullet>();
			bullet.CollisionMask = mask;
			bullet.Speed = Data.BulletSpeed * (1f + (float)GD.RandRange(-Data.SpeedVariation, Data.SpeedVariation));
			bullet.Lifetime = Data.Range / bullet.Speed;

			// Add it to the level, not to the shooter, or it would move with them.
			GetTree().CurrentScene.AddChild(bullet);
			bullet.GlobalPosition = _muzzle.GlobalPosition;
			bullet.GlobalRotation = GlobalRotation + Mathf.DegToRad((float)GD.RandRange(-Data.Spread, Data.Spread));
			if (blocked.Count > 0)
			{
				bullet.GlobalPosition = (Vector2)blocked["position"];
				bullet.Hit((Node2D)blocked["collider"]);
			}
		}

		_flashLeft = 0.05f;
		_shotSound.PitchScale = Data.ShotPitch * (float)GD.RandRange(0.95, 1.05); // never the exact same bang twice
		_shotSound.Play();
		Effects.Spawn(SmokeScene, this, _muzzle.GlobalPosition, GlobalRotation);

		if (!HeldByGoon)
		{
			ShakeCamera.Shake(Data.Shake);
			Noise.Make(this, GlobalPosition, Data.Noise);
		}
		return true;
	}
}

using Godot;

namespace RentDay;

// A gun you can give to anyone. It sits at the muzzle (the tip of the barrel)
// and fires bullets the way it points. The player's gun and the goons' guns are
// the same scene with different settings.
public partial class Gun : Marker2D
{
	[Export] public PackedScene BulletScene;
	[Export] public float Cooldown = 0.15f; // minimum seconds between shots

	// What our bullets can hit. The player's gun hits World | Enemies,
	// the goons' guns hit World | Player, so goons never shoot each other.
	[Export(PropertyHint.Layers2DPhysics)] public uint BulletMask = Layers.World | Layers.Enemies;

	[Export] public PackedScene SmokeScene;      // puff of smoke at the muzzle on every shot
	[Export] public float ShakeAmount = 0.35f;   // screen shake per shot (0 for the goons' guns)

	private float _cooldownLeft;
	private float _flashLeft;
	private Sprite2D _flash;
	private AudioStreamPlayer2D _sound;

	public override void _Ready()
	{
		_flash = GetNode<Sprite2D>("Flash");
		_sound = GetNode<AudioStreamPlayer2D>("ShotSound");
	}

	public override void _Process(double delta)
	{
		_cooldownLeft -= (float)delta;

		// The muzzle flash only shows for a split second after each shot.
		_flashLeft -= (float)delta;
		_flash.Visible = _flashLeft > 0;
	}

	// Fires if the gun is ready. Returns true if a bullet came out.
	public bool TryFire()
	{
		if (_cooldownLeft > 0)
			return false;
		_cooldownLeft = Cooldown;

		var bullet = BulletScene.Instantiate<Bullet>();
		bullet.CollisionMask = BulletMask;

		// Add it to the level, not to the shooter, or it would move with them.
		GetTree().CurrentScene.AddChild(bullet);
		bullet.GlobalPosition = GlobalPosition;
		bullet.GlobalRotation = GlobalRotation;

		_flashLeft = 0.05f;
		_sound.Play();
		Effects.Spawn(SmokeScene, this, GlobalPosition, GlobalRotation);
		if (ShakeAmount > 0)
			ShakeCamera.Shake(ShakeAmount);
		return true;
	}
}

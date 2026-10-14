using Godot;

namespace RentDay;

// Everything that makes one kind of gun different from another, kept in a
// resource file (pistol.tres, shotgun.tres, uzi.tres) so it can be tuned in the
// Inspector without touching code. [GlobalClass] makes "WeaponData" show up in
// Godot's "New Resource" list, so making a new gun is: new resource, fill it in.
[GlobalClass]
public partial class WeaponData : Resource
{
	[ExportGroup("Looks")]
	[Export] public string Name = "Pistol";
	[Export] public Texture2D Look;              // drawn in the hand
	[Export] public Texture2D FloorLook;         // drawn on the floor (with a light rim, so it stands out)
	[Export] public Vector2 HoldOffset;          // where the picture's centre sits, measured from the hand
	[Export] public Vector2 Muzzle;              // where bullets come out, measured from the hand

	[ExportGroup("Shooting")]
	[Export] public int Ammo = 6;                // rounds in a fresh gun; there is no reloading
	[Export] public bool Automatic;              // hold the button to keep firing
	[Export] public float Cooldown = 0.18f;      // seconds between your shots
	[Export] public float GoonCooldown = 0.6f;   // seconds between shots when a goon holds it
	[Export] public int Pellets = 1;             // bullets per shot (a shotgun fires several)
	[Export] public float Spread = 1.5f;         // random wobble either side of the aim, in degrees
	[Export] public float BulletSpeed = 750f;    // pixels per second
	[Export] public float SpeedVariation;        // 0.2 = each bullet up to 20% faster or slower
	[Export] public float Range = 600f;          // pixels a bullet flies before vanishing

	[ExportGroup("Feel")]
	[Export] public float Shake = 0.35f;         // screen shake per shot (your shots only)
	[Export] public float Kickback;              // pushes you backwards per shot, pixels per second
	[Export] public float Noise = 240f;          // patrolling and wandering goons this close hear your shot, through walls
	[Export] public AudioStream ShotSound;
	[Export] public float ShotPitch = 1f;        // the same sound pitched down sounds bigger, up sounds smaller
	[Export] public AudioStream PickupSound;     // played when you grab it (slide rack, pump...)
}

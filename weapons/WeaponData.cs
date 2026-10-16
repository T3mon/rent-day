using Godot;

namespace RentDay;

// Everything that makes one kind of weapon different from another, kept in a
// resource file (pistol.tres, bat.tres, fists.tres...) so it can be tuned in the
// Inspector without touching code. Guns shoot; melee weapons (Melee ticked) swing. [GlobalClass] makes "WeaponData" show up in
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
	[Export] public float GoonCooldown = 0.17f;  // seconds between shots (or swings) when a goon holds it
	[Export] public int Pellets = 1;             // bullets per shot (a shotgun fires several)
	[Export] public float Spread = 1.5f;         // random wobble either side of the aim, in degrees
	[Export] public float BulletSpeed = 750f;    // pixels per second
	[Export] public float SpeedVariation;        // 0.2 = each bullet up to 20% faster or slower
	[Export] public float Range = 600f;          // pixels a bullet flies before vanishing

	[ExportGroup("Melee")]
	[Export] public bool Melee;                  // swung, not fired: no ammo, no bullets, no noise (Cooldown is the wait between swings)
	[Export] public bool Lethal = true;          // off: a hit knocks down instead of killing (fists)
	[Export] public float SwingTime = 0.2f;      // seconds a swing hits whatever comes in reach
	[Export] public float SwingArc = 200f;       // degrees the hands sweep around the front in a swing; 0 = a straight jab (fists)
	[Export] public float SwingLead = -65f;      // degrees the weapon points ahead of the hands as a swing starts: below 0 trails behind (a bat held back)
	[Export] public bool TwoHanded = true;       // both hands on it (bat); otherwise the other hand stays by the shoulder
	[Export] public float Reach = 35f;           // a swing hits up to this far in front of you, 22 px wide
	[Export] public bool KillsWhenThrown;        // thrown, it kills instead of knocking down (knife)
	[Export] public AudioStream HitSound;        // when a swing connects

	[ExportGroup("Feel")]
	[Export] public float Shake = 0.35f;         // screen shake per shot or melee hit (yours only)
	[Export] public float Kickback;              // pushes you backwards per shot, pixels per second
	[Export] public float Noise = 240f;          // patrolling and wandering goons this close hear your shot, through walls
	[Export] public AudioStream ShotSound;       // the bang, or the whoosh of a swing
	[Export] public float ShotPitch = 1f;        // the same sound pitched down sounds bigger, up sounds smaller
	[Export] public AudioStream PickupSound;     // played when you grab it (slide rack, pump...)
}

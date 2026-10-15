using Godot;

namespace RentDay;

// Switches for testing. Click debug/dev_settings.tres in the FileSystem dock, change them
// in the Inspector, then press F5: the run uses them. They only work in runs from the
// editor (debug builds); an exported game always plays normally.
// Game code reads them through DevSettings.Current, e.g. DevSettings.Current.GoonsShoot.
[GlobalClass]
public partial class DevSettings : Resource
{
	[ExportGroup("Goons")]
	[Export] public bool GoonsSeeYou = true;      // off: goons are blind to you (they still patrol, wander, hear shots)
	[Export] public bool GoonsHearShots = true;   // off: your gunshots alert nobody
	[Export] public bool GoonsShoot = true;       // off: goons still spot you and aim, but never pull the trigger

	[ExportGroup("You")]
	[Export] public WeaponData StartWeapon;       // start the floor holding this gun (empty = empty-handed, as normal)
	[Export] public bool InfiniteAmmo;            // your guns never run dry

	[ExportGroup("Show")]
	[Export] public bool ShowGoonInfo;            // draw what each goon sees and where he's headed, and how far your shots carry
	[Export] public bool RedGoo;                  // plain red blood instead of the mixed-colour goo

	private const string FilePath = "res://debug/dev_settings.tres";
	private static DevSettings _current;

	// The settings for this run: the file in debug runs, plain defaults (a normal game) otherwise.
	public static DevSettings Current =>
		_current ??= (OS.IsDebugBuild() ? GD.Load<DevSettings>(FilePath) : null) ?? new DevSettings();

	// Short list of what differs from a normal game, for the corner of the screen.
	public string ActiveSummary()
	{
		var on = new System.Collections.Generic.List<string>();
		if (!GoonsSeeYou) on.Add("BLIND GOONS");
		if (!GoonsHearShots) on.Add("DEAF GOONS");
		if (!GoonsShoot) on.Add("NO GOON FIRE");
		if (StartWeapon != null) on.Add("START " + StartWeapon.Name.ToUpper());
		if (InfiniteAmmo) on.Add("INF AMMO");
		if (ShowGoonInfo) on.Add("GOON INFO");
		if (RedGoo) on.Add("RED GOO");
		return on.Count > 0 ? "DEV: " + string.Join("  ", on) : "";
	}
}

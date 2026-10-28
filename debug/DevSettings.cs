using Godot;

namespace RentDay;

// Switches for testing. Flip them in the admin panel (F1 in a run, AdminPanel), or click
// debug/dev_settings.tres in the FileSystem dock and change them in the Inspector before pressing F5.
// They only work in runs from the editor (debug builds); an exported game always plays normally.
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
	[Export] public bool GodMode;                 // you can't die
	[Export] public bool InfiniteSlowMotion;      // the slow-motion meter never runs dry
	[Export] public int StartFloor = 1;           // start on this floor (1 = the first, as normal)

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
		if (GodMode) on.Add("GOD MODE");
		if (InfiniteSlowMotion) on.Add("INF SLOWMO");
		if (StartFloor > 1) on.Add("FLOOR " + StartFloor);
		if (ShowGoonInfo) on.Add("GOON INFO");
		if (RedGoo) on.Add("RED GOO");
		return on.Count > 0 ? "DEV: " + string.Join("  ", on) : "";
	}

	// Every switch back to normal (the admin panel's Reset): each exported field as a new one has it.
	public void Reset()
	{
		var normal = new DevSettings();
		foreach (System.Reflection.FieldInfo field in typeof(DevSettings).GetFields())
			if (field.IsDefined(typeof(ExportAttribute), false))
				field.SetValue(this, field.GetValue(normal));
	}

	// Keeps the admin panel's changes for the next run too (the file stays local: git ignores it).
	public void Save()
	{
		if (ResourcePath == FilePath)
			ResourceSaver.Save(this);
	}
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace RentDay;

// The game's checks, run by test.sh (which also fails on any ERROR: line, as Godot itself often exits 0 after one):
// every file loads, the data follows the rules the code relies on, and the menu and every floor run for a while.
// Touches nothing on disk: dev settings change in memory only, and no save is ever active here.
public partial class TestRunner : SceneTree
{
	private static readonly string[] NotGame = { "addons", "graphify-out", "research_notes", "reports", "tests", "tools" };
	private readonly List<string> _files = new();
	private int _failed;

	public override async void _Initialize()
	{
		try
		{
			await RunAll();
		}
		catch (Exception e) // or nothing would ever quit
		{
			_failed++;
			GD.PrintErr($"FAIL the tests themselves stopped: {e}");
		}
		GD.Print(_failed == 0 ? "all passed" : $"{_failed} failed");
		Quit(_failed == 0 ? 0 : 1);
	}

	private async Task RunAll()
	{
		DevSettings.Current.Reset(); // a normal game, whatever the local switches say
		GD.Seed(1);
		Walk("res://");

		Check("every scene and resource loads", LoadProblems());
		Check("animation frames lie inside their pictures", FrameProblems());
		Check("characters have every animation the code plays", AnimationProblems());
		Check("weapons have their pictures and sound", WeaponProblems());
		Check("floors are set up right", FloorProblems());
		Check("every shader is in the warm-up list", ShaderProblems());
		Check("the export leaves out what players shouldn't get", ExportProblems());

		await Run("the menu runs", Menu.ScenePath, null);
		var floors = GD.Load<PackedScene>("res://main.tscn").Instantiate<Main>();
		foreach (PackedScene floor in floors.Floors)
			await Run(floor?.ResourcePath + " runs", "res://main.tscn", floor);
		floors.Free();
		await DashDodges();
	}

	private async Task Frames(int n)
	{
		for (int i = 0; i < n; i++)
			await ToSignal(this, SignalName.PhysicsFrame);
	}

	// The player alone in an empty world: a bullet flies on through him mid-dash, kills him standing, and a
	// second dash right after the first doesn't happen.
	private async Task DashDodges()
	{
		var world = new Node2D(); // afterimages go into the current scene
		Root.AddChild(world);
		CurrentScene = world;
		var player = GD.Load<PackedScene>("res://player/player.tscn").Instantiate<Player>();
		world.AddChild(player);
		await Frames(2);
		var problems = new List<string>();
		await Press("dash");
		if (!player.Dodging)
			problems.Add("the dash key didn't dash");
		Vector2 ahead = player.Velocity.Normalized();
		Bullet bullet = Shoot(player, -ahead); // from behind: it catches him up
		await Frames(12);
		if (player.IsDead || !IsInstanceValid(bullet) || (bullet.GlobalPosition - player.GlobalPosition).Dot(ahead) <= 0f) // not past him
			problems.Add("a bullet stopped at the dashing player");
		await Frames(30);
		await Press("dash");
		if (player.Dodging)
			problems.Add("dashed again before it recharged");
		Shoot(player, Vector2.Right);
		await Frames(6);
		if (!player.IsDead)
			problems.Add("a bullet didn't kill the player standing");
		Check("a bullet flies through a dash, kills you standing, and the dash recharges", problems);
		world.Free();
	}

	private async Task Press(string action)
	{
		Input.ActionPress(action);
		await Frames(2); // a frame's signal comes before the player's own update in it
		Input.ActionRelease(action);
	}

	// From 30 px off to one side of him, straight at him.
	private Bullet Shoot(Player at, Vector2 side)
	{
		var bullet = GD.Load<PackedScene>("res://weapons/bullet.tscn").Instantiate<Bullet>();
		bullet.CollisionMask = Layers.Player;
		Root.AddChild(bullet);
		bullet.GlobalPosition = at.GlobalPosition + side * 30f;
		bullet.Rotation = (-side).Angle();
		return bullet;
	}

	private void Check(string what, IEnumerable<string> problems)
	{
		string[] found = problems.ToArray();
		_failed += found.Length > 0 ? 1 : 0;
		if (found.Length == 0)
			GD.Print($"ok   {what}");
		else
			GD.PrintErr($"FAIL {what}:\n  " + string.Join("\n  ", found));
	}

	// One floor at a time (the rest of Main as in the game), or any other scene, playing untouched for 10 game seconds.
	private async Task Run(string what, string scene, PackedScene floor)
	{
		var node = GD.Load<PackedScene>(scene).Instantiate();
		if (node is Main main)
			main.Floors = new[] { floor };
		Root.AddChild(node);
		CurrentScene = node;
		for (int i = 0; i < 600; i++)
			await ToSignal(this, SignalName.PhysicsFrame);
		var problems = new List<string>();
		if (node is Main && (GetFirstNodeInGroup("player") is not Player player || !player.GlobalPosition.IsFinite()))
			problems.Add("the player is gone or nowhere");
		Check(what, problems);
		node.Free();
		await ToSignal(this, SignalName.PhysicsFrame);
		Engine.TimeScale = 1;
	}

	private void Walk(string folder)
	{
		foreach (string file in DirAccess.GetFilesAt(folder))
			if (file.EndsWith(".tscn") || file.EndsWith(".tres"))
				_files.Add(folder.PathJoin(file));
		foreach (string sub in DirAccess.GetDirectoriesAt(folder))
			if (!NotGame.Contains(sub))
				Walk(folder.PathJoin(sub));
	}

	private IEnumerable<string> LoadProblems()
	{
		foreach (string file in _files)
		{
			foreach (string dependency in ResourceLoader.GetDependencies(file))
			{
				string path = dependency.Split("::").Last(); // uid://...::::res://path
				if (!ResourceLoader.Exists(path))
					yield return $"{file} needs {path}, which is missing";
			}
			if (ResourceLoader.Load(file) == null)
				yield return $"{file} doesn't load";
		}
	}

	private IEnumerable<SpriteFrames> Looks() => _files.Where(f => f.StartsWith("res://art/anim/")).Select(f => ResourceLoader.Load(f)).OfType<SpriteFrames>();

	private IEnumerable<string> FrameProblems()
	{
		foreach (SpriteFrames look in Looks())
			foreach (StringName name in look.GetAnimationNames())
				for (int i = 0; i < look.GetFrameCount(name); i++)
					if (look.GetFrameTexture(name, i) is AtlasTexture frame && !new Rect2(Vector2.Zero, frame.Atlas.GetSize()).Encloses(frame.Region))
						yield return $"{look.ResourcePath} {name} frame {i} is outside its picture (not re-imported?)";
	}

	private IEnumerable<WeaponData> Weapons() => _files.Where(f => f.StartsWith("res://weapons/")).Select(f => ResourceLoader.Load(f)).OfType<WeaponData>();

	private IEnumerable<string> AnimationProblems()
	{
		string[] kinds = Weapons().Where(w => w.Melee && w.Name != "Fists").Select(w => w.Name.ToLowerInvariant() + "_").Append("gun_").Append("").ToArray();
		var everyone = new List<string> { "legs", "punch", "pickup", "throw", "push_l", "push_r", "lie_fall", "lie_dead", "lie_dead_lean", "lie_slump", "lie_splat", "lie_getup", "lie_slump_getup" };
		everyone.AddRange(kinds.SelectMany(k => new[] { k + "idle", k + "walk" }));
		everyone.AddRange(kinds.Where(k => k != "" && k != "gun_").Select(k => k + "swing"));
		var player = new List<string> { "choke", "fin_knife", "fin_bat", "fin_fists", "kick", "shield_grab", "snap", "shove" };
		player.AddRange(kinds.Select(k => k + "shield"));
		player.AddRange(kinds.SelectMany(k => "fblr".Select(way => k + "dash_" + way)));
		var enemy = new List<string> { "held" };
		enemy.AddRange(kinds.Select(k => k + "look"));
		enemy.AddRange(Enum.GetNames<Enemy.Idle>().Where(n => n != nameof(Enemy.Idle.None)).Select(n => n.ToLowerInvariant()));

		foreach (SpriteFrames look in Looks())
			foreach (string name in everyone.Concat(look.ResourcePath.EndsWith("player.tres") ? player : enemy))
				if (!look.HasAnimation(name))
					yield return $"{look.ResourcePath} has no {name}";
	}

	private IEnumerable<string> WeaponProblems()
	{
		foreach (WeaponData weapon in Weapons().Where(w => w.Name != "Fists"))
			if (weapon.Look == null || weapon.FloorLook == null || weapon.ShotSound == null)
				yield return $"{weapon.ResourcePath} is missing its picture or sound";
	}

	private IEnumerable<string> FloorProblems()
	{
		var main = GD.Load<PackedScene>("res://main.tscn").Instantiate();
		if (main is not Main { Floors.Length: > 0 } game)
		{
			yield return "main.tscn isn't Main, or has no floors";
			main.Free();
			yield break;
		}
		foreach (PackedScene scene in game.Floors)
		{
			if (scene == null)
			{
				yield return "main.tscn has an empty floor";
				continue;
			}
			var level = scene.Instantiate<Node2D>();
			string name = scene.ResourcePath;
			var nodes = level.FindChildren("*", "", true, false).ToArray();
			if (level.Position != Vector2.Zero)
				yield return $"{name} was moved to {level.Position}";
			if (nodes.Count(n => n is Player) != 1)
				yield return $"{name} needs exactly one player";
			if (level.FindChild("Floor", true, false) is not TileMapLayer)
				yield return $"{name} has no Floor";
			if (!nodes.Any(n => n is Enemy))
				yield return $"{name} has no enemies";
			foreach (Enemy enemy in nodes.OfType<Enemy>())
			{
				if (enemy.Look == null)
					yield return $"{name} {enemy.Name} has no look";
				if (enemy.Rotation != 0f)
					yield return $"{name} {enemy.Name} is rotated (FacingDegrees turns him)";
			}
			level.Free();
		}
		main.Free();
	}

	private static IEnumerable<string> Shaders(string folder)
	{
		foreach (string file in DirAccess.GetFilesAt(folder).Where(f => f.EndsWith(".gdshader")))
			yield return folder.PathJoin(file);
		foreach (string sub in DirAccess.GetDirectoriesAt(folder).Where(s => !NotGame.Contains(s)))
			foreach (string shader in Shaders(folder.PathJoin(sub)))
				yield return shader;
	}

	private static IEnumerable<string> ShaderProblems() => Shaders("res://").Except(Menu.Shaders).Select(s => $"{s} isn't in Menu's warm-up list");

	private static IEnumerable<string> ExportProblems()
	{
		string presets = FileAccess.GetFileAsString("res://export_presets.cfg");
		return new[] { "addons/godot_ai/*", "graphify-out/*", "tests/*" }.Where(f => !presets.Contains(f)).Select(f => $"exclude_filter doesn't name {f}");
	}
}

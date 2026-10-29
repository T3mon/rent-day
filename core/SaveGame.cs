using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace RentDay;

// Saved games: one JSON file per playthrough, in Documents/My Games/Rent Day/Saves on a PC (the game's
// own storage on other systems), as many as you like. A playthrough has a name (made up for it, yours to
// change before it starts) and is saved at every floor start: the floor and the gun you came onto it
// with, so loading it starts that floor over. Plain JSON, never a Godot resource: a save file someone
// shares can't carry code. Each save keeps the one before it as save_N.json.bak, read if it breaks.
public static class SaveGame
{
	public const int Version = 1;      // bump when the format changes; newer saves than this are skipped
	public const int NameLength = 16;  // the longest name a playthrough can have

	// Floor counts from 1, as players say it. Weapon is the gun's file (res://weapons/...), or null.
	public record Data(int Version, int Floor, string Weapon, int Ammo, DateTime Saved, string Name);

	// A save on disk: Id is its file (save_3), Name the playthrough's.
	public record Entry(string Id, Data Data)
	{
		public string Name => Data.Name ?? Id.Replace('_', ' ').ToUpperInvariant();
	}

	public static string Folder { get; } = OS.HasFeature("pc")
		? Path.Combine(OS.GetSystemDir(OS.SystemDir.Documents), "My Games", "Rent Day", "Saves")
		: ProjectSettings.GlobalizePath("user://saves");

	private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	// Returns false if it couldn't be written (no permission, disk full...).
	public static bool Write(string id, Data data)
	{
		try
		{
			Directory.CreateDirectory(Folder);
			string path = Path.Combine(Folder, id + ".json"), temp = path + ".tmp";
			File.WriteAllText(temp, JsonSerializer.Serialize(data, Json)); // a crash while writing leaves the old save whole
			if (ReadFile(path) != null)
				File.Replace(temp, path, path + ".bak"); // in one step; the old save stays as the backup
			else
				File.Move(temp, path, overwrite: true); // nothing good to keep: the backup stays as it was
			return true;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			GD.PushWarning($"Could not save {id}: {e.Message}");
			return false;
		}
	}

	// The save, or the one before it if it's broken; null when neither can be read.
	public static Data Read(string id)
	{
		string path = Path.Combine(Folder, id + ".json");
		return ReadFile(path) ?? ReadFile(path + ".bak");
	}

	// null when the file is missing, broken, or from a newer version of the game.
	private static Data ReadFile(string path)
	{
		try
		{
			var data = JsonSerializer.Deserialize<Data>(File.ReadAllText(path), Json);
			return data is { Version: > 0 and <= Version, Floor: > 0 } ? data : null;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
		{
			return null;
		}
	}

	// Every readable save, newest first.
	public static List<Entry> All() => !Directory.Exists(Folder) ? new()
		: Directory.GetFiles(Folder, "*.json")
			.Select(file => Path.GetFileNameWithoutExtension(file))
			.Select(id => new Entry(id, Read(id)))
			.Where(entry => entry.Data != null)
			.OrderByDescending(entry => entry.Data.Saved)
			.ToList();

	// The file for a new playthrough: save_1, save_2... (never one a file, or a backup, already has).
	public static string NewId()
	{
		int last = !Directory.Exists(Folder) ? 0 : Directory.GetFiles(Folder, "save_*")
			.Select(file => int.TryParse(Path.GetFileName(file)[5..].Split('.')[0], out int n) ? n : 0)
			.DefaultIfEmpty(0).Max();
		return "save_" + (last + 1);
	}

	// A playthrough gone for good: its save and its backup.
	public static void Delete(string id)
	{
		try
		{
			string path = Path.Combine(Folder, id + ".json");
			File.Delete(path);
			File.Delete(path + ".bak");
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			GD.PushWarning($"Could not delete {id}: {e.Message}");
		}
	}

	// A made-up name for a new playthrough: two words, not one a save already has, at most NameLength long.
	private static readonly string[] Firsts =
		{ "NEON", "VELVET", "CHROME", "CRIMSON", "COBALT", "PASTEL", "LUCKY", "SILENT", "RUSTY", "GOLDEN",
		  "BRASS", "LATE", "COLD", "WILD", "LONELY", "FROZEN", "HOLLOW", "QUIET", "ROYAL", "STATIC" };
	private static readonly string[] Seconds =
		{ "TIGER", "PALM", "SUNSET", "ARCADE", "CASSETTE", "CHOPPER", "LANDLORD", "TENANT", "VIPER", "FLAMINGO",
		  "JACKAL", "COYOTE", "DOLPHIN", "PANTHER", "MOTEL", "JUKEBOX", "LOBSTER", "RAVEN", "DRIFTER", "COMET" };

	public static string RandomName()
	{
		HashSet<string> taken = All().Select(entry => entry.Name).ToHashSet();
		string name;
		int tries = 0;
		do
			name = Firsts[Random.Shared.Next(Firsts.Length)] + " " + Seconds[Random.Shared.Next(Seconds.Length)];
		while (taken.Contains(name) && ++tries < 50);
		return name;
	}

	// The gun a save names, if it's one of the game's weapons.
	public static WeaponData WeaponOf(Data data) =>
		data.Weapon is { } path && path.StartsWith("res://weapons/") && path.EndsWith(".tres") && ResourceLoader.Exists(path)
			? GD.Load<WeaponData>(path)
			: null;
}

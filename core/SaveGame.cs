using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace RentDay;

// Saved games: one JSON file each, in Documents/My Games/Rent Day/Saves on a PC (the game's own
// storage on other systems). A save is a checkpoint: the floor and the gun you came onto it with,
// so loading one starts that floor over. Plain JSON, never a Godot resource: a save file someone
// shares can't carry code.
//   autosave  - written every time a floor starts
//   quicksave - F5 (F9 loads it)
//   save_N    - "Save game" in the Esc menu, a new one each time
public static class SaveGame
{
	public const int Version = 1;      // bump when the format changes; newer saves than this are skipped
	public const string Auto = "autosave";
	public const string Quick = "quicksave";

	// Floor counts from 1, as players say it. Weapon is the gun's file (res://weapons/...), or null.
	public record Data(int Version, int Floor, string Weapon, int Ammo, DateTime Saved);
	public record Entry(string Name, Data Data);

	public static string Folder { get; } = OS.HasFeature("pc")
		? Path.Combine(OS.GetSystemDir(OS.SystemDir.Documents), "My Games", "Rent Day", "Saves")
		: ProjectSettings.GlobalizePath("user://saves");

	private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

	// Returns false if it couldn't be written (no permission, disk full...).
	public static bool Write(string name, Data data)
	{
		try
		{
			Directory.CreateDirectory(Folder);
			string path = Path.Combine(Folder, name + ".json"), temp = path + ".tmp";
			File.WriteAllText(temp, JsonSerializer.Serialize(data, Json));
			File.Move(temp, path, overwrite: true); // a crash while writing leaves the old save whole
			return true;
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			GD.PushWarning($"Could not save {name}: {e.Message}");
			return false;
		}
	}

	// null when the save is missing, broken, or from a newer version of the game.
	public static Data Read(string name)
	{
		try
		{
			var data = JsonSerializer.Deserialize<Data>(File.ReadAllText(Path.Combine(Folder, name + ".json")), Json);
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
			.Select(name => new Entry(name, Read(name)))
			.Where(entry => entry.Data != null)
			.OrderByDescending(entry => entry.Data.Saved)
			.ToList();

	// The name for a new "Save game": save_1, save_2...
	public static string NextSlot()
	{
		int last = All().Select(entry => entry.Name.StartsWith("save_") && int.TryParse(entry.Name[5..], out int n) ? n : 0).DefaultIfEmpty(0).Max();
		return "save_" + (last + 1);
	}

	// "autosave" -> "AUTOSAVE", "save_3" -> "SAVE 3".
	public static string Title(string name) => name == Quick ? "QUICK SAVE" : name.Replace('_', ' ').ToUpperInvariant();

	// The gun a save names, if it's one of the game's weapons.
	public static WeaponData WeaponOf(Data data) =>
		data.Weapon is { } path && path.StartsWith("res://weapons/") && path.EndsWith(".tres") && ResourceLoader.Exists(path)
			? GD.Load<WeaponData>(path)
			: null;
}

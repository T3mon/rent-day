using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;

namespace RentDay;

// The player's settings: the Settings page, the music page's volume, shuffle and loop, and keys moved off
// their defaults. Kept in settings.json next to the Saves folder. This is an autoload, so it reads them
// when the game starts, before any scene; a change applies at once and is saved. Sound effects play on
// the Effects bus and music on the Music bus, both under Master: one volume each.
public partial class Settings : Node
{
	// The Settings page's values, by its row keys: toggles 0 or 1, volumes and bars 0 to 10, a choice its index.
	public static readonly Dictionary<string, int> Values = new()
	{
		["screen"] = 0, ["resolution"] = 3, ["vsync"] = 1, // screen: Windowed, FullWindow, Fullscreen; resolution: in Sizes (3 = 1600 x 900)
		["master"] = 8, ["music"] = 8, ["sfx"] = 8,      // volumes
		["shuffle"] = 0, ["loop"] = 0,                   // the music page
		["shake"] = 10, ["flash"] = 1,                   // accessibility
	};

	public const int Windowed = 0, FullWindow = 1, Fullscreen = 2; // the "screen" setting: a window, a borderless window covering the screen, or exclusive fullscreen
	public static float ShakeScale => Values["shake"] / 10f;
	public static bool Flashes => Values["flash"] == 1;

	// Window sizes to pick from, all 16:9 like the picture, which stretches to fill any of them (and the screen,
	// covering it). The menu offers the ones that fit the screen, and always the smallest.
	private static readonly Vector2I[] Sizes = [new(1024, 576), new(1280, 720), new(1366, 768), new(1600, 900), new(1920, 1080), new(2560, 1440), new(3200, 1800), new(3840, 2160)];
	public static readonly string[] Resolutions = Sizes.Where((size, i) => i == 0 || Fits(size)).Select(Label).ToArray();

	private static string Label(Vector2I size) => $"{size.X} X {size.Y}";

	// Why a setting can't change right now (said when you try), or null when it can.
	public static string Locked(string key) => key switch
	{
		"screen" or "resolution" when Engine.IsEmbeddedInEditor() => "THE EDITOR'S GAME TAB SIZES THE GAME",
		"resolution" when Values["screen"] != Windowed => "FULLSCREEN USES THE SCREEN'S OWN SIZE",
		_ => null,
	};

	// What the page shows for a setting instead of its own value, or null: covering the screen, its size.
	public static string Shown(string key) =>
		key == "resolution" && Values["screen"] != Windowed ? Label(DisplayServer.ScreenGetSize(DisplayServer.WindowGetCurrentScreen())) : null;

	// Keys moved off their defaults: action -> its keys and mouse buttons, main first (see Write).
	private static readonly Dictionary<string, string[]> Keys = new();

	// settings.json. MusicVolume, Shuffle and Loop are how the music page saved them before: read, never written.
	private record Saved(Dictionary<string, int> Values, Dictionary<string, string[]> Keys, int? MusicVolume = null, bool? Shuffle = null, bool? Loop = null);
	private static readonly JsonSerializerOptions Json = new() { WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
	private static string FilePath => Path.Combine(Path.GetDirectoryName(SaveGame.Folder), "settings.json");
	private static readonly StringName Master = "Master", Effects = "Effects";

	public override void _Ready()
	{
		Load();
		foreach (string key in new[] { "screen", "vsync", "master", "music", "sfx" })
			Apply(key);
		// Every sound effect (a 2D sound left on Master) plays on Effects, whoever made it.
		GetTree().NodeAdded += node =>
		{
			if (node is AudioStreamPlayer2D sound && sound.Bus == Master)
				sound.Bus = Effects;
		};
	}

	// Covering the screen, the game goes quiet while another window has the focus; in a window it plays on.
	public override void _Notification(int what)
	{
		if (what is (int)NotificationApplicationFocusOut or (int)NotificationApplicationFocusIn)
			AudioServer.SetBusMute(0, what == NotificationApplicationFocusOut && Values["screen"] != Windowed);
	}

	public static void Set(string key, int value)
	{
		if (Values[key] == value)
			return; // a drag along a bar sends many of the same
		Values[key] = value;
		Apply(key);
		Save();
	}

	// An action's keys and mouse buttons, main first (its gamepad buttons aren't changed here).
	public static List<InputEvent> KeysOf(string action) =>
		InputMap.HasAction(action) ? InputMap.ActionGetEvents(action).Where(IsKey).ToList() : new();

	public static bool Same(InputEvent a, InputEvent b) => a.IsMatch(b, false);

	// Puts `key` in `action`'s main (slot 0) or second (1) key box, taking it off `from` first, if another action had it.
	public static void Bind(string action, int slot, InputEvent key, string from = null)
	{
		if (from != null)
			ChangeKeys(from, KeysOf(from).Where(k => !Same(k, key)).ToList());
		List<InputEvent> keys = KeysOf(action).Where(k => !Same(k, key)).ToList();
		if (slot < keys.Count)
			keys[slot] = key;
		else
			keys.Add(key);
		ChangeKeys(action, keys);
		Save();
	}

	public static void ResetKeys()
	{
		InputMap.LoadFromProjectSettings();
		Keys.Clear();
		Save();
	}

	private static void Apply(string key)
	{
		switch (key)
		{
			case "screen" or "resolution" when !Engine.IsEmbeddedInEditor(): // the editor's Game tab sizes the game itself
				DisplayServer.WindowSetMode(Values["screen"] switch
				{
					FullWindow => DisplayServer.WindowMode.Fullscreen, // Godot's "fullscreen" is the borderless kind
					Fullscreen => DisplayServer.WindowMode.ExclusiveFullscreen,
					_ => DisplayServer.WindowMode.Windowed,
				});
				// Covering the screen, the mouse stays in it: a click at the edge mid-fight never lands on another window.
				Input.MouseMode = Values["screen"] == Windowed ? Input.MouseModeEnum.Visible : Input.MouseModeEnum.Confined;
				if (Values["screen"] == Windowed)
					SizeWindow();
				break;
			case "vsync":
				DisplayServer.WindowSetVsyncMode(Values["vsync"] == 1 ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
				break;
			case "master" or "music" or "sfx":
				AudioServer.SetBusVolumeDb(Bus(key switch { "master" => "Master", "music" => "Music", _ => "Effects" }),
					Values[key] == 0 ? -80f : Mathf.LinearToDb(Values[key] / 10f));
				break;
		}
	}

	// The chosen size (the biggest that fits, if the screen got smaller), in the middle of the screen.
	private static void SizeWindow()
	{
		Vector2I size = Sizes[Math.Min(Values["resolution"], Resolutions.Length - 1)];
		if (DisplayServer.WindowGetSize() == size)
			return;
		Rect2I screen = DisplayServer.ScreenGetUsableRect(DisplayServer.WindowGetCurrentScreen());
		DisplayServer.WindowSetSize(size);
		DisplayServer.WindowSetPosition(screen.Position + (screen.Size - size) / 2);
	}

	private static bool Fits(Vector2I size)
	{
		Vector2I screen = DisplayServer.ScreenGetUsableRect(DisplayServer.WindowGetCurrentScreen()).Size;
		return size.X <= screen.X && size.Y + 40 <= screen.Y; // 40: the window's title bar
	}

	// The Music and Effects buses are made here, if the project's bus layout doesn't have them.
	private static int Bus(string name)
	{
		int bus = AudioServer.GetBusIndex(name);
		if (bus >= 0)
			return bus;
		bus = AudioServer.BusCount;
		AudioServer.AddBus();
		AudioServer.SetBusName(bus, name);
		AudioServer.SetBusSend(bus, Master);
		return bus;
	}

	private static bool IsKey(InputEvent e) => e is InputEventKey or InputEventMouseButton;

	private static void ChangeKeys(string action, IReadOnlyCollection<InputEvent> keys)
	{
		SetKeys(action, keys);
		Keys[action] = keys.Select(Write).ToArray();
	}

	private static void SetKeys(string action, IEnumerable<InputEvent> keys)
	{
		foreach (InputEvent old in KeysOf(action))
			InputMap.ActionEraseEvent(action, old);
		foreach (InputEvent key in keys)
			InputMap.ActionAddEvent(action, key);
	}

	// A key or mouse button as text in the file: "key 87" (W), "physical 87", "mouse 1" (left button).
	private static string Write(InputEvent e) => e switch
	{
		InputEventKey { Keycode: not Key.None } key => $"key {(long)key.Keycode}",
		InputEventKey key => $"physical {(long)key.PhysicalKeycode}",
		InputEventMouseButton mouse => $"mouse {(long)mouse.ButtonIndex}",
		_ => null,
	};

	private static InputEvent Read(string text)
	{
		string[] parts = text?.Split(' ') ?? [];
		if (parts.Length != 2 || !long.TryParse(parts[1], out long code))
			return null;
		return parts[0] switch
		{
			"key" => new InputEventKey { Keycode = (Key)code },
			"physical" => new InputEventKey { PhysicalKeycode = (Key)code },
			"mouse" => new InputEventMouseButton { ButtonIndex = (MouseButton)code },
			_ => null,
		};
	}

	private static void Load()
	{
		Saved saved;
		try
		{
			saved = JsonSerializer.Deserialize<Saved>(File.ReadAllText(FilePath));
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
		{
			return; // no settings yet (or a broken file): the defaults
		}
		if (saved == null)
			return;
		foreach (var (key, value) in saved.Values ?? new())
		{
			if (Values.ContainsKey(key))
				Values[key] = Math.Clamp(value, 0, 10);
		}
		if (saved.MusicVolume is int volume)
			Values["music"] = Math.Clamp(volume, 0, 10);
		if (saved.Shuffle is bool shuffle)
			Values["shuffle"] = shuffle ? 1 : 0;
		if (saved.Loop is bool loop)
			Values["loop"] = loop ? 1 : 0;
		foreach (var (action, keys) in saved.Keys ?? new())
		{
			InputEvent[] events = keys?.Select(Read).ToArray();
			if (!InputMap.HasAction(action) || events == null || events.Contains(null))
				continue; // an action that's gone, or a key it can't read: that action keeps its defaults
			SetKeys(action, events);
			Keys[action] = keys;
		}
	}

	private static void Save()
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
			string temp = FilePath + ".tmp";
			File.WriteAllText(temp, JsonSerializer.Serialize(new Saved(Values, Keys), Json));
			File.Move(temp, FilePath, true); // whole or not at all
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			GD.PushWarning($"Could not save the settings: {e.Message}");
		}
	}
}

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Godot;

namespace RentDay;

// The game's music: one player that outlives scene changes (it hangs off the tree's root, not a
// level). The main menu loops menu.mp3. A floor plays one of the floor songs, which keeps going
// through deaths and restarts; a new floor starts the next song (a random one with shuffle, the
// same one from the start with loop). Clearing a floor cuts to win.mp3. N skips a floor song; the
// Esc menu's MUSIC page has the rest. Volume, shuffle and loop are remembered in settings.json.
public static class Music
{
	// Floor songs: every mp3 in this folder, in name order. A new song is just a new file there.
	private const string FloorFolder = "res://audio/music/floor";
	public static readonly string[] Songs = ResourceLoader.ListDirectory(FloorFolder).Where(f => f.EndsWith(".mp3")).Order().Select(f => f[..^4]).ToArray();
	private const string MenuSong = "res://audio/music/menu.mp3", ClearSong = "res://audio/music/win.mp3";

	public static int Volume { get; private set; } = 8;  // 0..10
	public static bool Shuffle { get; private set; }
	public static bool Loop { get; private set; }
	public static int Current { get; private set; } = -1; // the floor song (index into Songs); -1 before the first floor
	public static bool OnFloorSong => _player != null && Current >= 0 && _floor >= 0 && !_cleared;
	public static bool Paused => _player != null && _player.StreamPaused;
	public static string Title => Current < 0 ? "" : Songs[Current].Replace('_', ' ').ToUpperInvariant();
	public static int Started { get; private set; }      // counts floor songs started, so the game can show a note
	public static float Position => _player != null && _player.IsInsideTree() ? _player.GetPlaybackPosition() : 0f;
	public static float Length => (float)(_player?.Stream?.GetLength() ?? 0);

	// The floor songs and the clear song are read in the background when the player is made, then kept in
	// memory (about 3 MB each): the first read of a song off a slow drive (an SD card) stalled the game.
	// shortcut: every song held at once; stream them from disk if the list grows past a few dozen.
	private static readonly AudioStream[] Loaded = new AudioStream[Songs.Length];
	private static AudioStream _clearSong;

	private static AudioStreamPlayer _player;
	private static int _floor = -1; // the floor the song belongs to; -1 = the menu
	private static bool _cleared;

	private record Prefs(int MusicVolume, bool Shuffle, bool Loop);
	private static string PrefsFile => Path.Combine(Path.GetDirectoryName(SaveGame.Folder), "settings.json");

	public static void Menu(Node from)
	{
		Ready(from);
		_floor = -1;
		if (_player.Stream?.ResourcePath != MenuSong)
			Start(Looping(GD.Load<AudioStreamMP3>(MenuSong)));
	}

	// A floor (0 = the first) is starting: keep the song through a death or restart, else the next one.
	public static void Floor(Node from, int floor)
	{
		Ready(from);
		if (Songs.Length == 0 || floor == _floor && !_cleared)
			return;
		if (floor != _floor)
			Current = Current < 0 ? (Shuffle ? (int)(GD.Randi() % Songs.Length) : 0) : Loop ? Current : Next(1);
		_floor = floor;
		_cleared = false;
		StartSong();
	}

	// Picked from the list: plays now (over the clear song too); the next floor goes on from it.
	public static void PlaySong(int song)
	{
		if (_player == null || _floor < 0 || song < 0 || song >= Songs.Length)
			return;
		Current = song;
		_cleared = false;
		StartSong();
	}

	public static void Clear()
	{
		if (_player == null || _cleared)
			return;
		_cleared = true;
		Start(_clearSong ??= Looping((AudioStreamMP3)Take(ClearSong)));
	}

	public static void Skip(int dir)
	{
		if (!OnFloorSong)
			return;
		Current = Next(dir);
		StartSong();
	}

	public static void TogglePause()
	{
		if (_player != null)
			_player.StreamPaused = !_player.StreamPaused;
	}

	public static void SetVolume(int volume)
	{
		volume = Math.Clamp(volume, 0, 10);
		if (volume == Volume)
			return; // a drag sends many of the same
		Volume = volume;
		Apply();
		Save();
	}
	public static void SetShuffle(bool on) { Shuffle = on; Save(); }
	public static void SetLoop(bool on) { Loop = on; Save(); }

	private static int Next(int dir)
	{
		if (Shuffle && dir > 0 && Songs.Length > 1)
			return (Current + 1 + (int)(GD.Randi() % (Songs.Length - 1))) % Songs.Length; // any other song
		return (Current + dir + Songs.Length) % Songs.Length;
	}

	private static void StartSong()
	{
		Start(Loaded[Current] ??= (AudioStream)Take(SongPath(Current)));
		Started++;
	}

	private static void Start(AudioStream stream)
	{
		_player.Stream = stream;
		_player.StreamPaused = false;
		if (_player.IsInsideTree())
			_player.Play();
		else
			_player.CallDeferred(AudioStreamPlayer.MethodName.Play);
	}

	private static string SongPath(int song) => $"{FloorFolder}/{Songs[song]}.mp3";

	// A song read in the background, or read now if it isn't in yet (a floor right at start-up). Never waits
	// on LoadThreadedGet: that runs deferred calls early, before the player is added to the tree.
	private static Resource Take(string path) =>
		ResourceLoader.LoadThreadedGetStatus(path) == ResourceLoader.ThreadLoadStatus.Loaded ? ResourceLoader.LoadThreadedGet(path) : GD.Load(path);

	private static AudioStream Looping(AudioStreamMP3 stream)
	{
		stream.Loop = true;
		return stream;
	}

	// The player is made once, on the first call, and added to the root (deferred: the root may be busy
	// adding the scene that called). Settings are read then too.
	private static void Ready(Node from)
	{
		if (_player != null && GodotObject.IsInstanceValid(_player))
			return;
		_player = new AudioStreamPlayer { Name = "Music", ProcessMode = Node.ProcessModeEnum.Always };
		_player.Finished += () =>
		{
			if (OnFloorSong) // a floor song ended: the same one again with loop, else the next
			{
				if (!Loop)
					Current = Next(1);
				StartSong();
			}
		};
		from.GetTree().Root.CallDeferred(Node.MethodName.AddChild, _player);
		for (int i = 0; i < Songs.Length; i++)
			ResourceLoader.LoadThreadedRequest(SongPath(i));
		ResourceLoader.LoadThreadedRequest(ClearSong);
		if (!InputMap.HasAction("next_song")) // shortcut: added here, not in Project Settings; move it there when keys get rebindable (#8)
		{
			InputMap.AddAction("next_song");
			InputMap.ActionAddEvent("next_song", new InputEventKey { PhysicalKeycode = Key.N });
		}
		try
		{
			var prefs = JsonSerializer.Deserialize<Prefs>(File.ReadAllText(PrefsFile));
			(Volume, Shuffle, Loop) = (Math.Clamp(prefs.MusicVolume, 0, 10), prefs.Shuffle, prefs.Loop);
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or NullReferenceException)
		{
			// no settings yet (or a broken file): the defaults
		}
		Apply();
	}

	private static void Apply() => _player.VolumeDb = Volume == 0 ? -80f : Mathf.LinearToDb(Volume / 10f);

	private static void Save()
	{
		try
		{
			Directory.CreateDirectory(Path.GetDirectoryName(PrefsFile));
			File.WriteAllText(PrefsFile, JsonSerializer.Serialize(new Prefs(Volume, Shuffle, Loop)));
		}
		catch (Exception e) when (e is IOException or UnauthorizedAccessException)
		{
			GD.PushWarning($"Could not save the music settings: {e.Message}");
		}
	}
}

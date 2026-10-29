using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using static RentDay.Style;

namespace RentDay;

// The loading screen: the menu's other job, drawn the same way. It covers whatever is on (the game
// stays paused under it) and reads the scene, everything in it (every floor) and all the music, a few
// pieces a frame so it keeps moving, then switches to the scene. A quick load never shows it; once up,
// it stays a moment so it never just flashes. The scene stays in memory until the next one, so the
// stairs and restarts never touch the disk.
// Not Godot's background loading (LoadThreadedRequest): in 4.7 it crashed the game handing over a
// scene with C# in it.
public partial class Menu
{
	private const string ArtFolder = "res://ui/loading"; // its pictures: one at random each time, best at 400 x 225
	private const ulong ShowAfter = 250, ShowAtLeast = 600, FrameBudget = 12, WarmTime = 100; // milliseconds
	private static PackedScene _held;
	private static bool _warm; // the effects' shaders are built (once a run)
	private ulong _warmUntil;
	private string _loading; // the scene being read; null on a menu
	private ulong _shownAt;  // when the loading screen came up; 0 = not yet
	private readonly Stack<(string Path, bool Opened)> _todo = new(); // Opened: what it needs is queued above it
	private readonly HashSet<string> _seen = new();
	private readonly List<Resource> _read = new(); // held until the scene is in
	private int _done, _total;
	private float _progress;
	private Texture2D _art;

	// Up as the loading screen: one of its pictures, at random.
	private void ReadyLoading()
	{
		_view = View.Loading;
		string[] art = ResourceLoader.ListDirectory(ArtFolder).Where(f => f.EndsWith(".png")).ToArray();
		_art = art.Length > 0 ? GD.Load<Texture2D>($"{ArtFolder}/{art[GD.Randi() % art.Length]}") : null;
	}

	public static void LoadScene(SceneTree tree, string path)
	{
		var screen = new Menu { _loading = path, _total = 1 + Music.Unread };
		screen._todo.Push((path, false));
		screen._seen.Add(path);
		screen.SetAnchorsPreset(LayoutPreset.FullRect);
		var layer = new CanvasLayer { Layer = 100 }; // over the HUD and the Esc menu
		layer.AddChild(screen);
		tree.Root.AddChild(layer);
		tree.Paused = true; // nothing moves under it
	}

	private void PollLoading()
	{
		ulong now = Time.GetTicksMsec();
		if (_shownAt == 0 && now - _opened >= ShowAfter)
			_shownAt = now;
		bool done = ReadSome(now + FrameBudget);
		_progress = Math.Max(_progress, (float)_done / _total);
		if (done && !_warm)
			WarmUp(now);
		if (!done || now < _warmUntil || _shownAt > 0 && Time.GetTicksMsec() - _shownAt < ShowAtLeast)
			return;
		SceneTree tree = GetTree();
		tree.Paused = false;
		_held = GD.Load<PackedScene>(_loading); // already in memory
		if (_held != null)
			tree.ChangeSceneToPacked(_held);
		else
			tree.ChangeSceneToFile(_loading); // the read failed: the plain load says why
		_loading = null;
		GetParent().QueueFree();
	}

	// Reads pieces until the time is up (at least one); true once the scene and all the music are in.
	private bool ReadSome(ulong until)
	{
		do
		{
			if (_todo.Count > 0)
				ReadNext();
			else if (!Music.ReadNext())
				return true;
			_done++;
		} while (Time.GetTicksMsec() < until);
		return false;
	}

	// One piece of the scene: what it needs goes on top of it first, so by the time it is read, that is
	// all in memory and reading it is quick.
	private void ReadNext()
	{
		var (path, opened) = _todo.Pop();
		if (opened || ResourceLoader.HasCached(path))
		{
			_read.Add(GD.Load(path));
			return;
		}
		_todo.Push((path, true));
		_done--; // not read yet
		foreach (string dependency in ResourceLoader.GetDependencies(path))
		{
			string[] parts = dependency.Split("::"); // uid, type, path
			string need = parts[^1] != "" ? parts[^1] : parts[0];
			if (_seen.Add(need))
			{
				_todo.Push((need, false));
				_total++;
			}
		}
	}

	// Something drawn for the first time mid-play (the goo painting, the Esc menu's wash, smoke and sparks,
	// the stairs' arrow) stalls that frame while the graphics driver builds its shader. So the first load
	// of a run draws each once, a dot under the loading screen: every shader in the project, a particle
	// (drawn many at once, its own build) and a shape drawn in code.
	// Listed by hand: walking the project's folders for them took over half a second. A new shader goes here.
	private static readonly string[] Shaders =
		{ "res://art/goo_canvas.gdshader", "res://art/goo_stamp.gdshader", "res://art/vignette.gdshader", "res://ui/pause.gdshader",
		  "res://effects/slow_motion.gdshader", "res://effects/afterimage.gdshader" };

	private void WarmUp(ulong now)
	{
		foreach (string shader in Shaders)
			Under(new ColorRect { Size = Vector2.One, Material = new ShaderMaterial { Shader = GD.Load<Shader>(shader) } });
		Under(new CpuParticles2D { Amount = 1, Preprocess = 0.1f, Position = Vector2.One });
		var shape = new Control { Size = Vector2.One };
		shape.Draw += () =>
		{
			Vector2[] dot = [Vector2.Zero, Vector2.Right, Vector2.One];
			shape.DrawColoredPolygon(dot, Ink);
			shape.DrawPolyline(dot, Ink, 1f);
		};
		Under(shape);
		_warm = true;
		_warmUntil = now + WarmTime; // a few frames drawn with them
	}

	private void Under(Node node) // with the loading screen's layer, drawn first so the screen covers it
	{
		GetParent().AddChild(node);
		GetParent().MoveChild(node, 0);
	}

	public override void _Input(InputEvent e)
	{
		if (_view == View.Loading)
			GetViewport().SetInputAsHandled(); // nothing under the loading screen reacts
	}

	// The picture, a blinking fast-forward sign, how far the read has got.
	private void DrawLoading(ulong now)
	{
		if (_art != null) // filling the screen, cropped to it (a 400 x 225 picture fits pixel for pixel)
		{
			Vector2 size = _art.GetSize() * Math.Max(W / _art.GetSize().X, H / _art.GetSize().Y);
			DrawTextureRect(_art, new Rect2((new Vector2(W, H) - size) / 2, size), false);
		}
		float w = Osd("FF", 16, 26, Colors.White);
		if (now / 300 % 2 == 0 || !Settings.Flashes) // blinking, unless flashes are off
		{
			Tri(16 + w + 7, 15, Colors.White);
			Tri(16 + w + 13, 15, Colors.White);
		}
		Osd("LOADING", 16, H - 12, Colors.White);
		Progress(W - 96, H - 17, 80, _progress); // how far the read has got
	}
}

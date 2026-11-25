using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RentDay;

// R, played back. The floor still starts over at once (Main) and you can move straight away; over the fresh floor,
// everyone who moved on the try before races home the way they went, from where they were when you pressed R to
// where they stand now. They are see-through ghosts in the display's pink and cyan (Afterimage's silhouettes), under
// the living and in nobody's way, that slow as they arrive and melt into the real ones. Meanwhile the death screen's
// tape display rewinds: REWIND stays up with its key pressed, a tracking band races up (flashes only) and the counter
// spins back to 0:00:00; then PLAY, as on the Esc menu, and it's gone. Its beats are real time, as on the death screen.
// What it plays back is the tape: where everyone has been on the floor, a point every few pixels they move.
public partial class Rewind : Node2D
{
	private const float Home = 350f;  // ms: the ghosts' way home, and the counter's spin back
	private const float Race = 300f;  // ms: the band's race up the screen
	private const float Gone = 1350f; // ms: PLAY goes
	private const float Streak = 60f; // ms: behind each ghost, the way it came in that long
	private const float Step = 4f;    // px: a new point on the tape every this far someone moves
	private const float Least = 12f;  // px: who moved less gets no ghost
	private const int H = 225;

	// The tape of this floor: everyone, and where they've been since it started.
	private static readonly List<(Node2D Who, List<Vector2> Way)> Tape = new();
	private static Dictionary<string, List<Vector2>> _taken; // the try R restarted, by node path, for the fresh floor
	private static ulong _lasted;                             // how long it lasted, ms

	// Way runs from where they were at R to where they started; At is how far along it each point is.
	private record Ghost(Node2D Live, Vector2[] Way, float[] At, Vector2 Stood, Node2D Pink, Node2D Cyan);

	private readonly List<Ghost> _ghosts = new();
	private Dictionary<string, List<Vector2>> _ways;
	private ulong _from; // the counter's start, ms
	private Control _display;
	private CanvasItem _dash, _battery; // the HUD's, in the same corner
	private double _ms;
	private ulong _last;

	// A floor starts (Main): a new tape. If R brought us here, the try before plays back over it.
	public static void Start(Main main)
	{
		Tape.Clear();
		SceneTree tree = main.GetTree();
		foreach (Node2D who in tree.GetNodesInGroup("player").Concat(tree.GetNodesInGroup("enemies")).Cast<Node2D>())
			Tape.Add((who, new List<Vector2> { who.GlobalPosition }));
		if (_taken != null)
			main.AddChild(new Rewind { _ways = _taken, _from = _lasted });
		_taken = null;
	}

	// Every frame (Main): a new point for everyone who has moved on from their last.
	public static void Record()
	{
		foreach (var (who, way) in Tape)
			if (IsInstanceValid(who) && who.GlobalPosition.DistanceSquaredTo(way[^1]) >= Step * Step)
				way.Add(who.GlobalPosition);
	}

	// R (Main), as the floor reloads: the fresh floor plays this tape back.
	public static void Take(ulong lasted)
	{
		_taken = Tape.Where(e => IsInstanceValid(e.Who)).ToDictionary(e => e.Who.GetPath().ToString(), e => e.Way);
		_lasted = lasted;
	}

	public override void _Ready()
	{
		ZIndex = 3; // where a dash's afterimages are: under the living (4), over bodies and furniture
		ZAsRelative = false;
		_last = Time.GetTicksUsec();
		foreach (var (path, taken) in _ways)
		{
			if (GetNodeOrNull<Node2D>(path) is not { } live)
				continue;
			Vector2[] way = Enumerable.Reverse(taken).ToArray();
			var at = new float[way.Length];
			for (int i = 1; i < way.Length; i++)
				at[i] = at[i - 1] + way[i - 1].DistanceTo(way[i]);
			if (at[^1] >= Least)
				_ghosts.Add(new Ghost(live, way, at, live.GlobalPosition, Look(live), Look(live)));
		}
		var layer = new CanvasLayer { Layer = 1 }; // the death screen's: over the floor and the vignette, under the HUD
		_display = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		_display.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_display.Draw += DrawDisplay;
		layer.AddChild(_display);
		AddChild(layer);
		_dash = GetNodeOrNull<CanvasItem>("../HUD/Dash");
		_battery = GetNodeOrNull<CanvasItem>("../HUD/SlowMeter");
	}

	private Node2D Look(Node2D live)
	{
		Node2D look = Afterimage.Silhouette(live);
		look.Modulate = Colors.Transparent; // till its first frame
		AddChild(look);
		return look;
	}

	public override void _Process(double delta)
	{
		ulong now = Time.GetTicksUsec();
		_ms += Mathf.Min((now - _last) / 1000.0, 100.0); // real time, at most 0.1 s a frame, as on the death screen
		_last = now;
		float t = (float)_ms;
		if (t >= Gone)
		{
			QueueFree();
			return;
		}
		float p = DeathScreen.Ease(t / Home), alpha = Fade(t, 0.5f);
		foreach (Ghost g in _ghosts)
		{
			Vector2 at = Along(g, p) - g.Stood;
			g.Pink.Position = at + Vector2.Left;
			g.Cyan.Position = at + Vector2.Right;
			g.Pink.Modulate = Style.Pink with { A = alpha };
			g.Cyan.Modulate = Style.Cyan with { A = alpha };
		}
		QueueRedraw();
		_display.QueueRedraw();
	}

	// Behind each ghost, the way it came in the last moment: a pink and a cyan streak, fading out behind it.
	public override void _Draw()
	{
		float t = (float)_ms;
		float head = DeathScreen.Ease(t / Home), tail = DeathScreen.Ease((t - Streak) / Home), alpha = Fade(t, 0.6f);
		if (alpha <= 0f)
			return;
		foreach (Ghost g in _ghosts)
		{
			float from = tail * g.At[^1], to = head * g.At[^1];
			if (to - from < 1f)
				continue;
			Vector2 drift = Drift(g) * head;
			var line = new List<Vector2> { PointAt(g, from) + drift };
			var along = new List<float> { from };
			for (int i = 0; i < g.Way.Length; i++)
				if (g.At[i] > from && g.At[i] < to)
				{
					line.Add(g.Way[i] + drift);
					along.Add(g.At[i]);
				}
			line.Add(PointAt(g, to) + drift);
			along.Add(to);
			foreach (var (color, side) in new[] { (Style.Pink, Vector2.Left), (Style.Cyan, Vector2.Right) })
				DrawPolylineColors(line.Select(v => v + side).ToArray(), along.Select(a => color with { A = alpha * (a - from) / (to - from) }).ToArray(), 2f);
		}
	}

	// The tape display: the band racing up, the counter spinning back to 0:00:00, REWIND, then PLAY. The top left
	// gives way to the HUD's dash and battery, which come up there.
	private void DrawDisplay()
	{
		float t = (float)_ms;
		if (Settings.Flashes && t < Race)
			Menu.Band(_display, Mathf.RoundToInt(H + 20 - (H + 40) * t / Race), (ulong)t, 2.5f);
		DeathScreen.Counter(_display, Menu.Timecode(t < Home ? (ulong)(_from * (1f - DeathScreen.Ease(t / Home))) : Time.GetTicksMsec() - Main.FloorStarted));
		if (_dash?.Visible == true || _battery?.Visible == true)
			return;
		if (t < Home)
			DeathScreen.Sign(_display, 2, arrows: true, pressed: true);
		else
		{
			float w = DeathScreen.Osd(_display, "PLAY", 16, 26, 16, 1); // the Esc menu's, as it closes
			DeathScreen.Tri(_display, Mathf.Round(16 + w + 7), 15, 1, left: false);
		}
	}

	// The ghosts' see-through-ness: `most` on the way, melting away as they arrive.
	private static float Fade(float t, float most) => most * (1f - Mathf.SmoothStep(0.6f, 1f, t / Home));

	// Where a ghost is `p` of the way home: from where it was at R (0) to where its live one stands now (1).
	private static Vector2 Along(Ghost g, float p) => PointAt(g, p * g.At[^1]) + Drift(g) * p;

	// How far the live one has gone from where it started (you can move at once, and enemies set off).
	private static Vector2 Drift(Ghost g) => IsInstanceValid(g.Live) ? g.Live.GlobalPosition - g.Way[^1] : Vector2.Zero;

	// The point `d` pixels along a ghost's way.
	private static Vector2 PointAt(Ghost g, float d)
	{
		int i = 1;
		while (i < g.Way.Length - 1 && g.At[i] < d)
			i++;
		float length = g.At[i] - g.At[i - 1];
		return g.Way[i - 1].Lerp(g.Way[i], length > 0f ? Mathf.Clamp((d - g.At[i - 1]) / length, 0f, 1f) : 1f);
	}
}

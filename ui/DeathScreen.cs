using Godot;
using static RentDay.Style;

namespace RentDay;

// What you see when you die (Main adds it). The camera punches in on your body and the floor goes neon
// (ui/death.gdshader). Over that, the Esc menu's tape display has stopped: a tracking band rolls down, the time
// you lasted on the floor stays in the corner, and the top left says REWIND, with the restart key. No sound of
// its own. Its beats are in milliseconds of real time after you die (hit stop and slow motion don't stretch
// them), and it never holds you up: R restarts the floor on any frame, as it always has (Main), and the fresh floor
// plays the try back (Rewind).
public partial class DeathScreen : CanvasLayer
{
	public Node2D Body; // you: the zoom and the neon close in on where you lie

	private const int W = 400, H = 225;

	private ShaderMaterial _look;
	private Control _front;
	private string _lasted; // the tape counter, stopped: how long you lasted on the floor
	private double _ms;     // since you died
	private ulong _last;    // the clock at the last frame, in microseconds

	public override void _Ready()
	{
		Layer = 1; // over the floor and the vignette, under the HUD, the Esc menu and the fade
		_last = Time.GetTicksUsec();
		_lasted = Menu.Timecode(Time.GetTicksMsec() - Main.FloorStarted);
		_look = new ShaderMaterial { Shader = GD.Load<Shader>("res://ui/death.gdshader") };
		var look = new ColorRect { Material = _look, MouseFilter = Control.MouseFilterEnum.Ignore };
		look.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(look);
		_front = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
		_front.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		_front.Draw += DrawFront;
		AddChild(_front);
	}

	public override void _Process(double delta)
	{
		// The real clock (hit stop changes the game's speed mid-frame), at most 0.1 s a frame, so a pause under
		// the Esc menu doesn't skip it ahead.
		ulong now = Time.GetTicksUsec();
		_ms += Mathf.Min((now - _last) / 1000.0, 100.0);
		_last = now;
		float t = (float)_ms;
		Vector2 size = GetViewport().GetVisibleRect().Size;
		_look.SetShaderParameter("amount", Ease(t / 160f));
		_look.SetShaderParameter("flash", Settings.Flashes && t < 50f ? 0.7f * (1f - t / 50f) : 0f);
		_look.SetShaderParameter("body", GetViewport().GetCanvasTransform() * Body.GlobalPosition / size);
		_look.SetShaderParameter("size", size);
		_front.QueueRedraw();
	}

	// The Esc menu's display (Menu), stopped: a tracking band rolls down fast, then slowly as on the menu
	// (flashes only); the tape counter; the restart key and REWIND; scanlines over it all.
	private void DrawFront()
	{
		float t = (float)_ms;
		if (Settings.Flashes)
			Menu.Band(_front, t < 320f ? Mathf.RoundToInt((H + 40) * t / 320f) - 20 : (int)(t / 38f) % (H + 40) - 20, (ulong)t, t < 320f ? 2.5f : 1f);
		if (t >= 60f)
			Counter(_front, _lasted);
		if (t >= 200f) // its colour split comes in wide and settles, like a tape tracking in; the arrows blink (flashes only)
			Sign(_front, 2 + Mathf.RoundToInt(6f * (1f - Ease((t - 200f) / 150f))), !Settings.Flashes || (int)(t / 500f) % 2 == 0);
		Menu.Lines(_front);
	}

	// The tape counter, where the Esc menu has it. The rewind (Rewind) draws these pieces too.
	internal static void Counter(CanvasItem on, string count) => Osd(on, count, Mathf.Round(W - 16 - Width(count, OsdFont, 16)), H - 12, 16, 1);

	// Top left, where the Esc menu says PAUSE, at twice its size: the restart key (the HUD's keycap) and REWIND ◀◀,
	// its colour split `split` pixels out. Pressed, the key sits down on its shadow.
	internal static void Sign(CanvasItem on, int split, bool arrows, bool pressed = false)
	{
		const int Top = 16, Baseline = 36; // REWIND's capitals are 20 px tall
		string key = Menu.KeyName("restart", 0);
		float x = 16f;
		if (key != "") // no key: the word alone
		{
			float kw = Mathf.Round(Width(key, SmallFont, 16)) + 4f, down = pressed ? 2f : 0f;
			R(on, x + 2, Top + 2, kw, 20, Ink);
			R(on, x + down, Top + down, kw, 20, Colors.White);
			Text(on, key, Mathf.Round(x + down + kw / 2f - Width(key, SmallFont, 16) / 2f), Top + 15 + down, SmallFont, 16, Ink);
			x += kw + 8f;
		}
		x = Mathf.Round(x + Osd(on, "REWIND", x, Baseline, 32, split) + 14f);
		if (arrows)
		{
			Tri(on, x, Top + 1, 2, left: true);
			Tri(on, x + 10, Top + 1, 2, left: true);
		}
	}

	// The Esc menu's arrow (Menu.Tri), `scale` times the size, pointing right (or left).
	internal static void Tri(CanvasItem on, float x, float y, int scale, bool left)
	{
		for (int r = 0; r < 9; r++)
		{
			int len = r <= 4 ? r + 1 : 9 - r;
			R(on, left ? x + scale * (5 - len) : x, y + scale * r, scale * len, scale, Colors.White);
		}
	}

	// The on-screen display's lettering (Menu.Osd): white, with a pink and a cyan fringe `split` pixels out.
	// Returns its width.
	internal static float Osd(CanvasItem on, string text, float x, float y, int size, int split)
	{
		Text(on, text, x - split, y, OsdFont, size, Pink);
		Text(on, text, x + split, y, OsdFont, size, Cyan);
		Text(on, text, x, y, OsdFont, size, Colors.White);
		return Width(text, OsdFont, size);
	}

	internal static float Ease(float x) => 1f - Mathf.Pow(1f - Mathf.Clamp(x, 0f, 1f), 3f); // quick, then settles

	private static float Width(string text, Font font, int size) => font.GetStringSize(text, HorizontalAlignment.Left, -1, size).X;

	// y is the baseline.
	private static void Text(CanvasItem on, string text, float x, float y, Font font, int size, Color color) =>
		on.DrawString(font, new Vector2(x, y), text, HorizontalAlignment.Left, -1, size, color);

	private static void R(CanvasItem on, float x, float y, float w, float h, Color color) => on.DrawRect(new Rect2(x, y, w, h), color);
}

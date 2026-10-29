using Godot;

namespace RentDay;

// The game's own look, shared by the menus, the HUD, the admin panel and the stairs' arrow: its colours
// and its pixel fonts.
public static class Style
{
	public static readonly Color Ink = new("07030f");      // near black: outlines, text on white
	public static readonly Color Soft = new("d8d2e8");     // pale text
	public static readonly Color Dim = new("8f88a6");      // greyed-out text
	public static readonly Color Pink = new("ff2a6d");     // the titles' red-pink fringe, the admin panel's switches
	public static readonly Color Magenta = new("ff2bd6");  // the song playing, the stairs' arrow
	public static readonly Color Cyan = new("2de2e6");     // the titles' cyan fringe, the slow-motion meter
	public static readonly Color Faint = new(1f, 1f, 1f, 0.3f);  // thin lines
	public static readonly Color Track = new(1f, 1f, 1f, 0.2f);  // under a bar, a scrollbar or a song's progress

	// VT323 for most text, Silkscreen for small print, Blox2 for the title; all drawn pixel-sharp.
	public static readonly FontFile OsdFont = PixelFont("res://ui/fonts/VT323-Regular.ttf");
	public static readonly FontFile SmallFont = PixelFont("res://ui/fonts/Silkscreen-Regular.ttf");
	public static readonly FontFile TitleFont = PixelFont("res://ui/Blox2.ttf");

	private static FontFile PixelFont(string path)
	{
		var font = GD.Load<FontFile>(path);
		font.Antialiasing = TextServer.FontAntialiasing.None;
		font.Hinting = TextServer.Hinting.None;
		font.SubpixelPositioning = TextServer.SubpixelPositioning.Disabled;
		return font;
	}
}

using System;
using System.Linq;
using Godot;

namespace RentDay;

// The admin panel: F1 in a run from the editor (an exported game has none). Every testing switch of
// DevSettings, live, plus tools: a weapon in your hand, any floor, a restart, every goon dead. The game
// pauses while it's open; F1 or Esc closes it. Switches are saved to the settings file, so the next run
// starts with them too. Drawn at the window's own resolution, so it reads clearly over the pixel art.
public partial class AdminPanel : Control
{
	public int FloorCount = 1;
	public Action Changed; // a switch was flipped: the list of active ones needs redoing

	// Every weapon in weapons/ but bare fists: guns first.
	private static readonly WeaponData[] Weapons = ResourceLoader.ListDirectory("res://weapons")
		.Where(file => file.EndsWith(".tres") && file != "fists.tres")
		.Select(file => GD.Load<Resource>("res://weapons/" + file) as WeaponData)
		.Where(weapon => weapon != null)
		.OrderBy(weapon => weapon.Melee).ThenBy(weapon => weapon.Name).ToArray();

	// The switches: off, a light outline with its knob to the left; on, pink with a white knob to the right.
	// (The default off switch is dark grey, lost on the dark panel.)
	private static readonly Texture2D Off = SwitchPicture(false), On = SwitchPicture(true);

	private static ImageTexture SwitchPicture(bool on)
	{
		const int W = 36, H = 20, R = H / 2;
		var light = new Color(0.85f, 0.82f, 0.91f);
		var image = Image.CreateEmpty(W, H, false, Image.Format.Rgba8);
		for (int y = 0; y < H; y++)
			for (int x = 0; x < W; x++)
			{
				var at = new Vector2(x + 0.5f, y + 0.5f);
				float pill = at.DistanceTo(new Vector2(Mathf.Clamp(at.X, R, W - R), R));
				float knob = at.DistanceTo(new Vector2(on ? W - R : R, R));
				if (knob <= R - 4)
					image.SetPixel(x, y, on ? Colors.White : light);
				else if (pill <= R && (on || pill > R - 2))
					image.SetPixel(x, y, on ? new Color(1f, 0.17f, 0.43f) : light);
			}
		return ImageTexture.CreateFromImage(image);
	}

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always; // it runs while the game is paused
		DevSettings s = DevSettings.Current;
		var rows = new VBoxContainer();

		Heading(rows, "GOONS");
		Switch(rows, "See you", s.GoonsSeeYou, on => s.GoonsSeeYou = on);
		Switch(rows, "Hear your shots", s.GoonsHearShots, on => s.GoonsHearShots = on);
		Switch(rows, "Shoot", s.GoonsShoot, on => s.GoonsShoot = on);

		Heading(rows, "YOU");
		Switch(rows, "God mode (you can't die)", s.GodMode, on => s.GodMode = on);
		Switch(rows, "Infinite ammo", s.InfiniteAmmo, on => s.InfiniteAmmo = on);
		Switch(rows, "Infinite slow motion", s.InfiniteSlowMotion, on => s.InfiniteSlowMotion = on);
		HBoxContainer give = Row(rows, "Give");
		foreach (WeaponData weapon in Weapons)
			Button(give, weapon.Name, () => (GetTree().GetFirstNodeInGroup("player") as Player)?.Equip(weapon, weapon.Ammo));
		Choice(Row(rows, "Start floors with"), ["Nothing", .. Weapons.Select(w => w.Name)], Array.IndexOf(Weapons, s.StartWeapon) + 1,
			i => s.StartWeapon = i == 0 ? null : Weapons[i - 1]);

		Heading(rows, "SHOW");
		Switch(rows, "Goon info", s.ShowGoonInfo, on => s.ShowGoonInfo = on);
		Switch(rows, "Red goo", s.RedGoo, on => s.RedGoo = on);

		Heading(rows, "FLOORS");
		HBoxContainer go = Row(rows, "Go to");
		for (int floor = 1; floor <= FloorCount; floor++)
		{
			int to = floor;
			Button(go, to.ToString(), () => Close(() => Main.Begin(GetTree(), new SaveGame.Data(SaveGame.Version, to, null, 0, DateTime.Now))));
		}
		Choice(Row(rows, "New game starts on"), Enumerable.Range(1, FloorCount).Select(f => f.ToString()).ToArray(),
			Math.Clamp(s.StartFloor, 1, FloorCount) - 1, i => s.StartFloor = i + 1);
		HBoxContainer tools = Row(rows, "");
		Button(tools, "Restart floor", () => Close(() => GetTree().ReloadCurrentScene()));
		Button(tools, "Kill all goons", () => Close(() =>
		{
			foreach (Enemy goon in GetTree().GetNodesInGroup("enemies").OfType<Enemy>().Where(goon => !goon.IsDead))
				goon.Die(Vector2.Right);
		}));
		Button(Row(rows, ""), "Reset all to normal", () =>
		{
			DevSettings.Current.Reset();
			Flipped();
			GetParent().AddChild(new AdminPanel { FloorCount = FloorCount, Changed = Changed }); // drawn anew, showing the normal values
			QueueFree();
		});
		rows.AddChild(new Label { Text = "F1 or Esc closes", Modulate = new Color(1f, 1f, 1f, 0.5f) });

		var box = new PanelContainer();
		var back = new StyleBoxFlat { BgColor = new Color(0.05f, 0.01f, 0.13f, 0.95f), BorderColor = new Color(1f, 0.17f, 0.43f) };
		back.SetBorderWidthAll(2);
		box.AddThemeStyleboxOverride("panel", back); // solid, so it reads over any floor
		var margin = new MarginContainer();
		foreach (string side in new[] { "left", "top", "right", "bottom" })
			margin.AddThemeConstantOverride("margin_" + side, 12);
		margin.AddChild(rows);
		box.AddChild(margin);
		AddChild(box);
		// One unit a window pixel, not a pixel of the art (which the window scales up).
		float scale = GetViewport().GetVisibleRect().Size.Y / Math.Max(DisplayServer.WindowGetSize().Y, 1);
		box.Scale = new Vector2(scale, scale);
		box.Position = new Vector2(4, 4);
	}

	public override void _UnhandledInput(InputEvent e)
	{
		if (e.IsActionPressed("admin_panel") || e.IsActionPressed("ui_cancel"))
		{
			GetViewport().SetInputAsHandled();
			Close();
		}
	}

	// Back to the game, then `then` (a tool that reloads or kills runs on the unpaused game).
	private void Close(Action then = null)
	{
		GetTree().Paused = false;
		QueueFree();
		then?.Invoke();
	}

	private void Flipped()
	{
		DevSettings.Current.Save();
		Changed?.Invoke();
	}

	private static void Heading(Container rows, string text) =>
		rows.AddChild(new Label { Text = text, Modulate = new Color(1f, 0.42f, 0.66f) });

	private void Switch(Container rows, string text, bool on, Action<bool> set)
	{
		var box = new CheckButton { Text = text, ButtonPressed = on };
		box.AddThemeIconOverride("checked", On);
		box.AddThemeIconOverride("unchecked", Off);
		box.Toggled += value =>
		{
			set(value);
			Flipped();
		};
		rows.AddChild(box);
	}

	private void Choice(Container row, string[] items, int picked, Action<int> set)
	{
		var choice = new OptionButton();
		foreach (string item in items)
			choice.AddItem(item);
		choice.Selected = picked;
		choice.ItemSelected += i =>
		{
			set((int)i);
			Flipped();
		};
		row.AddChild(choice);
	}

	private static HBoxContainer Row(Container rows, string label)
	{
		var row = new HBoxContainer();
		if (label != "")
			row.AddChild(new Label { Text = label });
		rows.AddChild(row);
		return row;
	}

	private static void Button(Container row, string text, Action pressed)
	{
		var button = new Button { Text = text };
		button.Pressed += pressed;
		row.AddChild(button);
	}
}

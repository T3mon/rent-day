using System;
using System.Linq;
using Godot;

namespace RentDay;

// The admin panel: F1 in a run from the editor (an exported game has none). Every testing switch of
// DevSettings, live, plus tools: a weapon in your hand, any floor, a restart, every enemy dead. The game
// pauses while it's open; F1 or Esc closes it. Switches are saved to the settings file, so the next run
// starts with them too. Drawn at the window's own resolution, so it reads clearly over the pixel art.
//
// Laid out like a settings screen: centred over the dimmed game, switches on the left (label left, switch
// right, a dot on any that differ from a normal game), actions on the right (buttons that wrap), Reset
// alone at the bottom. Every switch explains itself on hover.
public partial class AdminPanel : Control
{
	public int FloorCount = 1;
	public Action Changed; // a switch was flipped: the list of active ones needs redoing

	private const int FontSize = 22, SmallSize = 16, Gap = 6, ColumnWidth = 300;

	// Every weapon in weapons/ but bare fists: guns first.
	private static readonly WeaponData[] Weapons = ResourceLoader.ListDirectory("res://weapons")
		.Where(file => file.EndsWith(".tres") && file != "fists.tres" && file != "heavy_fists.tres")
		.Select(file => GD.Load<Resource>("res://weapons/" + file) as WeaponData)
		.Where(weapon => weapon != null)
		.OrderBy(weapon => weapon.Melee).ThenBy(weapon => weapon.Name).ToArray();

	// The switches: off, a light outline with its knob to the left; on, pink with a white knob to the right.
	// (The default off switch is dark grey, lost on the dark panel.)
	private static readonly Texture2D Off = SwitchPicture(false), On = SwitchPicture(true);

	private static readonly DevSettings Normal = new(); // what a normal game has, for the "changed" dots
	private Label _changedCount;

	private static ImageTexture SwitchPicture(bool on)
	{
		const int W = 36, H = 20, R = H / 2;
		var image = Image.CreateEmpty(W, H, false, Image.Format.Rgba8);
		for (int y = 0; y < H; y++)
			for (int x = 0; x < W; x++)
			{
				var at = new Vector2(x + 0.5f, y + 0.5f);
				float pill = at.DistanceTo(new Vector2(Mathf.Clamp(at.X, R, W - R), R));
				float knob = at.DistanceTo(new Vector2(on ? W - R : R, R));
				if (knob <= R - 4)
					image.SetPixel(x, y, on ? Colors.White : Style.Soft);
				else if (pill <= R && (on || pill > R - 2))
					image.SetPixel(x, y, on ? Style.Pink : Style.Soft);
			}
		return ImageTexture.CreateFromImage(image);
	}

	public override void _Ready()
	{
		ProcessMode = ProcessModeEnum.Always; // it runs while the game is paused
		DevSettings s = DevSettings.Current;

		// The paused game, dimmed: the panel is the only thing to look at.
		AddChild(new ColorRect { Color = new Color(0f, 0f, 0f, 0.55f), MouseFilter = MouseFilterEnum.Stop, Size = GetViewport().GetVisibleRect().Size });

		var page = new VBoxContainer();
		page.AddThemeConstantOverride("separation", 10);

		// Title bar: what this is, how many switches differ from a normal game, how to close it.
		var title = new HBoxContainer();
		title.AddChild(new Label { Text = "ADMIN PANEL", ThemeTypeVariation = "Title" });
		_changedCount = new Label { ThemeTypeVariation = "Hint", SizeFlagsHorizontal = SizeFlags.ExpandFill, VerticalAlignment = VerticalAlignment.Center };
		title.AddChild(_changedCount);
		title.AddChild(new Label { Text = "F1 / Esc  close", ThemeTypeVariation = "Hint", VerticalAlignment = VerticalAlignment.Center });
		page.AddChild(title);
		page.AddChild(new HSeparator());

		var columns = new HBoxContainer();
		columns.AddThemeConstantOverride("separation", 24);
		page.AddChild(columns);
		VBoxContainer left = Column(columns), right = Column(columns);

		Section(left, "ENEMIES");
		Switch(left, "See you", "Off: enemies are blind to you. They still patrol and hear shots.",
			() => s.EnemiesSeeYou, on => s.EnemiesSeeYou = on, Normal.EnemiesSeeYou);
		Switch(left, "Hear your shots", "Off: your gunshots alert nobody.",
			() => s.EnemiesHearShots, on => s.EnemiesHearShots = on, Normal.EnemiesHearShots);
		Switch(left, "Attack", "Off: enemies still spot you and aim, but never shoot or swing.",
			() => s.EnemiesShoot, on => s.EnemiesShoot = on, Normal.EnemiesShoot);

		Section(left, "YOU");
		Switch(left, "God mode", "You can't die.", () => s.GodMode, on => s.GodMode = on, Normal.GodMode);
		Switch(left, "Infinite ammo", "Your guns never run dry.", () => s.InfiniteAmmo, on => s.InfiniteAmmo = on, Normal.InfiniteAmmo);
		Switch(left, "Infinite slow motion", "The slow-motion meter never runs dry.",
			() => s.InfiniteSlowMotion, on => s.InfiniteSlowMotion = on, Normal.InfiniteSlowMotion);
		Switch(left, "No cooldowns", "Abilities are ready again as soon as they end. So far: the dash.",
			() => s.NoCooldowns, on => s.NoCooldowns = on, Normal.NoCooldowns);

		Section(left, "SHOW");
		Switch(left, "Enemy info", "What each enemy sees and where he's headed, and how far your shots carry.",
			() => s.ShowEnemyInfo, on => s.ShowEnemyInfo = on, Normal.ShowEnemyInfo);
		Switch(left, "Red goo", "Plain red blood instead of the mixed-colour goo.", () => s.RedGoo, on => s.RedGoo = on, Normal.RedGoo);

		Section(right, "WEAPONS");
		Hint(right, "Put in your hand now");
		var give = Flow(right);
		foreach (WeaponData weapon in Weapons)
			Button(give, weapon.Name, "Equip it with a full load.", () => (GetTree().GetFirstNodeInGroup("player") as Player)?.Equip(weapon, weapon.Ammo));
		Choice(right, "Start floors with", "Every floor starts with this in your hand.",
			["Nothing", .. Weapons.Select(w => w.Name)], Array.IndexOf(Weapons, s.StartWeapon) + 1,
			i => s.StartWeapon = i == 0 ? null : Weapons[i - 1], () => s.StartWeapon != Normal.StartWeapon);

		Section(right, "FLOORS");
		Hint(right, "Go to (closes the panel)");
		var go = Flow(right);
		for (int floor = 1; floor <= FloorCount; floor++)
		{
			int to = floor;
			Button(go, to.ToString(), $"Start floor {to}.", () => Close(() => Main.GoTo(GetTree(), to)));
		}
		if (ResourceLoader.Exists(Main.TestLevelPath)) // left out of exports
			Button(go, "Test level", "The gym: every weapon, a room per enemy type.", () => Close(() => Main.GoToTestLevel(GetTree())));
		Choice(right, "New game starts on", "Floor a new game begins on.",
			Enumerable.Range(1, FloorCount).Select(f => "Floor " + f).ToArray(), Math.Clamp(s.StartFloor, 1, FloorCount) - 1,
			i => s.StartFloor = i + 1, () => s.StartFloor != Normal.StartFloor);

		Section(right, "THIS FLOOR");
		var tools = Flow(right);
		Button(tools, "Restart floor", "Start this floor over. Closes the panel.", () => Close(() => GetTree().ReloadCurrentScene()));
		Button(tools, "Kill all enemies", "Every enemy on this floor dies. Closes the panel.", () => Close(() =>
		{
			foreach (Enemy enemy in GetTree().GetNodesInGroup("enemies").OfType<Enemy>().Where(enemy => !enemy.IsDead))
				enemy.Die(Vector2.Right);
		}));

		// Reset: apart from everything else, at the bottom, so it's never hit by accident.
		page.AddChild(new HSeparator());
		var bottom = new HBoxContainer();
		var reset = new Button { Text = "Reset all to normal", TooltipText = "Every switch back to how a normal game plays.", ThemeTypeVariation = "Danger" };
		reset.Pressed += () =>
		{
			DevSettings.Current.Reset();
			Flipped();
			GetParent().AddChild(new AdminPanel { FloorCount = FloorCount, Changed = Changed }); // drawn anew, showing the normal values
			QueueFree();
		};
		bottom.AddChild(reset);
		bottom.AddChild(new Label { Text = "Switches are kept for the next run.", ThemeTypeVariation = "Hint", SizeFlagsHorizontal = SizeFlags.ExpandFill, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center });
		page.AddChild(bottom);

		var box = new PanelContainer { Theme = PanelTheme() };
		var margin = new MarginContainer();
		foreach (string side in new[] { "left", "top", "right", "bottom" })
			margin.AddThemeConstantOverride("margin_" + side, 18);
		margin.AddChild(page);
		box.AddChild(margin);
		AddChild(box);
		CountChanged();

		// One unit a window pixel, not a pixel of the art (which the window scales up); centred.
		// A small window shrinks it to fit.
		Vector2 screen = GetViewport().GetVisibleRect().Size;
		box.ResetSize();
		float scale = screen.Y / Math.Max(DisplayServer.WindowGetSize().Y, 1);
		scale = Math.Min(scale, Math.Min(screen.X * 0.96f / box.Size.X, screen.Y * 0.96f / box.Size.Y));
		box.Scale = new Vector2(scale, scale);
		box.Position = ((screen - box.Size * scale) / 2f).Floor();

		// Keyboard and gamepad work too: start on the first switch.
		left.GetChildren().OfType<Control>().SelectMany(row => row.GetChildren().OfType<CheckButton>()).FirstOrDefault()?.GrabFocus();
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
		CountChanged();
	}

	// "3 changed" in the title: how far this run is from a normal game.
	private void CountChanged()
	{
		string summary = DevSettings.Current.ActiveSummary();
		int count = summary == "" ? 0 : summary.Split("  ").Length;
		_changedCount.Text = count == 0 ? "   all normal" : $"   {count} changed";
	}

	// The panel's look: the game's pixel font and colours, flat dark boxes, a pink frame and focus ring.
	private static Theme PanelTheme()
	{
		var theme = new Theme { DefaultFont = Style.OsdFont, DefaultFontSize = FontSize };
		StyleBoxFlat Box(Color fill, Color border, int width = 1, int pad = 6)
		{
			var box = new StyleBoxFlat { BgColor = fill, BorderColor = border };
			box.SetBorderWidthAll(width);
			box.SetContentMarginAll(pad);
			box.ContentMarginLeft = box.ContentMarginRight = pad + 4;
			return box;
		}
		var panel = Box(new Color(0.05f, 0.01f, 0.13f, 0.97f), Style.Pink, 2, 0);
		theme.SetStylebox("panel", "PanelContainer", panel);

		var normal = Box(new Color(1f, 1f, 1f, 0.06f), Style.Faint);
		var hover = Box(new Color(1f, 1f, 1f, 0.14f), Style.Soft);
		var pressed = Box(new Color(Style.Pink, 0.35f), Style.Pink);
		var focus = Box(Colors.Transparent, Style.Cyan, 2);
		focus.DrawCenter = false;
		var flat = Box(Colors.Transparent, Colors.Transparent, 0, 2);
		var flatHover = Box(new Color(1f, 1f, 1f, 0.06f), Colors.Transparent, 0, 2);
		foreach (string type in new[] { "Button", "OptionButton" })
		{
			theme.SetStylebox("normal", type, normal);
			theme.SetStylebox("hover", type, hover);
			theme.SetStylebox("pressed", type, pressed);
			theme.SetStylebox("hover_pressed", type, pressed);
			theme.SetStylebox("focus", type, focus);
			theme.SetColor("font_color", type, Style.Soft);
			theme.SetColor("font_hover_color", type, Colors.White);
			theme.SetColor("font_pressed_color", type, Colors.White);
		}
		theme.SetStylebox("normal", "CheckButton", flat);
		theme.SetStylebox("hover", "CheckButton", flatHover);
		theme.SetStylebox("pressed", "CheckButton", flat);
		theme.SetStylebox("hover_pressed", "CheckButton", flatHover);
		theme.SetStylebox("focus", "CheckButton", focus);
		foreach (string state in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_hover_pressed_color", "font_focus_color" })
			theme.SetColor(state, "CheckButton", Style.Soft);

		// Reset: outlined in pink, so it reads as the one button that undoes things.
		theme.SetTypeVariation("Danger", "Button");
		theme.SetStylebox("normal", "Danger", Box(Colors.Transparent, Style.Pink));
		theme.SetStylebox("hover", "Danger", Box(new Color(Style.Pink, 0.25f), Style.Pink));
		theme.SetColor("font_color", "Danger", Style.Pink);

		theme.SetColor("font_color", "Label", Style.Soft);
		theme.SetTypeVariation("Title", "Label");
		theme.SetColor("font_color", "Title", Colors.White);
		theme.SetFontSize("font_size", "Title", 28);
		theme.SetTypeVariation("Section", "Label");
		theme.SetColor("font_color", "Section", Style.Pink);
		theme.SetFont("font", "Section", Style.SmallFont);
		theme.SetFontSize("font_size", "Section", SmallSize);
		theme.SetTypeVariation("Hint", "Label");
		theme.SetColor("font_color", "Hint", Style.Dim);
		theme.SetFontSize("font_size", "Hint", 20);

		var line = new StyleBoxLine { Color = Style.Faint, Thickness = 1 };
		theme.SetStylebox("separator", "HSeparator", line);
		theme.SetStylebox("panel", "TooltipPanel", Box(Style.Ink, Style.Soft));
		theme.SetColor("font_color", "TooltipLabel", Style.Soft);
		theme.SetFontSize("font_size", "TooltipLabel", 18);
		theme.SetStylebox("panel", "PopupMenu", Box(Style.Ink, Style.Pink));
		return theme;
	}

	private static VBoxContainer Column(Container columns)
	{
		var column = new VBoxContainer { CustomMinimumSize = new Vector2(ColumnWidth, 0) };
		column.AddThemeConstantOverride("separation", Gap);
		columns.AddChild(column);
		return column;
	}

	// A small pink heading with space above it, so the sections read as groups.
	private static void Section(Container column, string text)
	{
		if (column.GetChildCount() > 0)
			column.AddChild(new Control { CustomMinimumSize = new Vector2(0, 8) });
		column.AddChild(new Label { Text = text, ThemeTypeVariation = "Section" });
	}

	// A row: a dot when it differs from a normal game, the name, then the switch at the right edge.
	private void Switch(Container column, string text, string tip, Func<bool> get, Action<bool> set, bool normal)
	{
		var row = new HBoxContainer();
		var dot = Dot(row);
		var box = new CheckButton { Text = text, ButtonPressed = get(), TooltipText = tip, SizeFlagsHorizontal = SizeFlags.ExpandFill };
		box.AddThemeIconOverride("checked", On);
		box.AddThemeIconOverride("unchecked", Off);
		box.Toggled += value =>
		{
			set(value);
			dot.Modulate = Shown(value != normal);
			Flipped();
		};
		dot.Modulate = Shown(get() != normal);
		row.AddChild(box);
		column.AddChild(row);
	}

	private void Choice(Container column, string text, string tip, string[] items, int picked, Action<int> set, Func<bool> changed)
	{
		var row = new HBoxContainer();
		var dot = Dot(row);
		row.AddChild(new Label { Text = text, TooltipText = tip, MouseFilter = MouseFilterEnum.Stop, SizeFlagsHorizontal = SizeFlags.ExpandFill });
		var choice = new OptionButton { TooltipText = tip, CustomMinimumSize = new Vector2(120, 0) };
		foreach (string item in items)
			choice.AddItem(item);
		choice.Selected = picked;
		choice.ItemSelected += i =>
		{
			set((int)i);
			dot.Modulate = Shown(changed());
			Flipped();
		};
		dot.Modulate = Shown(changed());
		row.AddChild(choice);
		column.AddChild(row);
	}

	// A small cyan square: "changed from a normal game".
	private static ColorRect Dot(Container row)
	{
		var holder = new CenterContainer { CustomMinimumSize = new Vector2(14, 0), TooltipText = "Changed from a normal game" };
		var dot = new ColorRect { Color = Style.Cyan, CustomMinimumSize = new Vector2(6, 6), MouseFilter = MouseFilterEnum.Ignore };
		holder.AddChild(dot);
		row.AddChild(holder);
		return dot;
	}

	private static Color Shown(bool shown) => shown ? Colors.White : Colors.Transparent;

	// Buttons that wrap onto the next line instead of widening the panel.
	private static HFlowContainer Flow(Container column)
	{
		var flow = new HFlowContainer();
		flow.AddThemeConstantOverride("h_separation", Gap);
		flow.AddThemeConstantOverride("v_separation", Gap);
		Indent(column).AddChild(flow);
		return flow;
	}

	private static void Hint(Container column, string text) =>
		Indent(column).AddChild(new Label { Text = text, ThemeTypeVariation = "Hint" });

	// In line with the rows, past their "changed" square.
	private static MarginContainer Indent(Container column)
	{
		var margin = new MarginContainer();
		margin.AddThemeConstantOverride("margin_left", 18);
		column.AddChild(margin);
		return margin;
	}

	private static void Button(Container row, string text, string tip, Action pressed)
	{
		var button = new Button { Text = text, TooltipText = tip, CustomMinimumSize = new Vector2(44, 32) };
		button.Pressed += pressed;
		row.AddChild(button);
	}
}

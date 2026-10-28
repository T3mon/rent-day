using Godot;

namespace RentDay;

// Slow motion's afterimages: how someone looks this moment (legs, body, gun), left where he stands as a flat
// neon silhouette that turns from cyan to magenta as it fades, over a moment of real time.
public static class Afterimage
{
	private const float Life = 0.3f; // real seconds
	private static readonly Color Fresh = new(0.18f, 0.89f, 0.9f, 0.5f), Gone = new(0.97f, 0.02f, 0.81f, 0f);
	private static readonly Material Flat = new ShaderMaterial { Shader = GD.Load<Shader>("res://effects/afterimage.gdshader") };

	public static void Leave(Node2D who)
	{
		// Above slow motion's look (z 7), under you (lifted to 9 meanwhile).
		var ghost = new Node2D { ZIndex = 8, ZAsRelative = false, Modulate = Fresh, Material = Flat };
		foreach (Node node in who.FindChildren("*", "", true, false))
		{
			if (node is not CanvasItem { Visible: true } part || !part.IsVisibleInTree())
				continue;
			var copy = node switch
			{
				AnimatedSprite2D a => new Sprite2D { Texture = a.SpriteFrames.GetFrameTexture(a.Animation, a.Frame), Centered = a.Centered, Offset = a.Offset, FlipH = a.FlipH, FlipV = a.FlipV },
				Sprite2D s => new Sprite2D { Texture = s.Texture, Centered = s.Centered, Offset = s.Offset, FlipH = s.FlipH, FlipV = s.FlipV,
					RegionEnabled = s.RegionEnabled, RegionRect = s.RegionRect, Hframes = s.Hframes, Vframes = s.Vframes, Frame = s.Frame },
				_ => null,
			};
			if (copy == null)
				continue;
			copy.Transform = ((Node2D)node).GlobalTransform; // the level's root sits at (0, 0)
			copy.UseParentMaterial = true;
			ghost.AddChild(copy);
		}
		who.GetTree().CurrentScene.AddChild(ghost);
		Tween fade = ghost.CreateTween().SetIgnoreTimeScale();
		fade.TweenProperty(ghost, "modulate", Gone, Life);
		fade.TweenCallback(Callable.From(ghost.QueueFree));
	}
}

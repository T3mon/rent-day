using Godot;

namespace RentDay;

public static class Effects
{
	// Drops a copy of an effect scene (goo, smoke, sparks...) into the level at a
	// position and angle. Effects go into the level, not into whoever caused
	// them, so they stay put when that character moves or dies.
	public static void Spawn(PackedScene scene, Node from, Vector2 position, float rotation)
	{
		if (scene == null)
			return;
		var fx = scene.Instantiate<Node2D>();
		fx.Position = position;
		fx.Rotation = rotation;
		from.GetTree().CurrentScene.AddChild(fx);
	}
}

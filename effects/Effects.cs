using Godot;

namespace RentDay;

public static class Effects
{
	// Drops a copy of an effect scene (goo, smoke, sparks...) into the level at a
	// position and angle. Effects go into the level, not into whoever caused
	// them, so they stay put when that character moves or dies.
	public static Node2D Spawn(PackedScene scene, Node from, Vector2 position, float rotation)
	{
		if (scene == null)
			return null;
		var fx = scene.Instantiate<Node2D>();
		from.GetTree().CurrentScene.AddChild(fx);
		fx.GlobalPosition = position;
		fx.GlobalRotation = rotation;
		return fx;
	}
}

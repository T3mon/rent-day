using Godot;

namespace RentDay;

// Gunshots are loud. Every goon within earshot comes to see what happened.
// Sound goes through walls on purpose: a loud gun can draw the whole floor.
public static class Noise
{
	public static void Make(Node from, Vector2 where, float radius)
	{
		if (DevSettings.Current.ShowGoonInfo)
			NoiseRing.Show(from, where, radius);
		if (!DevSettings.Current.GoonsHearShots)
			return;

		foreach (Node node in from.GetTree().GetNodesInGroup("enemies"))
		{
			if (node is Enemy goon && goon.GlobalPosition.DistanceTo(where) <= radius)
				goon.Hear(where);
		}
	}
}

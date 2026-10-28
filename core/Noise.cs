using System.Collections.Generic;
using System.Linq;
using Godot;

namespace RentDay;

// Gunshots are loud. Of the enemies within earshot who'd come running, the three with the shortest
// walk to the shot come to see what happened (the rest stay at what they're doing).
// Sound goes through walls on purpose: a loud gun can draw enemies from the next rooms.
public static class Noise
{
	private const int Comers = 3;

	public static void Make(Node from, Vector2 where, float radius)
	{
		if (DevSettings.Current.ShowEnemyInfo)
			NoiseRing.Show(from, where, radius);
		if (!DevSettings.Current.EnemiesHearShots)
			return;

		var heard = new List<Enemy>();
		foreach (Node node in from.GetTree().GetNodesInGroup("enemies"))
		{
			if (node is Enemy enemy && enemy.CanHear && enemy.GlobalPosition.DistanceTo(where) <= radius)
				heard.Add(enemy);
		}
		IEnumerable<Enemy> coming = heard.Count <= Comers ? heard
			: heard.OrderBy(enemy => LevelGrid.WalkLength(enemy.GlobalPosition, where)).Take(Comers);
		foreach (Enemy enemy in coming)
			enemy.Hear(where);
	}
}

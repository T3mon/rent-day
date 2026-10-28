using Godot;

namespace RentDay;

// "Hit stop": the whole game slows to a crawl for a split second when something
// dies, which makes the hit feel heavy.
public static class HitStop
{
	public static void Freeze(Node anyNode, float seconds = 0.07f, float slowTo = 0.05f)
	{
		if (SlowMotion.On)
			return; // already slowed down: the hit feels heavy enough
		Engine.TimeScale = slowTo;

		// This timer ignores the slow-down (last argument), so it lasts `seconds` of real time,
		// then puts the game back to its speed (slow, if slow motion started meanwhile).
		anyNode.GetTree().CreateTimer(seconds, true, false, true).Timeout += () => Engine.TimeScale = SlowMotion.Speed;
	}
}

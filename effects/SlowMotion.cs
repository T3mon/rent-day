using Godot;

namespace RentDay;

// Slow motion: the whole game runs at Speed, you included, and so do its sounds; the music keeps
// its own speed (Music.SlowDown). The player holds it and runs its meter. HitStop leaves it alone.
public static class SlowMotion
{
	public static float Speed { get; private set; } = 1f; // 1 = normal
	public static bool On => Speed < 1f;

	public static void Set(float speed)
	{
		Speed = speed;
		Engine.TimeScale = speed;
		AudioServer.PlaybackSpeedScale = speed;
		Music.SlowDown(speed);
	}
}

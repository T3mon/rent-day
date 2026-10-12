namespace RentDay;

// The physics layers, matching the names in Project Settings > Layer Names > 2D Physics.
// Each layer is one bit, so they combine with |, e.g. World | Enemies.
public static class Layers
{
	public const uint World = 1 << 0;   // layer 1: walls
	public const uint Player = 1 << 1;  // layer 2
	public const uint Enemies = 1 << 2; // layer 3
	public const uint Bullets = 1 << 3; // layer 4

	// layer 5: furniture blocks walking, but bullets and eyesight pass over it
	public const uint Furniture = 1 << 4;
}

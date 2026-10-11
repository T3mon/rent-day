using Godot;

namespace RentDay;

// Anything a bullet can kill. The bullet doesn't care whether it hit
// the player or a goon; it just calls Die().
public interface IKillable
{
	void Die(Vector2 hitDirection);
}

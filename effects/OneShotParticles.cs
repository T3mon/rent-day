using Godot;

namespace RentDay;

// A burst of particles (goo spray, smoke, sparks) that plays once and then
// removes itself, so finished effects don't pile up in the level.
public partial class OneShotParticles : CpuParticles2D
{
	public override void _Ready()
	{
		Finished += QueueFree;
		Restart(); // start the burst now
	}
}

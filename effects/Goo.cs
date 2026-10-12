using Godot;

namespace RentDay;

// A splat of neon goo left where someone died. It lies on the floor under the
// bodies, and every splat gets its own random trippy colours.
public partial class Goo : Sprite2D
{
	public override void _Ready()
	{
		// Each splat needs its own copy of the material, otherwise they'd all share one seed.
		var material = (ShaderMaterial)Material.Duplicate();
		material.SetShaderParameter("seed", GD.Randf());
		Material = material;

		Scale = Vector2.One * (float)GD.RandRange(1.1, 1.6);
		FlipV = GD.Randf() < 0.5f;
	}
}

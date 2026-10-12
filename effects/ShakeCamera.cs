using Godot;

namespace RentDay;

// The player's camera, with screen shake. Any script can call
// ShakeCamera.Shake(0.3f) without needing to find the camera first.
//
// Shake works with "trauma": every hit adds some (0..1), it fades over time,
// and the camera jitters by trauma squared, so small hits stay subtle while
// big ones really kick.
public partial class ShakeCamera : Camera2D
{
	[Export] public float MaxOffset = 8f; // pixels of jitter at full trauma
	[Export] public float Decay = 2.5f;   // trauma lost per second

	private static ShakeCamera _active;
	private float _trauma;

	public static void Shake(float amount)
	{
		if (_active != null)
			_active._trauma = Mathf.Min(1f, _active._trauma + amount);
	}

	public override void _EnterTree() => _active = this;

	public override void _ExitTree()
	{
		if (_active == this)
			_active = null;
	}

	public override void _Process(double delta)
	{
		_trauma = Mathf.Max(0f, _trauma - Decay * (float)delta);
		float strength = _trauma * _trauma * MaxOffset;

		// Whole pixels only, so the pixel art stays crisp while shaking.
		Offset = new Vector2(
			Mathf.Round((float)GD.RandRange(-1.0, 1.0) * strength),
			Mathf.Round((float)GD.RandRange(-1.0, 1.0) * strength));
	}
}

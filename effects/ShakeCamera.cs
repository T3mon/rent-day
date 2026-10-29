using Godot;

namespace RentDay;

// The player's camera, with screen shake. Any script can call
// ShakeCamera.Shake(2f) without needing to find the camera first.
//
// A hit sets how far the picture may jump, in pixels (a bigger shake already going wins).
// It jumps at random within that every frame and fades in a straight line, so even a small hit
// shows and a big one lasts longer (2 px fades in 0.2 s).
public partial class ShakeCamera : Camera2D
{
	[Export] public float Fade = 10f; // pixels of shake lost per second

	private static ShakeCamera _active;
	private float _shake;

	public static void Shake(float pixels)
	{
		if (_active != null)
			_active._shake = Mathf.Max(_active._shake, pixels * Settings.ShakeScale); // the SCREEN SHAKE setting
	}

	public override void _EnterTree() => _active = this;

	public override void _ExitTree()
	{
		if (_active == this)
			_active = null;
	}

	public override void _Process(double delta)
	{
		_shake = Mathf.Max(0f, _shake - Fade * (float)delta);

		// Whole pixels only, so the pixel art stays crisp while shaking.
		Offset = new Vector2(
			Mathf.Round((float)GD.RandRange(-_shake, _shake)),
			Mathf.Round((float)GD.RandRange(-_shake, _shake)));
	}
}

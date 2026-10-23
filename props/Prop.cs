using Godot;

namespace RentDay;

// A piece of furniture. Its shadow falls down-right on screen however the piece is turned, and the
// further the taller it stands, so a wardrobe, a sofa and a rug on the floor read as different heights.
public partial class Prop : StaticBody2D
{
	[Export] public float Height = 3f; // pixels its shadow falls

	public override void _Ready() => GetNode<Node2D>("Shadow").Position = new Vector2(Height, Height).Rotated(-GlobalRotation);
}

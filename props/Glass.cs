using System.Collections.Generic;
using Godot;

namespace RentDay;

// A glass pane: nobody walks through it, but bullets and eyesight pass. A shot (anyone's), a gun
// flying into it or a door swung into it shatters it: the shards fly out the far side, spinning, and
// stay on the floor, and the empty frame still blocks walking. Enemies don't hear it break, and only
// one shatter sound plays at a time. Goo stops at it, whole or broken, like at a wall.
public partial class Glass : Prop
{
	[Export] public Texture2D BrokenLook;
	[Export] public Texture2D ShardLook;        // the shard pictures side by side, ShardSize pixels each
	[Export] public int ShardCount = 24;
	[Export] public float ShardSpeed = 300f;    // the fastest a shard flies off (pixels per second)
	[Export] public float ShardFriction = 360f; // pixels per second a shard loses every second

	private const int ShardSize = 6;
	private const float Spread = 8f;            // degrees either side of straight out
	private const float SpinSpeed = 480f;       // fastest a shard turns (degrees per second)

	private class Shard { public Vector2 Pos, Vel; public float Angle, Spin; public int Look; }

	private static AudioStreamPlayer2D _sound;  // the shatter sound that played last
	private readonly List<Shard> _shards = new();
	private Node2D _floor;                      // draws the shards, on the floor among the clutter
	private bool _broken;

	// The pane, end to end (pixels).
	public float Length => ((RectangleShape2D)GetNode<CollisionShape2D>("CollisionShape2D").Shape).Size.X;

	public override void _Ready()
	{
		base._Ready();
		AddToGroup("glass");
		SetPhysicsProcess(false); // only while shards fly
	}

	// `direction`: the way the shot, gun or door was going; the shards fly out of the face it points to.
	public void Shatter(Vector2 direction)
	{
		if (_broken)
			return;
		_broken = true;
		SetDeferred(CollisionObject2D.PropertyName.CollisionLayer, CollisionLayer & ~Layers.Glass); // can be called mid physics step
		GetNode<Sprite2D>("Sprite").Texture = GetNode<Sprite2D>("Shadow").Texture = BrokenLook;

		Vector2 along = GlobalTransform.X.Normalized(), across = along.Orthogonal();
		if (across.Dot(direction) < 0f)
			across = -across;
		float length = Length;
		for (int i = 0; i < ShardCount; i++)
			_shards.Add(new Shard
			{
				Pos = GlobalPosition + along * length * ((i + 0.5f) / ShardCount - 0.5f), // spread along the whole pane
				Vel = across.Rotated(Mathf.DegToRad((float)GD.RandRange(-Spread, Spread))) * GD.Randf() * ShardSpeed,
				Angle = GD.Randf() * Mathf.Tau,
				Spin = Mathf.DegToRad((float)GD.RandRange(-SpinSpeed, SpinSpeed)),
				Look = GD.RandRange(0, ShardLook.GetWidth() / ShardSize - 1),
			});
		_floor = new Node2D { TopLevel = true, ZAsRelative = false, ZIndex = 2 };
		_floor.Draw += DrawShards;
		AddChild(_floor);
		SetPhysicsProcess(true);

		if (_sound == null || !IsInstanceValid(_sound) || !_sound.Playing)
		{
			_sound = GetNode<AudioStreamPlayer2D>("ShatterSound");
			_sound.Play();
		}
	}

	// Shards slow down and stop, or stop against a wall; then they lie still for good.
	public override void _PhysicsProcess(double delta)
	{
		float dt = (float)delta;
		var space = GetWorld2D().DirectSpaceState;
		bool flying = false;
		foreach (Shard shard in _shards)
		{
			if (shard.Vel == Vector2.Zero)
				continue;
			Vector2 step = shard.Vel * dt;
			if (space.IntersectRay(PhysicsRayQueryParameters2D.Create(shard.Pos, shard.Pos + step, Layers.World | Layers.Doors)).Count > 0)
				shard.Vel = Vector2.Zero;
			else
			{
				shard.Pos += step;
				shard.Angle += shard.Spin * dt;
				shard.Vel = shard.Vel.MoveToward(Vector2.Zero, ShardFriction * dt);
				flying |= shard.Vel != Vector2.Zero;
			}
		}
		_floor.QueueRedraw();
		SetPhysicsProcess(flying);
	}

	// Flying, a shard is faint with a shadow under it; lying on the floor, it's brighter.
	private void DrawShards()
	{
		var at = new Rect2(-ShardSize / 2f, -ShardSize / 2f, ShardSize, ShardSize);
		foreach (Shard shard in _shards)
		{
			var look = new Rect2(shard.Look * ShardSize, 0, ShardSize, ShardSize);
			bool flying = shard.Vel != Vector2.Zero;
			if (flying)
			{
				_floor.DrawSetTransform(shard.Pos + Vector2.One, shard.Angle);
				_floor.DrawTextureRectRegion(ShardLook, at, look, new Color(0f, 0f, 0f, 0.25f));
			}
			_floor.DrawSetTransform(shard.Pos, shard.Angle);
			_floor.DrawTextureRectRegion(ShardLook, at, look, new Color(1f, 1f, 1f, flying ? 0.5f : 0.85f));
		}
	}
}

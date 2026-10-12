using System.Linq;
using Godot;

namespace RentDay;

// Everything the player and the goons have in common: a body that faces where
// it aims, legs that walk where it moves, a gun (or bare fists), and death
// from a single hit. Player and Enemy build on this.
//
// Expected children:
//   Legs              - walking legs, turn toward the direction of movement
//   Visual            - rotates to face where we aim
//     Alive/Sprite    - the standing body
//     Alive/Gun
//     Corpse/Body     - the dead look, hidden until we die
//   CollisionShape2D
//   DeathSound
//
// Draw order (z index): floor 0, goo 1, corpses 2, the living 3, bullets 4.
public partial class Character : CharacterBody2D, IKillable
{
	// Fired once, at the moment we die. Main listens to this.
	[Signal] public delegate void DiedEventHandler();

	// Looks. Leave a slot empty to keep what the scene shows; fill them on a
	// placed goon to give him a different outfit.
	[Export] public Texture2D ArmedLook;
	[Export] public Texture2D UnarmedLook;
	[Export] public Texture2D LegsLook;

	// Death poses: one is picked at random each time, so bodies don't all look alike.
	[Export] public Texture2D DeadLook;          // on the back
	[Export] public Texture2D DeadLookFaceDown;
	[Export] public Texture2D DeadLookSide;      // curled up on the side

	[Export] public bool StartsArmed = true;

	// The splat of neon goo left on the floor when we die, and the spray flying out on the hit.
	[Export] public PackedScene GooScene;
	[Export] public PackedScene SprayScene;

	public bool IsDead { get; private set; }

	// Holding a gun? Swaps the look (one long gun arm vs. fists up) and shows the gun.
	public bool HasGun
	{
		get => _hasGun;
		set
		{
			_hasGun = value;
			Gun.Visible = value;
			Texture2D look = value ? ArmedLook : UnarmedLook;
			if (look != null)
				_bodySprite.Texture = look;
		}
	}

	protected Node2D Visual { get; private set; }
	protected Gun Gun { get; private set; }

	private const int CorpseZ = 2;

	// One step of the walk cycle per this many pixels travelled.
	private const float StepLength = 7f;
	private static readonly int[] WalkCycle = { 1, 0, 2, 0 };

	private bool _hasGun;
	private Sprite2D _bodySprite;
	private Sprite2D _corpseBody;
	private Sprite2D _legs;
	private Node2D _alive;
	private Node2D _corpse;
	private CollisionShape2D _collision;
	private AudioStreamPlayer2D _deathSound;
	private float _walked;

	public override void _Ready()
	{
		Visual = GetNode<Node2D>("Visual");
		Gun = GetNode<Gun>("Visual/Alive/Gun");
		_bodySprite = GetNode<Sprite2D>("Visual/Alive/Sprite");
		_legs = GetNode<Sprite2D>("Legs");
		_alive = GetNode<Node2D>("Visual/Alive");
		_corpse = GetNode<Node2D>("Visual/Corpse");
		_collision = GetNode<CollisionShape2D>("CollisionShape2D");
		_deathSound = GetNode<AudioStreamPlayer2D>("DeathSound");
		_corpseBody = GetNode<Sprite2D>("Visual/Corpse/Body");

		if (LegsLook != null)
			_legs.Texture = LegsLook;
		HasGun = StartsArmed;
	}

	// Runs every drawn frame: purely visual, so it doesn't need to be in step with physics.
	public override void _Process(double delta)
	{
		if (IsDead)
			return;

		float speed = Velocity.Length();
		if (speed < 10f)
		{
			_legs.Frame = 0; // standing
			_walked = 0;
			return;
		}

		// Legs point where we're going, which may differ from where we aim.
		_legs.Rotation = Velocity.Angle();
		_walked += speed * (float)delta;
		_legs.Frame = WalkCycle[(int)(_walked / StepLength) % WalkCycle.Length];
	}

	// hitDirection is the way the bullet was flying.
	public void Die(Vector2 hitDirection)
	{
		if (IsDead)
			return;
		IsDead = true;
		Velocity = Vector2.Zero;

		// Stop being solid, so bullets and people pass over the body.
		// "Deferred" means "after the current physics step": Godot doesn't allow
		// changing collisions in the middle of checking them.
		_collision.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);

		// Pick a random death pose (sometimes mirrored), fall the way the bullet
		// was going, and swap the standing look for the corpse.
		Texture2D[] poses = new[] { DeadLook, DeadLookFaceDown, DeadLookSide }.Where(t => t != null).ToArray();
		if (poses.Length > 0)
			_corpseBody.Texture = poses[GD.RandRange(0, poses.Length - 1)];
		_corpseBody.FlipV = GD.Randf() < 0.5f;
		Visual.Rotation = hitDirection.Angle();
		_alive.Visible = false;
		_legs.Visible = false;
		_corpse.Visible = true;
		ZIndex = CorpseZ;

		// Goo sprays out the way the bullet went, and a splat stays on the floor.
		Effects.Spawn(SprayScene, this, GlobalPosition, hitDirection.Angle());
		Effects.Spawn(GooScene, this, GlobalPosition + hitDirection * 6f,
			hitDirection.Angle() + (float)GD.RandRange(-0.4, 0.4));

		// The body is knocked back a little in the direction of the shot.
		CreateTween()
			.TweenProperty(this, "global_position", GlobalPosition + hitDirection * 8f, 0.2)
			.SetTrans(Tween.TransitionType.Cubic)
			.SetEase(Tween.EaseType.Out);

		// Make the kill land: kick the screen and freeze the game for a split second.
		ShakeCamera.Shake(0.6f);
		HitStop.Freeze(this);

		_deathSound.Play();
		EmitSignal(SignalName.Died);
	}
}

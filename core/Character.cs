using Godot;

namespace RentDay;

// Everything the player and the goons have in common: a body that faces where
// it aims, carries a gun, and dies from a single hit. Player and Enemy build on this.
//
// Expected children:
//   Visual            - rotates to face where we aim
//     Alive           - the standing look, including the Gun
//     Corpse          - the dead look, hidden until we die
//   CollisionShape2D
//   DeathSound
public partial class Character : CharacterBody2D, IKillable
{
	// Fired once, at the moment we die. Main listens to this.
	[Signal] public delegate void DiedEventHandler();

	public bool IsDead { get; private set; }

	protected Node2D Visual { get; private set; }
	protected Gun Gun { get; private set; }

	private Node2D _alive;
	private Node2D _corpse;
	private CollisionShape2D _collision;
	private AudioStreamPlayer2D _deathSound;

	public override void _Ready()
	{
		Visual = GetNode<Node2D>("Visual");
		Gun = GetNode<Gun>("Visual/Alive/Gun");
		_alive = GetNode<Node2D>("Visual/Alive");
		_corpse = GetNode<Node2D>("Visual/Corpse");
		_collision = GetNode<CollisionShape2D>("CollisionShape2D");
		_deathSound = GetNode<AudioStreamPlayer2D>("DeathSound");
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

		// Fall the way the bullet was going, and swap the standing look for the corpse.
		Visual.Rotation = hitDirection.Angle();
		_alive.Visible = false;
		_corpse.Visible = true;

		// The living are drawn at layer 1; a corpse drops to the floor's layer 0.
		ZIndex = 0;

		_deathSound.Play();
		EmitSignal(SignalName.Died);
	}
}

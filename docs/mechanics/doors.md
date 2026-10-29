# Doors

How doors behave, and how to place them in a level. Tuning values live in the door scene and are not listed here.

## Swinging

- Walk into a door and it flies open, away from you. Only walking into it pushes it: standing still next to a door does nothing.
- Every push sets the same swing speed, however fast you walk. Your push on doors in left-right walls is a little softer, so they open less wide than doors in up-down walls.
- A swinging door slows down at a steady rate and stays where it stops. Doors don't close by themselves.
- A door drifting slowly back past its doorway clicks shut.
- Each side has a hard stop; the door bounces off it just as fast as it hit it.
- Walls and furniture bounce a door back at half speed. A glass pane shatters first; its empty frame still bounces the door.
- A door that is still swinging hard ignores new pushes until it slows down.
- A door drifting slowly into someone standing still comes to rest against them.
- Nobody is ever blocked by a door: a door swinging hard passes through people.

## Slams

- A door you pushed, swinging hard, knocks down any enemy it passes. He drops his gun (see [enemies.md](enemies.md)).
- A slam sounds like the door cracking into him (`audio/door_hit.wav`, turned down 4 dB on the door's SlamSound), and he hits the floor with a body fall.
- A door an enemy pushed open never knocks anyone down.

## Bullets

- Doors stop bullets, wherever the door hangs.
- Each bullet nudges the door the way it flies. A single shot can't open a closed door (it clicks back shut), but nudges an open one.
- A shotgun blast, or a shot fired right against a door, flings it open.
- A door your bullets fling open slams enemies like one you pushed. Enemies' bullets push doors too, but never make a slam.

## Sight

- A door blocks enemies' sight wherever it hangs: a closed door hides the room, an open one only the line it lies across.

## Placing doors in a level

- Every door needs a completely free swing on both sides, all the way to its stops: no walls and no furniture within its reach.
- Never hinge a door where a wall runs off at a right angle, like the corner where a hallway meets a wall. The door would swing into it. Leave enough wall between the hinge and the side wall that the door at its stop can't reach it.
- So a doorway at the end of a hallway is narrower than the hallway, with the hinge set back from the side wall.
- Prefer single doors. A double door at the end of a hallway can't pass the rule above.
- Furniture inside a swing makes the door bounce back and can trap it against someone: keep it out.
- Doorways can have different widths; set the door's Length to the doorway.
- A door in a thick outer wall stops a little early against its own wall. That's fine.

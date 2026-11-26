# Floors and stairs

The building is played floor by floor, bottom to top.

## Clearing a floor

- A floor is clear when every enemy on it is dead. A knocked-down enemy still counts as alive.
- While any enemy is alive, the stairs up do nothing.
- When the floor is clear, "FLOOR CLEAR" shows for a moment and a pink arrow bobs over the stairs up.
- Walking onto the open stairs fades to black and starts the next floor, on the stairs you came up. No loading: every floor was read in on the loading screen.

## What carries over

- You keep the gun you're holding, with its rounds left, onto the next floor.
- Nothing else carries over: every floor starts fresh.

## Restarting and the end

- Dying: the camera punches in on your body, the floor goes neon, and a stopped tape display shows how long you
  lasted and REWIND with the restart key. It never holds you up.
- Shot standing, your body plays one of three dying animations, picked at random, along the way the shot went: blown
  back into a skid with a last kick, a crawl face down, or hauling yourself along on your back with one hand on your
  gut. They twitch and jerk rather than glide. Goo smears along where you slide and pools where you stop; the
  last pose holds. With a wall within 44 px that way, you just fall,
  so walls never hide you.
- R restarts the current floor, with the gun you came up the stairs with, on any frame.
- The fresh floor is yours at once. Over it, the try before plays back for a moment: everyone who moved races home
  the way they went as a see-through ghost, under the living and in nobody's way, and the tape counter spins back to
  0:00:00, then PLAY. Nothing waits for it.
- The last floor has no stairs up. Clearing it shows "RENT COLLECTED". R then starts again from the first floor, empty-handed.

## The edge of the map

- The map ends where its floor tiles end. Nobody walks off it: you stop at the edge, and so does an enemy thrown at it. Bullets fly on out.

## Level design for floors

- One floor = one level scene in `levels/` (`level_1.tscn` up), listed in order in `main.tscn` (Main's Floors).
- A new floor can be drafted with the level builder (`tools/levels/`, see [architecture.md](../architecture.md)),
  which checks the rules below that it can measure. After that, edit the scene in Godot.
- Each floor has stairs up (its exit), except the last. Floors above the first also have the stairs you arrive on, with the player placed at their top.
- Put the stairs up far from where you arrive, so the floor has to be crossed.
- Leave a gun near where you arrive, for a player who comes up empty-handed, and at most one more weapon lying about.
- Stairs are walked onto, not around: keep furniture off them and leave a free approach.
- No green anywhere in a level: green is the crosshair's colour, and it must always stand out.
- Size: 2 to 2.7 screens of floor inside the walls (a screen is 400 x 225), in 6 to 8 rooms. One or two of
  them small (110 x 110 or less), one or two left empty.
- Enemies: about 6 or 7 per screen of floor, so 14 to 18 a floor, in groups of 2 to 4. Nobody waits alone:
  every enemy that stays put has another within about 110 px. About a third carry a bat or a knife.
- Room to move: halls 64 to 96 px wide, open doorways 40 to 96 px.
- Few doors, 2 or 3 a floor; most rooms open into each other through gaps. Never one enemy alone behind a door.
- Every floor has a room walled in glass, so you can see a fight before you walk into it.
- From the second floor on, each floor brings in one new enemy type (dogs, the heavy, dodgers, shields); the
  last floor mixes them all.
- Mix the enemy behaviours: guards facing doorways, patrollers in corridors, wanderers in rooms.
- Give guards and wanderers something to do (Pose, see [enemies.md](enemies.md)): sitters on a couch or
  chair (facing = 90 + the seat's rotation; seats face down at rotation 0), leaners with their back
  to a wall, talkers in pairs facing each other. Patrollers never stand still, so they never pose.
- Give rooms more than one way in where you can, so there are routes around a guard.
- Doors follow [doors.md](doors.md); gaps for enemies follow [enemies.md](enemies.md).

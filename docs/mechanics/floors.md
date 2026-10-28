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

- R restarts the current floor, with the gun you came up the stairs with.
- The last floor has no stairs up. Clearing it shows "RENT COLLECTED". R then starts again from the first floor, empty-handed.

## The edge of the map

- The map ends where its floor tiles end. Nobody walks off it: you stop at the edge, and so does an enemy thrown at it. Bullets fly on out.

## Level design for floors

- One floor = one level scene in `levels/`, listed in order in `main.tscn` (Main's Floors).
- Each floor has stairs up (its exit), except the last. Floors above the first also have the stairs you arrive on, with the player placed at their top.
- Put the stairs up far from where you arrive, so the floor has to be crossed.
- Leave a gun near where you arrive, for a player who comes up empty-handed.
- Stairs are walked onto, not around: keep furniture off them and leave a free approach.
- No green anywhere in a level: green is the crosshair's colour, and it must always stand out.
- Mix the enemy behaviours: guards facing doorways, patrollers in corridors, wanderers in rooms.
- Give guards and wanderers something to do (Pose, see [enemies.md](enemies.md)): sitters on a couch or
  chair (facing = 90 + the seat's rotation; seats face down at rotation 0), leaners with their back
  to a wall, talkers in pairs facing each other. Patrollers never stand still, so they never pose.
- Give rooms more than one way in where you can, so there are routes around a guard.
- Doors follow [doors.md](doors.md); gaps for enemies follow [enemies.md](enemies.md).

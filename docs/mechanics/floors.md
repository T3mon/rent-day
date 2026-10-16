# Floors and stairs

The building is played floor by floor, bottom to top.

## Clearing a floor

- A floor is clear when every goon on it is dead. A knocked-down goon still counts as alive.
- While any goon is alive, the stairs up do nothing.
- When the floor is clear, "FLOOR CLEAR" shows for a moment and a pink arrow bobs over the stairs up.
- Walking onto the open stairs fades to black and starts the next floor, on the stairs you came up.

## What carries over

- You keep the gun you're holding, with its rounds left, onto the next floor.
- Nothing else carries over: every floor starts fresh.

## Restarting and the end

- R restarts the current floor, with the gun you came up the stairs with.
- The last floor has no stairs up. Clearing it shows "RENT COLLECTED". R then starts again from the first floor, empty-handed.

## Level design for floors

- One floor = one level scene in `levels/`, listed in order in `main.tscn` (Main's Floors).
- Each floor has stairs up (its exit), except the last. Floors above the first also have the stairs you arrive on, with the player placed at their top.
- Put the stairs up far from where you arrive, so the floor has to be crossed.
- Leave a gun near where you arrive, for a player who comes up empty-handed.
- Stairs are walked onto, not around: keep furniture off them and leave a free approach.
- Mix the goon behaviours: guards facing doorways, patrollers in corridors, wanderers in rooms.
- Give rooms more than one way in where you can, so there are routes around a guard.
- Doors follow [doors.md](doors.md); gaps for goons follow [goons.md](goons.md).

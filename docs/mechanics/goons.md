# Goons: how they react

How enemies notice you, shoot and react. Timing values are in the goon scene and are not listed here.

## Behaviours

Set per goon in the level:
- **Static:** stands guard facing one way. Reacts only to what it sees, never to gunshots. Returns to its post after a chase.
- **Patrol:** walks straight and turns when blocked. Hears gunshots.
- **Wander:** walks short random stretches, stops to look around, repeats. Hears gunshots.
- **Stay in room** (option, patrol and wander): treats doorways as walls when on its own. A chase can still take it through doors, but it walks back to its room afterwards.

## Seeing you

- Goons see in front of them within a range and a field of view.
- Walls and doors block sight (see [doors.md](doors.md)).
- Once alerted, a goon sees all around.
- A goon that sees you stops, turns to face you, and fires after a short reaction delay.
- The delay is longer when you are far away or behind the goon, plus a little randomness. This delay is the player's window to act.
- A goon only fires once his aim is roughly on you.

## Hearing

- Only player gunshots are heard.
- Patrolling and wandering goons inside the noise radius run to where the shot came from.
- Goons that are already chasing you ignore other noise.
- On arrival, they look around for a moment, then go back to their routine.

## Chasing and searching

- A goon that loses sight of you runs to the last place he saw or heard you.
- If you are not there, he searches briefly, then returns to his routine.
- Goons avoid getting stuck on corners: if they stop making progress while trying to walk, they turn.

## Being hit

- Any hit kills a goon, unless it is a thrown gun.
- A thrown gun knocks a goon down instead: he lies on the floor, squirms, and gets up after a while. How long he stays down is random each time.
- Bullets and thrown guns fly over a goon lying on the floor.
- A knocked-down goon drops his gun, and will go for the nearest gun he can see.
- Doors you push open can knock goons down. Doors goons push open do not (see [doors.md](doors.md)).
- A knocked-down goon can be finished off, and a standing goon right in front of you can be taken out with a stealth kill (see [triggers.md](triggers.md)). A stealth kill leaves no goo.

## Going for guns

A goon with no gun runs to the nearest gun he can see and picks it up.
Goons with guns never run out of ammo.

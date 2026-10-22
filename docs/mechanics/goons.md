# Goons: how they react

How enemies notice you, shoot and react. Timing values are in the goon scene and are not listed here.

## Behaviours

Set per goon in the level:
- **Static:** stands guard facing one way. Reacts only to what it sees, never to gunshots. Returns to its post after a chase.
- **Patrol:** walks straight. When blocked, stops and turns left a little at a time until the way is clear and it faces straight along the walls again. Hears gunshots.
- **Wander:** walks short stretches in random open directions, stops to look around, repeats. Hears gunshots.
- **Stay in room** (option, patrol and wander): treats doorways as walls when on its own. A chase can still take it through doors, but it walks back to its room afterwards.
- **Pose** (option, any behaviour): what he does while calm and standing still: smoke, phone, hide (crouched), sit, lean (back to a wall), talk. Only the look: he sees and reacts exactly as without it. Once alerted he drops it (a smoker flicks his cigarette away); it comes back when he gives up searching. A sitting goon can be placed on furniture (a couch): he gets up off it when alerted. A wanderer strikes his pose whenever he stops.

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

- A goon that loses sight of you runs to the last place he saw or heard you. Each goon picks a slightly different spot around it, so they don't pile up.
- If you are not there, he searches briefly, then returns to his routine.
- Goons route around walls and furniture with a little room to spare, so they never scrape corners. They go through doorways, pushing doors open.
- The edge of the map is a wall to goons: they never walk off it.
- A goon that stops making progress plans a new route; a patrolling or wandering one turns.

## Level design for goons

- Goons may not fit through a gap narrower than two floor tiles (32 px) between walls and furniture.
- Doorways are fine: doors don't count as obstacles.

## Being hit

- Any hit kills a goon, unless it is a punch or a thrown weapon (except a knife).
- A punch or a thrown gun or bat knocks a goon down instead: he lies on the floor, squirms, and gets up after a while. How long he stays down is random each time.
- Knocked into a wall, he sits slumped against it, dazed, instead of lying flat. Same rules otherwise; finishing him with fists is kicks to the head.
- A goon lying on the floor can still be shot or killed with a bat or knife swing. A thrown weapon slides past him.
- A knocked-down goon drops his gun, and will go for the nearest gun he can see.
- Doors you push open can knock goons down. Doors goons push open do not (see [doors.md](doors.md)).
- A goon you throw (from a human shield, see [triggers.md](triggers.md)) knocks down the first goon he slides into, and dies on a wall he hits fast (slumps against it if slower).
- A knocked-down goon can be finished off, and a standing goon right in front of you can be taken out with a stealth kill (see [triggers.md](triggers.md)). A stealth kill leaves no goo.
- **Body fall:** anyone knocked down (a punch, a thrown gun or bat, a door) hits the floor with a short thud (`audio/body_fall.tres`, a little different in pitch each time). A door slamming into him also plays its slam. A thrown knife lands with a meaty stab of its own (`audio/melee/knife_throw.wav`, the weapon's **Thrown Hit Sound**) as it kills him; a thrown bat plays its usual hit; a thrown gun has only his fall.

## Melee goons

- A goon holding a bat or knife, once he sees you and his reaction time has passed, runs at you (faster than a goon running to a noise) and swings when you're close. His swing kills you.
- He keeps coming as long as he sees you; if he loses you, he searches like any goon.
- Otherwise he behaves like his routine says (static, patrol, wander).

## Going for guns

A goon with no weapon runs to the nearest weapon he can see (gun, bat or knife) and picks it up. Goons never punch. He can reach a gun that landed where nobody fits, up to an arm's length away, but never one across a room.
Goons with guns never run out of ammo.

# Enemies: how they react

How enemies notice you, shoot and react. Timing values are in the enemy scene and are not listed here.

## Behaviours

Set per enemy in the level:
- **Static:** stands guard facing one way. Reacts only to what it sees, never to gunshots. Returns to its post after a chase.
- **Patrol:** walks straight. When blocked, stops and turns left a little at a time until the way is clear and it faces straight along the walls again. Hears gunshots.
- **Wander:** walks short stretches in random open directions, stops to look around, repeats. Hears gunshots.
- **Follow wall:** keeps a wall on its left, so it goes round whole rooms and out through doorways into the next. Round a pillar, or out in the open, it lets go after three quarters of a turn and walks straight on to the next wall. The dog's routine. Hears gunshots.
- **Stay in room** (option, any walking routine): treats doorways as walls when on its own. A chase can still take it through doors, but it walks back to its room afterwards.
- **Pose** (option, any behaviour): what he does while calm and standing still: smoke, phone, hide (crouched), sit, lean (back to a wall), talk. Only the look: he sees and reacts exactly as without it. Once alerted he drops it (a smoker flicks his cigarette away; a sitter stands up, a crouching enemy rises, a leaner pushes off the wall, each quick enough to finish inside his reaction time); it comes back when he gives up searching. A sitting enemy can be placed on furniture (a couch): he gets up off it when alerted, stepping out onto open floor before he goes anywhere. A wanderer strikes his pose whenever he stops.

## Seeing you

(temporary) Sight Range, Look Every, Notice Time and Stand Off are a first pass; they get balanced in [issue #11](https://github.com/T3mon/rent-day/issues/11).

- Enemies notice you all around them, but only within their **Sight Range** (temporary): a little short of how far the screen reaches to your side. Above and below you the screen ends sooner, so an enemy there can still see you just off screen. Sitting, smoking or on the phone makes no difference.
- Walls and doors block sight (see [doors.md](doors.md)).
- A calm enemy only looks around now and then (**Look Every**, half a second; not all enemies at the same moment; temporary). When he spots you, he carries on with what he was doing for a moment (**Notice Time**, temporary) before he reacts. Between his looks you can sneak up on him. Once he knows you're around (he saw you, heard a shot, or was knocked down), he watches all the time.
- An enemy that sees you stops, turns to face you, and fires after a short reaction delay.
- After that, an enemy with a gun walks at you while he keeps firing, until he's close (**Stand Off**, temporary), so he comes into view.
- The delay is longer when you are far away or behind the enemy, plus a little randomness. This delay is the player's window to act.
- An enemy only fires once his aim is roughly on you.

## Hearing

- Only player gunshots are heard.
- Of the patrolling and wandering enemies inside the noise radius, the three with the shortest walk to the shot come running; the rest carry on. Each heads for his own random spot around the shot (within **Noise Spread**, never behind a wall from it).
- Enemies that are already chasing you ignore other noise.
- On arrival, they look around for a moment, then go back to their routine.

## Chasing and searching

- An enemy that loses sight of you runs to the last place he saw or heard you. Each enemy picks his own spot around it (within **Search Spread**), so they don't pile up.
- If you are not there, he searches briefly, then returns to his routine.
- Enemies route around walls and furniture with a little room to spare, so they never scrape corners. They go through doorways, pushing doors open.
- The edge of the map is a wall to enemies: they never walk off it, and one thrown at it stops there.
- Enemies closer together than two bodies side by side push apart, so they never stand inside each other. Calm enemies in their poses are left where they were placed.
- An enemy whose body is in something (sitting in a couch, knocked against a wall) first steps straight out onto open floor, never through a wall, and only then sets off.
- An enemy that stops making progress plans a new route; a patrolling or wandering one turns.

## Level design for enemies

- Enemies may not fit through a gap narrower than two floor tiles (32 px) between walls and furniture.
- Doorways are fine: doors don't count as obstacles.

## Being hit

- Any hit kills an enemy, unless it is a punch or a thrown weapon (except a knife).
- A punch or a thrown gun or bat knocks an enemy down instead: he lies on the floor, squirms, and gets up after a while. How long he stays down is random each time.
- Knocked into a wall, he sits slumped against it, dazed, instead of lying flat. Same rules otherwise; finishing him with fists is kicks to the head.
- An enemy lying on the floor can still be shot or killed with a bat or knife swing. A thrown weapon slides past him.
- A knocked-down enemy drops his gun, and will go for the nearest gun he can see.
- Doors you push open can knock enemies down. Doors enemies push open do not (see [doors.md](doors.md)).
- An enemy you throw (from a human shield, see [triggers.md](triggers.md)) knocks down the first enemy he slides into, and dies on a wall he hits fast (slumps against it if slower).
- A knocked-down enemy can be finished off, and a standing enemy right in front of you can be taken out with a stealth kill (see [triggers.md](triggers.md)). A stealth kill leaves no goo.
- **Body fall:** anyone knocked down (a punch, a thrown gun or bat, a door) hits the floor with a short thud (`audio/body_fall.tres`, a little different in pitch each time). A door slamming into him also plays its slam. A thrown knife lands with a meaty stab of its own (`audio/melee/knife_throw.wav`, the weapon's **Thrown Hit Sound**) as it kills him; a thrown bat plays its usual hit; a thrown gun has only his fall.

## Melee enemies

- An enemy holding a bat or knife, once he sees you and his reaction time has passed, runs at you (faster than an enemy running to a noise) and swings when you're close. His swing kills you.
- He keeps coming as long as he sees you; if he loses you, he searches like any enemy.
- Otherwise he behaves like his routine says (static, patrol, wander).

## Dog

`enemies/dog.tscn`: a dog that sees and hears like anyone. Its art is drawn by `tools/anim/dog.py`.

- **Routine:** follows the wall, and every 7 seconds or so (**Sit Every**) sits down for about 3 (**Sit Time**). Set to Static, it sits at its post.
- **The warning:** when it spots you, it stops and barks (**Bark Time**, 0.4 s) before it charges. It barks again each time it finds you after losing you for a moment. Walking enemies within **Bark Noise** (240 px, as far as a pistol shot, through walls) hear the bark and come to the dog. Enemies standing still don't, as with shots.
- **The charge:** it speeds up straight at you (**Acceleration**) to faster than you can run or dash (**Run Speed**). It bounces off walls instead of going round them, so it can overshoot. If it loses you, it searches like anyone.
- **The bite:** running into you kills you. A dash goes through it, like through a bullet.
- **Killing it:** a bullet, a bat or a knife. Punches, door slams and thrown things do nothing, and it can't be grabbed or stealth-killed: it's the one enemy you can't take on bare-handed.
- It carries nothing, so it drops nothing.
- Sounds: a bark when it spots you (**Bark Sound**) and a snap of the jaws when it bites (**Bite Sound**). It dies silently.

## Heavy enemy

`enemies/heavy.tscn`: a big man with bare fists, far wider than anyone else, who sees and hears like anyone. His art is drawn by `tools/anim/heavy.py`.

- **Routine:** patrols (**Walk Speed** 45, slower than anyone).
- **Seeing you:** he grunts and walks straight at you (**Chase Speed** 100, slower than you), around walls. Once you're within **Swing Range** (24 px) he swings his fist (`weapons/heavy_fists.tres`, reach 16 px), and it kills you. A dash goes through the swing, like through a bullet.
- **Only bullets hurt him.** A bat, a knife, punches, door slams and thrown things do nothing, and he can't be grabbed or stealth-killed.
- **Bleeding out:** every bullet is a hit. At **Max Hits** (5) he dies at once. Before that he bleeds out, sooner the more hits he has, and keeps coming meanwhile: 1 hit about 6.4 s, 2 hits 4 s, 3 hits 2.3 s, 4 hits 1 s. Goo drips behind him while he bleeds, faster the more hits he has.
- A shotgun blast kills him at once (5 of its 7 pellets hit him even from 100 px away): the shotgun is the answer to him.
- Each bullet takes **Slow Per Hit** (12 px/s) off his chasing speed.
- He carries nothing, so he drops nothing.
- Sounds: none yet for his grunt (**Grunt Sound**); his swing uses the punch's whoosh and hit for now.

## Going for guns

An enemy with no weapon runs to the nearest weapon he can see (gun, bat or knife) and picks it up. Enemies never punch (the heavy enemy has only his fists). He can reach a gun that landed where nobody fits, up to an arm's length away, but never one across a room. He doesn't go for one behind a glass pane until the glass is shattered.
Enemies with guns never run out of ammo.

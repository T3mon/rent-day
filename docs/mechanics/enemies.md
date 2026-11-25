# Enemies: how they react

How enemies notice you, shoot and react. Timing values are in the enemy scene and are not listed here.

## Behaviours

Set per enemy in the level:
- **Static:** stands guard facing one way (**Facing**, in degrees clockwise: 0 right, 90 down, 180 left, 270 up, any angle between; the Inspector shows this beside the number; every enemy sets off facing this way). Reacts only to what it sees, never to gunshots. Returns to its post after a chase.
- **Patrol:** walks straight. When blocked, stops and turns left a little at a time until the way is clear and it faces straight along the walls again. Hears gunshots.
- **Wander:** walks short stretches in random open directions, stops to look around, repeats. Hears gunshots.
- **Follow wall:** keeps a wall on its left, so it goes round whole rooms and out through doorways into the next. Round a pillar, or out in the open, it lets go after three quarters of a turn and walks straight on to the next wall. The dog's routine. Hears gunshots.
- **Stay in room** (option, any walking routine): treats doorways as walls when on its own. A chase can still take it through doors, but it walks back to its room afterwards.
- **Pose** (option, any behaviour): what he does while calm and standing still: smoke, phone, hide (crouched), sit, lean (back to a wall), talk. The heavy enemy has his own: eat, phone, toilet (see [Heavy enemy](#heavy-enemy)); a pose an enemy has no animation for does nothing. Only the look: he sees and reacts exactly as without it. Once alerted he drops it (a smoker flicks his cigarette away; a sitter stands up, a crouching enemy rises, a leaner pushes off the wall, each quick enough to finish inside his reaction time); it comes back when he gives up searching. A sitting enemy can be placed on furniture (a couch): he gets up off it when alerted, stepping out onto open floor before he goes anywhere. A wanderer strikes his pose whenever he stops.

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

- **Speeds** (you run at 200 px/s): while he sees you, an enemy comes at you as fast as you (**Chase Speed** 200; with a bat or knife **Charge Speed** 200). Once he loses sight of you he is slower (**Search Speed** 133), so breaking his line of sight is how you get away. On patrol he walks (**Walk Speed** 55).
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
- A dodger on his feet slides out of bullets and thrown things he sees or hears coming (see [Dodger](#dodger)).
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
- **The charge:** it speeds up straight at you (**Acceleration**) to far faster than you can run (**Run Speed** 333). It bounces off walls instead of going round them, so it can overshoot. If it loses you, it searches like anyone.
- **The bite:** running into you kills you. A dash goes through it, like through a bullet.
- **Killing it:** a bullet, a bat or a knife. Punches, door slams and thrown things do nothing, and it can't be grabbed or stealth-killed: it's the one enemy you can't take on bare-handed.
- It carries nothing, so it drops nothing.
- Sounds: a bark when it spots you (**Bark Sound**) and a snap of the jaws when it bites (**Bite Sound**). It dies silently.

**Design choices:**
- It's the fast enemy: you can't outrun it, so you have to stand and deal with it, or dash through it.
- The bark is your warning and its weakness at once: a moment to react before the charge, but it calls the walking enemies over.
- It bounces off walls instead of steering round them, so luring it past a corner buys time.
- Fists, doors and throws do nothing, so a floor with a dog asks you to keep a gun, bat or knife.
- Following the walls makes it come past every door sooner or later, so no room is safe for long.

## Heavy enemy

`enemies/heavy.tscn`: a big man with bare fists, far wider than anyone else, who sees and hears like anyone. His art is drawn by `tools/anim/heavy.py`.

- **Routine:** patrols (**Walk Speed** 45, slower than anyone).
- **Poses** (set him to Static to see one): **Eat** (a burger: up to his mouth, a bite, chewing), **Phone** (a hand at his ear, the other waving about), **Toilet** (sitting with a newspaper, turning a page now and then; drawn with his legs). Toilet works like Sit: put him on `props/toilet.tscn` facing away from the tank (Facing 90 with the tank above him), and when alerted he drops the paper and gets up (`toilet_up`) inside his reaction time. Drawn in `tools/anim/heavy.py` (`poses()`).
- **Seeing you:** he walks straight at you (**Chase Speed** 200, as fast as you), around walls. Once you're within **Swing Range** (24 px) he swings his fist (`weapons/heavy_fists.tres`, reach 16 px), and it kills you. A dash goes through the swing, like through a bullet.
- **Only bullets hurt him.** He wears a bulletproof vest. A bat, a knife, punches, door slams and thrown things do nothing; blows and thrown things clang off the vest (**Clang Sound**, `audio/melee/heavy_clang.wav`). He can't be grabbed or stealth-killed.
- **Bleeding out:** every bullet is a hit. At **Max Hits** (5) he dies at once. Before that he bleeds out, sooner the more hits he has, and keeps coming meanwhile: 1 hit about 6.4 s, 2 hits 4 s, 3 hits 2.3 s, 4 hits 1 s. Goo drips behind him while he bleeds, faster the more hits he has.
- A shotgun blast kills him at once (5 of its 7 pellets hit him even from 100 px away): the shotgun is the answer to him.
- Each bullet takes **Slow Per Hit** (12 px/s) off his chasing speed.
- He carries nothing, so he drops nothing.
- Sounds: no alert sound. Each swing is a grunt (`audio/melee/heavy_grunt.tres`, five takes picked at random); the hit is the punch's. A bullet that doesn't kill him plays his hurt sound (**Hurt Sound**, `audio/heavy_hurt.wav`).

**Design choices:**
- He's the slow, tough enemy: the opposite of the dog. Easy to see coming, hard to put down.
- Only bullets work, so melee players have to pick up a gun for him.
- Bleeding out instead of a health bar: one bullet is enough if you can then keep away from him, more bullets end it sooner. Running and shooting both pay off.
- The shotgun kills him at once, so a shotgun on a floor with a heavy enemy is a gift.
- Bare fists and no gun, so he only threatens you up close, and you can always see his reach.
- The vest clang tells you straight away that a bat or punch was wasted.

## Shield enemy

`enemies/shield_enemy.tscn`: an ordinary enemy with a pistol behind a heavy black shield. He sees, hears, reacts and shoots like anyone. He wears the usual enemy clothes with a black helmet and a blue visor (the `riot` outfit in `tools/anim/outfits.py`); the shield is its own picture, drawn by `tools/anim/shield.py`.

- **The shield covers his front** (**Front**, 120°). From there bullets bounce off it, a little off true, and can kill you or other enemies; a bat, a knife, punches, thrown things, door slams and grabs do nothing to him. From his side or back he dies like anyone.
- **He turns slowly** (**Turn Speed** 150°/s, against everyone else's 540), and only fires once he's turned to you, so you can circle round him or dash past him.
- **A shotgun blast knocks him down**, even on the shield (its pellets still bounce off). Down, he can be finished off like anyone.
- **It opens up:** for **Peek Time** (0.35 s) after each of his shots he swings the shield aside to his left, and his front is open. A door slam or a thrown thing knocks it aside for **Lower Time** (1 s).
- A door slam on the shield shoves him back a little, a bat or knife a little less. Punches don't move him.
- **Spotting you** he bangs on his shield: a jab forward and a low clang.
- Lying down or dead, his shield lies beside him. Nobody can pick it up.
- Sounds: a clang when a blow hits the shield, and when he bangs on it (**Block Sound**, the heavy enemy's vest clang); a bullet's crack when one bounces off (**Ricochet Sound**, `audio/shield_ricochet.wav`).

**Design choices:**
- He's the enemy you have to go round: the puzzle is the angle, not the aim. His slow turn is what makes getting round him possible.
- He looks like the ordinary enemy on purpose, with only the helmet, visor and shield as the difference, so he reads as "the same enemy, armoured" at a glance.
- Ricochets instead of the shield just eating bullets: firing at his front is a real danger to you, and a chance to hit the enemies around him. Both versions were tried; this one stayed.
- Every way of hurting him has an answer on the shield, so the rule is simple: from the front, nothing works.
- The shotgun knocks him over, so it's the heavy weapon's answer to him, as it is to the heavy enemy.
- He lowers the shield to shoot, so a patient player can wait for his shot and fire into the gap.
- A thrown thing or a door knocks the shield aside, so fists-only players still have a way in.
- The shield stays with him when he dies (left for later in [issue #10](https://github.com/T3mon/rent-day/issues/10): could you pick it up?).

## Dodger

`enemies/dodger.tscn`: an ordinary enemy with a knife and a red headband, its two tails flying behind him. He sees, hears, reacts and charges like any enemy with a knife. He wears the usual enemy clothes (the `dodger` outfit in `tools/anim/outfits.py`, the band drawn on his hair); the tails are simulated per animation by `tools/anim/headband.py`, so they hang down when he stands, stream behind him when he runs and fly out when he slides.

- **On his feet, he slides out of what he sees or hears coming:** anything while he's onto you and you're in his sight, and anything for a second after a gunshot within earshot (the gun's noise range, whatever his routine; a silenced pistol makes none). One coming at him (a bullet about 80 px out, a thrown thing 0.12 s out), he slides aside and it flies on past, in slow motion too. An enemy you throw at him misses too.
- **Caught unaware, he's like anyone:** while he's calm and hasn't noticed you yet, a silenced shot, a throw, or a shot from beyond earshot kills him (a thrown gun or bat knocks him down). Once he's onto you, only when you're out of his sight, with no bang he could hear.
- **The slide** lasts **Dodge Time** (0.3 s) at up to **Dodge Speed** (225 px/s, your dash's), about 50 px: across the shot's path, angled **Dodge Angle** (35°) toward you, so each one brings him closer. A big feet-first slide along the floor in 13 frames, facing the same way throughout: he crouches low, pushes off and stretches long, drops with his near leg shot out ahead, slides on the floor leaning back on a hand dragging behind him with dust kicking up, brakes with his foot dug in, and pops back up. Afterimages trail him like your dash, with its whoosh (**Dodge Sound**, `audio/dash.tres`).
- **One slide covers a whole shotgun blast**, and he slides again as often as it takes, with no wait, to the other side each time if there's room. With no room either side (a corridor, a corner) he ducks on the spot.
- After a slide he knows you're around and comes to look where you are.
- **Up close he's like anyone, even mid-slide:** a bat or a knife kills him, a punch or a door slam knocks him down, and grabs, stealth kills and finishing off work as usual.
- **Knocked down, he can't slide:** a bullet or a thrown knife kills him then, like anyone.
- Disarmed, he only goes for a bat or a knife, never a gun.
- The slide's poses are `dash_spin_l`/`_r` (and the `gun_`, `bat_`, `knife_` ones) in `tools/anim/anims.py`, which copies Dodge Time, Dodge Speed, Dodge Angle and the slide's pace from `enemies/Dodger.cs`: change them in both.

**Design choices:**
- Once he's onto you, guns can't solve him: close in on him, or knock him down and then shoot.
- He dodges only what he sees or hears coming, as every enemy reacts only to what he sees or hears. So sneaking up pays off: a silenced shot, a throw or a long shot gets him before he knows you're there.
- Every slide brings him closer, so shooting at him only brings his knife to you sooner.
- One slide per blast, not one per pellet, so a shotgun blast reads as one clean miss.
- Blows still land mid-slide, so the slide is never a wall: there's always a way in.
- No limit on slides: a rule you can learn at once ("he dodges what he sees or hears coming") instead of a count to keep.
- He looks like the ordinary enemy on purpose, with the red headband and its flying tails as the one difference, so he reads as "the same enemy, but quick" at a glance.
- A knife, and only a bat or knife when disarmed: he's a melee fighter, and a dodger with a gun would be untouchable at range.

## Going for guns

An enemy with no weapon runs to the nearest weapon he can see (gun, bat or knife; a dodger only a bat or knife) and picks it up. Enemies never punch (the heavy enemy has only his fists). He can reach a gun that landed where nobody fits, up to an arm's length away, but never one across a room. He doesn't go for one behind a glass pane until the glass is shattered.
Enemies with guns never run out of ammo.

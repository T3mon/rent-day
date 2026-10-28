# Triggers, pick up and throw

What the buttons do. Control names refer to the Input Map in project settings.

## Attack (`shoot`, left mouse button)

What you hold decides: a gun shoots, a bat or knife swings, empty hands punch.

- **Single-shot guns:** one shot per click.
- **Automatic guns:** fire repeatedly while the button is held.
- Shooting is blocked while the gun's cooldown is still running.
- With an empty gun, the trigger plays a dry click instead.
- Shooting pushes you slightly backwards for guns that do this.

## Melee: fists, bat, knife

- Swings repeat while the button is held, each after a short wait. You can walk while swinging.
- A swing hits every enemy in a short box in front of you (about 35 px long, 22 px wide), once per swing, for as long as the swing lasts. A wall or door between you and him blocks the hit.
- Everything is held on the right: a bat back over the right shoulder, a knife in the right hand, the right fist jabs. After each swing it returns to the same place.
- **Fists:** knock an enemy down; he drops his gun. Finish him with Execute. Punching an enemy already on the floor does nothing.
- **Bat:** kills. Slower swing, longer wait.
- **Knife:** kills. The fastest swing.
- A bat or knife swing kills an enemy lying on the floor instantly.
- Melee makes no noise: nobody hears it.
- Bats and knives are picked up and thrown like guns. A thrown bat knocks an enemy down; a thrown knife kills him.

## Grab (`grab`, right mouse button)

One button does two jobs, depending on what is under your feet:

1. **A weapon lies within reach:** you pick it up. The one you were holding is thrown toward the mouse, with its remaining ammo, like any throw.
2. **Nothing within reach, and you hold one:** you throw it toward the mouse. A thrown weapon can knock an enemy down; a thrown knife kills (see [enemies.md](enemies.md)).

Throwing does nothing if you hold nothing.

## Dropping

- Dying drops your gun on the floor near you.
- Enemies drop their gun when they die or get knocked down.
- Dropped guns keep their remaining ammo.

## Swapping

Picking up a gun while holding one swaps them: the held gun is thrown toward the mouse, so it can knock down (a knife: kill) an enemy in the way.

## Pickup sound

Each gun has its own pick-up sound, so you can tell what you grabbed by ear.

## Look (`look`, Shift)

- The camera always leans a little toward the cursor, so you see more of where you aim.
- Holding Shift leans much further, to scout ahead. You always stay on screen, near the edge at most.
- The lean follows the cursor's spot on the screen, so it eases in and out smoothly instead of running away.

## Slow motion (`slow_motion`, Alt)

- Holding Alt slows the whole game down, you as much as the enemies, while a meter drains (a full meter lasts 4 s of real time).
- Let go, or run the meter dry, and the game is back to normal speed. Running dry, it starts again only on a fresh press.
- The meter refills on its own, only over time: half a second after you let go, then 8 s from empty to full. It takes a little in the meter to start (red while there's too little).
- The meter is a battery in the top-left corner. It shows only while it isn't full.
- Game sounds (shots, hits, doors, steps) slow down with the game. The music keeps its speed.
- The look: the world washes a little toward blue, with soft dark edges, faint scanlines and red and cyan fringes at the edges, like a worn tape. It comes in over a quarter of a second, and goes the same way. You and the bullets keep your own colours.
- Moving, you leave afterimages behind you: neon silhouettes fading from cyan to magenta.
- Bullets fly with a warm tracer behind them.
- Kills don't freeze the game for a split second while it's slowed (hit stop): it's slow already.
- Pausing, dying, a restart or the stairs end it. Numbers are tuned on the player (`player/player.tscn`).

## Admin panel (`admin_panel`, F1)

Testing switches and tools, in runs from the editor only: see [testing.md](testing.md).

## Pause (`ui_cancel`, Esc)

Opens the Esc menu over the frozen floor: resume, save game, load game, settings, quit to the main menu. The game stays paused until it closes; Esc goes back one page, and from the first page resumes.

## Quick save and load (`quick_save` F5, `quick_load` F9)

F5 saves to the one quick save; F9 loads it. See [saves.md](saves.md).

## Next song (`next_song`, N)

Skips to the next floor song, without pausing. See [music.md](music.md).

## Execute (`execute`, Space)

One key, two takedowns. An enemy standing right in front of you comes first; otherwise the nearest enemy lying on the floor within reach.

**Stealth kill** (an enemy standing right in front of you, whichever way he faces):
- You grab him and his gun flies out of his hands. He struggles for a moment, weaker and weaker, then goes limp.
- No clicks needed.
- Non-lethal style: no goo and no blood, he just slumps face down. He still counts as taken out.

**Finishing a downed enemy:** you drop onto him and crouch over him. What happens depends on what you hold:
- **Fists, or a gun** (dropped beside you first): every click is one blow. A random number of blows is needed (2 to 5); clicks while a blow is still landing don't count. Each blow cracks harder than the last, ending on the hardest crack right before the killing blow, which is a much bigger crush with the full splat.
- An enemy slumped against a wall: you step up to him instead of crouching on him, and with fists (or a gun) every blow is a kick to the head. He dies slumped over.
- **Bat:** every click swings the bat down on him. Always 3 blows. You keep the bat.
- **Knife:** no clicks. A moment on him, then one cut across the throat, blood squirting to the side. You keep the knife.
- Small goo splats build up under him with every blow.

**Both:**
- A gun in your hand is dropped beside you first (a bat or knife too, for a stealth kill).
- You can't move or shoot until it's over, and other enemies can still shoot you.
- The enemy can't act or get up while you're on him.

## Human shield (`shield`, E)

Pick up the nearest enemy lying on the floor within reach and hold him in front of you. Works with anything in your hand. Picking him up takes a quarter second: you reach down and pull him up as he gets to his feet, and you stand still meanwhile.

- **Holding him:** you move at 2/3 speed and turn slowly (at most a full turn a second), his legs walking in front of yours.
- **Attacking:** any gun still fires, from out past his right shoulder. A bat, knife or fists can't: you just carry him. Picking up, throwing and Execute are off while you hold him.
- **Bullets:** he takes enemy bullets coming from the front 120°, jolting and bleeding. After 5 he dies and drops, and you carry on. A shotgun blast kills him at once (and he takes the whole blast). Bullets from the side or behind still hit you, and he goes down with you.
- **Throwing him (right-click):** you shove him the way you face and he flies like a hard knockdown (about 7 tiles). The first standing enemy he slides into goes down too, and he stops there: both are knocked down, alive. Thrown hard into a wall, he dies on it: flat against it with his arms flung out, a crash, goo splashing back off the wall, then he slides down it into a crumpled heap. Slowed down by then, he just slumps against it, alive, with a softer crash. Only the thrown body knocks enemies over, never the enemies he knocks down.
- **Letting go (E again):** barely shot (0 to 2 hits), you snap his neck: you stand still for 0.3 s, then a crack and he drops dead. Shot 3 or more times, you just drop his body.
- Tuning is on the Player: Shield Hits, Shield Drop After, Shield Speed, Shield Turn, Shield Front, Snap Time, Lift Time, Shield Throw Speed. How fast a thrown enemy must be to die on a wall or bowl an enemy over is on the enemy (Splat Speed, Bowl Speed).

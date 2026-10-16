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
- A swing hits every goon in a short box in front of you (about 35 px long, 22 px wide), once per swing, for as long as the swing lasts. A wall or door between you and him blocks the hit.
- Everything is held on the right: a bat back over the right shoulder, a knife in the right hand, the right fist jabs. After each swing it returns to the same place.
- **Fists:** knock a goon down; he drops his gun. Finish him with Execute. Punching a goon already on the floor does nothing.
- **Bat:** kills. Slower swing, longer wait.
- **Knife:** kills. The fastest swing.
- A bat or knife swing kills a goon lying on the floor instantly.
- Melee makes no noise: nobody hears it.
- Bats and knives are picked up and thrown like guns. A thrown bat knocks a goon down; a thrown knife kills him.

## Grab (`grab`, right mouse button)

One button does two jobs, depending on what is under your feet:

1. **A weapon lies within reach:** you pick it up. The one you were holding drops where you stand, with its remaining ammo.
2. **Nothing within reach, and you hold one:** you throw it toward the mouse. A thrown weapon can knock a goon down; a thrown knife kills (see [goons.md](goons.md)).

Throwing does nothing if you hold nothing.

## Dropping

- Dying drops your gun on the floor near you.
- Goons drop their gun when they die or get knocked down.
- Dropped guns keep their remaining ammo.

## Swapping

Picking up a gun while holding one swaps them: the held gun lands at your feet.

## Pickup sound

Each gun has its own pick-up sound, so you can tell what you grabbed by ear.

## Look (`look`, Shift)

- The camera always leans a little toward the cursor, so you see more of where you aim.
- Holding Shift leans much further, to scout ahead. You always stay on screen, near the edge at most.
- The lean follows the cursor's spot on the screen, so it eases in and out smoothly instead of running away.

## Execute (`execute`, Space)

One key, two takedowns. A goon standing right in front of you comes first; otherwise the nearest goon lying on the floor within reach.

**Stealth kill** (a goon standing right in front of you, whichever way he faces):
- You grab him and his gun flies out of his hands. He struggles for a moment, weaker and weaker, then goes limp.
- No clicks needed.
- Non-lethal style: no goo and no blood, he just slumps face down. He still counts as taken out.

**Finishing a downed goon:** you drop onto him and crouch over him. What happens depends on what you hold:
- **Fists, or a gun** (dropped beside you first): every click is one blow. A random number of blows is needed (2 to 5); clicks while a blow is still landing don't count. Each blow cracks harder than the last, ending on the hardest crack right before the killing blow, which is a much bigger crush with the full splat.
- **Bat:** every click swings the bat down on him. Always 3 blows. You keep the bat.
- **Knife:** no clicks. A moment on him, then one cut across the throat, blood squirting to the side. You keep the knife.
- Small goo splats build up under him with every blow.

**Both:**
- A gun in your hand is dropped beside you first (a bat or knife too, for a stealth kill).
- You can't move or shoot until it's over, and other goons can still shoot you.
- The goon can't act or get up while you're on him.

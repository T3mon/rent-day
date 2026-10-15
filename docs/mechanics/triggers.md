# Triggers, pick up and throw

What the buttons do. Control names refer to the Input Map in project settings.

## Shoot (`shoot`, left mouse button)

- **Single-shot guns:** one shot per click.
- **Automatic guns:** fire repeatedly while the button is held.
- Shooting is blocked while holding no gun, and while the gun's cooldown is still running.
- With an empty gun, the trigger plays a dry click instead.
- Shooting pushes you slightly backwards for guns that do this.

## Grab (`grab`, right mouse button)

One button does two jobs, depending on what is under your feet:

1. **A gun lies within reach:** you pick it up. The gun you were holding drops where you stand, with its remaining ammo.
2. **No gun within reach, and you hold one:** you throw it toward the mouse. A thrown gun can knock a goon down (see [goons.md](goons.md)).

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

**Finishing a downed goon:**
- You drop onto him and crouch over him.
- Every click is one blow. A random number of blows is needed; clicks while a blow is still landing don't count.
- Each blow cracks harder than the last, ending on the hardest crack right before the killing blow, which is a much bigger crush with the full splat.
- Small goo splats build up under him with every blow.

**Both:**
- A gun in your hand is dropped beside you first.
- You can't move or shoot until it's over, and other goons can still shoot you.
- The goon can't act or get up while you're on him.

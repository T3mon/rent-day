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

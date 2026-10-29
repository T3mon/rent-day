# Shooting

How guns work in Rent Day. Tuning values live in the gun files and are not listed here.

## Guns are data

Each gun is one data file (`weapons/*.tres`) with its own look, sounds, ammo and feel.
Adding a gun means adding a file, not writing code.

Settings a gun controls:
- **Ammo:** a fixed number of rounds, no reloading. An empty gun clicks and becomes a throwing weapon.
- **Trigger mode:** single shot per click, or automatic while the button is held.
- **Pellets:** one bullet per shot, or several spread in a cone.
- **Spread:** random angle on each bullet.
- **Speed and range:** each bullet has its own speed (with optional variation) and a maximum distance.
- **Cooldown:** minimum time between shots, separate values for the player and for enemies.
- **Feel:** screen shake (in pixels, your shots only), recoil push-back on the shooter, sound pitch.
- **Noise:** how far a shot can be heard.

## Hitting things

- Bullets fly straight and stop at the first thing they hit.
- Each bullet checks the whole stretch it flies every frame, so a fast bullet can't skip over a thin door.
- Walls and doors stop bullets. Furniture does not. Bullets also push doors (see [doors.md](doors.md)).
- **Glass panes** (`props/glass_partition.tscn`): nobody walks through one, but bullets and sight pass.
  Any bullet (yours or an enemy's) shatters it and flies on; so does a gun flying into it (thrown or
  dropped), and a door swung into it. Its 24 shards fly out of the far side, spinning and slowing
  down, stop at walls, and stay on the floor for good. The empty frame still blocks walking. One of
  two shatter sounds plays (`audio/glass_shatter.tres`, picked at random), never two at once; enemies
  hear only the gunshot, not the glass. Windows in outer walls are part of the wall and never break.
- Bullets fly over dead bodies, but a knocked-down enemy can still be shot.
- A shot is checked from the shooter's center to the muzzle, so a barrel poking through a wall
  can't shoot through it: the shot hits the wall (or whoever stands in between) right there.
- Bullets damage characters on the enemy side only: the player's bullets hit enemies, enemy bullets hit the player. Enemies never hit each other.
- Any hit on a character kills it in one shot.

## Feedback

- A muzzle flash, smoke puff and sound on every shot.
- An empty gun plays a dry click and does not fire.
- Killing a character gives a hit stop, a screen shake and goo (see below).
- **Screen shake:** a hit sets how many pixels the picture may jump; it fades in a straight line
  (10 px a second, the camera's Fade), and a smaller hit never cuts a bigger shake short. Every hit
  shows: 1 px for an uzi shot, a punch or a grab, 1.7 for a shotgun, 2.3 for a kill, 3.3 for your own
  death (the biggest).
- The ammo counter in the corner shows rounds left, or "empty".

## Goo

- A kill sprays drops the way the shot went. They slow down, stop at walls and land as small splats.
- A big splat lands just past the body, with a smear the way it slid, and a pool spreads under it for a few seconds.
- Goo stays on the floor it lands on: it can flow through an open doorway in front of it, never through or around a wall, or under a glass pane (whole or broken). Flying goo splashes against whole glass, and flies on through a broken pane's empty frame. Walls and furniture are drawn over it.
- Each blow of an execution leaves a small splat and a few drops. Stealth kills leave none.
- Walking through goo leaves footprints for a few steps, fading out. Enemies leave them too.
- Every splat gets its own fixed mix of two colours. Nothing cycles or changes colour over time.
- Goo stays until the floor restarts.

## Noise

- Player shots make noise. Enemies' shots do not.
- Noise has a radius and goes through walls.
- Enemies inside the radius may react (see [enemies.md](enemies.md)).
- Different guns have different radii.

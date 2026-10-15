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
- **Cooldown:** minimum time between shots, separate values for the player and for goons.
- **Feel:** screen shake, recoil push-back on the shooter, sound pitch.
- **Noise:** how far a shot can be heard.

## Hitting things

- Bullets fly straight and stop at the first thing they hit.
- Each bullet checks the whole stretch it flies every frame, so a fast bullet can't skip over a thin door.
- Walls and doors stop bullets. Furniture does not. Bullets also push doors (see [doors.md](doors.md)).
- Bullets fly over anyone lying on the floor, dead or knocked down.
- A shot is checked from the shooter's center to the muzzle, so a barrel poking through a wall
  can't shoot through it: the shot hits the wall (or whoever stands in between) right there.
- Bullets damage characters on the enemy side only: the player's bullets hit goons, goon bullets hit the player. Goons never hit each other.
- Any hit on a character kills it in one shot.

## Feedback

- A muzzle flash, smoke puff and sound on every shot.
- An empty gun plays a dry click and does not fire.
- Killing a character gives a hit stop, a screen shake and a goo splat.
- The ammo counter in the corner shows rounds left, or "empty".

## Noise

- Player shots make noise. Goons' shots do not.
- Noise has a radius and goes through walls.
- Goons inside the radius may react (see [goons.md](goons.md)).
- Different guns have different radii.

"""The dodger's red headband. The band is drawn into his sprites (outfits.py); its two tails are drawn here, frame by
frame (anims.render): standing still they hang down his back, moving they fly out behind his head and flutter, and
they lag behind when he turns. Their shape in each frame is simulated through the whole animation, from where his
head goes in it and how the body moves through the world meanwhile (anims.motion_for).
"""
import math
from puppet import Layer, capsule, rot, head_blob, C

RED, RED_DARK = (214, 38, 50, 255), (138, 22, 34, 255)
TAILS = ((8.0, 0.3), (6.0, -0.4))  # each tail: its length (pixels) and its angle off straight back (radians)
POINTS = 5                         # along each tail, the knot first
HANG = 0.45                        # hanging down his back, this much of a tail's length shows from above
LIFT_SPEED = 160.0                 # the knot moving this fast (pixels per second), the tails fly straight out
STEP = 0.002                       # seconds per simulation step
DAMP = 0.975                       # each step, the tails keep this much of their speed (air)
PULL = 700.0                       # how hard a hanging tail is pulled back to lie down his back
FLUTTER = (1.2, 11.0)              # flying out, the tips wave this far (pixels) this many times a second


HAIR = ((59, 46, 40), (90, 70, 60))  # enemy_a's, which the dodger is made from


def worn(ch):
    return any(c[:3] == RED_DARK[:3] for c in ch.head.values())


def ring(layer):
    """The band round the head (a turned head's layer, puppet.head): the hair's edge, against the outline or the face."""
    hair = lambda k: k in layer.px and layer.px[k][:3] in HAIR
    edge = [(x, y) for (x, y) in layer.px if hair((x, y))
            and not all(hair((x + dx, y + dy)) or layer.px.get((x + dx, y + dy), RED)[:3] == RED_DARK[:3]
                        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))]
    for k in edge:
        layer.px[k] = RED
    return layer


def knot(ch, pose):
    """The knot (from the frame's centre) in this pose, and the way straight back from his head."""
    _, _, mx, my = head_blob(ch)
    if not hasattr(ch, '_knot'):
        dark = [k for k, c in ch.head.items() if c[:3] == RED_DARK[:3]]
        ch._knot = (sum(x for x, y in dark) / len(dark) - mx, sum(y for x, y in dark) / len(dark) - my)
    turn = pose.get('turn', 0.0)
    hx, hy = pose.get('head', (0.0, 0.0))
    kx, ky = rot(ch._knot, turn)
    return (mx + kx + hx, my + ky + hy), math.pi + turn


def hanging(ch, pose):
    """The tails at rest in this pose: lying down his back, spread a little."""
    (kx, ky), back = knot(ch, pose)
    return [[(kx + math.cos(back + a) * length * HANG * i / (POINTS - 1),
              ky + math.sin(back + a) * length * HANG * i / (POINTS - 1)) for i in range(POINTS)] for length, a in TAILS]


def draw_tails(canvas, ch, pose):
    tails = pose.get('tails') or hanging(ch, pose)
    layer = Layer()
    for points in tails:
        for a, b in zip(points, points[1:]):
            capsule(layer, (C + a[0], C + a[1]), (C + b[0], C + b[1]), 0.5, RED)
    layer.stamp_onto(canvas)


def simulate(ch, frames, motion=None, loop=False):
    """The tails' points (frame coordinates) for each frame of an animation, [(pose, ms)], each taken halfway through
    its frame. motion(t) -> ((x, y), angle): where the body is in the world t seconds in, and which way it faces;
    None = standing still. A looping animation runs twice, and the second time round is kept."""
    motion = motion or (lambda t: ((0.0, 0.0), 0.0))
    starts, total = [], 0.0
    for _, ms in frames:
        starts.append(total)
        total += ms / 1000
    middles = [s + ms / 2000 for s, (_, ms) in zip(starts, frames)]

    def at(t):  # the knot and the way back from the head, in the world, t seconds in
        local = t % total if loop else min(t, total - 1e-9)
        k = max(i for i, s in enumerate(starts) if s <= local + 1e-9)
        (kx, ky), back = knot(ch, frames[k][0])
        (bx, by), angle = motion(t)
        x, y = rot((kx, ky), angle)
        return (bx + x, by + y), back + angle

    (kx, ky), back = at(0.0)
    tails = [[(kx + math.cos(back + a) * L * HANG * i / (POINTS - 1), ky + math.sin(back + a) * L * HANG * i / (POINTS - 1))
              for i in range(POINTS)] for L, a in TAILS]
    before = [list(points) for points in tails]
    last, speed = (kx, ky), 0.0
    out, wanted = [None] * len(frames), [(t + (total if loop else 0.0), i) for i, t in enumerate(middles)]
    t, end = 0.0, (2 * total if loop else total)
    while t <= end + 1e-9 and wanted:
        (kx, ky), back = at(t)
        speed += (math.dist((kx, ky), last) / STEP - speed) * min(1.0, STEP / 0.04)
        last = (kx, ky)
        lift = min(1.0, speed / LIFT_SPEED)
        for (L, a), points, old in zip(TAILS, tails, before):
            seg = L * (HANG + (1 - HANG) * lift) / (POINTS - 1)
            new = [(kx, ky)]
            for i in range(1, POINTS):
                (x, y), (px, py) = points[i], old[i]
                rx = kx + math.cos(back + a) * seg * i
                ry = ky + math.sin(back + a) * seg * i
                pull = PULL * (1 - 0.75 * lift)  # flying out, still a little apart
                new.append((x + (x - px) * DAMP + (rx - x) * pull * STEP * STEP,
                            y + (y - py) * DAMP + (ry - y) * pull * STEP * STEP))
            for _ in range(3):  # each point its segment's length from the one before it, the knot held
                for i in range(1, POINTS):
                    (ax, ay), (x, y) = new[i - 1], new[i]
                    d = math.dist((ax, ay), (x, y)) or 1e-6
                    new[i] = (ax + (x - ax) * seg / d, ay + (y - ay) * seg / d)
            old[:] = points
            points[:] = new
        while wanted and t >= wanted[0][0] - 1e-9:
            i = wanted.pop(0)[1]
            (bx, by), angle = motion(t)
            out[i] = [wave([rot((x - bx, y - by), -angle) for x, y in points], lift, t, j * math.pi)
                      for j, points in enumerate(tails)]
        t += STEP
    return out


def wave(points, lift, t, phase):
    """Flying out, a ripple runs down each tail toward its tip (the two out of step, so they flap apart)."""
    amp, hz = FLUTTER
    out = [points[0]]
    for i in range(1, len(points)):
        (ax, ay), (x, y) = points[i - 1], points[i]
        d = math.dist((ax, ay), (x, y)) or 1e-6
        w = amp * lift * (i / (len(points) - 1)) * math.sin(2 * math.pi * hz * t - 1.4 * i + phase)
        out.append((x - (y - ay) / d * w, y + (x - ax) / d * w))
    return out


def add_tails(ch, animations, motion_for, loops):
    """Every frame of every animation ({name: [(pose, ms)]}) gets its tails simulated in."""
    for name, frames in animations.items():
        tails = simulate(ch, frames, motion_for(name), name in loops)
        animations[name] = [({**pose, 'tails': t}, ms) for (pose, ms), t in zip(frames, tails)]


def lying_band(head_x):
    """A lying body's head (anims.lying, a ball at head_x): the tails spread on the floor (under the head) and the
    band across it (over it)."""
    band, tails = Layer(), Layer()
    x = C + head_x
    capsule(band, (x, C - 3.5), (x, C + 3.5), 0.5, RED)
    for side in (1, -1):
        capsule(tails, (x - 1, C + 4 * side), (x - 4, C + 8.5 * side), 0.5, RED)
    return tails, band

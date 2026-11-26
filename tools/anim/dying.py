"""The player's dying animations (die_*): what his body does under the death screen. Each runs along the hit (+x, as
the lying layer is turned), ends on a held pose. No blood is drawn in: the game paints goo along the slide (each animation's slide per frame
goes in the SpriteFrames as metadata slide_<name>, see build.py).

- die_kick: blown back off his feet, he skids on his back, kicks twice, a hand twitches.
- die_crawl: face down, he drags himself on three times, reaches once more and stops.
- die_one_arm: on his back, a hand on his gut, he hauls himself along with the other arm; his face stays up.

Frames are 104 x 104 (the lying layer's 72 doesn't hold the slide), centred where the body lies when it falls.
Poses are keys; numbers snap between them (most of a move at once, then settling: jerk), a frame every STEP ms.
Twitches are 40 ms keys back and forth (shudder), with holds of uneven length between moves.
"""
import math
from puppet import blank, SIZE, C, read_png, PROJECT
import anims as an

N, M = 104, 52            # frame size, its middle
STEP = 40                 # ms per in-between
SCUFF = (70, 64, 92, 255)
CLEAR = (0, 0, 0, 0)
PINK = {(232, 80, 140), (168, 50, 106)}


def canvas():
    return [[CLEAR] * N for _ in range(N)]


def put(out, frame, ang=0.0, dx=0.0, dy=0.0):
    """Draws a 72 x 72 frame into a 104 one, turned about its middle and moved (nearest pixel, so it stays crisp)."""
    ca, sa = math.cos(-ang), math.sin(-ang)
    for y in range(N):
        for x in range(N):
            u, v = x + 0.5 - M - dx, y + 0.5 - M - dy
            X, Y = math.floor(u * ca - v * sa + C), math.floor(u * sa + v * ca + C)
            if 0 <= X < SIZE and 0 <= Y < SIZE and frame[Y][X][3]:
                out[y][x] = frame[Y][X]


def streak(out, x0, x1, width, y, colour):
    """Along the floor from x0 (where it began: thin) to x1 (fresh: wide), x from the middle."""
    lo, hi = sorted((x0, x1))
    if hi - lo < 1:
        return
    for yy in range(N):
        for xx in range(N):
            u, v = xx + 0.5 - M, yy + 0.5 - M - y
            if lo <= u <= hi:
                w = width * (0.55 + 0.45 * abs(u - x0) / (hi - lo)) + 0.5 * math.sin(u * 0.9)
                if abs(v) <= w:
                    out[yy][xx] = colour


def lerp(a, b, t):
    if isinstance(a, tuple):
        return tuple(lerp(x, y, t) for x, y in zip(a, b))
    if isinstance(a, (int, float)) and not isinstance(a, bool) and isinstance(b, (int, float)):
        return a + (b - a) * t
    return a if t < 0.5 else b


def jerk(t, b):
    """Most of a move in its first frame or two, then settling: a body pulled by spasms, not gliding."""
    return 1 - (1 - t) ** 3


def shudder(a, b, times, ms=STEP):
    """Keys flicking between two small changes, `times` flicks, ending on b."""
    return [(a if i % 2 == 0 else b, ms) for i in range(times)] + [(b, ms)]


def tween(keys, draw, default, ease, along):
    """keys: (changes, ms the move into them takes); each key keeps what the one before set, and {} holds.
    The first key is held for its ms; the last pose is where the animation stops.
    Returns (frames, ms, how far along the hit the body is on each frame: along(state))."""
    states, s = [], dict(default)
    for change, ms in keys:
        s = {**s, **change}
        states.append((s, ms))
    frames, ms_out, slide = [draw(states[0][0])], [states[0][1]], [along(states[0][0])]
    for (a, _), (b, ms) in zip(states, states[1:]):
        n = max(1, round(ms / STEP))
        for j in range(1, n + 1):  # ends on b
            now = {k: lerp(a[k], b[k], ease(j / n, b)) for k in a}
            f = draw(now)
            if f == frames[-1]:  # a hold, or a move too small to show: the frame before just lasts longer
                ms_out[-1] += round(ms / n)
            else:
                frames.append(f)
                ms_out.append(round(ms / n))
                slide.append(round(along(now), 1))
    return frames, ms_out, slide


# ---------------------------------------------------------------- die_kick and die_crawl: the puppet's lying body
PUPPET = dict(pose='hit', x=0.0, spread=5.0, arms=16.0, head=14.0, kick=0.0, hand=(10.0, 11.0), other=(8.0, -12.0),
              trail=None, trail_from=0.0)


def puppet_draw(ch, flip):
    def draw(s):
        out, x = canvas(), s['x']
        if s['trail'] == 'scuff':
            for y in (-4, 4):
                streak(out, s['trail_from'], x + 8, 0.6, y, SCUFF)
        if s['pose'] == 'hit':  # rocked back on his feet
            put(out, an.render(ch, dict(body=(-2, 0), head=(-3, 0), rh=(-4, 13), lh=(-4, -13), shear=1)), 0, x)
        elif s['pose'] == 'back':  # flat on his back, head the way he was thrown (-x)
            sp, k = s['spread'], s['kick']
            legs = ((-8, 3.5, -19 - 3 * k, sp - (sp - 5) * k), (-8, -3.5, -19, -sp))
            put(out, an.lying(ch, torso_len=16, head_x=round(s['head']), legs=legs,
                              hands=((s['arms'], 14), (s['arms'], -14))), math.pi, x)
        else:  # face down, a hand reaching
            put(out, an.lying(ch, torso_len=15, head_x=11, legs=((-7, 3.5, -19, 6), (-7, -3.5, -18, -2)), back=False,
                              hands=(s['hand'], s['other'])), 0, x)
        return [row[::-1] for row in out[::-1]] if flip else out
    return draw


def die_kick(ch):
    # Thrown back along -x, then the whole thing is turned round, so he goes the way the hit went (+x).
    keys = [({'pose': 'hit'}, 80),
            ({'pose': 'back', 'x': -4, 'trail': 'scuff', 'trail_from': 8}, 280),
            ({'x': -21, 'spread': 11, 'arms': 14, 'head': 12}, 200)]
    keys += shudder({'x': -20, 'head': 13}, {'x': -21, 'head': 12}, 3)          # the skid shakes him to a stop
    keys += [({}, 240),
             ({'kick': 1}, 40), ({'kick': 0}, 120),  # a kick, a pause, two quick ones
             ({}, 360), ({'kick': 1}, 40), ({'kick': 0.3}, 40), ({'kick': 1}, 40), ({'kick': 0}, 80)]
    keys += shudder({'arms': 11, 'head': 13}, {'arms': 15, 'head': 12}, 5)      # the hands and head jitter
    keys += [({'arms': 14}, 520),
             ({'kick': 0.5}, 40), ({'kick': 0}, 600),               # one last leg jerk
             ({'arms': 12}, 40), ({'arms': 14}, 700), ({}, 1000)]
    return tween(keys, puppet_draw(ch, flip=True), PUPPET, jerk, lambda s: -s['x'])  # turned round: -x is along the hit


def die_crawl(ch):
    keys = [({'pose': 'front', 'hand': (10, 11), 'other': (9, -12),
             }, 200)]
    keys += shudder({'x': 1}, {'x': 0}, 3)  # shaking where he landed
    # three pulls, each weaker and slower to come: (how far the hand reaches, how far he gets, the wait before)
    x = 0
    for reach, gain, wait in ((22, 7, 160), (19, 5, 420), (17, 3, 700)):
        keys += [({}, wait),
                 ({'hand': (reach, 8), 'other': (11, -11)}, 80),                          # the hand shoots out
                 ({'hand': (reach - 1, 9)}, 40), ({'hand': (reach, 8)}, 120),               # grips, trembling
                 ({'hand': (reach - 6, 10), 'other': (20, -8), 'x': x + gain * 0.7}, 80),  # the pull: a lurch
                 ({'hand': (10, 11), 'other': (10, -12), 'x': x + gain}, 160)]
        x += gain
    keys += [({}, 500), ({'hand': (21, 6)}, 80), ({'hand': (20, 7)}, 40), ({'hand': (21, 6)}, 300),  # one more reach
             ({'hand': (16, 10)}, 80)]
    keys += shudder({'hand': (15, 11), 'x': x + 0.6}, {'hand': (16, 10), 'x': x}, 5)  # the last shakes
    keys += [({}, 700), ({'hand': (15, 11)}, 40), ({'hand': (16, 10)}, 600), ({}, 1000)]
    return tween(keys, puppet_draw(ch, flip=False), PUPPET, jerk, lambda s: s['x'])


# ---------------------------------------------------------------- die_one_arm: his own death sprite, cut at the joints
def cut(ch, kind, parts):
    """The hand-drawn sprite art/anim/<name>/<name>_<kind>.png split into parts: [(name, pivot, test(x, y, rgb))].
    A pixel goes to the first part that wants it; the rest is the torso."""
    w, h, px = read_png(f'{PROJECT}art/anim/{ch.name}/{ch.name}_{kind}.png')
    out = {name: ({}, pivot) for name, pivot, _ in parts}
    out['torso'] = ({}, (28, 21))
    for y in range(h):
        for x in range(w):
            c = px[y][x]
            if c[3]:
                name = next((n for n, _, test in parts if test(x, y, c[:3])), 'torso')
                out[name][0][(x, y)] = c
    return out


def on_back(ch):  # the lie_dead pose on his back, face up
    return cut(ch, 'dead', [
        ('head', (37, 20.5), lambda x, y, c: (x >= 38 and 15 <= y <= 26) or (x == 37 and y in (20, 21))),
        ('arm_a', (35, 12), lambda x, y, c: y <= 11 and x >= 30),
        ('arm_b', (36, 29), lambda x, y, c: (y >= 29 and x >= 33) or (y == 28 and x >= 37)),
        ('leg_a', (20, 15), lambda x, y, c: x <= 19 and y <= 19),
        ('leg_b', (20, 26), lambda x, y, c: x <= 19 and y >= 20)])


def stamp(out, px, pivot, rot, sx, sy, body):
    """A part drawn turned by rot and scaled about its joint, then placed by `body` (sprite -> frame, and back)."""
    to, back = body
    c, s = math.cos(rot), math.sin(rot)
    pts = []
    for (x, y) in px:
        for dx in (0, 1):
            for dy in (0, 1):
                u, v = (x + dx - pivot[0]) * sx, (y + dy - pivot[1]) * sy
                pts.append(to((pivot[0] + u * c - v * s, pivot[1] + u * s + v * c)))
    x0, x1 = max(0, math.floor(min(p[0] for p in pts))), min(N - 1, math.ceil(max(p[0] for p in pts)))
    y0, y1 = max(0, math.floor(min(p[1] for p in pts))), min(N - 1, math.ceil(max(p[1] for p in pts)))
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            p = back((x + 0.5, y + 0.5))
            u, v = p[0] - pivot[0], p[1] - pivot[1]
            k = (math.floor(pivot[0] + (u * c + v * s) / sx), math.floor(pivot[1] + (-u * s + v * c) / sy))
            if k in px:
                out[y][x] = px[k]


PARTS = dict(bx=0.0, brot=0.0, head=0.0, arm_a=0.0, arm_b=0.0, arm_b_k=1.0, leg_a=0.0, leg_b=0.0)
ORDER = ('leg_a', 'leg_b', 'torso', 'head', 'arm_a', 'arm_b')


def parts_draw(parts):
    def draw(s):
        out = canvas()
        mid = (28, 21)  # the sprite's middle sits on the frame's
        bc, bs = math.cos(s['brot']), math.sin(s['brot'])

        def to(p):
            u, v = p[0] - mid[0], p[1] - mid[1]
            return (M + u * bc - v * bs + s['bx'], M + u * bs + v * bc)

        def back(q):
            u, v = q[0] - M - s['bx'], q[1] - M
            return (mid[0] + u * bc + v * bs, mid[1] - u * bs + v * bc)

        for name in ORDER:
            px, pivot = parts[name]
            k = s.get(name + '_k', 1.0)
            stamp(out, px, pivot, s.get(name, 0.0), k, k, (to, back))
        return out
    return draw


def die_one_arm(ch):
    R = math.radians
    keys = [({'arm_b': R(-170), 'arm_b_k': 0.8, 'head': R(-12)}, 120)]  # the lower hand snaps to his gut
    keys += shudder({'head': R(-16), 'brot': R(-2)}, {'head': R(-12), 'brot': 0}, 3)
    x = 0
    for wait, gain in ((200, 6), (480, 5), (760, 3)):  # three hauls, each weaker and slower to come
        keys += [({}, wait),
                 ({'arm_a': R(80), 'head': R(-20), 'brot': R(-4)}, 120),        # the arm flings back
                 ({'arm_a': R(72)}, 40), ({'arm_a': R(80)}, 80),                 # gropes for the floor
                 ({'bx': x + gain, 'arm_a': R(10), 'head': R(-8), 'brot': R(3),
                   'leg_a': R(6), 'leg_b': R(-6)}, 120),                         # yanks himself along
                 ({'leg_a': 0, 'leg_b': 0, 'brot': R(1)}, 160)]
        x += gain
    keys += [({}, 400), ({'arm_a': R(60)}, 120), ({'arm_a': R(50)}, 40), ({'arm_a': R(60)}, 200),  # tries once more
             ({'arm_a': R(-10), 'head': R(-25)}, 120)]
    keys += shudder({'head': R(-21), 'arm_b': R(-162)}, {'head': R(-25), 'arm_b': R(-170)}, 5)
    keys += [({}, 600), ({'leg_a': R(5)}, 40), ({'leg_a': 0}, 700), ({}, 1000)]
    return tween(keys, parts_draw(on_back(ch)), PARTS, jerk, lambda s: s['bx'])


def animations(ch):
    """{name: (frames, milliseconds per frame, frame size, slide per frame)}"""
    return {name: (frames, ms, N, slide) for name, make in
            (('die_kick', die_kick), ('die_crawl', die_crawl), ('die_one_arm', die_one_arm))
            for frames, ms, slide in [make(ch)]}

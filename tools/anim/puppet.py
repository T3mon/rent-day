"""Part-based character frames: torso, head, hands, sleeves and feet, posed per frame.

Characters face +x, centred in a 72x72 frame (legs: 28x28). Heads, hands and colours come from each
character's hand-drawn sprites in art/ (<name>_armed, _unarmed, _legs, _dead*), so every character
keeps its look; torsos, sleeves and feet are drawn here so they can move.
"""
import math, os
from pngio import read_png, png

PROJECT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..') + '/'
ART = PROJECT + 'art/'
INK = (11, 6, 18, 255)
CLEAR = (0, 0, 0, 0)
SIZE, C = 72, 36  # frames are 72x72 so swung weapons fit; the old sprites were 56x56 around (28, 28)


def blank(n=SIZE):
    return [[CLEAR] * n for _ in range(n)]


class Character:
    """Colours and stamps taken from a character's existing sprites."""

    def __init__(self, name):
        self.name = name
        _, _, self.unarmed = read_png(f'{ART}{name}_unarmed.png')
        _, _, self.armed = read_png(f'{ART}{name}_armed.png')
        _, _, self.legs = read_png(f'{ART}{name}_legs.png')
        u, a = self.unarmed, self.armed
        self.jacket_dark, self.jacket, self.jacket_light = u[20][21], u[22][24], u[22][23]
        self.sleeve, self.sleeve_dark, self.skin = a[30][38], a[29][38], a[29][43]
        self.shoe_lit, self.shoe, self.shoe_heel = self.legs[8][12], self.legs[7][12], self.legs[7][18]
        # The head: everything from the hair line forward, rows 22..34, minus the jacket.
        jacket = {self.jacket_dark[:3], self.jacket[:3], self.jacket_light[:3]}
        self.head = {}
        for y in range(21, 36):
            for x in range(24, 40):
                p = u[y][x]
                if p[3] and p[:3] not in jacket and x >= 25:
                    self.head[(x - 28, y - 28)] = p
        # Trim outline pixels that belong to the torso's edge (keep only ink touching head colour).
        colour = {k for k, v in self.head.items() if v[:3] != INK[:3]}
        self.head = {k: v for k, v in self.head.items()
                     if v[:3] != INK[:3] or any((k[0] + dx, k[1] + dy) in colour for dx in (-1, 0, 1) for dy in (-1, 0, 1))}
        # The armed look's extended arm (sleeve + hand), from x 34 on, rows 26..33.
        self.gun_arm = {(x - 28, y - 28): a[y][x] for y in range(26, 34) for x in range(33, 47) if a[y][x][3]}
        # Lying bodies (old sprites are 56x42 around (28, 21)): back, face down, on the side.
        self.corpses = []
        for kind in ('dead', 'dead_face', 'dead_side'):
            _, _, c = read_png(f'{ART}{name}_{kind}.png')
            self.corpses.append({(x - 28, y - 21): c[y][x] for y in range(42) for x in range(56) if c[y][x][3]})
        d = self.corpses[0]
        self.pants, self.pants_lit, self.shoe_dark = d[(12 - 28, 13 - 21)], d[(12 - 28, 14 - 21)], d[(5 - 28, 12 - 21)]
        self.shirt = d.get((26 - 28, 19 - 21), self.jacket_light)
        self.hair = u[28][28]


# ---------------------------------------------------------------- drawing with per-part outlines
class Layer:
    def __init__(self, n=SIZE):
        self.n = n
        self.px = {}

    def put(self, x, y, c):
        x, y = math.floor(x + 0.5), math.floor(y + 0.5)  # round half up; round() goes to even and skips rows
        if 0 <= x < self.n and 0 <= y < self.n:
            self.px[(x, y)] = c

    def stamp_onto(self, canvas, outline=True):
        if outline:
            for (x, y) in self.px:
                for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                    q = (x + dx, y + dy)
                    if q not in self.px and 0 <= q[0] < self.n and 0 <= q[1] < self.n:
                        canvas[q[1]][q[0]] = INK
        for (x, y), c in self.px.items():
            canvas[y][x] = c


def capsule(layer, a, b, r, colour, edge=None):
    """A thick line with round ends; `edge` colours the pixels on its rim (shading)."""
    ax, ay = a; bx, by = b
    for y in range(int(min(ay, by) - r) - 1, int(max(ay, by) + r) + 2):
        for x in range(int(min(ax, bx) - r) - 1, int(max(ax, bx) + r) + 2):
            px, py = x + 0.5, y + 0.5
            vx, vy = bx - ax, by - ay
            L = vx * vx + vy * vy
            t = 0 if L == 0 else max(0, min(1, ((px - ax) * vx + (py - ay) * vy) / L))
            d = math.hypot(px - (ax + vx * t), py - (ay + vy * t))
            if d <= r:
                layer.put(x, y, edge if edge and d > r - 1 else colour)


def rot(p, ang):
    c, s = math.cos(ang), math.sin(ang)
    return (p[0] * c - p[1] * s, p[0] * s + p[1] * c)


# ---------------------------------------------------------------- body parts
def torso(ch, shear=0.0, shift=(0.0, 0.0)):
    """The jacket seen from above: a block 12 deep and 18 wide with chamfered corners, a dark rim and a
    light stripe at the back. shear twists it the pixel-art way: the upper shoulder rows slide forward
    by up to `shear` pixels and the lower ones back (or the other way round)."""
    layer = Layer()
    for y in range(-9, 10):
        dx = round(shear * y / -8.5)            # top rows (y < 0) move by +shear
        for x in range(-7, 5):
            lx, ly = x + 0.5 + 1.5, y + 0.5 + 0.5  # the old polygon's frame
            if abs(ly) > 8.5 or lx < -5.5 or lx > 5.5:
                continue
            if abs(ly) > 7.0 and abs(lx) > 4.0:   # chamfer
                continue
            c = ch.jacket_dark if (abs(ly) > 7.6 or lx < -4.6) else ch.jacket_light if -3.6 < lx < -2.4 else ch.jacket
            layer.put(C + x + dx + shift[0], C + y + shift[1], c)
    return layer


def head(ch, shift=(0.0, 0.0), turn=0.0):
    layer = Layer()
    for (x, y), c in ch.head.items():
        if c[:3] == INK[:3]:
            continue
        rx, ry = rot((x, y), turn)
        layer.put(C + rx + shift[0], C + ry + shift[1], c)
    return layer


def hand(ch, at):
    layer = Layer()
    x, y = at
    for dx in (-1, 0, 1):
        for dy in (-1, 0):
            layer.put(C + x + dx, C + y + dy, ch.skin)
    return layer


def arm(ch, shoulder, hand_at):
    layer = Layer()
    capsule(layer, (C + shoulder[0], C + shoulder[1]), (C + hand_at[0], C + hand_at[1]), 1.6, ch.sleeve, ch.sleeve_dark)
    return layer


def foot(ch, at, lifted=False):
    """A shoe 10 long, 4 wide: toe lit, heel dark."""
    layer = Layer(28)
    x0, y0 = 14 + at[0], 14 + at[1]
    for dx in range(-5, 5):
        for dy in (-1, 0, 1):
            c = ch.shoe_heel if dx < -2 else (ch.shoe_lit if dy == 0 and dx < 3 else ch.shoe)
            layer.put(x0 + dx, y0 + dy, c)
    return layer


def compose(layers, n=SIZE):
    canvas = blank(n)
    for l in layers:
        l.stamp_onto(canvas)
    return canvas


# ---------------------------------------------------------------- animations
def walk_legs(ch, frames=8, stride=5.0):
    out = []
    for i in range(frames):
        p = 2 * math.pi * i / frames
        s = math.sin(p)
        out.append(compose([foot(ch, (round(-stride * s), -5)), foot(ch, (round(stride * s), 5))], 28))
    return out

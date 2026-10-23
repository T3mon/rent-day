# Pixel-art kit for the synthwave props: a canvas with shapes, gradients, neon rim light and glow.
import math
from PIL import Image

def C(h, a=255):
    return (int(h[1:3], 16), int(h[3:5], 16), int(h[5:7], 16), a)

def mix(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3)) + (255,)

INK = C('#07030f')
D0, D1, D2, D3, D4 = C('#0d0221'), C('#1a0b3d'), C('#261447'), C('#35205e'), C('#4a2c7d')
WD0, WD1, WD2, WD3 = C('#1e1016'), C('#2e1a22'), C('#40262e'), C('#573640')        # espresso wood
PU1, PU2, PU3 = C('#6d2aa8'), C('#8b3fd1'), C('#b06ae8')
HP1, HP2, HP3, HP4 = C('#ff2a6d'), C('#ff6fa8'), C('#c4164f'), C('#7a0c36')
MG, MG2, MGL = C('#f706cf'), C('#a3048a'), C('#ff7ae6')
CY1, CY2, CY3, CY4 = C('#2de2e6'), C('#a8f8fa'), C('#169fb0'), C('#0b5a6e')
SUN = [C('#fdee82'), C('#ffc65c'), C('#ff9a4a'), C('#ff6a5c'), C('#ff2a6d'), C('#d11a87'), C('#8b3fd1')]
CH = [C('#f4f6ff'), C('#c9d0f0'), C('#8f98c8'), C('#5a6296'), C('#333a66')]
YEL, WHT, SOFT, SOFT2, DIM = C('#fdee82'), C('#ffffff'), C('#d8d2e8'), C('#b4abcc'), C('#8f88a6')
CON1, CON2, CON3 = C('#4a4458'), C('#3a3446'), C('#2a2534')                        # dark concrete
K1, K2, K3, K4 = C('#c9a070'), C('#a77b4f'), C('#7d5a37'), C('#553a22')            # cardboard
CASH, CASH2 = C('#ddd5b0'), C('#bdb48c')
T1, T2 = C('#ff9a4a'), C('#ff6a5c')
NEON = [CY1, HP1, MG, MGL, YEL, CY2, HP2]
BAYER = (0.125, 0.625, 0.875, 0.375)


class P:
    def __init__(s, w, h, pad=0):
        s.pad, s.bw, s.bh = pad, w, h
        s.w, s.h = w + 2 * pad, h + 2 * pad
        s.im = Image.new('RGBA', (s.w, s.h), (0, 0, 0, 0))
        s.px = s.im.load()

    def set(s, x, y, c):
        x, y = int(x) + s.pad, int(y) + s.pad
        if c and 0 <= x < s.w and 0 <= y < s.h:
            s.px[x, y] = c

    def get(s, x, y):
        x, y = int(x) + s.pad, int(y) + s.pad
        return s.px[x, y] if 0 <= x < s.w and 0 <= y < s.h else (0, 0, 0, 0)

    def rect(s, x, y, w, h, c):
        for yy in range(int(y), int(y + h)):
            for xx in range(int(x), int(x + w)):
                s.set(xx, yy, c)

    def frame(s, x, y, w, h, c=INK):
        s.rect(x, y, w, 1, c); s.rect(x, y + h - 1, w, 1, c)
        s.rect(x, y, 1, h, c); s.rect(x + w - 1, y, 1, h, c)

    def box(s, x, y, w, h, fill, hi=None, sh=None, out=INK):
        s.rect(x, y, w, h, fill)
        if hi:
            s.rect(x + 1, y + 1, w - 2, 1, hi); s.rect(x + 1, y + 1, 1, h - 2, hi)
        if sh:
            s.rect(x + 1, y + h - 2, w - 2, 1, sh); s.rect(x + w - 2, y + 2, 1, h - 3, sh)
        if out:
            s.frame(x, y, w, h, out)

    def disc(s, cx, cy, r, c):
        for y in range(int(cy - r - 1), int(cy + r + 2)):
            for x in range(int(cx - r - 1), int(cx + r + 2)):
                if (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2 <= r * r:
                    s.set(x, y, c)

    def ellipse(s, cx, cy, rx, ry, c):
        for y in range(int(cy - ry - 1), int(cy + ry + 2)):
            for x in range(int(cx - rx - 1), int(cx + rx + 2)):
                if ((x + 0.5 - cx) / rx) ** 2 + ((y + 0.5 - cy) / ry) ** 2 <= 1:
                    s.set(x, y, c)

    def ring(s, cx, cy, r, c, width=1):
        for y in range(int(cy - r - 1), int(cy + r + 2)):
            for x in range(int(cx - r - 1), int(cx + r + 2)):
                d = math.hypot(x + 0.5 - cx, y + 0.5 - cy)
                if r - width < d <= r:
                    s.set(x, y, c)

    def line(s, x0, y0, x1, y1, c):
        x0, y0, x1, y1 = int(x0), int(y0), int(x1), int(y1)
        dx, dy = abs(x1 - x0), -abs(y1 - y0)
        sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
        err = dx + dy
        while True:
            s.set(x0, y0, c)
            if x0 == x1 and y0 == y1:
                break
            e2 = 2 * err
            if e2 >= dy:
                err += dy; x0 += sx
            if e2 <= dx:
                err += dx; y0 += sy

    def grad(s, x, y, w, h, stops, vertical=True, mask=None):
        n = h if vertical else w
        for i in range(n):
            t = i / max(n - 1, 1) * (len(stops) - 1)
            k, f = int(t), t - int(t)
            for j in range(w if vertical else h):
                xx, yy = (x + j, y + i) if vertical else (x + i, y + j)
                if mask and not mask(xx, yy):
                    continue
                s.set(xx, yy, stops[min(k + 1, len(stops) - 1)] if f > BAYER[(yy % 2) * 2 + xx % 2] else stops[k])

    def chrome(s, x, y, w, h, vertical=False):
        """Polished metal: bright and dark bands across it."""
        s.grad(x, y, w, h, [CH[0], CH[1], CH[2], CH[3], CH[2], CH[1]], vertical=not vertical)

    def outline(s, c=INK):
        edge = []
        for y in range(s.h):
            for x in range(s.w):
                if s.px[x, y][3] == 0 and any(0 <= x + dx < s.w and 0 <= y + dy < s.h and s.px[x + dx, y + dy][3] == 255
                                              and s.px[x + dx, y + dy] != c for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                    edge.append((x, y))
        for x, y in edge:
            s.px[x, y] = c

    def rim(s, tl=CY1, br=HP1, only=None, k=0.42):
        """The room's neon on the object's outer edges: a cyan tint on top and left, pink on bottom and right."""
        clear = lambda x, y: not (0 <= x < s.w and 0 <= y < s.h) or s.px[x, y][3] < 255
        outer = {(x, y) for y in range(s.h) for x in range(s.w) if s.px[x, y] == INK and any(
            clear(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))}
        hole = lambda x, y: clear(x, y) or (x, y) in outer
        changes = []
        for y in range(s.h):
            for x in range(s.w):
                c = s.px[x, y]
                if c[3] < 255 or c == INK or (only and c not in only):
                    continue
                if hole(x, y - 1) or hole(x - 1, y):
                    changes.append((x, y, mix(c, tl, k)))
                elif hole(x, y + 1) or hole(x + 1, y):
                    changes.append((x, y, mix(c, br, k)))
        for x, y, c in changes:
            s.px[x, y] = c

    def _sources(s, colors):
        keys = [c[:3] for c in colors]
        return keys, [(x, y, s.px[x, y]) for y in range(s.h) for x in range(s.w) if s.px[x, y][3] == 255 and s.px[x, y][:3] in keys]

    def glow(s, colors, r=2.5, a0=120):
        """A soft halo, in the light's own colour, round the given colours (onto the floor)."""
        keys, src = s._sources(colors)
        add = {}
        for x, y, c in src:
            for yy in range(int(y - r), int(y + r + 1)):
                for xx in range(int(x - r), int(x + r + 1)):
                    if 0 <= xx < s.w and 0 <= yy < s.h and s.px[xx, yy][3] < 255:
                        d = math.hypot(xx - x, yy - y)
                        if d <= r:
                            a = int(a0 * (1 - d / (r + 0.6)))
                            if a > add.get((xx, yy), (0, 0))[0]:
                                add[(xx, yy)] = (a, c)
        for (xx, yy), (a, c) in add.items():
            if a > s.px[xx, yy][3]:
                s.px[xx, yy] = c[:3] + (a,)

    def bloom(s, colors, r=2, k=0.5):
        """Lights spill onto the object around them."""
        keys, src = s._sources(colors)
        best = {}
        for x, y, c in src:
            for yy in range(int(y - r), int(y + r + 1)):
                for xx in range(int(x - r), int(x + r + 1)):
                    if 0 <= xx < s.w and 0 <= yy < s.h and s.px[xx, yy][3] == 255 and s.px[xx, yy][:3] not in keys and s.px[xx, yy] != INK:
                        d = math.hypot(xx - x, yy - y)
                        if d <= r:
                            t = k * (1 - d / (r + 0.5))
                            if t > best.get((xx, yy), (0, 0))[0]:
                                best[(xx, yy)] = (t, c)
        for (xx, yy), (t, c) in best.items():
            s.px[xx, yy] = mix(s.px[xx, yy], c, t)

    def underglow(s, c, r=2.5, a0=95):
        """Light spilling onto the floor all round the object."""
        edge = [(x, y) for y in range(s.h) for x in range(s.w) if s.px[x, y] == INK]
        add = {}
        for x, y in edge:
            for yy in range(int(y - r), int(y + r + 1)):
                for xx in range(int(x - r), int(x + r + 1)):
                    if 0 <= xx < s.w and 0 <= yy < s.h and s.px[xx, yy][3] == 0:
                        d = math.hypot(xx - x, yy - y)
                        if d <= r:
                            add[(xx, yy)] = max(add.get((xx, yy), 0), int(a0 * (1 - d / (r + 0.6))))
        for (xx, yy), a in add.items():
            s.px[xx, yy] = c[:3] + (a,)

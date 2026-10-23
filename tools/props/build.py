"""Draws every prop (props/<name>.png), the wall tiles (levels/walls3.png) and the room floors
(levels/floors3.png), synthwave but realistic:
real shapes and materials (dark wood with grain, velvet, leather, porcelain, chrome), lit by the room's
neon (a faint cyan edge on top and left, pink on bottom and right), and glowing only where something
gives off light. Sprites that glow are padded with see-through pixels; they stay centred on the body.

    python tools/props/build.py            every prop and the walls
    python tools/props/build.py sofa bed   just these (file names without .png)

A new prop: a function here, a line in PROPS, then its scene in props/ (see docs/architecture.md).
"""
import math, os, random, sys
from PIL import Image
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from synthkit import *

PROJECT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..')


def grain(p, x, y, w, h, base=WD1, dark=WD0, light=WD2, vertical=False):
    """Dark lacquered wood: streaks of grain along it."""
    p.rect(x, y, w, h, base)
    if vertical:
        for xx in range(x + 1, x + w - 1, 2):
            yy = y + random.randrange(0, 4)
            while yy < y + h:
                run = random.randrange(3, 9)
                p.rect(xx, yy, 1, min(run, y + h - yy), dark if random.random() < 0.8 else light)
                yy += run + random.randrange(2, 7)
    else:
        for yy in range(y + 1, y + h - 1, 2):
            xx = x + random.randrange(0, 4)
            while xx < x + w:
                run = random.randrange(3, 9)
                p.rect(xx, yy, min(run, x + w - xx), 1, dark if random.random() < 0.8 else light)
                xx += run + random.randrange(2, 7)

WOOD = {WD0, WD1, WD2, WD3}

def plate(p, x, y):
    p.disc(x, y, 2.6, INK); p.disc(x, y, 1.9, SOFT); p.set(x, y, SOFT2)

def bottle(p, x, y, c):
    p.disc(x, y, 1.7, INK); p.disc(x, y, 1.1, c); p.set(x - 1, y - 1, WHT)


# ======================================================================= living room
def sofa():
    p = P(48, 40, 3)
    p.rect(1, 1, 46, 17, D1); p.rect(1, 1, 17, 38, D1)
    p.rect(1, 1, 46, 5, D2); p.rect(1, 1, 5, 38, D2)
    for x in range(8, 42, 4):                                   # tufted back
        p.set(x, 3, D0); p.set(x + 1, 2, D3)
    for y in range(8, 34, 4):
        p.set(3, y, D0); p.set(2, y + 1, D3)
    for x0, x1 in ((6, 17), (18, 30), (31, 42)):
        p.rect(x0, 6, x1 - x0, 11, D3); p.rect(x0, 6, x1 - x0, 1, D4); p.line(x0 + 2, 8, x0 + 6, 8, D4)
        p.frame(x0, 6, x1 - x0, 11, D0)
    for y0, y1 in ((17, 27), (27, 35)):
        p.rect(6, y0, 11, y1 - y0, D3); p.rect(6, y0, 1, y1 - y0, D4); p.frame(6, y0, 11, y1 - y0, D0)
    p.rect(42, 1, 5, 17, D2); p.frame(42, 1, 5, 17, D0)
    p.rect(1, 34, 17, 5, D2); p.frame(1, 34, 17, 5, D0)
    p.box(20, 8, 6, 5, CY3, CY1, CY4, out=D0)                   # throw pillows
    p.box(8, 20, 6, 5, HP3, HP1, HP4, out=D0)
    p.outline(); p.rim(); p.underglow(MG, 2, 65)
    return p

def bean_bag():
    p = P(18, 17, 2)
    p.ellipse(9, 9, 8, 7.5, D2); p.ellipse(8.5, 8.5, 7, 6.5, D3); p.ellipse(7.5, 7, 5, 4, D4)
    p.ellipse(10, 10, 3.5, 3, D2); p.ellipse(10.5, 10.5, 2, 1.5, D1)       # where someone sat
    p.line(4, 5, 7, 3, PU2); p.set(5, 4, SOFT)                            # leather shine
    p.line(3, 12, 6, 14, D1); p.line(13, 3, 15, 6, D1)
    p.outline(); p.rim()
    return p

def vhs_shelf():
    p = P(40, 10, 2)
    grain(p, 0, 0, 40, 10)
    p.rect(2, 2, 36, 6, D0)
    x = 2
    while x < 37:
        if random.random() < 0.1:
            x += 1; continue
        w = min(2 if random.random() < 0.8 else 3, 38 - x)
        p.rect(x, 2, w, 6, C('#15121e')); p.rect(x, 4, w, 2, random.choice([HP1, CY1, YEL, PU3, MG, SUN[2], SOFT]))
        x += w
    p.rect(1, 8, 38, 1, MG)                                                # LED strip on the shelf edge
    p.frame(0, 0, 40, 10)
    p.rim(only=WOOD); p.glow([MG], 2, 80); p.bloom([MG], 1.5, 0.3)
    return p

def floor_lamp():
    p = P(14, 14, 3)
    p.disc(7, 7, 7, INK); p.disc(7, 7, 6, D3); p.ring(7, 7, 6, D4)          # fabric shade, lit from inside
    p.disc(7, 7, 4.5, MG2); p.disc(7, 7, 3.5, MGL); p.disc(7, 7, 2, C('#ffd0f4')); p.disc(7, 7, 1, WHT)
    p.underglow(MGL, 3, 85)
    return p

def stereo():
    p = P(40, 12, 2)
    grain(p, 0, 1, 40, 11); p.frame(0, 1, 40, 11)
    for x in (1, 29):                                                       # speakers
        p.box(x, 0, 10, 12, D1, D2, D0)
        p.disc(x + 5, 6.5, 3.6, D0); p.ring(x + 5, 6.5, 3.6, CY1); p.disc(x + 5, 6.5, 1.5, D3); p.set(x + 5, 6, CH[1])
    p.box(12, 2, 11, 9, D2, D3, D0)                                         # turntable
    p.disc(17.5, 6.5, 4, C('#101014')); p.ring(17.5, 6.5, 2.8, C('#22222c')); p.disc(17.5, 6.5, 1.3, HP1)
    p.line(22, 3, 20, 7, CH[1]); p.set(22, 3, CH[0])
    for i, h in enumerate((3, 5, 4, 6, 2)):                                 # level meter
        for k in range(h):
            p.set(23 + i, 10 - k, [HP1, HP1, SUN[3], SUN[2], SUN[1], SUN[0]][k])
    p.rim(only=WOOD | {D1, D2}); p.glow([CY1], 1.5, 70); p.bloom([CY1], 1.2, 0.3)
    return p

def aquarium():
    p = P(34, 14, 3)
    p.box(0, 0, 34, 14, D0, D2, INK)
    p.grad(2, 2, 30, 10, [CY3, C('#185a9a'), C('#1e2f78'), D2])
    for x in range(2, 32):
        if (x * 7) % 5 < 2:
            p.set(x, 11, PU1)
    p.ellipse(8, 8, 2, 2.5, MG2); p.set(8, 6, MG); p.set(7, 8, HP1)          # coral
    p.ellipse(25, 9, 1.5, 2, PU2); p.set(25, 8, PU3)
    for fx, fy, c in ((14, 5, YEL), (20, 7, HP2), (27, 4, CY2)):             # fish
        p.set(fx, fy, c); p.set(fx + 1, fy, c); p.set(fx - 1, fy, mix(c, D0, 0.4))
    p.line(4, 3, 7, 3, CY2); p.line(18, 9, 20, 9, CY1)
    p.set(29, 3, WHT); p.set(30, 5, CY2); p.set(29, 7, CY2)
    p.rect(1, 1, 32, 1, CY1)                                                # the lid's light
    p.glow([CY1], 2, 70); p.underglow(CY1, 2, 55)
    return p

def plant():
    p = P(18, 18, 1)
    p.disc(9, 9, 5, INK); p.disc(9, 9, 4.2, CON2); p.disc(9, 9, 3.4, C('#21140f'))   # pot of soil
    leaf, light, dark = C('#7a2462'), C('#a8408a'), C('#4e1440')
    for i, a in enumerate(range(15, 375, 45)):                              # cordyline: long purple leaves
        r = math.radians(a + (i % 2) * 8)
        L = 8.4 if i % 2 == 0 else 7
        for t in [j / 4 for j in range(int(L * 4) + 1)]:
            p.disc(9 + math.cos(r) * t, 9 + math.sin(r) * t, 0.5 + 1.3 * math.sin(math.pi * min(t / L, 1)), leaf)
        for t in [j / 4 for j in range(2, int(L * 4) - 2)]:                 # the midrib, catching the light
            p.set(9 + math.cos(r) * t, 9 + math.sin(r) * t, light if t > L * 0.4 else dark)
    p.disc(9, 9, 1.6, dark)
    p.outline(); p.rim(k=0.3)
    return p

# ======================================================================= bedroom
def sunset_bed():
    p = P(36, 44, 2)
    grain(p, 0, 0, 36, 44, vertical=True); p.frame(0, 0, 36, 44)
    p.chrome(1, 1, 34, 4); p.rect(1, 5, 34, 1, INK)                         # chrome headboard
    p.rect(2, 6, 32, 36, SOFT)
    for x, y in ((4, 7), (19, 8)):
        p.rect(x, y, 13, 7, SOFT); p.rect(x, y, 13, 1, WHT); p.frame(x, y, 13, 7, SOFT2)
    p.rect(2, 16, 32, 2, SOFT); p.rect(2, 18, 32, 1, SOFT2)                 # sheet folded over
    p.grad(2, 19, 32, 23, SUN)                                              # sunset-print duvet
    for i, y in enumerate((30, 33, 36, 39)):
        p.rect(2, y, 32, 1 + i // 2, PU1)
    p.rim(only=WOOD); p.underglow(MG, 2, 65)
    return p

def single_bed():
    p = P(22, 42, 2)
    grain(p, 0, 0, 22, 42, vertical=True); p.frame(0, 0, 22, 42)
    p.rect(2, 2, 18, 38, SOFT)
    p.rect(4, 3, 14, 7, SOFT); p.rect(4, 3, 14, 1, WHT); p.frame(4, 3, 14, 7, SOFT2)
    p.rect(2, 12, 18, 28, D3)
    for k in range(5):                                                       # creases
        for t in range(26):
            x = 3 + k * 4 + t * 3 // 25
            if x < 20:
                p.set(x, 39 - t, D4)
    p.rect(2, 12, 18, 1, D4); p.rect(2, 16, 18, 2, HP3); p.rect(2, 35, 18, 2, HP3)
    p.rim(only=WOOD); p.underglow(HP1, 2, 60)
    return p

def wardrobe():
    p = P(34, 16, 2)
    grain(p, 0, 0, 34, 16); p.frame(0, 0, 34, 16)
    p.disc(8, 7, 4.5, INK); p.disc(8, 7, 3.5, HP3); p.ring(8, 7, 3.5, HP1); p.line(4, 7, 12, 7, CY1)   # hat box
    p.box(17, 3, 14, 8, D2, D3, D1); p.rect(17, 7, 14, 1, D1); p.rect(23, 2, 3, 1, CH[1])           # suitcase
    p.rect(15, 14, 1, 1, CH[0]); p.rect(18, 14, 1, 1, CH[0])                                        # door handles
    p.rim(only=WOOD)
    return p

def vanity():
    p = P(30, 24, 3)
    grain(p, 0, 0, 30, 12); p.frame(0, 0, 30, 12)
    p.ellipse(15, 3, 9, 3.5, INK); p.ellipse(15, 3, 8, 2.5, CY4); p.line(10, 3, 13, 1, CY2)       # mirror
    for k in range(190, 351, 20):                                                                 # bulbs round it
        a = math.radians(k)
        p.set(15 + math.cos(a) * 9.6, 3 + math.sin(a) * 4.3 + 0.5, YEL)
    p.box(2, 6, 4, 5, HP3, HP2, None); p.box(5, 8, 4, 4, PU1, PU3, None)                         # perfume
    p.rect(21, 8, 6, 2, MG2); p.rect(27, 8, 1, 2, SOFT2); p.rect(17, 6, 1, 4, HP1)               # brush, lipstick
    p.disc(15, 18, 5, INK); p.ring(15, 18, 4, CH[2]); p.disc(15, 18, 3.2, D3); p.disc(14, 17, 1.6, D4)  # stool
    p.glow([YEL], 2, 110); p.bloom([YEL], 2, 0.4); p.rim(only=WOOD)
    return p

# ======================================================================= kitchen and dining
def round_table():
    p = P(36, 36, 2)
    for x, y, bx, by, bw, bh in ((14, 0, 14, 0, 8, 3), (14, 28, 14, 33, 8, 3), (0, 14, 0, 14, 3, 8), (28, 14, 33, 14, 3, 8)):
        p.box(x, y, 8, 8, D3, D4, D2)
        p.box(bx, by, bw, bh, WD2, WD3, WD0)
    p.disc(18, 18, 12, INK); p.disc(18, 18, 11, WD1)
    p.line(9, 15, 15, 9, WD2); p.line(21, 27, 27, 21, WD0); p.line(11, 22, 14, 25, WD0)
    p.ring(18, 18, 11, CH[2])
    for x, y in ((18, 9.5), (18, 26.5), (9.5, 18), (26.5, 18)):
        plate(p, x, y)
    p.disc(18, 18, 1.6, SOFT); p.set(18, 17, YEL)                                   # candle
    p.bloom([YEL], 3, 0.45); p.rim(only=WOOD | {D3, D4, D2})
    return p

def stove():
    p = P(18, 20, 2)
    p.rect(0, 0, 18, 20, CH[2]); p.rect(1, 1, 16, 18, D0); p.frame(0, 0, 18, 20)
    for i, (x, y) in enumerate(((5.5, 5.5), (12.5, 5.5), (5.5, 11.5), (12.5, 11.5))):
        if i == 1:                                                                   # one ring on, glowing
            p.ring(x, y, 3.2, HP1); p.ring(x, y, 2.2, SUN[3]); p.disc(x, y, 1.2, SUN[1])
        else:
            p.ring(x, y, 3.2, C('#2c2546')); p.ring(x, y, 1.8, C('#2c2546'))
    p.rect(2, 16, 14, 2, D1); p.rect(3, 16, 4, 1, CY1)
    for x in (10, 12, 14):
        p.set(x, 17, SOFT2)
    p.bloom([HP1, SUN[3], SUN[1]], 2, 0.4); p.bloom([CY1], 1, 0.3)
    return p

def kitchen_island():
    p = P(42, 26, 2)
    for x in (9, 21, 33):                                                             # stools
        p.disc(x, 21.5, 4.2, INK); p.ring(x, 21.5, 3.6, CH[1]); p.disc(x, 21.5, 2.8, HP3); p.disc(x - 0.7, 20.8, 1.3, HP1)
    p.box(0, 0, 42, 16, D1, D2, D0)
    for x0, y0, x1, y1 in ((4, 3, 9, 6), (9, 6, 14, 5), (25, 10, 31, 12), (31, 12, 37, 11), (17, 2, 20, 4)):
        p.line(x0, y0, x1, y1, PU1)                                                   # dark marble veins
    p.set(11, 5, PU3); p.set(28, 11, PU3)
    p.disc(30, 6, 4, INK); p.disc(30, 6, 3, CH[2])                                    # fruit bowl
    p.disc(29, 5, 1.4, HP1); p.disc(31, 7, 1.4, YEL); p.set(30, 5, PU3)
    p.rect(1, 14, 40, 1, CH[2])
    p.rim(only={D1, D2})
    return p

def trash_bins():
    p = P(24, 12, 1)
    p.disc(6, 6, 6, INK); p.disc(6, 6, 5, CH[2]); p.disc(5.5, 5.5, 3.8, CH[1]); p.disc(6, 6, 2.4, CH[3]); p.rect(4, 5, 5, 2, CH[0])
    p.box(13, 1, 11, 11, D2, D3, D1); p.rect(15, 3, 7, 7, HP3); p.rect(15, 3, 7, 1, HP1)
    p.set(17, 6, SOFT); p.set(18, 5, SOFT); p.set(19, 6, SOFT)
    p.rim(only={D2, D3, D1})
    return p

# ======================================================================= bathroom and laundry
def shower():
    p = P(24, 24, 2)
    p.box(0, 0, 24, 24, SOFT, WHT, SOFT2)
    p.rect(2, 2, 20, 20, D2)
    for k in range(2, 22, 5):
        p.line(k, 2, k, 21, D3); p.line(2, k, 21, k, D3)
    for x, y in ((4, 9), (14, 4), (9, 16), (18, 13)):
        p.set(x, y, D4)
    p.disc(12, 12, 1.8, CH[2]); p.set(12, 12, D0)
    p.rect(2, 2, 20, 1, CY1)                                                          # light in the niche
    p.rect(1, 22, 22, 1, CY2); p.rect(22, 1, 1, 22, CY2)
    p.set(20, 22, WHT); p.set(22, 18, WHT); p.set(22, 6, WHT)
    p.disc(4.5, 4.5, 2.5, INK); p.disc(4.5, 4.5, 1.8, CH[1]); p.set(4, 4, WHT)
    p.bloom([CY1], 2.5, 0.4); p.rim(only={SOFT, WHT, SOFT2})
    return p

def washer_dryer():
    p = P(36, 18, 2)
    for x in (0, 19):
        p.box(x, 0, 17, 18, SOFT, WHT, SOFT2)
    p.disc(8.5, 10, 5.5, INK); p.ring(8.5, 10, 5, CH[2], 1.5); p.disc(8.5, 10, 3.6, CY4); p.disc(8.5, 10, 2.6, CY3)
    p.set(7, 9, CY1); p.set(9, 11, CY2)
    p.rect(2, 2, 13, 2, D1); p.rect(10, 2, 4, 1, CY1)
    p.rect(21, 2, 13, 2, D1); p.rect(29, 2, 3, 1, HP1)
    p.box(22, 6, 11, 9, SOFT2, SOFT, DIM, out=DIM); p.line(23, 10, 31, 10, DIM)
    p.bloom([CY1, HP1], 1.5, 0.4); p.rim(only={SOFT, WHT, SOFT2})
    return p

def clawfoot_tub():
    p = P(24, 42, 2)
    for x, y in ((3, 6), (19, 6), (3, 35), (19, 35)):
        p.disc(x + 1, y + 1, 2.2, INK); p.disc(x + 1, y + 1, 1.5, CH[1])
    p.ellipse(12, 21, 10.5, 19.5, INK); p.ellipse(12, 21, 9.5, 18.5, D1)              # black enamel outside
    p.ellipse(12, 21.5, 7.8, 16.8, SOFT)
    p.ellipse(12, 22, 6.8, 15.2, CY3); p.line(8, 13, 11, 9, CY2); p.line(9, 31, 13, 26, CY2)   # water
    p.rect(11, 2, 3, 3, CH[1]); p.set(12, 2, WHT)
    p.rim(only={D1})
    return p

# ======================================================================= office
def corner_desk():
    p = P(48, 32, 2)
    grain(p, 1, 1, 46, 14); grain(p, 33, 1, 14, 30, vertical=True)
    for x in (8, 19):                                                                  # monitors
        p.box(x, 2, 10, 4, D0, D1, INK); p.rect(x + 1, 5, 8, 1, CY1); p.rect(x + 4, 6, 2, 1, CH[3])
    p.rect(9, 9, 18, 4, D0); p.grad(10, 10, 16, 2, [HP1, MG, PU2, CY1], vertical=False)   # backlit keyboard
    p.disc(30, 10.5, 1.6, INK); p.set(30, 10, D2); p.set(30, 11, HP1)
    p.box(36, 18, 8, 10, SOFT, WHT, SOFT2, out=SOFT2); p.line(37, 21, 42, 21, DIM); p.line(37, 23, 42, 23, DIM)
    p.disc(40, 7, 2.2, INK); p.disc(40, 7, 1.5, HP3); p.set(40, 7, WD0)
    p.outline(); p.rim(only=WOOD); p.bloom([CY1], 2, 0.45)
    return p

def filing_cabinets():
    p = P(32, 13, 1)
    for i in range(3):
        x, w = i * 10 + (1 if i else 0), 10 + (1 if i == 0 else 0)
        p.box(x, 0, w, 13, D2, D3, D1); p.rect(x + 3, 8, 4, 1, CH[1]); p.rect(x + 3, 3, 4, 2, SOFT2)
    p.rim(only={D2, D3, D1})
    return p

def safe():
    p = P(18, 18, 1)
    p.chrome(0, 0, 18, 18); p.frame(0, 0, 18, 18)
    p.box(3, 3, 12, 12, CH[3], CH[2], CH[4], out=CH[4])
    p.disc(9, 9, 3.6, INK); p.disc(9, 9, 2.8, D1); p.ring(9, 9, 2.8, HP1); p.set(9, 7, WHT)
    p.line(13, 11, 15, 13, CH[0])
    for x, y in ((1, 1), (16, 1), (1, 16), (16, 16)):
        p.set(x, y, CH[0])
    return p

def copier():
    p = P(26, 18, 2)
    p.box(0, 0, 22, 18, D2, D3, D1)
    p.box(2, 2, 18, 9, D1, D2, D0); p.rect(3, 3, 16, 1, CY1)
    p.rect(2, 12, 18, 4, D0); p.rect(3, 13, 5, 2, CY3); p.set(4, 13, CY1); p.set(10, 14, HP1); p.set(12, 14, YEL)
    p.box(21, 4, 5, 10, CH[2], CH[1], CH[3]); p.rect(22, 5, 3, 8, WHT)
    p.bloom([CY1], 2, 0.45); p.rim(only={D2, D3, D1})
    return p

# ======================================================================= bar and den
def pool_table():
    p = P(58, 34, 3)
    grain(p, 0, 0, 58, 34); p.frame(0, 0, 58, 34)
    p.frame(2, 2, 54, 30, CY1)                                                         # LED-lit rail
    p.rect(3, 3, 52, 28, PU1)
    p.dither = None
    for y in range(3, 31):
        for x in range(3, 55):
            d = ((x - 29) / 26) ** 2 + ((y - 17) / 14) ** 2                            # lamp-lit felt
            if d < 0.35:
                p.set(x, y, PU2 if (x + y) % 3 else PU1)
            elif d > 0.8 and (x + y) % 2 == 0:
                p.set(x, y, C('#5a2290'))
    for x, y in ((3, 3), (29, 2.5), (55, 3), (3, 31), (29, 31.5), (55, 31)):
        p.disc(x, y, 2.5, INK); p.ring(x, y, 2.5, CH[2])
    p.rect(15, 16, 2, 2, WHT)
    for x, y, c in ((40, 16, YEL), (42, 15, C('#3d6fa8')), (42, 17, HP3), (44, 14, PU3), (44, 16, INK), (44, 18, SUN[2]),
                    (46, 13, C('#8a2a34')), (46, 15, YEL), (46, 17, C('#3d6fa8')), (46, 19, HP3)):
        p.rect(x, y, 2, 2, c); p.set(x, y, WHT if c != INK else D3)
    p.line(3, 29, 13, 19, K1); p.line(4, 29, 14, 19, K2); p.set(14, 19, SOFT); p.line(3, 29, 5, 27, D0)   # cue
    p.rim(only=WOOD); p.glow([CY1], 2, 70); p.underglow(CY1, 2, 60)
    return p

def bar_counter():
    p = P(62, 18, 2)
    grain(p, 0, 0, 62, 18); p.frame(0, 0, 62, 18)
    p.rect(1, 15, 60, 1, HP1)                                                          # neon under the edge
    cols = [C('#3d6fa8'), HP3, YEL, PU2, SUN[2], SOFT2, CY3, MG2, K2]
    for i, x in enumerate(range(4, 40, 4)):
        bottle(p, x, 3.5, cols[i % len(cols)])
    for x in (44, 49):
        p.disc(x, 10, 2, INK); p.disc(x, 10, 1.3, CY4); p.set(x - 1, 9, CY2)
    p.box(52, 1, 6, 5, CH[2], CH[0], CH[3])
    p.rect(10, 9, 5, 3, CASH); p.rect(12, 9, 1, 3, HP3)
    p.rim(only=WOOD); p.glow([HP1], 2, 85)
    return p

def jukebox():
    p = P(20, 16, 3)
    p.ellipse(10, 6, 9.5, 6.5, INK); p.rect(0, 6, 20, 10, INK)
    shape = lambda x, y: (((x + 0.5 - 10) / 8.5) ** 2 + ((y + 0.5 - 6) / 5.5) ** 2 <= 1) or (y >= 6 and 1 <= x < 19 and y < 15)
    p.grad(1, 1, 18, 14, [SUN[1], SUN[3], SUN[4], SUN[5], PU1], mask=shape)
    p.ellipse(10, 6, 6.5, 3.5, CH[1]); p.ellipse(10, 6.5, 5, 2.5, CY4); p.line(6, 6, 14, 6, CH[2])
    p.rect(3, 12, 14, 2, D0)
    for x in range(3, 17, 2):
        p.set(x, 12, CH[3])
    p.rect(1, 10, 18, 1, MGL); p.rect(1, 11, 18, 1, CY1)
    p.glow([MGL, CY1], 2, 110); p.bloom([MGL, CY1], 1.5, 0.35)
    return p

def arcade():
    p = P(16, 20, 3)
    p.rect(0, 0, 16, 20, D0); p.frame(0, 0, 16, 20)
    p.rect(1, 4, 1, 15, MG2); p.rect(14, 4, 1, 15, MG2)                                  # side art
    p.grad(1, 1, 14, 3, SUN[:5])                                                         # backlit marquee
    p.rect(2, 5, 12, 8, INK)
    p.grad(3, 6, 10, 3, [C('#2a0b4a'), C('#6d2aa8')])
    p.disc(8, 9, 2.5, SUN[1]); p.rect(3, 9, 10, 3, C('#14062a'))
    p.line(3, 11, 12, 11, MG); p.set(5, 10, MG); p.set(11, 10, MG)
    p.chrome(1, 14, 14, 5); p.frame(1, 14, 14, 5, CH[4])
    p.disc(4.5, 16.5, 1.5, HP1); p.set(4, 16, WHT); p.set(8, 16, CY1); p.set(10, 16, YEL); p.set(12, 16, MG)
    p.glow(SUN[:5], 1.5, 80); p.bloom([MG, SUN[1]], 1, 0.3)
    return p

def weight_bench():
    p = P(36, 18, 1)
    p.rect(1, 5, 34, 1, CH[1])
    for x in (2, 31):
        p.box(x, 1, 3, 10, C('#16141c'), C('#2a2836'), None)
    p.rect(14, 2, 2, 6, CH[2]); p.rect(20, 2, 2, 6, CH[2])
    p.box(8, 7, 20, 7, D1, D2, D0)
    for x in range(9, 27, 2):
        p.set(x, 10, HP3)                                                                # pink stitching
    p.rect(30, 14, 5, 2, CH[1]); p.rect(29, 13, 2, 4, D0); p.rect(34, 13, 2, 4, D0)
    p.outline(); p.rim(only={D1, D2})
    return p

# ======================================================================= rugs (flat: no outline, no shadow)
def sunset_rug():
    p = P(46, 30)
    p.grad(0, 0, 46, 15, [C('#14062a'), D2, PU1])
    for x, y in ((5, 3), (14, 6), (38, 4), (42, 9), (24, 2), (8, 10)):
        p.set(x, y, SOFT)
    for y in range(3, 15):
        for x in range(10, 36):
            if (x + 0.5 - 23) ** 2 + (y + 0.5 - 15) ** 2 <= 144 and y not in (10, 12, 13):
                p.set(x, y, SUN[min(int((y - 3) / 12 * 5), 5)])
    p.rect(0, 15, 46, 15, D1); p.rect(0, 15, 46, 1, MG)
    for i in range(-6, 7):
        p.line(23 + i * 2, 15, 23 + i * 9, 29, MG2)
    for y in (17, 20, 24, 29):
        p.rect(0, y, 46, 1, MG2)
    p.frame(0, 0, 46, 30, CY3)
    return p

def sun_round_rug():
    p = P(34, 34)
    p.disc(17, 17, 17, PU1); p.disc(17, 17, 15.5, D2)
    for y in range(34):
        for x in range(34):
            if (x + 0.5 - 17) ** 2 + (y + 0.5 - 17) ** 2 <= 12.5 ** 2 and y not in (19, 22, 23, 25, 26, 28, 29):
                p.set(x, y, SUN[min(int((y - 4) / 26 * 6), 6)])
    p.ring(17, 17, 16.3, CY3)
    return p

def runner():
    p = P(16, 58)
    p.rect(0, 2, 16, 54, D1)
    p.rect(1, 3, 1, 52, CY3); p.rect(14, 3, 1, 52, CY3); p.rect(3, 3, 1, 52, HP4); p.rect(12, 3, 1, 52, HP4)
    for y in range(7, 52, 9):                                                            # chevrons down the middle
        for k in range(5):
            p.set(5 + k, y + k, HP3); p.set(10 - k, y + k, HP3)
    for x in range(1, 16, 2):
        p.set(x, 0, SOFT2); p.set(x, 1, SOFT); p.set(x, 56, SOFT); p.set(x, 57, SOFT2)
    return p

def tiger_rug():
    p = P(46, 34)
    O, OL, ST, WH, EDGE = C('#e0802a'), C('#f2a24c'), C('#24160e'), C('#efe6d8'), C('#6e3a12')
    p.ellipse(23, 18, 11, 7, O)
    for (x0, y0, x1, y1) in ((14, 14, 4, 4), (32, 14, 42, 4), (14, 22, 4, 31), (32, 22, 42, 31)):
        for t in range(11):
            p.disc(x0 + (x1 - x0) * t / 10, y0 + (y1 - y0) * t / 10, 3.2 - t * 0.12, O)
        p.disc(x1, y1, 1.8, WH)
    for t in range(9):
        p.disc(34 + t, 19 + math.sin(t / 2) * 2, 1.4, O)
    p.set(43, 18, ST); p.set(42, 19, ST)
    p.ellipse(10.5, 17, 5.5, 5, O); p.ellipse(7.5, 17, 2.5, 2.2, WH)
    p.disc(11, 12.5, 1.6, O); p.disc(11, 21.5, 1.6, O); p.set(11, 12, ST); p.set(11, 22, ST)
    p.set(10, 15, YEL); p.set(10, 19, YEL); p.set(6, 17, C('#b05a5a')); p.set(5, 17, C('#b05a5a'))
    for x in range(15, 33, 3):
        p.line(x, 12 + (x % 2), x + 1, 15, ST); p.line(x + 1, 21, x, 24 - (x % 2), ST)
    p.line(13, 15, 14, 17, ST); p.line(13, 19, 14, 18, ST)
    p.ellipse(23, 18, 6, 2.5, OL)
    p.outline(EDGE)
    return p

def shag_rug():
    p = P(38, 26)
    for y in range(26):
        for x in range(38):
            if ((x + 0.5 - 19) / 18.5) ** 2 + ((y + 0.5 - 13) / 12.5) ** 2 <= 1 - random.random() * 0.12:
                r = random.random()
                p.set(x, y, HP2 if r < 0.2 else HP3 if r < 0.42 else MGL if r < 0.5 else HP1)
    for _ in range(26):
        x, y = random.randrange(4, 34), random.randrange(3, 23)
        if p.get(x, y)[3]:
            p.set(x, y, HP4); p.set(x + 1, y, HP2)
    return p

def memphis_rug():
    p = P(46, 30)
    p.rect(0, 0, 46, 30, D0); p.frame(0, 0, 46, 30, CY3); p.frame(1, 1, 44, 28, CY1)
    for x0, y0 in ((5, 6), (24, 18), (34, 5)):                                           # squiggles
        for k in range(8):
            p.set(x0 + k, y0 + (1 if k % 4 in (1, 2) else 0), CY1)
    for cx, cy in ((14, 20), (31, 12)):                                                   # triangles
        for k in range(5):
            p.set(cx - k, cy + k, HP1); p.set(cx + k, cy + k, HP1)
        p.rect(cx - 4, cy + 4, 9, 1, HP1)
    for x, y in ((9, 13), (19, 7), (39, 21), (27, 24), (40, 14), (6, 23)):                # dots
        p.rect(x, y, 2, 2, YEL)
    for k in range(10):                                                                   # zigzag
        p.set(18 + k, 13 + (k % 2), MG)
    return p

# ======================================================================= walls, windows and corners
def night_window():
    p = P(32, 8, 1)
    p.chrome(0, 0, 32, 8); p.frame(0, 0, 32, 8)
    p.grad(2, 2, 28, 4, [C('#14062a'), D2, PU1])
    for x in range(2, 30):                                                                 # the city at night
        r = random.random()
        if r < 0.35:
            p.set(x, 4 + (x % 2), random.choice([YEL, HP1, CY1, SOFT, SUN[2]]))
    p.line(5, 5, 8, 2, MGL); p.line(20, 5, 23, 2, CY2)                                     # reflections
    p.rect(15, 1, 2, 6, CH[2])
    return p

def radiator():
    p = P(24, 8, 1)
    p.rect(0, 1, 22, 7, CH[2]); p.frame(0, 1, 22, 7)
    for x in range(1, 21):
        p.rect(x, 2, 1, 5, CH[0] if x % 2 else CH[3])
    p.rect(21, 3, 3, 3, CH[3]); p.set(22, 4, HP1)
    return p

def pillar():
    p = P(16, 16, 2)
    p.box(0, 0, 16, 16, CON2, CON1, CON3)
    p.frame(2, 2, 12, 12, CON3)
    p.line(4, 10, 7, 8, D0); p.line(7, 8, 8, 9, D0); p.set(11, 4, D0)                    # cracks
    p.line(9, 12, 12, 11, HP1); p.set(12, 10, HP1); p.set(10, 13, MGL)                     # a spray tag
    p.rect(1, 14, 14, 1, CY1)                                                              # LED strip on its face
    p.glow([CY1], 2, 80); p.rim(only={CON1, CON2, CON3})
    return p

def glass_partition():
    p = P(34, 6, 2)
    p.box(0, 0, 34, 6, CH[3], CH[2], CH[4])
    p.rect(1, 2, 32, 2, C('#a9dce8'))
    for x in range(1, 33):
        if x % 3 == 0:
            p.set(x, 2, SOFT)
    p.rect(1, 1, 32, 1, MG); p.rect(1, 4, 32, 1, MG)                                       # LED edges
    p.set(5, 2, WHT); p.set(16, 3, WHT); p.set(28, 2, WHT)
    p.glow([MG], 2, 85)
    return p

# ======================================================================= clutter
def pizza_boxes():
    p = P(22, 22, 1)
    p.box(4, 4, 17, 17, K2, K1, K3)
    p.box(0, 0, 17, 17, SOFT, WHT, SOFT2)
    for k in range(5):                                                                      # a slice printed on the lid
        p.rect(6 + k, 5 + k * 2, 6 - k, 2, HP1 if k < 4 else YEL)
    p.rect(6, 5, 6, 1, YEL)
    p.ellipse(13, 13, 2, 1.5, C('#c9b58c'))                                                # grease
    p.rim(only={SOFT, WHT, SOFT2, K1, K2, K3})
    return p

def duffel_bag():
    p = P(28, 16, 1)
    p.ellipse(14, 8, 13.5, 6.5, INK); p.ellipse(14, 8, 12.5, 5.5, D1); p.ellipse(13, 7, 10, 3.5, D2)
    p.rect(7, 6, 15, 4, D0)
    for x in range(8, 21, 4):
        p.rect(x, 6, 3, 4, CASH); p.rect(x, 7, 3, 1, HP3); p.set(x, 9, CASH2)
    p.line(7, 5, 21, 5, CH[1]); p.line(7, 10, 21, 10, CH[1])
    p.line(3, 3, 9, 1, D2); p.line(19, 1, 25, 3, D2)
    p.rim(only={D1, D2})
    return p

def keytar():
    p = P(36, 14, 1)
    for k in range(10):                                                                     # neck, angled up
        p.rect(24 + k, 6 - k // 2, 2, 3, HP3)
    p.rect(33, 0, 3, 4, HP4)
    p.box(0, 2, 26, 10, SOFT, WHT, SOFT2)
    p.rect(3, 4, 20, 6, WHT)
    for x in range(3, 23, 2):
        p.set(x, 9, SOFT2)
    for x in (4, 6, 10, 12, 14, 18, 20):
        p.rect(x, 4, 1, 3, D0)
    p.set(1, 5, CY1); p.set(1, 7, HP1)
    p.outline(); p.rim(only={SOFT, WHT, SOFT2})
    return p


# ======================================================================= the existing props, redrawn (same sizes)
def old_armchair():
    p = P(22, 22, 2)
    p.box(0, 0, 22, 22, D2, D3, D1)
    p.rect(1, 1, 20, 5, D1); p.rect(1, 1, 4, 20, D1); p.rect(17, 1, 4, 20, D1)
    for x in range(4, 18, 3):
        p.set(x, 3, D0)
    p.box(5, 6, 12, 14, D3, D4, D2, out=D0)
    p.box(7, 7, 6, 5, HP3, HP1, HP4, out=D0)
    p.rim()
    return p

def old_bathtub():
    p = P(22, 42, 2)
    p.box(0, 0, 22, 42, SOFT, WHT, SOFT2)
    p.ellipse(11, 21.5, 8, 18.5, SOFT2); p.ellipse(11, 22.5, 7, 16.5, CY3)
    p.line(7, 12, 10, 8, CY2); p.line(8, 30, 12, 25, CY2); p.set(13, 16, CY2)
    p.rect(9, 1, 4, 3, CH[1]); p.set(10, 1, WHT)
    p.rim(only={SOFT, WHT, SOFT2})
    return p

def old_bed():
    p = P(34, 46, 3)
    grain(p, 0, 0, 34, 46, vertical=True); p.frame(0, 0, 34, 46)
    p.rect(1, 1, 32, 5, D0); p.rect(2, 3, 30, 1, MG); p.rect(2, 2, 30, 1, MGL)            # LED headboard
    p.rect(2, 6, 30, 38, D2)
    for x, y in ((4, 7), (18, 8)):
        p.rect(x, y, 12, 7, D4); p.rect(x, y, 12, 1, PU1); p.frame(x, y, 12, 7, D0)
    p.rect(2, 16, 30, 28, PU1)
    for k in range(-1, 6):
        for t in range(27):
            x = 2 + k * 6 + t * 8 // 26
            if 2 <= x < 32:
                p.set(x, 43 - t, PU2)
            if 2 <= x + 1 < 32:
                p.set(x + 1, 43 - t, D4)
    p.line(2, 16, 31, 16, D0)
    for y in range(16, 25):
        for x in range(32 - (25 - y) * 2, 32):
            p.set(x, y, CY3 if (x + y) % 4 else CY1)
    p.rim(only=WOOD); p.glow([MG, MGL], 2.5, 105); p.underglow(CY1, 2, 60)
    return p

def old_box():
    p = P(16, 16, 1)
    p.box(0, 0, 16, 16, K2, K1, K3)
    p.rect(7, 1, 2, 14, HP3); p.rect(7, 1, 1, 14, HP1)                                     # tape
    p.rect(2, 10, 4, 3, SOFT); p.rect(2, 11, 3, 1, DIM)
    p.line(2, 4, 5, 2, K4)
    p.rim(only={K1, K2, K3})
    return p

def old_cabinet():
    p = P(18, 14, 1)
    grain(p, 0, 0, 18, 14); p.frame(0, 0, 18, 14)
    p.line(9, 1, 9, 12, WD0)
    p.rect(7, 11, 1, 2, CH[0]); p.rect(10, 11, 1, 2, CH[0])
    p.rim(only=WOOD)
    return p

def old_car():
    p = P(66, 32, 3)
    body = lambda x, y: ((x + 0.5 - 33) / 32.5) ** 6 + ((y + 0.5 - 16) / 13) ** 4 <= 1
    for x, y in ((12, 2), (12, 28), (50, 2), (50, 28)):                                   # tyres
        p.rect(x - 4, y - 2, 9, 4, D0)
    p.grad(1, 3, 64, 26, [HP2, HP1, HP3, HP4], mask=body)                               # hot pink paint
    p.outline()
    p.grad(36, 7, 11, 18, [C('#2a0b4a'), PU1, SUN[4], SUN[2]], vertical=False,          # windscreen, the sunset in it
           mask=lambda x, y: abs(y - 16) <= 6 + (x - 36) * 0.35)
    p.rect(22, 8, 14, 16, HP3); p.rect(22, 8, 14, 1, HP2); p.frame(22, 8, 14, 16, HP4)   # roof
    p.grad(14, 9, 8, 14, [D2, PU1], vertical=False)                                        # rear window
    p.rect(60, 8, 3, 4, YEL); p.rect(60, 20, 3, 4, YEL)                                    # pop-up lights
    p.rect(2, 7, 2, 18, HP1); p.rect(2, 7, 2, 1, WHT)                                       # tail lights
    p.line(6, 15, 58, 15, HP4); p.line(6, 17, 58, 17, HP4)                                  # vents along the body
    p.rect(30, 2, 3, 2, CH[1]); p.rect(30, 28, 3, 2, CH[1])                                 # mirrors
    p.glow([YEL], 2, 90); p.underglow(CY1, 3, 85)
    return p

def old_chair():
    p = P(12, 12, 1)
    p.chrome(0, 0, 12, 2); p.frame(0, 0, 12, 2)
    p.box(1, 2, 10, 9, HP3, HP1, HP4)
    p.rect(0, 2, 1, 9, CH[2]); p.rect(11, 2, 1, 9, CH[2])
    p.outline()
    return p

def old_coffee_table():
    p = P(30, 18, 1)
    p.rect(0, 0, 30, 18, CH[2]); p.frame(0, 0, 30, 18)
    p.grad(1, 1, 28, 16, [C('#1c3a4a'), CY4, C('#16303e')])                                 # smoked glass
    p.line(3, 13, 9, 3, CY3); p.line(5, 13, 11, 3, C('#1c4a5a'))
    p.box(14, 4, 9, 7, SOFT, WHT, SOFT2); p.rect(15, 5, 7, 3, HP1); p.rect(15, 9, 5, 1, DIM)  # magazine
    p.disc(25, 13, 1.8, INK); p.disc(25, 13, 1.1, CY1)                                       # a lit coaster
    p.bloom([CY1], 1.5, 0.35)
    return p

def old_couch():
    p = P(50, 22, 3)
    p.box(0, 0, 50, 22, D2, D3, D1)
    p.rect(1, 1, 48, 5, D1)
    for x in range(5, 46, 4):
        p.set(x, 3, D0); p.set(x + 1, 2, D3)
    p.rect(1, 1, 5, 20, D1); p.rect(44, 1, 5, 20, D1)
    for x0 in (6, 19, 32):
        p.rect(x0, 6, 12, 14, D3); p.rect(x0, 6, 12, 1, D4); p.line(x0 + 2, 8, x0 + 6, 8, D4); p.frame(x0, 6, 12, 14, D0)
    p.box(8, 7, 6, 5, HP3, HP1, HP4, out=D0); p.box(37, 8, 6, 5, CY3, CY1, CY4, out=D0)
    p.rim(); p.underglow(MG, 2, 60)
    return p

def old_counter():
    p = P(66, 16, 2)
    p.box(0, 0, 66, 16, D1, D2, D0)
    for x0, y0, x1, y1 in ((3, 3, 8, 5), (44, 9, 50, 11), (56, 4, 60, 6)):
        p.line(x0, y0, x1, y1, PU1)
    p.rect(4, 2, 18, 11, D0)                                                                 # cooktop
    for x, y, on in ((8.5, 5.5, True), (16.5, 5.5, False), (8.5, 10.5, False), (16.5, 10.5, False)):
        if on:
            p.ring(x, y, 2.6, HP1); p.disc(x, y, 1.3, SUN[2])
        else:
            p.ring(x, y, 2.6, C('#2c2546'))
    p.box(36, 2, 16, 11, CH[2], CH[1], CH[3])                                               # sink
    p.rect(38, 4, 12, 7, CH[3]); p.set(44, 8, D0); p.rect(43, 2, 2, 2, CH[0])
    p.rect(1, 14, 64, 1, CY1)                                                                # LED along the front
    p.bloom([HP1, SUN[2]], 2, 0.4); p.glow([CY1], 2, 75); p.rim(only={D1, D2})
    return p

def old_desk():
    p = P(38, 20, 2)
    grain(p, 0, 0, 38, 20); p.frame(0, 0, 38, 20)
    p.box(4, 2, 12, 5, D0, D1, INK); p.rect(5, 6, 10, 1, CY1); p.rect(9, 7, 2, 1, CH[3])
    p.rect(4, 10, 14, 4, D0); p.grad(5, 11, 12, 2, [HP1, MG, PU2, CY1], vertical=False)
    p.box(24, 4, 9, 11, SOFT, WHT, SOFT2, out=SOFT2); p.line(25, 7, 31, 7, DIM); p.line(25, 9, 31, 9, DIM)
    p.disc(20, 4, 2, INK); p.disc(20, 4, 1.3, HP3)
    p.rim(only=WOOD); p.bloom([CY1], 2, 0.45)
    return p

def old_door():
    p = P(48, 8)
    grain(p, 0, 0, 48, 8); p.frame(0, 0, 48, 8)
    p.rect(2, 3, 44, 1, WD3)
    p.rect(40, 2, 4, 4, CH[1]); p.rect(41, 3, 2, 2, CH[0])                                   # handle at the free end
    p.rim(only=WOOD)
    return p

def old_dresser():
    p = P(30, 14, 1)
    grain(p, 0, 0, 30, 14); p.frame(0, 0, 30, 14)
    p.box(3, 3, 6, 5, CH[2], CH[1], CH[3]); p.rect(4, 4, 4, 3, PU1)                         # a photo frame
    p.box(12, 4, 3, 4, HP3, HP2, None)                                                      # perfume
    p.rect(9, 12, 3, 1, CH[0]); p.rect(18, 12, 3, 1, CH[0])
    p.rim(only=WOOD)
    return p

def old_fridge():
    p = P(18, 18, 1)
    p.grad(0, 0, 18, 18, [HP2, HP1, HP3], vertical=False); p.frame(0, 0, 18, 18)           # retro pink fridge
    p.rect(1, 1, 16, 1, C('#ffb0cf'))
    p.chrome(2, 13, 14, 2); p.frame(2, 13, 14, 2, CH[4])                                    # handle
    p.rect(4, 4, 3, 3, YEL); p.rect(11, 5, 3, 3, CY1); p.set(12, 6, WHT)                    # magnets
    return p

def old_nightstand():
    p = P(14, 14, 3)
    grain(p, 0, 0, 14, 14); p.frame(0, 0, 14, 14)
    p.disc(7, 7, 4, INK); p.disc(7, 7, 3.2, C('#ffcf7a')); p.disc(7, 7, 1.8, YEL); p.set(7, 7, WHT)  # lamp
    p.underglow(SUN[1], 2.5, 70); p.bloom([YEL], 2, 0.35); p.rim(only=WOOD)
    return p

def old_office_chair():
    p = P(14, 14, 1)
    p.disc(7, 7, 7, INK); p.disc(7, 7, 6, D1); p.disc(7, 8, 4.5, D2)
    p.rect(2, 2, 10, 3, D0); p.rect(3, 2, 8, 1, HP3)                                         # backrest, pink piping
    p.ring(7, 8, 4.5, HP4)
    p.rim(only={D1, D2})
    return p

def old_sink():
    p = P(16, 12, 1)
    p.box(0, 0, 16, 12, SOFT, WHT, SOFT2)
    p.ellipse(8, 6.5, 5.5, 3.8, SOFT2); p.ellipse(8, 7, 4.5, 3, C('#c8d8e8')); p.set(8, 7, CH[3])
    p.rect(7, 1, 2, 3, CH[1]); p.set(7, 1, WHT)
    p.rim(only={SOFT, WHT, SOFT2})
    return p

def old_stairs():
    p = P(32, 48)
    p.rect(0, 0, 32, 48, CON3)
    for y in range(0, 48, 4):                                                                 # steps, chrome nosing
        p.rect(0, y, 32, 4, CON2 if (y // 4) % 2 else CON3)
        p.rect(0, y, 32, 1, CH[2])
    p.rect(9, 0, 14, 48, HP4)                                                                  # the runner
    for y in range(0, 48, 4):
        p.rect(9, y, 14, 1, HP3); p.rect(9, y + 1, 14, 2, C('#5e0a2c'))
    p.rect(9, 0, 1, 48, HP3); p.rect(22, 0, 1, 48, HP3)
    for y in range(2, 48, 8):                                                                  # step lights
        p.set(1, y, CY1); p.set(30, y, CY1)
    p.frame(0, 0, 32, 48)
    return p

def old_table():
    p = P(34, 26, 2)
    grain(p, 0, 0, 34, 26); p.frame(0, 0, 34, 26)
    p.rect(1, 1, 32, 1, WD3)
    for x, y in ((8, 7), (26, 7), (8, 19), (26, 19)):
        plate(p, x, y)
    p.disc(17, 13, 1.6, SOFT); p.set(17, 12, YEL)
    p.bloom([YEL], 3, 0.45); p.rim(only=WOOD)
    return p

def old_toilet():
    p = P(14, 18, 1)
    p.box(1, 0, 12, 6, SOFT, WHT, SOFT2); p.rect(6, 1, 2, 1, CH[1])                          # tank and flush
    p.ellipse(7, 11.5, 6, 6, INK); p.ellipse(7, 11.5, 5, 5, SOFT); p.ellipse(7, 12, 3.2, 3.5, C('#c8d8e8'))
    p.rim(only={SOFT, WHT, SOFT2})
    return p

def old_tv():
    p = P(34, 14, 2)
    grain(p, 0, 4, 34, 10); p.frame(0, 4, 34, 10)
    p.box(3, 0, 28, 5, D0, D1, INK)                                                           # slim TV
    p.rect(4, 4, 26, 1, CY1)
    p.box(23, 8, 8, 4, D0, D2, INK); p.set(24, 10, HP1)                                        # a console
    p.rect(5, 9, 6, 3, CH[3]); p.rect(6, 10, 4, 1, CH[1])                                      # remote
    p.rim(only=WOOD); p.bloom([CY1], 2, 0.4); p.glow([CY1], 1.5, 70)
    return p




# ---------------------------------------------------------------- the walls (levels/walls3.png)
# A wall seen from above is its cut-through: a dark outline, the two faces, and the core between them.
# Corners, junctions and wall ends get a square post, so every bend in a wall reads at a glance.
# Inner walls come in four real materials (the level says which wall is which):
#   row 0  drywall: white board faces, yellow insulation and wooden studs inside
#   row 3  concrete block: painted grey faces, block joints and hollow cores (bathrooms, kitchens)
#   row 4  painted brick: white faces, white-washed brick with grey mortar
#   row 5  timber: wooden faces with grain round a darker core
# Row 1 is the outer walls (brick between plaster faces); row 2, column 0 the drop shadow under walls.
N, E, S, W = 1, 2, 4, 8
CYAN_LIT, PINK_LIT = C('#a8f8fa'), C('#ff6fa8')

def drywall_core(x, y, mask):
    c = C('#e6c050') if (x + 2 * y) % 5 else C('#d2a63c')
    if (mask & (E | W) and x % 4 == 0 and 3 <= y <= 4) or (mask & (N | S) and y % 4 == 0 and 3 <= x <= 4):
        c = C('#9a6a44')                                             # studs every 4 px along the wall
    return c

def block_core(x, y, mask):
    along = x if mask & (E | W) else y
    if along % 4 == 3:
        return C('#6e6a76')                                          # joint between blocks
    return C('#57535f') if along % 4 == 1 and 3 <= (y if mask & (E | W) else x) <= 4 else C('#8e8a96')  # hollow cores

def brick_core(x, y, mask):
    row = y // 4
    if y % 4 == 3 or (x + 4 * row) % 8 == 0:
        return C('#aaa4b2')                                          # grey mortar
    return C('#e2dce6') if (x // 4 + y) % 3 else C('#d4cdd9')

def timber_core(x, y, mask):
    along = x if mask & (E | W) else y
    return C('#6e4428') if along % 5 else C('#58361f')

INNER = [  # (row, faces, core, post frame, post core)
    (0, C('#e4e0e8'), drywall_core, C('#d2ccd8'), C('#3a3346')),
    (3, C('#bdb9c4'), block_core, C('#aaa6b2'), C('#46424e')),
    (4, C('#ece8ee'), brick_core, C('#d8d2de'), C('#4a4452')),
    (5, C('#a0683e'), timber_core, C('#8e5a34'), C('#3e2616')),
]

def face(x, y, mask, colour):
    """Open sides: the outline, then two pixels of face (lit cyan on top and left, pink on bottom and right)."""
    sides = [(y, N, CYAN_LIT, 0.2), (x, W, CYAN_LIT, 0.2), (7 - y, S, PINK_LIT, 0.14), (7 - x, E, PINK_LIT, 0.14)]
    for dist, side, lit, k in sides:
        if not mask & side and dist == 0:
            return INK
    for dist, side, lit, k in sides:
        if not mask & side and dist <= 2:
            return mix(colour, lit, k)
    return None

def post(x, y, frame, core):
    """A square post on a corner, junction or end: an outlined frame round a dark core."""
    if 1 <= x <= 6 and 1 <= y <= 6:
        if x in (1, 6) or y in (1, 6):
            return INK
        if x in (2, 5) or y in (2, 5):
            return frame
        return core if (x + y) % 2 else mix(core, frame, 0.3)
    return None

def inner(mask, faces, core, frame, post_core):
    t = Image.new('RGBA', (8, 8))
    straight = mask in (N | S, E | W)
    for y in range(8):
        for x in range(8):
            c = face(x, y, mask, faces) or core(x, y, mask)
            if not straight:
                c = post(x, y, frame, post_core) or c
            t.putpixel((x, y), c)
    return t

def outer(mask):
    t = Image.new('RGBA', (8, 8))
    for y in range(8):
        for x in range(8):
            row = y // 4                                             # two courses of brick a tile, offset by half
            if y % 4 == 3 or (x + 4 * row) % 8 == 0:
                c = C('#4a2830')
            else:
                c = C('#b45a4e') if y % 4 == 0 else C('#8e3a36') if ((x + 4 * row) // 8 + row) % 2 == 0 else C('#9e4640')
            t.putpixel((x, y), face(x, y, mask, C('#ddd6e4')) or c)
    return t

def wall_sheet():
    """16 columns, one per neighbour mask (a wall to the north 1, east 2, south 4, west 8); rows as above."""
    sheet = Image.new('RGBA', (128, 48))
    for m in range(16):
        for row, faces, core, frame, post_core in INNER:
            sheet.alpha_composite(inner(m, faces, core, frame, post_core), (m * 8, row * 8))
        sheet.alpha_composite(outer(m), (m * 8, 8))
    sheet.paste((0, 0, 0, 120), (0, 16, 8, 24))
    return sheet


# ---------------------------------------------------------------- the floors (levels/floors3.png)
# 16 px tiles that repeat seamlessly. Rows 0 and 1: the street and the first floors, kept as they are.
# Rows 2 and 3: room floors, each its own strong colour, so neighbouring rooms never look alike.
def noise(t, base, specks, chance=0.35):
    for y in range(16):
        for x in range(16):
            t.putpixel((x, y), random.choice(specks) if random.random() < chance else base)

def planks(colours, seam, joints):
    """Four boards across the tile, each with its end joint somewhere else."""
    t = Image.new('RGBA', (16, 16))
    for b in range(4):
        base = colours[b % len(colours)]
        for y in range(b * 4, b * 4 + 4):
            for x in range(16):
                c = base
                if y == b * 4 + 3 or x == joints[b]:
                    c = seam
                elif random.random() < 0.12:
                    c = mix(base, seam, 0.35)
                t.putpixel((x, y), c)
    return t

def parquet(light, dark, seam):
    """Basket weave: 8 px squares of strips, turning a quarter turn from one square to the next."""
    t = Image.new('RGBA', (16, 16))
    for y in range(16):
        for x in range(16):
            across = ((x // 8) + (y // 8)) % 2 == 0
            along = y if across else x
            c = light if (along // 2) % 2 == 0 else dark
            if x % 8 == 7 or y % 8 == 7:
                c = seam
            t.putpixel((x, y), c)
    return t

def tiles(size, colours, grout, shine=None):
    t = Image.new('RGBA', (16, 16))
    for y in range(16):
        for x in range(16):
            k = (x // size + y // size * 3) % len(colours)
            c = grout if x % size == size - 1 or y % size == size - 1 else colours[k]
            if shine and x % size == 0 and y % size == 0:
                c = shine
            t.putpixel((x, y), c)
    return t

def carpet(base, dark, light):
    t = Image.new('RGBA', (16, 16))
    noise(t, base, [dark, dark, light], 0.4)
    return t

def stone():
    t = Image.new('RGBA', (16, 16))
    noise(t, C('#e0d4b4'), [C('#d2c5a2'), C('#ece2c6')], 0.3)
    for i in range(16):
        t.putpixel((15, i), C('#b0a280')); t.putpixel((i, 15), C('#b0a280'))
    return t

def velvet():
    t = Image.new('RGBA', (16, 16))
    noise(t, C('#4e2a7a'), [C('#3e1e66'), C('#6a3a9a')], 0.45)
    for _ in range(5):
        t.putpixel((random.randrange(16), random.randrange(16)), C('#b05aa8'))
    return t

def concrete():
    t = Image.new('RGBA', (16, 16))
    noise(t, C('#8e8a96'), [C('#827e8c'), C('#9a96a2')], 0.35)
    t.putpixel((4, 11), C('#6e6a78')); t.putpixel((5, 11), C('#6e6a78')); t.putpixel((11, 3), C('#a6a2ae'))
    return t

FLOORS = [   # row 2, then row 3, left to right
    ('oak', lambda: planks([C('#c8955a'), C('#bd8a50'), C('#cf9d63')], C('#8a5e34'), (5, 13, 1, 9))),
    ('honey', lambda: planks([C('#d6833e'), C('#c9762f'), C('#e09050')], C('#8e4a1e'), (11, 3, 8, 14))),
    ('cherry', lambda: planks([C('#8e3e2e'), C('#9c4a36'), C('#82352a')], C('#4e1c18'), (2, 10, 6, 13))),
    ('parquet', lambda: parquet(C('#b8804c'), C('#9c6638'), C('#6e4422'))),
    ('red carpet', lambda: carpet(C('#a82a3c'), C('#962234'), C('#ba384a'))),
    ('purple carpet', lambda: carpet(C('#7a2e8e'), C('#6a2480'), C('#8c3aa0'))),
    ('blue carpet', lambda: carpet(C('#2c4aa0'), C('#24408e'), C('#3a58b4'))),
    ('teal carpet', lambda: carpet(C('#1f8090'), C('#18707e'), C('#2a92a4'))),
    ('pink tiles', lambda: tiles(8, [C('#f0a0c4'), C('#ea96bc')], C('#c87aa0'), C('#fbc4dc'))),
    ('pool tiles', lambda: tiles(4, [C('#6ad0ec'), C('#58c0e0'), C('#7ad8f0')], C('#3a98bc'))),
    ('checker', lambda: tiles(8, [C('#1e1a26'), C('#ece8ee')], C('#8a8494'))),
    ('terracotta', lambda: tiles(8, [C('#c86a40'), C('#b85e38')], C('#7e3e24'), C('#d8804e'))),
    ('cream stone', stone),
    ('lavender lino', lambda: carpet(C('#a59ad0'), C('#9488c0'), C('#b6acdc'))),
    ('purple velvet', velvet),
    ('polished concrete', concrete),
]

def floor_sheet(old):
    sheet = Image.new('RGBA', (128, 64))
    sheet.alpha_composite(old.crop((0, 0, 128, 32)), (0, 0))
    for i, (name, fn) in enumerate(FLOORS):
        random.seed(name)
        sheet.alpha_composite(fn(), ((i % 8) * 16, 32 + (i // 8) * 16))
    return sheet


PROPS = {
    # the ones the levels already use
    'armchair': old_armchair, 'bathtub': old_bathtub, 'bed': old_bed, 'box': old_box, 'cabinet': old_cabinet,
    'car': old_car, 'chair': old_chair, 'coffee_table': old_coffee_table, 'couch': old_couch, 'counter': old_counter,
    'desk': old_desk, 'door': old_door, 'dresser': old_dresser, 'fridge': old_fridge, 'nightstand': old_nightstand,
    'office_chair': old_office_chair, 'plant': plant, 'sink': old_sink, 'stairs': old_stairs, 'table': old_table,
    'toilet': old_toilet, 'tv': old_tv,
    # living room
    'velvet_sectional': sofa, 'bean_bag': bean_bag, 'vhs_shelf': vhs_shelf, 'floor_lamp': floor_lamp,
    'record_player': stereo, 'aquarium': aquarium,
    # bedroom
    'sunset_bed': sunset_bed, 'single_bed': single_bed, 'wardrobe': wardrobe, 'vanity': vanity,
    # kitchen and dining
    'round_table': round_table, 'stove': stove, 'kitchen_island': kitchen_island, 'trash_bins': trash_bins,
    # bathroom and laundry
    'shower': shower, 'washer_dryer': washer_dryer, 'clawfoot_tub': clawfoot_tub,
    # office
    'corner_desk': corner_desk, 'filing_cabinets': filing_cabinets, 'safe': safe, 'copier': copier,
    # bar and den
    'pool_table': pool_table, 'bar_counter': bar_counter, 'jukebox': jukebox, 'arcade': arcade, 'weight_bench': weight_bench,
    # rugs
    'rug_sunset': sunset_rug, 'rug_sun': sun_round_rug, 'rug_runner': runner, 'rug_tiger': tiger_rug,
    'rug_shag': shag_rug, 'rug_memphis': memphis_rug,
    # walls, windows and corners
    'window': night_window, 'radiator': radiator, 'pillar': pillar, 'glass_partition': glass_partition,
    # clutter
    'pizza_boxes': pizza_boxes, 'duffel_bag': duffel_bag, 'keytar': keytar,
}

if __name__ == '__main__':
    names = sys.argv[1:] or list(PROPS)
    for name in names:
        random.seed(name)                     # each prop draws the same every time, whatever else is built
        PROPS[name]().im.save(os.path.join(PROJECT, 'props', name + '.png'))
    if not sys.argv[1:]:
        wall_sheet().save(os.path.join(PROJECT, 'levels', 'walls3.png'))
        floors = os.path.join(PROJECT, 'levels', 'floors3.png')
        floor_sheet(Image.open(floors).convert('RGBA')).save(floors)
    print(len(names), 'props' + ('' if sys.argv[1:] else ', the walls and the floors'))

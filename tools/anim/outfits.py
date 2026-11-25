"""Makes extra enemy outfits by recolouring an existing enemy's hand-drawn sprites (art/anim/<name>/<name>_*.png).

    python tools/anim/outfits.py     then     python tools/anim/build.py
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pngio import read_png, png
import headband as hb

ART = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'art', 'anim') + '/'
PARTS = ('armed', 'unarmed', 'legs', 'dead', 'dead_face', 'dead_side')

# new outfit: (made from, {old colour: new colour}). No green: green is the crosshair's.
OUTFITS = {
    'enemy_c': ('enemy_b', {  # red tracksuit, white trim, black hair
        '4a3328': 'b8282e', '2a1c15': '6e1418', '26262e': 'e6e2d8', 'd9b45a': '1c1418', 'f2d98a': '3a2e30',
        '7a5644': '2a2024', '121620': '6e1418', '1e2433': '9e2228', 'b07a58': '8a5a3c', 'e0a882': 'b07a58'}),
    'enemy_d': ('enemy_b', {  # cream suit, dark purple shirt, slicked brown hair
        '4a3328': 'd8d4c8', '2a1c15': '9a968c', '26262e': '2a1c3a', 'd9b45a': '5a3a2a', 'f2d98a': '7a5240',
        '7a5644': '3a2418', '121620': 'a8a49a', '1e2433': 'cfcabe'}),
    'riot': ('enemy_a', {  # the shield enemy: the usual enemy in a black helmet
        '3b2e28': '22252e', '5a463c': '8a96aa'}),
    'dodger': ('enemy_a', {}),  # the usual enemy in a red headband (headband() below; its tails: headband.py)
}
# The riot outfit's visor: a band of blue glass along the helmet's front edge, where it meets the face (standing only).
HELMET, FACE, VISOR = '22252e', ('c98e6a', '9a6448'), (110, 170, 210, 255)
HAIR = ('3b2e28', '5a463c')

hexc = lambda c: '%02x%02x%02x' % c[:3]


def headband(out, part):
    """The dodger's headband on enemy_a's head. From above only its knot at the back of the head (the band round
    the hair is drawn once the head is turned: headband.ring). On his back: across the forehead. Face down: across
    the back of the head, the knot in the middle."""
    hair = lambda x, y: out[y][x][3] and hexc(out[y][x]) in HAIR
    spots = [(x, y) for y in range(1, len(out) - 1) for x in range(1, len(out[0]) - 1) if hair(x, y)]
    if part == 'dead_face':
        mid = round(sum(x for x, y in spots) / len(spots))
        band = [(x, y) for x, y in spots if x == mid]
        knot = [min(band, key=lambda p: abs(p[1] - sum(y for x, y in band) / len(band)))]
    else:
        skin = lambda x, y: hexc(out[y][x]) in FACE
        edge = (lambda x, y: any(not hair(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))) \
            if part in ('armed', 'unarmed') else \
            (lambda x, y: any(skin(x + dx, y + dy) for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))))
        band = [(x, y) for x, y in spots if edge(x, y)]
        mid_y = sum(y for x, y in spots) / len(spots)
        knot = [p for p in band if p[0] == min(q[0] for q in band) and abs(p[1] - mid_y) < 1.5] if part in ('armed', 'unarmed') else []
    for x, y in band if part not in ('armed', 'unarmed') else ():
        out[y][x] = hb.RED
    for x, y in knot:
        out[y][x] = hb.RED_DARK
for name, (base, swap) in OUTFITS.items():
    for part in PARTS:
        w, h, px = read_png(f'{ART}{base}/{base}_{part}.png')
        out = [[tuple(int(swap[hexc(c)][i:i + 2], 16) for i in (0, 2, 4)) + (c[3],) if c[3] and hexc(c) in swap else c for c in row] for row in px]
        if name == 'riot' and part == 'unarmed':
            edge = [(x, y) for y in range(23, 36) for x in range(24, 40)
                    if hexc(out[y][x]) == HELMET and hexc(out[y][x + 1]) in FACE]
            for x, y in edge:
                out[y][x] = VISOR
        if name == 'dodger' and part != 'legs':
            headband(out, part)
        os.makedirs(f'{ART}{name}', exist_ok=True)
        png(f'{ART}{name}/{name}_{part}.png', w, h, out)
    print(name, 'from', base)

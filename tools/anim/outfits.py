"""Makes extra goon outfits by recolouring an existing goon's hand-drawn sprites (art/anim/<name>/<name>_*.png).

    python tools/anim/outfits.py     then     python tools/anim/build.py
"""
import os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pngio import read_png, png

ART = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'art', 'anim') + '/'
PARTS = ('armed', 'unarmed', 'legs', 'dead', 'dead_face', 'dead_side')

# new outfit: (made from, {old colour: new colour}). No green: green is the crosshair's.
OUTFITS = {
    'goon_c': ('goon_b', {  # red tracksuit, white trim, black hair
        '4a3328': 'b8282e', '2a1c15': '6e1418', '26262e': 'e6e2d8', 'd9b45a': '1c1418', 'f2d98a': '3a2e30',
        '7a5644': '2a2024', '121620': '6e1418', '1e2433': '9e2228', 'b07a58': '8a5a3c', 'e0a882': 'b07a58'}),
    'goon_d': ('goon_b', {  # cream suit, dark purple shirt, slicked brown hair
        '4a3328': 'd8d4c8', '2a1c15': '9a968c', '26262e': '2a1c3a', 'd9b45a': '5a3a2a', 'f2d98a': '7a5240',
        '7a5644': '3a2418', '121620': 'a8a49a', '1e2433': 'cfcabe'}),
}

hexc = lambda c: '%02x%02x%02x' % c[:3]
for name, (base, swap) in OUTFITS.items():
    for part in PARTS:
        w, h, px = read_png(f'{ART}{base}/{base}_{part}.png')
        out = [[tuple(int(swap[hexc(c)][i:i + 2], 16) for i in (0, 2, 4)) + (c[3],) if c[3] and hexc(c) in swap else c for c in row] for row in px]
        os.makedirs(f'{ART}{name}', exist_ok=True)
        png(f'{ART}{name}/{name}_{part}.png', w, h, out)
    print(name, 'from', base)

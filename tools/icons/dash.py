"""The dash's ability icon: 14x14, one still picture, in art/icons/dash.png (the HUD dims it and writes the
seconds over it while it recharges). Run: py -I tools/icons/dash.py [a|b] [scale] [out]"""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'anim'))
from pngio import png

SIZE = 14
SOFT, CYAN, PINK = (216, 210, 232, 255), (45, 226, 230, 255), (255, 42, 109, 255)

RUNNER = """
..............
..............
.........###..
.........###..
.....#####....
....#.#####.#.
###...####.#..
......####....
#####.##.##...
.....##...##..
###.##.....#..
...##......##.
..............
..............
"""
FIGURE = """
....##
....##
..####
.#.##.
...##.
..#.#.
.#...#
#.....
"""


def runner():
    """A: a runner leaning into it, speed lines behind (pink in the middle, cyan around)."""
    px = [[None] * SIZE for _ in range(SIZE)]
    for y, line in enumerate(RUNNER.split()):
        for x, c in enumerate(line):
            if c == '#':
                px[y][x] = SOFT if x > 3 else (PINK if y == 8 else CYAN)
    return px


def trail():
    """B: the dash's own afterimages: the figure three times, pink fading to cyan, then himself."""
    px = [[None] * SIZE for _ in range(SIZE)]
    shape = [(x, y) for y, line in enumerate(FIGURE.split()) for x, c in enumerate(line) if c == '#']
    for left, colour in ((0, PINK), (4, CYAN), (8, SOFT)):
        for x, y in shape:
            px[y + 3][x + left] = colour  # later ones over earlier
    return px


style = sys.argv[1] if len(sys.argv) > 1 else 'a'
scale = int(sys.argv[2]) if len(sys.argv) > 2 else 1
out = sys.argv[3] if len(sys.argv) > 3 else os.path.join(os.path.dirname(__file__), '..', '..', 'art', 'icons', 'dash.png')
f = runner() if style == 'a' else trail()
back = (7, 3, 15, 255) if scale > 1 else (0, 0, 0, 0)
rows = []
for y in range(SIZE):
    row = []
    for x in range(SIZE):
        row += [f[y][x] or back] * scale
    rows += [row] * scale
png(out, SIZE * scale, SIZE * scale, rows)

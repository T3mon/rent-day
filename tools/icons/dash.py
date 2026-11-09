"""The dash icon: a 4-frame run cycle, 14x14 each, side by side in art/icons/dash.png. A pale runner with
cyan and pink speed lines streaming past. Run: py -I tools/icons/dash.py [scale] [out]"""
import os, sys
sys.path.insert(0, os.path.join(os.path.dirname(__file__), '..', 'anim'))
from pngio import png

STRIDE = """
.........###..
.........###..
.....#####....
....#.#####.#.
......####.#..
......####....
......##.##...
.....##...##..
....##.....#..
...##......##.
"""
PASS = """
..............
.........###..
.........###..
.......####...
......#####...
.....#.####.#.
.......###.#..
.......#####..
.......#...#..
......##...#..
"""
SIZE, TOP = 14, 2
SOFT, CYAN, PINK = (216, 210, 232, 255), (45, 226, 230, 255), (255, 42, 109, 255)
LINES = [(4, 3), (6, 5), (8, 3)]  # (row, length) of the speed lines behind him


def frame(pose, shift):
    px = [[None] * SIZE for _ in range(SIZE)]
    for row, length in LINES:  # streaming left, wrapping in the 6 px behind him
        for i in range(length):
            x = (i - shift + 6) % 6
            if x < 5:
                px[TOP + row][x] = CYAN if row != 6 else PINK
    body = [(x, y + TOP) for y, line in enumerate(pose.split()) for x, c in enumerate(line) if c == '#']
    for x, y in body:
        px[y][x] = SOFT
    return px


frames = [frame(p, s) for s, p in enumerate([STRIDE, PASS, STRIDE, PASS])]
scale = int(sys.argv[1]) if len(sys.argv) > 1 else 1
out = sys.argv[2] if len(sys.argv) > 2 else os.path.join(os.path.dirname(__file__), '..', '..', 'art', 'icons', 'dash.png')
back = (7, 3, 15, 255) if scale > 1 else (0, 0, 0, 0)
rows = []
for y in range(SIZE):
    row = []
    for f in frames:
        for x in range(SIZE):
            row += [f[y][x] or back] * scale
        row += [back] * scale * (scale > 1)  # a gap between frames in the preview
    rows += [row] * scale
png(out, len(rows[0]), len(rows), rows)

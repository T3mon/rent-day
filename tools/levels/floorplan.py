"""Rent Day level builder: a floor plan in, a level scene out. Run build.py, which holds the levels.

A floor is drawn as room rectangles (px, on the 8 px wall grid) with 8 px gaps between them. The gaps become
inner walls, and a 16 px band round the lot becomes the outer wall. Openings, doors and glass are carved into
those walls. Wall pieces (neighbour masks), wall materials, floor tiles, shadows and windows are worked out
from that; furniture, stairs, weapons and enemies are placed as given. check() lists what breaks the level
rules, stats() measures the floor, svg() draws it.
"""
import base64, struct, math, os, re
from collections import deque, Counter

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))  # the repo, two up from here
C, T = 8, 16           # wall cell, floor tile
SIGHT, SCREEN = 183, 400 * 225

MAT = dict(oak=(0, 2), honey=(1, 2), cherry=(2, 2), parquet=(3, 2), red=(4, 2), purple=(5, 2), blue=(6, 2),
           teal=(7, 2), pink=(0, 3), pool=(1, 3), checker=(2, 3), terracotta=(3, 3), stone=(4, 3), lino=(5, 3),
           velvet=(6, 3), concrete=(7, 3))
TILED = {'pink', 'pool', 'checker', 'terracotta', 'stone'}
WOOD = {'oak', 'honey', 'cherry', 'parquet'}
DRYWALL, BRICK, SHADOW, BLOCK, PAINTED, TIMBER = 0, 1, 2, 3, 4, 5
ASPHALT, SIDEWALK, STREET = (0, 0), (1, 0), (0, 1)

POSE = dict(none=0, smoke=1, phone=2, hide=3, sit=4, lean=5, talk=6, eat=7, toilet=8, drink=9, pee=10)
ROUTINE = dict(static=0, patrol=1, wander=2, wall=3)
KINDS = {  # scene, default routine, default weapon
    'enemy': ('res://enemies/enemy.tscn', 'static', 'pistol'),
    'dog': ('res://enemies/dog.tscn', 'wall', None),
    'heavy': ('res://enemies/heavy.tscn', 'patrol', None),
    'dodger': ('res://enemies/dodger.tscn', 'static', 'knife'),
    'shield': ('res://enemies/shield_enemy.tscn', 'static', 'pistol'),
}
LOOKS = {'b': ('uid://u6tef2csv2e', 'res://art/anim/enemy_b.tres'), 'c': ('uid://caxniidqnw0hv', 'res://art/anim/enemy_c.tres'),
         'd': ('uid://c7hl8t1v25fg4', 'res://art/anim/enemy_d.tres')}
MELEE = {'bat', 'knife'}
ROT = {0: None, 90: '1.570796', 180: '3.141593', 270: '-1.570796'}


def prop_shapes():
    """Each prop's collision rectangles at rotation 0, as (dx, dy, w, h), read from props/*.tscn."""
    out = {}
    for f in sorted(os.listdir(f'{ROOT}/props')):
        if not f.endswith('.tscn'):
            continue
        sections = re.split(r'\n(?=\[)', open(f'{ROOT}/props/{f}').read())
        sizes, rects = {}, []
        for s in sections:
            head = s.split('\n', 1)[0]
            m = re.match(r'\[sub_resource type="RectangleShape2D" id="([^"]+)"', head)
            if m:
                w, h = map(float, re.search(r'size = Vector2\(([^)]+)\)', s).group(1).split(','))
                sizes[m.group(1)] = (w, h)
            if 'type="CollisionShape2D"' in head:
                sh = re.search(r'shape = SubResource\("([^"]+)"\)', s)
                pos = re.search(r'position = Vector2\(([^)]+)\)', s)
                if sh and sh.group(1) in sizes:
                    dx, dy = map(float, pos.group(1).split(',')) if pos else (0.0, 0.0)
                    rects.append((dx, dy) + sizes[sh.group(1)])
        out[f[:-5]] = rects
    return out


def png_size(name):
    p = f'{ROOT}/props/{name}.png'
    if not os.path.exists(p):
        return (16, 16)
    return struct.unpack('>II', open(p, 'rb').read(24)[16:24])


SHAPES = prop_shapes()


def turned(dx, dy, w, h, rot):
    """A rect (centre offset, size) turned by rot degrees (multiples of 90, clockwise on screen)."""
    for _ in range((rot // 90) % 4):
        dx, dy, w, h = -dy, dx, h, w
    return dx, dy, w, h


def rect_of(x, y, dx, dy, w, h):
    return (x + dx - w / 2, y + dy - h / 2, x + dx + w / 2, y + dy + h / 2)


def overlap(a, b, grow=0.0):
    return a[0] - grow < b[2] and b[0] - grow < a[2] and a[1] - grow < b[3] and b[1] - grow < a[3]


def circle_hits(cx, cy, r, b):
    nx, ny = min(max(cx, b[0]), b[2]), min(max(cy, b[1]), b[3])
    return (nx - cx) ** 2 + (ny - cy) ** 2 < r * r


def encode(cells):
    out = bytearray(struct.pack('<H', 0))
    for (x, y), (ax, ay) in sorted(cells.items(), key=lambda kv: (kv[0][1], kv[0][0])):
        out += struct.pack('<hhHHHH', x, y, 0, ax, ay, 0)
    return base64.b64encode(bytes(out)).decode()


def near(cells, r):
    """Every cell within Chebyshev distance r of `cells`."""
    out = set()
    for (x, y) in cells:
        for dy in range(-r, r + 1):
            for dx in range(-r, r + 1):
                out.add((x + dx, y + dy))
    return out


def shrink(cells, r):
    return {c for c in cells if all((c[0] + dx, c[1] + dy) in cells for dx in range(-r, r + 1) for dy in range(-r, r + 1))}


class Room:
    def __init__(self, name, kind, mat, rects):
        self.name, self.kind, self.mat, self.rects = name, kind, mat, rects


class Floor:
    def __init__(self, file, root, ground=False):
        self.file, self.root, self.ground = file, root, ground
        self.rooms, self.cuts, self.doors, self.panes = [], [], [], []
        self.props, self.enemies, self.weapons = [], [], []
        self.up = self.down = self.player = None
        self.problems = []

    # --- drawing the plan ---
    def room(self, name, kind, mat, *rects):
        assert mat in MAT, mat
        self.rooms.append(Room(name, kind, mat, list(rects)))

    def gap_h(self, wy, x0, x1, thick=8):
        self.cuts.append(('gap', (x0, wy, x1, wy + thick)))

    def gap_v(self, wx, y0, y1, thick=8):
        self.cuts.append(('gap', (wx, y0, wx + thick, y1)))

    def door_h(self, wy, x, rot, L=40, thick=8):
        """A door in the horizontal wall wy..wy+thick, hinged at x, opening along +x (rot 0) or -x (180)."""
        y = wy + thick / 2
        self.cuts.append(('door', (x, wy, x + L, wy + thick) if rot == 0 else (x - L, wy, x, wy + thick)))
        self.doors.append((x, y, rot, L))

    def door_v(self, wx, y, rot, L=40, thick=8):
        """A door in the vertical wall wx..wx+thick, hinged at y, along +y (rot 90) or -y (270)."""
        x = wx + thick / 2
        self.cuts.append(('door', (wx, y, wx + thick, y + L) if rot == 90 else (wx, y - L, wx + thick, y)))
        self.doors.append((x, y, rot, L))

    def glass_h(self, wy, x0, x1, thick=8):
        assert (x1 - x0) % 32 == 0, (x0, x1)
        self.cuts.append(('glass', (x0, wy, x1, wy + thick)))
        self.panes += [(x, wy + thick / 2, 0) for x in range(x0 + 16, x1, 32)]

    def glass_v(self, wx, y0, y1, thick=8):
        assert (y1 - y0) % 32 == 0, (y0, y1)
        self.cuts.append(('glass', (wx, y0, wx + thick, y1)))
        self.panes += [(wx + thick / 2, y, 90) for y in range(y0 + 16, y1, 32)]

    def prop(self, scene, x, y, rot=0):
        assert scene in SHAPES, scene
        self.props.append((scene, x, y, rot % 360))

    def enemy(self, x, y, kind='enemy', face=0, act=None, pose='none', look=None, gun='', stay=False):
        self.enemies.append(dict(x=x, y=y, kind=kind, face=face % 360, act=act or KINDS[kind][1], pose=pose,
                                 look=look, gun=KINDS[kind][2] if gun == '' else gun, stay=stay))

    def weapon(self, w, x, y):
        self.weapons.append((w, x, y))

    def stairs(self, up=None, down=None):
        self.up, self.down = up, down
        if down:
            self.player = (down[0], down[1] - 41)

    def start(self, x, y):
        self.player = (x, y)

    # --- working it out ---
    def build(self):
        P = self.problems
        owner = {}
        for i, r in enumerate(self.rooms):
            for (x0, y0, x1, y1) in r.rects:
                if any(v % C for v in (x0, y0, x1, y1)):
                    P.append(f'{r.name}: rect {x0, y0, x1, y1} is off the 8 px grid')
                for cy in range(y0 // C, y1 // C):
                    for cx in range(x0 // C, x1 // C):
                        if owner.get((cx, cy), i) != i:
                            P.append(f'{r.name} overlaps {self.rooms[owner[(cx, cy)]].name} at {cx * C, cy * C}')
                        owner[(cx, cy)] = i
        rooms = set(owner)
        interior = shrink(near(rooms, 1), 1) | rooms
        inner = interior - rooms
        outer = near(interior, 2) - interior
        thick = [c for c in inner if all((c[0] + a, c[1] + b) in inner for a, b in ((1, 0), (0, 1), (1, 1)))]
        if thick:
            P.append(f'inner walls thicker than 8 px near {sorted(thick)[0][0] * C, sorted(thick)[0][1] * C}')
        opening = {}
        for kind, (x0, y0, x1, y1) in self.cuts:
            hit = 0
            for cy in range(int(y0) // C, -(-int(y1) // C)):
                for cx in range(int(x0) // C, -(-int(x1) // C)):
                    if (cx, cy) in inner or (cx, cy) in outer:
                        opening[(cx, cy)] = 'outer' if (cx, cy) in outer else kind
                        inner.discard((cx, cy)); outer.discard((cx, cy)); hit += 1
            if not hit:
                P.append(f'{kind} at {x0, y0, x1, y1} cuts no wall')
        self.owner, self.inner, self.outer, self.opening = owner, inner, outer, opening
        self.walls = walls = inner | outer
        # an opening's cells belong to the room beside them (for floor tiles and for joining rooms up)
        self.links = set()
        for c in opening:
            beside = [owner.get((c[0] + a, c[1] + b)) for a, b in ((1, 0), (-1, 0), (0, 1), (0, -1))]
            beside = sorted({b for b in beside if b is not None})
            if len(beside) >= 2:
                self.links.add(tuple(beside[:2]))
        # wall pieces and materials
        def mask(c):
            x, y = c
            return ((x, y - 1) in walls) | ((x + 1, y) in walls) << 1 | ((x, y + 1) in walls) << 2 | ((x - 1, y) in walls) << 3
        def pick(sides):
            sides = [self.rooms[i] for i in sides if i is not None]
            if any(r.mat in TILED for r in sides): return BLOCK
            if any(r.kind == 'hall' for r in sides): return DRYWALL
            if any(r.mat in WOOD for r in sides): return TIMBER
            return PAINTED
        mat = {}
        for c in inner:
            x, y = c
            h = (x + 1, y) in walls or (x - 1, y) in walls
            v = (x, y + 1) in walls or (x, y - 1) in walls
            if h and not v:
                mat[c] = pick([owner.get((x, y - 1)), owner.get((x, y + 1))])
            elif v and not h:
                mat[c] = pick([owner.get((x - 1, y)), owner.get((x + 1, y))])
        order = [BLOCK, DRYWALL, TIMBER, PAINTED]
        for c in inner:
            if c in mat:
                continue
            x, y = c
            votes = Counter(mat[n] for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)) if n in mat)
            if votes:
                best = max(votes.values())
                mat[c] = next(m for m in order if votes.get(m) == best)
            else:
                mat[c] = pick([owner.get((x + a, y + b)) for a in (-1, 0, 1) for b in (-1, 0, 1)])
        self.inner_tiles = {c: (mask(c), mat[c]) for c in inner}
        self.outer_tiles = {c: (mask(c), BRICK) for c in outer}
        self.shadow_tiles = {c: (0, SHADOW) for c in walls}
        # the map: building plus margin
        xs = [c[0] for c in walls]; ys = [c[1] for c in walls]
        bx0, bx1, by0, by1 = min(xs) * C, (max(xs) + 1) * C, min(ys) * C, (max(ys) + 1) * C
        self.bbox = (bx0, by0, bx1, by1)
        if self.ground:
            side, top, bottom = 80, 80, 96
        else:
            side = top = bottom = 48
        mx0, my0, mx1, my1 = bx0 - side, by0 - top, bx1 + side, by1 + bottom
        for v in (mx0, my0, mx1, my1):
            if v % T:
                P.append(f'map edge {v} is off the 16 px tile grid')
        self.map = (mx0, my0, mx1, my1)
        if mx0 < 0 or my0 < 0:
            P.append(f'map starts at {mx0, my0}, left of or above 0')
        floor = {}
        for ty in range(my0 // T, my1 // T):
            for tx in range(mx0 // T, mx1 // T):
                subs = [(2 * tx + a, 2 * ty + b) for b in (0, 1) for a in (0, 1)]
                mats = [self.rooms[owner[s]].mat for s in subs if s in owner]
                if mats:
                    floor[(tx, ty)] = MAT[Counter(mats).most_common(1)[0][0]]
                    continue
                if any(opening.get(s) == 'outer' for s in subs):
                    floor[(tx, ty)] = MAT['lino']  # the doorstep
                    continue
                if any(s in opening or s in inner for s in subs):
                    found = None
                    for r in range(1, 4):
                        for (a, b) in sorted(near({(0, 0)}, r), key=lambda d: abs(d[0]) + abs(d[1])):
                            for s in subs:
                                if (s[0] + a, s[1] + b) in owner:
                                    found = self.rooms[owner[(s[0] + a, s[1] + b)]].mat; break
                            if found: break
                        if found: break
                    floor[(tx, ty)] = MAT[found or 'concrete']
                    continue
                px, py = tx * T + 8, ty * T + 8
                if self.ground:
                    walk = bx0 - 48 <= px < bx1 + 48 and by0 - 48 <= py < by1 + 48
                    line = py // T == (by1 + 48) // T + 1
                    floor[(tx, ty)] = SIDEWALK if walk else (STREET if line and tx % 3 == 1 else ASPHALT)
                else:
                    floor[(tx, ty)] = ASPHALT
        self.floor_tiles = floor
        self.windows = self.place_windows()
        self.furniture, self.furniture_prop = [], []   # one entry per collision rect, and which prop it belongs to
        for i, (scene, x, y, rot) in enumerate(self.props):
            for s in SHAPES[scene]:
                self.furniture.append((scene, rect_of(x, y, *turned(*s, rot))))
                self.furniture_prop.append(i)
        self.pane_rects = [rect_of(x, y, *turned(0, 0, 32, 4, rot)) for (x, y, rot) in self.panes]

    def place_windows(self):
        out = []
        inside = set(self.owner) | {c for c, k in self.opening.items() if k != 'outer'}
        o = self.outer
        sides = {  # cell beside the wall, then the window centre for that cell
            'n': (lambda x, y: (x, y - 1) in o and (x, y - 2) in o, lambda x, y: (x * C + 4, y * C - 8), 0),
            's': (lambda x, y: (x, y + 1) in o and (x, y + 2) in o, lambda x, y: (x * C + 4, (y + 2) * C), 0),
            'w': (lambda x, y: (x - 1, y) in o and (x - 2, y) in o, lambda x, y: (x * C - 8, y * C + 4), 90),
            'e': (lambda x, y: (x + 1, y) in o and (x + 2, y) in o, lambda x, y: ((x + 2) * C, y * C + 4), 90),
        }
        for side, (test, centre, rot) in sides.items():
            cells = sorted((c for c in inside if c in self.owner and test(*c)),
                           key=lambda c: (c[1], c[0]) if rot == 0 else (c[0], c[1]))
            runs, run = [], []
            for c in cells:
                if run and (c[1] == run[-1][1] and c[0] == run[-1][0] + 1 if rot == 0 else c[0] == run[-1][0] and c[1] == run[-1][1] + 1):
                    run.append(c)
                else:
                    if run: runs.append(run)
                    run = [c]
            if run: runs.append(run)
            for run in runs:
                length = len(run) * C
                n = 0 if length < 56 else 1 + int((length - 56) // 110)
                for i in range(n):
                    k = (i + 0.5) * len(run) / n
                    a = centre(*run[int(k)])
                    off = (k - int(k)) * C - 4
                    out.append(((a[0] + off, a[1]) if rot == 0 else (a[0], a[1] + off), rot))
        return out

    # --- checks ---
    def obstacles(self):
        """Walls, furniture, glass and pillars, as rects: what an enemy has to route round."""
        rects = [(cx * C, cy * C, cx * C + C, cy * C + C) for (cx, cy) in self.walls]
        rects += [r for _, r in self.furniture] + self.pane_rects
        return rects

    def walkable(self):
        obs = self.obstacles()
        buckets = {}
        for r in obs:
            for bx in range(int(r[0]) // 32 - 1, int(r[2]) // 32 + 2):
                for by in range(int(r[1]) // 32 - 1, int(r[3]) // 32 + 2):
                    buckets.setdefault((bx, by), []).append(r)
        mx0, my0, mx1, my1 = self.map
        open_ = set()
        for cy in range(my0 // C, my1 // C):
            for cx in range(mx0 // C, mx1 // C):
                x, y = cx * C + 4, cy * C + 4
                if not (mx0 + 10 <= x <= mx1 - 10 and my0 + 10 <= y <= my1 - 10):
                    continue
                if not any(circle_hits(x, y, 10, r) for r in buckets.get((x // 32, y // 32), [])):
                    open_.add((cx, cy))
        return open_

    def check(self):
        P = self.problems
        names = {i: r.name for i, r in enumerate(self.rooms)}
        # rooms side by side never share a floor
        pairs = set()
        for (x, y), i in self.owner.items():
            for a, b in ((2, 0), (0, 2)):
                j = self.owner.get((x + a, y + b))
                if j is not None and j != i and ((x + a // 2, y + b // 2) in self.inner or (x + a // 2, y + b // 2) in self.opening):
                    pairs.add(tuple(sorted((i, j))))
        self.neighbours = pairs
        for i, j in pairs:
            if self.rooms[i].mat == self.rooms[j].mat:
                P.append(f'{names[i]} and {names[j]} share the floor {self.rooms[i].mat}')
        # door swings: nothing within the door's reach, 135 degrees either way of shut
        solid = [(cx * C, cy * C, cx * C + C, cy * C + C) for (cx, cy) in self.walls]
        stuff = [(n, r) for n, r in self.furniture] + [('glass', r) for r in self.pane_rects]
        if self.up: stuff.append(('stairs', rect_of(*self.up, 0, 0, 32, 48)))
        if self.down: stuff.append(('stairs', rect_of(*self.down, 0, 0, 32, 48)))
        for n, (hx, hy, rot, L) in enumerate(self.doors):
            bad = set()
            for step in range(-54, 55):
                a = math.radians(rot + step * 2.5)
                for r in range(8, L + 1, 2):  # nearer the hinge the door only pivots
                    if r > L - 4 and abs(step) * 2.5 < 8:
                        continue
                    px, py = hx + r * math.cos(a), hy + r * math.sin(a)
                    pt = (px - 2, py - 2, px + 2, py + 2)
                    along, side = r * math.cos(math.radians(step * 2.5)), r * math.sin(math.radians(step * 2.5))
                    own_wall = along < 0 and abs(side) < 11  # the wall it hangs in, behind the hinge
                    if not own_wall and any(overlap(pt, s) for s in solid):
                        bad.add('a wall')
                    for name, s in stuff:
                        if overlap(pt, s):
                            bad.add(name)
            if bad:
                P.append(f'door {n + 1} at {hx, hy} swings into {", ".join(sorted(bad))}')
            for m, (gx, gy, grot, gL) in enumerate(self.doors[n + 1:], n + 2):
                if math.hypot(gx - hx, gy - hy) < L + gL:
                    P.append(f'doors {n + 1} and {m} can swing into each other')
        # furniture in walls, on other furniture, on stairs
        flat = {'chair', 'office_chair'}
        for k, (n, r) in enumerate(self.furniture):
            if any(overlap(r, s) for s in solid):
                P.append(f'{n} at {r[0]:.0f},{r[1]:.0f} is in a wall')
            for k2 in range(k + 1, len(self.furniture)):
                n2, r2 = self.furniture[k2]
                if self.furniture_prop[k2] != self.furniture_prop[k] and overlap(r, r2):
                    P.append(f'{n} and {n2} overlap at {r[0]:.0f},{r[1]:.0f}')
            for p in self.pane_rects:
                if overlap(r, p, 2):
                    P.append(f'{n} at {r[0]:.0f},{r[1]:.0f} touches glass')
        for label, s in (('stairs up', self.up), ('arrival stairs', self.down)):
            if not s: continue
            sr = rect_of(*s, 0, 0, 32, 48)
            if any(overlap(sr, w) for w in solid):
                P.append(f'{label} are in a wall')
            for n, r in self.furniture:
                if overlap(sr, r, 10):
                    P.append(f'{n} crowds the {label}')
        # paths: everything reachable from the player
        open_ = self.walkable()
        self.open = open_
        start = self.nearest_open(self.player)
        seen = set()
        if start:
            q = deque([start]); seen.add(start)
            while q:
                x, y = q.popleft()
                for n in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
                    if n in open_ and n not in seen:
                        seen.add(n); q.append(n)
        else:
            P.append('the player stands nowhere open')
        self.reached = seen
        for i, r in enumerate(self.rooms):
            if not any(c in seen for c in self.owner if self.owner[c] == i):
                P.append(f'{r.name} cannot be walked into')
        for k, e in enumerate(self.enemies):
            c = self.nearest_open((e['x'], e['y']), seen)
            if c is None:
                P.append(f'enemy {k + 1} ({e["kind"]}) at {e["x"], e["y"]} is cut off')
            cell = (int(e['x']) // C, int(e['y']) // C)
            if cell in self.walls:
                P.append(f'enemy {k + 1} at {e["x"], e["y"]} is in a wall')
            if e['pose'] not in ('sit', 'toilet'):
                for n, r in self.furniture:
                    if n not in flat and overlap((e['x'] - 6, e['y'] - 6, e['x'] + 6, e['y'] + 6), r):
                        P.append(f'enemy {k + 1} at {e["x"], e["y"]} stands in {n}')
        for w, x, y in self.weapons:
            c = self.nearest_open((x, y), seen)
            if c is None:
                P.append(f'{w} at {x, y} is out of reach')
        if self.up and self.nearest_open(self.up, seen) is None:
            P.append('stairs up are out of reach')
        # nobody standing alone: someone else within 110 px (walkers move about anyway)
        for k, e in enumerate(self.enemies):
            if e['act'] != 'static':
                continue
            d = min((math.hypot(e['x'] - f['x'], e['y'] - f['y']) for f in self.enemies if f is not e), default=1e9)
            if d > 110:
                P.append(f'enemy {k + 1} ({e["kind"]}, {self.room_at(e["x"], e["y"])}) stands alone ({d:.0f} px to the next)')
        return P

    def nearest_open(self, p, among=None):
        if p is None: return None
        among = self.open if among is None else among
        cx, cy = int(p[0]) // C, int(p[1]) // C
        for ring in range(0, 5):
            for c in sorted(near({(cx, cy)}, ring) - (near({(cx, cy)}, ring - 1) if ring else set())):
                if c in among:
                    return c
        return None

    def room_at(self, x, y):
        i = self.owner.get((int(x) // C, int(y) // C))
        return self.rooms[i].name if i is not None else 'nowhere'

    # --- measuring ---
    def stats(self):
        area = Counter(self.owner.values())
        total = sum(area.values()) * C * C + sum(1 for k in self.opening.values() if k != 'outer') * C * C
        per_room = Counter(self.room_at(e['x'], e['y']) for e in self.enemies)
        rooms = []
        for i, r in enumerate(self.rooms):
            xs = [c[0] for c, j in self.owner.items() if j == i]; ys = [c[1] for c, j in self.owner.items() if j == i]
            rooms.append((r.name, (max(xs) - min(xs) + 1) * C, (max(ys) - min(ys) + 1) * C, area[i] * C * C, per_room.get(r.name, 0)))
        nn = []
        for e in self.enemies:
            ds = [math.hypot(e['x'] - f['x'], e['y'] - f['y']) for f in self.enemies if f is not e]
            if ds: nn.append(min(ds))
        nn.sort()
        melee = sum(1 for e in self.enemies if e['gun'] in MELEE)
        kinds = Counter(e['kind'] for e in self.enemies)
        return dict(
            area=total, screens=total / SCREEN, enemies=len(self.enemies), per_screen=len(self.enemies) / (total / SCREEN),
            rooms=rooms, doors=len(self.doors), panes=len(self.panes), melee=melee, kinds=dict(kinds),
            nn=nn[len(nn) // 2] if nn else 0, biggest=max(r[3] for r in rooms) / total,
            small=sum(1 for r in rooms if r[1] <= 110 and r[2] <= 110), empty=[r[0] for r in rooms if r[4] == 0],
            long=sum(1 for r in rooms if max(r[1], r[2]) > SIGHT), weapons=[w for w, _, _ in self.weapons],
            walk=sum(1 for e in self.enemies if e['act'] != 'static'),
            exit=math.hypot(self.up[0] - self.player[0], self.up[1] - self.player[1]) if self.up else 0)

    # --- the scene ---
    def tscn(self):
        ext, ids = [], {}
        def res(kind, path, uid=None, short=None):
            if path not in ids:
                ids[path] = short or f'{len(ids) + 1}_{os.path.basename(path).split(".")[0]}'
                ext.append(f'[ext_resource type="{kind}"' + (f' uid="{uid}"' if uid else '') + f' path="{path}" id="{ids[path]}"]')
            return ids[path]
        floors = res('TileSet', 'res://levels/floors3.tres', 'uid://kekc68nbqdgk', 'floors')
        walls = res('TileSet', 'res://levels/walls3.tres', 'uid://cim5lnglo5lk3', 'walls')
        body = [f'[node name="{self.root}" type="Node2D"]', '']
        def node(name, parent, inst=None, typ=None, props=()):
            head = f'[node name="{name}"' + (f' type="{typ}"' if typ else '') + f' parent="{parent}"' + (f' instance=ExtResource("{inst}")' if inst else '') + ']'
            body.append(head)
            body.extend(props)
            body.append('')
        def pos(x, y):
            fx = lambda v: f'{int(v)}' if float(v).is_integer() else f'{v:g}'
            return f'position = Vector2({fx(x)}, {fx(y)})'
        node('Floor', '.', typ='TileMapLayer', props=[f'tile_map_data = PackedByteArray("{encode(self.floor_tiles)}")', f'tile_set = ExtResource("{floors}")'])
        node('Stairs', '.', typ='Node2D')
        st = res('PackedScene', 'res://props/stairs.tscn', short='stairs')
        if self.down:
            node('Down', 'Stairs', st, props=[pos(*self.down), 'LeadsUp = false'])
        if self.up:
            node('Up', 'Stairs', st, props=[pos(*self.up)])
        node('Shadows', '.', typ='TileMapLayer', props=['z_index = 5', 'position = Vector2(3, 3)', f'tile_map_data = PackedByteArray("{encode(self.shadow_tiles)}")', f'tile_set = ExtResource("{walls}")'])
        node('OuterWalls', '.', typ='TileMapLayer', props=['z_index = 5', f'tile_map_data = PackedByteArray("{encode(self.outer_tiles)}")', f'tile_set = ExtResource("{walls}")'])
        node('Walls', '.', typ='TileMapLayer', props=['z_index = 5', f'tile_map_data = PackedByteArray("{encode(self.inner_tiles)}")', f'tile_set = ExtResource("{walls}")'])
        node('Props', '.', typ='Node2D', props=['z_index = 3'])
        count = Counter()
        def uniq(base):
            count[base] += 1
            return base if count[base] == 1 else f'{base}{count[base]}'
        camel = lambda s: ''.join(w.capitalize() for w in s.split('_'))
        for (scene, x, y, rot) in self.props:
            p = [pos(x, y)] + ([f'rotation = {ROT[rot]}'] if ROT[rot] else [])
            node(uniq(camel(scene)), 'Props', res('PackedScene', f'res://props/{scene}.tscn', short=f'p_{scene}'), props=p)
        win = res('PackedScene', 'res://props/window.tscn', short='p_window') if self.windows else None
        for (x, y), rot in self.windows:
            node(uniq('Window'), 'Props', win, props=[pos(x, y)] + ([f'rotation = {ROT[rot]}'] if ROT[rot] else []))
        if self.panes:
            gl = res('PackedScene', 'res://props/glass_partition.tscn', 'uid://bjyh0jmn83uuu', 'glass')
            for (x, y, rot) in self.panes:
                node(uniq('Glass'), 'Props', gl, props=[pos(x, y)] + ([f'rotation = {ROT[rot]}'] if ROT[rot] else []))
        node('Doors', '.', typ='Node2D')
        if self.doors:
            dr = res('PackedScene', 'res://props/door.tscn', short='door')
            for k, (x, y, rot, L) in enumerate(self.doors, 1):
                node(f'Door{k}', 'Doors', dr, props=[pos(x, y)] + ([f'rotation = {ROT[rot]}'] if ROT[rot] else []) + ([f'Length = {L:.1f}'] if L != 40 else []))
        node('Weapons', '.', typ='Node2D')
        wid = lambda w: res('Resource', f'res://weapons/{w}.tres', short=f'w_{w}')
        if self.weapons:
            pk = res('PackedScene', 'res://weapons/pickup.tscn', 'uid://colcq6x584kno', 'pickup')
            for (w, x, y) in self.weapons:
                node(uniq(camel(w)), 'Weapons', pk, props=[pos(x, y), f'Data = ExtResource("{wid(w)}")'])
        node('Player', '.', res('PackedScene', 'res://player/player.tscn', 'uid://vxvxhq660cmp', 'player'), props=[pos(*self.player)])
        names = dict(enemy='Enemy', dog='Dog', heavy='Heavy', dodger='Dodger', shield='Shield')
        for e in self.enemies:
            scene, routine, weapon = KINDS[e['kind']]
            p = [pos(e['x'], e['y'])]
            if e['act'] != routine: p.append(f'Behaviour = {ROUTINE[e["act"]]}')
            if e['stay']: p.append('StayInRoom = true')
            if e['face']: p.append(f'FacingDegrees = {float(e["face"])}')
            if e['pose'] != 'none': p.append(f'Pose = {POSE[e["pose"]]}')
            if e['look']: p.append(f'Look = ExtResource("{res("SpriteFrames", LOOKS[e["look"]][1], LOOKS[e["look"]][0], "look_" + e["look"])}")')
            if e['gun'] != weapon: p.append(f'StartWeapon = ExtResource("{wid(e["gun"])}")' if e['gun'] else 'StartWeapon = null')
            count['enemy:' + e['kind']] += 1
            node(f"{names[e['kind']]}{count['enemy:' + e['kind']]}", '.', res('PackedScene', scene, short='e_' + e['kind']), props=p)
        return '\n'.join(['[gd_scene format=3]', ''] + ext + [''] + body).rstrip() + '\n'

    # --- a picture of the plan ---
    def svg(self, path, scale=1.0):
        COL = dict(oak='#c8955a', honey='#d6833e', cherry='#8e3e2e', parquet='#b8804c', red='#a82a3c', purple='#7a2e8e',
                   blue='#2c4aa0', teal='#1f8090', pink='#f0a0c4', pool='#6ad0ec', checker='#8a8494', terracotta='#c86a40',
                   stone='#d9d0b8', lino='#a59ad0', velvet='#6a2a5a', concrete='#8e8a96')
        WALLC = {DRYWALL: '#f2eee4', BRICK: '#7a3a2a', BLOCK: '#9a9aa4', PAINTED: '#ffffff', TIMBER: '#8a5a30'}
        mx0, my0, mx1, my1 = self.map
        out = [f'<svg xmlns="http://www.w3.org/2000/svg" width="{(mx1 - mx0) * scale:.0f}" height="{(my1 - my0) * scale:.0f}" '
               f'viewBox="{mx0} {my0} {mx1 - mx0} {my1 - my0}" font-family="sans-serif">']
        out.append(f'<rect x="{mx0}" y="{my0}" width="{mx1 - mx0}" height="{my1 - my0}" fill="#2a2730"/>')
        inv = {v: k for k, v in MAT.items()}
        for (tx, ty), m in self.floor_tiles.items():
            col = COL.get(inv.get(m), {SIDEWALK: '#6c6878', STREET: '#d0c060', ASPHALT: '#2a2730'}.get(m, '#2a2730'))
            out.append(f'<rect x="{tx * T}" y="{ty * T}" width="{T}" height="{T}" fill="{col}"/>')
        for c, (m, row) in list(self.inner_tiles.items()) + list(self.outer_tiles.items()):
            out.append(f'<rect x="{c[0] * C}" y="{c[1] * C}" width="{C}" height="{C}" fill="{WALLC[row]}" stroke="#1a1820" stroke-width="0.4"/>')
        for n, r in self.furniture:
            fill = '#55505e' if n != 'pillar' else '#b0aab8'
            out.append(f'<rect x="{r[0]:.1f}" y="{r[1]:.1f}" width="{r[2] - r[0]:.1f}" height="{r[3] - r[1]:.1f}" fill="{fill}" fill-opacity="0.85" stroke="#111" stroke-width="0.6"/>')
        for (scene, x, y, rot) in self.props:
            if not SHAPES[scene]:
                w, h = png_size(scene)
                _, _, w, h = turned(0, 0, w, h, rot)
                out.append(f'<rect x="{x - w / 2:.1f}" y="{y - h / 2:.1f}" width="{w}" height="{h}" fill="none" stroke="#d8d0e8" stroke-width="0.6" stroke-dasharray="2 2"/>')
        for r in self.pane_rects:
            out.append(f'<rect x="{r[0]:.1f}" y="{r[1]:.1f}" width="{r[2] - r[0]:.1f}" height="{r[3] - r[1]:.1f}" fill="#7cc6ff"/>')
        for (x, y), rot in self.windows:
            w, h = (34, 6) if rot == 0 else (6, 34)
            out.append(f'<rect x="{x - w / 2}" y="{y - h / 2}" width="{w}" height="{h}" fill="#7cc6ff" fill-opacity="0.6"/>')
        for (hx, hy, rot, L) in self.doors:
            a0, a1 = math.radians(rot - 135), math.radians(rot + 135)
            x0, y0 = hx + L * math.cos(a0), hy + L * math.sin(a0)
            x1, y1 = hx + L * math.cos(a1), hy + L * math.sin(a1)
            out.append(f'<path d="M{hx},{hy} L{x0:.1f},{y0:.1f} A{L},{L} 0 1 1 {x1:.1f},{y1:.1f} Z" fill="#ff9b54" fill-opacity="0.12" stroke="#ff9b54" stroke-width="0.5"/>')
            ex, ey = hx + L * math.cos(math.radians(rot)), hy + L * math.sin(math.radians(rot))
            out.append(f'<line x1="{hx}" y1="{hy}" x2="{ex:.1f}" y2="{ey:.1f}" stroke="#ff9b54" stroke-width="3"/>')
        for s, col in ((self.up, '#ffd166'), (self.down, '#8a7a50')):
            if s:
                out.append(f'<rect x="{s[0] - 16}" y="{s[1] - 24}" width="32" height="48" fill="{col}" stroke="#000"/>')
        for w, x, y in self.weapons:
            out.append(f'<path d="M{x},{y - 6} L{x + 6},{y} L{x},{y + 6} L{x - 6},{y} Z" fill="#ff4d6d" stroke="#fff" stroke-width="1"/>')
        KC = dict(enemy='#4cc9f0', dog='#c77dff', heavy='#ffffff', dodger='#ff8fab', shield='#3a86ff')
        for k, e in enumerate(self.enemies, 1):
            col = '#ffd166' if e['gun'] in MELEE and e['kind'] == 'enemy' else KC[e['kind']]
            a = math.radians(e['face'])
            fill = col if e['act'] == 'static' else 'none'
            out.append(f'<circle cx="{e["x"]}" cy="{e["y"]}" r="7" fill="{fill}" stroke="{col}" stroke-width="2"/>')
            out.append(f'<line x1="{e["x"]}" y1="{e["y"]}" x2="{e["x"] + 13 * math.cos(a):.1f}" y2="{e["y"] + 13 * math.sin(a):.1f}" stroke="{col}" stroke-width="2"/>')
            out.append(f'<text x="{e["x"] + 8}" y="{e["y"] - 7}" fill="#fff" font-size="9">{k}</text>')
        if self.player:
            x, y = self.player
            out.append(f'<circle cx="{x}" cy="{y}" r="8" fill="#ff4dff" stroke="#fff" stroke-width="2"/>')
        for i, r in enumerate(self.rooms):
            x0, y0, x1, y1 = r.rects[0]
            out.append(f'<text x="{x0 + 4}" y="{y0 + 11}" fill="#fff" stroke="#000" stroke-width="2" paint-order="stroke" font-size="10" font-weight="bold">{r.name}</text>')
        out.append('</svg>')
        open(path, 'w').write('\n'.join(out))

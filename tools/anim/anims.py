"""Every animation in the game, as posed frames: built from parts, rendered per character.

A pose is a dict; a missing key means the default. Coordinates are in the character's own frame:
facing +x, centred on the body, right side = +y. Each animation is a list of (pose, milliseconds).
"""
import math
from puppet import (Layer, C, SIZE, INK, blank, capsule, head, torso, arm, hand, rot,
                    read_png, PROJECT)

WEAPONS = {}


def weapon_px(name):
    if name not in WEAPONS:
        w, h, px = read_png(f'{PROJECT}weapons/{name}.png')
        WEAPONS[name] = (w, h, px)
    return WEAPONS[name]


RH, LH = (1, 10.5), (1, -10.5)       # resting hands: right (+y), left (-y)
R_SHOULDER, L_SHOULDER = (-1.5, 8.5), (-1.5, -8.5)
HOLD = {'bat': 7.5, 'knife': 3.5}    # the picture's centre is this far up the weapon from the hand


def lerp(a, b, t):
    if a is None or b is None:
        return a if t < 0.5 else b
    if isinstance(a, tuple):
        return tuple(lerp(x, y, t) for x, y in zip(a, b))
    if isinstance(a, (int, float)) and not isinstance(a, bool):
        return a + (b - a) * t
    return a if t < 0.5 else b


def blend(p, q, t):
    keys = set(p) | set(q)
    return {k: lerp(p.get(k, DEFAULT.get(k)), q.get(k, DEFAULT.get(k)), t) for k in keys}


DEFAULT = {'shear': 0.0, 'body': (0.0, 0.0), 'head': (0.0, 0.0), 'turn': 0.0, 'rh': RH, 'lh': LH,
           'weapon': None, 'wpos': (0.0, 0.0), 'wang': 0.0, 'wlen': 1.0, 'gun_arm': False,
           'smear': None, 'items': (), 'hands_top': False, 'legs': None}


# ---------------------------------------------------------------- rendering a standing pose
def draw_weapon(canvas, name, grip, angle, length=1.0):
    w, h, px = weapon_px(name)
    hold = HOLD[name] * length
    cx, cy = C + grip[0] + math.cos(angle) * hold, C + grip[1] + math.sin(angle) * hold
    ca, sa = math.cos(-angle), math.sin(-angle)
    R = int(math.hypot(w, h)) + 2
    for y in range(-R, R + 1):
        for x in range(-R, R + 1):
            u = (x * ca - y * sa) / max(length, 0.2) + w / 2
            v = x * sa + y * ca + h / 2
            sx, sy = math.floor(u), math.floor(v)
            if 0 <= sx < w and 0 <= sy < h and px[sy][sx][3]:
                X, Y = math.floor(cx + x), math.floor(cy + y)
                if 0 <= X < SIZE and 0 <= Y < SIZE:
                    canvas[Y][X] = px[sy][sx]


def draw_smear(canvas, smear):
    """A pale arc behind a fast swing: (centre, inner radius, outer radius, from angle, to angle) in degrees."""
    (cx, cy), r0, r1, a0, a1 = smear
    lo, hi = min(a0, a1), max(a0, a1)
    for y in range(SIZE):
        for x in range(SIZE):
            dx, dy = x + 0.5 - (C + cx), y + 0.5 - (C + cy)
            r = math.hypot(dx, dy)
            if r0 <= r <= r1:
                a = math.degrees(math.atan2(dy, dx))
                for a_ in (a, a + 360, a - 360):
                    if lo <= a_ <= hi:
                        # thicker toward the newest end; the old end frays into dots
                        k = (a_ - a0) / (a1 - a0) if a1 != a0 else 1
                        if k > 0.35 or (x + y) % 2 == 0:
                            if canvas[y][x][3] == 0:
                                canvas[y][x] = (255, 252, 240, 255) if r > (r0 + r1) / 2 - 0.5 or k > 0.6 else (220, 220, 230, 255)
                        break


def draw_item(canvas, item):
    kind, pos = item[0], item[1]
    x, y = C + pos[0], C + pos[1]
    put = lambda dx, dy, c: (0 <= math.floor(y + dy) < SIZE and 0 <= math.floor(x + dx) < SIZE) and canvas[math.floor(y + dy)].__setitem__(math.floor(x + dx), c)
    if kind == 'cig':
        ang = item[2] if len(item) > 2 else 0
        for i in range(3):
            put(math.cos(ang) * i, math.sin(ang) * i, (240, 236, 226, 255))
        put(math.cos(ang) * 3, math.sin(ang) * 3, (255, 120, 40, 255))
    elif kind == 'puff':
        r = item[2]
        for dy in range(-r, r + 1):
            for dx in range(-r, r + 1):
                if dx * dx + dy * dy <= r * r and (dx + dy) % 2 == 0:
                    put(dx, dy, (205, 205, 215, 255))
    elif kind == 'phone':
        for dy in range(-2, 2):
            for dx in range(-1, 1):
                put(dx, dy, (40, 40, 48, 255))
        put(-2, -2, INK); put(1, -2, INK)


def leg_parts(legs):
    """Extra leg pieces drawn into the body frame (sitting, kneeling, kicking): list of (hip, foot)."""
    return legs or ()


def render(ch, pose):
    p = {**DEFAULT, **pose}
    canvas = blank()
    layers = []
    # legs that show in this frame (sitting, kneeling, a kick) lie under everything
    for hip, ft in leg_parts(p['legs']):
        l = Layer()
        capsule(l, (C + hip[0], C + hip[1]), (C + ft[0], C + ft[1]), 2.2, ch.pants)
        capsule(l, (C + ft[0], C + ft[1]), (C + ft[0] + 0.01, C + ft[1]), 2.2, ch.shoe_dark)
        layers.append(l)
    body = torso(ch, p['shear'], p['body'])
    layers.append(body)
    for l in layers:
        l.stamp_onto(canvas)
    # arms (a sleeve shows once a hand is away from its resting spot), weapon, hands, head
    arms = []
    for hand_at, shoulder, rest in ((p['rh'], R_SHOULDER, RH), (p['lh'], L_SHOULDER, LH)):
        if hand_at is not None and math.dist(hand_at, rest) > 2.5:
            arms.append(arm(ch, shoulder, hand_at))
    for l in arms:
        l.stamp_onto(canvas)
    if p['weapon']:
        draw_weapon(canvas, p['weapon'], p['wpos'], p['wang'], p['wlen'])
    hands = [hand(ch, h) for h in (p['rh'], p['lh']) if h is not None]
    if not p['hands_top']:
        for l in hands:
            l.stamp_onto(canvas)
    head(ch, p['head'], p['turn']).stamp_onto(canvas)
    if p['hands_top']:
        for l in hands:
            l.stamp_onto(canvas)
    if p['gun_arm']:
        for (x, y), c in ch.gun_arm.items():
            canvas[C + y][C + x] = c
    for item in p['items']:
        draw_item(canvas, item)
    if p['smear']:
        draw_smear(canvas, p['smear'])
    return canvas


# ---------------------------------------------------------------- lying bodies (canvas centred on the body)
def corpse(ch, kind):
    canvas = blank()
    for (x, y), c in ch.corpses[kind].items():
        canvas[C + y][C + x] = c
    return canvas


def lying(ch, torso_len=16, head_x=12, legs=((-8, 3.5, -20, 5.0), (-8, -3.5, -20, -5.0)), hands=((6, 11), (6, -11)), back=True):
    """A body flat on the floor along +x (head forward), from parts."""
    canvas = blank()
    layers = []
    for hx, hy, fx, fy in legs:
        l = Layer()
        capsule(l, (C + hx, C + hy), (C + fx, C + fy), 2.4, ch.pants)
        layers.append(l)
        s = Layer()
        capsule(s, (C + fx - 1, C + fy), (C + fx + 0.5, C + fy), 2.4, ch.shoe_dark)
        layers.append(s)
    t = Layer()
    half = torso_len / 2
    for y in range(-8, 8):
        for x in range(-int(half), int(half)):
            edge = abs(y + 0.5) > 6.6 or x < -half + 1.5
            c = ch.jacket_dark if edge else (ch.shirt if back and abs(y + 0.5) < 3.5 and x > -half + 3 else ch.jacket)
            t.put(C + x, C + y, c)
    layers.append(t)
    for hx, hy in hands:
        layers.append(arm(ch, (half - 2, 7 if hy > 0 else -7), (hx, hy)))
        layers.append(hand(ch, (hx, hy)))
    hd = Layer()
    capsule(hd, (C + head_x, C), (C + head_x + 0.01, C), 4.5, ch.hair)
    layers.append(hd)
    for l in layers:
        l.stamp_onto(canvas)
    return canvas


# ---------------------------------------------------------------- the animations
def bat_pose(e, lift=0.0):
    """The bat swing in the hands' arc: e 0 = resting over the right shoulder, 1 = finished on the left."""
    half = math.radians(100)
    hands = half * (1 - 2 * e)                         # +100 deg (right, behind) -> -100 deg (left)
    lead = math.radians(65) * (1 - 2 * e)              # the bat trails behind the hands, straight out at impact
    grip = (math.cos(hands) * 11, math.sin(hands) * 11)
    ang = hands + lead
    off = (grip[0] - math.cos(ang) * 3, grip[1] - math.sin(ang) * 3)
    return {'rh': grip, 'lh': off, 'weapon': 'bat', 'wpos': grip, 'wang': ang, 'wlen': 1.0 - lift}


def knife_pose(e):
    half = math.radians(60)
    hands = half * (1 - 2 * e)                         # right front -> left front
    lead = math.radians(-40) * (1 - 2 * e)             # the blade leads the hand
    grip = (math.cos(hands) * 12, math.sin(hands) * 12)
    return {'rh': grip, 'weapon': 'knife', 'wpos': grip, 'wang': hands + lead}


def smear_for(kind, e0, e1):
    if kind == 'bat':
        a0 = math.degrees(math.radians(100) * (1 - 2 * e0))
        a1 = math.degrees(math.radians(100) * (1 - 2 * e1))
        return ((0, 0), 24, 27, a0, a1)   # a thin band along the bat's tip, like the original
    a0 = math.degrees(math.radians(60) * (1 - 2 * e0))
    a1 = math.degrees(math.radians(60) * (1 - 2 * e1))
    return ((0, 0), 15, 17.5, a0, a1)


def walk_cycle(base, frames=12, swing=3.5, shear=1.5, free_left=True, free_right=True, bob=None):
    out = []
    for i in range(frames):
        s = math.sin(2 * math.pi * i / frames)
        p = dict(base)
        p['shear'] = shear * s
        if free_left:
            p['lh'] = (LH[0] + swing * s, LH[1])
        if free_right:
            p['rh'] = (RH[0] - swing * s, RH[1])
        if bob:
            bob(p, s)
        out.append((p, 100))
    return out


def breathe(base, extra=None):
    """Standing still: a slow breath, the hands drifting out a pixel and back."""
    a = dict(base)
    b = dict(base)
    for k, side in (('lh', -1), ('rh', 1)):
        if base.get(k, DEFAULT[k]) in (LH, RH):
            v = base.get(k, DEFAULT[k])
            b[k] = (v[0], v[1] + 0.6 * side)
    return [(a, 700), (blend(a, b, 0.5), 250), (b, 600), (blend(a, b, 0.5), 250)]


def look_around(base, free):
    """Standing and looking around: the head turns right, then left; the shoulders and the `free` hands
    turn a little with it."""
    out = []
    for turn, ms in ((0.0, 150), (0.5, 80), (1.1, 400), (0.5, 80), (0.0, 120), (-0.5, 80), (-1.1, 400), (-0.5, 80)):
        p = dict(base)
        p['turn'] = turn
        p['shear'] = 1.3 * turn
        for k in free:
            p[k] = rot(base.get(k, DEFAULT[k]), turn * 0.15)
        out.append((p, ms))
    return out


def animations():
    A = {}
    fists = {}
    gun = {'gun_arm': True, 'rh': None}
    bat_rest = bat_pose(0)
    knife_rest = {'rh': (5, 9), 'weapon': 'knife', 'wpos': (5, 9), 'wang': math.radians(25)}

    # holding nothing / a gun / a bat / a knife: standing and walking
    A['idle'] = breathe(fists)
    A['walk'] = walk_cycle(fists)
    A['gun_idle'] = breathe(gun)
    A['gun_walk'] = walk_cycle(gun, shear=1.0, free_right=False)
    A['bat_idle'] = breathe(bat_rest)
    A['bat_walk'] = walk_cycle(bat_rest, shear=1.0, free_left=False, free_right=False,
                               bob=lambda p, s: p.update(bat_pose(0.0 - 0.03 * s)))
    A['knife_idle'] = breathe(knife_rest)
    A['knife_walk'] = walk_cycle(knife_rest, free_right=False)

    # attacks: short wind-up, smears on the fast part, a held follow-through, a quick recovery
    A['punch'] = [(fists, 20), ({'rh': (-3, 10.5), 'shear': -1.0}, 30), ({'rh': (15, 3), 'shear': 2.0}, 40),
                  ({'rh': (15, 3), 'shear': 2.0, 'smear': None}, 40), ({'rh': (14, 3.5), 'shear': 1.5}, 40),
                  ({'rh': (7, 8), 'shear': 0.5}, 30), (fists, 30)]
    bat = []
    for e, ms, sm in ((0.0, 20, None), (-0.06, 30, None), (0.22, 20, (0.0, 0.22)), (0.45, 20, (0.15, 0.45)),
                      (0.62, 20, (0.35, 0.62)), (0.8, 25, (0.55, 0.8)), (0.95, 30, None)):
        p = bat_pose(e)
        p['shear'] = 2.0 * math.sin(math.pi * max(0, e))
        if sm:
            p['smear'] = smear_for('bat', *sm)
        bat.append((p, ms))
    lift = bat_pose(0.5, lift=0.55)
    lift.update({'rh': (2, 3), 'lh': (2, -1), 'wpos': (2, 2), 'wang': math.radians(170)})
    bat += [(lift, 30), (bat_rest, 30)]
    A['bat_swing'] = bat
    knife = []
    for e, ms, sm in ((0.0, 15, None), (-0.08, 20, None), (0.25, 15, (0.0, 0.25)), (0.5, 15, (0.2, 0.5)),
                      (0.75, 15, (0.45, 0.75)), (0.95, 20, None)):
        p = knife_pose(e)
        if sm:
            p['smear'] = smear_for('knife', *sm)
        p['shear'] = 1.5 * math.sin(math.pi * max(0, e))
        knife.append((p, ms))
    knife += [(blend(knife_pose(0.95), knife_rest, 0.5), 25), (knife_rest, 25)]
    A['knife_swing'] = knife

    # throw, pick up, push a door (left or right side)
    A['throw'] = [({'rh': (-4, 11), 'shear': -1.0}, 40), ({'rh': (3, 7), 'shear': 0.5}, 40),
                  ({'rh': (16, 2), 'shear': 2.0}, 60), ({'rh': (15, 3), 'shear': 1.5}, 60), ({'rh': (6, 9)}, 50)]
    A['pickup'] = [({'rh': (6, 9), 'body': (0.5, 0)}, 40), ({'rh': (12, 6), 'body': (1, 0), 'head': (1, 0)}, 70),
                   ({'rh': (11, 7), 'body': (1, 0), 'head': (1, 0)}, 50), ({'rh': (5, 9.5)}, 40)]
    A['push_r'] = [({'rh': (6, 12), 'shear': -0.5}, 50), ({'rh': (9, 13), 'shear': -1.0, 'body': (0, 0.5)}, 110),
                   ({'rh': (8, 12.5), 'shear': -1.0}, 90), ({'rh': (4, 11)}, 50)]
    A['push_l'] = [({'lh': (6, -12), 'shear': 0.5}, 50), ({'lh': (9, -13), 'shear': 1.0, 'body': (0, -0.5)}, 110),
                   ({'lh': (8, -12.5), 'shear': 1.0}, 90), ({'lh': (4, -11)}, 50)]

    # finishing a downed enemy (the player kneels on him; the camera sees the back of the player)
    A['fin_fists'] = [({'rh': (-2, 9), 'shear': -1.0}, 30), ({'rh': (13, 2), 'shear': 1.5}, 40),
                      ({'rh': (12, 2.5), 'shear': 1.0}, 60), ({'rh': (4, 8)}, 70)]
    up = bat_pose(0.0)
    up.update({'rh': (1, 4), 'lh': (1, 1), 'wpos': (1, 3), 'wang': math.radians(180), 'wlen': 0.45})
    down = {'rh': (10, 2), 'lh': (8, -1), 'weapon': 'bat', 'wpos': (10, 1), 'wang': 0.0, 'shear': 1.5}
    A['fin_bat'] = [(up, 40), ({**down, 'wpos': (8, 1), 'smear': ((0, 0), 22, 25, -40, 0)}, 30), (down, 70),
                    (blend(down, up, 0.5), 60)]
    grab = {'lh': (12, -3), 'rh': (6, 10), 'weapon': 'knife', 'wpos': (6, 10), 'wang': math.radians(30)}
    cut = {'lh': (12, -3), 'rh': (11, -2), 'weapon': 'knife', 'wpos': (11, -2), 'wang': math.radians(-80),
           'smear': ((10, 0), 5, 7, 60, -80), 'shear': 1.0, 'hands_top': True}
    A['fin_knife'] = [(grab, 120), ({**grab, 'rh': (9, 9), 'wpos': (9, 9)}, 120), ({**grab, 'rh': (12, 6), 'wpos': (12, 6), 'wang': math.radians(-20)}, 120),
                      (blend(grab, cut, 0.6), 80), (cut, 160), ({**cut, 'smear': None}, 180)]
    hold = {'rh': (12, 4), 'lh': (12, -4), 'hands_top': True}
    A['choke'] = [(hold, 90), ({**hold, 'shear': 1.0, 'rh': (12, 5)}, 90), (hold, 90), ({**hold, 'shear': -1.0, 'lh': (12, -5)}, 90)]

    # human shield: he's held 12 px ahead, the left arm locked across his neck; whatever is in the right
    # hand stays there (a one-handed gun out front). The snap: both hands to his head, a sharp twist.
    neck = {'lh': (12, 4), 'hands_top': True}
    A['shield'] = breathe({**neck, 'rh': (11, -6)})
    A['gun_shield'] = breathe({**neck, 'rh': (15, 11)})  # the gun hand past his right shoulder (Player.ShieldGunHand)
    A['bat_shield'] = breathe({**bat_rest, **neck})
    A['knife_shield'] = breathe({**knife_rest, **neck})
    grip = {'lh': (13, -4), 'rh': (13, 4), 'hands_top': True}
    twist = {'lh': (15, -1), 'rh': (11, 3), 'shear': 1.5, 'hands_top': True}
    A['snap'] = [(grip, 60), (blend(grip, twist, 0.5), 40), (twist, 60), ({**twist, 'lh': (15, 0), 'rh': (10, 2), 'shear': 1.0}, 140)]
    # picking him up off the floor (Player.LiftTime): reach down, grip, pull him up, lock the arm round his neck
    reach = {'rh': (14, 6), 'lh': (14, -6), 'body': (1, 0), 'head': (1.5, 0), 'shear': 1.0, 'hands_top': True}
    pull = {'rh': (10, 5), 'lh': (11, -3), 'body': (0.5, 0), 'shear': 0.5, 'hands_top': True}
    # shoving him away (Player.ThrowShield): both hands into his back, a hard push, back to rest
    A['shove'] = [({'rh': (11, 6), 'lh': (11, -6), 'hands_top': True}, 40),
                  ({'rh': (18, 5), 'lh': (18, -5), 'shear': 2.0, 'body': (1, 0), 'hands_top': True}, 60),
                  ({'rh': (16, 5), 'lh': (16, -5), 'shear': 1.0, 'hands_top': True}, 70), ({'rh': (6, 9), 'lh': (6, -9)}, 60)]
    A['shield_grab'] = [(reach, 60), ({**reach, 'rh': (12, 5), 'lh': (12, -5), 'shear': 1.5}, 60), (pull, 60),
                        ({**neck, 'rh': (11, 7)}, 70)]
    # the enemy held up as one: arms pinned to his sides, squirming a little
    held = {'rh': (-1, 9.5), 'lh': (-1, -9.5), 'head': (0.5, 0)}
    A['held'] = [(held, 400), ({**held, 'shear': 0.8, 'rh': (0, 10)}, 250), (held, 400), ({**held, 'shear': -0.8, 'lh': (0, -10)}, 250)]
    leg = lambda f: ((-2, 4), f)
    A['kick'] = [({'legs': (leg((-6, 6)),), 'shear': -1.0}, 40), ({'legs': (leg((16, 3)),), 'shear': 2.0}, 50),
                 ({'legs': (leg((15, 3)),), 'shear': 1.5}, 70), ({'legs': (leg((4, 5)),)}, 60)]

    # enemies' idle poses for level design
    cig_side = ('cig', (2, 12), 0.4)
    smoke = [({'items': (cig_side,)}, 700), ({'rh': (5, 8), 'items': (('cig', (6, 9), -0.6),)}, 100),
             ({'rh': (8, 4), 'items': (('cig', (9, 4), -1.2),), 'hands_top': True}, 100),
             ({'rh': (9, 2), 'items': (('cig', (10, 2), -1.4),), 'hands_top': True}, 600),
             ({'rh': (6, 7), 'items': (('cig', (7, 7), -0.6),)}, 100), ({'items': (cig_side, ('puff', (11, 0), 1))}, 200),
             ({'items': (cig_side, ('puff', (14, -1), 2))}, 220), ({'items': (cig_side, ('puff', (17, -2), 2))}, 240),
             ({'items': (cig_side, ('puff', (20, -3), 1))}, 260), ({'items': (cig_side,)}, 900)]
    A['smoke'] = smoke
    phone = {'lh': (3, -6), 'items': (('phone', (4, -6)),), 'hands_top': True}
    A['phone'] = [(phone, 600), ({**phone, 'head': (0, -0.5)}, 400), (phone, 500), ({**phone, 'rh': (7, 11)}, 300),
                  ({**phone, 'rh': (9, 10)}, 300), ({**phone, 'rh': (6, 11)}, 300), (phone, 700), ({**phone, 'head': (0.5, 0)}, 400)]
    hide = {'body': (-1, 0), 'head': (-1, 0), 'rh': (5, 6), 'lh': (5, -6), 'shear': 0.0}
    A['hide'] = [(hide, 900), ({**hide, 'head': (-0.5, 0)}, 700)]
    sit_legs = (((-1, 4), (11, 5)), ((-1, -4), (11, -5)))
    sit = {'legs': sit_legs, 'rh': (6, 7), 'lh': (6, -7)}
    A['sit'] = breathe(sit)
    lean = {'shear': 2.0, 'body': (-1, 0), 'rh': (4, -3), 'lh': (4, 3), 'turn': 0.2}
    A['lean'] = breathe(lean)
    talk = {}
    A['talk'] = [(talk, 500), ({'rh': (6, 11)}, 150), ({'rh': (10, 9)}, 300), ({'rh': (8, 10), 'head': (0.5, 0)}, 200),
                 ({'rh': (11, 8)}, 300), ({'rh': (5, 11)}, 200), (talk, 400), ({'head': (0.5, 0)}, 300)]
    # getting up out of those when something alerts him: short enough to finish inside his reaction time
    A['sit_up'] = [({'legs': sit_legs, 'rh': (9, 7), 'lh': (9, -7), 'shear': 1.5, 'body': (1, 0), 'head': (1.5, 0)}, 60),
                   ({'legs': (((-1, 4), (6, 5)), ((-1, -4), (6, -5))), 'rh': (7, 8), 'lh': (7, -8), 'shear': 1.0,
                     'body': (1, 0), 'head': (1, 0)}, 60),
                   ({'legs': (((-1, 4), (2, 5)), ((-1, -4), (2, -5))), 'rh': (3, 10), 'lh': (3, -10), 'shear': 0.5,
                     'body': (0.5, 0)}, 60),
                   (fists, 60)]
    A['hide_up'] = [(blend(hide, fists, 0.35), 60), (blend(hide, fists, 0.75), 60), (fists, 40)]
    A['lean_off'] = [({'shear': 1.0, 'body': (0.5, 0), 'rh': (6, 2), 'lh': (6, -2), 'turn': 0.1}, 60),
                     ({'shear': 0.5, 'body': (1, 0), 'rh': (3, 9), 'lh': (3, -9)}, 60), (fists, 60)]
    # looking around an empty spot (an enemy who lost you, a wanderer between walks), with whatever is in hand
    A['look'] = look_around(fists, ('lh', 'rh'))
    A['gun_look'] = look_around(gun, ('lh',))
    A['bat_look'] = look_around(bat_rest, ())
    A['knife_look'] = look_around(knife_rest, ('lh',))

    return A


def lying_animations():
    L = {}
    # the body falling flat along the hit (head first), then the old corpse poses take over
    L['fall'] = [(dict(torso_len=13, head_x=9, legs=((-6, 3.5, -12, 4.5), (-6, -3.5, -12, -4.5)), hands=((2, 13), (2, -13))), 50),
                 (dict(torso_len=15, head_x=11, legs=((-7, 3.5, -17, 5), (-7, -3.5, -17, -5)), hands=((4, 13), (4, -13))), 50)]
    # getting up from face down: push up, kneel, crouch (then standing takes over)
    L['getup'] = [('dead_face', 80),
                  (dict(torso_len=12, head_x=9, legs=((-6, 3.5, -18, 4), (-6, -3.5, -18, -4)), hands=((6, 10), (6, -10)), back=False), 90),
                  ('kneel', 90), ('crouch', 80)]
    # knocked into a wall: sitting slumped against it (the wall behind, -x)
    L['slump'] = [('slump', 500), ('slump2', 500)]
    L['slump_getup'] = [('slump_up1', 90), ('slump_up2', 90), ('crouch', 80)]
    L['dead_lean'] = [('dead_lean', 1000)]
    # thrown into a wall: flat against it, arms flung out, then sliding down it into a heap
    L['splat'] = [('splat', 70), ('splat_slide1', 90), ('splat_slide2', 100), ('dead_wall', 1000)]
    L['dead'] = [(0, 1000), (1, 1000), (2, 1000),
                 # sprawled on the back: arms flung up, legs wide
                 (dict(torso_len=16, head_x=12, legs=((-8, 3.5, -19, 11), (-8, -3.5, -18, -12)), hands=((15, 13), (14, -15))), 1000),
                 # crumpled face down: one knee drawn up, an arm under the body
                 (dict(torso_len=15, head_x=11, legs=((-7, 3.5, -12, 11), (-7, -3.5, -19, -3)), hands=((3, 4), (9, -13)), back=False), 1000)]
    return L


# standing-type frames used while on the floor or getting up (rendered like standing, shown on the lying layer)
FLOOR_POSES = {
    'kneel': {'legs': (((-3, 4), (-11, 4)), ((-3, -4), (-11, -4))), 'body': (-0.5, 0)},
    'crouch': {'legs': (((-2, 4), (-6, 5)), ((-2, -4), (-6, -5))), 'body': (-0.5, 0), 'rh': (2, 10), 'lh': (2, -10)},
    'slump': {'legs': (((0, 4), (14, 5)), ((0, -4), (14, -5))), 'rh': (4, 11), 'lh': (3, -11), 'head': (1.5, 1.5), 'turn': 0.35},
    'slump2': {'legs': (((0, 4), (14, 5)), ((0, -4), (14, -5))), 'rh': (4, 11), 'lh': (3, -11.5), 'head': (1.5, 2.0), 'turn': 0.45},
    'slump_up1': {'legs': (((0, 4), (8, 5)), ((0, -4), (8, -5))), 'rh': (-2, 11), 'lh': (-2, -11), 'head': (1, 1), 'turn': 0.2},
    'slump_up2': {'legs': (((-1, 4), (3, 5)), ((-1, -4), (3, -5))), 'rh': (-1, 11), 'lh': (-1, -11), 'body': (-0.5, 0)},
    'dead_lean': {'legs': (((0, 4), (14, 6)), ((0, -4), (13, -6))), 'rh': (7, 13), 'lh': (5, -13), 'head': (2.5, 3), 'turn': 0.8},
    'splat': {'rh': (-2, 13), 'lh': (-2, -13), 'body': (-1.5, 0), 'head': (-1.5, 0)},
    'splat_slide1': {'legs': (((0, 4), (6, 6)), ((0, -4), (6, -6))), 'rh': (-1, 12.5), 'lh': (-1, -12.5), 'head': (0, 0.5)},
    'splat_slide2': {'legs': (((0, 4), (11, 6)), ((0, -4), (11, -6))), 'rh': (1, 12), 'lh': (0, -12), 'head': (1, 1.5), 'turn': 0.3},
    'dead_wall': {'legs': (((0, 4), (12, 9)), ((0, -4), (13, -3))), 'rh': (6, 13), 'lh': (-1, -12), 'body': (0, 1), 'head': (2.5, 4), 'turn': 1.0},
}


def shifted(canvas, dx):
    out = blank()
    for y in range(SIZE):
        for x in range(SIZE):
            if canvas[y][x][3] and 0 <= x + dx < SIZE:
                out[y][x + dx] = canvas[y][x]
    return out


def lying_frame(ch, spec):
    """Frames on the lying layer, which sits 3 px ahead of the body (like the old corpse sprite)."""
    if isinstance(spec, int):
        return corpse(ch, spec)
    if spec == 'dead_face':
        return corpse(ch, 1)
    if isinstance(spec, str):
        return shifted(render(ch, FLOOR_POSES[spec]), -3)
    return lying(ch, **spec)

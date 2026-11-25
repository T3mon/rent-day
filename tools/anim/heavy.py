"""Draws the heavy enemy, seen from above and facing +x like everyone, into art/anim/heavy/ and art/anim/heavy.tres.

    python tools/anim/heavy.py

Its animations: idle, walk (in step with its legs), look, punch (a big right swing), push_l/push_r (a hand on a
door), lie_fall, lie_dead and legs; and his calm poses (Enemy.Pose): eat (a burger), phone, toilet (sitting with a
newspaper; toilet_up gets him up). Far wider than anyone else, with a round belly, a bald head and a
black bulletproof vest over a t-shirt, so his outline alone says which enemy he is.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from puppet import Layer, C, capsule, compose, rot
from build import write

SHIRT = (214, 206, 190, 255)       # the t-shirt under it, seen at the sleeves
SHIRT_DARK = (150, 140, 126, 255)
VEST = (58, 58, 62, 255)           # a black bulletproof vest
VEST_DARK = (34, 34, 38, 255)
PLATE = (112, 112, 120, 255)          # its plates, straps and pouches
SKIN = (198, 140, 102, 255)
SKIN_DARK = (150, 98, 70, 255)
SCALP = (176, 120, 88, 255)
PANTS = (44, 48, 66, 255)
PANTS_DARK = (28, 30, 44, 255)
SHOE = (26, 22, 26, 255)
SHOE_LIT = (70, 60, 62, 255)
EYE = (236, 232, 222, 255)
WALK = 12  # walk frames, as many as its legs
REST_R, REST_L = (2, 16), (2, -16)  # where his fists hang
SHOULDER = 11


def at(p, twist=0.0):
    q = rot(p, twist)
    return (C + q[0], C + q[1])


def upper(rh=REST_R, lh=REST_L, twist=0.0, sway=0.0, turn=0.0, nod=(0.0, 0.0), under=None, top=None):
    """Standing: arms, body and head, each outlined. twist turns the shoulders (a swing), sway leans them sideways,
    nod moves the head. under and top: extra layers below everything (his legs, sitting) and over it (what he holds)."""
    arms, body, head = Layer(), Layer(), Layer()
    capsule(body, at((-2, -6 + sway), twist), at((-2, 6 + sway), twist), 7.0, VEST, VEST_DARK)  # shoulders and back
    capsule(body, at((1, -3 + sway), twist), at((1, 3 + sway), twist), 6.5, VEST, VEST_DARK)    # belly
    capsule(body, at((-5, -4 + sway), twist), at((-5, 4 + sway), twist), 2.2, PLATE)            # the back plate
    for side in (-1, 1):
        capsule(body, at((-1, side * 5 + sway), twist), at((3, side * 5 + sway), twist), 0.8, PLATE)  # shoulder straps
    for side in (-1, 1):  # t-shirt sleeves sticking out of the armholes
        capsule(body, at((-1, side * 12.5 + sway), twist), at((-1, side * 12.5 + sway), twist), 2.2, SHIRT, SHIRT_DARK)
    for y in (-4, 0, 4):  # pouches across the front
        capsule(body, at((6, y + sway), twist), at((6, y + sway), twist), 1.2, PLATE, VEST_DARK)
    for hand, side in ((rh, 1), (lh, -1)):
        shoulder = at((-1, side * SHOULDER + sway), twist)
        capsule(arms, shoulder, at(hand, twist), 2.6, SKIN, SKIN_DARK)
        elbow = ((-1 + hand[0]) / 2, (side * SHOULDER + sway + hand[1]) / 2)
        capsule(arms, shoulder, at(((-1 + elbow[0]) / 2, (side * SHOULDER + sway + elbow[1]) / 2), twist), 3.0, SHIRT, SHIRT_DARK)  # sleeve
        capsule(arms, at(hand, twist), at(hand, twist), 3.0, SKIN, SKIN_DARK)  # fist
    neck = (nod[0], sway + nod[1])
    capsule(head, at(neck, twist), at((neck[0] + 0.5, neck[1]), twist), 4.0, SKIN, SKIN_DARK)  # the bald head
    capsule(head, at((neck[0] - 1.5, neck[1]), twist), at((neck[0] - 1.5, neck[1]), twist), 2.0, SCALP)
    nose = rot((4.5, 0), turn)
    head.put(*at((neck[0] + nose[0], neck[1] + nose[1]), twist), SKIN_DARK)
    for y in (-1, 1):  # ears
        ear = rot((0, y * 4.6), turn)
        head.put(*at((neck[0] + ear[0], neck[1] + ear[1]), twist), SKIN_DARK)
    return compose([l for l in (under, arms, body, head, top) if l is not None])


BUN, BUN_DARK, MEAT = (214, 160, 84, 255), (150, 100, 50, 255), (96, 52, 36, 255)
PAPER, PAPER_DARK, PRINT = (226, 222, 210, 255), (170, 166, 156, 255), (110, 108, 104, 255)
PHONE, PHONE_LIT = (150, 150, 160, 255), (200, 220, 235, 255)  # light, or it's lost on the black vest


def fist(l, spot):
    """A hand over everything: one held up in front of him (at his mouth, at his ear) would hide under the vest."""
    capsule(l, at(spot), at(spot), 3.0, SKIN, SKIN_DARK)


def burger(spot, bitten=False, hand=None):
    """A burger held at `spot` (by a fist at `hand`, drawn over the vest); bitten, a bite gone out of its far side."""
    l = Layer()
    if hand:
        fist(l, hand)
    capsule(l, at(spot), at(spot), 2.6, BUN, BUN_DARK)
    l.put(*at((spot[0], spot[1] - 1)), MEAT)
    l.put(*at((spot[0] + 1, spot[1])), MEAT)
    if bitten:
        for d in ((-2, 0), (-2, -1), (-1, -1), (-2, 1)):
            x, y = at((spot[0] + d[0], spot[1] + d[1]))
            l.px.pop((math.floor(x + 0.5), math.floor(y + 0.5)), None)
    return l


def phone(spot, hand):
    """A phone at `spot`, held to his ear by a fist at `hand`."""
    l = Layer()
    fist(l, hand)
    for dx in (-2, -1, 0, 1, 2):
        for dy in (-1, 0):
            l.put(*at((spot[0] + dx, spot[1] + dy)), PHONE_LIT if dx == 2 and dy == 0 else PHONE)
    return l


def newspaper(spot, fold=0.0):
    """An open newspaper held out in front, `spot` its middle; fold (0..1) turns a page over from his right."""
    l = Layer()
    x0, y0 = spot
    for y in range(-8, 9):
        if fold and y > 8 - fold * 16:
            continue
        for x in range(-3, 3):
            c = PAPER_DARK if x in (-3, 2) or y in (-8, 8) or y == 0 else (PRINT if (x + 2) % 2 == 0 and y % 3 else PAPER)
            l.put(*at((x0 + x, y0 + y)), c)
    if fold:  # the page mid-turn, standing up off the paper
        for y in range(int(8 - fold * 16), 9):
            l.put(*at((x0 - 4, y0 + y)), PAPER)
    return l


def seated(knees=11.0):
    """His legs sitting, thighs out in front to the knees (the shins go straight down, so only the shoes show)."""
    l = Layer()
    for y in (-1, 1):
        capsule(l, at((0, y * 6)), at((knees, y * 7)), 3.6, PANTS, PANTS_DARK)
        capsule(l, at((knees + 2, y * 7)), at((knees + 3, y * 7)), 2.6, SHOE, SHOE_LIT)
    return l


def legs(phase):
    """Two big shoes in step: phase 0..1 through the stride."""
    layer = Layer(28)
    s = math.sin(2 * math.pi * phase) * 5
    for y, d in ((-6, s), (6, -s)):
        for dx in range(-6, 6):
            for dy in (-2, -1, 0, 1, 2):
                c = SHOE_LIT if dy == 0 and dx > 2 else SHOE
                layer.put(14 + d + dx, 14 + y + dy, c)
    return compose([layer], 28)


def lying(spread=0.0, face_down=False, side=False):
    """Flat on his back, head to +x: legs out to -x, arms flung wide by `spread`."""
    limbs, body = Layer(), Layer()
    depth = 6.5 if side else 9.5
    for y in (-1, 1):
        capsule(limbs, at((-6, y * 6)), at((-22, y * (7 + spread * 3))), 3.4, PANTS, PANTS_DARK)
        capsule(limbs, at((-23, y * (7 + spread * 3))), at((-25, y * (7 + spread * 3))), 2.6, SHOE)
        shoulder = (6, y * (depth - 1))  # the arm grows out of the body's edge
        hand = (10 - spread * 6, y * (17 + spread * 3)) if not side else (11, y * (depth + 6))  # on his side: both reach forward
        capsule(limbs, at(shoulder), at(hand), 2.8, SKIN, SKIN_DARK)
        capsule(limbs, at(hand), at(hand), 3.2, SKIN, SKIN_DARK)
        capsule(limbs, at(shoulder), at((shoulder[0] + (hand[0] - shoulder[0]) / 3, shoulder[1] + (hand[1] - shoulder[1]) / 3)), 3.0, SHIRT, SHIRT_DARK)  # sleeve
    capsule(body, at((-4, 0)), at((6, 0)), depth, VEST, VEST_DARK)
    capsule(body, at((-2, -depth / 2)), at((-2, depth / 2)), 2.5, PLATE)  # the plate, front or back
    if not face_down:
        for y in (-4, 0, 4) if not side else (-2, 2):  # pouches
            capsule(body, at((5, y)), at((5, y)), 1.2, PLATE, VEST_DARK)
    head = Layer()
    if face_down:  # head turned to one side, so a cheek and one staring eye still show past the vest
        capsule(head, at((15, 0)), at((16, 0)), 4.2, SKIN, SKIN_DARK)
        capsule(head, at((13.5, -2.5)), at((15.5, -2.5)), 2.2, SCALP)
        for x in (15, 16):
            for y in (1, 2):
                head.put(*at((x, y)), EYE)
        head.put(*at((16, 1)), PANTS_DARK)  # the pupil
        head.put(*at((13, 2)), PANTS_DARK)  # slack mouth
        head.put(*at((13, 3)), PANTS_DARK)
        head.put(*at((20, 0)), SKIN_DARK)
    else:
        capsule(head, at((13, 0)), at((13.5, 0)), 4.0, SKIN, SKIN_DARK)  # staring eyes and a slack mouth, like everyone's dead face
        for y in (-2, -1, 1, 2):
            head.put(*at((13, y)), EYE)
            head.put(*at((14, y)), EYE if abs(y) == 1 else PANTS_DARK)
        for x in (11, 12):
            head.put(*at((x, 0)), PANTS_DARK)
        head.put(*at((17, 0)), SKIN_DARK)
    return compose([limbs, body, head])


def build():
    walk = []
    for i in range(WALK):
        s = math.sin(2 * math.pi * i / WALK)
        walk.append(upper(rh=(2 - 3 * s, 16), lh=(2 + 3 * s, -16), sway=0.8 * s))
    anims = {
        'idle': ([upper(), upper(rh=(2, 16.5), lh=(2, -16.5))], [500, 500], 72),
        'walk': (walk, [100] * WALK, 72),
        'look': ([upper(turn=t) for t in (0.0, -0.6, -0.6, 0.0, 0.6, 0.6)], [150, 250, 150, 150, 250, 150], 72),
        # a big right swing: wound back, the fist out in front, held, back
        'punch': ([upper(rh=(-3, 15), twist=-0.3), upper(rh=(16, 6), twist=0.3), upper(rh=(17, 4), twist=0.35),
                   upper(rh=(11, 10), twist=0.15), upper()], [40, 40, 60, 60, 60], 72),
        'push_l': ([upper(lh=(12, -9))], [150], 72),
        'push_r': ([upper(rh=(12, 9))], [150], 72),
        'lie_fall': ([upper(twist=0.2), lying(spread=0.5)], [70, 70], 72),
        'lie_dead': ([lying(spread=1.0), lying(face_down=True), lying(side=True)], [100] * 3, 72),
        'legs': ([legs(i / WALK) for i in range(WALK)], [100] * WALK, 28),
    }
    anims.update(poses())
    write('heavy', anims)
    return anims


def poses():
    """His calm poses: eat, phone, toilet (and toilet_up, getting up off it)."""
    held = (7, 12)  # the burger hand at rest, a little in front
    eat = [
        (upper(rh=held, top=burger((8, 12))), 600),
        (upper(rh=(8, 8), top=burger((9, 7.5), hand=(8, 8))), 110),                                         # up it comes
        (upper(rh=(7, 4), nod=(0.5, 0.5), top=burger((8, 3), hand=(7, 4.5))), 130),                           # to his mouth
        (upper(rh=(7, 3.5), nod=(1.5, 0.5), top=burger((7.5, 2.5), hand=(7, 4))), 160),                          # the bite
        (upper(rh=(8, 6), nod=(0.5, 0), top=burger((9, 5.5), bitten=True, hand=(8, 6.5))), 110),
        (upper(rh=held, top=burger((8, 12), bitten=True)), 220),                               # chewing
        (upper(rh=held, nod=(0.4, 0.4), top=burger((8, 12), bitten=True)), 220),
        (upper(rh=held, nod=(0.4, -0.4), top=burger((8, 12), bitten=True)), 220),
        (upper(rh=held, nod=(0.4, 0.4), top=burger((8, 12), bitten=True)), 220),
        (upper(rh=held, top=burger((8, 12), bitten=True)), 500),
        (upper(rh=held, lh=(3, -15), top=burger((8, 12), bitten=True)), 300),                  # wipes his hand
    ]
    ear = (1, -7)  # the phone hand, up at his left ear
    call = lambda rh=REST_R, nod=(0.0, 0.0), turn=0.0, sway=0.0: upper(rh=rh, lh=ear, nod=nod, turn=turn, sway=sway, top=phone((3, -6), ear))
    on_phone = [
        (call(), 700), (call(nod=(0.3, 0)), 300), (call(), 400),
        (call(rh=(8, 14), sway=0.5), 200), (call(rh=(12, 11), sway=0.8, turn=0.3), 300),       # waving a hand about
        (call(rh=(10, 13), sway=0.5, turn=0.3), 250), (call(rh=(12, 11), sway=0.8, turn=0.3), 250),
        (call(rh=(7, 15)), 300), (call(), 600), (call(turn=-0.4), 500), (call(nod=(0.3, 0)), 300),
    ]
    sit = lambda fold=0.0, nod=(0.8, 0.0), rh=(9, 7): upper(rh=rh, lh=(9, -7), nod=nod, under=seated(),
                                                             top=newspaper((11, 0), fold))
    toilet = [
        (sit(), 900), (sit(nod=(1.0, 0.4)), 600), (sit(nod=(1.0, -0.4)), 600), (sit(), 500),
        (sit(rh=(10, 9), fold=0.3), 100), (sit(rh=(10, 2), fold=0.7), 100), (sit(rh=(9, -3), fold=1.0), 100),  # turns a page
        (sit(), 700), (sit(nod=(1.2, 0)), 500),
    ]
    toilet_up = [  # paper dropped, up onto his feet: inside his reaction time
        (upper(rh=(8, 13), lh=(8, -13), nod=(1.5, 0), under=seated(9)), 60),
        (upper(rh=(5, 15), lh=(5, -15), nod=(1, 0), under=seated(5)), 60),
        (upper(), 40),
    ]
    out = {}
    for key, frames in (('eat', eat), ('phone', on_phone), ('toilet', toilet), ('toilet_up', toilet_up)):
        out[key] = ([f for f, ms in frames], [ms for f, ms in frames], 72)
    return out


if __name__ == '__main__':
    anims = build()
    print('heavy', len(anims), 'animations', sum(len(f) for f, m, s in anims.values()), 'frames')

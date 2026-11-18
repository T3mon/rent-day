"""Draws the dog, seen from above and facing +x like everyone, into art/anim/dog/ and art/anim/dog.tres.

    python tools/anim/dog.py

Its animations: idle, walk (in step with its blank legs), look, sit, bark, push_l/push_r (nosing a door),
lie_fall and lie_dead. A brown coat with a dark saddle and a red collar: four legs and a long body, so it
never reads as a person.
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from puppet import Layer, C, capsule, compose, rot, blank, INK
from build import write

COAT_DARK = (62, 40, 36, 255)
COAT = (146, 94, 56, 255)
COAT_LIGHT = (190, 134, 82, 255)
MUZZLE = (214, 168, 116, 255)
COLLAR = (226, 58, 92, 255)
MOUTH = (120, 30, 46, 255)
TONGUE = (222, 96, 116, 255)
WALK = 8  # walk frames, as many as its legs


def at(p):
    return (C + p[0], C + p[1])


def legs(phase=0.0):
    """Four legs in a trot: front left with back right. phase 0..1 through the step."""
    layer = Layer()
    s = math.sin(2 * math.pi * phase) * 2.5
    for x, y, d in ((2, -1, s), (2, 1, -s), (-7, -1, -s), (-7, 1, s)):
        foot = (x + d, y * 6.5)
        capsule(layer, at((x, y * 3)), at(foot), 1.2, COAT)
        layer.put(*at(foot), COAT_LIGHT)
    return layer


def head(layer, neck, turn=0.0, mouth=False):
    """Neck with its collar, ears, skull and muzzle, turned about the neck by `turn` radians."""
    def p(q):
        return at(tuple(n + v for n, v in zip(neck, rot(q, turn))))

    capsule(layer, p((-2, 0)), p((1.5, 0)), 2.3, COAT)
    for y in (-2, -1, 0, 1, 2):
        layer.put(*p((-0.5, y)), COLLAR)
    for y in (-1, 1):
        capsule(layer, p((3, y * 2)), p((1.5, y * 3.5)), 0.9, COAT_DARK)  # ears, flopped back
    capsule(layer, p((4, 0)), p((4, 0)), 3.0, COAT, COAT_DARK)                 # skull
    if mouth:  # jaws apart, the mouth red between
        capsule(layer, p((6.5, -1.6)), p((9.5, -2.2)), 1.1, MUZZLE)
        capsule(layer, p((6.5, 1.6)), p((9, 2.6)), 1.1, MUZZLE)
        for x in (7, 8, 9):
            layer.put(*p((x, 0)), MOUTH)
            layer.put(*p((x - 0.5, 0.9)), MOUTH)
        layer.put(*p((8, 1)), TONGUE)
        layer.put(*p((10, -2.2)), INK)
    else:
        capsule(layer, p((6.5, 0)), p((9, 0)), 1.6, MUZZLE)
        layer.put(*p((10, 0)), INK)
        layer.put(*p((10, -1)), INK)


def body(neck=(6.0, 0.0), turn=0.0, tail=0.0, mouth=False):
    """Standing: tail, hips, chest, saddle and head as one outlined shape."""
    layer = Layer()
    capsule(layer, at((-11, 0)), at((-16, tail)), 1.0, COAT)
    capsule(layer, at((-9, 0)), at((-4, 0)), 3.4, COAT, COAT_DARK)  # hips
    capsule(layer, at((-4, 0)), at((2, 0)), 4.2, COAT, COAT_DARK)   # chest
    capsule(layer, at((-8, 0)), at((0, 0)), 2.0, COAT_DARK)         # saddle
    head(layer, (neck[0] + 1, neck[1]), turn, mouth)
    return layer


def sitting(tail=0.0):
    """Sitting: haunches spread under the hips, front paws out ahead, tail round the side."""
    paws, layer = Layer(), Layer()
    for y in (-1, 1):
        capsule(paws, at((3, y * 2.5)), at((9, y * 2.5)), 1.2, COAT)
        paws.put(*at((9, y * 2.5)), COAT_LIGHT)
    capsule(layer, at((-7, 2)), at((-4 + tail, 7)), 1.0, COAT)
    for y in (-1, 1):
        capsule(layer, at((-5, y * 3)), at((-2, y * 4)), 2.2, COAT, COAT_DARK)  # haunches
    capsule(layer, at((-5, 0)), at((0, 0)), 4.4, COAT, COAT_DARK)
    capsule(layer, at((-4, 0)), at((0, 0)), 2.0, COAT_DARK)
    head(layer, (5, 0))
    return compose([paws, layer])


def standing(phase=0.0, **pose):
    return compose([legs(phase), body(**pose)])


def lying(curl=0.0, kick=0.0):
    """On its side, back to -y: belly and legs to +y. curl bends the head back; kick spreads the legs."""
    layer_legs, layer = Layer(), Layer()
    for x, reach in ((3, 4), (1, 2), (-6, -2), (-8, -4)):
        hip = at((x, 2.5))
        foot = at((x + reach + kick * (1 if reach > 0 else -1), 9 + abs(kick) / 2))
        capsule(layer_legs, hip, foot, 1.2, COAT)
        layer_legs.put(*foot, COAT_LIGHT)
    capsule(layer, at((-11, 0)), at((-17, 1 + curl)), 1.0, COAT)
    capsule(layer, at((-9, 0)), at((3, 0)), 4.4, COAT, COAT_DARK)
    capsule(layer, at((-8, 1.8)), at((2, 1.8)), 2.0, COAT_LIGHT)  # belly
    capsule(layer, at((-8, -3)), at((2, -3)), 1.0, COAT_DARK)     # back
    head(layer, (6, curl), turn=0.5 + curl * 0.6)
    return compose([layer_legs, layer])


def build():
    walk = [standing(i / WALK, tail=2 * math.sin(2 * math.pi * i / WALK)) for i in range(WALK)]
    anims = {
        'idle': ([standing(tail=-1.5), standing(tail=1.5)], [400, 400], 72),
        'walk': (walk, [100] * WALK, 72),
        'look': ([standing(turn=t) for t in (0.0, -0.5, -0.5, 0.0, 0.5, 0.5)], [150, 250, 150, 150, 250, 150], 72),
        'sit': ([sitting(tail=t) for t in (0, 1)], [600, 600], 72),
        'bark': ([standing(neck=(n, 0), mouth=m) for n, m in ((7.5, True), (6, False), (7.5, True), (6, False))],
                 [90, 70, 90, 150], 72),
        'push_l': ([standing(neck=(7, -1))], [150], 72),
        'push_r': ([standing(neck=(7, 1))], [150], 72),
        'lie_fall': ([standing(tail=3), lying(kick=2)], [70, 70], 72),
        'lie_dead': ([lying(), lying(curl=1.5, kick=1), lying(kick=-1.5)], [100] * 3, 72),
        'legs': ([blank(28)] * WALK, [100] * WALK, 28),  # its legs are drawn in the body
    }
    write('dog', anims)
    return anims


if __name__ == '__main__':
    anims = build()
    print('dog', len(anims), 'animations', sum(len(f) for f, m, s in anims.values()), 'frames')

"""Draws the shield enemy's shield, seen from above, into art/anim/shield.png (32x32, facing +x).

    python tools/anim/shield.py

A heavy black ballistic shield seen from above: a thick slab, bowed out to the front, lit along its front face,
with a strip of blue glass (the viewport) in the middle. Carried in front of the riot outfit
(enemies/shield_enemy.tscn).
"""
import math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from puppet import Layer, compose, png, PROJECT

SIZE, C = 32, 16
BODY = (48, 52, 62, 255)
BACK = (28, 30, 38, 255)      # the inside, towards him
LIT = (104, 112, 130, 255)    # the front face, catching the light
GLASS = (122, 196, 226, 255)  # the viewport
GLINT = (226, 244, 250, 255)
HALF = 12                     # half its width, pixels
THICK = 2.6                   # half its thickness, pixels


def bow(y):
    """How far forward the slab's middle sits at height y: furthest in the middle."""
    return C + 1 - 2.5 * (y / HALF) ** 2


def build():
    shield = Layer(SIZE)
    for py in range(SIZE):
        for px in range(SIZE):
            y = py + 0.5 - C
            if abs(y) > HALF:
                continue
            depth = px + 0.5 - bow(y)  # across the slab: negative towards him, positive the front
            if abs(depth) > THICK:
                continue
            colour = BACK if depth < -1.2 else LIT if depth > 1.2 else BODY
            if abs(y) < 4 and depth > 0.2:
                colour = GLINT if y < -2 else GLASS
            shield.put(px, py, colour)
    png(PROJECT + 'art/anim/shield.png', SIZE, SIZE, compose([shield], SIZE))


if __name__ == '__main__':
    build()
    print('shield')

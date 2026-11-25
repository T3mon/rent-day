"""Renders every animation for every character into art/anim/: one strip per animation
(art/anim/<name>/<animation>.png) and one SpriteFrames per character (art/anim/<name>.tres).

    python tools/anim/build.py

Poses and timings are in anims.py, the body parts in puppet.py.
"""
import os, re, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import puppet as pp
import anims as an

OUT = pp.PROJECT + 'art/anim/'


def save_strip(path, frames, size):
    rows = [[None] * (size * len(frames)) for _ in range(size)]
    for i, f in enumerate(frames):
        for y in range(size):
            for x in range(size):
                rows[y][i * size + x] = f[y][x]
    pp.png(path, size * len(frames), size, rows)


def build(name):
    ch = pp.Character(name)
    anims = {}
    for key, frames in an.animations().items():
        anims[key] = ([an.render(ch, p) for p, ms in frames], [ms for p, ms in frames], 72)
    for key, frames in an.lying_animations().items():
        anims['lie_' + key] = ([an.lying_frame(ch, spec) for spec, ms in frames], [ms for spec, ms in frames], 72)
    legs = pp.walk_legs(ch)
    anims['legs'] = (legs, [100] * len(legs), 28)
    write(name, anims)
    return anims


def write(name, anims):
    """anims: {animation: (frames, milliseconds per frame, frame size)}. Saves the strips and the SpriteFrames."""
    os.makedirs(OUT + name, exist_ok=True)
    for key, (frames, ms, size) in anims.items():
        save_strip(f'{OUT}{name}/{key}.png', frames, size)
    # SpriteFrames: one animation per sheet, frame durations in seconds (speed 1). Written exactly as the
    # editor saves it (ids on every picture, animations by name, one value per line), or saving a level
    # makes the editor rewrite the file.
    res, entries = [], []
    for i, key in enumerate(anims):
        uid = _uid(f'{OUT}{name}/{key}.png.import')  # shortcut: a new strip has none until Godot imports it, so run this again after
        res.append(f'[ext_resource type="Texture2D"{f" uid=\"{uid}\"" if uid else ""} path="res://art/anim/{name}/{key}.png" id="t{i}"]')
    ids = {key: i for i, key in enumerate(anims)}
    subs = []
    for key in sorted(anims):
        frames, ms, size = anims[key]
        i, fr = ids[key], []
        for j in range(len(frames)):
            sid = f'a{i}_{j}'
            subs.append(f'[sub_resource type="AtlasTexture" id="{sid}"]\natlas = ExtResource("t{i}")\nregion = Rect2({j * size}, 0, {size}, {size})\n')
            fr.append(f'{{\n"duration": {ms[j] / 1000!r},\n"texture": SubResource("{sid}")\n}}')
        entries.append(f'{{\n"frames": [{", ".join(fr)}],\n"loop": {int(key in LOOPS)},\n"name": &"{key}",\n"speed": 1.0\n}}')
    # keep the id Godot gave the file: scenes may refer to it by that
    path = f'{OUT}{name}.tres'
    uid = _uid(path)
    head = '[gd_resource type="SpriteFrames" format=3%s]' % (f' uid="{uid}"' if uid else '')
    text = head + '\n\n' + '\n'.join(res) + '\n\n' + '\n'.join(subs) + '\n[resource]\nanimations = [' + ', '.join(entries) + ']\n'
    open(path, 'w', encoding='utf-8', newline='\n').write(text)


def _uid(path):
    """The uid at the top of a .tres or in a .import file, or None."""
    if not os.path.exists(path):
        return None
    m = re.search(r'uid="(uid://\w+)"', open(path, encoding='utf-8').read(2000))
    return m and m.group(1)


LOOPS = {'idle', 'walk', 'gun_idle', 'gun_walk', 'bat_idle', 'bat_walk', 'knife_idle', 'knife_walk', 'choke',
         'shield', 'gun_shield', 'bat_shield', 'knife_shield', 'held',
         'smoke', 'phone', 'hide', 'sit', 'lean', 'talk', 'lie_slump', 'look', 'gun_look', 'bat_look', 'knife_look'}

if __name__ == '__main__':
    for name in ('player', 'enemy_a', 'enemy_b', 'enemy_c', 'enemy_d', 'riot'):
        anims = build(name)
        print(name, len(anims), 'animations', sum(len(f) for f, m, s in anims.values()), 'frames')

"""Renders every animation for every character into art/anim/: one strip per animation
(art/anim/<name>/<animation>.png) and one SpriteFrames per character (art/anim/<name>.tres).

    python tools/anim/build.py

Poses and timings are in anims.py, the body parts in puppet.py.
"""
import os, re, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import puppet as pp
import anims as an
import headband as hb
import dying

OUT = pp.PROJECT + 'art/anim/'


def save_strip(path, frames, size):
    rows = [[None] * (size * len(frames)) for _ in range(size)]
    for i, f in enumerate(frames):
        for y in range(size):
            for x in range(size):
                rows[y][i * size + x] = f[y][x]
    pp.png(path, size * len(frames), size, rows)


EXTRA = {'dodger': an.dodge_animations}  # a character's own animations, on top of everyone's
DYING = {'player'}  # who has the dying animations (dying.py)


def build(name):
    ch = pp.Character(name)
    standing = an.animations()
    if name in EXTRA:
        standing.update(EXTRA[name]())
    if hb.worn(ch):
        hb.add_tails(ch, standing, an.motion_for, LOOPS)
    anims = {}
    for key, frames in standing.items():
        anims[key] = ([an.render(ch, p) for p, ms in frames], [ms for p, ms in frames], 72)
    for key, frames in an.lying_animations().items():
        anims['lie_' + key] = ([an.lying_frame(ch, spec) for spec, ms in frames], [ms for spec, ms in frames], 72)
    if name in DYING:
        anims.update(dying.animations(ch))
    legs = pp.walk_legs(ch)
    anims['legs'] = (legs, [100] * len(legs), 28)
    write(name, anims)
    return anims


def write(name, anims):
    """anims: {animation: (frames, milliseconds per frame, frame size[, slide per frame])}. Saves the strips and the
    SpriteFrames; a slide goes in as metadata slide_<animation> (Character paints goo along it)."""
    os.makedirs(OUT + name, exist_ok=True)
    for key, (frames, ms, size, *_) in anims.items():
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
        frames, ms, size, *_ = anims[key]
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
    meta = ''.join(f'metadata/slide_{key} = PackedFloat32Array({", ".join(map(str, a[3]))})\n'
                   for key, a in sorted(anims.items()) if len(a) > 3)
    text = head + '\n\n' + '\n'.join(res) + '\n\n' + '\n'.join(subs) + '\n[resource]\nanimations = [' + ', '.join(entries) + ']\n' + meta
    open(path, 'w', encoding='utf-8', newline='\n').write(text)


def _uid(path):
    """The uid at the top of a .tres or in a .import file, or None."""
    if not os.path.exists(path):
        return None
    f = open(path, encoding='utf-8')
    text = f.read() if path.endswith('.import') else f.readline()  # a .tres: the first line only, not a picture's
    m = re.search(r'uid="(uid://\w+)"', text)
    return m and m.group(1)


LOOPS = {'idle', 'walk', 'gun_idle', 'gun_walk', 'bat_idle', 'bat_walk', 'knife_idle', 'knife_walk', 'choke',
         'shield', 'gun_shield', 'bat_shield', 'knife_shield', 'held',
         'smoke', 'phone', 'hide', 'sit', 'lean', 'talk', 'lie_slump', 'look', 'gun_look', 'bat_look', 'knife_look'}

if __name__ == '__main__':
    for name in sys.argv[1:] or ('player', 'enemy_a', 'enemy_b', 'enemy_c', 'enemy_d', 'riot', 'dodger'):
        anims = build(name)
        print(name, len(anims), 'animations', sum(len(a[0]) for a in anims.values()), 'frames')

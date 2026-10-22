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
    os.makedirs(OUT + name, exist_ok=True)
    anims = {}
    for key, frames in an.animations().items():
        anims[key] = ([an.render(ch, p) for p, ms in frames], [ms for p, ms in frames], 72)
    for key, frames in an.lying_animations().items():
        anims['lie_' + key] = ([an.lying_frame(ch, spec) for spec, ms in frames], [ms for spec, ms in frames], 72)
    legs = pp.walk_legs(ch)
    anims['legs'] = (legs, [100] * len(legs), 28)
    for key, (frames, ms, size) in anims.items():
        save_strip(f'{OUT}{name}/{key}.png', frames, size)
    # SpriteFrames: one animation per sheet, frame durations in seconds (speed 1)
    res, subs, entries = [], [], []
    for i, (key, (frames, ms, size)) in enumerate(anims.items()):
        res.append(f'[ext_resource type="Texture2D" path="res://art/anim/{name}/{key}.png" id="t{i}"]')
        fr = []
        for j in range(len(frames)):
            sid = f'a{i}_{j}'
            subs.append(f'[sub_resource type="AtlasTexture" id="{sid}"]\natlas = ExtResource("t{i}")\nregion = Rect2({j * size}, 0, {size}, {size})\n')
            fr.append(f'{{"duration": {ms[j] / 1000:.3f}, "texture": SubResource("{sid}")}}')
        loop = 'true' if key in LOOPS else 'false'
        entries.append(f'{{\n"frames": [{", ".join(fr)}],\n"loop": {loop},\n"name": &"{key}",\n"speed": 1.0\n}}')
    # keep the id Godot gave the file: scenes may refer to it by that
    path = f'{OUT}{name}.tres'
    uid = re.search(r' uid="(uid://\w+)"', open(path, encoding='utf-8').readline()) if os.path.exists(path) else None
    head = '[gd_resource type="SpriteFrames" format=3%s]' % (f' uid="{uid.group(1)}"' if uid else '')
    text = head + '\n\n' + '\n'.join(res) + '\n\n' + '\n'.join(subs) + '\n[resource]\nanimations = [' + ', '.join(entries) + ']\n'
    open(path, 'w', encoding='utf-8', newline='\n').write(text)
    return anims


LOOPS = {'idle', 'walk', 'gun_idle', 'gun_walk', 'bat_idle', 'bat_walk', 'knife_idle', 'knife_walk', 'choke',
         'shield', 'gun_shield', 'bat_shield', 'knife_shield', 'held',
         'smoke', 'phone', 'hide', 'sit', 'lean', 'talk', 'lie_slump'}

if __name__ == '__main__':
    for name in ('collector', 'goon_a', 'goon_b', 'goon_c', 'goon_d'):
        anims = build(name)
        print(name, len(anims), 'animations', sum(len(f) for f, m, s in anims.values()), 'frames')

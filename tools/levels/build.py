"""Levels 1 to 5 as floor plans, and the command that checks them and writes their scenes.

    python tools/levels/build.py            check every level, draw its plan
    python tools/levels/build.py 3          just level 3
    python tools/levels/build.py 6 --write  a new level 6: also write levels/level_6.tscn

Each run prints the level's numbers (size in screens, enemies per screen, rooms) and anything that breaks the
level rules, and draws its plan to tools/levels/plans/level_<n>.svg (and .png when rsvg-convert is installed).
--write never writes over a level that is already there: once a level is edited in Godot, the scene is the
truth and its plan here is only where it started. Delete the scene first to build it again.
A new level: a function here that returns its Floor, added to FLOORS (see docs/architecture.md).
"""
import sys, os, shutil, subprocess
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from floorplan import Floor, ROOT

PLANS = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'plans')


def lobby():
    """Ground floor of an office block: one big pillared lobby you walk into from the street, glass offices on
    its side, a loop round through the lounge, cafe and security room, the stairs up at the back."""
    f = Floor('level_1.tscn', 'Level1', ground=True)
    f.room('cafe', 'room', 'terracotta', (96, 96, 304, 240))
    f.room('security', 'room', 'concrete', (312, 136, 416, 240))
    f.room('lift hall', 'hall', 'red', (424, 96, 752, 240))
    f.room('lounge', 'room', 'blue', (96, 248, 248, 432))
    f.room('lobby', 'room', 'stone', (256, 248, 616, 496))
    f.room('office A', 'room', 'teal', (624, 248, 752, 368))
    f.room('office B', 'room', 'lino', (624, 376, 752, 496))

    f.gap_h(496, 400, 472, thick=16)          # the front entrance
    f.gap_v(248, 296, 392)                    # lobby - lounge
    f.glass_v(616, 256, 320); f.gap_v(616, 328, 368)   # office A: glass front and a doorway
    f.gap_v(616, 376, 416); f.glass_v(616, 424, 488)   # office B
    f.gap_h(240, 496, 592)                    # lobby - lift hall
    f.door_h(240, 352, 0)                     # lobby - security
    f.door_v(416, 176, 90)                    # security - lift hall
    f.gap_v(304, 168, 216)                    # cafe - security
    f.gap_h(240, 120, 200)                    # cafe - lounge

    # lobby: reception desk, four pillars, a bench, plants, a runner from the door
    f.prop('rug_runner', 436, 452); f.prop('rug_runner', 436, 394)
    f.prop('counter', 404, 304); f.prop('counter', 468, 304)
    f.prop('office_chair', 404, 286); f.prop('office_chair', 468, 286)
    for x in (336, 536):
        for y in (368, 448):
            f.prop('pillar', x, y)
    f.prop('couch', 288, 260)
    f.prop('plant', 272, 482); f.prop('plant', 600, 482); f.prop('plant', 600, 262)
    # lounge
    f.prop('aquarium', 104, 344, 270)
    f.prop('rug_shag', 172, 386)
    f.prop('couch', 172, 420, 180); f.prop('coffee_table', 172, 388)
    f.prop('armchair', 128, 384, 270); f.prop('armchair', 216, 384, 90)
    f.prop('plant', 108, 260); f.prop('plant', 236, 260); f.prop('floor_lamp', 106, 420)
    # cafe
    f.prop('counter', 136, 104); f.prop('stove', 180, 106); f.prop('counter', 224, 104); f.prop('fridge', 272, 105)
    f.prop('round_table', 160, 184)
    f.prop('chair', 160, 160); f.prop('chair', 136, 184, 270); f.prop('chair', 184, 184, 90)
    f.prop('table', 256, 192); f.prop('chair', 256, 172); f.prop('chair', 256, 212, 180)
    # security
    f.prop('tv', 348, 142); f.prop('desk', 348, 166); f.prop('office_chair', 348, 186, 180); f.prop('safe', 322, 150)
    # lift hall
    f.prop('couch', 520, 106); f.prop('trash_bins', 600, 102); f.prop('plant', 440, 108); f.prop('plant', 680, 106)
    f.prop('couch', 680, 230, 180)
    # office A
    f.prop('desk', 680, 308, 270); f.prop('office_chair', 700, 308, 90); f.prop('filing_cabinets', 720, 254)
    f.prop('plant', 740, 356)
    # office B: nobody in, a shotgun on the floor
    f.prop('copier', 728, 388); f.prop('box', 740, 480); f.prop('box', 724, 480); f.prop('box', 740, 464)
    f.prop('plant', 636, 484)
    f.weapon('shotgun', 680, 440)

    f.stairs(up=(728, 124))
    f.start(436, 540)
    f.weapon('bat', 500, 536)

    E = f.enemy
    E(404, 286, face=90, pose='sit')                                # receptionist
    E(444, 282, face=180, pose='talk', gun='knife', look='b')
    E(300, 470, act='patrol', gun='bat', look='c')
    E(172, 418, face=270, pose='sit', gun='uzi', look='d')          # lounge
    E(128, 384, face=0, pose='sit')
    E(190, 300, act='wander', stay=True, gun='bat', look='b')
    E(224, 124, face=90, pose='drink', gun='knife')                 # cafe
    E(160, 162, face=90, pose='sit', look='c')
    E(184, 184, face=180, pose='sit', gun='shotgun')
    E(348, 186, face=270, pose='sit', look='d')                     # security
    E(392, 216, face=180, pose='smoke', gun='bat')
    E(472, 200, act='patrol', gun='uzi', look='b')                  # lift hall
    E(660, 128, face=180, pose='smoke')
    E(634, 128, face=0, pose='talk', gun='knife', look='c')
    E(700, 308, face=180, pose='sit', look='d')                     # office A
    E(660, 344, act='wander', stay=True, look='b')
    return f


def spa():
    """A spa: a loop of hallway round a glass-walled pool hall, the rooms off the loop. Dogs follow the walls,
    so they run round and round the loop and come past every doorway."""
    f = Floor('level_2.tscn', 'Level2')
    f.room('gym', 'room', 'concrete', (64, 96, 272, 184))
    f.room('juice bar', 'room', 'checker', (280, 64, 456, 184))
    f.room('office', 'room', 'parquet', (464, 96, 704, 184))
    f.room('loop', 'hall', 'blue', (64, 192, 584, 256), (64, 256, 128, 384), (64, 384, 584, 448), (520, 256, 584, 384))
    f.room('pool', 'room', 'pool', (136, 264, 512, 376))
    f.room('massage', 'room', 'lino', (592, 192, 704, 312))
    f.room('showers', 'room', 'pink', (592, 320, 704, 384))

    f.gap_h(184, 160, 224); f.gap_h(184, 336, 400); f.gap_h(184, 480, 528)   # gym, juice bar, office - loop
    f.gap_v(272, 120, 168)                                                  # gym - juice bar
    f.door_v(456, 136, 90)                                                  # juice bar - office
    f.gap_h(184, 640, 688)                                                  # office - massage
    f.door_v(584, 232, 90)                                                  # loop - massage
    f.gap_v(584, 328, 376)                                                  # loop - showers
    for wy in (256, 376):                                                   # the pool's glass sides
        f.glass_h(wy, 160, 288); f.glass_h(wy, 352, 480)
    f.gap_v(128, 296, 344); f.gap_v(512, 296, 344)                          # its two ends open

    # gym
    f.prop('weight_bench', 104, 120); f.prop('weight_bench', 176, 120); f.prop('rug_tiger', 140, 158)
    f.prop('box', 252, 106); f.prop('box', 252, 122)
    # juice bar
    f.prop('bar_counter', 340, 104); f.prop('fridge', 300, 74); f.prop('jukebox', 440, 74)
    f.prop('chair', 324, 124, 180); f.prop('chair', 356, 124, 180); f.prop('plant', 292, 172)
    # office, with the stairs up
    f.prop('desk', 560, 128, 180); f.prop('office_chair', 560, 110)
    f.prop('filing_cabinets', 520, 102); f.prop('couch', 616, 172, 180)
    # pool hall
    for x in (200, 248, 400, 448):
        f.prop('single_bed', x, 320)
    f.prop('clawfoot_tub', 324, 344, 90)
    # massage room, showers
    f.prop('single_bed', 656, 260); f.prop('single_bed', 690, 260); f.prop('plant', 696, 204)
    f.prop('shower', 690, 336); f.prop('shower', 690, 368); f.prop('sink', 624, 328)
    # the loop
    f.prop('couch', 300, 202); f.prop('couch', 300, 438, 180); f.prop('plant', 76, 204)
    f.prop('plant', 572, 436)

    f.stairs(up=(680, 120), down=(96, 424))
    f.weapon('pistol', 112, 352)

    E = f.enemy
    E(200, 220, 'dog', face=0)                                         # the loop
    E(440, 420, 'dog', face=180)
    E(248, 322, face=90, pose='sit', gun='uzi')                         # pool hall
    E(400, 322, face=90, pose='sit', look='b')
    E(324, 300, face=90, pose='smoke', gun='bat', look='c')
    E(140, 160, face=0, pose='talk', gun='bat')                         # gym
    E(168, 160, face=180, pose='talk', look='d')
    E(232, 150, 'dog', face=180, act='static')
    E(340, 84, face=90, gun='shotgun', look='b')                        # juice bar
    E(324, 128, face=270, pose='sit', gun='bat')
    E(356, 128, face=270, pose='drink', look='c')
    E(560, 110, face=90, pose='sit', gun='uzi', look='d')               # office
    E(620, 120, face=180, pose='phone')
    E(656, 262, face=90, pose='sit', look='b')                          # massage
    E(620, 290, face=0, pose='smoke', gun='knife')
    return f


def pod(f, cx, cy):
    """Four desks back to back with their chairs: 72 x 36, blocks walking, not bullets."""
    for dx in (-18, 18):
        f.prop('desk', cx + dx, cy - 9, 180); f.prop('office_chair', cx + dx, cy - 30)
        f.prop('desk', cx + dx, cy + 9); f.prop('office_chair', cx + dx, cy + 30, 180)


def office():
    """An open-plan office: one big room round a glass meeting box, desks in pods. You can see and shoot
    across it but have to walk round the desks. Two heavy enemies; the shotgun is the answer to them."""
    f = Floor('level_3.tscn', 'Level3')
    f.room('open office', 'room', 'blue', (64, 96, 488, 192), (64, 192, 168, 288), (384, 192, 488, 288), (64, 288, 488, 384))
    f.room('meeting A', 'room', 'oak', (176, 200, 272, 280))
    f.room('meeting B', 'room', 'parquet', (280, 200, 376, 280))
    f.room('boss', 'room', 'cherry', (496, 64, 704, 200))
    f.room('kitchen', 'room', 'checker', (496, 208, 704, 384))
    f.room('toilets', 'room', 'pink', (64, 392, 160, 512))
    f.room('copy room', 'room', 'concrete', (168, 392, 272, 512))
    f.room('lift hall', 'hall', 'red', (280, 392, 608, 512))

    f.glass_h(192, 176, 272); f.glass_h(280, 176, 272); f.gap_v(168, 216, 264)   # meeting A
    f.glass_h(192, 280, 376); f.glass_h(280, 280, 376); f.gap_v(376, 216, 264)   # meeting B
    f.gap_h(384, 304, 400)                    # lift hall - office
    f.gap_v(488, 296, 360)                    # office - kitchen
    f.door_v(488, 136, 90)                    # office - boss
    f.gap_h(200, 600, 648)                    # boss - kitchen
    f.gap_h(384, 528, 576)                    # lift hall - kitchen
    f.gap_v(272, 416, 464)                    # lift hall - copy room
    f.door_h(384, 112, 0)                     # office - toilets

    pod(f, 120, 136); pod(f, 248, 136); pod(f, 376, 136); pod(f, 200, 336)
    f.prop('filing_cabinets', 70, 232, 270); f.prop('filing_cabinets', 70, 264, 270); f.prop('plant', 156, 204)
    f.prop('couch', 476, 240, 90); f.prop('coffee_table', 446, 240, 90); f.prop('plant', 476, 204)
    f.prop('plant', 476, 372); f.prop('copier', 300, 296)
    # meeting rooms
    for cx in (224, 328):
        f.prop('table', cx, 240); f.prop('chair', cx, 220); f.prop('chair', cx, 260, 180)
    # boss, with the stairs up
    f.prop('desk', 600, 112, 180); f.prop('office_chair', 600, 92)
    f.prop('couch', 690, 160, 90); f.prop('coffee_table', 660, 160, 90); f.prop('safe', 520, 80)
    f.prop('plant', 520, 188); f.prop('rug_memphis', 600, 150)
    # kitchen
    f.prop('kitchen_island', 600, 296); f.prop('counter', 697, 256, 90); f.prop('fridge', 696, 304)
    f.prop('stove', 696, 326); f.prop('table', 552, 248); f.prop('chair', 552, 228); f.prop('chair', 552, 268, 180)
    # toilets, copy room, lift hall
    f.prop('toilet', 72, 460, 270); f.prop('toilet', 72, 430, 270); f.prop('sink', 155, 430, 90)
    f.prop('copier', 220, 400); f.prop('filing_cabinets', 250, 498, 180); f.prop('box', 180, 404)
    f.weapon('shotgun', 210, 466)
    f.prop('couch', 440, 494, 180); f.prop('plant', 596, 404); f.prop('trash_bins', 380, 498)

    f.stairs(up=(680, 88), down=(304, 480))
    f.weapon('pistol', 344, 452)

    E = f.enemy
    E(560, 440, face=180, pose='talk', look='b')                       # lift hall, far end
    E(536, 440, face=0, pose='talk', gun='bat')
    E(72, 460, 'heavy', face=0, act='static', pose='toilet')           # toilets
    E(92, 430, face=180, pose='pee', gun='knife', look='c')
    E(320, 360, 'heavy', face=180)                                     # open office
    E(102, 106, face=90, pose='sit', gun='uzi', look='d')
    E(138, 166, face=270, pose='sit', gun='bat', look='c')
    E(266, 166, face=270, pose='sit')
    E(358, 106, face=90, pose='sit', look='b')
    E(430, 210, face=180, pose='talk', gun='knife')
    E(406, 210, face=0, pose='talk', look='c')
    E(110, 336, act='wander', stay=True, gun='bat')
    E(224, 222, face=90, pose='sit', look='b')                         # meeting A
    E(224, 260, face=270, pose='sit', gun='shotgun')
    E(600, 270, face=90, pose='drink')                                 # kitchen
    E(630, 322, face=180, pose='smoke', gun='knife', look='d')
    E(600, 92, face=90, pose='sit', gun='uzi', look='c')               # boss
    E(640, 150, face=180, pose='phone', look='b')
    return f


def club():
    """A club: a big dance floor with a crowd on it, a bar along one side, the VIP lounge behind glass,
    a stage, toilets. Dodgers dance in the crowd."""
    f = Floor('level_4.tscn', 'Level4')
    f.room('back office', 'room', 'cherry', (64, 96, 224, 200))
    f.room('bar', 'room', 'purple', (232, 96, 488, 200))
    f.room('vip', 'room', 'velvet', (496, 64, 672, 200))
    f.room('toilets', 'room', 'pink', (64, 208, 176, 312))
    f.room('cloakroom', 'room', 'honey', (64, 320, 176, 432))
    f.room('dance floor', 'room', 'checker', (184, 208, 568, 480))
    f.room('stage', 'room', 'oak', (576, 208, 672, 352))
    f.room('storage', 'room', 'concrete', (576, 360, 672, 480))

    f.gap_h(200, 256, 336); f.gap_h(200, 400, 480)       # bar - dance floor
    f.door_v(224, 136, 90)                               # back office - bar
    f.glass_v(488, 96, 160); f.gap_v(488, 160, 200)      # vip - bar
    f.glass_h(200, 504, 568)                             # vip - dance floor
    f.door_h(200, 616, 0)                                # vip - stage
    f.gap_v(568, 232, 328)                               # stage - dance floor
    f.gap_v(568, 384, 432)                               # storage - dance floor
    f.door_v(176, 256, 90)                               # toilets - dance floor
    f.gap_v(176, 344, 408)                               # cloakroom - dance floor

    # dance floor
    for x in (280, 472):
        for y in (296, 400):
            f.prop('pillar', x, y)
    f.prop('rug_sun', 376, 348); f.prop('cabinet', 556, 220); f.prop('cabinet', 556, 340)
    f.prop('round_table', 216, 448); f.prop('chair', 216, 424); f.prop('round_table', 536, 456)
    # bar
    for x in (310, 370, 430):
        f.prop('bar_counter', x, 128)
    for x in (320, 360, 400):
        f.prop('chair', x, 146, 180)
    f.prop('jukebox', 474, 108)
    # vip
    f.prop('velvet_sectional', 652, 88, 90); f.prop('coffee_table', 620, 112); f.prop('armchair', 584, 100, 270)
    f.prop('plant', 508, 76); f.prop('rug_tiger', 600, 150)
    # stage, storage
    f.prop('record_player', 600, 280, 90); f.prop('cabinet', 660, 300); f.prop('cabinet', 660, 340)
    f.prop('box', 660, 372); f.prop('box', 644, 372); f.prop('box', 660, 388); f.prop('duffel_bag', 600, 470)
    # toilets, cloakroom
    f.prop('toilet', 72, 232, 270); f.prop('toilet', 72, 264, 270)
    f.prop('sink', 110, 213); f.prop('sink', 140, 213)
    f.prop('counter', 120, 376, 90); f.prop('wardrobe', 71, 344, 270); f.prop('wardrobe', 71, 400, 270)
    # back office, with the stairs up
    f.prop('desk', 160, 120, 180); f.prop('office_chair', 160, 102); f.prop('safe', 80, 188)
    f.prop('filing_cabinets', 150, 194, 180)

    f.stairs(up=(88, 120), down=(648, 456))
    f.weapon('pistol', 612, 440)

    E = f.enemy
    E(330, 340, 'dodger', act='wander', stay=True)                     # dance floor
    E(420, 380, 'dodger', act='wander', stay=True, gun='bat')
    E(376, 300, 'dodger', act='wander', stay=True)
    E(300, 250, act='wander', stay=True, look='b')
    E(240, 330, face=0, pose='smoke', gun='uzi', look='c')
    E(624, 280, face=180, look='d')                                    # stage: the DJ
    E(620, 330, 'dodger', act='wander', stay=True)
    E(370, 108, face=90, gun='shotgun')                                # bar
    E(320, 148, face=270, pose='sit', gun='bat', look='b')
    E(400, 150, face=270, pose='drink', look='c')
    E(660, 100, face=180, pose='sit', gun='uzi')                       # vip
    E(584, 100, face=0, pose='sit', look='d')
    E(92, 232, face=180, pose='pee', gun='knife')                      # toilets
    E(130, 290, face=270, pose='smoke', look='b')
    E(160, 102, face=90, pose='sit', gun='uzi', look='c')              # back office
    E(120, 188, face=270, pose='lean')
    return f


def penthouse():
    """The landlord's penthouse, the last floor: a gallery round a glass-walled pool, rooms off it. Two shield
    enemies hold the gallery; every other type is here too."""
    f = Floor('level_5.tscn', 'Level5')
    f.room('kitchen', 'room', 'terracotta', (64, 64, 248, 176))
    f.room('study', 'room', 'cherry', (256, 64, 440, 176))
    f.room('bedroom', 'room', 'purple', (448, 64, 616, 176))
    f.room('bathroom', 'room', 'pink', (624, 64, 704, 176))
    f.room('gallery', 'hall', 'concrete', (64, 184, 456, 256), (64, 256, 152, 416))
    f.room('pool', 'room', 'pool', (160, 264, 456, 480))
    f.room('cinema', 'room', 'velvet', (464, 184, 704, 336))
    f.room('lounge', 'room', 'teal', (464, 344, 640, 448))

    f.gap_h(176, 104, 168); f.gap_h(176, 312, 376)        # gallery - kitchen, study
    f.door_v(440, 96, 90)                                 # study - bedroom
    f.gap_v(616, 104, 144)                                # bedroom - bathroom
    f.door_h(176, 488, 0)                                 # bedroom - cinema
    f.glass_h(256, 176, 304); f.gap_h(256, 328, 392)      # gallery - pool
    f.glass_v(152, 304, 400)
    f.gap_v(456, 200, 248)                                # gallery - cinema
    f.gap_v(456, 368, 432)                                # pool - lounge
    f.gap_h(336, 560, 608)                                # cinema - lounge

    # gallery
    f.prop('aquarium', 200, 190); f.prop('plant', 76, 196); f.prop('plant', 440, 196); f.prop('plant', 76, 268)
    f.prop('floor_lamp', 290, 248); f.prop('rug_runner', 108, 300)
    # pool
    for x in (224, 392):
        for y in (336, 432):
            f.prop('pillar', x, y)
    for x in (256, 288, 328, 360):
        f.prop('single_bed', x, 456, 180)
    f.prop('clawfoot_tub', 420, 300, 90); f.prop('plant', 172, 276); f.prop('plant', 444, 468)
    # kitchen
    f.prop('counter', 96, 72); f.prop('counter', 160, 72); f.prop('stove', 208, 73); f.prop('fridge', 232, 73)
    f.prop('kitchen_island', 128, 128); f.prop('table', 200, 136); f.prop('chair', 200, 116); f.prop('chair', 200, 156, 180)
    f.weapon('shotgun', 96, 160)
    # study
    for x in (280, 320, 360):
        f.prop('vhs_shelf', x, 68)
    f.prop('desk', 330, 130); f.prop('office_chair', 330, 148, 180); f.prop('armchair', 276, 150, 270); f.prop('floor_lamp', 268, 128)
    # bedroom, bathroom
    f.prop('sunset_bed', 540, 88); f.prop('nightstand', 512, 72); f.prop('nightstand', 568, 72); f.prop('dresser', 590, 170, 180)
    f.prop('rug_shag', 540, 136)
    f.prop('clawfoot_tub', 690, 96); f.prop('toilet', 650, 72); f.prop('sink', 690, 160, 180)
    # cinema
    f.prop('tv', 698, 260, 90)
    for x in (540, 600):
        f.prop('couch', x, 236, 270); f.prop('couch', x, 292, 270)
    # lounge
    f.prop('bar_counter', 600, 396, 90); f.prop('pool_table', 520, 400); f.prop('plant', 476, 356)

    f.stairs(down=(108, 392))
    f.weapon('pistol', 128, 330)

    E = f.enemy
    E(280, 222, 'shield', face=180)                                    # gallery
    E(420, 222, 'shield', face=180)
    E(240, 196, face=90, pose='lean', gun='knife', look='c')
    E(300, 380, 'dodger', act='wander', stay=True)                     # pool
    E(240, 452, face=270, pose='sit', gun='uzi', look='b')
    E(344, 452, face=270, pose='sit', look='d')
    E(170, 110, 'heavy', face=90, act='static', pose='eat')            # kitchen
    E(128, 100, face=90, pose='drink', gun='knife')
    E(330, 148, face=270, pose='sit', look='b')                        # study
    E(400, 110, face=180, pose='phone', gun='bat')
    E(540, 136, 'dog', face=90, act='static')                          # bedroom
    E(500, 150, face=270, pose='smoke', gun='shotgun')
    E(500, 260, 'dodger', act='wander', stay=True, gun='bat')          # cinema
    E(600, 236, face=0, pose='sit', look='c')
    E(600, 292, face=0, pose='sit', gun='uzi')
    E(624, 396, face=180, gun='shotgun', look='d')                     # lounge
    E(540, 372, face=0, pose='drink', gun='knife', look='b')
    return f


FLOORS = [lobby, spa, office, club, penthouse]


def report(name, f):
    s = f.stats()
    print(f'== {name}: {s["area"] / 1000:.0f}k px2 = {s["screens"]:.2f} screens, {s["enemies"]} enemies '
          f'({s["per_screen"]:.1f}/screen), melee {s["melee"]}, walking {s["walk"]}, kinds {s["kinds"]}')
    print(f'   doors {s["doors"]}, glass panes {s["panes"]}, nearest-enemy median {s["nn"]:.0f} px, biggest room '
          f'{s["biggest"] * 100:.0f}%, small rooms {s["small"]}, longer than sight {s["long"]}, empty {s["empty"]}, '
          f'weapons {s["weapons"]}, exit {s["exit"]:.0f} px from start')
    for name, w, h, a, e in s['rooms']:
        print(f'   {name:12s} {w:4d} x {h:<4d} {a / 1000:5.1f}k  {e} enemies')
    for p in f.problems:
        print('   !', p)
    return s


def main():
    args = sys.argv[1:]
    write = '--write' in args
    pick = [int(a) for a in args if a != '--write'] or range(1, len(FLOORS) + 1)
    os.makedirs(PLANS, exist_ok=True)
    for n in pick:
        f = FLOORS[n - 1]()
        f.build(); f.check()
        name = f.file[:-5]
        report(f'{name} ({FLOORS[n - 1].__name__})', f)
        f.svg(f'{PLANS}/{name}.svg')
        if shutil.which('rsvg-convert'):
            subprocess.run(['rsvg-convert', '-z', '1.5', f'{PLANS}/{name}.svg', '-o', f'{PLANS}/{name}.png'], check=True)
        if write:
            path = f'{ROOT}/levels/{f.file}'
            if os.path.exists(path):  # it may hold hand edits
                print(f'   levels/{f.file} is already there, not written (delete it first to build it again)')
            else:
                open(path, 'w').write(f.tscn())
                print('   wrote levels/' + f.file)


if __name__ == '__main__':
    main()

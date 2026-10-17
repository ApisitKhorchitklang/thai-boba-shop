"""Extra assets for the living soi (exec'd by thai_boba_assets.py, which provides Asset, mat, PAL, R ...).

    SM_ToppingBar          topping station: Tub_Konjac / Tub_Jelly / Tub_Pudding (+ Bar_Body)
    SM_PickupRack          delivery pick-up rack for the counter (3 cup rings + sign)
    SM_Cat                 stray cat in parts (Cat_Body, Cat_Head, Cat_Tail, Cat_Leg_FL/FR/BL/BR) for procedural animation
    SM_RedLanterns         Chinese New Year lanterns on a wire (4.4 m)
    SM_JayFlags            vegetarian festival flags: yellow with a red "เจ" (4.4 m)
    SM_MotherBunting       Mother's Day: light blue / white flags and a jasmine garland (4.4 m)
    SM_OpeningWreath       Thai grand-opening flower stand (rival shop)
    SM_ChickenRiceStall    the neighbour's cart: glass case with hanging chickens, chopping board, soup pot
                           (Stall_Food shown while open, Stall_Tarp while closed)
    SM_RollShutter         roll-down shutter for a 4.6 m shophouse front, origin at the top (scale Y to roll)
Shophouse styles added: SM_Shop_ChickenRiceHome (no built-in cooking counter), SM_Shop_Rival, SM_Shop_Branch2.
"""

PAL.update({
    "konjac":       ((0.62, 0.36, 0.14), 0.1, 0, 0.75, 0),
    "jelly_green":  ((0.30, 0.78, 0.38), 0.1, 0, 0.75, 0),
    "pudding":      ((0.98, 0.80, 0.33), 0.35, 0, 1, 0),
    "caramel":      ((0.55, 0.27, 0.07), 0.2, 0, 1, 0),
    "cat_fur":      ((0.92, 0.55, 0.22), 0.85, 0, 1, 0),
    "cat_white":    ((0.96, 0.94, 0.9), 0.85, 0, 1, 0),
    "cat_eye":      ((0.75, 0.8, 0.2), 0.1, 0, 1, 0),
    "pink":         ((0.95, 0.6, 0.65), 0.6, 0, 1, 0),
    "lantern_red":  ((0.9, 0.08, 0.06), 0.5, 0, 1, 1.5),
    "jay_yellow":   ((1.0, 0.85, 0.1), 0.6, 0, 1, 0),
    "mother_blue":  ((0.45, 0.68, 0.95), 0.6, 0, 1, 0),
    "facade_lilac": ((0.80, 0.72, 0.92), 0.8, 0, 1, 0),
    "sign_purple":  ((0.42, 0.18, 0.62), 0.4, 0, 1, 0),
    "chicken":      ((0.97, 0.86, 0.58), 0.45, 0, 1, 0),
    "tarp_blue":    ((0.1, 0.35, 0.75), 0.7, 0, 1, 0),
    "rice":         ((0.97, 0.96, 0.9), 0.8, 0, 1, 0),
    "chopping":     ((0.62, 0.45, 0.28), 0.8, 0, 1, 0),
    "balloon_pink": ((1.0, 0.45, 0.7), 0.3, 0, 1, 0),
    "balloon_blue": ((0.35, 0.6, 1.0), 0.3, 0, 1, 0),
    "balloon_gold": ((1.0, 0.8, 0.25), 0.3, 0.6, 1, 0),
    "rose":         ((0.85, 0.08, 0.2), 0.7, 0, 1, 0),
})


# ----------------------------------------------------------------------------- cup toppings
def add_cup_toppings(a):
    """Konjac cubes, pandan jelly cubes and a pudding band in Cup_Modular. They hug the cup wall (inside radius
    0.033 + 0.075 z) so they show through the plastic in front of the (opaque) drink, like the pearls do."""
    rnd = random.Random(17)
    for g, m, n, z0, z1 in (("Cup_Konjac", "konjac", 14, 0.074, 0.112), ("Cup_Jelly", "jelly_green", 14, 0.078, 0.118)):
        for i in range(n):
            z = rnd.uniform(z0, z1)
            ang = i / n * 6.283 + rnd.uniform(-0.15, 0.15)
            r = 0.033 + 0.075 * z - 0.0075
            a.box((0.012, 0.012, 0.012), (math.cos(ang) * r, math.sin(ang) * r, z), m,
                  rot=(rnd.uniform(0, 60), rnd.uniform(0, 60), rnd.uniform(0, 90)), g=g)
    # pudding: a soft yellow band with caramel on top, just inside the cup wall
    a.cyl(0.0383, 0.024, (0, 0, 0.083), "pudding", r2=0.0398, seg=20, g="Cup_Pudding")
    a.cyl(0.0399, 0.004, (0, 0, 0.097), "caramel", r2=0.0402, seg=20, g="Cup_Pudding")


# ----------------------------------------------------------------------------- topping station
def build_topping_bar():
    a = Asset("SM_ToppingBar")
    a._g("Bar_Body")
    a.name_default = "Bar_Body"
    a.bmm(-0.38, 0.38, -0.25, 0.25, 0.78, 0.82, "steel", bevel=0.005)
    a.bmm(-0.36, 0.36, -0.23, 0.23, 0.25, 0.27, "steel")
    for x in (-0.34, 0.34):
        for y in (-0.21, 0.21):
            a.cyl(0.015, 0.8, (x, y, 0.4), "steel_dark", seg=8)
    a.bmm(-0.36, 0.36, 0.21, 0.24, 0.82, 1.3, "sign_pink")                                # back board
    a.text("ท็อปปิ้ง", 0.075, (0, 0.205, 1.2), "white", extrude=0.002, res=3)
    a.text("บุก  ·  วุ้นใบเตย  ·  พุดดิ้ง", 0.038, (0, 0.205, 1.1), "sign_yellow", extrude=0.002, res=3)
    rnd = random.Random(23)
    for x, g, m, lab in ((-0.235, "Tub_Konjac", "konjac", "บุก"), (0.0, "Tub_Jelly", "jelly_green", "วุ้น"),
                         (0.235, "Tub_Pudding", "pudding", "พุดดิ้ง")):
        a.bmm(x - 0.1, x + 0.1, -0.13, 0.11, 0.82, 0.93, "steel", g=g)                   # tub
        a.bmm(x - 0.09, x + 0.09, -0.12, 0.1, 0.9, 0.925, m if m != "pudding" else "pudding", g=g)
        if m == "pudding":
            for k in range(4):
                a.cyl(0.035, 0.03, (x - 0.045 + (k % 2) * 0.09, -0.06 + (k // 2) * 0.1, 0.94), "pudding", r2=0.028, seg=12, g=g)
                a.cyl(0.029, 0.006, (x - 0.045 + (k % 2) * 0.09, -0.06 + (k // 2) * 0.1, 0.957), "caramel", seg=12, g=g)
        else:
            for k in range(12):
                a.box((0.022, 0.022, 0.02), (x + rnd.uniform(-0.07, 0.07), rnd.uniform(-0.1, 0.08), 0.935),
                      m, rot=(rnd.uniform(0, 40), rnd.uniform(0, 40), rnd.uniform(0, 90)), g=g)
        a.tube((x + 0.05, 0.0, 0.93), (x + 0.08, 0.08, 1.05), 0.006, "steel_dark", g=g)      # ladle
        a.bmm(x - 0.09, x + 0.09, -0.142, -0.13, 0.84, 0.91, "white", g=g)                # label
        a.text(lab, 0.035, (x, -0.143, 0.875), "wood_dark", extrude=0.001, g=g, res=3)
    return a.build()


# ----------------------------------------------------------------------------- delivery pick-up rack
def build_pickup_rack():
    a = Asset("SM_PickupRack")
    a.bmm(-0.17, 0.17, -0.11, 0.11, 0.0, 0.015, "steel")
    for x in (-0.105, 0.0, 0.105):
        a.cyl(0.05, 0.012, (x, 0.0, 0.03), "steel", seg=16)
        a.cyl(0.044, 0.014, (x, 0.0, 0.03), "steel_dark", seg=16)
    for x in (-0.16, 0.16):
        a.bmm(x - 0.008, x + 0.008, 0.09, 0.105, 0.015, 0.3, "steel_dark")
    a.bmm(-0.17, 0.17, 0.095, 0.105, 0.17, 0.31, "green_plastic", bevel=0.005)             # sign, readable from the street
    a.text("เดลิเวอรี่", 0.045, (0, 0.094, 0.26), "white", extrude=0.002, rot=(90, 0, 0), res=3)
    a.text("PICK-UP", 0.032, (0, 0.094, 0.205), "sign_yellow", extrude=0.002, rot=(90, 0, 0), res=3)
    return a.build()


# ----------------------------------------------------------------------------- stray cat
def build_cat():
    """A street cat in separate parts: Cat_Body, Cat_Head (pivot at the neck), Cat_Tail (pivot at its base),
    Cat_Leg_FL/FR/BL/BR (pivot at the hip / shoulder). Faces -Y, ~0.32 m to the ears, origin on the floor.
    M_cat_fur is tinted per cat in Unity (orange, black, grey tabby...)."""
    a = Asset("SM_Cat")
    g = "Cat_Body"
    a.sphere(0.1, (0, 0.0, 0.19), "cat_fur", scale=(0.88, 2.0, 0.85), seg=16, rings=10, g=g)
    a.sphere(0.1, (0, -0.02, 0.17), "cat_white", scale=(0.72, 1.6, 0.62), seg=14, rings=8, g=g)      # belly
    a.sphere(0.07, (0, -0.12, 0.215), "cat_fur", scale=(1.0, 1.2, 1.0), seg=12, rings=8, g=g)        # shoulders
    a.sphere(0.075, (0, 0.13, 0.2), "cat_fur", scale=(1.05, 1.1, 0.95), seg=12, rings=8, g=g)        # haunches
    g = "Cat_Head"
    a.sphere(0.07, (0, -0.25, 0.285), "cat_fur", scale=(1.1, 0.95, 0.9), seg=16, rings=10, g=g)
    a.sphere(0.052, (0, -0.2, 0.25), "cat_fur", scale=(1.0, 1.15, 1.05), seg=12, rings=8, g=g)        # neck: no gap when the head turns
    a.sphere(0.04, (0, -0.225, 0.215), "cat_white", scale=(1.0, 0.9, 1.1), seg=10, rings=6, g=g)      # white chest
    a.sphere(0.034, (0, -0.315, 0.268), "cat_white", scale=(1.3, 0.85, 0.8), seg=12, rings=8, g=g)  # muzzle
    a.sphere(0.008, (0, -0.344, 0.282), "pink", seg=8, rings=5, g=g)                                 # nose
    for sx in (-1, 1):
        a.sphere(0.0135, (sx * 0.03, -0.31, 0.3), "cat_eye", scale=(1, 0.6, 1), seg=10, rings=6, g=g)
        a.sphere(0.006, (sx * 0.03, -0.318, 0.3), "black", scale=(0.6, 0.6, 1.4), seg=8, rings=5, g=g)
        a.cyl(0.03, 0.055, (sx * 0.045, -0.245, 0.35), "cat_fur", r2=0.002, rot=(0, sx * 18, 0), seg=4, g=g)   # ears
        a.cyl(0.018, 0.035, (sx * 0.045, -0.252, 0.345), "pink", r2=0.001, rot=(0, sx * 18, 0), seg=4, g=g)
        for k in range(2):                                                                            # whiskers
            a.tube((sx * 0.02, -0.33, 0.265 + k * 0.008), (sx * 0.085, -0.34, 0.258 + k * 0.016), 0.0012, "white", seg=3, g=g)
    a.origin(g, (0, -0.19, 0.25))
    g = "Cat_Tail"
    pts = [(0, 0.2, 0.22), (0, 0.27, 0.25), (0, 0.32, 0.31), (0, 0.345, 0.38), (0, 0.34, 0.44), (0.015, 0.32, 0.48)]
    a.polyline(pts, 0.016, "cat_fur", seg=8, g=g)
    a.sphere(0.017, pts[-1], "cat_fur", seg=8, rings=5, g=g)
    a.origin(g, (0, 0.2, 0.22))
    for nm, x, y, top in (("Cat_Leg_FL", 0.045, -0.12, 0.17), ("Cat_Leg_FR", -0.045, -0.12, 0.17),
                          ("Cat_Leg_BL", 0.05, 0.13, 0.18), ("Cat_Leg_BR", -0.05, 0.13, 0.18)):
        a.tube((x, y, top), (x, y - 0.005, 0.025), 0.021, "cat_fur", seg=8, g=nm)
        a.sphere(0.024, (x, y - 0.012, 0.02), "cat_white", scale=(1, 1.3, 0.75), seg=10, rings=6, g=nm)   # paw
        a.origin(nm, (x, y, top))
    return a.build()


# ----------------------------------------------------------------------------- festival decor (4.4 m strings)
def _sag(t, depth):
    return -depth * 4 * t * (1 - t)


def build_red_lanterns():
    a = Asset("SM_RedLanterns")
    pts = [(-2.2 + i / 20 * 4.4, 0, _sag(i / 20, 0.15)) for i in range(21)]
    a.polyline(pts, 0.006, "black", seg=5)
    for k in range(5):
        t = (k + 0.5) / 5
        x, z = -2.2 + t * 4.4, _sag(t, 0.15)
        a.tube((x, 0, z), (x, 0, z - 0.12), 0.005, "gold", seg=4)
        zc = z - 0.33
        a.sphere(0.17, (x, 0, zc), "lantern_red", scale=(1, 1, 0.82), seg=16, rings=10)
        for dz in (0.13, -0.13):
            a.cyl(0.075, 0.035, (x, 0, zc + dz), "gold", seg=12)
        for j in range(8):                                                # ribs
            ang = j / 8 * 6.283
            a.box((0.006, 0.006, 0.26), (x + math.cos(ang) * 0.168, math.sin(ang) * 0.168, zc), "gold", rot=(0, 0, math.degrees(ang)))
        a.tube((x, 0, zc - 0.15), (x, 0, zc - 0.32), 0.012, "lantern_red", seg=6)   # tassel
        a.cyl(0.028, 0.08, (x, 0, zc - 0.36), "lantern_red", r2=0.008, seg=8)
    return a.build()


def build_jay_flags():
    a = Asset("SM_JayFlags")
    pts = [(-2.2 + i / 20 * 4.4, 0, _sag(i / 20, 0.16)) for i in range(21)]
    a.polyline(pts, 0.006, "black", seg=5)
    n = 9
    for i in range(n):
        t = (i + 0.5) / n
        x, z = -2.2 + t * 4.4, _sag(t, 0.16)
        a.prism([(x - 0.2, z), (x + 0.2, z), (x, z - 0.42)], -0.004, 0.004, "jay_yellow")
        a.text("เจ", 0.16, (x, -0.006, z - 0.13), "red", extrude=0.002, res=3)
        a.text("เจ", 0.16, (x, 0.006, z - 0.13), "red", extrude=0.002, rot=(90, 0, 180), res=3)
    return a.build()


def build_mother_bunting():
    a = Asset("SM_MotherBunting")
    n = 15
    pts = [(-2.2 + i / 20 * 4.4, 0, _sag(i / 20, 0.16)) for i in range(21)]
    a.polyline(pts, 0.006, "black", seg=5)
    for i in range(n):
        t = (i + 0.5) / n
        x, z = -2.2 + t * 4.4, _sag(t, 0.16)
        a.prism([(x - 0.12, z), (x + 0.12, z), (x, z - 0.24)], -0.004, 0.004, "mother_blue" if i % 2 == 0 else "white")
    # jasmine garland (พวงมาลัย) hanging in the middle: a ring of jasmine buds, a rose and marigold tassels
    zc = _sag(0.5, 0.16) - 0.42
    for k in range(28):
        ang = k / 28 * 6.283
        a.sphere(0.022, (math.cos(ang) * 0.13, -0.01, zc + math.sin(ang) * 0.16), "jasmine", seg=8, rings=5)
    a.sphere(0.04, (0, -0.02, zc - 0.16), "rose", seg=10, rings=6)
    for dx in (-0.03, 0.03):
        a.tube((dx, -0.01, zc - 0.19), (dx * 1.5, -0.01, zc - 0.34), 0.012, "marigold", seg=6)
    a.tube((0, 0, _sag(0.5, 0.16)), (0, -0.01, zc + 0.16), 0.004, "jasmine", seg=4)
    a.text("รักแม่", 0.07, (0, -0.02, zc), "mother_blue", extrude=0.004, res=3)
    return a.build()


def build_opening_wreath():
    """Grand-opening flower stand (ซุ้มดอกไม้แสดงความยินดี): tripod, a big ring of flowers, ribbons."""
    a = Asset("SM_OpeningWreath")
    for ang in (90, 210, 330):
        x, y = math.cos(R(ang)) * 0.35, math.sin(R(ang)) * 0.35
        a.tube((x, y, 0), (0, 0.02, 1.15), 0.012, "gold", seg=5)
    rnd = random.Random(31)
    cols = ["balloon_pink", "white", "sign_yellow", "rose", "jasmine"]
    for k in range(40):
        ang = k / 40 * 6.283
        a.sphere(0.06, (math.cos(ang) * 0.38, -0.02, 1.45 + math.sin(ang) * 0.38), rnd.choice(cols), seg=8, rings=5)
    a.cyl(0.3, 0.02, (0, -0.03, 1.45), "white", rot=(90, 0, 0), seg=20)
    a.text("ยินดีด้วย", 0.09, (0, -0.045, 1.47), "rose", extrude=0.003, res=3)
    a.text("เปิดร้านใหม่", 0.055, (0, -0.045, 1.37), "wood_dark", extrude=0.002, res=3)
    for sx in (-1, 1):
        a.box((0.07, 0.006, 0.7), (sx * 0.16, -0.05, 0.95), "red", rot=(0, sx * 8, 0))
    return a.build()


# ----------------------------------------------------------------------------- the neighbour's chicken-rice stall
def build_chicken_rice_stall():
    """Front-of-shop cooking counter of the chicken-rice shop next door (ข้าวมันไก่): steel counter, glass case with
    boiled chickens hanging on hooks, a chopping board with a cleaver, the soup pot. Stall_Food = what you see while
    it is open; Stall_Tarp = the blue tarp tied over it after closing. Front faces -Y (the street)."""
    a = Asset("SM_ChickenRiceStall")
    a._g("Stall_Body")
    a.name_default = "Stall_Body"
    a.bmm(-0.7, 0.7, -0.35, 0.35, 0.0, 0.9, "steel")
    a.bmm(-0.72, 0.72, -0.37, 0.37, 0.88, 0.92, "steel_dark")
    a.bmm(-0.7, 0.2, -0.33, 0.33, 0.92, 1.55, "glass")                                   # glass case (left)
    for x in (-0.7, 0.2):
        for y in (-0.33, 0.33):
            a.bmm(x - 0.012, x + 0.012, y - 0.012, y + 0.012, 0.92, 1.55, "red")
    a.bmm(-0.72, 0.22, -0.35, 0.35, 1.55, 1.59, "red")
    a.bmm(-0.7, 0.2, -0.345, -0.335, 1.42, 1.55, "red")
    a.text("ข้าวมันไก่", 0.085, (-0.25, -0.347, 1.485), "white", extrude=0.002, res=3)
    a.bmm(-0.62, 0.12, -0.36, -0.35, 0.3, 0.65, "sign_yellow")
    a.text("ไก่ต้ม · ไก่ทอด", 0.06, (-0.25, -0.362, 0.475), "red", extrude=0.002, res=3)
    g = "Stall_Food"
    a.tube((-0.65, 0.0, 1.45), (0.15, 0.0, 1.45), 0.008, "steel", seg=6, g=g)            # rail with hooks
    for x in (-0.5, -0.25, 0.0):
        a.tube((x, 0, 1.45), (x, 0, 1.36), 0.004, "steel", seg=4, g=g)
        a.sphere(0.075, (x, 0, 1.25), "chicken", scale=(0.9, 0.75, 1.25), seg=12, rings=8, g=g)
        for sx in (-1, 1):
            a.tube((x + sx * 0.03, 0, 1.33), (x + sx * 0.05, 0, 1.4), 0.012, "chicken", seg=6, g=g)
    a.cyl(0.17, 0.05, (0.45, 0.13, 0.945), "chopping", seg=20, g=g)                     # chopping board, back edge
    a.sphere(0.06, (0.42, 0.12, 0.99), "chicken", scale=(1.3, 0.8, 0.5), seg=10, rings=6, g=g)
    a.bmm(0.5, 0.62, 0.16, 0.18, 0.97, 1.05, "steel", g=g)                                # cleaver
    a.bmm(0.6, 0.66, 0.168, 0.176, 1.0, 1.02, "wood_dark", g=g)
    a.cyl(0.14, 0.24, (-0.35, 0.18, 1.04), "steel", seg=18, g=g)                          # soup pot (inside the case)
    a.cyl(0.13, 0.01, (-0.35, 0.18, 1.15), "boil_water", seg=18, g=g)
    a.cyl(0.12, 0.2, (0.55, -0.2, 1.02), "white", seg=16, g=g)                            # rice warmer, front
    a.cyl(0.125, 0.03, (0.55, -0.2, 1.13), "red", seg=16, g=g)
    for k in range(4):                                                                     # plates
        a.cyl(0.09, 0.008, (0.3 + k * 0.004, -0.22, 0.925 + k * 0.009), "white", seg=14, g=g)
    g = "Stall_Tarp"
    a.bmm(-0.76, 0.76, -0.41, 0.41, 0.6, 1.66, "tarp_blue", g=g)
    a.prism([(-0.76, 1.66), (0.76, 1.66), (0.0, 1.74)], -0.41, 0.41, "tarp_blue", g=g)
    for x in (-0.5, 0.5):
        a.bmm(x - 0.01, x + 0.01, -0.42, 0.42, 1.0, 1.02, "yellow", g=g)                  # ropes
    return a.build()


def build_roll_shutter():
    """Corrugated roll-down shutter for a 4.6 m shophouse front, with its roller box. The origin sits at the top
    (z = 3.05) so Unity can roll it up by scaling Shutter_Door along Y."""
    a = Asset("SM_RollShutter")
    sx, top = 4.6 / 2 - 0.42, 3.05
    g = "Shutter_Door"
    a.bmm(-sx, sx, 0.08, 0.12, 0, top, "shutter", g=g)
    z = 0.1
    while z < top - 0.05:
        a.bmm(-sx, sx, 0.05, 0.08, z, z + 0.03, "shutter", g=g)
        z += 0.1
    a.bmm(-sx, sx, 0.04, 0.13, 0, 0.06, "steel_dark", g=g)                               # bottom bar
    a.box((0.12, 0.05, 0.08), (0, 0.03, 0.15), "gold", g=g)                                # padlock
    a.origin(g, (0, 0.1, top))
    a.bmm(-sx - 0.02, sx + 0.02, 0.05, 0.3, top, top + 0.22, "shutter", g="Shutter_Box")
    return a.build()


# ----------------------------------------------------------------------------- shophouse styles for the soi stories
SHOP_STYLES["SM_Shop_ChickenRiceHome"] = ("facade_yellow", dict(
    floors=2, front="open", sign=("ข้าวมันไก่", "CHICKEN RICE", "sign_red", "white"),
    awning=("awning_red", "sign_yellow"), extras={"tables", "lights"}))
SHOP_STYLES["SM_Shop_Rival"] = ("facade_lilac", dict(
    floors=3, front="open", sign=("ชาไข่มุกเจ้าใหม่", "BOBA BOSS  ·  GRAND OPENING", "sign_purple", "white"),
    awning=("sign_purple", "white"), extras={"kiosk", "balloons", "lights", "laundry"}))
SHOP_STYLES["SM_Shop_Branch2"] = ("facade_mint", dict(
    floors=3, front="open", sign=("บ้านชานมไข่มุก สาขา 2", "BAAN BOBA  ·  BRANCH 2", "sign_pink", "white"),
    awning=("awning_red", "awning_white"), extras={"kiosk", "lights", "plants", "tank"}))


def add_kiosk(a, rnd, accent):
    """Counter across the open front with cups on it and a lit menu on the back wall (a working drink shop)."""
    a.bmm(-1.5, 1.5, 0.75, 1.3, 0.0, 1.0, "white")
    a.bmm(-1.55, 1.55, 0.72, 1.33, 1.0, 1.04, "marble")
    a.bmm(-1.5, 1.5, 0.74, 0.75, 0.35, 0.7, accent)
    cols = ["thai_tea", "matcha", "taro", "pink_milk", "cocoa", "milk_tea"]
    for k in range(8):
        x = -1.2 + k * 0.3
        a.cyl(0.033, 0.16, (x, 0.95, 1.12), "plastic_clear", r2=0.045, seg=10)
        a.cyl(0.031, 0.12, (x, 0.95, 1.1), rnd.choice(cols), r2=0.04, seg=10)
    a.bmm(-1.2, 1.2, 6.7, 6.78, 1.6, 2.4, "light_tube")
    a.bmm(-1.15, 1.15, 6.68, 6.7, 1.65, 2.35, accent)
    for k in range(3):
        a.cyl(0.12, 0.3, (-0.6 + k * 0.6, 6.5, 1.05), "steel", seg=12)                 # urns on the back counter
    a.bmm(-1.4, 1.4, 6.2, 6.8, 0, 0.9, "steel")


def add_balloons(a, rnd, hw):
    for s in (-1, 1):
        x = s * (hw - 0.2)
        for k in range(7):
            col = ["balloon_pink", "balloon_blue", "balloon_gold", "white"][k % 4]
            a.sphere(0.13, (x + rnd.uniform(-0.15, 0.15), -0.25 + rnd.uniform(-0.1, 0.1), 2.6 + k * 0.12 + rnd.uniform(-0.05, 0.05)),
                     col, scale=(1, 1, 1.15), seg=10, rings=7)
        a.tube((x, -0.25, 1.2), (x, -0.25, 2.5), 0.004, "white", seg=4)


# ----------------------------------------------------------------------------- story: the very first cart
def build_boba_cart():
    """The cart the shop started from at the mouth of the soi (story mode memento): pink cart, a tea urn,
    a pearl pot, cups and the hand-painted sign. Front faces -Y."""
    a = Asset("SM_BobaCart")
    a.bmm(-0.7, 0.7, -0.35, 0.35, 0.3, 0.95, "sign_pink")
    a.bmm(-0.72, 0.72, -0.37, 0.37, 0.93, 0.97, "white")
    a.bmm(-0.6, -0.59, -0.36, -0.355, 0.4, 0.85, "white")
    a.bmm(0.59, 0.6, -0.36, -0.355, 0.4, 0.85, "white")
    a.text("ชานมไข่มุก", 0.12, (0, -0.352, 0.7), "white", extrude=0.003, res=3)
    a.text("แก้วละ 25 บาท", 0.06, (0, -0.352, 0.52), "sign_yellow", extrude=0.002, res=3)
    a.cyl(0.12, 0.32, (-0.42, 0.05, 1.13), "steel", seg=16)                                 # tea urn
    a.bmm(-0.45, -0.39, -0.1, -0.06, 1.0, 1.04, "black")
    a.cyl(0.13, 0.16, (0.0, 0.1, 1.05), "steel", seg=16)                                     # pearl pot
    a.cyl(0.125, 0.01, (0.0, 0.1, 1.13), "pearl", seg=16)
    for k in range(6):                                                                       # cup stack + cups
        a.cyl(0.033, 0.16, (0.45, 0.15, 1.05 + k * 0.018), "plastic_clear", r2=0.045, seg=10)
    for k, c in enumerate(["thai_tea", "milk_tea", "thai_tea"]):
        x = 0.25 + k * 0.12
        a.cyl(0.033, 0.16, (x, -0.2, 1.05), "plastic_clear", r2=0.045, seg=10)
        a.cyl(0.031, 0.12, (x, -0.2, 1.03), c, r2=0.04, seg=10)
    a.tube((-0.66, 0.3, 0.95), (-0.66, 0.3, 2.0), 0.02, "steel_dark")                       # umbrella
    a.cyl(0.85, 0.25, (-0.66, 0.3, 2.05), "awning_red", r2=0.05, seg=12, stripes="awning_white")
    for s_ in (-1, 1):
        a.cyl(0.3, 0.05, (0.35, s_ * 0.42, 0.3), "rubber", rot=(90, 0, 0), seg=18)
        a.cyl(0.18, 0.06, (0.35, s_ * 0.42, 0.3), "chrome", rot=(90, 0, 0), seg=10)
        a.tube((-0.7, s_ * 0.3, 0.5), (-1.15, s_ * 0.3, 0.75), 0.02, "steel")
    a.tube((-1.15, -0.3, 0.75), (-1.15, 0.3, 0.75), 0.022, "black")
    a.tube((-0.55, 0, 0.3), (-0.55, 0, 0.0), 0.03, "steel_dark")
    return a.build()


EXTRA_BUILDERS = [
    build_topping_bar, build_pickup_rack, build_cat, build_red_lanterns, build_jay_flags, build_mother_bunting,
    build_opening_wreath, build_chicken_rice_stall, build_roll_shutter, build_boba_cart,
]

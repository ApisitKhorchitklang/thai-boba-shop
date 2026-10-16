"""
Thai Bubble-Tea Shop asset generator (Blender 4.x, headless).

Run:
    "D:\\Bder\\blender.exe" -b --factory-startup -P BlenderSource/thai_boba_assets.py

Outputs
    Assets/Models/ThaiBobaShop/*.fbx     one FBX per asset (1 unit = 1 m, front faces Unity +Z)
    BlenderSource/ThaiBobaShop.blend      the showcase scene
    BlenderSource/renders/*.png           preview renders

Conventions (Blender space): Z up, asset front faces -Y, origin on the floor.
Low-poly, flat-colour materials (mobile friendly). Parts that gameplay code toggles
(cup liquids, pearls, ice, seal, straw, fan blades) are exported as separate child objects.
"""
import bpy, bmesh, math, random, os
from mathutils import Matrix, Vector, Euler

ROOT = r"D:\Project-Start\A4"
FBX_DIR = os.path.join(ROOT, "Assets", "Models", "ThaiBobaShop")
SRC_DIR = os.path.join(ROOT, "BlenderSource")
REN_DIR = os.path.join(SRC_DIR, "renders")
# OFL fonts shipped in the project (Assets/Fonts/Thai) — safe to use in a commercial game
FONT_TH_BOLD = os.path.join(ROOT, "Assets", "Fonts", "Thai", "Kanit-Bold.ttf")
FONT_TH = os.path.join(ROOT, "Assets", "Fonts", "Thai", "Sarabun-SemiBold.ttf")
for d in (FBX_DIR, SRC_DIR, REN_DIR):
    os.makedirs(d, exist_ok=True)

R = math.radians
random.seed(7)
bpy.ops.wm.read_factory_settings(use_empty=True)
SCENE = bpy.context.scene

# ----------------------------------------------------------------------------- materials
PAL = {
    # name: (rgb, roughness, metallic, alpha, emission)
    "plaster":      ((0.93, 0.89, 0.80), 0.85, 0, 1, 0),
    "facade_mint":  ((0.52, 0.80, 0.72), 0.8, 0, 1, 0),
    "facade_peach": ((0.96, 0.70, 0.55), 0.8, 0, 1, 0),
    "concrete":     ((0.58, 0.58, 0.56), 0.9, 0, 1, 0),
    "tile_floor":   ((0.86, 0.83, 0.76), 0.4, 0, 1, 0),
    "tile_wall":    ((0.80, 0.92, 0.95), 0.3, 0, 1, 0),
    "wood":         ((0.55, 0.36, 0.20), 0.7, 0, 1, 0),
    "wood_dark":    ((0.30, 0.18, 0.10), 0.7, 0, 1, 0),
    "steel":        ((0.80, 0.80, 0.82), 0.3, 1, 1, 0),
    "steel_dark":   ((0.22, 0.22, 0.24), 0.5, 0.8, 1, 0),
    "shutter":      ((0.62, 0.64, 0.67), 0.5, 0.6, 1, 0),
    "glass":        ((0.70, 0.86, 0.92), 0.05, 0, 0.30, 0),
    "plastic_clear":((0.95, 0.97, 1.00), 0.10, 0, 0.12, 0),
    "ice":          ((0.85, 0.95, 1.00), 0.05, 0, 0.55, 0),
    "white":        ((0.95, 0.95, 0.93), 0.5, 0, 1, 0),
    "black":        ((0.04, 0.04, 0.05), 0.6, 0, 1, 0),
    "rubber":       ((0.05, 0.05, 0.05), 0.9, 0, 1, 0),
    "red":          ((0.80, 0.07, 0.07), 0.5, 0, 1, 0),
    "red_plastic":  ((0.88, 0.12, 0.10), 0.45, 0, 1, 0),
    "blue_plastic": ((0.12, 0.35, 0.85), 0.45, 0, 1, 0),
    "green_plastic":((0.10, 0.55, 0.25), 0.5, 0, 1, 0),
    "yellow":       ((0.98, 0.80, 0.10), 0.5, 0, 1, 0),
    "gold":         ((1.00, 0.72, 0.20), 0.25, 1, 1, 0),
    "awning_red":   ((0.85, 0.15, 0.20), 0.8, 0, 1, 0),
    "awning_white": ((0.97, 0.95, 0.90), 0.8, 0, 1, 0),
    "sign_pink":    ((0.93, 0.30, 0.55), 0.5, 0, 1, 0),
    "sign_yellow":  ((1.00, 0.88, 0.35), 0.4, 0, 1, 0.6),
    "sign_blue":    ((0.05, 0.30, 0.70), 0.4, 0, 1, 0),
    "chalkboard":   ((0.10, 0.14, 0.12), 0.9, 0, 1, 0),
    "light_tube":   ((1.00, 0.98, 0.92), 0.3, 0, 1, 6),
    "neon_pink":    ((1.00, 0.25, 0.65), 0.3, 0, 1, 8),
    "neon_cyan":    ((0.20, 0.95, 1.00), 0.3, 0, 1, 8),
    "tutorial_glow":((1.00, 0.80, 0.20), 0.3, 0, 1, 3),
    "cardboard":    ((0.72, 0.53, 0.32), 0.9, 0, 1, 0),
    "cheese_foam":  ((0.99, 0.93, 0.76), 0.55, 0, 1, 0),
    "boil_water":   ((0.62, 0.42, 0.24), 0.05, 0, 0.75, 0),
    "frappe":       ((0.97, 0.96, 0.95), 0.6, 0, 1, 0),
    "marsh_pink":   ((1.00, 0.78, 0.86), 0.7, 0, 1, 0),
    "lantern":      ((1.00, 0.62, 0.22), 0.6, 0, 1, 4),
    "tutorial_rim": ((1.00, 0.97, 0.88), 0.3, 0, 1, 2),
    "screen":       ((0.20, 0.70, 0.95), 0.2, 0, 1, 2),
    # drinks
    "thai_tea":     ((0.93, 0.45, 0.10), 0.2, 0, 1, 0),
    "matcha":       ((0.42, 0.62, 0.22), 0.2, 0, 1, 0),
    "milk_tea":     ((0.80, 0.62, 0.45), 0.2, 0, 1, 0),
    "taro":         ((0.62, 0.48, 0.80), 0.2, 0, 1, 0),
    "pink_milk":    ((0.98, 0.55, 0.70), 0.2, 0, 1, 0),
    "cocoa":        ((0.33, 0.18, 0.10), 0.2, 0, 1, 0),
    "red_lime":     ((0.85, 0.08, 0.15), 0.1, 0, 1, 0),
    "butterfly":    ((0.33, 0.36, 0.92), 0.1, 0, 1, 0),
    "pearl":        ((0.04, 0.025, 0.02), 0.15, 0, 1, 0),
    "seal_film":    ((1.00, 0.85, 0.92), 0.3, 0, 0.6, 0),
    "straw":        ((0.95, 0.35, 0.55), 0.4, 0, 1, 0),
    # street
    "asphalt":      ((0.17, 0.17, 0.18), 0.95, 0, 1, 0),
    "paver_a":      ((0.72, 0.66, 0.60), 0.9, 0, 1, 0),
    "paver_b":      ((0.62, 0.55, 0.50), 0.9, 0, 1, 0),
    "curb_red":     ((0.80, 0.10, 0.10), 0.8, 0, 1, 0),
    "curb_white":   ((0.95, 0.95, 0.95), 0.8, 0, 1, 0),
    "road_line":    ((0.97, 0.85, 0.25), 0.8, 0, 1, 0),
    "leaf":         ((0.20, 0.50, 0.16), 0.7, 0, 1, 0),
    "leaf_light":   ((0.40, 0.65, 0.20), 0.7, 0, 1, 0),
    "trunk":        ((0.45, 0.35, 0.25), 0.9, 0, 1, 0),
    "terracotta":   ((0.72, 0.36, 0.20), 0.8, 0, 1, 0),
    "tuk_blue":     ((0.08, 0.30, 0.75), 0.35, 0.2, 1, 0),
    "tuk_yellow":   ((1.00, 0.78, 0.05), 0.4, 0, 1, 0),
    "seat_red":     ((0.70, 0.05, 0.08), 0.35, 0, 1, 0),
    "chrome":       ((0.90, 0.90, 0.92), 0.12, 1, 1, 0),
    "headlight":    ((1.00, 0.97, 0.80), 0.1, 0, 1, 3),
    "marigold":     ((1.00, 0.55, 0.05), 0.7, 0, 1, 0),
    "jasmine":      ((0.98, 0.98, 0.92), 0.7, 0, 1, 0),
    "red_drink":    ((0.85, 0.05, 0.12), 0.1, 0, 1, 0),
    "marble":       ((0.92, 0.92, 0.90), 0.3, 0, 1, 0),
    "ceramic_brown":((0.40, 0.18, 0.08), 0.2, 0, 1, 0),
    "fan_blue":     ((0.30, 0.65, 0.95), 0.3, 0, 0.8, 0),
    "silver":       ((0.75, 0.76, 0.78), 0.25, 1, 1, 0),
    "window":       ((0.08, 0.1, 0.13), 0.08, 0.3, 1, 0),
    "facade_yellow":((0.98, 0.84, 0.52), 0.8, 0, 1, 0),
    "facade_blue":  ((0.62, 0.76, 0.9), 0.8, 0, 1, 0),
    "facade_white": ((0.93, 0.93, 0.9), 0.8, 0, 1, 0),
    "facade_green": ((0.68, 0.82, 0.58), 0.8, 0, 1, 0),
    "facade_grey":  ((0.7, 0.71, 0.73), 0.8, 0, 1, 0),
    "facade_pink":  ((0.96, 0.75, 0.8), 0.8, 0, 1, 0),
    "facade_cream": ((0.95, 0.9, 0.75), 0.8, 0, 1, 0),
    "sign_green":   ((0.08, 0.55, 0.3), 0.4, 0, 1, 0),
    "sign_red":     ((0.8, 0.1, 0.1), 0.4, 0, 1, 0),
    "neon_green":   ((0.3, 1.0, 0.45), 0.3, 0, 1, 8),
    "tunnel_dark":  ((0.015, 0.015, 0.018), 0.95, 0, 1, 0),
    "cloud":        ((0.97, 0.97, 0.98), 1.0, 0, 1, 0),
    "puddle":       ((0.1, 0.12, 0.15), 0.02, 0.0, 1, 0),
    "umbrella":     ((0.9, 0.2, 0.35), 0.5, 0, 1, 0),
    "umbrella2":    ((0.97, 0.97, 0.95), 0.5, 0, 1, 0),
    "sodium_light": ((1.0, 0.6, 0.2), 0.3, 0, 1, 5),
    "taxi_pink":    ((0.95, 0.35, 0.6), 0.3, 0.2, 1, 0),
    "taxi_green":   ((0.25, 0.65, 0.25), 0.3, 0.2, 1, 0),
    # people
    "skin":         ((0.80, 0.60, 0.45), 0.6, 0, 1, 0),
    "skin_light":   ((0.93, 0.76, 0.63), 0.6, 0, 1, 0),
    "hair":         ((0.05, 0.04, 0.04), 0.5, 0, 1, 0),
    "hair_grey":    ((0.55, 0.55, 0.55), 0.6, 0, 1, 0),
    "hair_blonde":  ((0.85, 0.70, 0.40), 0.6, 0, 1, 0),
    "navy":         ((0.08, 0.10, 0.25), 0.7, 0, 1, 0),
    "shirt_blue":   ((0.55, 0.72, 0.92), 0.7, 0, 1, 0),
    "pants_grey":   ((0.25, 0.26, 0.30), 0.7, 0, 1, 0),
    "khaki":        ((0.72, 0.62, 0.45), 0.8, 0, 1, 0),
    "floral":       ((0.62, 0.30, 0.65), 0.7, 0, 1, 0),
    "sarong":       ((0.35, 0.18, 0.12), 0.7, 0, 1, 0),
    "rider_orange": ((0.98, 0.45, 0.08), 0.6, 0, 1, 0),
    "polo_green":   ((0.15, 0.45, 0.30), 0.7, 0, 1, 0),
    "denim":        ((0.20, 0.30, 0.50), 0.8, 0, 1, 0),
    "tropical":     ((0.10, 0.60, 0.70), 0.7, 0, 1, 0),
}
DRINKS = ["ThaiTea", "Matcha", "MilkTea", "Taro", "PinkMilk", "Cocoa", "RedLime", "ButterflyPea"]   # ButterflyPea = secret recipe
DRINK_MAT = {"ThaiTea": "thai_tea", "Matcha": "matcha", "MilkTea": "milk_tea", "Taro": "taro",
             "PinkMilk": "pink_milk", "Cocoa": "cocoa", "RedLime": "red_lime", "ButterflyPea": "butterfly"}

_mats = {}


def mat(name):
    if name in _mats:
        return _mats[name]
    rgb, rough, metal, alpha, emit = PAL[name]
    m = bpy.data.materials.new("M_" + name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Base Color"].default_value = (*rgb, 1)
    b.inputs["Roughness"].default_value = rough
    b.inputs["Metallic"].default_value = metal
    m.diffuse_color = (*rgb, alpha)
    if alpha < 1:
        b.inputs["Alpha"].default_value = alpha
        for attr, val in (("surface_render_method", "BLENDED"), ("blend_method", "BLEND")):
            try:
                setattr(m, attr, val)
            except Exception:
                pass
    if emit > 0:
        b.inputs["Emission Color"].default_value = (*rgb, 1)
        b.inputs["Emission Strength"].default_value = emit
    _mats[name] = m
    return m


_fonts = {}


def font(path):
    if path not in _fonts:
        _fonts[path] = bpy.data.fonts.load(path, check_existing=True)
    return _fonts[path]


def MX(loc=(0, 0, 0), rot=(0, 0, 0), scale=(1, 1, 1)):
    return (Matrix.Translation(Vector(loc)) @ Euler([R(a) for a in rot]).to_matrix().to_4x4()
            @ Matrix.Diagonal((*scale, 1)))


# ----------------------------------------------------------------------------- builder
class Asset:
    """Accumulates primitives into one bmesh per group; each group becomes one mesh object."""

    def __init__(self, name):
        self.name = name
        self.groups = {}
        self.order = []
        self.origins = {}

    name_default = None

    def _g(self, g):
        g = g or self.name_default or self.name
        if g not in self.groups:
            bm = bmesh.new()
            bm.loops.layers.uv.verify()
            self.groups[g] = (bm, [])
            self.order.append(g)
        return g, self.groups[g][0]

    def _paint(self, g, verts, m, smooth, stripes=None, center=None):
        bm, mats = self.groups[g]

        def idx(name):
            mm = mat(name)
            if mm not in mats:
                mats.append(mm)
            return mats.index(mm)

        mi = idx(m)
        faces = {f for v in verts for f in v.link_faces}
        for f in faces:
            f.material_index = mi
            f.smooth = smooth
            if smooth and len(f.verts) > 4:          # keep cylinder caps crisp
                for e in f.edges:
                    e.smooth = False
        if stripes:
            mi2 = idx(stripes)
            sides = [f for f in faces if len(f.verts) == 4]
            n = len(sides)
            for f in sides:
                c = f.calc_center_median() - Vector(center)
                k = int((math.atan2(c.y, c.x) + math.pi) / (2 * math.pi) * n + 0.5)
                if k % 2:
                    f.material_index = mi2
        return faces

    # primitives ---------------------------------------------------------------
    def box(self, size, loc, m, rot=(0, 0, 0), bevel=0.0, g=None):
        g, bm = self._g(g)
        vs = bmesh.ops.create_cube(bm, size=1.0, matrix=MX(loc, rot, size), calc_uvs=True)["verts"]
        self._paint(g, vs, m, False)
        if bevel > 0:
            edges = list({e for v in vs for e in v.link_edges})
            bmesh.ops.bevel(bm, geom=list(vs) + edges, offset=bevel, segments=1, profile=0.5,
                            affect='EDGES', clamp_overlap=True)

    def bmm(self, x0, x1, y0, y1, z0, z1, m, g=None, bevel=0.0):
        """Box from min/max extents."""
        self.box((x1 - x0, y1 - y0, z1 - z0), ((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2), m,
                 bevel=bevel, g=g)

    def cyl(self, r, h, loc, m, rot=(0, 0, 0), r2=None, seg=16, g=None, smooth=True, stripes=None):
        g, bm = self._g(g)
        vs = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r,
                                   radius2=r if r2 is None else r2, depth=h,
                                   matrix=MX(loc, rot), calc_uvs=True)["verts"]
        self._paint(g, vs, m, smooth, stripes, loc)

    def sphere(self, r, loc, m, scale=(1, 1, 1), seg=12, rings=8, rot=(0, 0, 0), g=None, smooth=True):
        g, bm = self._g(g)
        vs = bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r,
                                       matrix=MX(loc, rot, scale), calc_uvs=True)["verts"]
        self._paint(g, vs, m, smooth)

    def tube(self, p0, p1, r, m, seg=8, g=None):
        g, bm = self._g(g)
        p0, p1 = Vector(p0), Vector(p1)
        d = p1 - p0
        q = Vector((0, 0, 1)).rotation_difference(d.normalized())
        mx = Matrix.Translation((p0 + p1) / 2) @ q.to_matrix().to_4x4()
        vs = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r,
                                   radius2=r, depth=d.length, matrix=mx, calc_uvs=True)["verts"]
        self._paint(g, vs, m, True)

    def polyline(self, pts, r, m, seg=6, g=None):
        for a, b in zip(pts, pts[1:]):
            self.tube(a, b, r, m, seg, g)

    def prism(self, pts_xz, y0, y1, m, loc=(0, 0, 0), g=None):
        """Polygon in the XZ plane extruded along Y (gable roofs, wedges)."""
        g, bm = self._g(g)
        o = Vector(loc)
        a = [bm.verts.new(o + Vector((x, y0, z))) for x, z in pts_xz]
        b = [bm.verts.new(o + Vector((x, y1, z))) for x, z in pts_xz]
        faces = [bm.faces.new(a), bm.faces.new(b[::-1])]
        n = len(pts_xz)
        for i in range(n):
            j = (i + 1) % n
            faces.append(bm.faces.new((a[i], b[i], b[j], a[j])))
        bmesh.ops.recalc_face_normals(bm, faces=faces)
        self._paint(g, a + b, m, False)

    def text(self, body, size, loc, m, rot=(90, 0, 0), extrude=0.01, fnt=FONT_TH_BOLD,
             align='CENTER', g=None):
        g, bm = self._g(g)
        cu = bpy.data.curves.new("tmp_txt", 'FONT')
        cu.body = body
        cu.font = font(fnt)
        cu.size = size
        cu.extrude = extrude
        cu.align_x = align
        cu.align_y = 'CENTER'
        ob = bpy.data.objects.new("tmp_txt", cu)
        SCENE.collection.objects.link(ob)
        dg = bpy.context.evaluated_depsgraph_get()
        me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
        mx = MX(loc, rot)
        vs = [bm.verts.new(mx @ v.co) for v in me.vertices]
        for p in me.polygons:
            try:
                bm.faces.new([vs[i] for i in p.vertices])
            except ValueError:
                pass
        bpy.data.objects.remove(ob)
        bpy.data.curves.remove(cu)
        bpy.data.meshes.remove(me)
        self._paint(g, vs, m, False)

    def origin(self, g, p):
        self.origins[g] = p

    # output -------------------------------------------------------------------
    def build(self):
        objs = []
        multi = len(self.order) > 1
        for g in self.order:
            bm, mats = self.groups[g]
            me = bpy.data.meshes.new(g)
            bm.normal_update()
            bm.to_mesh(me)
            bm.free()
            for m in mats:
                me.materials.append(m)
            ob = bpy.data.objects.new(g if multi else self.name, me)
            SCENE.collection.objects.link(ob)
            if g in self.origins:
                o = Vector(self.origins[g])
                me.transform(Matrix.Translation(-o))
                ob.location = o
            objs.append(ob)
        if not multi:
            return objs[0]
        root = bpy.data.objects.new(self.name, None)
        root.empty_display_size = 0.3
        SCENE.collection.objects.link(root)
        for ob in objs:
            ob.parent = root
        return root


# ============================================================================= ASSETS
# ---------------------------------------------------------------- shop buildings
def build_shophouse(name, facade, open_front=True, style=None):
    """Thai shophouse (ตึกแถว), 4.6 m wide, front at y=0 facing -Y, depth +Y.
    style keys:
      floors   2 or 3
      front    'boba' | 'closed' | 'open' | 'glass'
      sign     (thai, english, bg, fg) or None
      awning   (colour1, colour2) or None
      extras   set of: tank, dish, laundry, plants, blade_cross, goods, tables, steam_pot, lights, rent, gold
    """
    st = dict(floors=2, front="boba" if open_front else "closed", sign=None, awning=None, extras=set())
    if style:
        st.update(style)
    if not style and not open_front:
        st["extras"] = {"rent", "gold"}
    ex = st["extras"]
    a = Asset(name)
    W, Dp, H1, H2, P, T = 4.6, 7.0, 3.4, 3.0, 0.7, 0.2
    z1 = H1 + 0.25
    floors = st["floors"]
    top = z1 + H2 * (floors - 1)
    hw = W / 2
    rnd = random.Random(sum(map(ord, name)))
    # shell
    a.bmm(-hw, hw, 0, Dp, -0.1, 0, "tile_floor")
    for s in (-1, 1):
        a.bmm(s * hw - (T if s > 0 else 0), s * hw + (0 if s > 0 else T), 0.2, Dp, 0, top + P, "plaster")
    a.bmm(-hw + T, hw - T, Dp - T, Dp, 0, top, "plaster")
    a.bmm(-hw + T, hw - T, Dp - T - 0.01, Dp - T, 0, 1.3, "tile_wall")
    for f in range(floors - 1):
        a.bmm(-hw, hw, 0.2, Dp, H1 + f * H2, z1 + f * H2, "plaster")
    a.bmm(-hw, hw, 0, Dp, top, top + 0.15, "concrete")

    # upper floors: window + aircon on even floors, balcony with laundry on odd floors
    for f in range(floors - 1):
        zf = z1 + f * H2
        wx0, wx1, wz0, wz1 = -1.75, -0.25, zf + 0.9, zf + 2.5
        balcony = f % 2 == 1
        if balcony:
            a.bmm(-hw, hw, 0, 0.2, zf - 0.25, zf + 0.3, facade)
            a.bmm(-hw, hw, 0, 0.2, zf + 2.6, zf + H2 - 0.25, facade)
            a.bmm(-hw + 0.3, hw - 0.3, 0.6, 0.65, zf + 0.3, zf + 2.6, "glass")       # recessed glass doors
            for x in (-hw + 0.3, 0, hw - 0.3):
                a.bmm(x - 0.04, x + 0.04, 0.55, 0.7, zf + 0.3, zf + 2.6, "white")
            for s in (-1, 1):
                a.bmm(s * hw - (0.3 if s > 0 else 0), s * hw + (0 if s > 0 else 0.3), 0, 0.6, zf + 0.3, zf + 2.6, facade)
            a.bmm(-hw, hw, -0.7, 0.2, zf - 0.05, zf + 0.08, "concrete")             # balcony slab
            for i in range(17):                                                     # railing
                x = -hw + 0.1 + i * (W - 0.2) / 16
                a.bmm(x - 0.015, x + 0.015, -0.68, -0.64, zf + 0.08, zf + 1.05, "steel_dark")
            a.bmm(-hw, hw, -0.7, -0.62, zf + 1.02, zf + 1.08, "steel_dark")
            if "laundry" in ex:
                a.tube((-hw + 0.1, -0.5, zf + 2.0), (hw - 0.1, -0.5, zf + 2.0), 0.008, "steel")
                cols = ["awning_white", "blue_plastic", "yellow", "sign_pink", "green_plastic", "white"]
                x = -hw + 0.35
                while x < hw - 0.4:
                    w = rnd.uniform(0.3, 0.55)
                    a.box((w, 0.02, rnd.uniform(0.45, 0.8)), (x + w / 2, -0.5, zf + 1.65), rnd.choice(cols))
                    x += w + 0.12
            if "plants" in ex:
                for x in (-1.6, 1.5):
                    a.cyl(0.14, 0.3, (x, -0.35, zf + 0.23), "terracotta", r2=0.17, seg=10)
                    a.sphere(0.25, (x, -0.35, zf + 0.6), "leaf", scale=(1, 1, 1.2), seg=8, rings=6)
        else:
            a.bmm(-hw, hw, 0, 0.2, zf - 0.25, wz0, facade)
            a.bmm(-hw, hw, 0, 0.2, wz1, zf + H2 - 0.25, facade)
            a.bmm(-hw, wx0, 0, 0.2, wz0, wz1, facade)
            a.bmm(wx1, hw, 0, 0.2, wz0, wz1, facade)
            a.bmm(wx0, wx1, 0.08, 0.12, wz0, wz1, "glass")
            for x in (wx0, wx1):
                a.bmm(x - 0.05, x + 0.05, -0.02, 0.14, wz0, wz1, "white")
            for z in (wz0, wz1):
                a.bmm(wx0 - 0.05, wx1 + 0.05, -0.02, 0.14, z - 0.05, z + 0.05, "white")
            a.bmm(wx0 - 0.15, wx1 + 0.15, -0.18, 0.05, wz0 - 0.1, wz0 - 0.03, "white")
            for i in range(1, 10):
                x = wx0 + (wx1 - wx0) * i / 10
                a.bmm(x - 0.012, x + 0.012, -0.08, -0.05, wz0, wz1, "steel_dark")
            for z in (wz0 + 0.5, wz0 + 1.1):
                a.bmm(wx0, wx1, -0.08, -0.05, z - 0.012, z + 0.012, "steel_dark")
            a.box((0.8, 0.32, 0.55), (1.25, -0.18, zf + 1.7), "white", bevel=0.02)
            a.cyl(0.2, 0.02, (1.15, -0.35, zf + 1.7), "steel_dark", rot=(90, 0, 0), seg=20)
            for x in (0.95, 1.55):
                a.bmm(x - 0.02, x + 0.02, -0.4, 0, zf + 1.38, zf + 1.42, "steel_dark")
            a.tube((1.6, -0.1, zf + 1.9), (1.6, -0.1, zf + H2 - 0.2), 0.025, "white")
            for x in (-1.5, -1.0, -0.5):
                a.cyl(0.08, 0.14, (x, -0.1, wz0 - 0.03 + 0.07), "terracotta", r2=0.1, seg=10)
                a.sphere(0.11, (x, -0.1, wz0 + 0.17), "leaf", scale=(1, 1, 0.8), seg=8, rings=5)
    a.bmm(-hw, hw, 0, 0.25, top, top + P, facade)
    a.bmm(-hw - 0.05, hw + 0.05, -0.1, 0.25, top - 0.06, top + 0.04, "white")
    a.bmm(-hw - 0.05, hw + 0.05, -0.08, 0.25, top + P, top + P + 0.06, "white")

    # rooftop clutter
    if "tank" in ex:
        for s in (-1, 1):
            for y in (2.2, 3.2):
                a.tube((s * 0.5, y, top + 0.15), (s * 0.5, y, top + 1.0), 0.03, "steel_dark")
        a.cyl(0.55, 1.2, (0, 2.7, top + 1.0), "steel", rot=(0, 90, 0), seg=16)
    if "dish" in ex:
        a.tube((-1.4, 4.5, top + 0.15), (-1.4, 4.5, top + 0.9), 0.03, "steel_dark")
        a.cyl(0.4, 0.12, (-1.4, 4.42, top + 1.05), "white", r2=0.05, rot=(-60, 0, 0), seg=16)
        a.tube((1.6, 5.5, top + 0.15), (1.6, 5.5, top + 2.2), 0.015, "steel_dark")
        for z in (1.4, 1.8, 2.1):
            a.tube((1.2, 5.5, top + z), (2.0, 5.5, top + z), 0.01, "steel_dark")

    # ground floor frame
    for s in (-1, 1):
        a.bmm(s * hw - (0.4 if s > 0 else 0), s * hw + (0 if s > 0 else 0.4), -0.1, 0.2, 0, H1, facade)
        a.bmm(s * (hw - 0.45) - 0.03, s * (hw - 0.45) + 0.03, 0.05, 0.15, 0, H1 - 0.35, "shutter")
    a.bmm(-hw, hw, -0.1, 0.2, H1 - 0.35, H1, facade)
    a.bmm(-hw + 0.4, hw - 0.4, 0.2, 0.5, H1 - 0.62, H1 - 0.32, "shutter")

    front = st["front"]
    if st["awning"] or front == "boba":
        c1, c2 = st["awning"] or ("awning_red", "awning_white")
        N, aw = 10, W + 0.3
        for i in range(N):
            x = -aw / 2 + aw * (i + 0.5) / N
            c = c1 if i % 2 == 0 else c2
            a.box((aw / N, 1.45, 0.03), (x, -0.75, H1 - 0.3), c, rot=(19, 0, 0))
            a.box((aw / N, 0.03, 0.2), (x, -1.45, H1 - 0.62), c)
        for s in (-1, 1):
            a.tube((s * 2.1, 0, H1 - 1.0), (s * 2.1, -1.35, H1 - 0.55), 0.02, "steel_dark")

    if front == "boba":
        a.bmm(-2.0, 2.0, -0.14, 0.0, z1 - 0.1, z1 + 0.75, "sign_pink", bevel=0.02)
        a.bmm(-2.05, 2.05, -0.12, -0.02, z1 - 0.15, z1 + 0.8, "white")
        a.text("บ้านชานมไข่มุก", 0.40, (0.15, -0.15, z1 + 0.42), "white", extrude=0.012)
        a.text("BAAN BOBA  ·  BUBBLE TEA", 0.14, (0.15, -0.15, z1 + 0.06), "sign_yellow", extrude=0.008)
        a.cyl(0.13, 0.36, (-1.65, -0.17, z1 + 0.33), "neon_cyan", r2=0.17, seg=12)
        a.tube((-1.6, -0.2, z1 + 0.45), (-1.5, -0.2, z1 + 0.7), 0.025, "neon_pink")
        for y in (2.0, 4.5):
            a.bmm(-0.6, 0.6, y - 0.04, y + 0.04, H1 - 0.08, H1, "light_tube")
        return a.build()

    if front == "closed":
        sx = hw - 0.42
        a.bmm(-sx, sx, 0.08, 0.12, 0, H1 - 0.35, "shutter")
        z = 0.1
        while z < H1 - 0.4:
            a.bmm(-sx, sx, 0.05, 0.08, z, z + 0.03, "shutter")
            z += 0.1
        a.box((0.12, 0.05, 0.08), (0, 0.03, 0.15), "gold")
        if "rent" in ex:
            a.bmm(-0.8, 0.8, 0.03, 0.05, 1.2, 1.95, "white")
            a.text("ให้เช่า", 0.38, (0, 0.025, 1.52), "red", extrude=0.004)
    else:
        # open or glass storefront with a lit interior
        for y in (2.0, 4.5):
            a.bmm(-0.6, 0.6, y - 0.04, y + 0.04, H1 - 0.08, H1, "light_tube")
        if front == "glass":
            a.bmm(-hw + 0.4, hw - 0.4, 0.3, 0.34, 0, H1 - 0.62, "glass")
            for x in (-hw + 0.4, -0.6, 0.6, hw - 0.4):
                a.bmm(x - 0.04, x + 0.04, 0.28, 0.36, 0, H1 - 0.62, "steel")
            a.bmm(-hw + 0.4, hw - 0.4, 0.28, 0.36, 0.9, 0.95, "steel")
        if "goods" in ex:
            cols = ["red", "yellow", "blue_plastic", "green_plastic", "white", "sign_pink", "thai_tea"]
            for s in (-1, 1):
                x = s * (hw - 0.55)
                a.bmm(x - 0.3, x + 0.3, 1.0, 6.5, 0, 2.2, "white")
                for k in range(4):
                    z = 0.35 + k * 0.5
                    a.bmm(x - 0.33, x + 0.33, 1.0, 6.5, z, z + 0.03, "steel")
                    y = 1.15
                    while y < 6.3:
                        w = rnd.uniform(0.12, 0.3)
                        a.box((0.22, w, rnd.uniform(0.18, 0.4)), (x - s * 0.1, y + w / 2, z + 0.2), rnd.choice(cols))
                        y += w + 0.04
            a.bmm(-0.7, 0.7, 5.6, 6.2, 0, 1.0, "white")                         # cashier
        if "tables" in ex:
            for (x, y) in [(-1.0, 2.2), (0.9, 2.4), (-0.9, 4.3), (1.0, 4.6)]:
                a.cyl(0.38, 0.03, (x, y, 0.72), "steel", seg=16)
                a.cyl(0.03, 0.7, (x, y, 0.36), "steel", seg=8)
                for dx in (-0.5, 0.5):
                    a.cyl(0.15, 0.04, (x + dx, y, 0.45), "red_plastic", seg=12)
                    a.cyl(0.12, 0.44, (x + dx, y, 0.22), "red_plastic", r2=0.14, seg=10)
        if "steam_pot" in ex:
            a.bmm(-2.0, -0.6, -0.9, -0.2, 0, 0.9, "steel")                     # front cooking counter
            a.cyl(0.25, 0.35, (-1.6, -0.55, 1.07), "steel", seg=16)
            a.cyl(0.22, 0.35, (-1.0, -0.55, 1.07), "steel", seg=16)
            a.bmm(-2.0, -0.6, -0.92, -0.88, 0.9, 1.6, "glass")
        if "blade_cross" in ex:
            a.bmm(hw - 0.5, hw - 0.45, -1.0, -0.1, z1 + 0.6, z1 + 1.5, "white", bevel=0.02)
            for (dy, dz, sy, sz) in [(0, 0, 0.18, 0.6), (0, 0, 0.6, 0.18)]:
                a.box((0.08, sy, sz), (hw - 0.475, -0.55 + dy, z1 + 1.05 + dz), "neon_green")
    if "gold" in ex:
        a.bmm(-2.0, 2.0, -0.14, 0.0, z1 - 0.1, z1 + 0.75, "white", bevel=0.02)
        a.text("ร้านทองเยาวราช", 0.36, (0, -0.15, z1 + 0.35), "red", extrude=0.01)
    if st["sign"]:
        th, en, bg, fg = st["sign"]
        a.bmm(-2.05, 2.05, -0.16, -0.02, z1 - 0.12, z1 + 0.78, "white")
        a.bmm(-2.0, 2.0, -0.18, 0.0, z1 - 0.08, z1 + 0.74, bg, bevel=0.02)
        a.text(th, 0.36, (0, -0.19, z1 + 0.4), fg, extrude=0.012)
        if en:
            a.text(en, 0.13, (0, -0.19, z1 + 0.06), fg, extrude=0.008, fnt=FONT_TH)
    if "lights" in ex:
        for i in range(12):
            x = -hw + 0.2 + i * (W - 0.4) / 11
            z = H1 - 0.05 - 0.25 * math.sin(i / 11 * math.pi)
            a.sphere(0.05, (x, -0.2, z), ["neon_pink", "sign_yellow", "neon_cyan"][i % 3], seg=8, rings=5)
        a.polyline([(-hw + 0.2 + i * (W - 0.4) / 11, -0.2, H1 - 0.05 - 0.25 * math.sin(i / 11 * math.pi))
                    for i in range(12)], 0.006, "black", seg=4)
    return a.build()


def build_condo(name, floors, width, color, seed):
    """Background apartment block (คอนโด/แฟลต): balconies, AC units, laundry, rooftop tanks. Front faces -Y."""
    a = Asset(name)
    rnd = random.Random(seed)
    D, FH = 10.0, 3.0
    H = floors * FH
    hw = width / 2
    a.bmm(-hw, hw, 0, D, 0, H, color)
    a.bmm(-hw - 0.1, hw + 0.1, -0.1, D + 0.1, H, H + 0.4, "white")
    bays = int(width // 3.2)
    bw = width / bays
    for f in range(1, floors):
        z = f * FH
        a.bmm(-hw - 0.05, hw + 0.05, -0.9, 0.0, z - 0.12, z, "white")            # balcony slab strip
        for b in range(bays):
            x = -hw + bw * (b + 0.5)
            a.bmm(x - bw / 2 + 0.3, x + bw / 2 - 0.3, -0.02, 0.02, z + 0.2, z + 2.4, "window")
            a.bmm(x - bw / 2 + 0.05, x + bw / 2 - 0.05, -0.9, -0.82, z, z + 1.0,
                  rnd.choice(["white", "concrete", color]))                     # balustrade
            r = rnd.random()
            if r < 0.35:
                a.box((0.7, 0.3, 0.5), (x + bw / 2 - 0.6, -0.6, z + 0.3), "white", bevel=0.02)
            if r > 0.6:
                for k in range(rnd.randint(2, 4)):
                    a.box((0.35, 0.02, 0.55), (x - bw / 2 + 0.5 + k * 0.45, -0.7, z + 1.6),
                          rnd.choice(["awning_white", "blue_plastic", "yellow", "sign_pink", "red"]))
            if rnd.random() < 0.3:
                a.sphere(0.3, (x - bw / 2 + 0.4, -0.6, z + 1.3), "leaf", seg=8, rings=5)
    a.bmm(-hw, hw, -0.05, 0.0, 0, FH - 0.2, "window")                               # ground floor glass
    for k in range(rnd.randint(2, 4)):                                             # rooftop
        x = rnd.uniform(-hw + 1, hw - 1)
        a.cyl(0.8, 1.6, (x, rnd.uniform(2, D - 2), H + 1.2), "steel", seg=14)
    a.tube((hw - 1, D / 2, H + 0.4), (hw - 1, D / 2, H + 4), 0.05, "steel_dark")
    a.text(rnd.choice(["อพาร์ทเม้นท์", "คอนโด"]), 0.9, (0, -0.95, H - 0.9), "red", extrude=0.03)
    return a.build()


def build_street_lamp():
    """Steel street lamp; Unity adds the light. Lamp head faces the road (-Y)."""
    a = Asset("SM_StreetLamp")
    a.cyl(0.12, 0.3, (0, 0, 0.15), "concrete", seg=10)
    a.cyl(0.07, 6.5, (0, 0, 3.25), "steel", r2=0.05, seg=10)
    a.polyline([(0, 0, 6.4), (0, -0.4, 6.75), (0, -1.3, 6.85)], 0.045, "steel", seg=8)
    a.box((0.3, 0.6, 0.14), (0, -1.45, 6.8), "steel_dark", bevel=0.03)
    a.box((0.24, 0.5, 0.03), (0, -1.45, 6.72), "headlight")
    a.box((0.45, 0.04, 0.6), (0, 0.06, 3.4), "sign_yellow", bevel=0.01)          # small flag/banner
    a.box((0.45, 0.04, 0.2), (0, 0.06, 2.95), "red")
    return a.build()


def build_string_lights(length=12.4):
    """Festoon of coloured bulbs strung across the street (along -Y), hung at 5.2 m."""
    a = Asset("SM_StringLights")
    n = 34
    pts = []
    for i in range(n):
        t = i / (n - 1)
        pts.append((0, -t * length, 5.2 - 1.0 * math.sin(t * math.pi)))
    a.polyline(pts, 0.01, "black", seg=4)
    for i, p in enumerate(pts[1:-1]):
        a.sphere(0.07, (p[0], p[1], p[2] - 0.09), ["neon_pink", "sign_yellow", "neon_cyan", "light_tube"][i % 4],
                 scale=(1, 1, 1.2), seg=8, rings=6)
    return a.build()


def build_food_stall():
    """Hawker stall with umbrella, steaming pots and stools (Unity adds steam)."""
    a = Asset("SM_FoodStall")
    a.bmm(-0.8, 0.8, -0.35, 0.35, 0, 0.85, "steel")
    a.bmm(-0.82, 0.82, -0.37, 0.37, 0.85, 0.9, "steel_dark")
    for x in (-0.4, 0.35):
        a.cyl(0.22, 0.3, (x, 0, 1.05), "steel", seg=14)
        a.cyl(0.2, 0.02, (x, 0, 1.2), "cocoa", seg=14)
    a.bmm(-0.8, 0.8, -0.36, -0.35, 0.5, 0.8, "red")
    a.text("ข้าวแกง", 0.16, (0, -0.37, 0.65), "white", extrude=0.004)
    a.cyl(0.025, 2.3, (0.75, 0.3, 1.15), "steel", seg=8)
    a.cyl(1.2, 0.4, (0.75, 0.3, 2.3), "sign_blue", r2=0.04, seg=12, stripes="white", smooth=False)
    for x in (-0.6, 0.6):
        a.cyl(0.15, 0.04, (x, -0.8, 0.45), "red_plastic", seg=12)
        a.cyl(0.12, 0.44, (x, -0.8, 0.22), "red_plastic", r2=0.14, seg=10)
    return a.build()


def build_street_tree():
    """Shade tree in a curb planter."""
    a = Asset("SM_StreetTree")
    a.bmm(-0.6, 0.6, -0.6, 0.6, 0, 0.25, "concrete")
    a.cyl(0.14, 3.2, (0, 0, 1.6), "trunk", r2=0.1, seg=10)
    rnd = random.Random(4)
    for k in range(9):
        a.sphere(rnd.uniform(0.9, 1.3), (rnd.uniform(-1.1, 1.1), rnd.uniform(-1.1, 1.1), rnd.uniform(3.4, 4.6)),
                 "leaf" if k % 2 else "leaf_light", scale=(1, 1, 0.75), seg=10, rings=7)
    return a.build()


def build_tunnel_portal():
    """Road tunnel portal (อุโมงค์) cut into a green embankment. Origin = road centre at the portal,
    the tunnel runs toward +Y and is pitch dark inside so cars can vanish into it."""
    a = Asset("SM_TunnelPortal")
    HW, H, L = 4.6, 5.4, 26.0
    a.bmm(-HW, HW, 0, L, -0.1, 0, "tunnel_dark")                          # floor
    a.bmm(-HW - 0.6, -HW, 0, L, 0, H, "tunnel_dark")                     # inner walls
    a.bmm(HW, HW + 0.6, 0, L, 0, H, "tunnel_dark")
    a.bmm(-HW - 0.6, HW + 0.6, 0, L, H, H + 0.6, "tunnel_dark")         # ceiling
    a.bmm(-HW, HW, L - 0.5, L, 0, H, "black")                           # end cap
    for y in range(2, 22, 4):                                            # sodium lights fading into the dark
        for sx in (-1, 1):
            a.bmm(sx * HW - (0.05 if sx > 0 else -0.0), sx * HW + (0.0 if sx > 0 else 0.05), y, y + 1.2, H - 0.8, H - 0.6,
                  "sodium_light")
    a.bmm(-HW + 0.3, HW - 0.3, 0, 6, 0.0, 0.01, "asphalt")
    # concrete portal frame
    a.bmm(-HW - 1.2, -HW, -0.6, 0.4, 0, H + 1.2, "concrete")
    a.bmm(HW, HW + 1.2, -0.6, 0.4, 0, H + 1.2, "concrete")
    a.bmm(-HW - 1.2, HW + 1.2, -0.6, 0.4, H, H + 1.4, "concrete")
    for i in range(10):                                                  # yellow/black hazard band
        x = -HW + i * (2 * HW / 10)
        a.bmm(x, x + 2 * HW / 10, -0.65, -0.6, H - 0.35, H, "yellow" if i % 2 == 0 else "black")
    # blue sign
    a.bmm(-1.8, 1.8, -0.75, -0.62, H + 0.15, H + 1.1, "sign_blue", bevel=0.03)
    a.text("อุโมงค์", 0.5, (0, -0.76, H + 0.62), "white", extrude=0.01)
    # embankment around the portal + grass + shrubs
    a.bmm(-14, -HW - 1.2, 0, 4, 0, H + 1.4, "concrete")
    a.bmm(HW + 1.2, 14, 0, 4, 0, H + 1.4, "concrete")
    a.bmm(-14, 14, 0, L, H + 1.4, H + 2.2, "leaf")
    a.bmm(-14, -HW - 0.6, 4, L, 0, H + 1.4, "leaf")
    a.bmm(HW + 0.6, 14, 4, L, 0, H + 1.4, "leaf")
    rnd = random.Random(31)
    for k in range(10):
        a.sphere(rnd.uniform(0.8, 1.4), (rnd.uniform(-13, 13), rnd.uniform(3, 20), H + 2.4), "leaf_light",
                 scale=(1, 1, 0.8), seg=8, rings=6)
    for x in (-10, -7, 7, 10):                                           # wall planter shrubs
        a.sphere(0.7, (x, -0.4, H + 1.6), "leaf", seg=8, rings=5)
    return a.build()


def build_retaining_wall():
    """8 m concrete retaining wall with a planted top, front faces -Y."""
    a = Asset("SM_RetainingWall")
    a.bmm(-4, 4, 0, 1.0, 0, 5.0, "concrete")
    for x in (-3.5, -1.0, 1.5, 3.9):
        a.bmm(x - 0.08, x + 0.08, -0.05, 0, 0.2, 4.8, "concrete")       # expansion joints
    a.bmm(-4, 4, -0.05, 0, 0, 0.3, "curb_red")
    a.bmm(-4, 4, 0, 1.0, 5.0, 5.3, "leaf")
    rnd = random.Random(8)
    for k in range(5):
        a.sphere(rnd.uniform(0.5, 0.8), (rnd.uniform(-3.5, 3.5), 0.5, 5.5), "leaf_light", seg=8, rings=5)
    return a.build()


def build_cloud(name, seed, width):
    """Low-poly cumulus cloud made of flattened puffs (white; Unity tints it grey when overcast)."""
    a = Asset(name)
    rnd = random.Random(seed)
    n = int(width / 3) + 4
    for k in range(n):
        x = rnd.uniform(-width / 2, width / 2)
        r = rnd.uniform(width * 0.12, width * 0.22) * (1.0 - abs(x) / width)
        a.sphere(max(r, 2.0), (x, rnd.uniform(-width * 0.12, width * 0.12), rnd.uniform(0, r * 0.5)), "cloud",
                 scale=(1.2, 1.0, 0.62), seg=10, rings=6)
    a.sphere(width * 0.3, (0, 0, -width * 0.04), "cloud", scale=(1.7, 0.8, 0.28), seg=12, rings=6)   # flat base
    return a.build()


def build_puddle(name, seed):
    """Flat irregular puddle (a few mm thick) that fades in on wet days."""
    a = Asset(name)
    rnd = random.Random(seed)
    for k in range(5):
        a.cyl(rnd.uniform(0.35, 0.8), 0.006, (rnd.uniform(-0.7, 0.7), rnd.uniform(-0.35, 0.35), 0.003), "puddle",
              seg=14, smooth=False)
    return a.build()


def build_hand_umbrella():
    """Umbrella an NPC holds in the rain; origin at the hand grip, canopy ~0.9 m above it."""
    a = Asset("SM_HandUmbrella")
    a.cyl(0.012, 0.95, (0, 0, 0.47), "steel_dark", seg=6)
    a.tube((0, 0, 0.0), (0.06, 0, -0.08), 0.014, "black", seg=6)
    a.cyl(0.55, 0.22, (0, 0, 0.92), "umbrella", r2=0.04, seg=8, stripes="umbrella2", smooth=False)
    for k in range(8):
        t = k / 8 * 6.283
        a.sphere(0.018, (math.cos(t) * 0.55, math.sin(t) * 0.55, 0.81), "steel_dark", seg=4, rings=3)
    a.sphere(0.025, (0, 0, 1.05), "steel_dark", seg=6, rings=4)
    return a.build()


def build_ground():
    """Big ground slab under the whole street so nothing ever shows the void."""
    a = Asset("SM_Ground")
    a.bmm(-80, 80, -60, 40, -0.4, -0.25, "concrete")
    return a.build()


# ---------------------------------------------------------------- shop furniture
def build_lucky_cat():
    """Maneki-neko (แมวกวัก) for the counter: waving paw, red collar, gold bell and coin. ~0.2 m tall, faces -Y."""
    a = Asset("SM_LuckyCat")
    a.cyl(0.07, 0.02, (0, 0, 0.01), "red_plastic", seg=20)                            # cushion
    a.sphere(0.065, (0, 0, 0.085), "white", scale=(1, 0.85, 1.05), seg=16, rings=10)   # body
    a.sphere(0.055, (0, -0.005, 0.175), "white", scale=(1.15, 1, 0.95), seg=16, rings=10)   # head
    for sx in (-1, 1):
        a.cyl(0.022, 0.035, (sx * 0.035, 0, 0.22), "white", r2=0.002, seg=8)          # ears
        a.cyl(0.012, 0.022, (sx * 0.035, -0.008, 0.217), "pink_milk", r2=0.001, seg=8)
        a.sphere(0.007, (sx * 0.02, -0.058, 0.18), "black", seg=8, rings=5)           # eyes
    a.sphere(0.006, (0, -0.062, 0.168), "pink_milk", seg=8, rings=5)                  # nose
    a.cyl(0.06, 0.012, (0, -0.002, 0.128), "red_plastic", seg=20)                     # collar
    a.sphere(0.012, (0, -0.06, 0.12), "gold", seg=10, rings=6)                         # bell
    a.sphere(0.022, (0.05, -0.03, 0.2), "white", scale=(1, 1, 1.3), seg=10, rings=6)   # raised paw
    a.tube((0.05, -0.03, 0.12), (0.05, -0.03, 0.19), 0.02, "white", seg=10)
    a.cyl(0.032, 0.006, (-0.02, -0.06, 0.07), "gold", rot=(90, 0, 0), seg=16)         # koban coin
    a.text("福", 0.03, (-0.02, -0.064, 0.062), "red_plastic", extrude=0.001)
    return a.build()


def build_bunting():
    """Colourful triangle flags on a string across a 4.2 m shopfront (ธงราว). Origin at the middle of the string."""
    a = Asset("SM_Bunting")
    n = 15
    cols = ["sign_pink", "sign_yellow", "neon_cyan", "matcha", "taro", "white"]
    pts = []
    for i in range(n + 1):
        t = i / n
        pts.append((-2.1 + t * 4.2, 0, -0.18 * 4 * t * (1 - t)))
    a.polyline(pts, 0.006, "black", seg=5)
    for i in range(n):
        t = (i + 0.5) / n
        x = -2.1 + t * 4.2
        z = -0.18 * 4 * t * (1 - t)
        a.prism([(x - 0.12, z), (x + 0.12, z), (x, z - 0.24)], -0.004, 0.004, cols[i % len(cols)])
    return a.build()


def build_fairy_lights():
    """Ping-pong ball fairy lights draped along a 4.2 m shopfront (ไฟปิงปอง). Origin at the middle of the wire."""
    a = Asset("SM_FairyLights")
    pts = []
    for i in range(25):
        t = i / 24
        pts.append((-2.1 + t * 4.2, 0, -0.12 * abs(math.sin(t * math.pi * 3))))
    a.polyline(pts, 0.005, "black", seg=5)
    cols = ["sign_yellow", "neon_pink", "white", "neon_cyan"]
    for i in range(1, 24):
        t = i / 24
        x = -2.1 + t * 4.2
        z = -0.12 * abs(math.sin(t * math.pi * 3))
        a.sphere(0.03, (x, 0, z - 0.04), cols[i % 4], seg=10, rings=6)
    return a.build()


def build_mural():
    """Hand-painted bubble tea wall mural for the shop's side wall. 1.6 x 1.0 m, faces -Y."""
    a = Asset("SM_Mural")
    a.bmm(-0.84, 0.84, 0, 0.03, 0.9, 1.98, "wood_dark", bevel=0.01)                   # frame
    a.bmm(-0.8, 0.8, -0.005, 0.0, 0.94, 1.94, "sign_yellow")                           # painted wall
    for i, (c, x) in enumerate([("thai_tea", -0.48), ("taro", 0.0), ("matcha", 0.48)]):
        a.bmm(x - 0.13, x + 0.13, -0.012, -0.006, 1.06, 1.5, "white")                 # cups
        a.bmm(x - 0.115, x + 0.115, -0.016, -0.012, 1.08, 1.4, c)
        a.bmm(x - 0.15, x + 0.15, -0.016, -0.012, 1.5, 1.55, "sign_pink")             # lids
        a.tube((x + 0.04, -0.018, 1.55), (x + 0.1, -0.018, 1.72), 0.012, "red_plastic")   # straws
        for k in range(5):
            a.sphere(0.022, (x - 0.08 + k * 0.04, -0.02, 1.11), "pearl", scale=(1, 0.3, 1), seg=8, rings=5)
    a.text("ชานมไข่มุก", 0.13, (0, -0.02, 1.76), "sign_pink", extrude=0.004)
    a.text("BUBBLE TEA  ·  SINCE TODAY", 0.045, (0, -0.02, 0.98), "wood_dark", extrude=0.003)
    return a.build()


def build_front_bench():
    """Wooden bench for waiting customers. 1.2 m, faces -Y (street)."""
    a = Asset("SM_FrontBench")
    a.bmm(-0.6, 0.6, -0.2, 0.2, 0.42, 0.47, "wood", bevel=0.01)                        # seat
    a.bmm(-0.6, 0.6, 0.16, 0.2, 0.5, 0.85, "wood", bevel=0.01)                         # back
    for x in (-0.52, 0.52):
        for y in (-0.15, 0.15):
            a.bmm(x - 0.03, x + 0.03, y - 0.03, y + 0.03, 0, 0.42, "wood_dark")
    a.bmm(-0.4, 0.4, 0.155, 0.16, 0.62, 0.74, "sign_pink")
    a.text("นั่งรอได้นะ", 0.07, (0, 0.153, 0.68), "white", extrude=0.002)
    return a.build()


def build_season_cart():
    """Seasonal menu cart: a blender for frappes (hot season) and a jar of marshmallows (cool season).
    Children Cart_Body, Cart_Blender, Cart_Marshmallow (the two stations). Faces -Y, origin on the floor."""
    a = Asset("SM_SeasonCart")
    a._g("Cart_Body")
    a.name_default = "Cart_Body"
    a.bmm(-0.38, 0.38, -0.25, 0.25, 0.78, 0.82, "steel", bevel=0.005)
    a.bmm(-0.36, 0.36, -0.23, 0.23, 0.25, 0.27, "steel")
    for x in (-0.34, 0.34):
        for y in (-0.21, 0.21):
            a.cyl(0.015, 0.8, (x, y, 0.4), "steel_dark", seg=8)
    a.bmm(-0.36, 0.36, 0.21, 0.24, 0.82, 1.3, "sign_yellow")                          # back board
    a.text("เมนูตามฤดู", 0.07, (0, 0.205, 1.2), "sign_pink", extrude=0.002)
    a.text("ปั่น  ·  มาร์ชเมลโลว์", 0.04, (0, 0.205, 1.1), "wood_dark", extrude=0.002)
    g = "Cart_Blender"
    a.box((0.16, 0.16, 0.12), (-0.17, -0.02, 0.88), "black", bevel=0.01, g=g)         # motor base
    a.cyl(0.018, 0.01, (-0.17, -0.105, 0.88), "neon_cyan", rot=(90, 0, 0), seg=10, g=g)
    a.cyl(0.06, 0.24, (-0.17, -0.02, 1.06), "glass", r2=0.075, seg=16, g=g)           # jug
    a.cyl(0.05, 0.12, (-0.17, -0.02, 1.0), "thai_tea", r2=0.06, seg=16, g=g)
    a.cyl(0.078, 0.025, (-0.17, -0.02, 1.19), "black", seg=16, g=g)
    g = "Cart_Marshmallow"
    a.cyl(0.08, 0.2, (0.17, -0.02, 0.92), "glass", seg=18, g=g)                       # jar
    rnd = random.Random(4)
    for i in range(22):
        a.box((0.022, 0.022, 0.02), (0.17 + rnd.uniform(-0.05, 0.05), -0.02 + rnd.uniform(-0.05, 0.05), 0.84 + rnd.uniform(0, 0.13)),
              "white" if i % 2 else "marsh_pink", rot=(rnd.uniform(0, 40), 0, rnd.uniform(0, 90)), bevel=0.004, g=g)
    a.cyl(0.085, 0.03, (0.17, -0.02, 1.035), "sign_pink", seg=18, g=g)
    return a.build()


def build_sky_lantern():
    """Khom loy paper sky lantern for Loy Krathong night (used as a particle mesh). ~0.5 m tall."""
    a = Asset("SM_SkyLantern")
    a.cyl(0.16, 0.42, (0, 0, 0.21), "lantern", r2=0.21, seg=12)            # one material only: particle meshes need a single sub-mesh
    a.cyl(0.1, 0.012, (0, 0, 0.0), "lantern", seg=12)
    return a.build()


def build_order_computer():
    """Shop PC on a small desk for ordering supplies. Front (screen) faces -Y. Origin on the floor."""
    a = Asset("SM_OrderComputer")
    a.bmm(-0.45, 0.45, -0.28, 0.28, 0.72, 0.76, "wood", bevel=0.01)                     # desk top
    for x in (-0.41, 0.41):
        for y in (-0.24, 0.24):
            a.cyl(0.022, 0.72, (x, y, 0.36), "steel_dark", seg=8)
    a.bmm(-0.41, 0.41, 0.2, 0.24, 0.2, 0.6, "wood")                                  # back panel
    # monitor
    a.cyl(0.07, 0.015, (0, 0.1, 0.768), "steel_dark", seg=16)
    a.box((0.04, 0.03, 0.14), (0, 0.1, 0.84), "steel_dark")
    a.box((0.56, 0.045, 0.36), (0, 0.08, 1.05), "black", bevel=0.01)
    a.bmm(-0.255, 0.255, 0.055, 0.058, 0.89, 1.21, "screen")
    a.text("สั่งของ", 0.095, (0, 0.052, 1.09), "black", extrude=0.002)
    a.text("ORDER SUPPLIES", 0.034, (0, 0.052, 0.99), "sign_pink", extrude=0.002)
    for i, c in enumerate(["white", "sign_yellow", "white", "sign_yellow"]):                         # little item tiles on screen
        a.bmm(-0.2 + i * 0.105, -0.12 + i * 0.105, 0.051, 0.054, 0.915, 0.955, c)
    # keyboard + mouse
    a.box((0.38, 0.13, 0.022), (-0.03, -0.13, 0.771), "black", bevel=0.004)
    for r in range(3):
        a.bmm(-0.2, 0.14, -0.18 + r * 0.035, -0.155 + r * 0.035, 0.782, 0.788, "steel_dark")
    a.box((0.045, 0.07, 0.025), (0.27, -0.13, 0.772), "white", bevel=0.01)
    # a box of supplies under the desk
    a.box((0.34, 0.3, 0.24), (0.18, 0.02, 0.12), "cardboard", bevel=0.005)
    a.bmm(0.01, 0.35, -0.135, -0.13, 0.08, 0.16, "sign_pink")
    return a.build()


def build_tutorial_arrow():
    """Glowing downward arrow that hovers over the next station in the tutorial. Origin = tip."""
    a = Asset("SM_TutorialArrow")
    a.cyl(0.004, 0.13, (0, 0, 0.065), "tutorial_glow", r2=0.11, seg=20)                 # cone, tip down
    a.cyl(0.112, 0.012, (0, 0, 0.136), "tutorial_rim", seg=20)
    a.cyl(0.038, 0.17, (0, 0, 0.225), "tutorial_glow", seg=14)                         # shaft
    a.sphere(0.04, (0, 0, 0.31), "tutorial_rim", seg=14, rings=8)
    return a.build()


def build_counter():
    a = Asset("SM_Counter")
    L, Dd, H = 2.4, 0.65, 1.0
    a.bmm(-L / 2, L / 2, -Dd / 2, Dd / 2, 0.08, H - 0.05, "wood_dark")
    a.bmm(-L / 2 + 0.02, L / 2 - 0.02, -Dd / 2 + 0.02, Dd / 2 - 0.02, 0, 0.08, "black")
    a.bmm(-L / 2 - 0.04, L / 2 + 0.04, -Dd / 2 - 0.06, Dd / 2 + 0.03, H - 0.05, H, "marble", bevel=0.01)
    n = 16
    for i in range(n):                                                     # front slats
        x = -L / 2 + L * (i + 0.5) / n
        a.bmm(x - 0.055, x + 0.055, -Dd / 2 - 0.03, -Dd / 2, 0.12, H - 0.1, "wood")
    a.bmm(-0.7, 0.7, -Dd / 2 - 0.05, -Dd / 2 - 0.03, 0.4, 0.7, "sign_pink", bevel=0.01)
    a.text("ยินดีต้อนรับ", 0.13, (0, -Dd / 2 - 0.051, 0.6), "white", extrude=0.003)
    a.text("WELCOME", 0.07, (0, -Dd / 2 - 0.051, 0.47), "sign_yellow", extrude=0.003)
    a.bmm(-L / 2, L / 2, Dd / 2 - 0.25, Dd / 2, 0.45, 0.48, "wood")        # staff-side shelf
    return a.build()


def build_back_counter():
    """Prep table + wall shelf. Children Tin_<Flavor> are separate (ingredient stations)."""
    a = Asset("SM_BackCounter")
    a._g("BackCounter_Body")
    a.name_default = "BackCounter_Body"
    L, Dd, H = 2.6, 0.6, 0.9
    a.bmm(-L / 2, L / 2, -Dd / 2, Dd / 2, H - 0.04, H, "steel")
    a.bmm(-L / 2, L / 2, Dd / 2 - 0.02, Dd / 2, H, H + 0.15, "steel")    # splash guard
    a.bmm(-L / 2 + 0.05, L / 2 - 0.05, -Dd / 2 + 0.05, Dd / 2 - 0.05, 0.18, 0.21, "steel")
    for x in (-L / 2 + 0.04, L / 2 - 0.04):
        for y in (-Dd / 2 + 0.04, Dd / 2 - 0.04):
            a.cyl(0.02, H - 0.04, (x, y, (H - 0.04) / 2), "steel", seg=8)
    # wall shelf above
    a.bmm(-L / 2, L / 2, Dd / 2 - 0.25, Dd / 2, H + 0.75, H + 0.78, "wood")
    for x in (-L / 2 + 0.2, L / 2 - 0.2):
        a.bmm(x - 0.015, x + 0.015, Dd / 2 - 0.2, Dd / 2, H + 0.6, H + 0.75, "steel_dark")
    for i, d in enumerate(["ThaiTea", "Matcha", "Taro", "Cocoa", "MilkTea", "PinkMilk", "RedLime", "ButterflyPea"]):
        x = -L / 2 + 0.27 + i * 0.295
        a.cyl(0.08, 0.2, (x, Dd / 2 - 0.12, H + 0.88), "white", seg=12, g="Tin_" + d)
        a.cyl(0.082, 0.05, (x, Dd / 2 - 0.12, H + 0.86), DRINK_MAT[d], seg=12, g="Tin_" + d)
    return a.build()


PEARL_R = 0.0068


def add_cup_pearls(a, rnd, cx, cy, z0, layers_per_scoop=2, scoops=3, groups=True, seg=8, rings=5):
    """Boba layers in a Cup_Modular-shaped cup (r = 0.033 at the bottom, +0.075 per metre of height)."""
    for layer in range(layers_per_scoop * scoops):
        scoop = layer // layers_per_scoop
        g = ("Cup_Pearls" if scoop == 0 else "Cup_Pearls%d" % (scoop + 1)) if groups else None
        z = PEARL_R + 0.0005 + layer * 0.0112
        r_ring = 0.033 + 0.075 * z - PEARL_R - 0.0004          # just inside the cup wall
        n = int(2 * math.pi * r_ring / (2 * PEARL_R * 1.04))
        off = (layer % 2) * math.pi / n
        for k in range(n):
            ang = off + k / n * 2 * math.pi + rnd.uniform(-0.05, 0.05)
            rr = r_ring - rnd.uniform(0, 0.0008)
            kw = dict(seg=seg, rings=rings)
            if g: kw["g"] = g
            a.sphere(PEARL_R * rnd.uniform(0.92, 1.05),
                     (cx + math.cos(ang) * rr, cy + math.sin(ang) * rr, z0 + z + rnd.uniform(-0.001, 0.001)), "pearl", **kw)
        for k in range(4):                                       # a few in the middle
            ang, rr = rnd.uniform(0, 6.28), rnd.uniform(0, r_ring - 2.1 * PEARL_R)
            kw = dict(seg=seg, rings=rings)
            if g: kw["g"] = g
            a.sphere(PEARL_R, (cx + math.cos(ang) * rr, cy + math.sin(ang) * rr, z0 + z + 0.002), "pearl", **kw)


def build_cup_modular():
    """Gameplay cup. Children: Cup_Body, Liquid_<Flavor> x7, Cup_Pearls/2/3 (one per scoop), Cup_Ice, Cup_SealFilm, Cup_Straw."""
    a = Asset("Cup_Modular")
    a.cyl(0.033, 0.16, (0, 0, 0.08), "plastic_clear", r2=0.045, seg=20, g="Cup_Body")
    for d in DRINKS:
        a.cyl(0.031, 0.13, (0, 0, 0.068), DRINK_MAT[d], r2=0.0415, seg=20, g="Liquid_" + d)
    rnd = random.Random(3)
    # Pearls: one group per scoop (Cup_Pearls, Cup_Pearls2, Cup_Pearls3), two layers each, stacked from the bottom.
    # The outer ring hugs the cup wall and pokes through the drink, so the pearls show through the plastic like real boba.
    add_cup_pearls(a, rnd, 0.0, 0.0, 0.0, layers_per_scoop=2, scoops=3)
    for i in range(7):
        ang = i / 7 * 6.28
        a.box((0.018, 0.018, 0.018), (math.cos(ang) * 0.02, math.sin(ang) * 0.02, 0.105 + (i % 2) * 0.02),
              "ice", rot=(rnd.uniform(0, 90), rnd.uniform(0, 90), 0), g="Cup_Ice")
    a.cyl(0.0412, 0.026, (0, 0, 0.141), "cheese_foam", r2=0.0438, seg=20, g="Cup_Foam")      # cheese foam topping
    a.sphere(0.03, (0, 0, 0.152), "cheese_foam", scale=(1.35, 1.35, 0.32), seg=16, rings=6, g="Cup_Foam")
    a.sphere(0.04, (0, 0, 0.142), "frappe", scale=(1.05, 1.05, 0.55), seg=16, rings=8, g="Cup_Frappe")      # blended ice dome
    for i in range(10):
        ang = i * 2.399
        a.sphere(0.006, (math.cos(ang) * 0.025, math.sin(ang) * 0.025, 0.152), "pearl", seg=6, rings=4, g="Cup_Frappe")
    for i in range(7):                                                               # mini marshmallows floating
        ang = i / 7 * 6.283
        a.box((0.013, 0.013, 0.012), (math.cos(ang) * 0.022, math.sin(ang) * 0.022, 0.138), "white" if i % 2 else "marsh_pink",
              rot=(0, 0, i * 25), bevel=0.003, g="Cup_Marshmallow")
    a.cyl(0.046, 0.003, (0, 0, 0.1615), "seal_film", seg=20, g="Cup_SealFilm")
    a.tube((0.008, 0.004, 0.02), (0.022, 0.012, 0.25), 0.0055, "straw", seg=8, g="Cup_Straw")
    return a.build()


def build_drink_display():
    """Tray with one finished cup of every flavour (menu showcase)."""
    a = Asset("SM_DrinkDisplay")
    rnd = random.Random(8)
    a.bmm(-0.42, 0.42, -0.1, 0.1, 0, 0.02, "wood", bevel=0.005)
    for i, d in enumerate(DRINKS[:7]):                                  # showcase: the regular menu only
        x = -0.36 + i * 0.12
        a.cyl(0.033, 0.16, (x, 0, 0.1), "plastic_clear", r2=0.045, seg=14)
        a.cyl(0.031, 0.13, (x, 0, 0.088), DRINK_MAT[d], r2=0.0415, seg=14)
        a.cyl(0.046, 0.003, (x, 0, 0.1815), "seal_film", seg=14)
        a.tube((x + 0.008, 0.004, 0.04), (x + 0.022, 0.012, 0.27), 0.0055, "straw", seg=6)
        add_cup_pearls(a, rnd, x, 0.0, 0.02, layers_per_scoop=2, scoops=1, groups=False, seg=6, rings=4)
    return a.build()


def build_cup_stack():
    a = Asset("SM_CupStack")
    for i in range(12):
        a.cyl(0.033, 0.16, (0, 0, 0.08 + i * 0.018), "plastic_clear", r2=0.045, seg=16)
    a.cyl(0.05, 0.02, (0, 0, 0.01), "steel_dark", seg=16)
    return a.build()


def build_cup_sealer():
    """Cup sealing machine — iconic in Thai boba shops."""
    a = Asset("SM_CupSealer")
    a._g("Sealer_Body")
    a.name_default = "Sealer_Body"
    a.box((0.32, 0.36, 0.52), (0, 0, 0.26), "white", bevel=0.02)
    a.bmm(-0.11, 0.11, -0.185, -0.1, 0.05, 0.25, "black")                  # cup bay
    a.cyl(0.05, 0.02, (0, -0.12, 0.06), "steel", seg=16)                    # cup holder
    a.bmm(-0.14, 0.14, -0.19, -0.17, 0.3, 0.46, "sign_pink")               # control panel
    a.bmm(-0.1, 0.02, -0.2, -0.19, 0.38, 0.43, "screen")
    for i, c in enumerate(["green_plastic", "red_plastic"]):
        a.cyl(0.018, 0.02, (0.06 + i * 0.05, -0.2, 0.4), c, rot=(90, 0, 0), seg=10)
    a.cyl(0.07, 0.22, (0, 0.06, 0.6), "seal_film", rot=(0, 90, 0), seg=16)  # film roll
    a.cyl(0.02, 0.28, (0, 0.06, 0.6), "steel", rot=(0, 90, 0), seg=8)
    a.tube((0.17, 0, 0.42), (0.2, -0.2, 0.62), 0.015, "steel", g="Sealer_Lever")             # pull lever
    a.sphere(0.035, (0.2, -0.2, 0.62), "red_plastic", seg=10, rings=6, g="Sealer_Lever")
    a.origin("Sealer_Lever", (0.17, 0, 0.42))                               # pivot where the lever meets the machine
    return a.build()


def build_tea_urns():
    """Children Urn_ThaiTea / Urn_Matcha / Urn_MilkTea are separate so each can be its own station."""
    a = Asset("SM_TeaUrnSet")
    a.bmm(-0.5, 0.5, -0.18, 0.18, 0, 0.12, "steel_dark", g="TeaUrn_Stand")
    for i, (lab, c, d) in enumerate([("ชาไทย", "thai_tea", "ThaiTea"), ("ชาเขียว", "matcha", "Matcha"),
                                     ("ชานม", "milk_tea", "MilkTea")]):
        x = -0.32 + i * 0.32
        g = "Urn_" + d
        a.cyl(0.13, 0.38, (x, 0, 0.31), "steel", seg=20, g=g)
        a.sphere(0.13, (x, 0, 0.5), "steel", scale=(1, 1, 0.35), seg=20, rings=6, g=g)
        a.sphere(0.025, (x, 0, 0.55), "black", seg=8, rings=5, g=g)
        a.bmm(x - 0.03, x + 0.03, -0.17, -0.12, 0.17, 0.21, "black", g=g)  # tap
        a.cyl(0.012, 0.06, (x, -0.17, 0.15), "black", seg=8, g=g)
        a.bmm(x - 0.1, x + 0.1, -0.135, -0.125, 0.3, 0.42, c, g=g)         # label plate
        a.text(lab, 0.045, (x, -0.136, 0.36), "white", extrude=0.002, g=g)
    return a.build()


def build_boba_pot():
    """Pearl pot. Children: Pot_Body, Pot_Pearls (cooked pearls), Pot_Water (boiling water, shown while cooking)."""
    a = Asset("SM_BobaPot")
    a._g("Pot_Body")
    a.name_default = "Pot_Body"
    a.box((0.4, 0.4, 0.1), (0, 0, 0.05), "black", bevel=0.01)              # warmer
    a.cyl(0.03, 0.01, (0.12, -0.2, 0.05), "red_plastic", rot=(90, 0, 0), seg=8)
    a.cyl(0.16, 0.2, (0, 0, 0.2), "steel", seg=24)
    a.cyl(0.152, 0.01, (0, 0, 0.27), "pearl", seg=24, g="Pot_Pearls")
    rnd = random.Random(11)
    for i in range(40):
        ang, rad = rnd.uniform(0, 6.28), rnd.uniform(0, 0.14)
        a.sphere(0.011, (math.cos(ang) * rad, math.sin(ang) * rad, 0.278), "pearl", seg=6, rings=4, g="Pot_Pearls")
    a.cyl(0.153, 0.012, (0, 0, 0.262), "boil_water", seg=24, g="Pot_Water")
    for i in range(14):                                                     # bubbles
        ang, rad = rnd.uniform(0, 6.28), rnd.uniform(0, 0.13)
        a.sphere(0.012, (math.cos(ang) * rad, math.sin(ang) * rad, 0.27), "white", scale=(1, 1, 0.5), seg=8, rings=4, g="Pot_Water")
    for s in (-1, 1):
        a.bmm(s * 0.16 - 0.03, s * 0.16 + 0.03, -0.02, 0.02, 0.26, 0.28, "black")
    a.tube((0.05, 0.02, 0.27), (0.2, 0.1, 0.45), 0.01, "steel")            # ladle
    a.sphere(0.04, (0.05, 0.02, 0.275), "steel", scale=(1, 1, 0.5), seg=10, rings=5)
    return a.build()


def build_ice_cooler():
    """Classic Thai red/white ice bucket (กระติกน้ำแข็ง)."""
    a = Asset("SM_IceCooler")
    a.cyl(0.22, 0.42, (0, 0, 0.21), "red_plastic", seg=24)
    a.cyl(0.222, 0.08, (0, 0, 0.3), "white", seg=24)
    a.cyl(0.235, 0.06, (0, 0, 0.45), "white", r2=0.2, seg=24)
    a.cyl(0.06, 0.03, (0, 0, 0.495), "red_plastic", seg=12)
    a.polyline([(-0.23, 0, 0.38), (-0.23, 0, 0.5), (-0.12, 0, 0.6), (0.12, 0, 0.6),
                (0.23, 0, 0.5), (0.23, 0, 0.38)], 0.012, "black")
    return a.build()


def build_syrups():
    a = Asset("SM_SyrupBottles")
    for i, c in enumerate(["cocoa", "white", "pink_milk"]):
        x = -0.12 + i * 0.12
        a.cyl(0.045, 0.24, (x, 0, 0.12), c, seg=14)
        a.cyl(0.02, 0.05, (x, 0, 0.265), "white", seg=10)
        a.cyl(0.014, 0.05, (x, 0, 0.31), "black", seg=8)
        a.bmm(x - 0.008, x + 0.008, -0.07, 0, 0.32, 0.335, "black")
    return a.build()


def build_shaker():
    a = Asset("SM_Shaker")
    a.cyl(0.035, 0.16, (0, 0, 0.08), "steel", r2=0.045, seg=16)
    a.cyl(0.046, 0.05, (0, 0, 0.185), "steel", r2=0.03, seg=16)
    a.cyl(0.025, 0.03, (0, 0, 0.225), "steel", seg=12)
    return a.build()


def build_pos():
    """Tablet POS + cash drawer + QR scan-to-pay standee (very Thai).
    The screen faces the staff (+Y, inside the shop); only the QR standee faces the customers (-Y)."""
    a = Asset("SM_POS")
    a.box((0.42, 0.36, 0.1), (0, 0, 0.05), "black", bevel=0.01)
    a.tube((0, -0.05, 0.1), (0, -0.05, 0.22), 0.015, "steel_dark")
    a.box((0.3, 0.02, 0.21), (0, -0.04, 0.3), "black", rot=(15, 0, 0), bevel=0.005)
    a.box((0.27, 0.005, 0.18), (0, -0.027, 0.302), "screen", rot=(15, 0, 0))
    # QR standee
    qx = -0.32                                                      # left of the till (the drink showcase is on the right)
    a.box((0.16, 0.12, 0.015), (qx, -0.05, 0.0075), "black")
    a.box((0.15, 0.01, 0.22), (qx, -0.05, 0.12), "sign_blue", rot=(-8, 0, 0))
    a.box((0.11, 0.006, 0.11), (qx, -0.058, 0.13), "white", rot=(-8, 0, 0))
    rnd = random.Random(5)
    cells, cs = 9, 0.011
    for i in range(cells):
        for j in range(cells):
            corner = (i < 3 and j < 3) or (i > 5 and j < 3) or (i < 3 and j > 5)
            if corner or rnd.random() < 0.45:
                px = qx - 0.044 + i * cs
                pz = 0.085 + j * cs
                py = -0.062 - (pz - 0.13) * math.tan(R(8))
                a.box((cs, 0.003, cs), (px, py, pz), "black", rot=(-8, 0, 0))
    a.text("สแกนจ่าย", 0.03, (qx, -0.066, 0.21), "white", rot=(82, 0, 0), extrude=0.002)
    return a.build()


def build_menu_board():
    a = Asset("SM_MenuBoard")
    W, H = 1.8, 1.05
    a.bmm(-W / 2 - 0.05, W / 2 + 0.05, 0, 0.08, -0.05, H + 0.05, "wood_dark", bevel=0.01)
    a.bmm(-W / 2, W / 2, -0.01, 0.0, 0, H, "chalkboard")
    a.text("เมนู  MENU", 0.13, (0, -0.012, H - 0.12), "sign_yellow", extrude=0.003)
    items = [("ชาไทย", "45", "thai_tea"), ("ชาเขียว", "50", "matcha"), ("ชานม", "40", "milk_tea"),
             ("เผือก", "50", "taro"), ("นมชมพู", "35", "pink_milk"), ("โกโก้", "45", "cocoa")]
    for i, (n, p, c) in enumerate(items):
        col, row = i % 2, i // 2
        x0 = -W / 2 + 0.15 + col * (W / 2)
        z = H - 0.33 - row * 0.2
        a.cyl(0.04, 0.012, (x0, -0.006, z), c, rot=(90, 0, 0), seg=12)
        a.text(n, 0.085, (x0 + 0.08, -0.012, z), "white", extrude=0.002, align='LEFT')
        a.text(p + " ฿", 0.085, (x0 + 0.72, -0.012, z), "sign_yellow", extrude=0.002, align='RIGHT')
    a.text("ความหวาน  0% · 25% · 50% · 100%", 0.06, (0, -0.012, 0.13), "neon_pink", extrude=0.002)
    return a.build()


def build_fridge():
    a = Asset("SM_DrinkFridge")
    a.bmm(-0.35, 0.35, -0.3, 0.3, 0, 1.9, "white", bevel=0.01)
    a.bmm(-0.31, 0.31, -0.32, -0.3, 0.12, 1.65, "glass")
    a.bmm(-0.35, 0.35, -0.31, -0.29, 1.68, 1.88, "neon_cyan")
    a.text("ชีสโฟม", 0.11, (0, -0.315, 1.78), "white", extrude=0.003)
    for sh in range(4):
        z = 0.2 + sh * 0.37
        a.bmm(-0.3, 0.3, -0.25, 0.25, z, z + 0.015, "steel")
        if sh >= 2:                                                   # top shelves: tubs of cheese foam
            for k in range(4):
                a.cyl(0.06, 0.12, (-0.2 + k * 0.135, -0.1, z + 0.075), "cheese_foam", seg=14)
                a.cyl(0.062, 0.012, (-0.2 + k * 0.135, -0.1, z + 0.14), "sign_yellow", seg=14)
            continue
        for k in range(5):
            c = ["red_drink", "thai_tea", "pink_milk", "matcha", "white"][(k + sh) % 5]
            a.cyl(0.035, 0.22, (-0.24 + k * 0.12, -0.12, z + 0.125), c, seg=10)
    a.bmm(0.26, 0.29, -0.36, -0.32, 0.6, 1.2, "steel")
    return a.build()


def build_standing_fan():
    """Thai electric fan (พัดลม). Child Fan_Blades has its pivot at the hub — rotate local Y in Unity."""
    a = Asset("SM_StandingFan")
    g = "Fan_Body"
    a.cyl(0.18, 0.04, (0, 0, 0.02), "white", seg=20, g=g)
    a.cyl(0.025, 1.0, (0, 0, 0.54), "steel", seg=10, g=g)
    a.sphere(0.08, (0, 0.05, 1.1), "white", scale=(1, 1.4, 1), seg=12, rings=8, g=g)
    hub = (0, -0.06, 1.1)
    pts = [(math.cos(t / 16 * 6.283) * 0.22, -0.11, 1.1 + math.sin(t / 16 * 6.283) * 0.22) for t in range(17)]
    a.polyline(pts, 0.006, "steel", seg=4, g=g)
    for k in range(8):
        t = k / 8 * 6.283
        a.tube((0, -0.12, 1.1), (math.cos(t) * 0.22, -0.11, 1.1 + math.sin(t) * 0.22), 0.004, "steel", seg=4, g=g)
    for k in range(3):                                        # control buttons
        a.box((0.03, 0.03, 0.02), (-0.06 + k * 0.04, -0.12, 0.05), ["blue_plastic", "white", "red_plastic"][k], g=g)
    b = "Fan_Blades"
    a.cyl(0.035, 0.04, hub, "fan_blue", rot=(90, 0, 0), seg=12, g=b)
    for k in range(3):
        t = k * 120
        a.sphere(0.1, (math.cos(R(t)) * 0.1, hub[1], 1.1 + math.sin(R(t)) * 0.1), "fan_blue",
                 scale=(1, 0.08, 0.55), rot=(0, -t, 0), seg=10, rings=6, g=b)
    a.origin(b, hub)
    return a.build()


# ---------------------------------------------------------------- Thai street props
def build_spirit_house():
    """Spirit house (ศาลพระภูมิ) with red-drink and garland offerings."""
    a = Asset("SM_SpiritHouse")
    a.name_default = "SpiritHouse_Body"
    OFF = "SpiritHouse_Offerings"
    a.bmm(-0.45, 0.45, -0.45, 0.45, 0, 0.12, "marble")
    a.bmm(-0.15, 0.15, -0.15, 0.15, 0.12, 1.3, "marble")
    a.bmm(-0.2, 0.2, -0.2, 0.2, 1.15, 1.3, "gold")
    a.bmm(-0.5, 0.5, -0.4, 0.4, 1.3, 1.36, "marble")               # platform
    a.bmm(-0.52, 0.52, -0.42, 0.42, 1.28, 1.3, "gold")
    for i in range(11):                                             # balustrade
        x = -0.48 + i * 0.096
        for y in (-0.38, 0.38):
            a.cyl(0.01, 0.08, (x, y, 1.4), "gold", seg=6)
    for y in (-0.38, 0.38):
        a.bmm(-0.49, 0.49, y - 0.012, y + 0.012, 1.44, 1.46, "gold")
    # house
    hz = 1.36
    a.bmm(-0.24, 0.24, -0.16, 0.24, hz, hz + 0.42, "white")
    a.bmm(-0.09, 0.09, -0.17, -0.16, hz, hz + 0.3, "red")          # doorway
    a.bmm(-0.1, 0.1, -0.175, -0.165, hz + 0.3, hz + 0.33, "gold")
    for x in (-0.27, 0.27):
        for y in (-0.22, 0.27):
            a.cyl(0.02, 0.46, (x, y, hz + 0.23), "gold", seg=8)
    for k in range(3):                                              # front steps
        a.bmm(-0.12, 0.12, -0.3 + k * 0.04, -0.16, hz + k * 0.015, hz + (k + 1) * 0.015, "marble")
    # two-tier gable roof (ridge front-back)
    for tier, (w, h, z, y0, y1) in enumerate([(0.36, 0.3, hz + 0.46, -0.26, 0.31),
                                              (0.26, 0.28, hz + 0.62, -0.2, 0.24)]):
        a.prism([(-w, 0), (w, 0), (0, h)], y0, y1, "red", loc=(0, 0, z))
        a.bmm(-w - 0.01, w + 0.01, y0 - 0.01, y1 + 0.01, z - 0.025, z, "gold")
        ang = math.degrees(math.atan2(h, w))
        L = math.hypot(w, h)
        for s in (-1, 1):                                           # gold barge boards
            a.box((L, 0.02, 0.03), (s * w / 2, y0 - 0.01, z + h / 2), "gold", rot=(0, s * ang, 0))
        a.box((0.02, 0.02, 0.12), (0, y0 - 0.01, z + h + 0.05), "gold", rot=(0, 0, 0))   # chofa
        a.sphere(0.022, (0, y0 - 0.01, z + h + 0.12), "gold", seg=8, rings=5)
        for s in (-1, 1):
            a.box((0.02, 0.02, 0.09), (s * (w + 0.02), y0 - 0.01, z + 0.03), "gold", rot=(0, s * -30, 0))
    a.cyl(0.012, 0.3, (0, 0.03, hz + 1.05), "gold", r2=0.002, seg=8)  # spire
    # offerings: red drinks with straws, garland, incense
    for x in (-0.36, -0.28, 0.28, 0.36):
        a.cyl(0.022, 0.12, (x, -0.28, 1.42), "red_drink", seg=10, g=OFF)
        a.tube((x, -0.28, 1.45), (x + 0.01, -0.29, 1.54), 0.003, "straw", seg=4, g=OFF)
    pts = [(-0.42 + i * 0.042, -0.43, 1.33 - math.sin(i / 20 * math.pi) * 0.12) for i in range(21)]
    for i, p in enumerate(pts):
        a.sphere(0.017, p, "marigold" if i % 4 else "jasmine", seg=6, rings=4, g=OFF)
    a.cyl(0.04, 0.05, (0, -0.3, 1.39), "gold", seg=10)
    for k in range(3):
        a.cyl(0.002, 0.16, (-0.015 + k * 0.015, -0.3, 1.49), "red", seg=4, g=OFF)
    # small elephant figurines (stylised)
    for x in (-0.33, 0.33):
        a.sphere(0.035, (x, -0.12, 1.4), "white", scale=(1.3, 0.9, 0.9), seg=8, rings=5)
        a.sphere(0.022, (x + (0.045 if x > 0 else -0.045), -0.12, 1.42), "white", seg=8, rings=5)
    return a.build()


def seated_driver(a, y, seat_z, hand_y, hand_z, shirt="shirt_blue", legs="denim", skin="skin",
                  helmet=None, hair="hair"):
    """Low-poly person sitting on a seat, front -Y, hands reaching forward to the controls."""
    a.box((0.34, 0.22, 0.46), (0, y + 0.02, seat_z + 0.28), shirt, rot=(-8, 0, 0), bevel=0.06)
    a.cyl(0.045, 0.08, (0, y, seat_z + 0.55), skin, seg=8)
    a.sphere(0.105, (0, y - 0.02, seat_z + 0.68), skin, scale=(0.9, 1, 1.12), seg=12, rings=8)
    if helmet:
        a.sphere(0.13, (0, y - 0.01, seat_z + 0.71), helmet, scale=(1, 1.05, 0.95), seg=12, rings=8)
        a.box((0.18, 0.03, 0.06), (0, y - 0.13, seat_z + 0.7), "glass")
    else:
        a.sphere(0.11, (0, y + 0.01, seat_z + 0.72), hair, scale=(0.95, 1.02, 0.9), seg=12, rings=8)
    for sx in (-1, 1):
        hip = (sx * 0.1, y, seat_z + 0.06)
        knee = (sx * 0.14, y - 0.38, seat_z + 0.1)
        a.tube(hip, knee, 0.075, legs, seg=8)
        a.tube(knee, (sx * 0.14, y - 0.44, seat_z - 0.33), 0.06, legs, seg=8)
        a.box((0.1, 0.22, 0.07), (sx * 0.14, y - 0.5, seat_z - 0.36), "black", bevel=0.02)
        sh = (sx * 0.19, y + 0.02, seat_z + 0.47)
        elbow = (sx * 0.24, (y + hand_y) / 2 + 0.05, (seat_z + 0.47 + hand_z) / 2 - 0.05)
        a.tube(sh, elbow, 0.05, shirt, seg=8)
        a.tube(elbow, (sx * 0.24, hand_y, hand_z), 0.04, skin, seg=8)


def build_tuktuk(name="SM_TukTuk", driver=False):
    a = Asset(name)
    # wheels
    for (x, y) in [(0, -1.15), (-0.62, 0.75), (0.62, 0.75)]:
        a.cyl(0.24, 0.14, (x, y, 0.24), "rubber", rot=(0, 90, 0), seg=18)
        a.cyl(0.13, 0.15, (x, y, 0.24), "chrome", rot=(0, 90, 0), seg=12)
    a.bmm(-0.75, 0.75, 0.55, 0.95, 0.3, 0.48, "tuk_yellow")               # wheel arches
    # chassis & rear passenger tub
    a.bmm(-0.15, 0.15, -1.1, 0.2, 0.32, 0.45, "steel_dark")
    a.bmm(-0.68, 0.68, -0.15, 1.05, 0.42, 0.62, "tuk_blue", bevel=0.03)
    a.bmm(-0.7, 0.7, 0.95, 1.12, 0.42, 1.0, "tuk_blue", bevel=0.03)       # rear panel
    a.bmm(-0.68, 0.68, 1.12, 1.13, 0.55, 0.8, "chrome")
    a.text("TUK TUK", 0.14, (0, 1.14, 0.88), "tuk_yellow", rot=(90, 0, 180), extrude=0.01)
    a.text("ตุ๊กตุ๊ก", 0.12, (0, 1.14, 0.66), "white", rot=(90, 0, 180), extrude=0.01)
    for s in (-1, 1):
        a.bmm(s * 0.7 - 0.02, s * 0.7 + 0.02, -0.15, 1.05, 0.62, 0.85, "tuk_blue")
        a.bmm(s * 0.7 - 0.03, s * 0.7 + 0.03, -0.15, 1.05, 0.6, 0.63, "chrome")
        a.sphere(0.06, (s * 0.6, 1.13, 0.55), "red", scale=(1, 0.4, 1), seg=10, rings=6)
    # passenger bench
    a.bmm(-0.62, 0.62, 0.35, 0.85, 0.62, 0.76, "seat_red", bevel=0.03)
    a.bmm(-0.62, 0.62, 0.8, 0.95, 0.76, 1.25, "seat_red", bevel=0.03)
    # driver cabin: cowl, seat, handlebar, windscreen
    a.bmm(-0.3, 0.3, -1.35, -0.75, 0.42, 1.0, "tuk_blue", bevel=0.05)
    a.box((0.56, 0.04, 0.5), (0, -0.98, 1.25), "glass", rot=(-20, 0, 0))
    a.bmm(-0.32, 0.32, -1.38, -1.3, 0.6, 0.75, "chrome")
    a.sphere(0.09, (0, -1.38, 0.88), "headlight", scale=(1, 0.5, 1), seg=12, rings=6)
    for s in (-1, 1):
        a.sphere(0.04, (s * 0.24, -1.37, 0.98), "yellow", scale=(1, 0.5, 1), seg=8, rings=5)
    a.bmm(-0.25, 0.25, -0.4, -0.1, 0.62, 0.8, "black", bevel=0.03)        # driver seat
    a.polyline([(-0.35, -0.82, 1.05), (-0.15, -0.88, 1.0), (0.15, -0.88, 1.0), (0.35, -0.82, 1.05)],
               0.018, "chrome")
    # roof + pillars + fringe
    for (x, y) in [(-0.68, 1.05), (0.68, 1.05), (-0.3, -0.85), (0.3, -0.85), (-0.68, -0.1), (0.68, -0.1)]:
        a.tube((x, y, 0.6), (x * 1.02, y, 1.72), 0.022, "chrome")
    a.bmm(-0.78, 0.78, -0.95, 1.2, 1.72, 1.8, "tuk_yellow", bevel=0.03)
    a.bmm(-0.6, 0.6, -0.7, 1.0, 1.8, 1.84, "tuk_blue")
    for i in range(18):
        x = -0.75 + i * 0.088
        a.bmm(x, x + 0.044, -0.97, -0.95, 1.6, 1.72, "red" if i % 2 else "white")
    # garland hanging from the mirror
    pts = [(0.0 + 0.02 * math.sin(i), -0.95, 1.6 - i * 0.03) for i in range(8)]
    for i, p in enumerate(pts):
        a.sphere(0.016, p, "marigold" if i % 3 else "jasmine", seg=6, rings=4)
    if driver:
        seated_driver(a, -0.25, 0.8, -0.86, 1.02, shirt="awning_white", legs="pants_grey")
    return a.build()


def build_scooter(name="SM_Scooter", rider=False):
    """Step-through moped with front basket. rider=True adds a moto-taxi rider in an orange vest."""
    a = Asset(name)
    for y in (-0.62, 0.62):
        a.cyl(0.28, 0.08, (0, y, 0.28), "rubber", rot=(0, 90, 0), seg=18)
        a.cyl(0.16, 0.09, (0, y, 0.28), "silver", rot=(0, 90, 0), seg=12)
    a.tube((0, -0.62, 0.28), (0, -0.48, 0.95), 0.025, "chrome")          # fork
    a.tube((0, -0.48, 0.95), (0, -0.45, 1.05), 0.03, "black")
    a.tube((-0.32, -0.42, 1.06), (0.32, -0.42, 1.06), 0.015, "black")    # handlebar
    a.box((0.2, 0.14, 0.14), (0, -0.53, 1.0), "red", bevel=0.03)          # head cowl
    a.sphere(0.06, (0, -0.6, 1.0), "headlight", scale=(1, 0.5, 1), seg=10, rings=6)
    a.box((0.28, 0.08, 0.5), (0, -0.4, 0.62), "red", rot=(-15, 0, 0), bevel=0.03)   # leg shield
    a.box((0.22, 0.6, 0.06), (0, -0.05, 0.38), "black")                  # floorboard
    a.box((0.3, 0.6, 0.3), (0, 0.38, 0.6), "red", bevel=0.05)             # rear body
    a.box((0.26, 0.55, 0.08), (0, 0.33, 0.8), "black", bevel=0.03)        # seat
    a.bmm(-0.12, 0.12, 0.6, 0.9, 0.84, 0.86, "steel_dark")               # rear rack
    a.sphere(0.03, (0, 0.71, 0.66), "red", seg=8, rings=5)
    a.tube((0.08, 0.3, 0.35), (0.1, 0.85, 0.35), 0.025, "chrome")         # exhaust
    # front wire basket
    bx0, bx1, by0, by1, bz0, bz1 = -0.16, 0.16, -0.78, -0.56, 0.85, 1.05
    a.bmm(bx0, bx1, by0, by1, bz0, bz0 + 0.01, "steel_dark")
    for k in range(7):
        x = bx0 + (bx1 - bx0) * k / 6
        a.tube((x, by0, bz0), (x, by0, bz1), 0.004, "steel_dark", seg=4)
        a.tube((x, by1, bz0), (x, by1, bz1), 0.004, "steel_dark", seg=4)
    for y in (by0, by1):
        a.tube((bx0, y, bz1), (bx1, y, bz1), 0.006, "steel_dark", seg=4)
    for x in (bx0, bx1):
        a.tube((x, by0, bz1), (x, by1, bz1), 0.006, "steel_dark", seg=4)
    if rider:
        seated_driver(a, 0.3, 0.84, -0.42, 1.06, shirt="rider_orange", legs="denim", helmet="white")
    else:
        a.tube((-0.15, 0.1, 0.3), (-0.3, 0.2, 0.0), 0.012, "steel_dark")  # kickstand
    return a.build()


# ---------------------------------------------------------------- traffic vehicles (front = -Y)
def car_wheels(a, xs, ys, r=0.32, w=0.22):
    for x in xs:
        for y in ys:
            a.cyl(r, w, (x, y, r), "rubber", rot=(0, 90, 0), seg=16)
            a.cyl(r * 0.55, w + 0.01, (x, y, r), "silver", rot=(0, 90, 0), seg=10)


def car_lights(a, half_w, front_y, back_y, z):
    for sx in (-1, 1):
        a.box((0.28, 0.05, 0.12), (sx * (half_w - 0.22), front_y - 0.01, z), "headlight", bevel=0.02)
        a.box((0.26, 0.05, 0.12), (sx * (half_w - 0.2), back_y + 0.01, z), "red", bevel=0.02)


def build_taxi(name, body, roof):
    """Bangkok taxi-meter sedan (two-tone or pink), tinted windows."""
    a = Asset(name)
    W, L = 1.76, 4.5
    a.box((W, L, 0.58), (0, 0, 0.6), body, bevel=0.1)
    a.box((W - 0.12, 2.2, 0.5), (0, 0.25, 1.12), "window", bevel=0.12)
    a.box((W - 0.2, 1.75, 0.07), (0, 0.25, 1.38), roof, bevel=0.03)
    for y in (-0.8, 0.25, 1.3):
        for sx in (-1, 1):
            a.box((0.06, 0.08, 0.48), (sx * (W / 2 - 0.08), y, 1.12), roof)
    a.box((W, 1.0, 0.08), (0, -1.75, 0.92), body, rot=(-6, 0, 0), bevel=0.04)
    a.box((W + 0.04, 0.15, 0.2), (0, -L / 2, 0.42), "steel_dark", bevel=0.05)
    a.box((W + 0.04, 0.15, 0.2), (0, L / 2, 0.42), "steel_dark", bevel=0.05)
    car_lights(a, W / 2, -L / 2, L / 2, 0.66)
    a.box((0.6, 0.25, 0.16), (0, 0.15, 1.5), "yellow", bevel=0.03)
    a.text("TAXI-METER", 0.07, (0, 0.02, 1.5), "black", extrude=0.004, fnt=FONT_TH)
    a.text("แท็กซี่", 0.11, (W / 2 + 0.002, 0.6, 0.62), "white", rot=(90, 0, 90), extrude=0.004)
    a.box((0.18, 0.02, 0.08), (0, L / 2 + 0.03, 0.55), "white")
    car_wheels(a, (-W / 2 + 0.1, W / 2 - 0.1), (-1.35, 1.35))
    return a.build()


def build_pickup():
    """White pickup with crates in the bed."""
    a = Asset("SM_Car_Pickup")
    W = 1.8
    a.box((W, 5.0, 0.62), (0, 0, 0.68), "white", bevel=0.08)
    a.box((W - 0.1, 1.6, 0.6), (0, -0.55, 1.28), "window", bevel=0.12)
    a.box((W - 0.16, 1.4, 0.07), (0, -0.5, 1.56), "white", bevel=0.03)
    a.box((W, 1.2, 0.1), (0, -1.9, 1.0), "white", rot=(-6, 0, 0), bevel=0.04)
    for sx in (-1, 1):
        a.box((0.06, 2.1, 0.32), (sx * (W / 2 - 0.03), 1.4, 1.14), "white")
    a.box((W, 0.06, 0.32), (0, 2.47, 1.14), "white")
    for i, c in enumerate(["wood", "wood_dark", "wood"]):
        a.box((0.5, 0.5, 0.4), (-0.45 + i * 0.45, 1.3 + (i % 2) * 0.5, 1.2), c, bevel=0.02)
    a.box((W + 0.04, 0.15, 0.2), (0, -2.5, 0.45), "chrome", bevel=0.05)
    a.box((W + 0.04, 0.15, 0.2), (0, 2.5, 0.45), "chrome", bevel=0.05)
    car_lights(a, W / 2, -2.5, 2.5, 0.75)
    car_wheels(a, (-W / 2 + 0.1, W / 2 - 0.1), (-1.5, 1.45), r=0.36)
    return a.build()


def build_songthaew():
    """Red songthaew: pickup with a roofed bed and two passenger benches."""
    a = Asset("SM_Car_Songthaew")
    W = 1.85
    a.box((W, 5.2, 0.6), (0, 0, 0.68), "red", bevel=0.08)
    a.box((W - 0.1, 1.5, 0.6), (0, -1.45, 1.28), "window", bevel=0.12)
    a.box((W, 1.1, 0.1), (0, -2.2, 1.0), "red", rot=(-6, 0, 0), bevel=0.04)
    a.bmm(-W / 2, W / 2, -0.75, 2.6, 2.05, 2.15, "red", bevel=0.03)
    for y in (-0.7, 0.9, 2.55):
        for sx in (-1, 1):
            a.tube((sx * (W / 2 - 0.05), y, 1.0), (sx * (W / 2 - 0.05), y, 2.05), 0.03, "chrome")
    for sx in (-1, 1):
        a.bmm(sx * 0.55 - 0.2, sx * 0.55 + 0.2, -0.6, 2.4, 1.15, 1.22, "seat_red")
        x0 = sx * (W / 2 - 0.04)
        a.bmm(min(x0, x0 + sx * 0.02), max(x0, x0 + sx * 0.02), -0.7, 2.6, 1.0, 1.35, "red")
        a.bmm(sx * (W / 2) - 0.02, sx * (W / 2) + 0.02, -0.7, 2.6, 1.75, 1.82, "yellow")
    a.bmm(-0.4, 0.4, 2.55, 2.75, 0.3, 0.45, "steel_dark")
    a.box((W + 0.04, 0.15, 0.2), (0, -2.6, 0.45), "chrome", bevel=0.05)
    car_lights(a, W / 2, -2.6, 2.6, 0.75)
    car_wheels(a, (-W / 2 + 0.1, W / 2 - 0.1), (-1.7, 1.6), r=0.36)
    return a.build()


def build_bus():
    """City bus (cream/orange), route 8."""
    a = Asset("SM_Bus")
    W, L, H = 2.5, 10.5, 3.0
    a.box((W, L, H - 0.4), (0, 0, 0.4 + (H - 0.4) / 2), "white", bevel=0.12)
    a.bmm(-W / 2 - 0.005, W / 2 + 0.005, -L / 2 + 0.3, L / 2 - 0.3, 0.45, 1.25, "rider_orange")
    a.bmm(-W / 2 - 0.01, W / 2 + 0.01, -L / 2 + 0.6, L / 2 - 0.4, 1.55, 2.45, "window")
    a.bmm(-W / 2 + 0.1, W / 2 - 0.1, -L / 2 - 0.01, -L / 2 + 0.05, 1.35, 2.6, "window")
    a.bmm(-0.9, 0.9, -L / 2 - 0.02, -L / 2 + 0.05, 2.62, 2.9, "black")
    a.text("8  สะพานพุทธ", 0.17, (0, -L / 2 - 0.03, 2.76), "neon_cyan", extrude=0.005)
    for sx in (-1, 1):
        a.bmm(sx * (W / 2) - 0.01, sx * (W / 2) + 0.01, -L / 2 + 1.2, -L / 2 + 2.2, 0.4, 2.45, "steel_dark")
    a.box((W + 0.04, 0.2, 0.3), (0, -L / 2, 0.45), "steel_dark", bevel=0.05)
    car_lights(a, W / 2, -L / 2, L / 2, 0.8)
    car_wheels(a, (-W / 2 + 0.15, W / 2 - 0.15), (-L / 2 + 2.0, L / 2 - 2.3), r=0.5, w=0.3)
    return a.build()


def build_plastic_stool():
    a = Asset("SM_PlasticStool")
    a.cyl(0.16, 0.035, (0, 0, 0.45), "red_plastic", seg=20)
    a.cyl(0.04, 0.036, (0, 0, 0.45), "black", seg=10)
    for k in range(4):
        t = R(45 + k * 90)
        a.box((0.06, 0.03, 0.46), (math.cos(t) * 0.14, math.sin(t) * 0.14, 0.22), "red_plastic",
              rot=(0, 0, 45 + k * 90))
    a.cyl(0.155, 0.02, (0, 0, 0.12), "red_plastic", seg=20)
    return a.build()


def build_steel_table():
    a = Asset("SM_SteelTable")
    a.cyl(0.38, 0.025, (0, 0, 0.72), "steel", seg=28)
    a.cyl(0.385, 0.03, (0, 0, 0.705), "steel", r2=0.38, seg=28)
    a.cyl(0.03, 0.68, (0, 0, 0.36), "steel", seg=10)
    for k in range(4):
        t = R(k * 90)
        a.tube((0, 0, 0.05), (math.cos(t) * 0.3, math.sin(t) * 0.3, 0.02), 0.018, "steel")
    # condiment caddy (พริกน้ำปลา, น้ำตาล ...)
    for k, c in enumerate(["red", "white", "thai_tea", "wood"]):
        a.cyl(0.025, 0.07, (-0.05 + (k % 2) * 0.06, -0.03 + (k // 2) * 0.06, 0.77), c, seg=8)
    a.box((0.15, 0.15, 0.015), (0, 0, 0.74), "steel")
    return a.build()


def build_street_cart():
    """Food cart (รถเข็น) selling fried chicken."""
    a = Asset("SM_StreetCart")
    a.bmm(-0.7, 0.7, -0.35, 0.35, 0.3, 0.95, "steel")
    a.bmm(-0.72, 0.72, -0.37, 0.37, 0.93, 0.97, "steel_dark")
    a.bmm(-0.65, 0.65, -0.3, 0.3, 0.97, 1.45, "glass")
    for x in (-0.65, 0.65):
        for y in (-0.3, 0.3):
            a.bmm(x - 0.015, x + 0.015, y - 0.015, y + 0.015, 0.97, 1.45, "steel")
    a.bmm(-0.67, 0.67, -0.32, 0.32, 1.45, 1.48, "steel")
    a.bmm(-0.6, 0.6, -0.26, 0.26, 1.0, 1.01, "steel_dark")
    for i in range(10):
        a.sphere(0.06, (-0.5 + i * 0.11, -0.05 + (i % 2) * 0.12, 1.06), "thai_tea", scale=(1.2, 1, 0.7),
                 seg=8, rings=5)
    a.bmm(-0.66, 0.66, -0.31, -0.3, 1.25, 1.42, "red")
    a.text("ไก่ทอด", 0.12, (0, -0.311, 1.335), "white", extrude=0.003)
    for s in (-1, 1):
        a.cyl(0.3, 0.05, (0.35, s * 0.42, 0.3), "rubber", rot=(90, 0, 0), seg=18)
        a.cyl(0.18, 0.06, (0.35, s * 0.42, 0.3), "chrome", rot=(90, 0, 0), seg=10)
        a.tube((-0.7, s * 0.3, 0.5), (-1.15, s * 0.3, 0.75), 0.02, "steel")
    a.tube((-1.15, -0.3, 0.75), (-1.15, 0.3, 0.75), 0.022, "black")
    a.tube((-0.55, 0, 0.3), (-0.55, 0, 0.0), 0.03, "steel_dark")
    a.bmm(-0.65, 0.65, -0.3, 0.3, 0.12, 0.14, "steel_dark")
    return a.build()


def build_market_umbrella():
    a = Asset("SM_MarketUmbrella")
    a.cyl(0.25, 0.08, (0, 0, 0.04), "concrete", seg=16)
    a.cyl(0.025, 2.3, (0, 0, 1.19), "steel", seg=8)
    a.cyl(1.35, 0.45, (0, 0, 2.2), "awning_red", r2=0.04, seg=16, stripes="awning_white", smooth=False)
    a.sphere(0.04, (0, 0, 2.45), "white", seg=8, rings=5)
    return a.build()


def build_power_pole():
    """Concrete pole with the famous Thai cable spaghetti. Wires reach +8 m on X (tile poles 8 m apart)."""
    a = Asset("SM_PowerPole")
    a.cyl(0.15, 9.0, (0, 0, 4.5), "concrete", r2=0.1, seg=10, smooth=False)
    for z, w in ((7.6, 1.2), (8.4, 0.9)):
        a.bmm(-0.05, 0.05, -w, w, z - 0.05, z + 0.05, "steel_dark")
        for y in (-w + 0.1, 0, w - 0.1):
            a.cyl(0.035, 0.12, (0, y, z + 0.11), "white", seg=8)
    a.cyl(0.25, 0.7, (0.3, 0.0, 6.6), "concrete", seg=14)                 # transformer
    a.bmm(0.05, 0.12, -0.08, 0.08, 6.4, 6.8, "steel_dark")
    a.bmm(-0.2, 0.2, -0.2, -0.12, 2.4, 2.9, "white")                      # meter / sign box
    rnd = random.Random(21)
    for k in range(14):
        z0 = rnd.uniform(5.2, 7.2)
        y0 = rnd.uniform(-0.35, 0.35)
        sag = rnd.uniform(0.3, 1.0)
        pts = []
        for i in range(11):
            t = i / 10
            pts.append((t * 8.0, y0 + rnd.uniform(-0.03, 0.03), z0 - sag * 4 * t * (1 - t)))
        a.polyline(pts, rnd.choice([0.012, 0.018, 0.025]), "black", seg=5)
    for c in range(3):                                                      # coiled spare cable
        pts = [(0.25 + math.cos(t / 12 * 6.283) * 0.3, 0.2 + c * 0.04,
                5.6 + math.sin(t / 12 * 6.283) * 0.3) for t in range(13)]
        a.polyline(pts, 0.02, "black", seg=5)
    return a.build()


def build_soi_sign():
    a = Asset("SM_SoiSign")
    a.cyl(0.04, 2.8, (0, 0, 1.4), "steel", seg=10)
    a.bmm(-0.6, 0.6, -0.05, -0.03, 2.25, 2.75, "sign_blue", bevel=0.01)
    a.bmm(-0.57, 0.57, -0.055, -0.05, 2.28, 2.72, "white")
    a.bmm(-0.55, 0.55, -0.06, -0.055, 2.3, 2.7, "sign_blue")
    a.text("ซอยสุขใจ 7", 0.17, (0, -0.062, 2.56), "white", extrude=0.003)
    a.text("Soi Suk Jai 7", 0.08, (0, -0.062, 2.38), "white", extrude=0.003, fnt=FONT_TH)
    return a.build()


def build_trash_bin():
    a = Asset("SM_TrashBin")
    a.cyl(0.24, 0.8, (0, 0, 0.4), "green_plastic", r2=0.27, seg=18)
    a.cyl(0.29, 0.08, (0, 0, 0.84), "green_plastic", r2=0.25, seg=18)
    a.cyl(0.08, 0.04, (0, 0, 0.9), "green_plastic", seg=10)
    return a.build()


def build_potted_plant():
    a = Asset("SM_PottedPlant")
    a.cyl(0.2, 0.4, (0, 0, 0.2), "terracotta", r2=0.25, seg=16)
    a.cyl(0.235, 0.02, (0, 0, 0.39), "trunk", seg=16)
    rnd = random.Random(9)
    for k in range(9):
        t = k / 9 * 360 + rnd.uniform(-15, 15)
        tilt = rnd.uniform(25, 55)
        L = rnd.uniform(0.5, 0.8)
        cx = math.cos(R(t)) * math.sin(R(tilt)) * L / 2
        cy = math.sin(R(t)) * math.sin(R(tilt)) * L / 2
        cz = 0.4 + math.cos(R(tilt)) * L / 2
        a.sphere(L / 2, (cx, cy, cz), "leaf" if k % 2 else "leaf_light", scale=(0.25, 0.05, 1),
                 rot=(0, tilt, t - 90 + 90), seg=8, rings=6)
    return a.build()


def build_palm_tree():
    a = Asset("SM_PalmTree")
    pts = [(0.03 * math.sin(i * 0.8) + i * 0.04, 0, i * 0.6) for i in range(10)]
    for i, (p, q) in enumerate(zip(pts, pts[1:])):
        a.cyl(0.16 - i * 0.008, 0.62, ((p[0] + q[0]) / 2, 0, (p[2] + q[2]) / 2), "trunk",
              r2=0.15 - i * 0.008, seg=10, rot=(0, math.degrees(math.atan2(q[0] - p[0], 0.6)), 0))
    top = pts[-1]
    for k in range(9):
        t = k / 9 * 360
        for j in range(3):
            d = 0.45 + j * 0.45
            droop = -0.12 * j * j
            a.sphere(0.32, (top[0] + math.cos(R(t)) * d, math.sin(R(t)) * d, top[2] + 0.1 + droop),
                     "leaf" if (k + j) % 2 else "leaf_light", scale=(1, 0.28, 0.06),
                     rot=(0, 15 * j, t), seg=8, rings=4)
    for k in range(4):
        t = R(k * 90 + 20)
        a.sphere(0.1, (top[0] + math.cos(t) * 0.14, math.sin(t) * 0.14, top[2] - 0.12), "leaf_light",
                 seg=8, rings=6)
    return a.build()


def build_dragon_jar():
    """Ratchaburi dragon water jar (โอ่งมังกร)."""
    a = Asset("SM_DragonJar")
    a.sphere(0.36, (0, 0, 0.42), "ceramic_brown", scale=(1, 1, 1.15), seg=20, rings=12)
    a.cyl(0.22, 0.1, (0, 0, 0.82), "ceramic_brown", r2=0.25, seg=20)
    a.cyl(0.26, 0.03, (0, 0, 0.87), "ceramic_brown", seg=20)
    a.cyl(0.18, 0.08, (0, 0, 0.04), "ceramic_brown", r2=0.24, seg=20)
    # stylised dragon: a coiled gold-yellow band with bumps
    pts = []
    for i in range(25):
        t = i / 24 * 6.283
        z = 0.45 + 0.12 * math.sin(t * 2)
        rr = 0.36 * math.sqrt(max(0.0, 1 - ((z - 0.42) / (0.36 * 1.15)) ** 2)) + 0.01
        pts.append((math.cos(t) * rr, math.sin(t) * rr, z))
    a.polyline(pts, 0.028, "sign_yellow", seg=6)
    a.sphere(0.05, pts[0], "sign_yellow", seg=8, rings=6)
    a.cyl(0.24, 0.02, (0, 0, 0.86), "black", seg=20)                        # water surface
    return a.build()


def build_aframe_sign():
    a = Asset("SM_PromoSign")
    for s in (-1, 1):
        a.box((0.6, 0.03, 1.0), (0, s * 0.17, 0.48), "wood", rot=(s * 10, 0, 0))
    a.box((0.52, 0.01, 0.8), (0, -0.19, 0.5), "chalkboard", rot=(-10, 0, 0))
    a.text("โปรเด็ด!", 0.11, (0, -0.21, 0.75), "sign_yellow", rot=(80, 0, 0), extrude=0.002)
    a.text("2 แก้ว 80 ฿", 0.085, (0, -0.2, 0.55), "white", rot=(80, 0, 0), extrude=0.002)
    a.text("Buy 2 = 80 THB", 0.045, (0, -0.19, 0.4), "neon_pink", rot=(80, 0, 0), extrude=0.002, fnt=FONT_TH)
    a.cyl(0.05, 0.14, (-0.12, -0.19, 0.23), "thai_tea", r2=0.065, rot=(-10, 0, 0), seg=12)
    a.cyl(0.05, 0.14, (0.12, -0.19, 0.23), "taro", r2=0.065, rot=(-10, 0, 0), seg=12)
    return a.build()


def build_coin():
    """10-baht style bimetal coin (generic design) — use as tip / pickup VFX."""
    a = Asset("SM_Coin10")
    a.cyl(0.013, 0.002, (0, 0, 0.001), "silver", seg=24)
    a.cyl(0.0085, 0.0024, (0, 0, 0.0012), "gold", seg=24)
    a.text("10", 0.007, (0, 0, 0.0025), "silver", rot=(0, 0, 0), extrude=0.0003)
    return a.build()


def build_sidewalk():
    """2 m (X) x 3 m (Y) sidewalk tile; red/white painted curb on the -Y edge, top at z=0."""
    a = Asset("SM_Sidewalk")
    for i in range(4):
        for j in range(6):
            c = "paver_a" if (i + j) % 2 == 0 else "paver_b"
            a.bmm(-1 + i * 0.5 + 0.005, -1 + (i + 1) * 0.5 - 0.005, -1.5 + j * 0.5 + 0.005,
                  -1.5 + (j + 1) * 0.5 - 0.005, -0.04, 0, c)
    a.bmm(-1, 1, -1.5, 1.5, -0.15, -0.04, "concrete")
    for k in range(4):
        c = "curb_red" if k % 2 == 0 else "curb_white"
        a.bmm(-1 + k * 0.5, -1 + (k + 1) * 0.5, -1.7, -1.5, -0.3, 0.03, c)
    return a.build()


def build_road():
    """4 m (X) x 7 m (Y) asphalt tile, top at z=0, yellow centre dash."""
    a = Asset("SM_RoadTile")
    a.bmm(-2, 2, -3.5, 3.5, -0.1, 0, "asphalt")
    a.bmm(-1.2, 0.8, -0.06, 0.06, 0, 0.004, "road_line")
    for y in (-3.3, 3.3):
        a.bmm(-2, 2, y - 0.05, y + 0.05, 0, 0.004, "white")
    return a.build()


# ---------------------------------------------------------------- people (static low-poly NPCs)
def person(name, skin, top, bottom, shoes="black", hair="hair", skirt=False, long_skirt=False,
           short_sleeve=True, hair_style="short", height=1.0, wide=1.0):
    """~1.65 m stylised person, front faces -Y, feet at z=0. Returns the Asset so accessories can be added."""
    a = Asset(name)
    h = height
    hip = 0.82 * h
    if long_skirt:
        a.cyl(0.17 * wide, hip - 0.08, (0, 0, 0.08 + (hip - 0.08) / 2), bottom, r2=0.19 * wide, seg=12)
    elif skirt:
        a.cyl(0.24 * wide, 0.32, (0, 0, hip - 0.16), bottom, r2=0.17 * wide, seg=12)
    for s in (-1, 1):
        if not long_skirt:
            a.cyl(0.055, hip - 0.06, (s * 0.09 * wide, 0, 0.06 + (hip - 0.06) / 2),
                  skin if skirt else bottom, r2=0.065, seg=8)
            if not skirt:
                a.cyl(0.07, 0.3, (s * 0.09 * wide, 0, hip - 0.15), bottom, seg=8)
        a.box((0.1, 0.22, 0.07), (s * 0.09 * wide, -0.04, 0.035), shoes, bevel=0.02)
    # torso + shoulders
    a.cyl(0.17 * wide, 0.5 * h, (0, 0, hip + 0.25 * h), top, r2=0.19 * wide, seg=12)
    a.sphere(0.19 * wide, (0, 0, hip + 0.5 * h), top, scale=(1, 0.75, 0.45), seg=12, rings=6)
    sh = hip + 0.46 * h
    for s in (-1, 1):
        x = s * 0.22 * wide
        a.sphere(0.065, (x, 0, sh), top, seg=8, rings=5)
        a.tube((x, 0, sh), (x + s * 0.04, 0.01, sh - 0.28), 0.055, top, seg=8)
        a.tube((x + s * 0.04, 0.01, sh - 0.28), (x + s * 0.05, -0.02, sh - 0.55), 0.045,
               skin if short_sleeve else top, seg=8)
        a.sphere(0.05, (x + s * 0.05, -0.02, sh - 0.58), skin, seg=8, rings=5)
    # neck, head, face
    a.cyl(0.05, 0.08, (0, 0, sh + 0.06), skin, seg=8)
    head = sh + 0.2
    a.sphere(0.12, (0, 0, head), skin, scale=(1, 1, 1.12), seg=14, rings=10)
    for s in (-1, 1):
        a.sphere(0.014, (s * 0.04, -0.112, head + 0.01), "black", seg=6, rings=4)
    a.box((0.05, 0.01, 0.012), (0, -0.115, head - 0.05), "red")
    if hair_style == "bob":
        a.sphere(0.135, (0, 0.015, head + 0.02), hair, scale=(1.02, 1.0, 1.05), seg=14, rings=8)
        a.box((0.2, 0.05, 0.06), (0, -0.1, head + 0.1), hair)
    elif hair_style == "bun":
        a.sphere(0.128, (0, 0.02, head + 0.04), hair, scale=(1, 1, 0.95), seg=14, rings=8)
        a.sphere(0.06, (0, 0.08, head + 0.14), hair, seg=8, rings=6)
    elif hair_style == "long":
        a.sphere(0.13, (0, 0.02, head + 0.03), hair, seg=14, rings=8)
        a.box((0.24, 0.08, 0.32), (0, 0.07, head - 0.12), hair, bevel=0.03)
    elif hair_style != "none":
        a.sphere(0.127, (0, 0.02, head + 0.04), hair, scale=(1, 1, 0.9), seg=14, rings=8)
    a.head_z = head
    a.sh_z = sh
    return a


def build_people():
    out = []
    # นักเรียน: white shirt, navy skirt, backpack
    a = person("SM_NPC_Student", "skin_light", "white", "navy", hair_style="bob", skirt=True, height=0.92)
    a.box((0.26, 0.12, 0.32), (0, 0.17, a.sh_z - 0.2), "blue_plastic", bevel=0.03)
    a.box((0.06, 0.012, 0.06), (0.1, -0.15, a.sh_z - 0.08), "navy")
    out.append(a.build())
    # พนักงานออฟฟิศ: long sleeves, tie, staff card, laptop bag
    a = person("SM_NPC_Office", "skin", "shirt_blue", "pants_grey", short_sleeve=False)
    a.box((0.05, 0.02, 0.3), (0, -0.15, a.sh_z - 0.16), "red")
    a.box((0.06, 0.012, 0.08), (0.08, -0.17, a.sh_z - 0.32), "white")
    a.box((0.32, 0.08, 0.24), (0.3, -0.02, 0.62), "black", bevel=0.02)
    out.append(a.build())
    # นักท่องเที่ยว: tropical shirt, shorts, sun hat, camera
    a = person("SM_NPC_Tourist", "skin_light", "tropical", "khaki", shoes="white", hair="hair_blonde", wide=1.05)
    a.cyl(0.24, 0.02, (0, 0, a.head_z + 0.09), "khaki", seg=16)
    a.cyl(0.13, 0.1, (0, 0, a.head_z + 0.14), "khaki", r2=0.11, seg=16)
    a.box((0.12, 0.06, 0.08), (0, -0.2, a.sh_z - 0.25), "black", bevel=0.01)
    a.cyl(0.025, 0.04, (0, -0.24, a.sh_z - 0.25), "steel_dark", rot=(90, 0, 0), seg=10)
    out.append(a.build())
    # ป้าข้างบ้าน: floral blouse, sarong, bun, market bag
    a = person("SM_NPC_Auntie", "skin", "floral", "sarong", hair="hair_grey", hair_style="bun",
               long_skirt=True, wide=1.12, height=0.95, shoes="red_plastic")
    a.box((0.26, 0.1, 0.3), (-0.34, -0.02, 0.5), "red_plastic", bevel=0.02)
    out.append(a.build())
    # ไรเดอร์: orange jacket, helmet with visor, delivery box
    a = person("SM_NPC_Rider", "skin", "rider_orange", "denim", short_sleeve=False, hair_style="none")
    a.sphere(0.15, (0, 0, a.head_z + 0.03), "rider_orange", scale=(1, 1.05, 0.95), seg=14, rings=8)
    a.box((0.2, 0.02, 0.07), (0, -0.14, a.head_z + 0.01), "glass")
    a.box((0.42, 0.36, 0.38), (0, 0.3, a.sh_z - 0.15), "rider_orange", bevel=0.03)
    a.box((0.3, 0.01, 0.12), (0, 0.481, a.sh_z - 0.12), "white")
    out.append(a.build())
    # รีวิวเวอร์: pink top, white pants, sunglasses, phone on a selfie stick
    a = person("SM_NPC_Influencer", "skin_light", "sign_pink", "white", shoes="white", hair_style="long")
    a.box((0.2, 0.02, 0.04), (0, -0.125, a.head_z + 0.015), "black")
    a.box((0.06, 0.01, 0.11), (0.3, -0.25, a.sh_z - 0.1), "black", rot=(10, 0, 0))
    a.tube((0.27, -0.02, a.sh_z - 0.55), (0.3, -0.24, a.sh_z - 0.15), 0.008, "steel", seg=4)
    out.append(a.build())
    # ลุงคนเดินผ่าน: polo shirt, shorts, slippers
    a = person("SM_NPC_Uncle", "skin", "polo_green", "khaki", shoes="blue_plastic", hair="hair_grey", wide=1.1)
    a.box((0.12, 0.02, 0.1), (0, -0.19, a.sh_z - 0.02), "polo_green")
    out.append(a.build())
    return out


# ============================================================================= BUILD + EXPORT

SHOP_STYLES = {
    "SM_Shop_Pharmacy": ("facade_white", dict(floors=3, front="glass", sign=("ร้านขายยา", "PHARMACY", "sign_green", "white"),
                                              extras={"blade_cross", "goods", "tank", "plants", "laundry"})),
    "SM_Shop_ChickenRice": ("facade_yellow", dict(floors=2, front="open", sign=("ข้าวมันไก่", "CHICKEN RICE", "sign_red", "white"),
                                                  awning=("awning_red", "sign_yellow"), extras={"tables", "steam_pot", "lights"})),
    "SM_Shop_Mart": ("facade_grey", dict(floors=2, front="glass", sign=("มินิมาร์ท 24 ชม.", "MINI MART", "sign_blue", "white"),
                                         extras={"goods", "dish"})),
    "SM_Shop_Salon": ("facade_pink", dict(floors=3, front="glass", sign=("ร้านเสริมสวย", "BEAUTY SALON", "white", "sign_pink"),
                                          extras={"laundry", "plants", "tank"})),
    "SM_Shop_Phone": ("facade_blue", dict(floors=2, front="open", sign=("ซ่อมมือถือ", "MOBILE REPAIR", "sign_yellow", "black"),
                                          awning=("blue_plastic", "white"), extras={"goods", "dish"})),
    "SM_Shop_Coffee": ("facade_green", dict(floors=3, front="open", sign=("กาแฟโบราณ", "THAI COFFEE", "wood_dark", "sign_yellow"),
                                            awning=("green_plastic", "white"), extras={"tables", "lights", "laundry", "tank"})),
    "SM_Shop_ClosedPlain": ("facade_cream", dict(floors=3, front="closed", extras={"laundry", "tank", "plants"})),
}
BUILDERS = [
    lambda: build_shophouse("SM_Shophouse_BobaShop", "facade_mint", True),
    lambda: build_shophouse("SM_Shophouse_Closed", "facade_peach", False),
    *[(lambda n=n, v=v: build_shophouse(n, v[0], style=v[1])) for n, v in SHOP_STYLES.items()],
    lambda: build_condo("SM_Condo_A", 9, 16.0, "facade_cream", 11),
    lambda: build_condo("SM_Condo_B", 6, 22.0, "facade_blue", 23),
    lambda: build_condo("SM_Condo_C", 12, 14.0, "facade_grey", 37),
    build_street_lamp, build_string_lights, build_food_stall, build_street_tree, build_ground,
    build_tunnel_portal, build_retaining_wall,
    lambda: build_cloud("SM_Cloud_A", 3, 34.0), lambda: build_cloud("SM_Cloud_B", 7, 24.0), lambda: build_cloud("SM_Cloud_C", 11, 46.0),
    lambda: build_puddle("SM_Puddle_A", 5), lambda: build_puddle("SM_Puddle_B", 9), build_hand_umbrella,
    build_tutorial_arrow, build_order_computer, build_lucky_cat, build_bunting, build_mural, build_front_bench, build_fairy_lights,
    build_season_cart, build_sky_lantern,
    build_counter, build_back_counter, build_cup_modular, build_drink_display, build_cup_stack,
    build_cup_sealer, build_tea_urns, build_boba_pot, build_ice_cooler, build_syrups, build_shaker,
    build_pos, build_menu_board, build_fridge, build_standing_fan,
    build_spirit_house, build_tuktuk, build_scooter,
    lambda: build_tuktuk("SM_Traffic_TukTuk", driver=True), lambda: build_scooter("SM_Traffic_Scooter", rider=True),
    lambda: build_taxi("SM_Car_Taxi_Pink", "taxi_pink", "taxi_pink"),
    lambda: build_taxi("SM_Car_Taxi_GreenYellow", "yellow", "taxi_green"),
    build_pickup, build_songthaew, build_bus, build_plastic_stool, build_steel_table,
    build_street_cart, build_market_umbrella, build_power_pole, build_soi_sign, build_trash_bin,
    build_potted_plant, build_palm_tree, build_dragon_jar, build_aframe_sign, build_coin,
    build_sidewalk, build_road,
]
ASSETS = {}
for fn in BUILDERS:
    root = fn()
    ASSETS[root.name] = root
for root in build_people():
    ASSETS[root.name] = root


def export_fbx(root):
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    for o in [root] + list(root.children_recursive):
        o.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(FBX_DIR, root.name + ".fbx"), use_selection=True,
        object_types={'EMPTY', 'MESH'}, apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL',
        bake_space_transform=True, axis_forward='-Z', axis_up='Y', mesh_smooth_type='OFF',
        use_mesh_modifiers=True, add_leaf_bones=False, bake_anim=False, path_mode='AUTO')


for root in ASSETS.values():
    export_fbx(root)
print("EXPORTED", len(ASSETS), "assets to", FBX_DIR)

# ============================================================================= SHOWCASE SCENE
SHOW = SCENE.collection


PLACED = set()


def place(name, loc, rz=0.0):
    PLACED.add(name)
    o = ASSETS[name]
    o.location = loc
    o.rotation_euler = (0, 0, R(rz))
    return o


def dup(name, loc, rz=0.0, hide=()):
    src = ASSETS[name]
    new = src.copy()
    SHOW.objects.link(new)
    for c in src.children:
        nc = c.copy()
        SHOW.objects.link(nc)
        nc.parent = new
        if c.name in hide:
            nc.hide_render = True
    new.location = loc
    new.rotation_euler = (0, 0, R(rz))
    return new


# street
place("SM_Shophouse_BobaShop", (0, 0, 0))
place("SM_Shop_ChickenRice", (4.6, 0, 0))
place("SM_Shop_Pharmacy", (-4.6, 0, 0))
place("SM_Shophouse_Closed", (9.2, 0, 0))
place("SM_Shop_Coffee", (-9.2, 0, 0))
place("SM_Ground", (0, 0, 0))
place("SM_StringLights", (2.3, -0.3, 0))
place("SM_StreetLamp", (-6.9, -2.7, 0))
place("SM_Condo_A", (-4, 16, 0))
for i in range(10):
    x = -9 + i * 2
    o = place("SM_Sidewalk", (x, -1.5, 0)) if i == 0 else dup("SM_Sidewalk", (x, -1.5, 0))
for i in range(5):
    x = -8 + i * 4
    o = place("SM_RoadTile", (x, -6.7, -0.15)) if i == 0 else dup("SM_RoadTile", (x, -6.7, -0.15))
# shop interior
place("SM_Counter", (0, 1.4, 0))
place("SM_BackCounter", (0, 6.48, 0))
place("SM_MenuBoard", (0, 6.79, 2.15), 0)
ASSETS["SM_MenuBoard"].rotation_euler = (0, 0, 0)
place("SM_CupSealer", (0.75, 1.5, 1.0))
place("SM_POS", (-0.75, 1.35, 1.0))
place("SM_CupStack", (0.35, 1.55, 1.0))
place("SM_DrinkDisplay", (-0.1, 1.3, 1.0))
place("SM_TeaUrnSet", (-0.6, 6.5, 0.9))
place("SM_BobaPot", (0.5, 6.5, 0.9))
place("SM_SyrupBottles", (1.0, 6.5, 0.9))
place("SM_Shaker", (0.15, 6.4, 0.9))
place("SM_IceCooler", (1.7, 5.2, 0))
place("SM_DrinkFridge", (-1.85, 4.6, 0), 90)
place("SM_StandingFan", (1.7, 3.0, 0), 200)
place("SM_PottedPlant", (-1.8, -0.4, 0))
dup("SM_PottedPlant", (1.8, -0.4, 0), 40)
place("SM_PromoSign", (1.0, -1.0, 0), -15)
# sidewalk life
place("SM_SpiritHouse", (-4.0, -1.0, 0), 10)
place("SM_DragonJar", (-5.2, -0.6, 0))
place("SM_PalmTree", (-6.6, -1.2, 0), 40)
place("SM_SteelTable", (2.6, -1.7, 0))
place("SM_PlasticStool", (2.0, -1.9, 0))
dup("SM_PlasticStool", (3.2, -1.6, 0), 30)
dup("SM_PlasticStool", (2.6, -1.0, 0), 10)
place("SM_MarketUmbrella", (2.6, -1.7, 0))
place("SM_StreetCart", (5.6, -1.6, 0), 180)
place("SM_Scooter", (-1.6, -2.4, 0), 75)
place("SM_TrashBin", (7.4, -2.6, 0))
place("SM_PowerPole", (-2.6, -2.9, 0))
dup("SM_PowerPole", (-10.6, -2.9, 0))
place("SM_SoiSign", (-7.6, -2.75, 0), 0)
place("SM_TukTuk", (1.0, -4.0, -0.15), 90)
place("SM_Car_Taxi_Pink", (-5.0, -8.6, -0.15), -90)
place("SM_Car_Songthaew", (11.0, -5.9, -0.15), 90)
place("SM_Car_Pickup", (-9.0, -5.9, -0.15), 90)
place("SM_Bus", (-16.0, -8.6, -0.15), -90)
place("SM_Traffic_TukTuk", (4.6, -8.6, -0.15), -90)
place("SM_Car_Taxi_GreenYellow", (5.0, -5.9, -0.15), 90)
place("SM_Traffic_Scooter", (-1.5, -5.9, -0.15), 90)
place("Cup_Modular", (0.1, 1.25, 1.0))
for c in ASSETS["Cup_Modular"].children:
    if c.name.startswith("Liquid_") and c.name != "Liquid_ThaiTea":
        c.hide_render = True
place("SM_Coin10", (-0.4, 1.2, 1.001))
place("SM_NPC_Student", (0.0, -0.4, 0), 180)
place("SM_NPC_Office", (-0.15, -1.3, 0), 190)
place("SM_NPC_Auntie", (0.1, -2.2, 0), 170)
place("SM_NPC_Rider", (-2.6, -2.0, 0), -60)
place("SM_NPC_Tourist", (4.3, -2.3, 0), 70)
place("SM_NPC_Uncle", (2.0, -2.4, 0), -110)
place("SM_NPC_Influencer", (-3.4, -1.7, 0), 30)

# props lineup studio (far away)
STUDIO_X = 60
studio = Asset("Studio_Backdrop")
studio.bmm(-20, 20, -12, 4, -0.02, 0, "paver_a")
studio.bmm(-20, 20, 4, 4.05, 0, 10, "plaster")
st = studio.build()
st.location = (STUDIO_X, 0, 0)
lineup = [("Cup_Modular", 0), ("SM_DrinkDisplay", 0), ("SM_CupSealer", 0), ("SM_TeaUrnSet", 0),
          ("SM_BobaPot", 0), ("SM_IceCooler", 0), ("SM_POS", 0), ("SM_SyrupBottles", 0),
          ("SM_Shaker", 0), ("SM_CupStack", 0)]
row2 = [("SM_SpiritHouse", 0), ("SM_PlasticStool", 0), ("SM_SteelTable", 0), ("SM_StandingFan", 0),
        ("SM_DragonJar", 0), ("SM_PromoSign", 0), ("SM_SoiSign", 0)]
hide_liq = tuple("Liquid_" + d for d in DRINKS if d != "Taro")


def width_x(name):
    src = ASSETS[name]
    xs = []
    for o in [src] + list(src.children):
        if o.type == 'MESH':
            xs += [(o.matrix_basis @ Vector(c)).x if o.parent else Vector(c).x for c in o.bound_box]
    return min(xs), max(xs)


def lay_row(items, y, gap=0.18):
    total = sum(width_x(n)[1] - width_x(n)[0] for n in items) + gap * (len(items) - 1)
    x = STUDIO_X - total / 2
    for n in items:
        x0, x1 = width_x(n)
        dup(n, (x - x0, y, 0), 0, hide=hide_liq)
        x += (x1 - x0) + gap
    return total


w1 = lay_row(lineup_names := [n for n, _ in lineup], -1.2)
w2 = lay_row([n for n, _ in row2], 0.9, gap=0.35)
lay_row(["SM_NPC_Student", "SM_NPC_Office", "SM_NPC_Tourist", "SM_NPC_Auntie", "SM_NPC_Rider",
         "SM_NPC_Influencer", "SM_NPC_Uncle"], 2.8, gap=0.45)
ROW_W = max(w1, w2)

for n, o in ASSETS.items():
    if n not in PLACED and n != "SM_Shophouse_BobaShop" and o.location.length < 0.001:
        o.location = (500, 0, 0)          # unused in the showcase: keep it out of the shots

# lighting & world
world = bpy.data.worlds.new("Sky")
SCENE.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.75, 0.97, 1)
world.node_tree.nodes["Background"].inputs[1].default_value = 0.9
sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", 'SUN'))
sun.data.energy = 3.2
sun.data.color = (1.0, 0.93, 0.82)
sun.data.angle = R(3)
sun.rotation_euler = (R(48), R(8), R(-35))
SHOW.objects.link(sun)
for loc, power in [((0, 3.5, 3.2), 70), ((0, 1.0, 3.0), 30)]:
    L = bpy.data.objects.new("ShopLight", bpy.data.lights.new("ShopLight", 'AREA'))
    L.data.energy = power
    L.data.size = 2.5
    L.data.color = (1.0, 0.95, 0.88)
    L.location = loc
    SHOW.objects.link(L)
for loc in [(STUDIO_X - 4, -6, 5), (STUDIO_X + 4, -6, 4)]:
    L = bpy.data.objects.new("StudioLight", bpy.data.lights.new("StudioLight", 'AREA'))
    L.data.energy = 220
    L.data.size = 4
    L.location = loc
    L.rotation_euler = (R(50), 0, R(20 if loc[0] < STUDIO_X else -20))
    SHOW.objects.link(L)


def camera(name, loc, target, lens=30):
    cam = bpy.data.objects.new(name, bpy.data.cameras.new(name))
    cam.data.lens = lens
    cam.location = loc
    d = Vector(target) - Vector(loc)
    cam.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()
    SHOW.objects.link(cam)
    return cam


cams = [
    camera("Cam_Street", (7.5, -13.0, 4.2), (0.2, 0, 2.2), 28),
    camera("Cam_Shop", (0.7, -1.3, 1.65), (-0.1, 2.6, 1.15), 26),
    camera("Cam_Props", (STUDIO_X, -1.2 - ROW_W * 0.78, 2.3), (STUDIO_X, 0.2, 0.62), 36),
]

# render settings
for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
    try:
        SCENE.render.engine = eng
        break
    except TypeError:
        pass
try:
    SCENE.eevee.taa_render_samples = 48
    SCENE.eevee.use_shadows = True
except Exception:
    pass
SCENE.render.resolution_x = 1600
SCENE.render.resolution_y = 900
SCENE.render.film_transparent = False
try:
    SCENE.view_settings.view_transform = 'Standard'
    SCENE.view_settings.exposure = -0.25
except Exception:
    pass

bpy.ops.wm.save_as_mainfile(filepath=os.path.join(SRC_DIR, "ThaiBobaShop.blend"))

import sys
if "--no-render" not in sys.argv:
    for cam in cams:
        SCENE.camera = cam
        SCENE.render.filepath = os.path.join(REN_DIR, cam.name + ".png")
        bpy.ops.render.render(write_still=True)
        print("RENDERED", SCENE.render.filepath)
print("DONE")

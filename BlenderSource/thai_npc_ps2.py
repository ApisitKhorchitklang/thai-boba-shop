"""
PS2-era "realistic" Thai NPCs: realistic proportions, ~3k tris, one baked 512px texture each,
real skeleton with Walk + Idle animations.

Run:
    "D:\\Bder\\blender.exe" -b --factory-startup -P BlenderSource/thai_npc_ps2.py

Outputs
    Assets/Models/ThaiBobaShop/Characters/NPC_<Name>.fbx      rigged + animated (takes "Walk", "Idle")
    Assets/Models/ThaiBobaShop/Characters/Textures/*.png      baked albedo (colour x AO)
    BlenderSource/ThaiNPC_PS2.blend, BlenderSource/renders/npc_ps2_*.png

Method: a joint skeleton -> Skin modifier + Subdivision gives a smooth anatomical body; clothes are assigned by
nearest body segment; procedural fabric/face shaders are baked (Cycles) into a single texture like PS2 games did;
weights come from the same skeleton (nearest bone with joint blending), and the walk cycle is keyed in pose mode.
Front faces -Y in Blender (= +Z forward in Unity).
"""
import bpy, bmesh, math, os, random
import numpy as np
from mathutils import Vector, Matrix, Euler, Quaternion

ROOT = r"D:\Project-Start\A4"
OUT = os.path.join(ROOT, "Assets", "Models", "ThaiBobaShop", "Characters")
TEX = os.path.join(OUT, "Textures")
REN = os.path.join(ROOT, "BlenderSource", "renders")
for d in (OUT, TEX, REN):
    os.makedirs(d, exist_ok=True)

R = math.radians
bpy.ops.wm.read_factory_settings(use_empty=True)
SC = bpy.context.scene
SC.render.fps = 24
world = bpy.data.worlds.new("W")
SC.world = world
world.light_settings.distance = 0.05            # AO bake distance (crevices only)

# ----------------------------------------------------------------------------- skeleton
BASE_H = 1.68
JOINTS = {
    "pelvis": (0, 0, 0.95), "waist": (0, 0, 1.07), "chest": (0, 0, 1.25), "upperchest": (0, 0, 1.385),
    "neck": (0, 0.005, 1.475), "headbase": (0, 0.0, 1.548), "head": (0, -0.005, 1.627), "headtop": (0, 0.0, 1.717),
    "nose": (0, -0.088, 1.61), "jaw": (0, -0.04, 1.572),
    "shoulder.L": (0.175, 0.0, 1.405), "elbow.L": (0.215, 0.03, 1.13), "wrist.L": (0.235, 0.0, 0.885),
    "hand.L": (0.24, -0.012, 0.8),
    "hip.L": (0.09, 0.0, 0.91), "knee.L": (0.097, -0.005, 0.5), "calf.L": (0.097, 0.018, 0.33),
    "ankle.L": (0.097, 0.03, 0.085), "toe.L": (0.097, -0.115, 0.03),
}
for k in list(JOINTS):
    if k.endswith(".L"):
        x, y, z = JOINTS[k]
        JOINTS[k[:-2] + ".R"] = (-x, y, z)

EDGES = [("pelvis", "waist"), ("waist", "chest"), ("chest", "upperchest"), ("upperchest", "neck"),
         ("neck", "headbase")]
for s in ("L", "R"):
    EDGES += [("upperchest", "shoulder." + s), ("shoulder." + s, "elbow." + s), ("elbow." + s, "wrist." + s),
              ("wrist." + s, "hand." + s), ("pelvis", "hip." + s), ("hip." + s, "knee." + s),
              ("knee." + s, "calf." + s), ("calf." + s, "ankle." + s), ("ankle." + s, "toe." + s)]

# body region for clothing assignment, per segment
SEG_REGION = {("pelvis", "waist"): "torso_low", ("waist", "chest"): "torso", ("chest", "upperchest"): "torso",
              ("upperchest", "neck"): "neck", ("neck", "headbase"): "neck", ("headbase", "head"): "head",
              ("head", "headtop"): "head", ("head", "nose"): "head", ("head", "jaw"): "head"}
for s in ("L", "R"):
    SEG_REGION.update({("upperchest", "shoulder." + s): "torso", ("shoulder." + s, "elbow." + s): "upperarm",
                       ("elbow." + s, "wrist." + s): "forearm", ("wrist." + s, "hand." + s): "hand",
                       ("pelvis", "hip." + s): "torso_low", ("hip." + s, "knee." + s): "thigh",
                       ("knee." + s, "calf." + s): "shin", ("calf." + s, "ankle." + s): "shin",
                       ("ankle." + s, "toe." + s): "foot"})


def radii(spec):
    w, f, belly = spec["build"], spec["sex"] == "f", spec.get("belly", 0)
    r = {"pelvis": (0.165 + 0.012 * f, 0.105), "waist": (0.13 - 0.012 * f + belly, 0.092 + belly),
         "chest": (0.16 - 0.01 * f, 0.105 + 0.012 * f), "upperchest": (0.17 - 0.012 * f, 0.092),
         "neck": (0.05 - 0.005 * f, 0.052), "headbase": (0.062, 0.07), "head": (0.088, 0.103),
         "headtop": (0.066, 0.075), "nose": (0.016, 0.014), "jaw": (0.052 - 0.006 * f, 0.045),
         "shoulder": (0.063, 0.063), "elbow": (0.044, 0.044), "wrist": (0.031, 0.026), "hand": (0.036, 0.017),
         "hip": (0.088, 0.092), "knee": (0.056, 0.056), "calf": (0.058, 0.062), "ankle": (0.038, 0.04),
         "toe": (0.04, 0.024)}
    out = {}
    for k in JOINTS:
        base = k.split(".")[0]
        rx, ry = r[base]
        if base in ("pelvis", "waist", "chest", "upperchest", "hip", "knee", "calf", "shoulder", "elbow"):
            rx *= w
            ry *= w
        out[k] = (rx, ry)
    return out


def joints_for(spec):
    s = spec["height"] / BASE_H
    return {k: Vector((x * s * (spec["build"] if abs(x) > 0.05 and not k.startswith(("nose", "head", "jaw")) else 1),
                       y * s, z * s)) for k, (x, y, z) in JOINTS.items()}


# ----------------------------------------------------------------------------- node helpers
class NB:
    def __init__(self, mat):
        mat.use_nodes = True
        self.nt = mat.node_tree
        self.bsdf = self.nt.nodes["Principled BSDF"]
        self.bsdf.inputs["Roughness"].default_value = 0.85
        tc = self.nt.nodes.new("ShaderNodeTexCoord")
        sep = self.nt.nodes.new("ShaderNodeSeparateXYZ")
        self.link(tc.outputs["Object"], sep.inputs[0])
        self.obj = tc.outputs["Object"]
        self.x, self.y, self.z = sep.outputs[0], sep.outputs[1], sep.outputs[2]

    def link(self, a, b):
        self.nt.links.new(a, b)

    def val(self, v):
        if isinstance(v, (int, float)):
            n = self.nt.nodes.new("ShaderNodeValue")
            n.outputs[0].default_value = v
            return n.outputs[0]
        return v

    def math(self, op, a, b=0.0):
        n = self.nt.nodes.new("ShaderNodeMath")
        n.operation = op
        for i, v in enumerate((a, b)):
            if isinstance(v, (int, float)):
                n.inputs[i].default_value = v
            else:
                self.link(v, n.inputs[i])
        return n.outputs[0]

    def rgb(self, c):
        n = self.nt.nodes.new("ShaderNodeRGB")
        n.outputs[0].default_value = (*c, 1)
        return n.outputs[0]

    def mix(self, fac, a, b, blend="MIX"):
        n = self.nt.nodes.new("ShaderNodeMix")
        n.data_type = 'RGBA'
        n.blend_type = blend
        for sock, v in ((n.inputs[0], fac), (n.inputs[6], a), (n.inputs[7], b)):
            if isinstance(v, (int, float)):
                sock.default_value = v
            elif isinstance(v, tuple):
                sock.default_value = (*v, 1)
            else:
                self.link(v, sock)
        return n.outputs[2]

    def noise(self, scale, detail=3.0):
        n = self.nt.nodes.new("ShaderNodeTexNoise")
        n.inputs["Scale"].default_value = scale
        n.inputs["Detail"].default_value = detail
        self.link(self.obj, n.inputs["Vector"])
        return n.outputs["Fac"]

    def voronoi(self, scale, offset=(0, 0, 0)):
        n = self.nt.nodes.new("ShaderNodeTexVoronoi")
        n.inputs["Scale"].default_value = scale
        vm = self.nt.nodes.new("ShaderNodeVectorMath")
        vm.operation = 'ADD'
        vm.inputs[1].default_value = offset
        self.link(self.obj, vm.inputs[0])
        self.link(vm.outputs[0], n.inputs["Vector"])
        return n.outputs["Distance"]

    def wave(self, scale, direction='X'):
        n = self.nt.nodes.new("ShaderNodeTexWave")
        n.wave_type = 'BANDS'
        n.bands_direction = direction
        n.inputs["Scale"].default_value = scale
        self.link(self.obj, n.inputs["Vector"])
        return n.outputs["Fac"]

    def remap(self, v, a, b, c, d):
        n = self.nt.nodes.new("ShaderNodeMapRange")
        n.inputs[1].default_value, n.inputs[2].default_value = a, b
        n.inputs[3].default_value, n.inputs[4].default_value = c, d
        self.link(v, n.inputs[0])
        return n.outputs[0]

    def ellipse(self, cx, cz, rx, rz, ymax):
        """Mask for an ellipse in the XZ plane, front-facing surface only (y < ymax)."""
        dx = self.math('POWER', self.math('DIVIDE', self.math('SUBTRACT', self.x, cx), rx), 2)
        dz = self.math('POWER', self.math('DIVIDE', self.math('SUBTRACT', self.z, cz), rz), 2)
        inside = self.math('LESS_THAN', self.math('ADD', dx, dz), 1.0)
        return self.math('MULTIPLY', inside, self.math('LESS_THAN', self.y, ymax))

    def band(self, axis_sock, lo, hi):
        return self.math('MULTIPLY', self.math('GREATER_THAN', axis_sock, lo), self.math('LESS_THAN', axis_sock, hi))

    def out(self, color):
        self.link(color, self.bsdf.inputs["Base Color"])


def fabric(nb, base, folds=0.12, grain=0.0, grain_scale=80):
    """Base colour with cloth folds (large noise) and optional weave grain."""
    c = nb.rgb(base)
    f = nb.remap(nb.noise(5.0, 4.0), 0.3, 0.7, 1.0 - folds, 1.0 + folds * 0.6)
    c = nb.mix(1.0, c, f, "MULTIPLY")
    if grain > 0:
        g = nb.remap(nb.noise(grain_scale, 1.0), 0.3, 0.7, 1.0 - grain, 1.0 + grain)
        c = nb.mix(1.0, c, g, "MULTIPLY")
    return c


def face_color(nb, p, J):
    c = fabric(nb, p["skin"], folds=0.04)
    hz = J["head"].z
    s = p["height"] / BASE_H
    front = J["head"].y - 0.06 * s
    cheek = nb.math('ADD', nb.ellipse(0.045 * s, hz - 0.02 * s, 0.025 * s, 0.018 * s, front),
                    nb.ellipse(-0.045 * s, hz - 0.02 * s, 0.025 * s, 0.018 * s, front))
    c = nb.mix(nb.math('MULTIPLY', cheek, 0.25), c, (0.85, 0.45, 0.4))
    for sx in (-1, 1):
        ex, ez = sx * 0.034 * s, hz + 0.012 * s
        c = nb.mix(nb.ellipse(ex, ez + 0.004 * s, 0.021 * s, 0.012 * s, front), c,
                   tuple(v * 0.75 for v in p["skin"]))                       # eye socket shade
        c = nb.mix(nb.ellipse(ex, ez, 0.016 * s, 0.0065 * s, front), c, (0.92, 0.9, 0.86))   # sclera
        c = nb.mix(nb.ellipse(ex, ez, 0.0075 * s, 0.0065 * s, front), c, (0.13, 0.07, 0.04))  # iris
        c = nb.mix(nb.ellipse(ex, ez, 0.0032 * s, 0.0032 * s, front), c, (0.01, 0.01, 0.01))  # pupil
        c = nb.mix(nb.ellipse(ex, ez + 0.0068 * s, 0.017 * s, 0.0022 * s, front), c, (0.05, 0.04, 0.03))  # lash
        c = nb.mix(nb.ellipse(ex + sx * 0.002 * s, ez + 0.03 * s, 0.021 * s, 0.0042 * s, front), c, p["brow"])
    c = nb.mix(nb.ellipse(0, hz - 0.052 * s, 0.021 * s, 0.0062 * s, front), c, p["lips"])        # lips
    c = nb.mix(nb.ellipse(0, hz - 0.0525 * s, 0.02 * s, 0.0012 * s, front), c, tuple(v * 0.5 for v in p["lips"]))
    c = nb.mix(nb.ellipse(0, hz - 0.03 * s, 0.012 * s, 0.006 * s, front - 0.02), c, tuple(v * 0.8 for v in p["skin"]))
    return c


def top_color(nb, p, J):
    t = p["top"]
    c = fabric(nb, t["color"], folds=0.14, grain=0.04)
    pat = t.get("pattern")
    if pat in ("floral", "tropical"):
        c = nb.mix(nb.math('LESS_THAN', nb.voronoi(14 if pat == "floral" else 9), 0.22), c, t["color2"])
        c = nb.mix(nb.math('LESS_THAN', nb.voronoi(11, (3.3, 1.7, 0.4)), 0.14), c, t.get("color3", (0.95, 0.9, 0.6)))
    elif pat == "pinstripe":
        c = nb.mix(nb.math('GREATER_THAN', nb.wave(70, 'X'), 0.88), c, t["color2"])
    elif pat == "reflective":
        z0 = J["chest"].z
        c = nb.mix(nb.band(nb.z, z0 - 0.06, z0 - 0.03), c, (0.85, 0.87, 0.9))
        c = nb.mix(nb.band(nb.z, z0 + 0.04, z0 + 0.07), c, (0.85, 0.87, 0.9))
    if t.get("buttons"):
        c = nb.mix(nb.math('MULTIPLY', nb.band(nb.x, -0.004, 0.004), nb.math('LESS_THAN', nb.y, -0.05)),
                   c, tuple(v * 0.8 for v in t["color"]))
    return c


def bottom_color(nb, p):
    b = p["bottom"]
    c = fabric(nb, b["color"], folds=0.16, grain=0.08 if b.get("denim") else 0.03, grain_scale=120)
    if b.get("pleats"):
        c = nb.mix(1.0, c, nb.remap(nb.wave(45, 'X'), 0, 1, 0.82, 1.05), "MULTIPLY")
    if b.get("pattern") == "batik":
        c = nb.mix(nb.math('LESS_THAN', nb.voronoi(16), 0.2), c, b["color2"])
        c = nb.mix(nb.band(nb.z, 0.1, 0.16), c, b["color2"])
    return c


def seg_dist(nb, a, b):
    """Shader-side distance from the shading point to segment a-b (object space)."""
    ab = b - a
    vm = lambda op: nb.nt.nodes.new("ShaderNodeVectorMath")
    sub = vm(0); sub.operation = 'SUBTRACT'; nb.link(nb.obj, sub.inputs[0]); sub.inputs[1].default_value = a
    dot = vm(0); dot.operation = 'DOT_PRODUCT'; nb.link(sub.outputs[0], dot.inputs[0]); dot.inputs[1].default_value = ab
    t = nb.math('MULTIPLY', dot.outputs["Value"], 1.0 / max(ab.length_squared, 1e-9))
    t = nb.math('MINIMUM', nb.math('MAXIMUM', t, 0.0), 1.0)
    sc = vm(0); sc.operation = 'SCALE'; sc.inputs[0].default_value = ab; nb.link(t, sc.inputs["Scale"])
    d = vm(0); d.operation = 'SUBTRACT'; nb.link(sub.outputs[0], d.inputs[0]); nb.link(sc.outputs[0], d.inputs[1])
    ln = vm(0); ln.operation = 'LENGTH'; nb.link(d.outputs[0], ln.inputs[0])
    return ln.outputs["Value"]


def body_color(nb, p, J):
    """Whole-body texture with clean hemlines: skin + face, then trousers, shirt, shoes layered by masks."""
    s = p["height"] / BASE_H
    w = p["build"]
    top, bot = p["top"], p["bottom"]
    arm = None
    for side in ("L", "R"):
        for a, b, r in (("shoulder", "elbow", 0.075), ("elbow", "wrist", 0.05), ("wrist", "hand", 0.045)):
            m = nb.math('LESS_THAN', seg_dist(nb, J[a + "." + side], J[b + "." + side]), r * s * w)
            arm = m if arm is None else nb.math('MAXIMUM', arm, m)
    not_arm = nb.math('SUBTRACT', 1.0, arm)
    z = nb.z
    c = face_color(nb, p, J)
    # trousers / shorts / underwear under skirts
    leg_hem = {"pants": 0.09, "shorts": 0.56}.get(bot["type"], 0.8) * s
    bottom_mask = nb.math('MULTIPLY', not_arm, nb.band(z, leg_hem, 1.04 * s))
    c = nb.mix(bottom_mask, c, bottom_color(nb, p))
    # shirt: torso between hem and collar (V-neck dips at the centre), sleeves down to elbow or wrist
    hem = (0.97 if top.get("untucked") else 1.0) * s
    collar = 1.455 * s
    if top.get("vneck"):
        dip = nb.math('MULTIPLY', nb.math('MAXIMUM', nb.math('SUBTRACT', 1.0, nb.math('DIVIDE', nb.math('ABSOLUTE', nb.x), 0.07 * s)), 0.0), 0.09 * s)
        collar = nb.math('SUBTRACT', collar, dip)
    torso = nb.math('MULTIPLY', nb.math('GREATER_THAN', z, hem), nb.math('LESS_THAN', z, collar))
    sleeve_end = (J["wrist.L"].z + 0.025 * s) if top.get("long") else (J["elbow.L"].z + 0.13 * s)
    sleeve = nb.math('MULTIPLY', arm, nb.math('GREATER_THAN', z, sleeve_end))
    top_mask = nb.math('MAXIMUM', nb.math('MULTIPLY', not_arm, torso), sleeve)
    c = nb.mix(top_mask, c, top_color(nb, p, J))
    # socks + shoes
    shoe_top = (0.03 if p.get("sandals") else 0.085) * s
    if p.get("socks"):
        c = nb.mix(nb.math('MULTIPLY', not_arm, nb.math('LESS_THAN', z, 0.19 * s)), c, p["socks"])
    c = nb.mix(nb.math('MULTIPLY', not_arm, nb.math('LESS_THAN', z, shoe_top)), c,
               fabric(nb, p["shoes"], folds=0.05, grain=0.05))
    return c


def make_material(name, kind, spec, J):
    m = bpy.data.materials.new(name)
    nb = NB(m)
    p = spec
    if kind == "body":
        nb.out(body_color(nb, p, J))
    elif kind == "hair":
        c = nb.rgb(p["hair_color"])
        c = nb.mix(1.0, c, nb.remap(nb.wave(55, 'X'), 0.0, 1.0, 0.75, 1.15), "MULTIPLY")
        c = nb.mix(1.0, c, nb.remap(nb.noise(30, 2), 0.3, 0.7, 0.85, 1.1), "MULTIPLY")
        nb.out(c)
    elif kind == "bottom":
        nb.out(bottom_color(nb, p))
    else:  # accessory: kind = ("acc", colour)
        nb.out(fabric(nb, kind[1], folds=0.08, grain=0.03))
    return m


# ----------------------------------------------------------------------------- geometry
def make_head(spec, J):
    """Head sculpted from a UV sphere: skull, tapered jaw, chin, nose, brow ridge, eye sockets, ears."""
    s = spec["height"] / BASE_H
    f = spec["sex"] == "f"
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=28, v_segments=18, radius=1.0)
    g = lambda a, b, c, ra, rb, rc, x, y, z: math.exp(-(((x - a) / ra) ** 2 + ((y - b) / rb) ** 2 + ((z - c) / rc) ** 2))
    for v in bm.verts:
        x, y, z = v.co
        sx, sy, sz = 0.077 - 0.004 * f, 0.095, 0.108
        if z < 0:                                   # jaw tapers toward the chin
            sx *= 1 - 0.32 * (-z) ** 1.4
            if y > 0:
                sy *= 1 - 0.35 * (-z) ** 1.2         # no skull at the back of the jaw
        if y > 0 and z > -0.1:
            sy *= 1.06                               # back of the skull
        nx, ny, nz = x * sx, y * sy, z * sz
        if y < 0:
            ny -= 0.02 * g(0, -1, -0.15, 0.17, 0.6, 0.25, x, y, z)          # nose
            ny -= 0.007 * g(0, -1, -0.62, 0.35, 0.6, 0.18, x, y, z)         # chin
            ny -= 0.006 * g(0, -1, 0.3, 0.6, 0.6, 0.1, x, y, z)             # brow ridge
            for ex in (-0.42, 0.42):
                ny += 0.008 * g(ex, -1, 0.12, 0.18, 0.6, 0.12, x, y, z)     # eye sockets
            nx *= 1 + 0.05 * g(0, -0.6, -0.05, 2, 0.5, 0.25, abs(x), y, z)  # cheekbones
        nx *= 1 + 0.13 * g(1, 0, 0.0, 0.15, 0.25, 0.28, abs(x), y, z)       # ears
        v.co = Vector((nx * s, ny * s, nz * s)) + J["head"]
    me = bpy.data.meshes.new("head")
    bm.to_mesh(me)
    bm.free()
    for p_ in me.polygons:
        p_.use_smooth = True
    return link_obj("head", me)


def link_obj(name, me):
    ob = bpy.data.objects.new(name, me)
    SC.collection.objects.link(ob)
    return ob


def build_body(spec, J):
    rad = radii(spec)
    used = {n for e in EDGES for n in e}
    names = [n for n in J if n in used]
    me = bpy.data.meshes.new("skel")
    me.from_pydata([J[n] for n in names], [(names.index(a), names.index(b)) for a, b in EDGES], [])
    ob = link_obj("skel", me)
    sk = ob.modifiers.new("Skin", 'SKIN')
    sk.use_smooth_shade = True
    sk.branch_smoothing = 0.3
    for i, n in enumerate(names):
        v = me.skin_vertices[0].data[i]
        v.radius = rad[n]
        v.use_root = (n == "pelvis")
    sub = ob.modifiers.new("Sub", 'SUBSURF')
    sub.levels = 1
    dg = bpy.context.evaluated_depsgraph_get()
    body_me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    bpy.data.objects.remove(ob)
    body = link_obj(spec["name"], body_me)
    return body


def seg_closest(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return (a + ab * t - p).length, t


def assign_regions(body, spec, J, mats):
    """Pick a material per face from the nearest skeleton segment + clothing rules."""
    sh = spec["height"] / BASE_H
    top, bottom = spec["top"], spec["bottom"]
    for poly in body.data.polygons:
        c = body.matrix_world @ poly.center
        best, region, tt = 9, None, 0
        for (a, b), reg in SEG_REGION.items():
            d, t = seg_closest(c, J[a], J[b])
            if d < best:
                best, region, tt = d, reg, t
        z = c.z / sh
        if region == "head":
            m = "face"
        elif region in ("neck",):
            m = "skin" if z > 1.43 or top.get("vneck") else "top"
        elif region == "hand":
            m = "skin"
        elif region == "forearm":
            m = "top" if top.get("long") else "skin"
        elif region == "upperarm":
            m = "top" if (top.get("long") or tt < 0.55) else "skin"
        elif region == "torso":
            m = "skin" if (z > 1.44 and abs(c.x) < 0.07) else "top"
        elif region == "torso_low":
            m = "top" if z > (0.99 if top.get("untucked") else 1.02) else "bottom"
        elif region == "thigh":
            m = "bottom" if bottom["type"] in ("pants", "shorts") or bottom["type"] == "sarong" else "skin"
            if bottom["type"] == "shorts" and z < 0.6:
                m = "skin"
        elif region == "shin":
            m = "bottom" if bottom["type"] == "pants" else "skin"
            if z < 0.12:
                m = "shoes"
        elif region == "foot":
            m = "skin" if spec.get("sandals") and c.z > 0.035 * sh else "shoes"
        else:
            m = "skin"
        poly.material_index = mats.index(m)


def sphere_obj(name, center, r, scale=(1, 1, 1), seg=16, rings=10):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=seg, v_segments=rings, radius=r,
                              matrix=Matrix.Translation(center) @ Matrix.Diagonal((*scale, 1)))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    return link_obj(name, me)


def cut(ob, keep):
    """Delete vertices for which keep(co) is False."""
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not keep(v.co)], context='VERTS')
    bm.to_mesh(ob.data)
    bm.free()


def box_obj(name, size, loc, rot=(0, 0, 0), bevel=0.01):
    bm = bmesh.new()
    vs = bmesh.ops.create_cube(bm, size=1, matrix=Matrix.Translation(loc) @ Euler([R(a) for a in rot]).to_matrix().to_4x4()
                               @ Matrix.Diagonal((*size, 1)))["verts"]
    if bevel:
        bmesh.ops.bevel(bm, geom=list(vs) + list({e for v in vs for e in v.link_edges}), offset=bevel,
                        segments=2, affect='EDGES', profile=0.5)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    return link_obj(name, me)


def cone_obj(name, r1, r2, h, loc, seg=20, cap=True):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=seg, radius1=r1, radius2=r2, depth=h,
                          matrix=Matrix.Translation(loc))
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = len(p.vertices) == 4
    return link_obj(name, me)


def hair_and_extras(spec, J):
    """Returns list of (object, material key)."""
    s = spec["height"] / BASE_H
    H = J["head"]
    hz, hy = H.z, H.y
    parts = []
    style = spec["hair"]
    if style != "helmet":
        hr = (0.094 if style == "buzz" else 0.1) * s
        h = sphere_obj("hair", H + Vector((0, 0.012 * s, 0.014 * s)), hr, (1.03, 1.1, 1.02), seg=20, rings=14)

        def keep(co):
            face = co.y < hy - 0.035 * s and co.z < hz + 0.045 * s
            low_side = co.z < hz - (0.075 if style in ("bob", "long") else 0.035) * s
            nape = co.y > hy + 0.02 * s and co.z > hz - 0.09 * s
            return not face and (not low_side or nape)
        cut(h, keep)
        parts.append((h, "hair"))
        if style == "long":
            parts.append((box_obj("hairlong", (0.19 * s, 0.07 * s, 0.3 * s), H + Vector((0, 0.07 * s, -0.17 * s)),
                                  bevel=0.03 * s), "hair"))
        if style == "ponytail":
            parts.append((sphere_obj("tie", H + Vector((0, 0.1 * s, 0.03 * s)), 0.04 * s, seg=10, rings=8), "hair"))
            parts.append((box_obj("tail", (0.07 * s, 0.06 * s, 0.22 * s), H + Vector((0, 0.12 * s, -0.09 * s)),
                                  rot=(12, 0, 0), bevel=0.025 * s), "hair"))
        if style == "bun":
            parts.append((sphere_obj("bun", H + Vector((0, 0.085 * s, 0.07 * s)), 0.045 * s, seg=12, rings=8), "hair"))
    else:
        hm = sphere_obj("helmet", H + Vector((0, 0.008 * s, 0.018 * s)), 0.135 * s, (1, 1.08, 1.0), seg=22, rings=14)
        cut(hm, lambda co: co.z > hz - 0.06 * s and not (co.y < hy - 0.04 * s and co.z < hz + 0.03 * s))
        parts.append((hm, ("acc", spec["top"]["color"])))
        parts.append((box_obj("visor", (0.2 * s, 0.03 * s, 0.07 * s), H + Vector((0, -0.125 * s, 0.03 * s)),
                              rot=(-15, 0, 0), bevel=0.01), ("acc", (0.15, 0.18, 0.22))))
    b = spec["bottom"]
    if b["type"] == "skirt":
        parts.append((cone_obj("skirt", 0.22 * s * spec["build"], 0.165 * s * spec["build"], 0.36 * s,
                               Vector((0, 0.005, 0.83 * s)), cap=False), "bottom"))
    if b["type"] == "sarong":
        parts.append((cone_obj("sarong", 0.2 * s * spec["build"], 0.17 * s * spec["build"], 0.92 * s,
                               Vector((0, 0.005, 0.55 * s))), "bottom"))
    for acc in spec.get("extras", []):
        kind, col = acc[0], acc[1]
        if kind == "backpack":
            parts.append((box_obj("backpack", (0.27 * s, 0.13 * s, 0.36 * s), J["chest"] + Vector((0, 0.16 * s, -0.04 * s)),
                                  bevel=0.035 * s), ("acc", col)))
        elif kind == "deliverybox":
            parts.append((box_obj("box", (0.42 * s, 0.36 * s, 0.4 * s), J["chest"] + Vector((0, 0.3 * s, 0.0)),
                                  bevel=0.02 * s), ("acc", col)))
        elif kind == "tie":
            parts.append((box_obj("tie", (0.045 * s, 0.012 * s, 0.32 * s), J["chest"] + Vector((0, -0.115 * s, 0.0)),
                                  rot=(-6, 0, 0), bevel=0.005), ("acc", col)))
        elif kind == "lanyard":
            parts.append((box_obj("card", (0.06 * s, 0.01 * s, 0.085 * s), J["chest"] + Vector((0.05 * s, -0.122 * s, -0.08 * s)),
                                  bevel=0.003), ("acc", col)))
        elif kind == "hat":
            parts.append((cone_obj("brim", 0.2 * s, 0.2 * s, 0.012 * s, H + Vector((0, 0, 0.075 * s)), seg=24), ("acc", col)))
            parts.append((cone_obj("crown", 0.105 * s, 0.09 * s, 0.09 * s, H + Vector((0, 0.005, 0.12 * s)), seg=20), ("acc", col)))
        elif kind == "camera":
            parts.append((box_obj("camera", (0.12 * s, 0.06 * s, 0.08 * s), J["chest"] + Vector((0, -0.15 * s, -0.08 * s)),
                                  bevel=0.01), ("acc", col)))
        elif kind == "bag":
            parts.append((box_obj("bag", (0.06 * s, 0.26 * s, 0.24 * s), J["hand.L"] + Vector((0.03 * s, 0, -0.12 * s)),
                                  bevel=0.02 * s), ("acc", col)))
        elif kind == "phone":
            parts.append((box_obj("phone", (0.035 * s, 0.075 * s, 0.01 * s), J["hand.R"] + Vector((0, -0.04 * s, 0.0)),
                                  rot=(70, 0, 0), bevel=0.003), ("acc", col)))
        elif kind == "sunglasses":
            parts.append((box_obj("glasses", (0.15 * s, 0.015 * s, 0.032 * s), H + Vector((0, -0.1 * s, 0.012 * s)),
                                  bevel=0.006), ("acc", col)))
        elif kind == "visor":
            parts.append((box_obj("visor", (0.2 * s, 0.11 * s, 0.012 * s), H + Vector((0, -0.12 * s, 0.07 * s)),
                                  rot=(-8, 0, 0), bevel=0.004), ("acc", col)))
            parts.append((cone_obj("band", 0.105 * s, 0.105 * s, 0.035 * s, H + Vector((0, 0.005, 0.065 * s)),
                                   seg=20, cap=False), ("acc", col)))
        elif kind == "buckethat":
            parts.append((cone_obj("bucket", 0.16 * s, 0.105 * s, 0.1 * s, H + Vector((0, 0.005, 0.085 * s)), seg=20),
                          ("acc", col)))
        elif kind == "apron":
            parts.append((box_obj("apron", (0.3 * s * spec["build"], 0.02 * s, 0.55 * s),
                                  J["waist"] + Vector((0, -0.13 * s * spec["build"], -0.2 * s)), bevel=0.006), ("acc", col)))
        elif kind == "cane":
            hand = J["hand.R"]
            parts.append((cone_obj("cane", 0.014 * s, 0.014 * s, hand.z, Vector((hand.x, hand.y - 0.03, hand.z / 2)), seg=8),
                          ("acc", col)))
        elif kind == "towel":
            parts.append((box_obj("towel", (0.08 * s, 0.22 * s, 0.03 * s), J["shoulder.L"] + Vector((-0.02, 0, 0.04 * s)),
                                  bevel=0.01), ("acc", col)))
        elif kind == "headband":
            parts.append((cone_obj("band", 0.1 * s, 0.1 * s, 0.03 * s, H + Vector((0, 0.003, 0.045 * s)), seg=20, cap=False),
                          ("acc", col)))
        elif kind == "belly":
            pass
    return parts


# ----------------------------------------------------------------------------- rig
BONES = [  # name, head joint, tail joint, parent
    ("Hips", "pelvis", "waist", None), ("Spine", "waist", "chest", "Hips"), ("Chest", "chest", "upperchest", "Spine"),
    ("Neck", "upperchest", "headbase", "Chest"), ("Head", "headbase", "headtop", "Neck"),
]
for s in ("L", "R"):
    BONES += [("Shoulder." + s, "upperchest", "shoulder." + s, "Chest"),
              ("UpperArm." + s, "shoulder." + s, "elbow." + s, "Shoulder." + s),
              ("LowerArm." + s, "elbow." + s, "wrist." + s, "UpperArm." + s),
              ("Hand." + s, "wrist." + s, "hand." + s, "LowerArm." + s),
              ("UpperLeg." + s, "hip." + s, "knee." + s, "Hips"),
              ("LowerLeg." + s, "knee." + s, "ankle." + s, "UpperLeg." + s),
              ("Foot." + s, "ankle." + s, "toe." + s, "LowerLeg." + s)]


def build_rig(name, J):
    arm_data = bpy.data.armatures.new(name + "_Rig")
    arm = link_obj(name + "_Rig", arm_data)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    for bn, h, t, par in BONES:
        eb = arm_data.edit_bones.new(bn)
        eb.head, eb.tail = J[h], J[t]
        if par:
            eb.parent = arm_data.edit_bones[par]
    bpy.ops.object.mode_set(mode='OBJECT')
    return arm


def skin_weights(ob, J):
    children = {}
    for bn, h, t, par in BONES:
        if par:
            children.setdefault(par, []).append(bn)
    segs = {bn: (J[h], J[t], par) for bn, h, t, par in BONES}
    groups = {bn: ob.vertex_groups.new(name=bn) for bn, *_ in BONES}
    for v in ob.data.vertices:
        p = v.co
        best = None
        for bn, (a, b, par) in segs.items():
            if bn.startswith("Shoulder") and abs(p.x) < 0.12:
                continue                                     # keep the upper chest on Chest
            d, t = seg_closest(p, a, b)
            if best is None or d < best[0]:
                best = (d, bn, t)
        _, bn, t = best
        w = {bn: 1.0}
        par = segs[bn][2]
        if t < 0.25 and par:
            w[par] = (0.25 - t) / 0.5
        kids = [k for k in children.get(bn, []) if not k.startswith(("Shoulder", "UpperLeg")) or bn not in ("Chest", "Hips")]
        if t > 0.75 and kids:
            w[kids[0]] = (t - 0.75) / 0.5
        tot = sum(w.values())
        for k, val in w.items():
            groups[k].add([v.index], val / tot, 'REPLACE')


def key_pose(arm, frame, angles, hips_z=0.0):
    """angles: bone -> (deg about armature X, deg about armature Z)."""
    for pb in arm.pose.bones:
        rest = pb.bone.matrix_local.to_3x3()
        ax, az = angles.get(pb.name, (0.0, 0.0))
        rw = (Matrix.Rotation(R(az), 3, 'Z') @ Matrix.Rotation(R(ax), 3, 'X'))
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = (rest.inverted() @ rw @ rest).to_quaternion()
        pb.keyframe_insert("rotation_quaternion", frame=frame)
        if pb.name == "Hips":
            pb.location = rest.inverted() @ Vector((0, 0, hips_z))
            pb.keyframe_insert("location", frame=frame)


def animate(arm, spec):
    arm.animation_data_create()
    stride = spec.get("stride", 1.0)
    # Walk: 24 frames = one full cycle (two steps) at ~1.3 m/s
    walk = bpy.data.actions.new("Walk")
    walk.use_fake_user = True
    arm.animation_data.action = walk
    for f in range(0, 25, 2):
        th = 2 * math.pi * f / 24
        s, c = math.sin(th), math.cos(th)
        legL, legR = -24 * s * stride, 24 * s * stride
        kneeL, kneeR = 6 + 38 * max(0.0, c), 6 + 38 * max(0.0, -c)
        a = {
            "UpperLeg.L": (legL, 0), "UpperLeg.R": (legR, 0),
            "LowerLeg.L": (kneeL, 0), "LowerLeg.R": (kneeR, 0),
            "Foot.L": (-0.35 * kneeL + 6 * s, 0), "Foot.R": (-0.35 * kneeR - 6 * s, 0),
            "UpperArm.L": (17 * s, 0), "UpperArm.R": (-17 * s, 0),
            "LowerArm.L": (-10 - 12 * max(0.0, -s), 0), "LowerArm.R": (-10 - 12 * max(0.0, s), 0),
            "Hips": (0, 5 * s), "Chest": (-3, -6 * s), "Head": (2, 3 * s),
        }
        key_pose(arm, f, a, hips_z=0.018 * abs(math.cos(th)) * spec["height"] / BASE_H)
    # Idle: 48 frames of breathing + weight shift
    idle = bpy.data.actions.new("Idle")
    idle.use_fake_user = True
    arm.animation_data.action = idle
    for f in range(0, 49, 4):
        th = 2 * math.pi * f / 48
        s = math.sin(th)
        a = {"Chest": (-1.5 * s, 0), "Spine": (0, 1.5 * s), "Head": (1.0 * s, 4 * math.sin(th * 0.5)),
             "UpperArm.L": (2 * s, 0), "UpperArm.R": (2 * s, 0), "LowerArm.L": (-6, 0), "LowerArm.R": (-6, 0)}
        key_pose(arm, f, a)
    arm.animation_data.action = walk


# ----------------------------------------------------------------------------- bake
def bake_texture(ob, name):
    SC.render.engine = 'CYCLES'
    SC.cycles.device = 'CPU'
    SC.cycles.samples = 16
    SC.render.bake.margin = 4
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=R(60), island_margin=0.012)
    bpy.ops.object.mode_set(mode='OBJECT')

    imgs = {}
    for kind in ("col",):
        img = bpy.data.images.new(f"{name}_{kind}", 512, 512)
        imgs[kind] = img
        for m in ob.data.materials:
            nodes = m.node_tree.nodes
            n = nodes.get("BAKE") or nodes.new("ShaderNodeTexImage")
            n.name = "BAKE"
            n.image = img
            nodes.active = n
        if kind == "col":
            bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'}, margin=4, use_clear=True)
        else:
            bpy.ops.object.bake(type='AO', margin=4, use_clear=True)
    col = np.array(imgs["col"].pixels[:]).reshape(-1, 4)
    ao = None
    shade = 1.0   # AO disabled: self-intersections of the skin hull bake as black patches
    col[:, :3] = np.clip(col[:, :3] * shade, 0, 1)
    final = bpy.data.images.new(f"{name}_albedo", 512, 512)
    final.pixels[:] = col.ravel()
    final.filepath_raw = os.path.join(TEX, f"{name}_albedo.png")
    final.file_format = 'PNG'
    final.save()

    m = bpy.data.materials.new("M_" + name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Roughness"].default_value = 0.85
    t = m.node_tree.nodes.new("ShaderNodeTexImage")
    t.image = final
    m.node_tree.links.new(t.outputs["Color"], b.inputs["Base Color"])
    ob.data.materials.clear()
    ob.data.materials.append(m)
    for p in ob.data.polygons:
        p.material_index = 0


# ----------------------------------------------------------------------------- characters
SKIN_TH = (0.78, 0.56, 0.42)
SKIN_LIGHT = (0.9, 0.72, 0.6)
SKIN_FAIR = (0.95, 0.78, 0.68)
LIPS = (0.62, 0.3, 0.3)
CHARS = [
    dict(name="NPC_Student", sex="f", height=1.58, build=0.95, skin=SKIN_LIGHT, lips=(0.75, 0.38, 0.4),
         brow=(0.08, 0.06, 0.05), hair="bob", hair_color=(0.03, 0.025, 0.025),
         top=dict(color=(0.94, 0.94, 0.92), buttons=True), bottom=dict(type="skirt", color=(0.07, 0.09, 0.22), pleats=True),
         shoes=(0.04, 0.04, 0.04), socks=(0.93, 0.93, 0.93), extras=[("backpack", (0.12, 0.2, 0.42))]),
    dict(name="NPC_Office", sex="m", height=1.72, build=1.0, skin=SKIN_TH, lips=LIPS, brow=(0.05, 0.04, 0.03),
         hair="short", hair_color=(0.03, 0.025, 0.02),
         top=dict(color=(0.6, 0.75, 0.92), long=True, pattern="pinstripe", color2=(0.45, 0.6, 0.85), buttons=True),
         bottom=dict(type="pants", color=(0.22, 0.23, 0.27)), shoes=(0.05, 0.035, 0.03),
         extras=[("tie", (0.55, 0.06, 0.1)), ("lanyard", (0.95, 0.95, 0.95))]),
    dict(name="NPC_Tourist", sex="m", height=1.82, build=1.08, skin=SKIN_FAIR, lips=(0.75, 0.42, 0.4),
         brow=(0.55, 0.42, 0.25), hair="short", hair_color=(0.75, 0.6, 0.35),
         top=dict(color=(0.08, 0.5, 0.58), pattern="tropical", color2=(0.95, 0.55, 0.2), color3=(0.95, 0.9, 0.7),
                  untucked=True, vneck=True),
         bottom=dict(type="shorts", color=(0.7, 0.62, 0.45)), shoes=(0.92, 0.92, 0.9), socks=(0.9, 0.9, 0.9),
         extras=[("hat", (0.82, 0.74, 0.55)), ("camera", (0.06, 0.06, 0.07)), ("backpack", (0.25, 0.3, 0.22))]),
    dict(name="NPC_Auntie", sex="f", height=1.54, build=1.14, belly=0.025, skin=SKIN_TH, lips=(0.62, 0.25, 0.3),
         brow=(0.25, 0.22, 0.2), hair="bun", hair_color=(0.42, 0.4, 0.4),
         top=dict(color=(0.55, 0.28, 0.6), pattern="floral", color2=(0.95, 0.75, 0.85), color3=(0.95, 0.85, 0.4),
                  untucked=True),
         bottom=dict(type="sarong", color=(0.35, 0.17, 0.1), pattern="batik", color2=(0.75, 0.55, 0.2)),
         shoes=(0.75, 0.1, 0.1), sandals=True, extras=[("bag", (0.8, 0.12, 0.15))], stride=0.75),
    dict(name="NPC_Rider", sex="m", height=1.7, build=1.03, skin=SKIN_TH, lips=LIPS, brow=(0.04, 0.03, 0.03),
         hair="helmet", hair_color=(0.03, 0.03, 0.03),
         top=dict(color=(0.95, 0.42, 0.07), long=True, pattern="reflective", untucked=True),
         bottom=dict(type="pants", color=(0.17, 0.26, 0.45), denim=True), shoes=(0.12, 0.12, 0.13),
         extras=[("deliverybox", (0.95, 0.42, 0.07))]),
    dict(name="NPC_Influencer", sex="f", height=1.64, build=0.94, skin=SKIN_LIGHT, lips=(0.8, 0.3, 0.4),
         brow=(0.1, 0.07, 0.06), hair="long", hair_color=(0.12, 0.07, 0.05),
         top=dict(color=(0.95, 0.5, 0.68), vneck=True), bottom=dict(type="pants", color=(0.95, 0.94, 0.92)),
         shoes=(0.95, 0.95, 0.95), extras=[("sunglasses", (0.04, 0.04, 0.05)), ("phone", (0.1, 0.1, 0.12))]),
    dict(name="NPC_SchoolBoy", sex="m", height=1.6, build=0.93, skin=SKIN_TH, lips=LIPS, brow=(0.05, 0.04, 0.03),
         hair="buzz", hair_color=(0.03, 0.025, 0.02),
         top=dict(color=(0.94, 0.94, 0.92), buttons=True), bottom=dict(type="shorts", color=(0.55, 0.45, 0.3)),
         shoes=(0.05, 0.05, 0.05), socks=(0.55, 0.45, 0.3), extras=[("backpack", (0.15, 0.15, 0.17))]),
    dict(name="NPC_UniStudent", sex="f", height=1.62, build=0.93, skin=SKIN_LIGHT, lips=(0.78, 0.35, 0.4),
         brow=(0.08, 0.06, 0.05), hair="ponytail", hair_color=(0.05, 0.035, 0.03),
         top=dict(color=(0.96, 0.96, 0.95), buttons=True), bottom=dict(type="skirt", color=(0.05, 0.05, 0.06)),
         shoes=(0.04, 0.04, 0.04), extras=[("bag", (0.35, 0.22, 0.15))]),
    dict(name="NPC_Worker", sex="m", height=1.68, build=1.05, skin=(0.62, 0.42, 0.3), lips=LIPS,
         brow=(0.05, 0.04, 0.03), hair="short", hair_color=(0.03, 0.025, 0.02),
         top=dict(color=(0.3, 0.38, 0.5), long=True, untucked=True), bottom=dict(type="pants", color=(0.3, 0.27, 0.22)),
         shoes=(0.12, 0.1, 0.08), extras=[("buckethat", (0.45, 0.42, 0.3)), ("towel", (0.9, 0.9, 0.85))]),
    dict(name="NPC_Vendor", sex="f", height=1.56, build=1.1, belly=0.015, skin=SKIN_TH, lips=(0.65, 0.28, 0.32),
         brow=(0.1, 0.08, 0.06), hair="ponytail", hair_color=(0.04, 0.03, 0.03),
         top=dict(color=(0.95, 0.85, 0.3), untucked=True), bottom=dict(type="pants", color=(0.2, 0.2, 0.35)),
         shoes=(0.85, 0.2, 0.3), sandals=True, extras=[("apron", (0.85, 0.2, 0.25)), ("visor", (0.95, 0.95, 0.95))],
         stride=0.8),
    dict(name="NPC_Jogger", sex="m", height=1.75, build=0.98, skin=SKIN_TH, lips=LIPS, brow=(0.05, 0.04, 0.03),
         hair="buzz", hair_color=(0.03, 0.025, 0.02),
         top=dict(color=(0.1, 0.12, 0.15), pattern="reflective", untucked=True),
         bottom=dict(type="shorts", color=(0.05, 0.05, 0.06)), shoes=(0.95, 0.35, 0.1), socks=(0.95, 0.95, 0.95),
         extras=[("headband", (0.95, 0.35, 0.1))], stride=1.15),
    dict(name="NPC_Grandpa", sex="m", height=1.62, build=0.95, skin=(0.7, 0.5, 0.38), lips=(0.55, 0.3, 0.28),
         brow=(0.8, 0.8, 0.78), hair="buzz", hair_color=(0.85, 0.85, 0.84),
         top=dict(color=(0.95, 0.95, 0.92), vneck=True, untucked=True),
         bottom=dict(type="pants", color=(0.25, 0.2, 0.35)), shoes=(0.3, 0.25, 0.2), sandals=True,
         extras=[("cane", (0.4, 0.26, 0.14))], stride=0.65),
    dict(name="NPC_Uncle", sex="m", height=1.66, build=1.12, belly=0.035, skin=SKIN_TH, lips=LIPS,
         brow=(0.35, 0.33, 0.32), hair="short", hair_color=(0.5, 0.5, 0.5),
         top=dict(color=(0.13, 0.42, 0.28), untucked=True), bottom=dict(type="shorts", color=(0.62, 0.55, 0.42)),
         shoes=(0.1, 0.3, 0.75), sandals=True, stride=0.85),
]

built = []
for spec in CHARS:
    name = spec["name"]
    J = joints_for(spec)
    body = build_body(spec, J)
    mats = {k: make_material(f"{name}_{k}", k, spec, J) for k in ("body", "hair", "bottom")}
    body.data.materials.append(mats["body"])
    extras = hair_and_extras(spec, J)
    head = make_head(spec, J)
    head.data.materials.append(mats["body"])
    objs = [body, head]
    for ob, mk in extras:
        if isinstance(mk, tuple):
            m = make_material(f"{name}_{ob.name}", mk, spec, J)
        else:
            m = mats[mk]
        ob.data.materials.append(m)
        objs.append(ob)
    with bpy.context.temp_override(active_object=body, selected_editable_objects=objs, object=body):
        bpy.ops.object.join()
    # consistent outward normals (the skin modifier flips some branch faces -> black AO patches)
    bm = bmesh.new()
    bm.from_mesh(body.data)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(body.data)
    bm.free()
    body.name = name
    body.data.name = name
    # remove unused material slots to keep the bake small
    bake_texture(body, name)

    arm = build_rig(name, J)
    skin_weights(body, J)
    body.parent = arm
    mod = body.modifiers.new("Armature", 'ARMATURE')
    mod.object = arm
    animate(arm, spec)
    built.append((arm, body, spec))
    tri = sum(len(p.vertices) - 2 for p in body.data.polygons)
    print(f"BUILT {name}: {tri} tris")

# ----------------------------------------------------------------------------- export
for arm, body, spec in built:
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    arm.select_set(True)
    body.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(OUT, spec["name"] + ".fbx"), use_selection=True, object_types={'ARMATURE', 'MESH'},
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
        add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_simplify_factor=0.0, mesh_smooth_type='OFF',
        path_mode='RELATIVE')
print("EXPORTED", len(built), "characters to", OUT)

# ----------------------------------------------------------------------------- preview render
for i, (arm, body, spec) in enumerate(built):
    arm.location = ((i - (len(built) - 1) / 2) * 0.62, 0, 0)
SC.frame_set(5)
ground = bpy.data.meshes.new("ground")
ground.from_pydata([(-8, -6, 0), (8, -6, 0), (8, 6, 0), (-8, 6, 0)], [], [(0, 1, 2, 3)])
link_obj("ground", ground)
world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.7, 0.75, 0.82, 1)
sun = link_obj("sun", bpy.data.lights.new("sun", 'SUN'))
sun.data.energy = 3.5
sun.rotation_euler = (R(50), 0, R(-30))
cam = link_obj("cam", bpy.data.cameras.new("cam"))
cam.data.lens = 32
cam.location = (0, -9.0, 1.2)
cam.rotation_euler = (R(88), 0, 0)
SC.camera = cam
for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
    try:
        SC.render.engine = eng
        break
    except TypeError:
        pass
SC.render.resolution_x, SC.render.resolution_y = 2000, 700
SC.view_settings.view_transform = 'Standard'
SC.render.filepath = os.path.join(REN, "npc_ps2_lineup.png")
bpy.ops.render.render(write_still=True)
# close-up of faces
cam.location = (built[0][0].location.x + 0.75 * 1, -1.6, 1.55)
cam.data.lens = 60
cam.rotation_euler = (R(88), 0, 0)
SC.frame_set(0)
for arm, body, spec in built:
    arm.animation_data.action = bpy.data.actions["Idle"]
SC.render.resolution_x, SC.render.resolution_y = 1200, 700
SC.render.filepath = os.path.join(REN, "npc_ps2_closeup.png")
bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "BlenderSource", "ThaiNPC_PS2.blend"))
print("DONE")

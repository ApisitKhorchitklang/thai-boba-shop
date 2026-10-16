"""
Thai NPCs with realistic proportions: ~7k tris, one baked 1024 px texture each, a real skeleton with Walk + Idle,
plus seated (posed, static) variants for vehicle drivers and passengers.

Run:
    "D:\\Bder\\blender.exe" -b --factory-startup -P BlenderSource/thai_npc.py [-- NPC_Name ... noexport]

Outputs
    Assets/Models/ThaiBobaShop/Characters/NPC_<Name>.fbx      rigged + animated (takes "Walk", "Idle")
    Assets/Models/ThaiBobaShop/Characters/SEAT_<Name>.fbx     seated pose, static (vehicle seats)
    Assets/Models/ThaiBobaShop/Characters/Textures/*.png      baked albedo (colour x soft AO)
    BlenderSource/ThaiNPC.blend, BlenderSource/renders/npc_*.png

Method
    Body: a joint graph with mid-limb nodes (deltoids, biceps, forearms, thighs, calves) -> Skin modifier +
    Subdivision. Clothes are the body's own faces split along the hem lines and pushed out by the cloth
    thickness; the split is bridged so every hem, sleeve and trouser cuff has a real edge. Collars, untucked
    shirt tails, pleated skirts and sarongs are added geometry.
    Head: a sculpted sphere (skull, jaw, chin, cheekbones, brow, eye sockets, nose, lips) with real eyeballs
    and ears; hair is a shell over the skull (with bangs / curtains / ponytail / bun per style).
    Hands have fingers; shoes, flip-flops and sneakers are lofted.
    Procedural fabric / skin / hair shaders are baked (Cycles) into one texture, weights come from the same
    skeleton, the gait is keyed in pose mode (pelvis bob and twist, heel-toe roll, counter-swinging arms).
Front faces -Y in Blender (= +Z forward in Unity).
"""
import bpy, bmesh, math, os, random, sys
import numpy as np
from collections import defaultdict
from mathutils import Vector, Matrix, Euler, Quaternion

ROOT = r"D:\Project-Start\A4"
OUT = os.path.join(ROOT, "Assets", "Models", "ThaiBobaShop", "Characters")
TEX = os.path.join(OUT, "Textures")
REN = os.path.join(ROOT, "BlenderSource", "renders")
for d in (OUT, TEX, REN):
    os.makedirs(d, exist_ok=True)
ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
EXPORT = "noexport" not in ARGS
ONLY = [a for a in ARGS if a.startswith("NPC_")]
TEX_SIZE = 1024

R = math.radians
bpy.ops.wm.read_factory_settings(use_empty=True)
SC = bpy.context.scene
SC.render.fps = 24
world = bpy.data.worlds.new("W")
SC.world = world
world.light_settings.distance = 0.04            # AO bake distance (creases only)

# ----------------------------------------------------------------------------- skeleton
BASE_H = 1.68
JOINTS = {
    "pelvis": (0, 0, 0.95), "waist": (0, 0.004, 1.07), "chest": (0, 0, 1.25), "upperchest": (0, 0.008, 1.385),
    "neck": (0, 0.012, 1.475), "headbase": (0, 0.008, 1.548), "head": (0, -0.005, 1.627), "headtop": (0, 0.0, 1.725),
    "shoulder.L": (0.172, 0.012, 1.4), "elbow.L": (0.205, 0.035, 1.125), "wrist.L": (0.222, 0.008, 0.885),
    "hand.L": (0.226, -0.004, 0.795),
    "hip.L": (0.09, 0.0, 0.91), "knee.L": (0.098, -0.008, 0.5),
    "ankle.L": (0.1, 0.03, 0.085), "toe.L": (0.1, -0.115, 0.025),
}
for k in list(JOINTS):
    if k.endswith(".L"):
        x, y, z = JOINTS[k]
        JOINTS[k[:-2] + ".R"] = (-x, y, z)


def joints_for(spec):
    s = spec["height"] / BASE_H
    w = spec["build"]
    out = {}
    for k, (x, y, z) in JOINTS.items():
        lat = w if abs(x) > 0.05 else 1.0
        out[k] = Vector((x * s * lat, y * s, z * s))
    return out


def lerpv(a, b, t):
    return a + (b - a) * t


def seg_closest(p, a, b):
    ab = b - a
    t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9)))
    return (a + ab * t - p).length, t


def link_obj(name, me):
    ob = bpy.data.objects.new(name, me)
    SC.collection.objects.link(ob)
    return ob


# ----------------------------------------------------------------------------- body graph
def skin_graph(spec, J):
    """Nodes name -> (pos, rx, ry) and edges for the Skin modifier; mid-limb nodes shape the muscles."""
    s = spec["height"] / BASE_H
    w = spec["build"]
    f = 1.0 if spec["sex"] == "f" else 0.0
    belly = spec.get("belly", 0.0)
    N, E = {}, []

    def node(n, p, rx, ry):
        N[n] = (Vector(p), rx, ry)
    node("pelvis", J["pelvis"] + Vector((0, 0.01, -0.025)) * s, (0.148 + 0.02 * f) * w * s, (0.102 + 0.012 * f) * w * s)
    node("waist", J["waist"], (0.122 - 0.018 * f + belly) * w * s, (0.088 - 0.004 * f + belly * 1.3) * w * s)
    node("ribs", lerpv(J["waist"], J["chest"], 0.55) + Vector((0, -0.004 - belly * 0.4, 0)) * s,
         (0.14 - 0.016 * f + belly * 0.5) * w * s, (0.097 + belly * 0.8) * w * s)
    node("chest", J["chest"] + Vector((0, -0.008 - 0.01 * f, 0)) * s, (0.152 - 0.018 * f) * w * s, (0.1 + 0.02 * f) * w * s)
    node("upperchest", J["upperchest"], (0.162 - 0.02 * f) * w * s, 0.086 * w * s)
    node("neck", J["neck"], 0.056 * s * (1 - 0.12 * f), 0.058 * s * (1 - 0.08 * f))
    node("headbase", J["headbase"], 0.045 * s, 0.05 * s)
    E += [("pelvis", "waist"), ("waist", "ribs"), ("ribs", "chest"), ("chest", "upperchest"), ("upperchest", "neck"), ("neck", "headbase")]
    for sd in ("L", "R"):
        S = lambda n: n + "." + sd
        node(S("clav"), lerpv(J["upperchest"], J[S("shoulder")], 0.5) + Vector((0, 0.006, 0.02)) * s, 0.054 * s, 0.054 * s)
        node(S("shoulder"), J[S("shoulder")], (0.056 - 0.007 * f) * w * s, (0.058 - 0.006 * f) * w * s)
        node(S("arm1"), lerpv(J[S("shoulder")], J[S("elbow")], 0.3), (0.049 - 0.007 * f) * w * s, (0.051 - 0.007 * f) * w * s)
        node(S("arm2"), lerpv(J[S("shoulder")], J[S("elbow")], 0.62), (0.042 - 0.006 * f) * w * s, (0.046 - 0.006 * f) * w * s)
        node(S("elbow"), J[S("elbow")], 0.035 * s, 0.038 * s)
        node(S("fore1"), lerpv(J[S("elbow")], J[S("wrist")], 0.25), (0.038 - 0.005 * f) * s, (0.034 - 0.004 * f) * s)
        node(S("fore2"), lerpv(J[S("elbow")], J[S("wrist")], 0.65), (0.03 - 0.003 * f) * s, (0.025 - 0.002 * f) * s)
        node(S("wrist"), J[S("wrist")], 0.025 * s * (1 - 0.1 * f), 0.018 * s)
        E += [("upperchest", S("clav")), (S("clav"), S("shoulder")), (S("shoulder"), S("arm1")), (S("arm1"), S("arm2")),
              (S("arm2"), S("elbow")), (S("elbow"), S("fore1")), (S("fore1"), S("fore2")), (S("fore2"), S("wrist"))]
        lw = w ** 0.5
        node(S("hip"), J[S("hip")], (0.083 + 0.008 * f) * w * s, 0.09 * w * s)
        node(S("thigh1"), lerpv(J[S("hip")], J[S("knee")], 0.3), (0.073 + 0.005 * f) * w * s, 0.077 * w * s)
        node(S("thigh2"), lerpv(J[S("hip")], J[S("knee")], 0.68), 0.057 * w * s, 0.06 * w * s)
        node(S("knee"), J[S("knee")], 0.045 * s * lw, 0.049 * s * lw)
        node(S("calf1"), lerpv(J[S("knee")], J[S("ankle")], 0.22) + Vector((0, 0.012, 0)) * s, 0.049 * s * lw, 0.054 * s * lw)
        node(S("calf2"), lerpv(J[S("knee")], J[S("ankle")], 0.45) + Vector((0, 0.012, 0)) * s, 0.043 * s * lw, 0.047 * s * lw)
        node(S("shin"), lerpv(J[S("knee")], J[S("ankle")], 0.75), 0.031 * s, 0.033 * s)
        node(S("ankle"), J[S("ankle")], 0.028 * s, 0.03 * s)
        E += [("pelvis", S("hip")), (S("hip"), S("thigh1")), (S("thigh1"), S("thigh2")), (S("thigh2"), S("knee")),
              (S("knee"), S("calf1")), (S("calf1"), S("calf2")), (S("calf2"), S("shin")), (S("shin"), S("ankle"))]
    return N, E


def build_body(spec, J):
    N, E = skin_graph(spec, J)
    names = list(N)
    me = bpy.data.meshes.new("skel")
    me.from_pydata([N[n][0] for n in names], [(names.index(a), names.index(b)) for a, b in E], [])
    ob = link_obj("skel", me)
    sk = ob.modifiers.new("Skin", 'SKIN')
    sk.use_smooth_shade = True
    sk.branch_smoothing = 0.35
    for i, n in enumerate(names):
        v = me.skin_vertices[0].data[i]
        v.radius = (N[n][1], N[n][2])
        v.use_root = (n == "pelvis")
    sub = ob.modifiers.new("Sub", 'SUBSURF')
    sub.levels = 1
    dg = bpy.context.evaluated_depsgraph_get()
    body_me = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    bpy.data.objects.remove(ob)
    return link_obj(spec["name"], body_me), N, E


# ----------------------------------------------------------------------------- clothes on the body
def edge_part(a, b):
    """Name of a body part for a graph edge (used by the clothing rules)."""
    key = (a.split(".")[0], b.split(".")[0])
    return {
        ("pelvis", "waist"): "torso", ("waist", "ribs"): "torso", ("ribs", "chest"): "torso", ("chest", "upperchest"): "torso",
        ("upperchest", "neck"): "neck", ("neck", "headbase"): "neck", ("upperchest", "clav"): "torso", ("clav", "shoulder"): "shoulder",
        ("shoulder", "arm1"): "ua1", ("arm1", "arm2"): "ua2", ("arm2", "elbow"): "ua3",
        ("elbow", "fore1"): "fa1", ("fore1", "fore2"): "fa2", ("fore2", "wrist"): "fa3",
        ("pelvis", "hip"): "hip", ("hip", "thigh1"): "th1", ("thigh1", "thigh2"): "th2", ("thigh2", "knee"): "th3",
        ("knee", "calf1"): "sh1", ("calf1", "calf2"): "sh2", ("calf2", "shin"): "sh3", ("shin", "ankle"): "sh4",
    }[key]


SLEEVES = {"none": set(), "short": {"ua1", "ua2"}, "elbow": {"ua1", "ua2", "ua3"},
           "long": {"ua1", "ua2", "ua3", "fa1", "fa2", "fa3"}}
LEGS = {"pants": {"hip", "th1", "th2", "th3", "sh1", "sh2", "sh3", "sh4"}, "shorts": {"hip", "th1", "th2"},
        "knee": {"hip", "th1", "th2", "th3"}, "skirt": {"hip"}, "sarong": {"hip"}}
REG_SKIN, REG_TOP, REG_BOTTOM, REG_HEM = 0, 1, 2, 3


def classify(spec, N, E, c):
    """Region of a body face centred at c."""
    best = None
    for a, b in E:
        d, t = seg_closest(c, N[a][0], N[b][0])
        ra = (N[a][1] + N[a][2]) / 2
        rb = (N[b][1] + N[b][2]) / 2
        d -= ra + (rb - ra) * t                      # distance to that limb's surface, not its bone
        if best is None or d < best[0]:
            best = (d, a, b, t)
    _, a, b, t = best
    part = edge_part(a, b)
    top, bot = spec["top"], spec["bottom"]
    s = spec["height"] / BASE_H
    hem_z = (N["pelvis"][0].z + 0.02 * s) if top.get("untucked") else N["waist"][0].z
    if part == "neck":
        return REG_TOP if top.get("turtle") else REG_SKIN
    if part == "torso":
        if c.z < hem_z:
            return REG_BOTTOM
        if top.get("vneck"):
            zt = N["upperchest"][0].z + 0.03 * s
            depth = 0.075 * s
            if c.y < 0 and c.z > zt - depth and abs(c.x) < 0.05 * s * (c.z - (zt - depth)) / depth:
                return REG_SKIN
        if c.z > N["upperchest"][0].z + 0.035 * s and abs(c.x) < 0.075 * s:
            return REG_SKIN                                    # neckline
        return REG_TOP
    if part == "shoulder":
        return REG_SKIN if top.get("sleeveless") else REG_TOP
    if part == "hip" and c.z > hem_z:
        return REG_TOP
    if part in SLEEVES[top.get("sleeve", "short")]:
        return REG_TOP
    if part.startswith(("ua", "fa")):
        return REG_SKIN
    if part in LEGS[bot["type"]]:
        return REG_BOTTOM
    return REG_SKIN


def dress(body, spec, N, E, mats):
    """Split the body along hem lines, push clothes out by their thickness and bridge the cut (real hems)."""
    s = spec["height"] / BASE_H
    top, bot = spec["top"], spec["bottom"]
    thick = {REG_SKIN: 0.0, REG_TOP: (0.014 if top.get("loose") else 0.009) * s,
             REG_BOTTOM: (0.013 if bot.get("loose") else 0.008) * s}
    bm = bmesh.new()
    bm.from_mesh(body.data)
    bm.normal_update()
    reg = bm.faces.layers.int.new("reg")
    nx, ny, nz = (bm.verts.layers.float.new(k) for k in ("nx", "ny", "nz"))
    for v in bm.verts:
        v[nx], v[ny], v[nz] = v.normal
    for f in bm.faces:
        f[reg] = classify(spec, N, E, f.calc_center_median())
    # tidy single islands (one face of another region inside a garment)
    for _ in range(2):
        for f in bm.faces:
            nb = [g[reg] for e in f.edges for g in e.link_faces if g is not f]
            if nb and all(r != f[reg] for r in nb) and nb.count(max(set(nb), key=nb.count)) >= 3:
                f[reg] = max(set(nb), key=nb.count)
    bnd = [e for e in bm.edges if len(e.link_faces) == 2 and e.link_faces[0][reg] != e.link_faces[1][reg]]
    bmesh.ops.split_edges(bm, edges=bnd)
    buckets = defaultdict(list)
    key = lambda co: (round(co.x, 5), round(co.y, 5), round(co.z, 5))
    for e in bm.edges:
        if len(e.link_faces) == 1:
            buckets[tuple(sorted((key(e.verts[0].co), key(e.verts[1].co))))].append(e)
    bridges = []
    flap_edges = []
    for es in buckets.values():
        if len(es) != 2:
            continue
        e1, e2 = es
        r1, r2 = e1.link_faces[0][reg], e2.link_faces[0][reg]
        if thick[r1] < thick[r2]:
            e1, e2, r1, r2 = e2, e1, r2, r1
        a1, a2 = e1.verts
        b1 = e2.verts[0] if (e2.verts[0].co - a1.co).length < 1e-5 else e2.verts[1]
        b2 = e2.verts[1] if b1 is e2.verts[0] else e2.verts[0]
        try:
            fb = bm.faces.new((a1, a2, b2, b1))
        except ValueError:
            continue
        fb[reg] = REG_HEM
        fb.material_index = mats.index("top" if r1 == REG_TOP else "bottom" if r1 == REG_BOTTOM else "skin")
        bridges.append(fb)
        pass
    # push every garment out along the original normal (+ a little fabric noise)
    rnd = random.Random(hash(spec["name"]) & 0xffff)
    for v in bm.verts:
        regs = [f[reg] for f in v.link_faces if f[reg] != REG_HEM]
        if not regs:
            continue
        t = max(thick[r] for r in regs)
        if t > 0:
            n = Vector((v[nx], v[ny], v[nz]))
            wob = 0.0025 * s * math.sin(v.co.x * 61 + v.co.z * 47) * math.cos(v.co.y * 53 + v.co.z * 29)
            v.co += n * (t + wob)
    for f in bm.faces:
        if f[reg] != REG_HEM:
            f.material_index = mats.index({REG_SKIN: "skin", REG_TOP: "top", REG_BOTTOM: "bottom"}[f[reg]])
    # untucked shirt tails hang over the waistband
    if flap_edges:
        made = {}
        flap = 0.035 * s

        def low(v):
            if v not in made:
                n = Vector((v[nx], v[ny], 0)).normalized()
                made[v] = bm.verts.new(v.co + Vector((0, 0, -flap)) + n * 0.005 * s)
            return made[v]
        for e in flap_edges:
            v1, v2 = e.verts
            try:
                ff = bm.faces.new((v1, v2, low(v2), low(v1)))
                ff[reg] = REG_HEM
                ff.material_index = mats.index("top")
            except ValueError:
                pass
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    # collar: a stand + a fall around the neckline of collared shirts
    if top.get("collar"):
        neck_z = N["upperchest"][0].z
        cedges = [e for e in bm.edges if len(e.link_faces) == 2 and e.verts[0].co.z > neck_z
                  and {f[reg] for f in e.link_faces} == {REG_TOP, REG_HEM}]
        ups, falls = {}, {}

        def outn(v):
            c = Vector((0, N["neck"][0].y, 0))
            d = Vector((v.co.x, v.co.y, 0)) - c
            return d.normalized() if d.length > 1e-6 else Vector((0, -1, 0))

        def up(v):
            if v not in ups:
                ups[v] = bm.verts.new(v.co + Vector((0, 0, 0.026 * s)) - outn(v) * 0.004 * s)
            return ups[v]

        def fall(v):
            if v not in falls:
                u = up(v)
                falls[v] = bm.verts.new(u.co + outn(v) * 0.03 * s - Vector((0, 0, 0.034 * s)))
            return falls[v]
        made_f = []
        for e in cedges:
            v1, v2 = e.verts
            if v1.co.y < 0 and v2.co.y < 0 and abs(v1.co.x) < 0.016 * s and abs(v2.co.x) < 0.016 * s:
                continue                                      # open at the throat
            for quad in ((v1, v2, up(v2), up(v1)), (up(v1), up(v2), fall(v2), fall(v1))):
                try:
                    fc = bm.faces.new(quad)
                    fc.material_index = mats.index("top")
                    fc[reg] = REG_HEM
                    made_f.append(fc)
                except ValueError:
                    pass
        for fc in made_f:
            fc.normal_update()
            c = fc.calc_center_median()
            d = Vector((c.x, c.y - N["neck"][0].y, 0))
            if fc.normal.dot(d) < 0:
                fc.normal_flip()
    for f in bm.faces:
        f.smooth = True
    bm.to_mesh(body.data)
    bm.free()


# ----------------------------------------------------------------------------- head, eyes, ears
def spline(x, pts):
    """Catmull-Rom through (x, y) points sorted by x (clamped at the ends)."""
    if x <= pts[0][0]:
        return pts[0][1]
    if x >= pts[-1][0]:
        return pts[-1][1]
    for i in range(len(pts) - 1):
        x1, y1 = pts[i]
        x2, y2 = pts[i + 1]
        if x1 <= x <= x2:
            x0, y0 = pts[max(i - 1, 0)]
            x3, y3 = pts[min(i + 2, len(pts) - 1)]
            t = (x - x1) / (x2 - x1)
            m1 = (y2 - y0) / (x2 - x0) * (x2 - x1) if x2 != x0 else 0.0
            m2 = (y3 - y1) / (x3 - x1) * (x2 - x1) if x3 != x1 else 0.0
            t2, t3 = t * t, t * t * t
            return (2 * t3 - 3 * t2 + 1) * y1 + (t3 - 2 * t2 + t) * m1 + (-2 * t3 + 3 * t2) * y2 + (t3 - t2) * m2
    return pts[-1][1]


# head profiles (metres, male, scale 1), z relative to the head joint: half width, front depth, back depth
HEAD_W = [(-0.113, 0.0), (-0.109, 0.024), (-0.1, 0.038), (-0.088, 0.05), (-0.072, 0.0605), (-0.055, 0.066), (-0.035, 0.07),
          (-0.012, 0.0725), (0.012, 0.0735), (0.04, 0.0735), (0.06, 0.07), (0.08, 0.06), (0.096, 0.045), (0.106, 0.028), (0.112, 0.0)]
HEAD_DF = [(-0.113, 0.0), (-0.109, 0.045), (-0.1, 0.07), (-0.088, 0.08), (-0.07, 0.083), (-0.05, 0.085), (-0.03, 0.084),
           (-0.01, 0.082), (0.012, 0.08), (0.03, 0.086), (0.045, 0.087), (0.065, 0.08), (0.085, 0.065), (0.1, 0.045), (0.112, 0.0)]
HEAD_DB = [(-0.113, 0.0), (-0.1, 0.0), (-0.085, 0.012), (-0.065, 0.045), (-0.045, 0.08), (-0.02, 0.098), (0.01, 0.104),
           (0.04, 0.102), (0.07, 0.088), (0.095, 0.06), (0.112, 0.0)]


def _G(u, z, cu, cz, su, sz):
    return math.exp(-(((u - cu) / su) ** 2 + ((z - cz) / sz) ** 2))


def face_offset(u, z, spec, almond=True):
    """Forward (-Y) relief of the face at lateral u, height z (metres, scale 1), and lateral bulge."""
    f = spec["sex"] == "f"
    age = spec.get("age", 0.3)
    nz = 0.86 if f else 1.0
    dy = 0.0
    ridge = spline(z, [(-0.045, 0.0), (-0.039, 0.006), (-0.034, 0.0145), (-0.028, 0.0195), (-0.021, 0.019), (-0.008, 0.012),
                       (0.006, 0.0065), (0.02, 0.0035), (0.032, 0.0015), (0.04, 0.0)]) * nz
    width = spline(z, [(-0.045, 0.012), (-0.033, 0.0145), (-0.02, 0.0112), (0.0, 0.0085), (0.03, 0.0085)]) * (0.92 if f else 1.0)
    dy += ridge * math.exp(-(u / width) ** 2)
    for sg in (-1, 1):
        dy += 0.007 * nz * _G(u, z, sg * 0.0135, -0.031, 0.0065, 0.0065)              # nostril wings
        dy -= 0.0075 * _G(u, z, sg * 0.032, 0.0135, 0.019, 0.013)                    # eye sockets
        dy += (0.0045 - 0.002 * age) * _G(u, z, sg * 0.047, -0.012, 0.02, 0.015)     # cheekbones
        dy -= 0.0016 * _G(u, z, sg * 0.024, -0.054, 0.004, 0.006)                    # mouth corners
        dy -= (0.0012 + 0.0015 * age) * _G(u, z, sg * 0.023, -0.042, 0.004, 0.009)   # nasolabial folds
    dy += (0.002 if f else 0.0042) * _G(u, z, 0, 0.034, 0.05, 0.007)                 # brow ridge
    lip = 1.15 if f else 1.0
    dy += 0.0058 * lip * _G(u, z, 0, -0.047, 0.022, 0.0055)                          # upper lip
    dy += 0.0052 * lip * _G(u, z, 0, -0.0605, 0.019, 0.0058)                         # lower lip
    dy -= 0.0026 * _G(u, z, 0, -0.0545, 0.024, 0.0016)                               # mouth line
    dy -= 0.0012 * _G(u, z, 0, -0.04, 0.0035, 0.004)                                 # philtrum
    dy += (0.0035 if f else 0.0052) * _G(u, z, 0, -0.088, 0.02, 0.011)               # chin
    if almond:
        for sg in (-1, 1):
            ex, ez = sg * 0.032, 0.0135
            du = (u - ex) / 0.0142
            dz = (z - ez - 0.0018 * du * sg) / (0.0045 * (1 - 0.3 * du * du))
            d = du * du + dz * dz
            if d < 1.6:
                dy -= 0.0072 * max(0.0, min(1.0, (1.6 - d) / 0.7))                     # eye opening
    return dy


def head_point(theta, z, spec, almond=True):
    """Surface point (scale 1, relative to the head joint) at azimuth theta (0 = front) and height z."""
    f = spec["sex"] == "f"
    W = spline(z, HEAD_W) * (0.955 if f else 1.0)
    if f and z < -0.05:
        W *= 1 - 0.07 * min(1.0, (-0.05 - z) / 0.04)
    Df = spline(z, HEAD_DF)
    Db = spline(z, HEAD_DB)
    st, ct = math.sin(theta), math.cos(theta)
    n = 2.5 if ct > 0 else 2.1
    x = math.copysign(abs(st) ** (2 / n), st) * W
    y = -math.copysign(abs(ct) ** (2 / n), ct) * (Df if ct > 0 else Db)
    if ct > 0.15:
        wgt = min(1.0, (ct - 0.15) / 0.35)
        dy = face_offset(x, z, spec, almond)
        y -= dy * wgt
        x *= 1 + 0.06 * (1 - spec.get("age", 0.3)) * _G(abs(x), z, 0.05, -0.012, 0.02, 0.015) * wgt
    return x, y, z


def make_head(spec, J):
    """Head lofted from width / depth profiles with the face relief on the front (nose, lips, chin, brow,
    cheekbones) and almond eye openings for the eyeballs. Denser where the features are."""
    s = spec["height"] / BASE_H
    front = [0, 3, 6, 9, 13, 17, 22, 28, 34, 41, 49, 57]
    th = [R(a) for a in front + list(range(70, 291, 22)) + [360 - a for a in reversed(front[1:])]]
    zs = [0.106, 0.096, 0.083, 0.068, 0.054, 0.044] + [0.037 - 0.0047 * k for k in range(23)] + [-0.077, -0.087, -0.097, -0.105, -0.11]
    verts, faces = [], []
    n = len(th)
    for z in zs:
        for t in th:
            x, y, zz = head_point(t, z, spec)
            verts.append(J["head"] + Vector((x, y, zz)) * s)
    top = len(verts)
    verts.append(J["head"] + Vector((0, -0.006, 0.1125)) * s)
    bot = len(verts)
    verts.append(J["head"] + Vector((0, -0.03, -0.1125)) * s)
    for i in range(len(zs) - 1):
        for j in range(n):
            j2 = (j + 1) % n
            faces.append((i * n + j, i * n + j2, (i + 1) * n + j2, (i + 1) * n + j))
    for j in range(n):
        faces.append((top, (j + 1) % n, j))
        last = (len(zs) - 1) * n
        faces.append((bot, last + j, last + (j + 1) % n))
    me = bpy.data.meshes.new("head")
    me.from_pydata(verts, [], faces)
    me.validate()
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    return link_obj("head", me)


def front_y(spec, J, x, z, almond=False):
    """World y of the face surface at world (x, z) (front side), via the head profiles."""
    s = spec["height"] / BASE_H
    H = J["head"]
    u, zz = (x - H.x) / s, (z - H.z) / s
    best = None
    for k in range(91):
        t = R(-60 + 120 * k / 90)
        px, py, _ = head_point(t, zz, spec, almond)
        if best is None or abs(px - u) < best[0]:
            best = (abs(px - u), py)
    return H.y + best[1] * s


def surface_y(ob, x, z, y0=-1.0):
    """Front-most y of mesh ob along the ray from y0 toward +Y at (x, z)."""
    from mathutils.bvhtree import BVHTree
    bvh = BVHTree.FromObject(ob, bpy.context.evaluated_depsgraph_get())
    hit = bvh.ray_cast(Vector((x, y0, z)), Vector((0, 1, 0)))
    return hit[0].y if hit[0] is not None else None


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


def make_eyes_ears(spec, J, head):
    s = spec["height"] / BASE_H
    H = J["head"]
    parts = []
    ez = H.z + 0.0135 * s
    for sx in (-1, 1):
        ex = H.x + sx * 0.032 * s
        lid = front_y(spec, J, ex, ez, almond=False)
        e = sphere_obj("eye", Vector((ex, lid + 0.0132 * s, ez)), 0.0118 * s, seg=10, rings=7)
        parts.append((e, "eye"))
        if spec["hair"] in ("bob", "long"):
            continue
        # ear: a cupped, rimmed shell (helix rim, concha hollow), tilted back
        bm = bmesh.new()
        bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=7, radius=1.0)
        for v in bm.verts:
            x, y, z = v.co
            cup = 0.75 * math.exp(-((y / 0.5) ** 2 + ((z + 0.1) / 0.55) ** 2)) if x * sx > 0 else 0.0
            xl = (x * 0.0075 - sx * cup * 0.0055) * s
            v.co = Vector((xl, y * (0.0165 - 0.006 * max(0.0, -z)) * s, z * 0.031 * s))
        me = bpy.data.meshes.new("ear")
        bm.to_mesh(me)
        bm.free()
        for p in me.polygons:
            p.use_smooth = True
        zE = 0.008
        wE = spline(zE, HEAD_W) * (0.955 if spec["sex"] == "f" else 1.0)
        rot = Euler((R(-14), 0, R(sx * -22))).to_matrix().to_4x4()
        me.transform(Matrix.Translation(Vector((H.x + sx * (wE + 0.005) * s, H.y + 0.014 * s, H.z + zE * s))) @ rot)
        parts.append((link_obj("ear", me), "skin"))
    return parts


# ----------------------------------------------------------------------------- hands and feet
def make_hand(spec, J, sd):
    """Palm (flattened sphere) + fingers and thumb (Skin modifier chains), relaxed curl, palm toward the thigh."""
    s = spec["height"] / BASE_H
    f = spec["sex"] == "f"
    sg = 1 if sd == "L" else -1
    W = J["wrist." + sd]
    D = (J["hand." + sd] - W).normalized()
    F = Vector((0, -1, 0))
    F = (F - D * F.dot(D)).normalized()
    Nrm = D.cross(F).normalized()
    if Nrm.x * sg > 0:
        Nrm = -Nrm                                  # palm normal points at the thigh
    k = s * (0.92 if f else 1.0)
    palm_c = W + D * 0.048 * k + Nrm * 0.002 * k
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=10, v_segments=8, radius=1.0)
    M = Matrix((Nrm * 0.0155 * k, F * 0.041 * k, D * 0.052 * k)).transposed()
    for v in bm.verts:
        v.co = palm_c + M @ v.co
    me = bpy.data.meshes.new("palm")
    bm.to_mesh(me)
    bm.free()
    palm = link_obj("palm", me)
    for p in me.polygons:
        p.use_smooth = True
    # fingers: index at the front, pinky at the back; each segment curls toward the palm
    verts, edges, radii = [], [], []
    curl = spec.get("curl", (14, 26, 20))
    for i, (L, off) in enumerate(((0.074, 0.028), (0.08, 0.009), (0.076, -0.01), (0.062, -0.027))):
        base = W + D * 0.088 * k + F * off * k + Nrm * 0.004 * k
        d = D.copy()
        p = base
        idx0 = len(verts)
        verts.append(p)
        radii.append(0.0088 * k)
        cum = 4.0
        for j, frac in enumerate((0.5, 0.5)):
            cum += curl[j] + (curl[2] if j == 1 else 0)
            d = (D * math.cos(R(cum)) + Nrm * math.sin(R(cum))).normalized()
            p = p + d * L * frac * k
            verts.append(p)
            edges.append((len(verts) - 2, len(verts) - 1))
            radii.append((0.0082 - 0.0011 * j) * k)
    tb = W + D * 0.03 * k + F * 0.03 * k + Nrm * 0.006 * k
    td = (D * 0.55 + F * 0.65 + Nrm * 0.3).normalized()
    verts.append(tb)
    radii.append(0.0115 * k)
    for j, L in enumerate((0.03, 0.027)):
        td = (td + Nrm * 0.25).normalized()
        verts.append(verts[-1] + td * L * k)
        edges.append((len(verts) - 2, len(verts) - 1))
        radii.append((0.0095 - 0.0012 * j) * k)
    me = bpy.data.meshes.new("fingers")
    me.from_pydata(verts, edges, [])
    ob = link_obj("fingers", me)
    sk = ob.modifiers.new("Skin", 'SKIN')
    sk.use_smooth_shade = True
    roots = {0, 3, 6, 9, 12}
    for i, r in enumerate(radii):
        sv = me.skin_vertices[0].data[i]
        sv.radius = (r, r * 0.9)
        sv.use_root = i in roots
    sub = ob.modifiers.new("Sub", 'SUBSURF')
    sub.levels = 1
    dg = bpy.context.evaluated_depsgraph_get()
    fme = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    bpy.data.objects.remove(ob)
    fingers = link_obj("fingers", fme)
    return [(palm, "skin"), (fingers, "skin")]


def loft_obj(name, rings, cap0=True, cap1=True):
    n = len(rings[0])
    verts = [p for r in rings for p in r]
    faces = []
    for i in range(len(rings) - 1):
        for j in range(n):
            j2 = (j + 1) % n
            faces.append((i * n + j, i * n + j2, (i + 1) * n + j2, (i + 1) * n + j))
    if cap0:
        faces.append(tuple(range(n))[::-1])
    if cap1:
        faces.append(tuple((len(rings) - 1) * n + j for j in range(n)))
    me = bpy.data.meshes.new(name)
    me.from_pydata(verts, [], faces)
    me.validate()
    ob = link_obj(name, me)
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    for fc in bm.faces:
        fc.smooth = True
    bm.to_mesh(me)
    bm.free()
    return ob


def rrect(w, h, r, n=2):
    """Rounded rectangle (u across, v up) centred on u, from v=0 to v=h."""
    r = max(1e-4, min(r, w - 1e-4, h / 2 - 1e-4))
    pts = []
    for cu, cv, a0 in ((w - r, r, -90), (w - r, h - r, 0), (-w + r, h - r, 90), (-w + r, r, 180)):
        for k in range(n + 1):
            t = R(a0 + 90 * k / n)
            pts.append((cu + r * math.cos(t), cv + r * math.sin(t)))
    return pts


def make_feet(spec, J, sd):
    """Shoes (sneaker / leather / school) or flip-flops with a bare foot."""
    s = spec["height"] / BASE_H
    A = J["ankle." + sd]
    T = J["toe." + sd]
    Fw = Vector((T.x - A.x, T.y - A.y, 0)).normalized()
    Lt = Vector((Fw.y, -Fw.x, 0))
    kind = spec.get("footwear", "sneaker")
    k = s * (0.93 if spec["sex"] == "f" else 1.0)
    parts = []

    def ring(u, w, h, z0=0.0, r=0.018):
        c = Vector((A.x, A.y, 0)) + Fw * u * k
        return [c + Lt * (pu * w * k) + Vector((0, 0, z0 + pv * k)) for pu, pv in rrect(1.0, h, r / max(w, 1e-3), 2)]

    def foot_rings(scale_w=1.0, scale_h=1.0, z0=0.0):
        prof = ((-0.075, 0.03, 0.05), (-0.06, 0.036, 0.085), (-0.02, 0.038, 0.098), (0.03, 0.04, 0.085),
                (0.08, 0.046, 0.068), (0.13, 0.048, 0.055), (0.165, 0.042, 0.045), (0.19, 0.03, 0.035), (0.198, 0.016, 0.024))
        return [ring(u, w * scale_w, h * scale_h, z0, 0.02) for u, w, h in prof]
    if kind == "flipflop":
        sole = [ring(u, w, 0.014, 0.0, 0.006) for u, w in ((-0.07, 0.034), (0.0, 0.038), (0.09, 0.047), (0.17, 0.045), (0.21, 0.03))]
        parts.append((loft_obj("sole", sole), "sole"))
        foot = [ring(u, w, h, 0.012, 0.018) for u, w, h in ((-0.06, 0.028, 0.06), (-0.04, 0.032, 0.08), (0.02, 0.034, 0.07),
                                                             (0.09, 0.041, 0.042), (0.15, 0.042, 0.03), (0.185, 0.034, 0.024))]
        parts.append((loft_obj("foot", foot), "skin"))
        for i in range(5):
            u = 0.19 - abs(i - 0.8) * 0.006
            c = Vector((A.x, A.y, 0)) + Fw * u * k + Lt * ((-0.026 + i * 0.013) * (1 if sd == "L" else -1) * k)
            parts.append((sphere_obj("toe", c + Vector((0, 0, 0.026 * k)), (0.0085 if i == 0 else 0.0065) * k, (1, 1.2, 0.8), 8, 6), "skin"))
        for sgn in (1, -1):
            a0 = Vector((A.x, A.y, 0)) + Fw * 0.165 * k + Vector((0, 0, 0.03 * k))
            a1 = Vector((A.x, A.y, 0)) + Fw * 0.075 * k + Lt * sgn * 0.043 * k + Vector((0, 0, 0.018 * k))
            parts.append((tube_obj("strap", a0, a1, 0.005 * k), "shoe"))
        return parts
    h_scale = 1.0 if kind != "leather" else 0.9
    parts.append((loft_obj("shoe", foot_rings(1.0, h_scale, 0.0)), "shoe"))
    if kind in ("sneaker", "school"):
        sole = [ring(u, w, 0.026, 0.0, 0.01) for u, w in ((-0.078, 0.034), (-0.04, 0.041), (0.08, 0.049), (0.15, 0.05), (0.195, 0.034), (0.204, 0.018))]
        parts.append((loft_obj("sole", sole), "sole"))
    return parts


def tube_obj(name, a, b, r, seg=6):
    d = b - a
    bm = bmesh.new()
    q = Vector((0, 0, 1)).rotation_difference(d.normalized())
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=seg, radius1=r, radius2=r, depth=d.length,
                          matrix=Matrix.Translation((a + b) / 2) @ q.to_matrix().to_4x4())
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    return link_obj(name, me)


# ----------------------------------------------------------------------------- hair
def head_dir(J, co, s):
    d = co - J["head"]
    return Vector((d.x / (0.074 * s), d.y / (0.09 * s), d.z / (0.112 * s)))


HAIRLINES = {   # azimuth (deg, 0 = front) -> hairline height in unit head space
    "short": [(0, 0.56), (25, 0.5), (45, 0.4), (66, 0.27), (80, 0.32), (96, 0.3), (112, -0.2), (140, -0.48), (180, -0.58)],
    "slick": [(0, 0.6), (25, 0.53), (45, 0.42), (66, 0.28), (80, 0.32), (96, 0.3), (112, -0.2), (140, -0.48), (180, -0.6)],
    "buzz": [(0, 0.58), (25, 0.52), (45, 0.42), (66, 0.3), (80, 0.34), (96, 0.32), (112, -0.15), (140, -0.42), (180, -0.52)],
    "bangs": [(0, 0.6), (25, 0.53), (45, 0.42), (66, 0.2), (80, 0.0), (96, -0.1), (112, -0.25), (140, -0.48), (180, -0.6)],
}


def _head_normal(theta, z, spec):
    p0 = Vector(head_point(theta, z, spec, False))
    pt = Vector(head_point(theta + 0.02, z, spec, False))
    pz = Vector(head_point(theta, z + 0.003, spec, False))
    n = (pt - p0).cross(pz - p0)
    if n.length < 1e-9:
        n = Vector((p0.x, p0.y, 0.4 * (z + 0.02)))
    n.normalize()
    if n.dot(Vector((p0.x, p0.y, z + 0.01))) < 0:
        n = -n
    return p0, n


def hair_cap(spec, J, cap, t_top, t_side):
    """Hair laid over the skull from a smooth hairline (per-azimuth height) up to the crown, solid, with the
    edge tapering onto the scalp. A little lift and wave so it does not read as a swimming cap."""
    s = spec["height"] / BASE_H
    H = J["head"]
    azs = list(range(-180, 180, 8))
    nq = 7
    rows, inner_row = [], []
    for k in range(nq + 1):
        q = k / nq
        row = []
        for az in azs:
            wig = 0.02 * math.sin(R(az) * 9.0) if cap != "buzz" else 0.0
            zl = (spline(abs(az), HAIRLINES[cap]) + wig) * 0.112
            z = zl + (0.105 - zl) * (1 - (1 - q) ** 1.4)
            p, n = _head_normal(R(az), z, spec)
            zu = z / 0.112
            t = (t_side + (t_top - t_side) * max(0.0, min(1.0, (zu + 0.1) / 0.9))) / s
            if cap == "short":
                t += 0.008 * max(0.0, math.cos(R(az))) * max(0.0, zu - 0.35)
            t *= 0.25 + 0.75 * min(1.0, q * 3.5)
            wave = 0.0012 * math.sin(R(az) * 14 + q * 5) * min(1.0, q * 4)
            row.append(H + (p + n * (t + wave)) * s)
            if k == 0:
                inner_row.append(H + (p - n * 0.0008) * s)
        rows.append(row)
    top = H + Vector((0, 0.004, 0.112 + t_top / s)) * s
    n = len(azs)
    verts = [v for r in rows for v in r] + inner_row + [top]
    faces = []
    for i in range(nq):
        for j in range(n):
            j2 = (j + 1) % n
            faces.append((i * n + j, (i + 1) * n + j, (i + 1) * n + j2, i * n + j2))
    base_in = (nq + 1) * n
    for j in range(n):
        j2 = (j + 1) % n
        faces.append((base_in + j, j, j2, base_in + j2))
        faces.append((nq * n + j, len(verts) - 1, nq * n + j2))
    me = bpy.data.meshes.new("hair")
    me.from_pydata(verts, [], faces)
    me.validate()
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bm.to_mesh(me)
    bm.free()
    for p_ in me.polygons:
        p_.use_smooth = True
    return link_obj("hair", me)


def make_hair(spec, J, head):
    """Shell over the skull from the hairline up (thickness per style), plus bangs / curtains / tail / bun."""
    s = spec["height"] / BASE_H
    H = J["head"]
    style = spec["hair"]
    if style == "helmet":
        return make_helmet(spec, J)
    parts = []
    cap = {"bob": "bangs", "long": "bangs", "ponytail": "slick", "bun": "slick", "buzz": "buzz"}.get(style, "short")
    t_top = {"buzz": 0.0035, "slick": 0.007, "short": 0.017, "bangs": 0.012}[cap] * s
    t_side = {"buzz": 0.003, "slick": 0.005, "short": 0.007, "bangs": 0.009}[cap] * s
    parts.append((hair_cap(spec, J, cap, t_top, t_side), "hair"))

    f = spec["sex"] == "f"
    wscale = 0.955 if f else 1.0

    def curtain(z_top, z_bot, a0, a1, pad, t, back=0.0, grow=0.0, n_a=14, n_z=6, name="curtain", curl=0.006):
        """Hanging hair: rings around the head from z_top down to z_bot (unit metres), falling straight below
        the widest part of the skull, solid (outer + inner surface)."""
        rings = []
        widest_w = max(spline(z, HEAD_W) for z in [z_top + (0.11 - z_top) * k / 8 for k in range(9)]) * wscale
        widest_b = max(spline(z, HEAD_DB) for z in [z_top + (0.11 - z_top) * k / 8 for k in range(9)])
        for i in range(n_z + 1):
            q = i / n_z
            z = z_top + (z_bot - z_top) * q
            W = max(spline(z, HEAD_W) * wscale, widest_w * min(1.0, q * 3 + 0.5)) + pad + grow * q
            Db = max(spline(z, HEAD_DB), widest_b * min(1.0, q * 3 + 0.5)) + pad + grow * q * 0.7 + back * q
            Df = spline(z, HEAD_DF) * 0.6 + pad
            tuck = curl * (q ** 6)
            outer, inner_ = [], []
            for j in range(n_a + 1):
                a = R(a0 + (a1 - a0) * j / n_a)
                st, ct = math.sin(a), math.cos(a)
                D = Df if ct > 0 else Db
                wav = 0.0025 * math.sin(j * 1.7 + i * 0.9)
                ox, oy = st * (W + wav - tuck), -ct * (D + wav - tuck)
                ix, iy = st * (W + wav - tuck - t), -ct * (D + wav - tuck - t)
                outer.append(H + Vector((ox, oy + 0.004, z)) * s)
                inner_.append(H + Vector((ix, iy + 0.004, z)) * s)
            rings.append(outer + inner_[::-1])
        return loft_obj(name, rings)

    def bangs(z_top, z_bot, half_w, t):
        rows = []
        for i in range(5):
            q = i / 4
            z = H.z + (z_top + (z_bot - z_top) * q) * s
            o, inn = [], []
            for j in range(11):
                x = H.x + (-half_w + 2 * half_w * j / 10) * s
                fy = front_y(spec, J, x, z)
                wav = 0.0015 * s * math.sin(j * 2.3)
                o.append(Vector((x, fy - (0.006 + 0.004 * q) * s - wav, z)))
                inn.append(Vector((x, fy - 0.0015 * s, z)))
            rows.append(o + inn[::-1])
        return loft_obj("bangs", rows)
    if style == "bob":
        parts.append((curtain(0.05, -0.074, 50, 310, 0.011, 0.012, name="bob"), "hair"))
        parts.append((bangs(0.075, 0.031, 0.06, 0.008), "hair"))
    elif style == "long":
        parts.append((curtain(0.05, -0.11, 52, 308, 0.011, 0.012, name="long_top"), "hair"))
        parts.append((curtain(-0.06, -0.4, 96, 264, 0.03, 0.016, back=0.06, grow=0.04, n_z=6, name="long_back", curl=0.0), "hair"))
        parts.append((bangs(0.08, 0.045, 0.045, 0.008), "hair"))
    elif style == "ponytail":
        tie = H + Vector((0, 0.098, 0.028)) * s
        parts.append((sphere_obj("tie", tie, 0.017 * s, (1, 0.8, 1), 8, 6), "acc_dark"))
        rings = []
        for i in range(8):
            q = i / 7
            c = tie + Vector((0, 0.03 * math.sin(q * 2.0) + 0.012, -0.26 * q)) * s
            r = (0.022 + 0.012 * math.sin(q * 3.0) - 0.017 * q) * s
            rings.append([c + Vector((math.cos(R(a)) * r, math.sin(R(a)) * r * 0.8, 0)) for a in range(0, 360, 40)])
        parts.append((loft_obj("tail", rings), "hair"))
    elif style == "bun":
        parts.append((sphere_obj("bun", H + Vector((0, 0.08, 0.07)) * s, 0.038 * s, (1, 0.9, 0.85), 12, 8), "hair"))
    return parts


def make_helmet(spec, J):
    """Open-face motorbike helmet with a dark visor (rider)."""
    s = spec["height"] / BASE_H
    H = J["head"]
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=14, radius=1.0)
    for v in bm.verts:
        x, y, z = v.co
        v.co = H + Vector((x * 0.104, y * 0.124 + 0.006, z * 0.13 + 0.022)) * s
    cut = [v for v in bm.verts if (v.co.z < H.z - 0.045 * s) or (v.co.y < H.y - 0.05 * s and v.co.z < H.z + 0.045 * s)]
    bmesh.ops.delete(bm, geom=cut, context='VERTS')
    me = bpy.data.meshes.new("helmet")
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    hm = link_obj("helmet", me)
    sol = hm.modifiers.new("sol", 'SOLIDIFY')
    sol.thickness = 0.012 * s
    dg = bpy.context.evaluated_depsgraph_get()
    me2 = bpy.data.meshes.new_from_object(hm.evaluated_get(dg))
    bpy.data.objects.remove(hm)
    hm = link_obj("helmet", me2)
    peak = []
    for i in range(3):
        row = []
        for j in range(9):
            a = R(-55 + 110 * j / 8)
            r = (0.122 + 0.03 * i) * s
            row.append(Vector((H.x + math.sin(a) * r * 0.86, H.y - math.cos(a) * r, H.z + (0.05 - 0.008 * i) * s)))
        peak.append(row)
    rows = [[p + Vector((0, 0, 0.004 * s)) for p in r] for r in peak] + [[p - Vector((0, 0, 0.004 * s)) for p in r] for r in reversed(peak)]
    return [(hm, "helmet"), (loft_obj("peak", [r for r in zip(*rows)], cap0=True, cap1=True), "visor")]


# ----------------------------------------------------------------------------- skirts and extras
def make_skirt(spec, J, kind):
    s = spec["height"] / BASE_H
    w = spec["build"]
    rings = []
    if kind == "skirt":
        z0, z1 = 1.0, 0.56 if spec.get("short_skirt", True) else 0.48
        prof = [(0.0, 0.135), (0.25, 0.165), (0.6, 0.19), (1.0, 0.215)]
        pleats = spec["bottom"].get("pleats", False)
    else:
        z0, z1 = 1.0, 0.1
        prof = [(0.0, 0.14), (0.2, 0.17), (0.5, 0.16), (0.85, 0.15), (1.0, 0.155)]
        pleats = False
    n = 32
    for q, rr in prof:
        z = (z0 + (z1 - z0) * q) * s
        ring = []
        for j in range(n):
            a = 2 * math.pi * j / n
            pl = (0.012 * (1 if j % 2 else -1) * min(1.0, q * 3)) if pleats else 0.0
            r = (rr * w + pl) * s
            ring.append(Vector((math.sin(a) * r * 1.05, -math.cos(a) * r * 0.82 + 0.008 * s, z)))
        rings.append(ring)
    return [(loft_obj(kind, rings, cap0=False, cap1=False), "bottom")]


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
    for p in me.polygons:
        p.use_smooth = True
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


def sweep_obj(name, pts, rw, rh, n=6):
    """Flat tube along a polyline (towels, straps): cross-section rw wide, rh thick, kept upright."""
    rings = []
    for i, p in enumerate(pts):
        a = pts[max(i - 1, 0)]
        b = pts[min(i + 1, len(pts) - 1)]
        T = (b - a).normalized()
        up = Vector((0, 0, 1))
        Nn = (up - T * up.dot(T))
        Nn = Nn.normalized() if Nn.length > 1e-6 else Vector((1, 0, 0))
        B = T.cross(Nn).normalized()
        rings.append([p + B * math.cos(R(360 * k / n)) * rw + Nn * math.sin(R(360 * k / n)) * rh for k in range(n)])
    return loft_obj(name, rings)


def extras(spec, J):
    s = spec["height"] / BASE_H
    H = J["head"]
    out = []
    head_worn = {"hat", "sunglasses", "visor", "buckethat", "headband"}
    chest_worn = {"backpack", "deliverybox", "towel"}
    held = {"phone": "Hand.R", "bag": "Hand.L", "cane": "Hand.R"}
    for acc in spec.get("extras", []):
        kind, col = acc[0], acc[1]
        m = ("acc", col)
        first = len(out)
        if kind == "backpack":
            c = J["chest"] + Vector((0, 0.15, -0.05)) * s
            out.append((box_obj("backpack", (0.27 * s, 0.13 * s, 0.37 * s), c, bevel=0.04 * s), m))
            out.append((box_obj("pocket", (0.2 * s, 0.05 * s, 0.13 * s), c + Vector((0, 0.07, -0.1)) * s, bevel=0.02 * s), m))
            for sx in (-1, 1):
                a = J["upperchest"] + Vector((sx * 0.085, -0.02, 0.015)) * s
                b = J["chest"] + Vector((sx * 0.1, -0.1, -0.08)) * s
                out.append((tube_obj("strap", a + Vector((0, 0.12, 0)) * s, a, 0.012 * s), ("acc", (0.1, 0.1, 0.1))))
                out.append((tube_obj("strap", a, b, 0.011 * s), ("acc", (0.1, 0.1, 0.1))))
        elif kind == "deliverybox":
            out.append((box_obj("box", (0.42 * s, 0.36 * s, 0.4 * s), J["chest"] + Vector((0, 0.3, 0.02)) * s, bevel=0.025 * s), m))
            for sx in (-1, 1):
                a = J["upperchest"] + Vector((sx * 0.09, 0.0, 0.01)) * s
                out.append((tube_obj("strap", a + Vector((0, 0.13, 0)) * s, a + Vector((0, -0.05, -0.2)) * s, 0.012 * s), ("acc", (0.1, 0.1, 0.1))))
        elif kind == "tie":
            rings = []
            for q, wd in ((0.0, 0.02), (0.12, 0.03), (0.9, 0.044), (1.0, 0.002)):
                c = J["upperchest"] + Vector((0, -0.105 - 0.03 * q, 0.04 - 0.36 * q)) * s
                rings.append([c + Vector((wd * x * s, 0.004 * y * s, 0)) for x, y in ((-1, 0), (0, -1.5), (1, 0), (0, 1))])
            out.append((loft_obj("tie", rings), m))
        elif kind == "lanyard":
            out.append((box_obj("card", (0.055 * s, 0.008 * s, 0.08 * s), J["chest"] + Vector((0.04, -0.118, -0.06)) * s, bevel=0.003), m))
            out.append((tube_obj("cord", J["upperchest"] + Vector((0.05, -0.07, 0.03)) * s, J["chest"] + Vector((0.04, -0.118, -0.02)) * s, 0.003 * s, 4), ("acc", (0.1, 0.3, 0.7))))
        elif kind == "hat":
            out.append((cone_obj("brim", 0.18 * s, 0.18 * s, 0.01 * s, H + Vector((0, 0.005, 0.072)) * s, seg=24), m))
            out.append((cone_obj("crown", 0.104 * s, 0.09 * s, 0.09 * s, H + Vector((0, 0.008, 0.12)) * s, seg=20), m))
        elif kind == "camera":
            c = J["chest"] + Vector((0, -0.145, -0.07)) * s
            out.append((box_obj("camera", (0.12 * s, 0.065 * s, 0.075 * s), c, bevel=0.008), m))
            out.append((cone_obj("lens", 0.024 * s, 0.022 * s, 0.05 * s, c, seg=12), m))
            bpy.context.view_layer.objects.active = out[-1][0]
            out[-1][0].rotation_euler = (R(90), 0, 0)
            out[-1][0].location = Vector((0, -0.04 * s, 0))
            bpy.context.view_layer.update()
            out[-1][0].data.transform(out[-1][0].matrix_world)
            out[-1][0].matrix_world = Matrix.Identity(4)
        elif kind == "bag":
            h = J["hand.L"]
            out.append((box_obj("bag", (0.07 * s, 0.26 * s, 0.2 * s), h + Vector((0.03, 0, -0.1)) * s, bevel=0.025 * s), m))
            out.append((tube_obj("handle", h + Vector((0.03, -0.07, 0.0)) * s, h + Vector((0.03, 0.07, 0.0)) * s, 0.006 * s), m))
        elif kind == "phone":
            out.append((box_obj("phone", (0.036 * s, 0.075 * s, 0.009 * s), J["hand.R"] + Vector((0.005, -0.03, 0.01)) * s, rot=(70, 0, 0), bevel=0.003), m))
        elif kind == "sunglasses":
            ez = H.z + 0.0135 * s
            for sx in (-1, 1):
                ex = H.x + sx * 0.033 * s
                fy = front_y(spec, J, ex, ez) - 0.012 * s
                out.append((sphere_obj("lens", Vector((ex, fy, ez - 0.002 * s)), 1.0, (0.025 * s, 0.0035 * s, 0.019 * s), 12, 6), m))
                out.append((tube_obj("temple", Vector((H.x + sx * 0.058 * s, fy + 0.004 * s, ez + 0.008 * s)),
                                     Vector((H.x + sx * 0.078 * s, H.y + 0.03 * s, ez + 0.006 * s)), 0.0022 * s, 4), m))
            fyc = front_y(spec, J, H.x, ez + 0.008 * s) - 0.008 * s
            out.append((tube_obj("bridge", Vector((H.x - 0.01 * s, fyc, ez + 0.009 * s)), Vector((H.x + 0.01 * s, fyc, ez + 0.009 * s)), 0.0025 * s, 4), m))
        elif kind == "visor":
            out.append((box_obj("visor", (0.18 * s, 0.1 * s, 0.01 * s), H + Vector((0, -0.112, 0.068)) * s, rot=(-10, 0, 0), bevel=0.004), m))
            out.append((cone_obj("band", 0.088 * s, 0.088 * s, 0.03 * s, H + Vector((0, 0.004, 0.064)) * s, seg=20, cap=False), m))
        elif kind == "buckethat":
            out.append((cone_obj("bucket", 0.135 * s, 0.085 * s, 0.085 * s, H + Vector((0, 0.005, 0.072)) * s, seg=20), m))
        elif kind == "apron":
            w = spec["build"]
            rings = []
            for z, hw, dep in ((J["chest"].z + 0.06 * s, 0.085, 0.112), (J["chest"].z - 0.04 * s, 0.1, 0.122),
                               (J["waist"].z, 0.14, 0.11), (J["pelvis"].z - 0.04 * s, 0.155, 0.12), (J["pelvis"].z - 0.24 * s, 0.16, 0.125)):
                o, inn = [], []
                for j in range(9):
                    u = -1 + 2 * j / 8
                    x = u * hw * w * s
                    y = -(dep * w + 0.012) * s * (1 - 0.35 * u * u)
                    o.append(Vector((x, y - 0.004 * s, z)))
                    inn.append(Vector((x, y + 0.002 * s, z)))
                rings.append(o + inn[::-1])
            out.append((loft_obj("apron", rings), m))
            for sx in (-1, 1):
                out.append((tube_obj("strap", J["chest"] + Vector((sx * 0.08 * w, -0.118 * w, 0.06)) * s,
                                     J["upperchest"] + Vector((sx * 0.055, -0.05, 0.05)) * s, 0.005 * s, 4), m))
                out.append((tube_obj("strap", J["waist"] + Vector((sx * 0.14 * w, -0.1 * w, 0.0)) * s,
                                     J["waist"] + Vector((sx * 0.13 * w, 0.06, 0.0)) * s, 0.005 * s, 4), m))
        elif kind == "cane":
            hand = J["hand.R"]
            out.append((cone_obj("cane", 0.012 * s, 0.012 * s, hand.z, Vector((hand.x, hand.y - 0.03, hand.z / 2)), seg=8), m))
        elif kind == "towel":
            pts = []
            for k in range(13):
                a = R(35 + 290 * k / 12)                                     # round the back of the neck
                pts.append(J["neck"] + Vector((math.sin(a) * 0.088, -math.cos(a) * 0.082 + 0.012, -0.05 - 0.012 * math.cos(a))) * s)
            pts = [J["chest"] + Vector((0.065, -0.118, -0.03)) * s] + pts + [J["chest"] + Vector((-0.065, -0.118, -0.03)) * s]
            out.append((sweep_obj("towel", pts, 0.024 * s, 0.006 * s), m))
        elif kind == "headband":
            out.append((cone_obj("band", 0.085 * s, 0.086 * s, 0.028 * s, H + Vector((0, 0.003, 0.045)) * s, seg=20, cap=False), m))
        bone = "Head" if kind in head_worn else "Chest" if kind in chest_worn else held.get(kind)
        for i in range(first, len(out)):
            out[i] = (out[i][0], out[i][1], bone)
    return out


# ----------------------------------------------------------------------------- shaders (baked)
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

    def noise(self, scale, detail=3.0, vec=None):
        n = self.nt.nodes.new("ShaderNodeTexNoise")
        n.inputs["Scale"].default_value = scale
        n.inputs["Detail"].default_value = detail
        self.link(vec or self.obj, n.inputs["Vector"])
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

    def wave(self, scale, direction='X', vec=None, distortion=0.0):
        n = self.nt.nodes.new("ShaderNodeTexWave")
        n.wave_type = 'BANDS'
        n.bands_direction = direction
        n.inputs["Scale"].default_value = scale
        n.inputs["Distortion"].default_value = distortion
        self.link(vec or self.obj, n.inputs["Vector"])
        return n.outputs["Fac"]

    def remap(self, v, a, b, c, d):
        n = self.nt.nodes.new("ShaderNodeMapRange")
        n.inputs[1].default_value, n.inputs[2].default_value = a, b
        n.inputs[3].default_value, n.inputs[4].default_value = c, d
        self.link(v, n.inputs[0])
        return n.outputs[0]

    def ellipse(self, cx, cz, rx, rz, ymax, soft=0.0):
        dx = self.math('POWER', self.math('DIVIDE', self.math('SUBTRACT', self.x, cx), rx), 2)
        dz = self.math('POWER', self.math('DIVIDE', self.math('SUBTRACT', self.z, cz), rz), 2)
        dd = self.math('ADD', dx, dz)
        inside = self.math('LESS_THAN', dd, 1.0) if soft <= 0 else self.remap(dd, 1.0 - soft, 1.0, 1.0, 0.0)
        return self.math('MULTIPLY', inside, self.math('LESS_THAN', self.y, ymax))

    def band(self, axis_sock, lo, hi):
        return self.math('MULTIPLY', self.math('GREATER_THAN', axis_sock, lo), self.math('LESS_THAN', axis_sock, hi))

    def out(self, color):
        self.link(color, self.bsdf.inputs["Base Color"])


def fabric(nb, base, folds=0.12, grain=0.0, grain_scale=80):
    c = nb.rgb(base)
    c = nb.mix(1.0, c, nb.remap(nb.noise(5.0, 4.0), 0.3, 0.7, 1.0 - folds, 1.0 + folds * 0.6), "MULTIPLY")
    c = nb.mix(1.0, c, nb.remap(nb.noise(18.0, 2.0), 0.35, 0.65, 1.0 - folds * 0.5, 1.0 + folds * 0.3), "MULTIPLY")
    if grain > 0:
        c = nb.mix(1.0, c, nb.remap(nb.noise(grain_scale, 1.0), 0.3, 0.7, 1.0 - grain, 1.0 + grain), "MULTIPLY")
    return c


def skin_color(nb, p, J):
    """Skin with a face: soft blush, brows, eyelid shadow and lash line, nostrils, two-tone lips, stubble."""
    s = p["height"] / BASE_H
    c = nb.rgb(p["skin"])
    c = nb.mix(1.0, c, nb.remap(nb.noise(40.0, 3.0), 0.3, 0.7, 0.95, 1.04), "MULTIPLY")
    H = J["head"]
    hz = H.z
    front = H.y - 0.05 * s
    skin = p["skin"]
    for sx in (-1, 1):
        c = nb.mix(nb.math('MULTIPLY', nb.ellipse(H.x + sx * 0.045 * s, hz - 0.022 * s, 0.024 * s, 0.017 * s, front, 0.9), 0.18), c, (0.86, 0.45, 0.42))
        ex, ez = H.x + sx * 0.032 * s, hz + 0.0135 * s
        c = nb.mix(nb.math('MULTIPLY', nb.ellipse(ex, ez + 0.007 * s, 0.019 * s, 0.0085 * s, front, 0.7), 0.4), c, tuple(v * 0.66 for v in skin))
        c = nb.mix(nb.ellipse(ex, ez + 0.0052 * s, 0.0148 * s, 0.0016 * s, front), c, (0.05, 0.04, 0.035))     # upper lash line
        brow_in = nb.ellipse(ex - sx * 0.004 * s, ez + 0.0205 * s, 0.012 * s, 0.0028 * s, front, 0.25)
        brow_out = nb.ellipse(ex + sx * 0.009 * s, ez + 0.021 * s, 0.011 * s, 0.0017 * s, front, 0.25)
        hair_n = nb.remap(nb.noise(300.0, 1.0), 0.35, 0.65, 0.75, 1.0)
        c = nb.mix(nb.math('MULTIPLY', nb.math('MAXIMUM', brow_in, brow_out), hair_n), c, p["brow"])
        c = nb.mix(nb.math('MULTIPLY', nb.ellipse(H.x + sx * 0.0072 * s, hz - 0.0365 * s, 0.0034 * s, 0.0016 * s, front - 0.012 * s, 0.5), 0.8),
                   c, tuple(v * 0.35 for v in skin))   # nostrils
    for sx in (-1, 1):
        ear = nb.math('MULTIPLY', nb.math('GREATER_THAN', nb.math('MULTIPLY', nb.x, float(sx)), 0.071 * s),
                      nb.band(nb.z, hz - 0.02 * s, hz + 0.036 * s))
        c = nb.mix(nb.math('MULTIPLY', ear, 0.35), c, tuple(v * 0.78 for v in skin))
    lip = tuple(0.55 * l + 0.45 * k for l, k in zip(p["lips"], skin))
    c = nb.mix(nb.math('MULTIPLY', nb.ellipse(H.x, hz - 0.0495 * s, 0.02 * s, 0.0038 * s, front, 0.6), 0.9), c, tuple(v * 0.92 for v in lip))
    c = nb.mix(nb.math('MULTIPLY', nb.ellipse(H.x, hz - 0.0595 * s, 0.0175 * s, 0.0046 * s, front, 0.6), 0.9), c, lip)
    c = nb.mix(nb.math('MULTIPLY', nb.ellipse(H.x, hz - 0.0545 * s, 0.019 * s, 0.0008 * s, front, 0.4), 0.85), c, tuple(v * 0.45 for v in lip))
    if p.get("stubble"):
        jaw = nb.math('MULTIPLY', nb.ellipse(H.x, hz - 0.074 * s, 0.058 * s, 0.03 * s, H.y + 0.02 * s, 0.6),
                      nb.remap(nb.noise(260.0, 1.0), 0.42, 0.62, 0.0, 0.35))
        c = nb.mix(jaw, c, tuple(v * 0.6 for v in skin))
    return c


def eye_color(nb, p, J):
    s = p["height"] / BASE_H
    c = nb.rgb((0.9, 0.88, 0.84))
    ez = J["head"].z + 0.0135 * s
    for sx in (-1, 1):
        ex = J["head"].x + sx * 0.032 * s
        c = nb.mix(nb.ellipse(ex, ez, 0.0064 * s, 0.0064 * s, 1.0, 0.25), c, tuple(v * 0.55 for v in p.get("iris", (0.16, 0.08, 0.04))))
        c = nb.mix(nb.ellipse(ex, ez, 0.0056 * s, 0.0056 * s, 1.0, 0.3), c, p.get("iris", (0.16, 0.08, 0.04)))
        c = nb.mix(nb.ellipse(ex, ez, 0.0026 * s, 0.0026 * s, 1.0), c, (0.01, 0.01, 0.01))
    return c


def top_color(nb, p, J):
    t = p["top"]
    c = fabric(nb, t["color"], folds=0.16, grain=0.04)
    pat = t.get("pattern")
    if pat in ("floral", "tropical"):
        c = nb.mix(nb.math('LESS_THAN', nb.voronoi(14 if pat == "floral" else 9), 0.22), c, t["color2"])
        c = nb.mix(nb.math('LESS_THAN', nb.voronoi(11, (3.3, 1.7, 0.4)), 0.14), c, t.get("color3", (0.95, 0.9, 0.6)))
    elif pat == "pinstripe":
        c = nb.mix(nb.math('GREATER_THAN', nb.wave(70, 'X'), 0.88), c, t["color2"])
    elif pat == "reflective":
        z0 = J["chest"].z
        for lo, hi in ((-0.06, -0.03), (0.04, 0.07)):
            c = nb.mix(nb.band(nb.z, z0 + lo, z0 + hi), c, (0.85, 0.87, 0.9))
    elif pat == "stripes":
        c = nb.mix(nb.math('GREATER_THAN', nb.wave(28, 'Z'), 0.7), c, t["color2"])
    if t.get("buttons"):
        placket = nb.math('MULTIPLY', nb.band(nb.x, -0.006, 0.006), nb.math('LESS_THAN', nb.y, -0.04))
        c = nb.mix(placket, c, tuple(v * 0.86 for v in t["color"]))
        dots = nb.math('MULTIPLY', placket, nb.math('GREATER_THAN', nb.wave(9, 'Z'), 0.82))
        c = nb.mix(dots, c, (0.85, 0.85, 0.85))
    if t.get("pocket"):
        s = p["height"] / BASE_H
        c = nb.mix(nb.math('MULTIPLY', nb.band(nb.x, 0.03 * s, 0.085 * s), nb.math('MULTIPLY', nb.band(nb.z, J["chest"].z + 0.0, J["chest"].z + 0.06 * s), nb.math('LESS_THAN', nb.y, -0.04))),
                   c, tuple(v * 0.88 for v in t["color"]))
    if t.get("logo"):
        s = p["height"] / BASE_H
        c = nb.mix(nb.ellipse(-0.06 * s, J["chest"].z + 0.04 * s, 0.018 * s, 0.012 * s, -0.04), c, t["logo"])
    return c


def bottom_color(nb, p):
    b = p["bottom"]
    c = fabric(nb, b["color"], folds=0.18, grain=0.08 if b.get("denim") else 0.03, grain_scale=140)
    if b.get("pleats"):
        c = nb.mix(1.0, c, nb.remap(nb.wave(45, 'X'), 0, 1, 0.82, 1.05), "MULTIPLY")
    if b.get("pattern") == "batik":
        c = nb.mix(nb.math('LESS_THAN', nb.voronoi(16), 0.2), c, b["color2"])
        c = nb.mix(nb.band(nb.z, 0.1, 0.16), c, b["color2"])
    if b.get("belt"):
        s = p["height"] / BASE_H
        c = nb.mix(nb.band(nb.z, 1.03 * s, 1.065 * s), c, b["belt"])
    return c


def hair_color(nb, p, J):
    c = nb.rgb(p["hair_color"])
    H = J["head"]
    # strands flow from the crown: bands across the azimuth + along the length
    vm = nb.nt.nodes.new("ShaderNodeVectorMath")
    vm.operation = 'SUBTRACT'
    vm.inputs[1].default_value = (H.x, H.y + 0.02, H.z + 0.08)
    nb.link(nb.obj, vm.inputs[0])
    ang = nb.math('ARCTAN2', nb.math('SUBTRACT', nb.x, H.x), nb.math('SUBTRACT', nb.y, H.y))
    c = nb.mix(1.0, c, nb.remap(nb.math('SINE', nb.math('MULTIPLY', ang, 160.0)), -1, 1, 0.9, 1.08), "MULTIPLY")
    c = nb.mix(1.0, c, nb.remap(nb.math('SINE', nb.math('MULTIPLY', ang, 37.0)), -1, 1, 0.86, 1.1), "MULTIPLY")
    c = nb.mix(1.0, c, nb.remap(nb.noise(25, 3, vm.outputs[0]), 0.3, 0.7, 0.82, 1.16), "MULTIPLY")
    return c


def make_material(name, kind, spec, J):
    m = bpy.data.materials.new(name)
    nb = NB(m)
    p = spec
    if kind == "skin":
        nb.out(skin_color(nb, p, J))
    elif kind == "eye":
        nb.out(eye_color(nb, p, J))
    elif kind == "top":
        nb.out(top_color(nb, p, J))
    elif kind == "bottom":
        nb.out(bottom_color(nb, p))
    elif kind == "hair":
        nb.out(hair_color(nb, p, J))
    elif kind == "shoe":
        nb.out(fabric(nb, p["shoes"], folds=0.05, grain=0.06))
    elif kind == "sole":
        nb.out(nb.rgb(p.get("sole", (0.92, 0.92, 0.9))))
    elif kind == "helmet":
        nb.out(fabric(nb, p.get("helmet", (0.95, 0.95, 0.95)), folds=0.03))
    elif kind == "visor":
        nb.out(nb.rgb((0.06, 0.07, 0.08)))
    elif kind == "acc_dark":
        nb.out(nb.rgb((0.05, 0.05, 0.06)))
    else:
        nb.out(fabric(nb, kind[1], folds=0.08, grain=0.03))
    return m


# ----------------------------------------------------------------------------- rig + weights + motion
BONES = [
    ("Hips", "pelvis", "waist", None), ("Spine", "waist", "chest", "Hips"), ("Chest", "chest", "upperchest", "Spine"),
    ("Neck", "upperchest", "headbase", "Chest"), ("Head", "headbase", "headtop", "Neck"),
]
for _s in ("L", "R"):
    BONES += [("Shoulder." + _s, "upperchest", "shoulder." + _s, "Chest"),
              ("UpperArm." + _s, "shoulder." + _s, "elbow." + _s, "Shoulder." + _s),
              ("LowerArm." + _s, "elbow." + _s, "wrist." + _s, "UpperArm." + _s),
              ("Hand." + _s, "wrist." + _s, "hand." + _s, "LowerArm." + _s),
              ("UpperLeg." + _s, "hip." + _s, "knee." + _s, "Hips"),
              ("LowerLeg." + _s, "knee." + _s, "ankle." + _s, "UpperLeg." + _s),
              ("Foot." + _s, "ankle." + _s, "toe." + _s, "LowerLeg." + _s)]


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


def skin_weights(ob, J, rigid):
    """Nearest bone segment with blending at the joints. rigid: vertex index -> bone (head, hands, feet parts)."""
    children = {}
    for bn, h, t, par in BONES:
        if par:
            children.setdefault(par, []).append(bn)
    segs = {bn: (J[h], J[t], par) for bn, h, t, par in BONES}
    groups = {bn: ob.vertex_groups.new(name=bn) for bn, *_ in BONES}
    for v in ob.data.vertices:
        if v.index in rigid:
            groups[rigid[v.index]].add([v.index], 1.0, 'REPLACE')
            continue
        p = v.co
        best = None
        for bn, (a, b, par) in segs.items():
            if bn.startswith("Shoulder") and abs(p.x) < 0.12:
                continue
            if bn == "Head" and p.z < J["headbase"].z:
                continue
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


def set_pose(arm, angles, hips=(0.0, 0.0, 0.0)):
    """angles: bone -> (deg about X, deg about Z, deg about Y) in armature axes, applied in the bone's rest frame."""
    for pb in arm.pose.bones:
        rest = pb.bone.matrix_local.to_3x3()
        ax, az, ay = (tuple(angles.get(pb.name, (0.0, 0.0, 0.0))) + (0.0, 0.0, 0.0))[:3]
        rw = Matrix.Rotation(R(az), 3, 'Z') @ Matrix.Rotation(R(ay), 3, 'Y') @ Matrix.Rotation(R(ax), 3, 'X')
        pb.rotation_mode = 'QUATERNION'
        pb.rotation_quaternion = (rest.inverted() @ rw @ rest).to_quaternion()
        if pb.name == "Hips":
            pb.location = rest.inverted() @ Vector(hips)


def key_pose(arm, frame, angles, hips=(0.0, 0.0, 0.0)):
    set_pose(arm, angles, hips)
    for pb in arm.pose.bones:
        pb.keyframe_insert("rotation_quaternion", frame=frame)
        if pb.name == "Hips":
            pb.keyframe_insert("location", frame=frame)


def animate(arm, spec):
    """Walk: 24 frames per two steps (~1.3 m/s). Hip flexion, knee double bump, heel-toe ankle roll,
    pelvis bob / sway / twist with counter-rotating chest, arms swinging with a soft elbow. Idle: breathing,
    weight shift, small head turns."""
    arm.animation_data_create()
    st = spec.get("stride", 1.0)
    sh = spec["height"] / BASE_H
    walk = bpy.data.actions.new("Walk")
    walk.use_fake_user = True
    arm.animation_data.action = walk
    for fr in range(0, 25):
        ph = 2 * math.pi * fr / 24
        a = {}
        for side, off in (("L", 0.0), ("R", math.pi)):
            q = ph + off
            hip = -(18 * math.cos(q) + 4) * st                    # flex forward at heel strike (negative X = forward)
            g = (q % (2 * math.pi)) / (2 * math.pi)                # 0 = heel strike
            knee = 6 + 14 * math.exp(-((g - 0.12) / 0.07) ** 2) + 58 * math.exp(-((g - 0.72) / 0.12) ** 2) * st
            ank = 8 * math.exp(-((g - 0.02) / 0.04) ** 2) - 16 * math.exp(-((g - 0.6) / 0.06) ** 2) + 4 * math.exp(-((g - 0.35) / 0.1) ** 2)
            a["UpperLeg." + side] = (hip, 0)
            a["LowerLeg." + side] = (knee, 0)
            a["Foot." + side] = (-ank, 0)
            arm_sw = 20 * math.cos(q + math.pi) * st
            a["UpperArm." + side] = (arm_sw, -4 if side == "L" else 4)
            a["LowerArm." + side] = (-(14 + 16 * max(0.0, -math.cos(q + math.pi))), 0)
            a["Hand." + side] = (-6, 0)
        a["Hips"] = (2, 5 * math.cos(ph), 3 * math.sin(ph))
        a["Spine"] = (2, -3 * math.cos(ph), -1.5 * math.sin(ph))
        a["Chest"] = (-2, -5 * math.cos(ph), 0)
        a["Neck"] = (0, 1.5 * math.cos(ph), 0)
        a["Head"] = (2, 1.5 * math.cos(ph), -1.5 * math.sin(ph))
        bob = (0.012 * math.cos(2 * ph) - 0.006) * sh
        sway = 0.012 * math.sin(ph) * sh
        key_pose(arm, fr, a, hips=(sway, 0.0, bob))
    idle = bpy.data.actions.new("Idle")
    idle.use_fake_user = True
    arm.animation_data.action = idle
    for fr in range(0, 97, 4):
        th = 2 * math.pi * fr / 96
        br = math.sin(th * 2)
        shift = math.sin(th)
        a = {"Chest": (-1.2 * br, 0, 0), "Spine": (0, 0, 1.2 * shift), "Hips": (0, 0, -1.5 * shift),
             "Head": (1.5 * math.sin(th * 1.5), 6 * math.sin(th * 0.5 + 0.7), 0),
             "UpperArm.L": (1.5 * br, -3, 0), "UpperArm.R": (1.5 * br, 3, 0),
             "LowerArm.L": (-10, 0, 0), "LowerArm.R": (-10, 0, 0),
             "UpperLeg.L": (0, 0, -1.5 * shift), "UpperLeg.R": (0, 0, -1.5 * shift),
             "LowerLeg.L": (2 + 2 * max(0.0, -shift), 0), "LowerLeg.R": (2 + 2 * max(0.0, shift), 0)}
        key_pose(arm, fr, a, hips=(0.008 * shift * sh, 0, -0.003 * abs(shift) * sh))
    arm.animation_data.action = walk


# ----------------------------------------------------------------------------- bake
def bake_texture(ob, name):
    SC.render.engine = 'CYCLES'
    SC.cycles.device = 'CPU'
    SC.cycles.samples = 12
    SC.render.bake.margin = 6
    for o in bpy.context.view_layer.objects:
        o.select_set(False)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=R(55), island_margin=0.0)
    bpy.ops.uv.pack_islands(rotate=True, margin=0.006)
    bpy.ops.object.mode_set(mode='OBJECT')
    imgs = {}
    for kind in ("col", "ao"):
        img = bpy.data.images.new(f"{name}_{kind}", TEX_SIZE, TEX_SIZE)
        imgs[kind] = img
        for m in ob.data.materials:
            nodes = m.node_tree.nodes
            n = nodes.get("BAKE") or nodes.new("ShaderNodeTexImage")
            n.name = "BAKE"
            n.image = img
            nodes.active = n
        if kind == "col":
            bpy.ops.object.bake(type='DIFFUSE', pass_filter={'COLOR'}, margin=6, use_clear=True)
        else:
            SC.cycles.samples = 24
            bpy.ops.object.bake(type='AO', margin=6, use_clear=True)
    col = np.array(imgs["col"].pixels[:]).reshape(-1, 4)
    ao = np.array(imgs["ao"].pixels[:]).reshape(-1, 4)[:, :1]
    shade = 0.62 + 0.38 * np.clip(ao * 1.15, 0, 1)
    col[:, :3] = np.clip(col[:, :3] * shade, 0, 1)
    final = bpy.data.images.new(f"{name}_albedo", TEX_SIZE, TEX_SIZE)
    final.pixels[:] = col.ravel()
    final.filepath_raw = os.path.join(TEX, f"{name}_albedo.png")
    final.file_format = 'PNG'
    final.save()
    m = bpy.data.materials.new("M_" + name)
    m.use_nodes = True
    b = m.node_tree.nodes["Principled BSDF"]
    b.inputs["Roughness"].default_value = 0.8
    t = m.node_tree.nodes.new("ShaderNodeTexImage")
    t.image = final
    m.node_tree.links.new(t.outputs["Color"], b.inputs["Base Color"])
    ob.data.materials.clear()
    ob.data.materials.append(m)
    for p in ob.data.polygons:
        p.material_index = 0
    return m


# ----------------------------------------------------------------------------- characters
SKIN_TH = (0.72, 0.5, 0.36)
SKIN_LIGHT = (0.84, 0.64, 0.51)
SKIN_FAIR = (0.92, 0.74, 0.63)
LIPS = (0.6, 0.3, 0.3)
CHARS = [
    dict(name="NPC_Student", sex="f", height=1.58, build=0.95, skin=SKIN_LIGHT, lips=(0.76, 0.38, 0.42), age=0.1,
         brow=(0.08, 0.06, 0.05), hair="bob", hair_color=(0.03, 0.025, 0.025),
         top=dict(color=(0.95, 0.95, 0.93), buttons=True, collar=True, sleeve="short"),
         bottom=dict(type="skirt", color=(0.07, 0.09, 0.24), pleats=True), footwear="school", shoes=(0.04, 0.04, 0.04),
         sole=(0.05, 0.05, 0.05), extras=[("backpack", (0.12, 0.2, 0.42))]),
    dict(name="NPC_Office", sex="m", height=1.72, build=1.0, skin=SKIN_TH, lips=LIPS, brow=(0.05, 0.04, 0.03), age=0.35,
         hair="short", hair_color=(0.03, 0.025, 0.02), stubble=True,
         top=dict(color=(0.62, 0.76, 0.93), sleeve="long", pattern="pinstripe", color2=(0.47, 0.62, 0.86), buttons=True, collar=True),
         bottom=dict(type="pants", color=(0.22, 0.23, 0.27), belt=(0.08, 0.06, 0.05)), footwear="leather", shoes=(0.07, 0.045, 0.035),
         extras=[("tie", (0.55, 0.06, 0.12)), ("lanyard", (0.95, 0.95, 0.95))]),
    dict(name="NPC_Tourist", sex="m", height=1.82, build=1.08, skin=SKIN_FAIR, lips=(0.75, 0.42, 0.4), age=0.45,
         brow=(0.55, 0.42, 0.25), hair="short", hair_color=(0.72, 0.58, 0.34), iris=(0.25, 0.35, 0.45), stubble=True,
         top=dict(color=(0.08, 0.5, 0.58), pattern="tropical", color2=(0.95, 0.55, 0.2), color3=(0.95, 0.9, 0.7),
                  untucked=True, vneck=True, sleeve="short", loose=True),
         bottom=dict(type="knee", color=(0.7, 0.62, 0.45), loose=True), footwear="sneaker", shoes=(0.92, 0.92, 0.9),
         extras=[("hat", (0.82, 0.74, 0.55)), ("camera", (0.06, 0.06, 0.07)), ("backpack", (0.25, 0.3, 0.22))]),
    dict(name="NPC_Auntie", sex="f", height=1.54, build=1.14, belly=0.025, skin=SKIN_TH, lips=(0.62, 0.25, 0.3), age=0.75,
         brow=(0.25, 0.22, 0.2), hair="bun", hair_color=(0.42, 0.4, 0.4),
         top=dict(color=(0.55, 0.28, 0.6), pattern="floral", color2=(0.95, 0.75, 0.85), color3=(0.95, 0.85, 0.4),
                  untucked=True, sleeve="short", loose=True),
         bottom=dict(type="sarong", color=(0.35, 0.17, 0.1), pattern="batik", color2=(0.75, 0.55, 0.2)),
         footwear="flipflop", shoes=(0.75, 0.1, 0.1), sole=(0.8, 0.15, 0.15), extras=[("bag", (0.8, 0.12, 0.15))], stride=0.75),
    dict(name="NPC_Rider", sex="m", height=1.7, build=1.03, skin=SKIN_TH, lips=LIPS, brow=(0.04, 0.03, 0.03), age=0.35,
         hair="helmet", hair_color=(0.03, 0.03, 0.03), helmet=(0.95, 0.95, 0.95), stubble=True,
         top=dict(color=(0.95, 0.42, 0.07), sleeve="long", pattern="reflective", untucked=True),
         bottom=dict(type="pants", color=(0.17, 0.26, 0.45), denim=True), footwear="sneaker", shoes=(0.12, 0.12, 0.13),
         sole=(0.2, 0.2, 0.2), extras=[("deliverybox", (0.95, 0.42, 0.07))]),
    dict(name="NPC_Influencer", sex="f", height=1.64, build=0.94, skin=SKIN_LIGHT, lips=(0.82, 0.3, 0.4), age=0.2,
         brow=(0.1, 0.07, 0.06), hair="long", hair_color=(0.12, 0.07, 0.05),
         top=dict(color=(0.95, 0.5, 0.68), vneck=True, sleeve="short"), bottom=dict(type="pants", color=(0.95, 0.94, 0.92)),
         footwear="sneaker", shoes=(0.95, 0.95, 0.95), extras=[("sunglasses", (0.04, 0.04, 0.05)), ("phone", (0.1, 0.1, 0.12))]),
    dict(name="NPC_SchoolBoy", sex="m", height=1.6, build=0.93, skin=SKIN_TH, lips=LIPS, brow=(0.05, 0.04, 0.03), age=0.05,
         hair="buzz", hair_color=(0.03, 0.025, 0.02),
         top=dict(color=(0.95, 0.95, 0.93), buttons=True, collar=True, sleeve="short", pocket=True),
         bottom=dict(type="shorts", color=(0.55, 0.45, 0.3)), footwear="school", shoes=(0.05, 0.05, 0.05),
         sole=(0.05, 0.05, 0.05), extras=[("backpack", (0.15, 0.15, 0.17))]),
    dict(name="NPC_UniStudent", sex="f", height=1.62, build=0.93, skin=SKIN_LIGHT, lips=(0.78, 0.35, 0.4), age=0.15,
         brow=(0.08, 0.06, 0.05), hair="ponytail", hair_color=(0.05, 0.035, 0.03),
         top=dict(color=(0.97, 0.97, 0.96), buttons=True, collar=True, sleeve="short"),
         bottom=dict(type="skirt", color=(0.05, 0.05, 0.06)), short_skirt=False, footwear="school", shoes=(0.04, 0.04, 0.04),
         sole=(0.05, 0.05, 0.05), extras=[("bag", (0.35, 0.22, 0.15))]),
    dict(name="NPC_Worker", sex="m", height=1.68, build=1.05, skin=(0.62, 0.42, 0.3), lips=LIPS, age=0.5,
         brow=(0.05, 0.04, 0.03), hair="short", hair_color=(0.03, 0.025, 0.02), stubble=True,
         top=dict(color=(0.3, 0.38, 0.5), sleeve="long", untucked=True, loose=True), bottom=dict(type="pants", color=(0.3, 0.27, 0.22), loose=True),
         footwear="flipflop", shoes=(0.12, 0.1, 0.08), sole=(0.15, 0.13, 0.1),
         extras=[("buckethat", (0.45, 0.42, 0.3)), ("towel", (0.9, 0.9, 0.85))]),
    dict(name="NPC_Vendor", sex="f", height=1.56, build=1.1, belly=0.015, skin=SKIN_TH, lips=(0.65, 0.28, 0.32), age=0.55,
         brow=(0.1, 0.08, 0.06), hair="ponytail", hair_color=(0.04, 0.03, 0.03),
         top=dict(color=(0.95, 0.85, 0.3), untucked=True, sleeve="short", loose=True), bottom=dict(type="pants", color=(0.2, 0.2, 0.35), loose=True),
         footwear="flipflop", shoes=(0.85, 0.2, 0.3), sole=(0.85, 0.25, 0.35),
         extras=[("apron", (0.85, 0.2, 0.25)), ("visor", (0.95, 0.95, 0.95))], stride=0.8),
    dict(name="NPC_Jogger", sex="m", height=1.75, build=0.98, skin=SKIN_TH, lips=LIPS, brow=(0.05, 0.04, 0.03), age=0.3,
         hair="buzz", hair_color=(0.03, 0.025, 0.02),
         top=dict(color=(0.1, 0.12, 0.15), pattern="reflective", untucked=True, sleeve="none", sleeveless=True),
         bottom=dict(type="shorts", color=(0.05, 0.05, 0.06)), footwear="sneaker", shoes=(0.95, 0.35, 0.1),
         extras=[("headband", (0.95, 0.35, 0.1))], stride=1.15),
    dict(name="NPC_Grandpa", sex="m", height=1.62, build=0.95, skin=(0.7, 0.5, 0.38), lips=(0.55, 0.3, 0.28), age=0.95,
         brow=(0.8, 0.8, 0.78), hair="buzz", hair_color=(0.82, 0.82, 0.8),
         top=dict(color=(0.95, 0.95, 0.92), vneck=True, untucked=True, sleeve="short", loose=True),
         bottom=dict(type="pants", color=(0.25, 0.2, 0.35), loose=True), footwear="flipflop", shoes=(0.3, 0.25, 0.2), sole=(0.25, 0.2, 0.16),
         extras=[("cane", (0.4, 0.26, 0.14))], stride=0.65),
    dict(name="NPC_Uncle", sex="m", height=1.66, build=1.12, belly=0.035, skin=SKIN_TH, lips=LIPS, age=0.65,
         brow=(0.35, 0.33, 0.32), hair="short", hair_color=(0.5, 0.5, 0.5), stubble=True,
         top=dict(color=(0.13, 0.42, 0.28), untucked=True, sleeve="short", collar=True, loose=True),
         bottom=dict(type="knee", color=(0.62, 0.55, 0.42), loose=True), footwear="flipflop", shoes=(0.1, 0.3, 0.75),
         sole=(0.1, 0.3, 0.75), stride=0.85),
]

# seated variants for vehicles: name, source character, pose
SEATED = [("SEAT_Rider", "NPC_Rider", "moto"), ("SEAT_TukDriver", "NPC_Worker", "tuk"),
          ("SEAT_PassengerA", "NPC_Student", "bench"), ("SEAT_PassengerB", "NPC_Auntie", "bench"),
          ("SEAT_PassengerC", "NPC_Office", "bench")]
POSES = {   # targets relative to the seat point (front -Y): hands, feet; spine lean; elbows / knees pole direction
    "moto": dict(hand=(0.31, -0.5, 0.17), foot=(0.17, -0.2, -0.47), lean=14, head=-8),
    "tuk": dict(hand=(0.36, -0.42, 0.27), foot=(0.15, -0.38, -0.33), lean=6, head=-4),
    "bench": dict(hand=(0.12, -0.2, 0.05), foot=(0.11, -0.42, -0.42), lean=-4, head=0),
}


def build_character(spec):
    name = spec["name"]
    J = joints_for(spec)
    body, N, E = build_body(spec, J)
    slots = ["skin", "top", "bottom", "eye", "hair", "shoe", "sole", "helmet", "visor", "acc_dark"]
    mats = {k: make_material(f"{name}_{k}", k, spec, J) for k in slots}
    for k in slots:
        body.data.materials.append(mats[k])
    dress(body, spec, N, E, slots)
    head = make_head(spec, J)
    parts = [(head, "skin", "Head")]
    parts += [(o, k, "Head") for o, k in make_eyes_ears(spec, J, head)]
    parts += [(o, k, "Head") for o, k in make_hair(spec, J, head)]
    for sd in ("L", "R"):
        parts += [(o, k, "Hand." + sd) for o, k in make_hand(spec, J, sd)]
        parts += [(o, k, "Foot." + sd) for o, k in make_feet(spec, J, sd)]
    if spec["bottom"]["type"] in ("skirt", "sarong"):
        parts += [(o, k, None) for o, k in make_skirt(spec, J, spec["bottom"]["type"])]
    parts += extras(spec, J)
    acc_count = 0
    for ob, mk, bone in parts:
        if isinstance(mk, tuple):
            m = make_material(f"{name}_acc{acc_count}", mk, spec, J)
            acc_count += 1
        else:
            m = mats[mk]
        ob.data.materials.clear()
        ob.data.materials.append(m)
        if bone:
            vg = ob.vertex_groups.new(name="RIGID_" + bone)
            vg.add(list(range(len(ob.data.vertices))), 1.0, 'REPLACE')
    objs = [body] + [o for o, _, _ in parts]
    with bpy.context.temp_override(active_object=body, selected_editable_objects=objs, object=body):
        bpy.ops.object.join()
    body.name = name
    body.data.name = name
    bake_texture(body, name)
    rigid = {}
    for vg in list(body.vertex_groups):
        if vg.name.startswith("RIGID_"):
            bone = vg.name[6:]
            gi = vg.index
            for v in body.data.vertices:
                for gg in v.groups:
                    if gg.group == gi and gg.weight > 0.5:
                        rigid[v.index] = bone
            body.vertex_groups.remove(vg)
    arm = build_rig(name, J)
    skin_weights(body, J, rigid)
    body.parent = arm
    mod = body.modifiers.new("Armature", 'ARMATURE')
    mod.object = arm
    animate(arm, spec)
    tri = sum(len(p.vertices) - 2 for p in body.data.polygons)
    print(f"BUILT {name}: {tri} tris")
    # keep finished characters out of the next ones' AO bake
    arm.hide_render = True
    body.hide_render = True
    return arm, body, J


def seated_variant(seat_name, arm, body, J, spec, pose):
    """Pose the rig with IK toward handlebars / footrests, bake the deformed mesh, origin at the seat point."""
    P = POSES[pose]
    s = spec["height"] / BASE_H
    seat_pt = Vector((0, J["pelvis"].y + 0.03 * s, J["pelvis"].z - 0.13 * s))
    arm.animation_data.action = None
    set_pose(arm, {"Spine": (P["lean"] * 0.4, 0), "Chest": (P["lean"] * 0.6, 0), "Head": (P["head"], 0), "Neck": (0, 0)})
    bpy.context.view_layer.update()
    empties = []

    def target(name, loc):
        e = bpy.data.objects.new(name, None)
        SC.collection.objects.link(e)
        e.location = loc
        empties.append(e)
        return e
    for sd, sg in (("L", 1), ("R", -1)):
        hx, hy, hz = P["hand"]
        fx, fy, fz = P["foot"]
        th = target("hand_t", seat_pt + Vector((sg * hx * s, hy * s, hz * s)) + (J["upperchest"] - J["pelvis"]) * 0.0)
        tf = target("foot_t", seat_pt + Vector((sg * fx * s, fy * s, fz * s)))
        pe = target("elbow_p", J["elbow." + sd] + Vector((sg * 0.3, 0.25, -0.1)))
        pk = target("knee_p", J["knee." + sd] + Vector((sg * 0.05, -0.6, 0.3)))
        for bn, t, pole, pang in (("LowerArm." + sd, th, pe, 0), ("LowerLeg." + sd, tf, pk, -90)):
            c = arm.pose.bones[bn].constraints.new('IK')
            c.target = t
            c.pole_target = pole
            c.pole_angle = R(pang)
            c.chain_count = 2
        # thighs forward before IK so the solver bends the right way
        arm.pose.bones["UpperLeg." + sd].rotation_mode = 'QUATERNION'
    set_pose(arm, {"Spine": (P["lean"] * 0.4, 0), "Chest": (P["lean"] * 0.6, 0), "Head": (P["head"], 0),
                   "UpperLeg.L": (-80, 0), "UpperLeg.R": (-80, 0), "LowerLeg.L": (80, 0), "LowerLeg.R": (80, 0),
                   "UpperArm.L": (-40, 0), "UpperArm.R": (-40, 0), "LowerArm.L": (-40, 0), "LowerArm.R": (-40, 0)})
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    me = bpy.data.meshes.new_from_object(body.evaluated_get(dg))
    me.transform(body.matrix_world)
    me.transform(Matrix.Translation(-seat_pt))
    ob = link_obj(seat_name, me)
    ob.data.materials.clear()
    ob.data.materials.append(body.data.materials[0])
    for pb in arm.pose.bones:
        for c in list(pb.constraints):
            pb.constraints.remove(c)
    set_pose(arm, {})
    for e in empties:
        bpy.data.objects.remove(e)
    arm.animation_data.action = bpy.data.actions.get("Walk")
    return ob


# ----------------------------------------------------------------------------- main
def main():
    built = []
    specs = [c for c in CHARS if not ONLY or c["name"] in ONLY]
    for spec in specs:
        built.append((*build_character(spec), spec))
    seated = []
    for seat_name, src, pose in SEATED:
        for arm, body, J, spec in built:
            if spec["name"] == src:
                seated.append((seat_name, seated_variant(seat_name, arm, body, J, spec, pose)))

    if EXPORT:
        for arm, body, J, spec in built:
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
        for seat_name, ob in seated:
            for o in bpy.context.view_layer.objects:
                o.select_set(False)
            ob.select_set(True)
            bpy.context.view_layer.objects.active = ob
            bpy.ops.export_scene.fbx(
                filepath=os.path.join(OUT, seat_name + ".fbx"), use_selection=True, object_types={'MESH'},
                apply_unit_scale=True, apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
                bake_space_transform=True, mesh_smooth_type='OFF', path_mode='RELATIVE', bake_anim=False)
        print("EXPORTED", len(built), "characters and", len(seated), "seated variants to", OUT)

    # ----------------------------------------------------------------------------- preview renders
    for arm, body, J, spec in built:
        arm.hide_render = False
        body.hide_render = False
    for i, (arm, body, J, spec) in enumerate(built):
        arm.location = ((i - (len(built) - 1) / 2) * 0.62, 0, 0)
    for i, (seat_name, ob) in enumerate(seated):
        ob.location = ((i - (len(seated) - 1) / 2) * 0.9, 3.0, 0.6)
    SC.frame_set(5)
    ground = bpy.data.meshes.new("ground")
    ground.from_pydata([(-12, -8, 0), (12, -8, 0), (12, 8, 0), (-12, 8, 0)], [], [(0, 1, 2, 3)])
    link_obj("ground", ground)
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.7, 0.75, 0.82, 1)
    sun = link_obj("sun", bpy.data.lights.new("sun", 'SUN'))
    sun.data.energy = 3.5
    sun.rotation_euler = (R(50), 0, R(-30))
    cam = link_obj("cam", bpy.data.cameras.new("cam"))
    SC.camera = cam
    for eng in ("BLENDER_EEVEE_NEXT", "BLENDER_EEVEE"):
        try:
            SC.render.engine = eng
            break
        except TypeError:
            pass
    SC.view_settings.view_transform = 'Standard'
    cam.data.lens = 32
    cam.location = (0, -0.9 * max(4.5, len(built) * 0.75), 1.2)
    cam.rotation_euler = (R(88), 0, 0)
    SC.render.resolution_x, SC.render.resolution_y = 2000, 760
    SC.render.filepath = os.path.join(REN, "npc_lineup.png")
    bpy.ops.render.render(write_still=True)
    SC.frame_set(0)
    for arm, body, J, spec in built:
        arm.animation_data.action = bpy.data.actions["Idle"]
    SC.render.resolution_x, SC.render.resolution_y = 1400, 800
    for i, (arm, body, J, spec) in enumerate(built[:4]):
        cam.data.lens = 85
        cam.location = (arm.location.x + 0.35, -1.25, J["head"].z + 0.02)
        cam.rotation_euler = (R(89), 0, R(16))
        SC.render.filepath = os.path.join(REN, f"npc_face_{spec['name']}.png")
        bpy.ops.render.render(write_still=True)
    if seated:
        cam.data.lens = 35
        cam.location = (2.2, -1.2, 2.2)
        look = Vector((0, 3.0, 1.0)) - cam.location
        cam.rotation_euler = look.to_track_quat('-Z', 'Y').to_euler()
        SC.render.filepath = os.path.join(REN, "npc_seated.png")
        bpy.ops.render.render(write_still=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(ROOT, "BlenderSource", "ThaiNPC.blend"))
    print("DONE")


if __name__ == "__main__":
    main()

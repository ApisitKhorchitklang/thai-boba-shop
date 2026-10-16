"""
Thai street vehicles with realistic proportions. exec'd by thai_boba_assets.py (uses Asset, PAL, mat, MX, R, FONT_*).

Car bodies are lofted from analytic profiles keyed along the length: plan-rounded bumpers, a hood that rolls
into the fascia, raked glass, a tumblehome greenhouse and real wheel arches (the outer skin is lifted onto
the arch and the step underneath becomes the arch liner). Lights, grille, plates, seams, handles, wipers and
mirrors follow the body surface. Motorbike and tuk-tuk panels are closed lofts (no paper-thin sheets).

Every wheel is its own child object (Wheel_*, origin at the hub, axle along X) so the game can spin it, and
"Seat_*" empties mark where a seated character goes. Kit conventions: front faces -Y, origin on the ground,
Z up, metres. Left of the vehicle = +X (it faces -Y); Thailand drives on the left, so kerb-side doors are +X.
"""

PAL.update({
    # paint: glossy clear coat (low roughness, a little metal so it picks up the sky)
    "paint_pink":    ((0.93, 0.33, 0.56), 0.2, 0.25, 1, 0),
    "paint_green":   ((0.10, 0.50, 0.21), 0.2, 0.25, 1, 0),
    "paint_yellow":  ((0.99, 0.79, 0.10), 0.2, 0.2, 1, 0),
    "paint_white":   ((0.90, 0.91, 0.92), 0.2, 0.15, 1, 0),
    "paint_red":     ((0.70, 0.05, 0.06), 0.2, 0.25, 1, 0),
    "paint_orange":  ((0.96, 0.40, 0.05), 0.25, 0.2, 1, 0),
    "paint_blue":    ((0.05, 0.25, 0.70), 0.2, 0.3, 1, 0),
    "car_glass":     ((0.04, 0.06, 0.08), 0.04, 0.5, 1, 0),
    "trim_black":    ((0.025, 0.025, 0.03), 0.45, 0, 1, 0),
    "plastic_grey":  ((0.15, 0.15, 0.16), 0.7, 0, 1, 0),
    "underbody":     ((0.05, 0.05, 0.05), 0.95, 0, 1, 0),
    "tire":          ((0.045, 0.045, 0.05), 0.85, 0, 1, 0),
    "rim":           ((0.66, 0.67, 0.70), 0.25, 1, 1, 0),
    "rim_dark":      ((0.16, 0.16, 0.18), 0.4, 0.8, 1, 0),
    "lamp_lens":     ((0.86, 0.89, 0.93), 0.05, 0.3, 1, 0.5),
    "lamp_tail":     ((0.72, 0.03, 0.04), 0.1, 0, 1, 1.0),
    "lamp_amber":    ((1.00, 0.50, 0.05), 0.1, 0, 1, 0.8),
    "plate_white":   ((0.95, 0.95, 0.93), 0.5, 0, 1, 0),
    "plate_yellow":  ((0.98, 0.82, 0.15), 0.5, 0, 1, 0),
    "plate_ink":     ((0.02, 0.02, 0.02), 0.5, 0, 1, 0),
    "seat_vinyl":    ((0.10, 0.10, 0.11), 0.5, 0, 1, 0),
    "mirror_glass":  ((0.70, 0.76, 0.82), 0.02, 1, 1, 0),
    "taxi_sign":     ((0.98, 0.96, 0.90), 0.3, 0, 1, 0.4),
    "engine":        ((0.30, 0.31, 0.33), 0.5, 0.6, 1, 0),
    "bed_liner":     ((0.06, 0.06, 0.065), 0.9, 0, 1, 0),
})


# ----------------------------------------------------------------------------- mesh helpers
def _mi(a, g, name):
    bm, mats = a.groups[g]
    m = mat(name)
    if m not in mats:
        mats.append(m)
    return mats.index(m)


def _sharpen(faces, angle):
    lim = R(angle)
    seen = set()
    for f in faces:
        if not f.is_valid:
            continue
        for e in f.edges:
            if e in seen or not e.is_valid:
                continue
            seen.add(e)
            lf = e.link_faces
            if len(lf) != 2:
                continue
            n1, n2 = lf[0].normal, lf[1].normal
            if lf[0].material_index != lf[1].material_index:
                e.smooth = False
            elif n1.length > 1e-8 and n2.length > 1e-8 and n1.angle(n2) > lim:
                e.smooth = False


def raw(a, verts, faces, mats, g=None, smooth=True, sharp=38.0, outward=None, weld=0.0):
    """Add a mesh to group g. faces = vertex-index tuples, mats = one material name or one per face.
    outward: "recalc" (closed shells) or f(face_centre) -> wanted normal direction."""
    g, bm = a._g(g)
    vs = [bm.verts.new(Vector(v)) for v in verts]
    fs = []
    for k, f in enumerate(faces):
        ids = []
        for i in f:
            if not ids or vs[i] is not vs[ids[-1]]:
                ids.append(i)
        if len(ids) > 2 and vs[ids[0]] is vs[ids[-1]]:
            ids.pop()
        if len(set(ids)) < 3:
            continue
        try:
            face = bm.faces.new([vs[i] for i in ids])
        except ValueError:
            continue
        face.material_index = _mi(a, g, mats if isinstance(mats, str) else mats[k])
        face.smooth = smooth
        fs.append(face)
    if weld > 0:
        bmesh.ops.remove_doubles(bm, verts=[v for v in vs if v.is_valid], dist=weld)
        fs = [f for f in fs if f.is_valid]
        bmesh.ops.dissolve_degenerate(bm, dist=weld * 0.5, edges=list({e for f in fs for e in f.edges if e.is_valid}))
        fs = [f for f in fs if f.is_valid]
    for f in fs:
        f.normal_update()
    if outward == "recalc":
        bmesh.ops.recalc_face_normals(bm, faces=fs)
    elif outward is not None:
        for f in fs:
            if f.normal.dot(Vector(outward(f.calc_center_median()))) < 0:
                f.normal_flip()
    for f in fs:
        f.normal_update()
    if smooth and sharp:
        _sharpen(fs, sharp)
    return fs


def grid(a, pts, mats, g=None, outward=None, sharp=38.0, closed_u=False):
    """pts[i][j] grid of 3D points -> quads. mats: name or f(i, j)."""
    nu, nv = len(pts), len(pts[0])
    verts = [p for row in pts for p in row]
    faces, mm = [], []
    for i in range(nu - 1 + (1 if closed_u else 0)):
        i2 = (i + 1) % nu
        for j in range(nv - 1):
            faces.append((i * nv + j, i * nv + j + 1, i2 * nv + j + 1, i2 * nv + j))
            mm.append(mats if isinstance(mats, str) else mats(i, j))
    return raw(a, verts, faces, mm, g, outward=outward, sharp=sharp)


def closed_loft(a, rings, m, g=None, caps=(True, True), sharp=40.0):
    """Solid shell through closed rings (same point count); m = name or f(centre, normal) -> name."""
    g, _ = a._g(g)
    n = len(rings[0])
    verts = [p for r in rings for p in r]
    faces = []
    for i in range(len(rings) - 1):
        for j in range(n):
            j2 = (j + 1) % n
            faces.append((i * n + j, i * n + j2, (i + 1) * n + j2, (i + 1) * n + j))
    if caps[0]:
        faces.append(tuple(range(n)))
    if caps[1]:
        faces.append(tuple((len(rings) - 1) * n + j for j in range(n)))
    fs = raw(a, verts, faces, m if isinstance(m, str) else "trim_black", g, outward="recalc", sharp=0)
    if not isinstance(m, str):
        for f in fs:
            f.material_index = _mi(a, g, m(f.calc_center_median(), f.normal))
    _sharpen(fs, sharp)
    return fs


def lathe(a, profile, c, axis, segs, m, g=None, outward=None, sharp=50.0, smooth=True):
    """Revolve [(radius, axial)] around the X axis through c; axial is multiplied by axis (+1 / -1)."""
    cx, cy, cz = c
    n = len(profile)
    verts, faces = [], []
    for k in range(segs):
        ang = 2 * math.pi * k / segs
        ca, sa = math.cos(ang), math.sin(ang)
        for (r, ax) in profile:
            verts.append((cx + ax * axis, cy + r * ca, cz + r * sa))
    for k in range(segs):
        k2 = (k + 1) % segs
        for p in range(n - 1):
            faces.append((k * n + p, k * n + p + 1, k2 * n + p + 1, k2 * n + p))
    return raw(a, verts, faces, m, g, outward=outward, sharp=sharp, smooth=smooth)


def rounded_rect(x0, x1, z0, z1, r, n=3):
    """Outline (x, z) of a rounded rectangle, counter-clockwise."""
    r = max(1e-4, min(r, (x1 - x0) / 2 - 1e-4, (z1 - z0) / 2 - 1e-4))
    pts = []
    for cx, cz, a0 in ((x1 - r, z0 + r, -90), (x1 - r, z1 - r, 0), (x0 + r, z1 - r, 90), (x0 + r, z0 + r, 180)):
        for k in range(n + 1):
            t = R(a0 + 90 * k / n)
            pts.append((cx + r * math.cos(t), cz + r * math.sin(t)))
    return pts


def ring_y(y, w, z0, z1, r, cx=0.0, n=2):
    """Rounded-rect ring across X/Z at station y (for lofts running along the length)."""
    return [(x, y, z) for x, z in rounded_rect(cx - w, cx + w, z0, z1, r, n)]


def ring_z(z, yc, w, d, r, n=2):
    """Rounded-rect ring across X/Y at height z (for lofts running upward)."""
    return [(x, y, z) for x, y in rounded_rect(-w, w, yc - d / 2, yc + d / 2, r, n)]


def arc_loft(a, g, c, radius, a0, a1, w, t, m, n=8, x0=0.0):
    """Mudguard / fender: a solid curved strip around the X axis through c (angles in degrees, 90 = top)."""
    cx, cy, cz = c
    sec = rounded_rect(x0 - w / 2, x0 + w / 2, 0.0, t, min(t, w) * 0.45, 1)
    rings = []
    for k in range(n + 1):
        ang = R(a0 + (a1 - a0) * k / n)
        dy, dz = math.cos(ang), math.sin(ang)
        rings.append([(cx + u, cy + dy * (radius + v), cz + dz * (radius + v)) for u, v in sec])
    closed_loft(a, rings, m, g)


def plate(a, g, center, facing, color="plate_white", line1="กข 1234", w=0.34, h=0.15):
    """Thai number plate: registration in big letters, the province as a fine line under it."""
    x, y, z = center
    a.box((w, 0.012, h), (x, y, z), color, bevel=0.006, g=g)
    rz = 0 if facing < 0 else 180
    yy = y + facing * 0.0075
    a.text(line1, h * 0.42, (x, yy, z + h * 0.1), "plate_ink", rot=(90, 0, rz), extrude=0.0, fnt=FONT_TH_BOLD, g=g, res=1)
    a.box((w * 0.62, 0.002, h * 0.09), (x, yy, z - h * 0.28), "plate_ink", g=g)


def keyed(y, keys):
    """Interpolate keys (y, value[, mode]); mode of the segment ending at that key: ss (default), lin, in, out."""
    if y <= keys[0][0]:
        return keys[0][1]
    for k0, k1 in zip(keys, keys[1:]):
        if y <= k1[0]:
            span = k1[0] - k0[0]
            t = 0.0 if span <= 0 else (y - k0[0]) / span
            mode = k1[2] if len(k1) > 2 else "ss"
            if mode == "ss":
                t = t * t * (3 - 2 * t)
            elif mode == "in":
                t = t * t
            elif mode == "out":
                t = 1 - (1 - t) ** 2
            return k0[1] + (k1[1] - k0[1]) * t
    return keys[-1][1]


# ----------------------------------------------------------------------------- car body
class CarBody:
    """Right half outline per station (bottom centre -> side -> greenhouse -> top centre), mirrored and lofted.
    18 outline points:  0 floor centre | 1 liner foot | 2 liner top | 3-4 bottom corner | 5-8 side |
    9-10 shoulder (10 = top of the shoulder) | 11 window seal | 12 | 13 roof-corner start | 14-15 roof corner |
    16 | 17 top centre.  Segment k joins point k to k+1."""
    NR = 18

    def __init__(self, yf, yr, hw, bot, belt, top, roof_hw, plan_r=(0.32, 0.28), end_r=(0.07, 0.06),
                 wheels=(), wheel_r=0.31, arch_pad=0.05, bulge=0.016, tuck=0.05, crown=0.025,
                 roof_r=0.07, shoulder_r=0.045, bottom_r=0.06, liner_in=0.3, step=0.28):
        self.yf, self.yr, self.hw = yf, yr, hw
        self.bot, self.belt, self.top, self.roof_hw = bot, belt, top, roof_hw
        self.plan_r, self.end_r = plan_r, end_r
        self.wheels, self.wheel_r, self.arch_pad = list(wheels), wheel_r, arch_pad
        self.bulge, self.tuck, self.crown = bulge, tuck, crown
        self.roof_r, self.shoulder_r, self.bottom_r = roof_r, shoulder_r, bottom_r
        self.liner_in, self.step = liner_in, step

    def half_width(self, y):
        w = self.hw
        for d, pr in ((y - self.yf, self.plan_r[0]), (self.yr - y, self.plan_r[1])):
            if d < pr:
                w = min(w, self.hw - pr + math.sqrt(max(0.0, pr * pr - (pr - d) ** 2)))
        return w

    def drop(self, y):
        out = 0.0
        for d, r in ((y - self.yf, self.end_r[0]), (self.yr - y, self.end_r[1])):
            if d < r:
                out = max(out, r - math.sqrt(max(0.0, r * r - (r - d) ** 2)))
        return out

    def section(self, y):
        H = self.half_width(y)
        dr = self.drop(y)
        zb = keyed(y, self.bot) + dr * 0.5
        zs = keyed(y, self.belt) - dr
        zt = max(keyed(y, self.top) - dr, zs + 0.015)
        rr = min(self.roof_r, (zt - zs) * 0.45)
        rb = min(self.bottom_r, (zs - zb) * 0.25)
        rs = min(self.shoulder_r, (zs - zb) * 0.25)
        xsb = H - self.bulge - self.tuck
        xst = H - self.bulge
        rw = min(keyed(y, self.roof_hw), xst - rs - 0.02)
        rw = max(rw, rr + 0.04)
        P, T = [(0.0, zb)], []

        def add(x, z, tag):
            P.append((x, z))
            T.append(tag)
        add(max(0.04, xsb - rb - self.liner_in), zb, "floor")
        add(xsb - rb, zb, "liner")
        cx, cz = xsb - rb, zb + rb
        for a_ in (-45, 0):
            add(cx + rb * math.cos(R(a_)), cz + rb * math.sin(R(a_)), "bcorner")
        z0, z1 = zb + rb, zs - rs
        for k in range(1, 5):
            t = k / 4
            add(H - self.bulge + self.bulge * math.sin(math.pi * t) - self.tuck * (1 - t) ** 3, z0 + (z1 - z0) * t, "side")
        cx, cz = xst - rs, zs - rs
        for a_ in (45, 90):
            add(cx + rs * math.cos(R(a_)), cz + rs * math.sin(R(a_)), "shoulder")
        sx, sz = cx, zs
        rx, rz = rw, zt - rr
        for k, t in enumerate((0.1, 0.55, 1.0)):
            add(sx + (rx - sx) * t, sz + (rz - sz) * t, "seal" if k == 0 else "gh")
        cx, cz = rw - rr, zt - rr
        for a_ in (45, 90):
            add(cx + rr * math.cos(R(a_)), cz + rr * math.sin(R(a_)), "rcorner")
        x0 = rw - rr
        for x in (x0 * 0.5, 0.0):
            add(x, zt + self.crown * (1 - (x / x0) ** 2), "roof")
        lift = [False] * len(P)
        Ra = self.wheel_r + self.arch_pad
        for yw in self.wheels:
            dy = y - yw
            if abs(dy) < Ra:
                az = self.wheel_r + math.sqrt(Ra * Ra - dy * dy)
                for k in range(2, len(P)):
                    x, z = P[k]
                    if z < az:
                        P[k] = (x, az)
                        lift[k] = True
        return P, T, lift

    def stations(self, extra=()):
        ys = []
        L = self.yr - self.yf
        n = max(6, int(round(L / self.step)))
        ys += [self.yf + L * k / n for k in range(n + 1)]
        for end, r in ((0, max(self.plan_r[0], self.end_r[0])), (1, max(self.plan_r[1], self.end_r[1]))):
            for f in (0.1, 0.35, 0.7):
                ys.append(self.yf + r * f if end == 0 else self.yr - r * f)
        Ra = self.wheel_r + self.arch_pad
        for yw in self.wheels:
            for k in range(7):
                ys.append(yw + Ra * math.cos(math.pi * k / 6))
            ys += [yw - Ra - 0.04, yw + Ra + 0.04]
        ys += list(extra)
        ys = sorted(y for y in ys if self.yf <= y <= self.yr)
        out = [ys[0]]
        for y in ys[1:]:
            if y - out[-1] > 0.02:
                out.append(y)
        out[-1] = self.yr
        return out

    def build(self, a, g, skin, skip=None, cap_front="paint_white", cap_rear="paint_white", extra=()):
        ys = self.stations(extra)
        NR = self.NR
        n = 2 * NR - 2
        rings, lifts, T = [], [], None
        for y in ys:
            P, T, F = self.section(y)
            rings.append([(x, y, z) for x, z in P] + [(-x, y, z) for x, z in P[-2:0:-1]])
            lifts.append(F + F[-2:0:-1])
        verts = [p for r in rings for p in r]
        faces, mats = [], []
        for i in range(len(rings) - 1):
            ym = (ys[i] + ys[i + 1]) / 2
            for j in range(n):
                j2 = (j + 1) % n
                tag = T[j] if j < NR - 1 else T[2 * NR - 3 - j]
                if tag == "floor" or (skip and skip(ym, tag)):
                    continue                                  # nobody sees the floor pan
                well = (lifts[i][j] and lifts[i][j2] and lifts[i + 1][j] and lifts[i + 1][j2]) or \
                       (tag == "liner" and (lifts[i][2] or lifts[i + 1][2]))
                faces.append((i * n + j, i * n + j2, (i + 1) * n + j2, (i + 1) * n + j))
                mats.append(skin(ym, tag, well))
        if cap_front:
            faces.append(tuple(range(n))[::-1])
            mats.append(cap_front)
        if cap_rear:
            last = (len(rings) - 1) * n
            faces.append(tuple(last + j for j in range(n)))
            mats.append(cap_rear)
        fs = raw(a, verts, faces, mats, g, outward="recalc", sharp=34.0, weld=0.0015)
        return fs

    # surface queries -------------------------------------------------------
    def side_x(self, y, z):
        """x of the outer skin at height z (side wall, shoulder, then up the greenhouse)."""
        P, _, _ = self.section(y)
        for k in range(3, 13):
            (x0, z0), (x1, z1) = P[k], P[k + 1]
            if z1 > z0 and z0 <= z <= z1:
                return x0 + (x1 - x0) * (z - z0) / (z1 - z0)
        return P[4][0] if z < P[4][1] else P[13][0]

    def surf(self, y, k, t, off=0.0):
        """Point on outline segment k at param t (right side), pushed `off` along the outward normal."""
        P, _, _ = self.section(y)
        (x0, z0), (x1, z1) = P[k], P[k + 1]
        dx, dz = x1 - x0, z1 - z0
        ln = math.hypot(dx, dz) or 1.0
        return (x0 + dx * t + dz / ln * off, z0 + dz * t - dx / ln * off), (dx / ln, dz / ln), (dz / ln, -dx / ln)

    # surface-following decals ---------------------------------------------
    def strip_y(self, a, g, y0, y1, k, t, width, m, off=0.004, n=8, sides=(1, -1)):
        """Band running along the body at outline position (k, t): trims, rub strips, stripes."""
        for s in sides:
            rows = []
            for i in range(n + 1):
                y = y0 + (y1 - y0) * i / n
                (x, z), (dx, dz), (nx, nz) = self.surf(y, k, t, off)
                rows.append([(s * (x - dx * width / 2), y, z - dz * width / 2), (s * (x + dx * width / 2), y, z + dz * width / 2)])
            grid(a, rows, m, g, outward=lambda c, s=s, nx=nx, nz=nz: (s * nx, 0, nz))

    def strip_z(self, a, g, y, z0, z1, width, m, off=0.004, n=4, sides=(1, -1)):
        """Thin vertical line on the side wall (door shut lines)."""
        for s in sides:
            rows = []
            for i in range(n + 1):
                z = z0 + (z1 - z0) * i / n
                x = self.side_x(y, z) + off
                rows.append([(s * x, y - width / 2, z), (s * x, y + width / 2, z)])
            grid(a, rows, m, g, outward=lambda c, s=s: (s, 0, 0))

    def side_patch(self, a, g, y0, y1, z0, z1, m, off=0.004, nu=4, nv=2, sides=(1, -1), taper=0.0):
        """Patch on the side wall (wrap-around lamps, door panels). taper narrows z toward y1."""
        for s in sides:
            rows = []
            for i in range(nu + 1):
                y = y0 + (y1 - y0) * i / nu
                tz = taper * i / nu
                za, zb_ = z0 + (z1 - z0) * tz * 0.5, z1 - (z1 - z0) * tz * 0.5
                rows.append([(s * (self.side_x(y, za + (zb_ - za) * j / nv) + off), y, za + (zb_ - za) * j / nv) for j in range(nv + 1)])
            grid(a, rows, m, g, outward=lambda c, s=s: (s, 0, 0))

    def end_patch(self, a, g, end, x0, x1, z0, z1, m, off=0.004, r=0.02, mirror=True):
        """Flat rounded rectangle on the front (end=0) or rear (end=1) fascia; mirror=True puts one each side."""
        y = self.yf - off if end == 0 else self.yr + off
        for s in ((1, -1) if mirror else (1,)):
            verts = [(s * x, y, z) for x, z in rounded_rect(x0, x1, z0, z1, r, 2)]
            raw(a, verts, [tuple(range(len(verts)))], m, g, smooth=False, outward=lambda c: (0, -1 if end == 0 else 1, 0))

    def across(self, a, g, y, k0, k1, width, m, off=0.006):
        """Band across the body at station y over outline segments k0..k1 (both halves)."""
        pts = []
        for k in range(k0, k1 + 1):
            (x, z), _, _ = self.surf(y, k, 0.0, off)
            pts.append((x, z))
        (x, z), _, _ = self.surf(y, k1, 1.0, off)
        pts.append((x, z))
        full = [(-x, z) for x, z in reversed(pts[:-1])] + pts
        rows = [[(x, y - width / 2, z), (x, y + width / 2, z)] for x, z in full]
        grid(a, rows, m, g, outward=lambda c: (0, 0, 1))


# ----------------------------------------------------------------------------- wheels
def _torus_out(c, rc):
    def f(p):
        rel = p - Vector(c)
        rho = math.hypot(rel.y, rel.z) or 1.0
        return (rel.x, rel.y * (1 - rc / rho), rel.z * (1 - rc / rho))
    return f


def wheel(a, g, c, r, w, side, style="alloy", segs=18):
    """Tyre + rim in their own group (origin at the hub). side = +1: outer face toward +X."""
    a._g(g)
    a.origin(g, c)
    cx, cy, cz = c
    out = lambda p: (side, 0, 0)
    tyre = [(0.64, -0.44), (0.9, -0.5), (1.0, -0.3), (1.0, 0.3), (0.9, 0.5), (0.64, 0.44)]
    lathe(a, [(r * p, w * q) for p, q in tyre], c, side, segs, "tire", g, outward=_torus_out(c, r * 0.8))
    if style == "moto":
        lathe(a, [(r * 0.64, 0.42 * w), (r * 0.6, 0.3 * w), (r * 0.6, -0.3 * w), (r * 0.64, -0.42 * w)], c, side, segs,
              "rim_dark", g, outward=lambda p: (0, cy - p.y, cz - p.z))
        for k in range(5):
            ang = 72 * k
            a.box((0.022, 0.03, r * 0.5), (cx, cy - math.sin(R(ang)) * r * 0.36, cz + math.cos(R(ang)) * r * 0.36),
                  "rim_dark", rot=(ang, 0, 0), g=g)
        a.cyl(r * 0.17, w * 1.3, c, "engine", rot=(0, 90, 0), seg=12, g=g)
        return
    lathe(a, [(r * 0.64, -0.4 * w), (r * 0.08, -0.3 * w)], c, side, segs, "underbody", g, outward=lambda p: (-side, 0, 0), smooth=False)
    lathe(a, [(r * 0.64, 0.42 * w), (r * 0.6, 0.46 * w), (r * 0.57, 0.36 * w)], c, side, segs,
          "rim" if style != "bus" else "rim_dark", g, outward=out)
    dish = {"alloy": 0.2, "steel": 0.3, "bus": 0.1}[style]
    lathe(a, [(r * 0.57, 0.36 * w), (r * 0.53, dish * w), (r * 0.18, dish * w)], c, side, segs,
          "rim_dark" if style == "alloy" else "rim", g, outward=out, smooth=False)
    if style == "alloy":
        for k in range(5):
            ang = 72 * k + 18
            rm = r * 0.37
            a.box((0.06 * w + 0.008, r * 0.13, r * 0.4), (cx + side * 0.3 * w, cy - math.sin(R(ang)) * rm, cz + math.cos(R(ang)) * rm),
                  "rim", rot=(ang, 0, 0), g=g)
        a.cyl(r * 0.19, 0.16 * w, (cx + side * 0.32 * w, cy, cz), "rim", rot=(0, 90, 0), seg=12, g=g)
        a.cyl(r * 0.08, 0.17 * w, (cx + side * 0.34 * w, cy, cz), "chrome", rot=(0, 90, 0), seg=8, g=g)
    elif style == "steel":
        for k in range(6):
            ang = 60 * k + 30
            a.cyl(r * 0.07, 0.01, (cx + side * (dish * w + 0.004), cy - math.sin(R(ang)) * r * 0.38, cz + math.cos(R(ang)) * r * 0.38),
                  "rim_dark", rot=(0, 90, 0), seg=8, g=g)
        a.cyl(r * 0.17, 0.08 * w, (cx + side * (dish + 0.03) * w, cy, cz), "rim_dark", rot=(0, 90, 0), seg=10, g=g)
        for k in range(5):
            ang = 72 * k
            a.cyl(r * 0.025, 0.05, (cx + side * (dish + 0.06) * w, cy - math.sin(R(ang)) * r * 0.11, cz + math.cos(R(ang)) * r * 0.11),
                  "chrome", rot=(0, 90, 0), seg=5, g=g)
    else:  # bus: deep dish, big hub with a ring of nuts
        a.cyl(r * 0.24, 0.3 * w, (cx + side * 0.18 * w, cy, cz), "rim", rot=(0, 90, 0), seg=12, g=g)
        a.cyl(r * 0.12, 0.34 * w, (cx + side * 0.2 * w, cy, cz), "chrome", rot=(0, 90, 0), seg=10, g=g)
        for k in range(8):
            ang = 45 * k
            a.cyl(r * 0.025, 0.04, (cx + side * 0.3 * w, cy - math.sin(R(ang)) * r * 0.19, cz + math.cos(R(ang)) * r * 0.19),
                  "chrome", rot=(0, 90, 0), seg=5, g=g)


def seat(root, name, loc, rz=0.0):
    """Empty marking where a seated character goes (front -Y)."""
    e = bpy.data.objects.new(name, None)
    e.empty_display_size = 0.2
    SCENE.collection.objects.link(e)
    e.parent = root
    e.location = loc
    e.rotation_euler = (0, 0, R(rz))
    return e


def door_mirrors(a, g, x, y, z, m_body):
    """Door mirrors (both sides): stalk + rounded housing + glass."""
    for s in (1, -1):
        a.box((0.1, 0.05, 0.035), (s * (x + 0.035), y, z - 0.04), "trim_black", bevel=0.012, g=g)
        a.box((0.18, 0.08, 0.115), (s * (x + 0.13), y + 0.02, z), m_body, rot=(0, 0, s * -6), bevel=0.03, g=g)
        a.box((0.15, 0.01, 0.09), (s * (x + 0.13), y + 0.066, z), "mirror_glass", rot=(0, 0, s * -6), g=g)


def _in(y, zones):
    return any(z0 <= y <= z1 for z0, z1 in zones)


# ----------------------------------------------------------------------------- sedan taxi
def build_taxi(name, scheme):
    """Bangkok taxi-meter (Corolla-class sedan). scheme: 'pink' or 'greenyellow' (owner-driver colours)."""
    lower, upper, roofc = {"pink": ("paint_pink", "paint_pink", "paint_pink"),
                           "greenyellow": ("paint_green", "paint_yellow", "paint_yellow")}[scheme]
    a = Asset(name)
    g = "Body"
    a._g(g)
    a.name_default = g
    yf, yr, r = -2.27, 2.27, 0.31
    yfa, yra = -1.37, 1.25
    ws, rear = (-0.8, 0.04), (0.98, 1.6)
    glass = [(-0.66, 0.1), (0.19, 1.06)]
    body = CarBody(yf, yr, 0.875,
                   bot=[(yf, 0.27), (yf + 0.45, 0.205), (yr - 0.5, 0.205), (yr, 0.31)],
                   belt=[(yf, 0.74), (yf + 0.6, 0.83, "out"), (-0.8, 0.91), (1.0, 0.965), (yr, 0.97)],
                   top=[(yf, 0.78), (yf + 0.6, 0.865, "out"), (-0.82, 0.945), (0.04, 1.44, "out"), (0.5, 1.47),
                        (0.98, 1.43), (1.6, 1.03, "in"), (2.0, 1.005), (yr, 0.99)],
                   roof_hw=[(yf, 0.66), (-0.92, 0.74), (-0.8, 0.72), (0.04, 0.63), (0.98, 0.63), (1.6, 0.7), (1.75, 0.74), (yr, 0.66)],
                   plan_r=(0.36, 0.3), end_r=(0.07, 0.06), wheels=[yfa, yra], wheel_r=r, arch_pad=0.055, roof_r=0.075,
                   crown=0.03)

    def skin(y, tag, well):
        cabin = ws[0] <= y <= rear[1]
        if well:
            return "trim_black"
        if tag == "liner":
            return "underbody"
        if tag in ("bcorner", "side", "shoulder"):
            return lower
        if tag in ("seal", "gh"):
            if _in(y, glass):
                return "trim_black" if tag == "seal" else "car_glass"
            return upper if cabin else lower
        if tag == "rcorner":
            return upper if cabin else lower
        if _in(y, (ws, rear)):
            return "car_glass"
        return roofc if cabin else lower

    extra = [ws[0], ws[1], rear[0], rear[1]] + [v for z in glass for v in z] + [0.5]
    body.build(a, g, skin, cap_front=lower, cap_rear=lower, extra=extra)

    # front: grille with a chrome bar, headlamps wrapping round the corners, lower intake, fogs, plate
    Hf = body.half_width(yf)
    body.end_patch(a, g, 0, -0.34, 0.34, 0.56, 0.645, "trim_black", r=0.03, mirror=False)
    body.end_patch(a, g, 0, -0.3, 0.3, 0.625, 0.64, "chrome", off=0.009, r=0.005, mirror=False)
    body.end_patch(a, g, 0, 0.37, Hf - 0.004, 0.565, 0.665, "lamp_lens", r=0.035)
    body.end_patch(a, g, 0, 0.43, 0.5, 0.585, 0.645, "chrome", off=0.008, r=0.028)
    body.side_patch(a, g, yf + 0.004, yf + 0.17, 0.575, 0.66, "lamp_lens", taper=0.55, nu=3)
    body.end_patch(a, g, 0, -0.46, 0.46, 0.3, 0.4, "trim_black", r=0.04, mirror=False)
    body.end_patch(a, g, 0, 0.55, 0.67, 0.32, 0.38, "lamp_lens", r=0.028)
    plate(a, g, (0, yf - 0.012, 0.48), -1, "plate_yellow", "ทข 4721")
    # rear: wrap-around tail lamps, reflector strip, bumper lip, plate, exhaust
    Hr = body.half_width(yr)
    body.end_patch(a, g, 1, 0.38, Hr - 0.004, 0.79, 0.9, "lamp_tail", r=0.03)
    body.end_patch(a, g, 1, 0.44, 0.56, 0.8, 0.84, "lamp_lens", off=0.008, r=0.015)
    body.side_patch(a, g, yr - 0.2, yr - 0.004, 0.8, 0.9, "lamp_tail", nu=3)
    body.end_patch(a, g, 1, -0.6, 0.6, 0.33, 0.42, "trim_black", r=0.03, mirror=False)
    body.end_patch(a, g, 1, -0.3, 0.3, 0.86, 0.875, "chrome", r=0.006, mirror=False)
    plate(a, g, (0, yr + 0.012, 0.64), 1, "plate_yellow", "ทข 4721")
    a.cyl(0.03, 0.12, (0.45, yr - 0.03, 0.27), "chrome", rot=(90, 0, 0), seg=8, g=g)
    # side: shut lines, handles, mirrors, door lettering
    for y in (-0.68, 0.15, 1.02):
        body.strip_z(a, g, y, 0.3, 0.9, 0.006, "trim_black")
    body.strip_y(a, g, -0.68, 1.02, 4, 0.35, 0.005, "trim_black", n=8)
    for y in (-0.26, 0.62):
        for s in (1, -1):
            x = body.side_x(y, 0.86) + 0.012
            a.box((0.025, 0.13, 0.03), (s * x, y, 0.86), "chrome", bevel=0.008, g=g)
    door_mirrors(a, g, body.side_x(-0.64, 0.97), -0.64, 0.99, upper)
    for s in (1, -1):
        x = body.side_x(0.6, 0.62) + 0.003
        a.text("TAXI-METER", 0.085, (s * x, 0.6, 0.62), "white" if scheme != "pink" else "black",
               rot=(90, 0, 90 * s), extrude=0.0, fnt=FONT_TH_BOLD, g=g, res=1)
    # wipers, roof sign, antenna
    body.across(a, g, -0.72, 15, 16, 0.016, "trim_black", off=0.008)
    a.box((0.66, 0.26, 0.2), (0, 0.42, 1.585), "taxi_sign", bevel=0.05, g=g)
    a.box((0.7, 0.3, 0.035), (0, 0.42, 1.485), "trim_black", bevel=0.01, g=g)
    for fy, rz in ((0.42 - 0.131, 0), (0.42 + 0.131, 180)):
        a.text("TAXI-METER", 0.07, (0, fy, 1.6), "plate_ink", rot=(90, 0, rz), extrude=0.0, fnt=FONT_TH_BOLD, g=g, res=1)
    a.tube((0.15, 1.15, 1.43), (0.15, 1.3, 1.6), 0.004, "trim_black", g=g)
    xw = 0.765
    for nm, (x, y) in {"Wheel_FL": (xw, yfa), "Wheel_FR": (-xw, yfa), "Wheel_RL": (xw, yra), "Wheel_RR": (-xw, yra)}.items():
        wheel(a, nm, (x, y, r), r, 0.2, 1 if x > 0 else -1, "alloy")
    return a.build()


# ----------------------------------------------------------------------------- pickup (+ songthaew)
def _pickup_body(a, g, paint, bed_top=1.2):
    """Hilux / D-Max class extended-cab pickup with an open bed."""
    yf, yr, r = -2.65, 2.65, 0.36
    yfa, yra = -1.62, 1.45
    cab_end = 0.38
    ws = (-1.22, -0.55)
    glass = [(-1.08, -0.12), (-0.03, 0.27)]
    bed0 = cab_end + 0.08
    body = CarBody(yf, yr, 0.9,
                   bot=[(yf, 0.46), (yf + 0.5, 0.4), (yr - 0.45, 0.42), (yr, 0.46)],
                   belt=[(yf, 0.95), (yf + 0.7, 1.02, "out"), (ws[0], 1.06), (cab_end, 1.1), (bed0, bed_top), (yr, bed_top)],
                   top=[(yf, 0.99), (yf + 0.7, 1.07, "out"), (ws[0] - 0.02, 1.1), (ws[1], 1.72, "out"), (cab_end - 0.08, 1.75),
                        (cab_end - 0.01, 1.7), (bed0, bed_top + 0.015, "in"), (yr, bed_top + 0.015)],
                   roof_hw=[(yf, 0.74), (ws[0] - 0.1, 0.76), (ws[0], 0.74), (ws[1], 0.68), (cab_end, 0.68),
                            (bed0, 0.8), (yr, 0.8)],
                   plan_r=(0.16, 0.08), end_r=(0.06, 0.03), wheels=[yfa, yra], wheel_r=r, arch_pad=0.07,
                   tuck=0.03, bulge=0.012, roof_r=0.07, crown=0.02)

    def skin(y, tag, well):
        if well:
            return "trim_black"
        if tag == "liner":
            return "underbody"
        if tag == "bcorner":
            return "plastic_grey"
        if tag in ("side", "shoulder"):
            return paint
        if tag in ("seal", "gh"):
            if _in(y, glass):
                return "trim_black" if tag == "seal" else "car_glass"
            return paint
        if tag == "roof" and ws[0] <= y <= ws[1]:
            return "car_glass"
        if tag == "roof" and cab_end - 0.06 < y < bed0 - 0.02:
            return "car_glass"                        # cab back window
        return paint

    def skip(y, tag):
        return y > bed0 + 0.01 and tag in ("rcorner", "roof")
    extra = [ws[0], ws[1], cab_end - 0.06, cab_end, bed0, bed0 + 0.03] + [v for z in glass for v in z]
    body.build(a, g, skin, skip=skip, cap_front=paint, cap_rear=paint, extra=extra)
    zf = 0.86
    xin = body.section(1.5)[0][13][0]
    a.bmm(-xin, xin, bed0 + 0.02, yr - 0.05, zf - 0.02, zf, "bed_liner", g=g)
    for s in (1, -1):
        a.bmm(min(s * xin, s * (xin - 0.03)), max(s * xin, s * (xin - 0.03)), bed0 + 0.02, yr - 0.05, zf, bed_top, "bed_liner", g=g)
        for yy in (0.9, 1.6, 2.3):
            a.bmm(min(s * (xin - 0.03), s * (xin - 0.07)), max(s * (xin - 0.03), s * (xin - 0.07)), yy - 0.03, yy + 0.03, zf, bed_top - 0.05, "bed_liner", g=g)
    a.bmm(-xin, xin, yr - 0.07, yr - 0.03, zf, bed_top, "bed_liner", g=g)
    a.bmm(-xin, xin, bed0, bed0 + 0.03, zf, bed_top, "bed_liner", g=g)
    # front: tall grille with chrome bars, headlamps, bumper + intake, plate
    Hf = body.half_width(yf)
    body.end_patch(a, g, 0, -0.46, 0.46, 0.68, 0.9, "trim_black", r=0.04, mirror=False)
    for z in (0.73, 0.79, 0.85):
        body.end_patch(a, g, 0, -0.43, 0.43, z, z + 0.018, "chrome", off=0.01, r=0.006, mirror=False)
    body.end_patch(a, g, 0, 0.5, Hf - 0.004, 0.75, 0.9, "lamp_lens", r=0.04)
    body.side_patch(a, g, yf + 0.004, yf + 0.15, 0.76, 0.89, "lamp_lens", taper=0.4, nu=3)
    body.end_patch(a, g, 0, -(Hf - 0.01), Hf - 0.01, 0.48, 0.64, "plastic_grey", off=0.006, r=0.05, mirror=False)
    body.end_patch(a, g, 0, -0.32, 0.32, 0.5, 0.6, "trim_black", off=0.012, r=0.03, mirror=False)
    plate(a, g, (0, yf - 0.022, 0.65), -1, "plate_white", "4กข 902")
    # rear: tall tail lamps on the bed corners, step bumper, plate
    Hr = body.half_width(yr)
    body.end_patch(a, g, 1, Hr - 0.12, Hr - 0.004, 0.85, 1.12, "lamp_tail", r=0.02)
    body.end_patch(a, g, 1, -0.55, 0.55, 1.08, 1.13, "trim_black", r=0.01, mirror=False)
    a.box((1.7, 0.16, 0.12), (0, yr + 0.06, 0.5), "chrome", bevel=0.03, g=g)
    plate(a, g, (0, yr + 0.012, 0.72), 1, "plate_white", "4กข 902")
    # side: shut lines, handles, mirrors, step
    for y in (-1.12, -0.1, 0.32):
        body.strip_z(a, g, y, 0.48, 1.06, 0.006, "trim_black")
    for s in (1, -1):
        x = body.side_x(-0.36, 0.98) + 0.012
        a.box((0.025, 0.13, 0.03), (s * x, -0.36, 0.98), "trim_black", bevel=0.008, g=g)
        a.box((0.12, 1.3, 0.05), (s * 0.84, -0.45, 0.38), "plastic_grey", bevel=0.015, g=g)
    door_mirrors(a, g, body.side_x(-1.06, 1.1), -1.06, 1.14, "trim_black")
    body.across(a, g, -1.14, 15, 16, 0.016, "trim_black", off=0.008)
    for nm, (x, y) in {"Wheel_FL": (0.78, yfa), "Wheel_FR": (-0.78, yfa), "Wheel_RL": (0.78, yra), "Wheel_RR": (-0.78, yra)}.items():
        wheel(a, nm, (x, y, r), r, 0.23, 1 if x > 0 else -1, "steel" if paint != "paint_white" else "alloy")
    return body, yf, yr, r, bed0, xin, zf


def build_pickup():
    """White extended-cab pickup carrying sacks and a crate (the Thai workhorse)."""
    a = Asset("SM_Car_Pickup")
    g = "Body"
    a._g(g)
    a.name_default = g
    body, yf, yr, r, bed0, xin, zf = _pickup_body(a, g, "paint_white")
    for i, (x, y, rot) in enumerate([(-0.35, 1.2, 5), (0.3, 1.3, -8), (-0.1, 1.95, 12)]):
        a.box((0.5, 0.36, 0.22), (x, y, zf + 0.11), "cardboard" if i == 2 else "white", rot=(0, 0, rot), bevel=0.06, g=g)
    a.box((0.55, 0.45, 0.3), (0.35, 2.2, zf + 0.15), "blue_plastic", bevel=0.02, g=g)
    return a.build()


def build_songthaew():
    """Red songthaew: pickup with a roofed bed, two benches facing each other, grab rails and a rear step."""
    a = Asset("SM_Car_Songthaew")
    g = "Body"
    a._g(g)
    a.name_default = g
    body, yf, yr, r, bed0, xin, zf = _pickup_body(a, g, "paint_red", bed_top=1.24)
    zr = 2.15
    a.bmm(-0.95, 0.95, bed0 - 0.05, yr + 0.06, zr, zr + 0.05, "paint_red", bevel=0.02, g=g)
    a.bmm(-0.92, 0.92, bed0 - 0.02, yr + 0.03, zr - 0.012, zr, "white", g=g)
    a.bmm(-0.96, 0.96, bed0 - 0.07, bed0 + 0.05, zr - 0.25, zr + 0.06, "paint_red", bevel=0.02, g=g)
    a.text("ลาดพร้าว - สะพานควาย", 0.1, (0, bed0 - 0.075, zr - 0.1), "white", extrude=0.0, g=g, res=1)
    for y in (bed0 + 0.05, (bed0 + yr) / 2, yr - 0.04):
        for s in (1, -1):
            a.tube((s * (xin - 0.02), y, 1.22), (s * (xin - 0.02), y, zr), 0.022, "chrome", g=g)
    for s in (1, -1):
        a.tube((s * (xin - 0.04), bed0 + 0.05, 1.75), (s * (xin - 0.04), yr - 0.04, 1.75), 0.016, "chrome", g=g)
        a.tube((s * 0.25, bed0 + 0.3, zr - 0.08), (s * 0.25, yr - 0.1, zr - 0.08), 0.014, "chrome", g=g)
        a.bmm(min(s * (xin - 0.42), s * (xin - 0.05)), max(s * (xin - 0.42), s * (xin - 0.05)), bed0 + 0.1, yr - 0.15, zf + 0.36, zf + 0.42, "seat_red", g=g)
        a.bmm(min(s * (xin - 0.08), s * (xin - 0.03)), max(s * (xin - 0.08), s * (xin - 0.03)), bed0 + 0.1, yr - 0.15, zf + 0.42, zf + 0.75, "seat_red", g=g)
        a.bmm(min(s * (xin - 0.4), s * (xin - 0.38)), max(s * (xin - 0.4), s * (xin - 0.38)), bed0 + 0.1, yr - 0.15, zf, zf + 0.36, "steel_dark", g=g)
    a.box((1.0, 0.32, 0.05), (0, yr + 0.2, 0.55), "steel", bevel=0.01, g=g)
    for s in (1, -1):
        a.tube((s * 0.55, yr + 0.02, 1.2), (s * 0.55, yr + 0.02, 1.95), 0.018, "chrome", g=g)
    root = a.build()
    for k, (x, y) in enumerate([(0.42, 1.2), (-0.42, 1.65), (0.42, 2.15)]):
        seat(root, f"Seat_Passenger{k}", (x, y, zf + 0.42), 90 if x > 0 else -90)
    return root


# ----------------------------------------------------------------------------- city bus
def build_bus():
    """BMTA air-con city bus (orange), route 8. Doors on the kerb side (+X)."""
    a = Asset("SM_Bus")
    g = "Body"
    a._g(g)
    a.name_default = g
    yf, yr, r = -5.3, 5.3, 0.5
    yfa, yra = -3.0, 2.55
    body = CarBody(yf, yr, 1.25,
                   bot=[(yf, 0.42), (yr, 0.45)],
                   belt=[(yf, 1.35), (yr, 1.38)],
                   top=[(yf, 3.02), (yr, 3.02)],
                   roof_hw=[(yf, 1.1), (yr, 1.1)],
                   plan_r=(0.22, 0.2), end_r=(0.22, 0.2), wheels=[yfa, yra], wheel_r=r, arch_pad=0.07,
                   bulge=0.008, tuck=0.03, crown=0.05, roof_r=0.2, shoulder_r=0.03, bottom_r=0.05, liner_in=0.35, step=0.55)
    pillars = [yf + 1.25 + 1.12 * k for k in range(9)]

    def skin(y, tag, well):
        if well:
            return "trim_black"
        if tag == "liner":
            return "underbody"
        if tag == "bcorner":
            return "trim_black"
        if tag in ("side", "shoulder"):
            return "paint_orange"
        if tag in ("seal", "gh"):
            if yf + 0.4 < y < yr - 0.5 and not any(abs(y - p) < 0.07 for p in pillars):
                return "trim_black" if tag == "seal" else "car_glass"
            return "paint_orange"
        return "paint_white"
    extra = [v for p in pillars for v in (p - 0.07, p + 0.07)] + [yf + 0.4, yr - 0.5]
    body.build(a, g, skin, cap_front="paint_orange", cap_rear="paint_orange", extra=extra)
    body.strip_y(a, g, yf + 0.2, yr - 0.2, 6, 0.5, 0.12, "paint_white", off=0.004, n=10)
    Hf = body.half_width(yf)
    body.end_patch(a, g, 0, -Hf + 0.1, Hf - 0.1, 1.25, 2.55, "car_glass", r=0.08, mirror=False)
    body.end_patch(a, g, 0, -0.012, 0.012, 1.26, 2.54, "trim_black", off=0.009, r=0.004, mirror=False)
    body.end_patch(a, g, 0, -0.85, 0.85, 2.62, 2.86, "trim_black", r=0.03, mirror=False)
    a.text("8   สะพานพุทธ - ท่าน้ำนนท์", 0.12, (0, yf - 0.012, 2.74), "lamp_amber", extrude=0.0, g=g, res=1)
    for s in (1, -1):
        for x in (0.88, 1.0):
            a.cyl(0.055, 0.02, (s * x, yf - 0.008, 0.8), "lamp_lens", rot=(90, 0, 0), seg=12, g=g)
        a.cyl(0.035, 0.02, (s * 1.08, yf - 0.008, 0.95), "lamp_amber", rot=(90, 0, 0), seg=8, g=g)
    body.end_patch(a, g, 0, -1.22, 1.22, 0.42, 0.66, "trim_black", off=0.008, r=0.05, mirror=False)
    body.end_patch(a, g, 0, -1.1, 1.1, 1.0, 1.08, "paint_white", r=0.02, mirror=False)
    a.text("ขสมก.", 0.1, (0, yf - 0.012, 1.15), "white", extrude=0.0, g=g, res=1)
    plate(a, g, (0, yf - 0.02, 0.55), -1, "plate_yellow", "11-4527")
    for s in (1, -1):
        a.box((0.03, 0.012, 0.75), (s * 0.55, yf - 0.02, 1.62), "trim_black", rot=(0, s * 30, 0), g=g)
        a.tube((s * 1.22, yf + 0.25, 2.55), (s * 1.38, yf - 0.25, 2.5), 0.02, "trim_black", g=g)
        a.tube((s * 1.38, yf - 0.25, 2.5), (s * 1.36, yf - 0.32, 2.1), 0.02, "trim_black", g=g)
        a.box((0.06, 0.25, 0.42), (s * 1.36, yf - 0.32, 1.95), "trim_black", bevel=0.02, g=g)
    body.end_patch(a, g, 1, -0.9, 0.9, 0.7, 1.15, "trim_black", r=0.04, mirror=False)
    for z in (0.78, 0.88, 0.98, 1.08):
        body.end_patch(a, g, 1, -0.86, 0.86, z, z + 0.03, "plastic_grey", off=0.008, r=0.008, mirror=False)
    body.end_patch(a, g, 1, -1.1, 1.1, 1.6, 2.6, "car_glass", r=0.08, mirror=False)
    for s in (1, -1):
        for z, m in ((1.25, "lamp_tail"), (1.4, "lamp_amber")):
            a.cyl(0.06, 0.02, (s * 1.02, yr + 0.008, z), m, rot=(90, 0, 0), seg=12, g=g)
    plate(a, g, (0, yr + 0.02, 0.6), 1, "plate_yellow", "11-4527")
    for y0, y1 in ((yf + 0.55, yf + 1.5), (0.95, 1.9)):
        body.side_patch(a, g, y0, y1, 0.48, 2.55, "trim_black", off=0.006, nu=2, nv=4, sides=(1,))
        for k in range(2):
            ya, yb = y0 + (y1 - y0) * k / 2 + 0.05, y0 + (y1 - y0) * (k + 1) / 2 - 0.05
            body.side_patch(a, g, ya, yb, 0.65, 2.45, "car_glass", off=0.012, nu=1, nv=3, sides=(1,))
    body.side_patch(a, g, yf + 1.65, yf + 2.5, 2.05, 2.3, "white", off=0.012, nu=1, nv=1, sides=(1,))
    a.text("8", 0.2, (body.side_x(yf + 2.07, 2.18) + 0.014, yf + 2.07, 2.18), "red", rot=(90, 0, 90), extrude=0.0, g=g, res=1)
    a.bmm(-0.8, 0.8, -1.6, 1.4, 3.02, 3.3, "paint_white", bevel=0.08, g=g)
    for y in (-1.2, -0.4, 0.4, 1.0):
        a.cyl(0.18, 0.02, (0, y, 3.31), "plastic_grey", seg=12, g=g)
    xw = 1.0
    for nm, (x, y) in {"Wheel_FL": (xw, yfa), "Wheel_FR": (-xw, yfa), "Wheel_RL": (xw, yra), "Wheel_RR": (-xw, yra)}.items():
        wheel(a, nm, (x, y, r), r, 0.3, 1 if x > 0 else -1, "bus")
    return a.build()


# ----------------------------------------------------------------------------- tuk-tuk
def _arch_ring(y, w, z0, z1, n=12, box=0.55):
    """Arched cross-section (open at the bottom) from +x over the top to -x; box < 1 squares it off."""
    ring = []
    for k in range(n + 1):
        t = math.pi * k / n
        c, s_ = math.cos(t), math.sin(t)
        ring.append((math.copysign(abs(c) ** 0.8, c) * w, y, z0 + (z1 - z0) * (s_ ** box)))
    return ring


def _cap(a, g, ring, m, facing):
    raw(a, ring, [tuple(range(len(ring)))], m, g, smooth=False, outward=lambda c: (0, facing, 0))


def build_tuktuk(name="SM_TukTuk", driver=False):
    """Bangkok tuk-tuk: single front wheel under a chromed cowl, windscreen, canopy roof on chrome posts,
    driver's saddle, red vinyl passenger bench over the rear tub (engine), rear wheels under fenders."""
    a = Asset(name)
    g = "Body"
    a._g(g)
    a.name_default = g
    body_c, roof_c, trim = "paint_blue", "paint_yellow", "chrome"
    a.bmm(-0.09, 0.09, -1.1, 0.3, 0.3, 0.4, "underbody", g=g)
    a.bmm(-0.58, 0.58, -0.22, 0.32, 0.38, 0.43, "trim_black", g=g)
    for s in (1, -1):
        a.bmm(min(s * 0.58, s * 0.63), max(s * 0.58, s * 0.63), -0.22, 0.32, 0.38, 0.6, body_c, bevel=0.01, g=g)
        a.tube((s * 0.635, -0.22, 0.6), (s * 0.635, 0.3, 0.6), 0.012, trim, g=g)
    cow = [(-1.42, 0.17, 0.58, 0.97), (-1.37, 0.25, 0.52, 1.02), (-1.25, 0.31, 0.46, 1.05), (-1.05, 0.35, 0.44, 1.05), (-0.8, 0.36, 0.44, 1.02)]
    rings = [_arch_ring(y, w, z0, z1) for y, w, z0, z1 in cow]
    grid(a, rings, body_c, g, outward=lambda c: (c.x, -0.3, c.z - 0.62))
    _cap(a, g, rings[0], body_c, -1)
    _cap(a, g, rings[-1], "trim_black", 1)
    for k in (2, 10):
        a.polyline([ring[k] for ring in rings], 0.008, trim, seg=5, g=g)
    a.cyl(0.105, 0.05, (0, -1.43, 0.8), "chrome", rot=(90, 0, 0), seg=16, g=g)
    a.cyl(0.088, 0.02, (0, -1.46, 0.8), "lamp_lens", rot=(90, 0, 0), seg=16, g=g)
    for s in (1, -1):
        a.sphere(0.032, (s * 0.2, -1.36, 0.96), "lamp_amber", scale=(1, 0.6, 1), seg=8, rings=5, g=g)
    a.box((0.36, 0.012, 0.12), (0, -1.425, 0.63), "plate_yellow", g=g)
    a.text("ตุ๊กตุ๊ก 1234", 0.04, (0, -1.433, 0.64), "plate_ink", extrude=0.0, g=g, res=1)
    for s in (1, -1):
        a.tube((s * 0.08, -1.15, 0.24), (s * 0.08, -1.08, 0.62), 0.022, "chrome", g=g)
    arc_loft(a, g, (0, -1.15, 0.24), 0.28, 25, 150, 0.15, 0.012, body_c, n=7)
    a.box((0.68, 0.015, 0.44), (0, -0.93, 1.26), "glass", rot=(-16, 0, 0), g=g)
    for s in (1, -1):
        a.tube((s * 0.34, -0.87, 1.03), (s * 0.34, -0.99, 1.47), 0.014, trim, g=g)
    a.tube((-0.34, -0.99, 1.47), (0.34, -0.99, 1.47), 0.012, trim, g=g)
    a.tube((0.05, -0.88, 1.06), (-0.22, -0.95, 1.3), 0.006, "trim_black", g=g)
    a.polyline([(-0.38, -0.7, 1.06), (-0.2, -0.77, 1.0), (0.2, -0.77, 1.0), (0.38, -0.7, 1.06)], 0.014, trim, g=g)
    for s in (1, -1):
        a.cyl(0.02, 0.11, (s * 0.4, -0.69, 1.07), "trim_black", rot=(0, 90, 0), seg=8, g=g)
    a.tube((0, -0.79, 0.62), (0, -0.77, 1.0), 0.03, "steel_dark", g=g)
    a.bmm(-0.24, 0.24, -0.52, -0.12, 0.62, 0.76, "seat_vinyl", bevel=0.04, g=g)
    a.bmm(-0.1, 0.1, -0.44, -0.2, 0.43, 0.62, "underbody", g=g)
    a.bmm(-0.42, 0.42, -0.14, -0.08, 0.62, 1.12, body_c, bevel=0.02, g=g)
    a.bmm(-0.24, 0.24, -0.16, -0.14, 0.78, 1.04, "seat_vinyl", bevel=0.01, g=g)
    prof = [(0.46, 0.42), (0.5, 0.5), (0.66, 0.56), (0.72, 0.62), (0.73, 0.72), (0.7, 0.82), (0.64, 0.85)]
    rings = []
    for y, sc, dz in ((0.3, 1.0, 0.0), (0.34, 1.0, 0.0), (0.55, 1.0, 0.0), (0.8, 1.0, 0.0), (1.0, 1.0, 0.0), (1.08, 0.96, 0.01), (1.12, 0.88, 0.03)):
        right = [(x * sc, y, z + (dz if z < 0.5 else -dz * 0.3)) for x, z in prof]
        rings.append(right + [(-x, y, z) for x, y, z in reversed(right)])
    grid(a, rings, body_c, g, outward=lambda c: (c.x, 0.15 * (c.y - 0.7), c.z - 0.62))
    _cap(a, g, rings[0], body_c, -1)
    _cap(a, g, rings[-1], body_c, 1)
    for s in (1, -1):
        a.tube((s * 0.735, 0.32, 0.68), (s * 0.735, 1.04, 0.68), 0.012, trim, g=g)
        a.tube((s * 0.705, 0.32, 0.81), (s * 0.705, 1.06, 0.81), 0.009, trim, g=g)
        arc_loft(a, g, (0, 0.72, 0.24), 0.275, 15, 165, 0.18, 0.01, "trim_black", n=7, x0=s * 0.585)
    a.bmm(-0.62, 0.62, 0.86, 1.12, 0.85, 1.1, body_c, bevel=0.03, g=g)
    a.text("TUK TUK", 0.12, (0, 1.125, 0.98), roof_c, rot=(90, 0, 180), extrude=0.0, g=g, res=1)
    a.box((0.32, 0.012, 0.12), (0, 1.13, 0.6), "plate_yellow", g=g)
    a.text("กทม 7788", 0.045, (0, 1.138, 0.61), "plate_ink", rot=(90, 0, 180), extrude=0.0, g=g, res=1)
    for s in (1, -1):
        a.cyl(0.05, 0.02, (s * 0.48, 1.125, 0.72), "lamp_tail", rot=(90, 0, 0), seg=10, g=g)
        a.cyl(0.03, 0.02, (s * 0.48, 1.125, 0.58), "lamp_amber", rot=(90, 0, 0), seg=8, g=g)
    a.cyl(0.03, 0.14, (0.3, 1.14, 0.45), "chrome", rot=(90, 0, 0), seg=8, g=g)
    a.bmm(-0.62, 0.62, 0.32, 0.86, 0.85, 0.98, "seat_red", bevel=0.04, g=g)
    a.bmm(-0.62, 0.62, 0.78, 0.9, 0.98, 1.34, "seat_red", bevel=0.04, g=g)
    for x in (-0.31, 0.0, 0.31):
        a.bmm(x - 0.004, x + 0.004, 0.775, 0.78, 1.0, 1.32, "seat_vinyl", g=g)
    for s in (1, -1):
        a.polyline([(s * 0.68, 0.32, 0.86), (s * 0.7, 0.32, 1.12), (s * 0.7, 0.84, 1.12)], 0.016, trim, g=g)
    rings = []
    for y, z in ((-1.04, 1.6), (-0.92, 1.74), (-0.6, 1.8), (0.0, 1.81), (0.6, 1.8), (1.0, 1.77), (1.2, 1.66)):
        hw = 0.78 if -0.92 <= y <= 1.0 else 0.72
        rings.append([(-hw + 2 * hw * k / 10, y, z + 0.06 * (1 - ((-hw + 2 * hw * k / 10) / hw) ** 2)) for k in range(11)])
    grid(a, rings, roof_c, g, outward=lambda c: (0, 0, 1))
    under = [[(p[0], p[1], p[2] - 0.035) for p in ring] for ring in rings]
    grid(a, under, "plastic_grey", g, outward=lambda c: (0, 0, -1))
    grid(a, [rings[0], under[0]], roof_c, g, outward=lambda c: (0, -1, 0))
    grid(a, [rings[-1], under[-1]], roof_c, g, outward=lambda c: (0, 1, 0))
    for k, sx in ((0, -1), (10, 1)):
        grid(a, [[ring[k] for ring in rings], [ring[k] for ring in under]], roof_c, g, outward=lambda c, sx=sx: (sx, 0, 0))
        a.polyline([(ring[k][0] * 1.005, ring[k][1], ring[k][2] - 0.018) for ring in rings], 0.01, trim, seg=5, g=g)
    for i in range(20):
        x = -0.74 + i * 0.078
        a.bmm(x, x + 0.04, -1.06, -1.05, 1.48, 1.58, "red" if i % 2 else "white", g=g)
    for (x, y, z0) in [(-0.7, 1.02, 0.85), (0.7, 1.02, 0.85), (-0.72, -0.12, 0.6), (0.72, -0.12, 0.6), (-0.34, -0.84, 1.02), (0.34, -0.84, 1.02)]:
        a.tube((x, y, z0), (x * 1.04, y, 1.79), 0.02, trim, g=g)
    pts = [(0.02 * math.sin(i), -0.98, 1.56 - i * 0.03) for i in range(8)]
    for i, p in enumerate(pts):
        a.sphere(0.016, p, "marigold" if i % 3 else "jasmine", seg=6, rings=4, g=g)
    wheel(a, "Wheel_F", (0, -1.15, 0.24), 0.24, 0.13, 1, "steel", segs=16)
    for nm, x in (("Wheel_RL", 0.585), ("Wheel_RR", -0.585)):
        wheel(a, nm, (x, 0.72, 0.24), 0.24, 0.14, 1 if x > 0 else -1, "steel", segs=16)
    root = a.build()
    if driver:
        seat(root, "Seat_Driver", (0, -0.32, 0.76))
    return root


# ----------------------------------------------------------------------------- motorbike
def build_scooter(name="SM_Scooter", rider=False):
    """Honda Wave-style underbone moped: headlamp in the bar shroud, leg shield, step-through tunnel,
    long dual seat over side covers, horizontal engine, twin shocks, muffler, rack and grab rail."""
    a = Asset(name)
    g = "Body"
    a._g(g)
    a.name_default = g
    paint, dark = "paint_red", "trim_black"
    yf_, yr_, r = -0.62, 0.6, 0.29
    # fork: chrome stanchions into dark sliders, raked
    for s in (1, -1):
        a.tube((s * 0.065, -0.45, 0.86), (s * 0.065, -0.56, 0.55), 0.019, "chrome", g=g)
        a.tube((s * 0.065, -0.56, 0.57), (s * 0.065, yf_, r), 0.026, "steel_dark", g=g)
    arc_loft(a, g, (0, yf_, r), 0.325, 20, 150, 0.105, 0.012, paint, n=8)
    # handlebar shroud with the headlamp, bars, grips, mirrors, indicators
    shroud = [ring_y(-0.64, 0.07, 0.92, 0.99, 0.03), ring_y(-0.6, 0.15, 0.9, 1.01, 0.045), ring_y(-0.5, 0.19, 0.91, 1.03, 0.05),
              ring_y(-0.4, 0.15, 0.94, 1.04, 0.04), ring_y(-0.36, 0.1, 0.97, 1.03, 0.025)]
    closed_loft(a, shroud, lambda c, nrm: dark if nrm.y > 0.6 else paint, g)
    a.box((0.17, 0.04, 0.065), (0, -0.635, 0.955), "lamp_lens", bevel=0.02, g=g)
    for s in (1, -1):
        a.sphere(0.022, (s * 0.15, -0.6, 0.945), "lamp_amber", scale=(1.4, 0.8, 0.8), seg=8, rings=5, g=g)
        a.tube((s * 0.16, -0.45, 0.985), (s * 0.33, -0.43, 0.99), 0.012, "steel_dark", g=g)
        a.cyl(0.019, 0.11, (s * 0.38, -0.43, 0.99), dark, rot=(0, 90, 0), seg=8, g=g)
        a.tube((s * 0.3, -0.45, 1.0), (s * 0.36, -0.52, 0.98), 0.006, "steel_dark", g=g)
        a.tube((s * 0.18, -0.44, 1.02), (s * 0.25, -0.43, 1.22), 0.006, "chrome", g=g)
        a.sphere(0.055, (s * 0.26, -0.43, 1.25), dark, scale=(1.1, 0.35, 0.8), seg=10, rings=6, g=g)
    # leg shield (solid, black on the rider's side) + step-through tunnel
    shield = [ring_z(0.42, -0.36, 0.13, 0.12, 0.04), ring_z(0.55, -0.42, 0.19, 0.13, 0.05), ring_z(0.7, -0.46, 0.2, 0.12, 0.05),
              ring_z(0.83, -0.48, 0.14, 0.1, 0.04), ring_z(0.9, -0.47, 0.07, 0.07, 0.025)]
    closed_loft(a, shield, lambda c, nrm: dark if nrm.y > 0.55 else paint, g)
    tunnel = [ring_y(-0.38, 0.07, 0.36, 0.5, 0.03), ring_y(-0.2, 0.08, 0.37, 0.48, 0.035), ring_y(-0.06, 0.1, 0.38, 0.55, 0.04)]
    closed_loft(a, tunnel, paint, g)
    for s in (1, -1):
        a.cyl(0.022, 0.1, (s * 0.18, -0.02, 0.31), dark, rot=(0, 90, 0), seg=8, g=g)
        a.tube((s * 0.06, -0.02, 0.31), (s * 0.13, -0.02, 0.31), 0.012, "steel_dark", g=g)
    # engine: horizontal cylinder with fins, crankcase, covers
    a.box((0.24, 0.3, 0.22), (0, 0.12, 0.31), "engine", bevel=0.05, g=g)
    a.box((0.15, 0.2, 0.13), (0, -0.1, 0.33), "engine", bevel=0.03, g=g)
    for k in range(4):
        a.box((0.17, 0.012, 0.15), (0, -0.16 + k * 0.035, 0.33), "engine", g=g)
    a.cyl(0.1, 0.03, (-0.125, 0.13, 0.31), "rim", rot=(0, 90, 0), seg=12, g=g)
    a.cyl(0.085, 0.03, (0.125, 0.16, 0.31), "engine", rot=(0, 90, 0), seg=12, g=g)
    # body under the seat (solid side covers), tail with lamp
    bodyr = [ring_y(-0.1, 0.09, 0.42, 0.62, 0.04), ring_y(0.0, 0.14, 0.43, 0.72, 0.05), ring_y(0.18, 0.16, 0.46, 0.78, 0.06),
             ring_y(0.42, 0.15, 0.56, 0.8, 0.06), ring_y(0.66, 0.11, 0.66, 0.8, 0.04), ring_y(0.84, 0.05, 0.71, 0.78, 0.02)]
    closed_loft(a, bodyr, lambda c, nrm: dark if c.z < 0.5 else ("white" if abs(nrm.x) > 0.8 and 0.6 < c.z < 0.66 else paint), g)
    seatr = []
    for y, w, h in ((-0.06, 0.08, 0.05), (0.02, 0.125, 0.085), (0.2, 0.14, 0.09), (0.44, 0.13, 0.08), (0.6, 0.1, 0.06), (0.68, 0.07, 0.04)):
        zb = keyed(y, [(-0.1, 0.62), (0.0, 0.72), (0.18, 0.78), (0.42, 0.8), (0.66, 0.8)]) - 0.012
        seatr.append(ring_y(y, w, zb, zb + h, min(0.045, h * 0.6)))
    closed_loft(a, seatr, "seat_vinyl", g)
    a.box((0.14, 0.05, 0.055), (0, 0.85, 0.745), "lamp_tail", bevel=0.02, g=g)
    for s in (1, -1):
        a.sphere(0.018, (s * 0.1, 0.83, 0.72), "lamp_amber", scale=(1.3, 0.8, 0.8), seg=8, rings=5, g=g)
    a.box((0.16, 0.01, 0.1), (0, 0.86, 0.6), "plate_white", rot=(-15, 0, 0), g=g)
    a.text("กทม 123", 0.03, (0, 0.866, 0.61), "plate_ink", rot=(75, 0, 180), extrude=0.0, g=g, res=1)
    a.tube((0, 0.82, 0.7), (0, 0.86, 0.62), 0.01, "steel_dark", g=g)
    arc_loft(a, g, (0, yr_, r), 0.33, 60, 135, 0.1, 0.01, dark, n=5)
    # swingarm, shocks with springs, chain guard, muffler on the right (-X)
    for s in (1, -1):
        a.tube((s * 0.085, 0.24, 0.32), (s * 0.085, yr_, r), 0.022, "steel_dark", g=g)
        a.tube((s * 0.1, 0.54, 0.33), (s * 0.11, 0.44, 0.7), 0.016, "chrome", g=g)
        for k in range(5):
            a.cyl(0.03, 0.01, (s * 0.103, 0.53 - k * 0.019, 0.38 + k * 0.065), "red_plastic", rot=(-15, 0, 0), seg=8, g=g)
    a.box((0.02, 0.36, 0.06), (0.11, 0.42, 0.34), dark, bevel=0.01, g=g)
    a.tube((-0.03, -0.12, 0.25), (-0.13, 0.18, 0.24), 0.022, "chrome", g=g)
    a.cyl(0.055, 0.42, (-0.15, 0.45, 0.3), "rim_dark", rot=(82, 0, 0), r2=0.045, seg=12, g=g)
    a.box((0.012, 0.22, 0.07), (-0.205, 0.38, 0.31), "chrome", rot=(-8, 0, 0), bevel=0.004, g=g)
    # rear rack + grab rail (the delivery box sits on the rack)
    a.bmm(-0.12, 0.12, 0.6, 0.9, 0.84, 0.86, "steel_dark", g=g)
    a.polyline([(-0.14, 0.45, 0.82), (-0.15, 0.78, 0.85), (0.15, 0.78, 0.85), (0.14, 0.45, 0.82)], 0.012, "chrome", g=g)
    if not rider:
        a.tube((0.08, 0.06, 0.26), (0.24, 0.16, 0.01), 0.012, "steel_dark", g=g)      # side stand
    wheel(a, "Wheel_F", (0, yf_, r), r, 0.085, 1, "moto", segs=18)
    wheel(a, "Wheel_R", (0, yr_, r), r, 0.095, 1, "moto", segs=18)
    root = a.build()
    if rider:
        seat(root, "Seat_Driver", (0, 0.1, 0.86))
    return root

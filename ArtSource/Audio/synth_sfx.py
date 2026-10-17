"""Synthesised sound effects for Thai Boba Shop (original, CC0 like the rest of the audio).

Pure Python (no numpy): python ArtSource/Audio/synth_sfx.py
Writes 44.1 kHz mono WAVs into Assets/Audio/ThaiBoba/:
    sfx_blender.wav        ice blender: spin-up, ice crushing, churning, spin-down
    sfx_horn_tuk.wav/2     tuk-tuk electric horn ("pip-peep", triple pip)
    sfx_horn_moto.wav      scooter horn (single meep)
    sfx_tuk_pass.wav       tuk-tuk pass-by: two-stroke putter with doppler, layered on the scooter recording
    sfx_meow.wav/2         stray cat meow / short "mrrp"
    sfx_chop.wav/2         cleaver on a wooden board (chicken-rice vendor)
    sfx_firecracker.wav    a string of firecrackers in the street (Chinese New Year)
    sfx_app_order.wav      delivery-app order chime
    sfx_shake.wav          ice rattling in a cocktail shaker
"""
import array
import math
import os
import random
import wave

SR = 44100
ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
OUT = os.path.join(ROOT, "Assets", "Audio", "ThaiBoba")
TAU = 2 * math.pi


# ----------------------------------------------------------------------------- helpers
def write(name, x, peak=0.85, fade=0.01):
    n = len(x)
    f = int(fade * SR)
    for i in range(min(f, n)):
        k = i / f
        x[i] *= k
        x[n - 1 - i] *= k
    m = max(abs(v) for v in x) or 1.0
    g = peak / m
    data = array.array('h', (int(max(-32767, min(32767, v * g * 32767))) for v in x))
    path = os.path.join(OUT, name)
    tmp = path + ".tmp"
    with wave.open(tmp, 'wb') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    os.replace(tmp, path)
    print(f"{name}: {n / SR:.2f} s")


def read(name):
    with wave.open(os.path.join(OUT, name)) as w:
        assert w.getnchannels() == 1 and w.getsampwidth() == 2
        raw = array.array('h', w.readframes(w.getnframes()))
    return [v / 32768.0 for v in raw]


class Biquad:
    """RBJ cookbook filter: 'lp', 'hp', 'bp' (0 dB peak)."""

    def __init__(self, kind, f, q=0.707):
        self.kind = kind
        self.x1 = self.x2 = self.y1 = self.y2 = 0.0
        self.set(f, q)

    def set(self, f, q=None):
        q = q or getattr(self, "q", 0.707)
        self.q = q
        f = min(f, SR * 0.45)
        w0 = TAU * f / SR
        cw, sw = math.cos(w0), math.sin(w0)
        al = sw / (2 * q)
        if self.kind == 'lp':
            b0, b1, b2 = (1 - cw) / 2, 1 - cw, (1 - cw) / 2
        elif self.kind == 'hp':
            b0, b1, b2 = (1 + cw) / 2, -(1 + cw), (1 + cw) / 2
        else:
            b0, b1, b2 = al, 0.0, -al
        a0, a1, a2 = 1 + al, -2 * cw, 1 - al
        self.b0, self.b1, self.b2, self.a1, self.a2 = b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0

    def __call__(self, x):
        y = self.b0 * x + self.b1 * self.x1 + self.b2 * self.x2 - self.a1 * self.y1 - self.a2 * self.y2
        self.x2, self.x1, self.y2, self.y1 = self.x1, x, self.y1, y
        return y


def onepole(cut):
    a = math.exp(-TAU * cut / SR)
    state = [0.0]

    def f(x):
        state[0] = (1 - a) * x + a * state[0]
        return state[0]
    return f


def add_event(buf, start, gen, length):
    i0 = int(start * SR)
    for j in range(int(length * SR)):
        i = i0 + j
        if i >= len(buf):
            break
        buf[i] += gen(j / SR)


def smooth_noise(rnd, rate, n):
    """Band-limited random wobble in [-1, 1] at about `rate` Hz (linear interpolation of random points)."""
    step = int(SR / rate)
    pts = [rnd.uniform(-1, 1) for _ in range(n // step + 3)]
    out = []
    for i in range(n):
        k, f = divmod(i, step)
        u = f / step
        u = u * u * (3 - 2 * u)
        out.append(pts[k] * (1 - u) + pts[k + 1] * u)
    return out


def reverb(x, wet=0.2, room=0.72):
    """Small Schroeder reverb (4 combs + 2 allpasses): a bit of street / shop space."""
    combs = [int(SR * d) for d in (0.0297, 0.0371, 0.0411, 0.0437)]
    bufs = [[0.0] * d for d in combs]
    idx = [0] * 4
    aps = [int(SR * d) for d in (0.005, 0.0017)]
    abufs = [[0.0] * d for d in aps]
    aidx = [0, 0]
    out = []
    for v in x:
        s = 0.0
        for c in range(4):
            b = bufs[c]
            y = b[idx[c]]
            b[idx[c]] = v + y * room
            idx[c] = (idx[c] + 1) % combs[c]
            s += y
        s *= 0.25
        for a in range(2):
            b = abufs[a]
            y = b[aidx[a]]
            z = -0.6 * s + y
            b[aidx[a]] = s + 0.6 * z
            aidx[a] = (aidx[a] + 1) % aps[a]
            s = z
        out.append(v * (1 - wet) + s * wet)
    return out


# ----------------------------------------------------------------------------- blender
def blender():
    rnd = random.Random(7)
    T = 2.7
    n = int(T * SR)
    t_off = 2.15
    wob = smooth_noise(rnd, 9, n)
    out = [0.0] * n
    brush = Biquad('bp', 2600, 0.7)
    hiss = Biquad('bp', 6800, 1.1)
    churn = Biquad('lp', 380, 0.8)
    jar = Biquad('bp', 1150, 3.0)
    ph = 0.0
    harm = [(k, (1.4 if k % 2 else 1.0) / k ** 1.1, rnd.uniform(0, TAU)) for k in range(1, 15)]
    r_off = 1.0
    for i in range(n):
        t = i / SR
        if t < t_off:
            r = 1 - math.exp(-t / 0.08)
            r_off = r
        else:
            r = r_off * math.exp(-(t - t_off) / 0.22)
        crush = max(0.0, min(1.0, (1.25 - t) / 0.5)) if t > 0.2 else 0.0
        load = 1 - 0.16 * crush + 0.05 * crush * wob[i]
        f0 = 395 * r * load
        ph += TAU * f0 / SR
        tone = 0.0
        if f0 > 20:
            for k, a, p0 in harm:
                if k * f0 < 11000:
                    tone += a * math.sin(k * ph + p0)
        nz = rnd.uniform(-1, 1)
        rot = 0.55 + 0.45 * abs(math.sin(ph * 0.5))
        b = brush(nz) * rot
        h = hiss(nz)
        liquid = churn(rnd.uniform(-1, 1)) * (0.6 + 0.4 * math.sin(TAU * 5.3 * t + 2 * wob[i]))
        wet = min(1.0, max(0.0, (t - 0.45) / 0.6))
        v = (0.22 * tone + 0.55 * b + 0.16 * h) * r ** 0.8 + 1.4 * liquid * wet * r
        out[i] = v + 0.35 * jar(v)
    # ice chunks cracking against the jar while it is being crushed
    t = 0.24
    while t < 1.3:
        rate = 95 * max(0.0, (1.3 - t) / 1.06)
        if rate <= 1:
            break
        t += rnd.expovariate(rate)
        amp = rnd.uniform(0.25, 1.0) * (0.4 + 0.6 * (1.3 - t))
        fr = rnd.uniform(1500, 5200)
        tau = rnd.uniform(0.002, 0.006)
        bp = Biquad('bp', fr, 2.5)
        th = rnd.uniform(170, 380)
        add_event(out, t, lambda u, bp=bp, tau=tau, amp=amp, th=th:
                  amp * (2.2 * bp(rnd.uniform(-1, 1)) * math.exp(-u / tau) + 0.5 * math.sin(TAU * th * u) * math.exp(-u / 0.012)),
                  0.05)
    lp = Biquad('lp', 9500, 0.7)
    out = [lp(v) for v in out]
    write("sfx_blender.wav", reverb(out, 0.12, 0.6), peak=0.8)


# ----------------------------------------------------------------------------- horns
def horn(name, beeps, f=420.0, formants=((1900, 5.0, 1.0), (3150, 6.0, 0.7)), duty=0.28, seed=3):
    rnd = random.Random(seed)
    T = max(s + d for s, d in beeps) + 0.2
    n = int(T * SR)
    res = [Biquad('bp', fr, q) for fr, q, _ in formants]
    body = Biquad('lp', 2600, 0.8)
    hp = Biquad('hp', 260, 0.7)
    coef = [(k, 2 / (k * math.pi) * math.sin(k * math.pi * duty)) for k in range(1, 40)]
    out = []
    ph = 0.0
    for i in range(n):
        t = i / SR
        env, since = 0.0, 1.0
        for s, d in beeps:
            if s <= t < s + d + 0.06:
                a = 1 - math.exp(-(t - s) / 0.004)
                rel = 1.0 if t < s + d else math.exp(-(t - s - d) / 0.012)
                env = max(env, a * rel)
                since = min(since, t - s)
        f_now = f * (1 + 0.04 * math.exp(-since / 0.02)) * (1 + 0.004 * math.sin(TAU * 7 * t))
        ph += TAU * f_now / SR
        src = 0.0
        if env > 1e-4:
            for k, c in coef:
                if k * f_now > 12000:
                    break
                src += c * math.sin(k * ph)
            src += 0.08 * rnd.uniform(-1, 1) * (1 if math.sin(ph) > 0.6 else 0.2)     # diaphragm buzz
        src *= env
        v = 0.6 * body(src)
        for (fr, q, g), bq in zip(formants, res):
            v += 1.6 * g * bq(src)
        out.append(hp(math.tanh(1.8 * v)))
    write(name, reverb(out, 0.1, 0.5), peak=0.85)


# ----------------------------------------------------------------------------- tuk-tuk pass-by
def tuk_pass():
    rec = read("sfx_moto_pass.wav")
    n = len(rec)
    win = int(0.04 * SR)
    env = []
    acc = 0.0
    for i, v in enumerate(rec):
        acc += v * v
        if i >= win:
            acc -= rec[i - win] ** 2
        env.append(math.sqrt(max(acc, 0.0) / win))
    i_pk = max(range(n), key=lambda i: env[i])
    tp = i_pk / SR
    rnd = random.Random(11)
    v, d, c = 7.0, 3.5, 343.0
    putter = [0.0] * n
    t_engine = 0.0                  # engine time (source clock)
    t_list = []
    k = 0
    while True:
        k += 1
        t_engine += (1 / 37.0) * (1 + 0.07 * rnd.gauss(0, 1))
        # listener time of this firing: source time + travel time
        x = v * (t_engine - tp)
        r = math.sqrt(x * x + d * d)
        t_listen = t_engine + r / c - d / c
        if t_listen > n / SR:
            break
        if rnd.random() < 0.03:
            continue                                           # misfire: the uneven putter
        vr = -v * x / r                                        # toward the listener
        dop = c / (c - vr)
        amp = (1 + 0.25 * rnd.gauss(0, 1)) / max(r, 2.0)
        t_list.append((t_listen, amp, dop))
    for t0, amp, dop in t_list:
        hp = Biquad('hp', 1800, 0.7)
        f1, f2 = 128 * dop, 610 * dop
        add_event(putter, t0, lambda u, amp=amp, f1=f1, f2=f2, hp=hp:
                  amp * (math.sin(TAU * f1 * u) * math.exp(-u / 0.011) + 0.35 * math.sin(TAU * f2 * u) * math.exp(-u / 0.006)
                         + 0.5 * hp(rnd.uniform(-1, 1)) * math.exp(-u / 0.004)), 0.06)
    # air absorption: duller when far
    lpf = Biquad('lp', 2000, 0.7)
    out = []
    pk_p = max(abs(x) for x in putter) or 1
    pk_r = max(abs(x) for x in rec) or 1
    for i in range(n):
        t = i / SR
        x = v * (t - tp)
        near = 1 / (1 + (x / 8) ** 2)
        if i % 64 == 0:
            lpf.set(1500 + 6500 * near)
        out.append(0.55 * rec[i] / pk_r + 0.9 * lpf(putter[i]) / pk_p)
    write("sfx_tuk_pass.wav", out, peak=0.8, fade=0.05)


# ----------------------------------------------------------------------------- cat
def meow(name, T, f_pts, f1_pts, f2_pts, trill=0.0, seed=5):
    """Harmonic voice with a pitch contour and two moving formants (closed 'm' -> open 'iaa' -> rounded 'ow')."""
    rnd = random.Random(seed)
    n = int(T * SR)

    def curve(pts, u):
        for (u0, a), (u1, b) in zip(pts, pts[1:]):
            if u0 <= u <= u1:
                k = (u - u0) / (u1 - u0)
                k = k * k * (3 - 2 * k)
                return a + (b - a) * k
        return pts[-1][1]
    breath = Biquad('bp', 4200, 0.8)
    out = []
    ph = 0.0
    jit = smooth_noise(rnd, 30, n)
    for i in range(n):
        t = i / SR
        u = t / T
        f0 = curve(f_pts, u) * (1 + 0.006 * math.sin(TAU * 6 * t) + 0.01 * jit[i])
        ph += TAU * f0 / SR
        F1, F2 = curve(f1_pts, u), curve(f2_pts, u)
        s = 0.0
        k = 1
        while k * f0 < 9000:
            fk = k * f0
            g = 1.0 / (1 + ((fk - F1) / 160) ** 2) + 0.7 / (1 + ((fk - F2) / 260) ** 2) + 0.05 / k
            s += g * math.sin(k * ph) / math.sqrt(k)
            k += 1
        env = min(1.0, t / 0.05) * min(1.0, (T - t) / 0.16)
        if trill:
            env *= 1 - trill * max(0.0, 1 - t / 0.12) * (0.5 + 0.5 * math.sin(TAU * 28 * t))
        out.append(env * (s + 0.05 * breath(rnd.uniform(-1, 1))))
    write(name, reverb(out, 0.08, 0.4), peak=0.8)


# ----------------------------------------------------------------------------- chop
def chop(name, seed):
    rnd = random.Random(seed)
    T = 0.3
    n = int(T * SR)
    out = [0.0] * n
    modes = [(rnd.uniform(380, 460), 0.05, 1.0), (rnd.uniform(720, 840), 0.03, 0.6), (rnd.uniform(1250, 1450), 0.02, 0.4),
             (rnd.uniform(2300, 2700), 0.01, 0.25)]
    click = Biquad('hp', 2500, 0.7)
    squish = Biquad('bp', 900, 1.0)
    for i in range(n):
        t = i / SR
        thud = math.sin(TAU * (160 * t - 120 * t * t)) * math.exp(-t / 0.035)
        board = sum(a * math.sin(TAU * f * t) * math.exp(-t / d) for f, d, a in modes)
        nz = rnd.uniform(-1, 1)
        out[i] = 0.9 * thud + 0.5 * board + 0.9 * click(nz) * math.exp(-t / 0.002) + 0.3 * squish(nz) * math.exp(-t / 0.03)
    write(name, reverb(out, 0.12, 0.5), peak=0.75, fade=0.003)


# ----------------------------------------------------------------------------- firecrackers
def firecrackers():
    rnd = random.Random(21)
    T = 3.6
    n = int(T * SR)
    out = [0.0] * n
    t = 0.05
    while t < T - 0.4:
        t += rnd.expovariate(34 if t < 2.6 else 14)
        amp = rnd.uniform(0.35, 1.0) * (1.6 if rnd.random() < 0.06 else 1.0)
        tau = rnd.uniform(0.003, 0.008)
        hp = Biquad('hp', rnd.uniform(250, 600), 0.7)
        lp = Biquad('lp', rnd.uniform(4000, 8000), 0.7)
        add_event(out, t, lambda u, amp=amp, tau=tau, hp=hp, lp=lp:
                  amp * (lp(hp(rnd.uniform(-1, 1))) * 2.2 * math.exp(-u / tau) + 0.5 * math.sin(TAU * 90 * u) * math.exp(-u / 0.015)),
                  0.08)
    write("sfx_firecracker.wav", reverb(out, 0.3, 0.78), peak=0.85, fade=0.02)


# ----------------------------------------------------------------------------- app chime
def app_order():
    T = 0.9
    n = int(T * SR)
    out = [0.0] * n
    for t0, f in ((0.0, 1318.5), (0.13, 1760.0)):
        add_event(out, t0, lambda u, f=f: (math.sin(TAU * f * u) + 0.3 * math.sin(TAU * 2 * f * u) * math.exp(-u / 0.08)
                                           + 0.12 * math.sin(TAU * 3.01 * f * u)) * math.exp(-u / 0.22) * min(1.0, u / 0.003), 0.75)
    write("sfx_app_order.wav", reverb(out, 0.15, 0.5), peak=0.7)


# ----------------------------------------------------------------------------- shaker
def shaker():
    rnd = random.Random(31)
    T = 2.0
    n = int(T * SR)
    out = [0.0] * n
    for b in range(6):                                   # 3 shakes a second, a burst of ice hits at each turn
        tb = b / 3.0 + 0.02
        for _ in range(rnd.randint(14, 22)):
            t = tb + abs(rnd.gauss(0, 0.035))
            amp = rnd.uniform(0.2, 1.0)
            bp = Biquad('bp', rnd.choice((3200, 4100, 5200, 2600)) * rnd.uniform(0.9, 1.1), 6)
            tau = rnd.uniform(0.004, 0.012)
            add_event(out, t, lambda u, amp=amp, bp=bp, tau=tau: amp * 3 * bp(rnd.uniform(-1, 1)) * math.exp(-u / tau), 0.06)
    write("sfx_shake.wav", reverb(out, 0.1, 0.5), peak=0.7)


if __name__ == "__main__":
    blender()
    horn("sfx_horn_tuk.wav", [(0.0, 0.11), (0.17, 0.3)], f=415)
    horn("sfx_horn_tuk2.wav", [(0.0, 0.08), (0.13, 0.08), (0.26, 0.22)], f=430, seed=4)
    horn("sfx_horn_moto.wav", [(0.0, 0.36)], f=492, formants=((2250, 4.0, 1.0), (3600, 5.0, 0.6)), duty=0.33, seed=6)
    tuk_pass()
    meow("sfx_meow.wav", 0.78, [(0, 520), (0.32, 760), (1, 470)], [(0, 650), (0.3, 1300), (1, 820)], [(0, 1700), (0.35, 2500), (1, 1250)])
    meow("sfx_meow2.wav", 0.36, [(0, 600), (0.5, 880), (1, 700)], [(0, 700), (0.5, 1150), (1, 900)], [(0, 1800), (0.5, 2300), (1, 1700)],
         trill=0.8, seed=6)
    chop("sfx_chop.wav", 1)
    chop("sfx_chop2.wav", 2)
    firecrackers()
    app_order()
    shaker()

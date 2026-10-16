// Thai Boba Shop app icon — Adobe Illustrator (ExtendScript).
// Run: (New-Object -ComObject Illustrator.Application).DoJavaScriptFile("<this file>")
// Same cup as the in-game logo (ArtSource/UI/make_ui_kit.jsx) on a Bangkok-sunset sky with market string lights.
// Exports icon_square_*.png (full bleed: Android / itch) and icon_rounded_*.png (transparent corners: Windows).
#target illustrator

var ROOT = "D:/Project-Start/A4";
var OUT = ROOT + "/Assets/Branding";
var AI_FILE = ROOT + "/ArtSource/Branding/ThaiBobaIcon.ai";
new Folder(OUT).create();
app.userInteractionLevel = UserInteractionLevel.DONTDISPLAYALERTS;
var doc = app.documents.add(DocumentColorSpace.RGB, 1024, 1024);

var C = {
    cream: "#FFF4E3", brown: "#6B3E26", brownDark: "#3E2416", pink: "#F25C8A", pinkDark: "#C23A66", pinkLight: "#FF9CBB",
    orange: "#F28C28", gold: "#FFC93C", sky: "#FFE08A", boba: "#2B1B14", white: "#FFFFFF", milk: "#E8C9A0", cyan: "#4FD6F0"
};
function col(hex) {
    var c = new RGBColor();
    c.red = parseInt(hex.substr(1, 2), 16); c.green = parseInt(hex.substr(3, 2), 16); c.blue = parseInt(hex.substr(5, 2), 16);
    return c;
}

var boards = [];
function board(name, x) {
    var b = { name: name, x: x, y: 0, w: 1024, h: 1024 };
    if (boards.length === 0) doc.artboards[0].artboardRect = [b.x, 0, b.x + 1024, -1024];
    else doc.artboards.add([b.x, 0, b.x + 1024, -1024]);
    doc.artboards[boards.length].name = name;
    boards.push(b);
    return b;
}
function style(item, fill, stroke, sw) {
    if (fill) { item.filled = true; item.fillColor = typeof fill === "string" ? col(fill) : fill; } else item.filled = false;
    if (stroke) { item.stroked = true; item.strokeColor = col(stroke); item.strokeWidth = sw || 6; item.strokeJoin = StrokeJoin.ROUNDENDJOIN; item.strokeCap = StrokeCap.ROUNDENDCAP; }
    else item.stroked = false;
    return item;
}
function into(g, item) { item.move(g, ElementPlacement.PLACEATBEGINNING); return item; }   // newest on top
function rrect(g, b, x, y, w, h, r, fill, stroke, sw) { return into(g, style(doc.pathItems.roundedRectangle(b.y - y, b.x + x, w, h, r, r), fill, stroke, sw)); }
function ellipse(g, b, x, y, w, h, fill, stroke, sw) { return into(g, style(doc.pathItems.ellipse(b.y - y, b.x + x, w, h), fill, stroke, sw)); }
function poly(g, b, pts, fill, stroke, sw, closed) {
    var p = doc.pathItems.add();
    var arr = [];
    for (var i = 0; i < pts.length; i++) arr.push([b.x + pts[i][0], b.y - pts[i][1]]);
    p.setEntirePath(arr);
    p.closed = closed !== false;
    return into(g, style(p, fill, stroke, sw));
}
function op(item, o) { item.opacity = o; return item; }

function skyGradient() {
    var gr = doc.gradients.add();
    gr.type = GradientType.RADIAL;
    gr.gradientStops[0].color = col(C.sky); gr.gradientStops[0].rampPoint = 0;
    gr.gradientStops[1].color = col(C.pink); gr.gradientStops[1].rampPoint = 100;
    var mid = gr.gradientStops.add();
    mid.color = col(C.orange); mid.rampPoint = 55; mid.midPoint = 50;
    var gc = new GradientColor();
    gc.gradient = gr;
    return gc;
}

// cup from the logo (512 design units) scaled by s, top-left of its box at (ox, oy)
function cup(g, b, ox, oy, s) {
    function P(x, y) { return [ox + x * s, oy + y * s]; }
    function E(x, y, w, h, fill, stroke, sw) { return ellipse(g, b, ox + x * s, oy + y * s, w * s, h * s, fill, stroke, sw ? sw * s : 0); }
    function Q(pts, fill, stroke, sw, closed) { var a = []; for (var i = 0; i < pts.length; i++) a.push(P(pts[i][0], pts[i][1])); return poly(g, b, a, fill, stroke, sw ? sw * s : 0, closed); }
    op(E(96, 440, 320, 50, C.brownDark), 55);                                      // shadow
    Q([[120, 150], [392, 150], [360, 460], [152, 460]], C.white, C.brown, 10);    // cup
    Q([[132, 230], [380, 230], [362, 452], [150, 452]], C.milk);                  // milk tea
    Q([[132, 230], [380, 230], [376, 270], [136, 270]], C.pinkLight);             // foam
    for (var i = 0; i < 21; i++) E(160 + (i % 7) * 27, 360 + Math.floor(i / 7) * 28, 27, 27, C.boba);
    for (i = 0; i < 21; i++) op(E(166 + (i % 7) * 27, 364 + Math.floor(i / 7) * 28, 8, 6, C.white), 45);   // pearl shine
    rrect(g, b, ox + 104 * s, oy + 120 * s, 304 * s, 46 * s, 23 * s, C.pink, C.brown, 10 * s);             // lid
    Q([[290, 130], [350, 18]], null, C.pinkDark, 30, false);                      // straw
    op(Q([[150, 175], [168, 175], [176, 440], [164, 440]], C.white), 55);         // cup shine
    E(200, 290, 22, 26, C.boba); E(292, 290, 22, 26, C.boba);                     // face
    op(E(209, 294, 7, 7, C.white), 90); op(E(301, 294, 7, 7, C.white), 90);
    op(E(176, 322, 34, 18, C.pinkLight), 80); op(E(304, 322, 34, 18, C.pinkLight), 80);
    Q([[236, 324], [256, 340], [276, 324]], null, C.boba, 8, false);
}

function drawIcon(b, rounded) {
    var g = doc.groupItems.add();
    var R = rounded ? 230 : 0;
    var bg = style(doc.pathItems.rectangle(b.y, b.x, 1024, 1024), skyGradient());
    into(g, bg);
    // sun rays
    var cx = 512, cy = 560;
    for (var i = 0; i < 18; i++) {
        var a0 = i * Math.PI * 2 / 18, a1 = a0 + Math.PI / 18;
        op(poly(g, b, [[cx, cy], [cx + Math.cos(a0) * 900, cy + Math.sin(a0) * 900], [cx + Math.cos(a1) * 900, cy + Math.sin(a1) * 900]], C.white), 13);
    }
    op(ellipse(g, b, cx - 300, cy - 300, 600, 600, C.white), 18);                 // soft sun glow
    // market string lights across the top
    var pts = [];
    for (i = 0; i <= 20; i++) { var t = i / 20; pts.push([-20 + t * 1064, 150 + Math.sin(t * Math.PI) * 90]); }
    poly(g, b, pts, null, C.brownDark, 7, false);
    var bulbs = [C.gold, C.pink, C.cyan, C.white];
    for (i = 1; i < 10; i++) {
        var t2 = i / 10, x = -20 + t2 * 1064, y = 150 + Math.sin(t2 * Math.PI) * 90;
        op(ellipse(g, b, x - 30, y - 6, 60, 60, bulbs[i % 4]), 35);                // glow
        ellipse(g, b, x - 17, y + 6, 34, 40, bulbs[i % 4], C.brownDark, 4);
    }
    // floating pearls / bubbles
    var bub = [[150, 700, 46], [860, 640, 38], [800, 860, 30], [210, 880, 26], [120, 470, 22], [900, 420, 26]];
    for (i = 0; i < bub.length; i++) {
        ellipse(g, b, bub[i][0], bub[i][1], bub[i][2], bub[i][2], C.boba, C.brownDark, 3);
        op(ellipse(g, b, bub[i][0] + bub[i][2] * 0.22, bub[i][1] + bub[i][2] * 0.18, bub[i][2] * 0.3, bub[i][2] * 0.22, C.white), 60);
    }
    cup(g, b, 512 - 256 * 1.55, 175, 1.55);
    if (rounded) rrect(g, b, 6, 6, 1012, 1012, R - 6, null, C.brown, 12);          // outline like the UI
    var mask = rounded ? doc.pathItems.roundedRectangle(b.y, b.x, 1024, 1024, R, R) : doc.pathItems.rectangle(b.y, b.x, 1024, 1024);
    mask.move(g, ElementPlacement.PLACEATBEGINNING);                               // clip path must be the top item
    g.clipped = true;
}

drawIcon(board("icon_square", 0), false);
drawIcon(board("icon_rounded", 1100), true);

var sizes = [1024, 512, 256, 128, 64, 48, 32];
for (var i = 0; i < boards.length; i++) {
    doc.artboards.setActiveArtboardIndex(i);
    for (var k = 0; k < sizes.length; k++) {
        var o = new ExportOptionsPNG24();
        o.antiAliasing = true; o.transparency = true; o.artBoardClipping = true;
        o.horizontalScale = o.verticalScale = sizes[k] / 1024 * 100;
        doc.exportFile(new File(OUT + "/" + boards[i].name + "_" + sizes[k] + ".png"), ExportType.PNG24, o);
    }
}
doc.saveAs(new File(AI_FILE));
"icon exported";

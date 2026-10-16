// Thai Boba Shop UI kit — generated in Adobe Illustrator (ExtendScript).
// Run from PowerShell:  (New-Object -ComObject Illustrator.Application).DoJavaScriptFile("<this file>")
// Exports one transparent PNG per artboard to Assets/UI/Sprites and saves the editable .ai next to this script.
#target illustrator

var ROOT = "D:/Project-Start/A4";
var OUT = ROOT + "/Assets/UI/Sprites";
var AI_FILE = ROOT + "/ArtSource/UI/ThaiBobaUI.ai";
new Folder(OUT).create();

app.userInteractionLevel = UserInteractionLevel.DONTDISPLAYALERTS;
var doc = app.documents.add(DocumentColorSpace.RGB, 512, 512);

// ---------------------------------------------------------------- palette
var C = {
    cream: "#FFF4E3", cream2: "#FBE3C2", brown: "#6B3E26", brownDark: "#3E2416", text: "#4A2C1A",
    pink: "#F25C8A", pinkDark: "#C23A66", pinkLight: "#FF9CBB",
    orange: "#F28C28", orangeDark: "#C0661A", green: "#7BB661", greenDark: "#4E8A3C",
    blue: "#4FA3E0", blueDark: "#2C74AE", taro: "#A78BDB", boba: "#2B1B14", gold: "#FFC93C", goldDark: "#D99A1E",
    white: "#FFFFFF", ice: "#CDEFFF", milk: "#E8C9A0"
};
function col(hex) {
    var c = new RGBColor();
    c.red = parseInt(hex.substr(1, 2), 16); c.green = parseInt(hex.substr(3, 2), 16); c.blue = parseInt(hex.substr(5, 2), 16);
    return c;
}

// ---------------------------------------------------------------- artboards
var boards = [];        // {name, x, y, w, h}
var cursorX = 0, cursorY = 0, rowH = 0, MAXW = 2600;
function board(name, w, h) {
    if (cursorX + w > MAXW) { cursorX = 0; cursorY -= rowH + 40; rowH = 0; }
    var b = { name: name, x: cursorX, y: cursorY, w: w, h: h };
    if (boards.length === 0) doc.artboards[0].artboardRect = [b.x, b.y, b.x + w, b.y - h];
    else doc.artboards.add([b.x, b.y, b.x + w, b.y - h]);
    doc.artboards[boards.length].name = name;
    boards.push(b);
    cursorX += w + 40; rowH = Math.max(rowH, h);
    return b;
}
// helpers draw in board-local coordinates: (0,0) top-left, y grows downward
function style(item, fill, stroke, sw) {
    if (fill) { item.filled = true; item.fillColor = col(fill); } else item.filled = false;
    if (stroke) { item.stroked = true; item.strokeColor = col(stroke); item.strokeWidth = sw || 6; item.strokeJoin = StrokeJoin.ROUNDENDJOIN; item.strokeCap = StrokeCap.ROUNDENDCAP; }
    else item.stroked = false;
    return item;
}
function rrect(b, x, y, w, h, r, fill, stroke, sw) {
    return style(doc.pathItems.roundedRectangle(b.y - y, b.x + x, w, h, r, r), fill, stroke, sw);
}
function ellipse(b, x, y, w, h, fill, stroke, sw) {
    return style(doc.pathItems.ellipse(b.y - y, b.x + x, w, h), fill, stroke, sw);
}
function poly(b, pts, fill, stroke, sw, closed) {
    var p = doc.pathItems.add();
    var arr = [];
    for (var i = 0; i < pts.length; i++) arr.push([b.x + pts[i][0], b.y - pts[i][1]]);
    p.setEntirePath(arr);
    p.closed = closed !== false;
    return style(p, fill, stroke, sw);
}
function opacity(item, o) { item.opacity = o; return item; }
function text(b, s, x, y, size, fill, fontName) {
    var t = doc.textFrames.add();
    t.contents = s;
    var a = t.textRange.characterAttributes;
    try { a.textFont = app.textFonts.getByName(fontName || "Tahoma-Bold"); } catch (e) {}
    a.size = size;
    a.fillColor = col(fill);
    t.textRange.paragraphAttributes.justification = Justification.CENTER;
    t.left = b.x + x - t.width / 2;
    t.top = b.y - y + t.height / 2;
    return t.createOutline();
}

// ---------------------------------------------------------------- 9-slice panels and buttons
var b;
b = board("panel", 256, 256);                                    // cream card with brown outline + top shine
rrect(b, 4, 10, 248, 242, 52, C.brownDark);
rrect(b, 4, 4, 248, 240, 52, C.cream, C.brown, 8);
opacity(rrect(b, 26, 18, 204, 30, 15, C.white), 55);

b = board("panel_dark", 256, 256);                               // translucent brown HUD backing
opacity(rrect(b, 2, 2, 252, 252, 56, C.brownDark), 82);

b = board("chip", 220, 96);                                      // pill for HUD stats
rrect(b, 3, 9, 214, 84, 42, C.brownDark);
rrect(b, 3, 3, 214, 82, 41, C.cream, C.brown, 6);

var BTN = [["button_pink", C.pink, C.pinkDark, C.pinkLight], ["button_green", C.green, C.greenDark, "#A8DB8F"],
           ["button_orange", C.orange, C.orangeDark, "#FFC07A"], ["button_cream", C.cream2, "#C9A274", C.white],
           ["button_blue", C.blue, C.blueDark, "#93CDF5"]];
for (var i = 0; i < BTN.length; i++) {
    b = board(BTN[i][0], 320, 120);
    rrect(b, 4, 16, 312, 100, 50, BTN[i][2]);                   // pressed-depth shadow
    rrect(b, 4, 4, 312, 100, 50, BTN[i][1], C.brown, 6);
    opacity(rrect(b, 30, 16, 260, 26, 13, BTN[i][3]), 85);      // gloss
}

var ROUND = [["round_pink", C.pink, C.pinkDark], ["round_blue", C.blue, C.blueDark], ["round_cream", C.cream2, "#C9A274"]];
for (i = 0; i < ROUND.length; i++) {
    b = board(ROUND[i][0], 220, 220);
    ellipse(b, 6, 18, 208, 198, ROUND[i][2]);
    ellipse(b, 6, 6, 208, 200, ROUND[i][1], C.brown, 7);
    opacity(ellipse(b, 50, 22, 120, 50, C.white), 35);
}

b = board("joystick_bg", 280, 280);
opacity(ellipse(b, 8, 8, 264, 264, C.cream), 45);
ellipse(b, 8, 8, 264, 264, null, C.brown, 8);
for (i = 0; i < 4; i++) {                                         // direction arrows
    var cx = 140, cy = 140, r = 104, a = i * Math.PI / 2;
    var dx = Math.cos(a), dy = Math.sin(a), px = -dy, py = dx;
    opacity(poly(b, [[cx + dx * (r + 14), cy + dy * (r + 14)], [cx + dx * r + px * 14, cy + dy * r + py * 14],
                     [cx + dx * r - px * 14, cy + dy * r - py * 14]], C.brown), 70);
}
b = board("joystick_knob", 140, 140);
ellipse(b, 6, 12, 128, 124, C.pinkDark);
ellipse(b, 6, 6, 128, 124, C.pink, C.brown, 6);
opacity(ellipse(b, 34, 18, 72, 30, C.white), 40);

// ---------------------------------------------------------------- icons (128x128)
function icon(name) { return board("icon_" + name, 128, 128); }

b = icon("coin");
ellipse(b, 10, 14, 108, 108, C.goldDark);
ellipse(b, 10, 8, 108, 108, C.gold, C.brown, 6);
ellipse(b, 26, 24, 76, 76, null, C.goldDark, 5);
text(b, "\u0E3F", 64, 62, 54, C.brown, "Tahoma-Bold");

b = icon("star");
var sp = [];
for (i = 0; i < 10; i++) {
    var rr = i % 2 === 0 ? 56 : 24, aa = -Math.PI / 2 + i * Math.PI / 5;
    sp.push([64 + Math.cos(aa) * rr, 68 + Math.sin(aa) * rr]);
}
poly(b, sp, C.gold, C.brown, 6);

b = icon("clock");
ellipse(b, 10, 10, 108, 108, C.cream, C.brown, 7);
poly(b, [[64, 64], [64, 30]], null, C.brown, 8, false);
poly(b, [[64, 64], [88, 76]], null, C.pink, 8, false);
ellipse(b, 57, 57, 14, 14, C.brown);

b = icon("cup");                                                  // boba cup
poly(b, [[30, 40], [98, 40], [90, 118], [38, 118]], C.milk, C.brown, 6);
rrect(b, 24, 30, 80, 16, 8, C.pinkLight, C.brown, 5);
poly(b, [[70, 32], [84, 6]], null, C.pink, 9, false);
for (i = 0; i < 6; i++) ellipse(b, 42 + (i % 3) * 16, 92 + Math.floor(i / 3) * 12, 12, 12, C.boba);

b = icon("boba");
var bp = [[30, 50], [62, 40], [92, 54], [44, 80], [78, 82], [60, 104]];
for (i = 0; i < bp.length; i++) { ellipse(b, bp[i][0] - 18, bp[i][1] - 18, 36, 36, C.boba, C.brown, 3); opacity(ellipse(b, bp[i][0] - 8, bp[i][1] - 12, 10, 8, C.white), 60); }

b = icon("ice");
rrect(b, 18, 30, 64, 64, 12, C.ice, C.blueDark, 6);
rrect(b, 50, 16, 60, 60, 12, C.ice, C.blueDark, 6);
opacity(rrect(b, 60, 24, 20, 12, 6, C.white), 90);

b = icon("tea");                                                  // tea leaf
poly(b, [[20, 108], [30, 50], [70, 18], [108, 14], [104, 56], [74, 98]], C.green, C.greenDark, 6);
poly(b, [[24, 104], [92, 30]], null, C.greenDark, 5, false);

b = icon("sugar");
rrect(b, 22, 40, 56, 56, 8, C.white, C.brown, 6);
rrect(b, 54, 22, 52, 52, 8, C.white, C.brown, 6);
text(b, "%", 80, 48, 30, C.pink, "Tahoma-Bold");

b = icon("seal");                                                 // sealer stamp
rrect(b, 30, 14, 68, 40, 10, C.pink, C.brown, 6);
rrect(b, 54, 50, 20, 30, 6, C.cream2, C.brown, 5);
rrect(b, 18, 82, 92, 30, 12, C.cream, C.brown, 6);

b = icon("trash");
rrect(b, 30, 36, 68, 80, 10, C.green, C.greenDark, 6);
rrect(b, 22, 24, 84, 16, 8, C.greenDark, C.brown, 5);
rrect(b, 52, 12, 24, 14, 6, C.greenDark);

b = icon("gear");
var gp = [];
for (i = 0; i < 16; i++) { var gr = i % 2 === 0 ? 56 : 44, ga = i * Math.PI / 8; gp.push([64 + Math.cos(ga) * gr, 64 + Math.sin(ga) * gr]); }
poly(b, gp, C.cream2, C.brown, 6);
ellipse(b, 46, 46, 36, 36, C.cream, C.brown, 6);

b = icon("pause");
rrect(b, 34, 22, 22, 84, 8, C.cream, C.brown, 6);
rrect(b, 72, 22, 22, 84, 8, C.cream, C.brown, 6);

b = icon("play");
poly(b, [[38, 20], [104, 64], [38, 108]], C.cream, C.brown, 7);

b = icon("home");
poly(b, [[14, 62], [64, 18], [114, 62]], C.pink, C.brown, 6);
rrect(b, 28, 58, 72, 56, 6, C.cream, C.brown, 6);
rrect(b, 54, 80, 22, 34, 4, C.brown);

b = icon("save");
rrect(b, 16, 16, 96, 96, 14, C.blue, C.brown, 6);
rrect(b, 34, 16, 60, 34, 4, C.cream, C.brown, 4);
rrect(b, 30, 66, 68, 46, 6, C.white, C.brown, 4);

b = icon("music");
poly(b, [[48, 96], [48, 24], [102, 14], [102, 84]], null, C.brown, 8, false);
ellipse(b, 24, 84, 32, 26, C.pink, C.brown, 5);
ellipse(b, 78, 74, 32, 26, C.pink, C.brown, 5);

b = icon("sound");
poly(b, [[16, 48], [40, 48], [70, 20], [70, 108], [40, 80], [16, 80]], C.cream, C.brown, 6);
poly(b, [[86, 44], [96, 64], [86, 84]], null, C.brown, 6, false);
poly(b, [[100, 30], [116, 64], [100, 98]], null, C.brown, 6, false);

b = icon("calendar");
rrect(b, 14, 22, 100, 92, 12, C.cream, C.brown, 6);
rrect(b, 14, 22, 100, 28, 12, C.pink, C.brown, 6);
for (i = 0; i < 6; i++) rrect(b, 28 + (i % 3) * 26, 62 + Math.floor(i / 3) * 24, 18, 16, 4, C.milk);

b = icon("trophy");
poly(b, [[30, 18], [98, 18], [92, 60], [64, 80], [36, 60]], C.gold, C.brown, 6);
rrect(b, 54, 78, 20, 18, 4, C.goldDark, C.brown, 4);
rrect(b, 36, 94, 56, 18, 6, C.brown);

b = icon("fire");
poly(b, [[64, 10], [96, 56], [92, 96], [64, 118], [36, 96], [34, 60], [52, 72], [50, 40]], C.orange, C.brown, 6);
poly(b, [[64, 60], [80, 86], [64, 106], [50, 88]], C.gold);

b = icon("levelup");
poly(b, [[64, 12], [110, 60], [82, 60], [82, 114], [46, 114], [46, 60], [18, 60]], C.green, C.brown, 6);

b = icon("shop");
poly(b, [[14, 52], [26, 18], [102, 18], [114, 52]], C.pink, C.brown, 6);
for (i = 0; i < 4; i++) opacity(poly(b, [[26 + i * 22, 52], [32 + i * 19, 18], [42 + i * 19, 18], [38 + i * 22, 52]], C.white), 80);
rrect(b, 22, 52, 84, 62, 4, C.cream, C.brown, 6);
rrect(b, 52, 72, 24, 42, 4, C.brown);

b = icon("rain");
ellipse(b, 14, 26, 56, 46, "#B9C7D6", C.brown, 5);
ellipse(b, 46, 16, 66, 56, "#B9C7D6", C.brown, 5);
for (i = 0; i < 3; i++) poly(b, [[34 + i * 26, 86], [26 + i * 26, 110]], null, C.blue, 7, false);

b = icon("sun");
for (i = 0; i < 8; i++) { var sa = i * Math.PI / 4; poly(b, [[64 + Math.cos(sa) * 40, 64 + Math.sin(sa) * 40], [64 + Math.cos(sa) * 58, 64 + Math.sin(sa) * 58]], null, C.orange, 7, false); }
ellipse(b, 34, 34, 60, 60, C.gold, C.brown, 6);

b = icon("moon");
ellipse(b, 18, 18, 92, 92, C.gold, C.brown, 6);
ellipse(b, 46, 8, 78, 78, C.brownDark);

// weather / season icons
b = icon("cloud");
ellipse(b, 14, 46, 52, 44, "#E9EEF4", C.brown, 5);
ellipse(b, 40, 26, 62, 56, "#E9EEF4", C.brown, 5);
ellipse(b, 70, 46, 46, 40, "#E9EEF4", C.brown, 5);
rrect(b, 20, 66, 90, 26, 13, "#E9EEF4");

b = icon("storm");
ellipse(b, 12, 20, 56, 46, "#8C97A6", C.brown, 5);
ellipse(b, 44, 10, 66, 56, "#8C97A6", C.brown, 5);
poly(b, [[66, 62], [48, 92], [62, 92], [52, 120], [84, 82], [68, 82], [80, 62]], C.gold, C.brown, 4);

b = icon("cool");                                                // snowflake-ish "หนาว"
for (i = 0; i < 3; i++) {
    var ca = i * Math.PI / 3;
    poly(b, [[64 - Math.cos(ca) * 50, 64 - Math.sin(ca) * 50], [64 + Math.cos(ca) * 50, 64 + Math.sin(ca) * 50]], null, C.blue, 9, false);
}
ellipse(b, 50, 50, 28, 28, C.white, C.blueDark, 5);

b = icon("umbrella");
poly(b, [[10, 64], [24, 34], [64, 14], [104, 34], [118, 64]], C.pink, C.brown, 6);
poly(b, [[64, 64], [64, 108], [52, 116]], null, C.brown, 7, false);

b = icon("foam");                                                 // cheese foam topping: cup with a creamy cap
poly(b, [[30, 52], [98, 52], [90, 120], [38, 120]], C.white, C.brown, 6);
poly(b, [[35, 74], [93, 74], [89, 115], [39, 115]], C.milk);
ellipse(b, 22, 26, 84, 44, "#FDEDC4", C.brown, 6);
ellipse(b, 44, 12, 40, 30, "#FDEDC4", C.brown, 5);
opacity(ellipse(b, 38, 34, 22, 10, C.white), 80);
ellipse(b, 74, 42, 8, 8, C.goldDark);
ellipse(b, 52, 50, 6, 6, C.goldDark);

// ---------------------------------------------------------------- logo cup for the main menu
b = board("logo_cup", 512, 512);
ellipse(b, 96, 440, 320, 50, C.brownDark);
poly(b, [[120, 150], [392, 150], [360, 460], [152, 460]], C.white, C.brown, 10);
poly(b, [[132, 230], [380, 230], [362, 452], [150, 452]], C.milk);
poly(b, [[132, 230], [380, 230], [376, 270], [136, 270]], C.pinkLight);
for (i = 0; i < 14; i++) ellipse(b, 165 + (i % 7) * 26, 385 + Math.floor(i / 7) * 28, 26, 26, C.boba);
rrect(b, 104, 120, 304, 46, 23, C.pink, C.brown, 10);
poly(b, [[290, 130], [350, 18]], null, C.pinkDark, 30, false);
ellipse(b, 200, 290, 22, 26, C.boba);                             // kawaii face
ellipse(b, 292, 290, 22, 26, C.boba);
opacity(ellipse(b, 176, 322, 34, 18, C.pinkLight), 80);
opacity(ellipse(b, 304, 322, 34, 18, C.pinkLight), 80);
poly(b, [[236, 324], [256, 340], [276, 324]], null, C.boba, 8, false);

// ---------------------------------------------------------------- export
var opts = new ExportOptionsPNG24();
opts.antiAliasing = true; opts.transparency = true; opts.artBoardClipping = true;
opts.horizontalScale = 100; opts.verticalScale = 100;
for (i = 0; i < boards.length; i++) {
    doc.artboards.setActiveArtboardIndex(i);
    doc.exportFile(new File(OUT + "/" + boards[i].name + ".png"), ExportType.PNG24, opts);
}
doc.saveAs(new File(AI_FILE));
"exported " + boards.length;

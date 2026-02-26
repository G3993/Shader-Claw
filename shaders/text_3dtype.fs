/*{
  "CATEGORIES": ["Generator", "Text"],
  "DESCRIPTION": "3D Type — layered depth text with parallax perspective and color gradients",
  "INPUTS": [
    { "NAME": "msg", "TYPE": "text", "DEFAULT": " ETHEREA", "MAX_LENGTH": 24 },
    { "NAME": "fontFamily", "LABEL": "Font", "TYPE": "long", "VALUES": [0,1,2,3], "LABELS": ["Inter","Times New Roman","Libre Caslon","Outfit"], "DEFAULT": 0 },
    { "NAME": "speed", "LABEL": "Speed", "TYPE": "float", "MIN": 0.0, "MAX": 3.0, "DEFAULT": 0.5 },
    { "NAME": "depth", "LABEL": "Depth", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "hueSpread", "LABEL": "Hue Spread", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.4 },
    { "NAME": "textScale", "LABEL": "Size", "TYPE": "float", "MIN": 0.3, "MAX": 2.0, "DEFAULT": 1.0 },
    { "NAME": "kerning", "LABEL": "Spacing", "TYPE": "float", "MIN": 0.0, "MAX": 3.0, "DEFAULT": 0.4 },
    { "NAME": "textColor", "LABEL": "Color", "TYPE": "color", "DEFAULT": [1.0, 0.18, 0.33, 1.0] },
    { "NAME": "bgColor", "LABEL": "Background", "TYPE": "color", "DEFAULT": [0.04, 0.04, 0.07, 1.0] },
    { "NAME": "transparentBg", "LABEL": "Transparent", "TYPE": "bool", "DEFAULT": true }
  ]
}*/

// Atlas-only character sampling (no bitmap charData = no 27-branch dead code)
float sampleAtlas(int ch, vec2 cellUV) {
    if (ch < 0 || ch > 25) return 0.0;
    if (cellUV.x < 0.0 || cellUV.x > 1.0 || cellUV.y < 0.0 || cellUV.y > 1.0) return 0.0;
    return smoothstep(0.1, 0.55, texture2D(fontAtlasTex, vec2((float(ch) + cellUV.x) / 27.0, cellUV.y)).r);
}

int getChar(int slot) {
    if (slot == 0) return int(msg_0);   if (slot == 1) return int(msg_1);
    if (slot == 2) return int(msg_2);   if (slot == 3) return int(msg_3);
    if (slot == 4) return int(msg_4);   if (slot == 5) return int(msg_5);
    if (slot == 6) return int(msg_6);   if (slot == 7) return int(msg_7);
    if (slot == 8) return int(msg_8);   if (slot == 9) return int(msg_9);
    if (slot == 10) return int(msg_10); if (slot == 11) return int(msg_11);
    if (slot == 12) return int(msg_12); if (slot == 13) return int(msg_13);
    if (slot == 14) return int(msg_14); if (slot == 15) return int(msg_15);
    if (slot == 16) return int(msg_16); if (slot == 17) return int(msg_17);
    if (slot == 18) return int(msg_18); if (slot == 19) return int(msg_19);
    if (slot == 20) return int(msg_20); if (slot == 21) return int(msg_21);
    if (slot == 22) return int(msg_22); return int(msg_23);
}

int charCount() {
    int n = int(msg_len);
    if (n <= 0) return 7;
    if (n > 24) return 24;
    return n;
}

vec3 hsv2rgb(vec3 c) {
    vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

vec3 rgb2hsv(vec3 c) {
    vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
    vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));
    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

// Multi-line text hit test — wraps to new lines before scaling down
float textHit(vec2 uv, float aspect) {
    int numChars = charCount();
    float _ts = textScale > 0.01 ? textScale : 1.0;
    float _kn = kerning > 0.01 ? kerning : 1.0;
    float charH = 0.18 * _ts;
    float charW = charH * (5.0 / 7.0);
    float gap = charW * 0.25 * _kn;
    float cellStep = charW + gap;
    float maxW = aspect * 0.9;

    int maxCols = int(floor((maxW + gap) / cellStep));
    if (maxCols < 1) maxCols = 1;
    if (maxCols > numChars) maxCols = numChars;
    int numRows = (numChars + maxCols - 1) / maxCols;

    float rw = float(maxCols) * cellStep - gap;
    if (rw > maxW) {
        float sc = maxW / rw;
        charH *= sc; charW = charH * (5.0 / 7.0); gap = charW * 0.25 * _kn;
        cellStep = charW + gap;
    }

    float lineH = charH * 1.3;
    float totalH = float(numRows) * lineH - (lineH - charH);
    float startY = 0.5 - totalH * 0.5;
    vec2 p = vec2((uv.x - 0.5) * aspect + 0.5, uv.y);

    // Bounding box early-out
    if (p.y < startY - charH * 0.1 || p.y > startY + totalH + charH * 0.1) return 0.0;

    int _col = 0;
    int _row = 0;
    float rowStartX = 0.5;
    for (int i = 0; i < 24; i++) {
        if (i >= numChars) break;
        if (_col == 0) {
            int charsInRow = numChars - _row * maxCols;
            if (charsInRow > maxCols) charsInRow = maxCols;
            float rwRow = float(charsInRow) * cellStep - gap;
            rowStartX = 0.5 - rwRow * 0.5;
        }
        int ch = getChar(i);
        if (ch >= 0 && ch <= 25) {
            float cx = rowStartX + float(_col) * cellStep;
            float cy = startY + float(_row) * lineH;
            vec2 cellUV = vec2((p.x - cx) / charW, (p.y - cy) / charH);
            float hit = sampleAtlas(ch, cellUV);
            if (hit > 0.5) return 1.0;
        }
        _col++;
        if (_col >= maxCols) { _col = 0; _row++; }
    }
    return 0.0;
}

// =======================================================================
// MAIN — 8-layer parallax (down from 25 for fast ANGLE compilation)
// =======================================================================

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE;
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    float layerSpacing = depth * 0.008;

    float perspX = sin(TIME * speed * 0.3) * 0.25;
    float perspY = cos(TIME * speed * 0.25) * 0.15;
    float breathe = sin(TIME * speed * 0.8) * 0.3;

    vec3 baseHSV = rgb2hsv(textColor.rgb);
    vec3 finalColor = transparentBg ? vec3(0.0) : bgColor.rgb;
    float finalAlpha = transparentBg ? 0.0 : 1.0;

    // 8 layers back to front (i=7 deepest, i=0 front)
    for (int i = 7; i >= 0; i--) {
        float t = float(i) / 7.0;
        float layerDepth = float(i) * layerSpacing * (1.0 + breathe * 0.2);

        float waveX = sin(TIME * speed * 0.5 + float(i) * 0.4) * 0.003;
        float waveY = cos(TIME * speed * 0.4 + float(i) * 0.3) * 0.002;

        vec2 offset = vec2(perspX, perspY) * layerDepth + vec2(waveX, waveY);
        float layerScale = 1.0 + t * 0.06;
        vec2 layerUV = (uv - 0.5) / layerScale + 0.5 + offset;

        float hit = textHit(layerUV, aspect);
        if (hit > 0.5) {
            vec3 hsv = baseHSV;
            hsv.x = fract(hsv.x + t * hueSpread);
            hsv.y = min(1.0, hsv.y + t * 0.2);
            vec3 layerColor = hsv2rgb(hsv);
            float alpha = 0.3 + 0.7 * (1.0 - t);
            finalColor = mix(finalColor, layerColor, alpha);
            finalAlpha = max(finalAlpha, alpha);
        }
    }

    // Scanline
    float scanline = 1.0 - 0.03 * step(0.5, fract(gl_FragCoord.y / 3.0));
    finalColor *= scanline;

    gl_FragColor = vec4(finalColor, finalAlpha);
}

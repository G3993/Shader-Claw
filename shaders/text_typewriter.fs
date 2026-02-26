/*{
  "CATEGORIES": ["Generator", "Text"],
  "DESCRIPTION": "Typewriter — characters appear one by one with blinking cursor",
  "INPUTS": [
    { "NAME": "msg", "TYPE": "text", "DEFAULT": "ETHEREA", "MAX_LENGTH": 24 },
    { "NAME": "fontFamily", "LABEL": "Font", "TYPE": "long", "VALUES": [0,1,2,3], "LABELS": ["Inter","Times New Roman","Libre Caslon","Outfit"], "DEFAULT": 0 },
    { "NAME": "speed", "LABEL": "Speed", "TYPE": "float", "MIN": 0.5, "MAX": 20.0, "DEFAULT": 4.0 },
    { "NAME": "cursorBlink", "LABEL": "Cursor Blink", "TYPE": "float", "MIN": 0.5, "MAX": 5.0, "DEFAULT": 2.0 },
    { "NAME": "textScale", "LABEL": "Size", "TYPE": "float", "MIN": 0.3, "MAX": 2.0, "DEFAULT": 1.0 },
    { "NAME": "kerning", "LABEL": "Spacing", "TYPE": "float", "MIN": 0.0, "MAX": 3.0, "DEFAULT": 0.4 },
    { "NAME": "textColor", "LABEL": "Color", "TYPE": "color", "DEFAULT": [1.0, 1.0, 1.0, 1.0] },
    { "NAME": "bgColor", "LABEL": "Background", "TYPE": "color", "DEFAULT": [0.02, 0.02, 0.04, 1.0] },
    { "NAME": "transparentBg", "LABEL": "Transparent", "TYPE": "bool", "DEFAULT": true },
    { "NAME": "loop", "LABEL": "Loop", "TYPE": "bool", "DEFAULT": true }
  ]
}*/

// Atlas-only font engine (no bitmap fallback — faster ANGLE compile)
float charPixel(int ch, float col, float row) {
    if (ch < 0 || ch > 25) return 0.0;
    vec2 uv = vec2(col / 5.0, row / 7.0);
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) return 0.0;
    return smoothstep(0.1, 0.55, texture2D(fontAtlasTex, vec2((float(ch) + uv.x) / 27.0, uv.y)).r);
}

float sampleChar(int ch, vec2 uv) {
    if (ch < 0 || ch > 25) return 0.0;
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) return 0.0;
    return texture2D(fontAtlasTex, vec2((float(ch) + uv.x) / 27.0, uv.y)).r;
}

int getChar(int slot) {
    if (slot == 0) return int(msg_0);
    if (slot == 1) return int(msg_1);
    if (slot == 2) return int(msg_2);
    if (slot == 3) return int(msg_3);
    if (slot == 4) return int(msg_4);
    if (slot == 5) return int(msg_5);
    if (slot == 6) return int(msg_6);
    if (slot == 7) return int(msg_7);
    if (slot == 8) return int(msg_8);
    if (slot == 9) return int(msg_9);
    if (slot == 10) return int(msg_10);
    if (slot == 11) return int(msg_11);
    if (slot == 12) return int(msg_12);
    if (slot == 13) return int(msg_13);
    if (slot == 14) return int(msg_14);
    if (slot == 15) return int(msg_15);
    if (slot == 16) return int(msg_16);
    if (slot == 17) return int(msg_17);
    if (slot == 18) return int(msg_18);
    if (slot == 19) return int(msg_19);
    if (slot == 20) return int(msg_20);
    if (slot == 21) return int(msg_21);
    if (slot == 22) return int(msg_22);
    if (slot == 23) return int(msg_23);
    return 26;
}

int charCount() {
    int n = int(msg_len);
    if (n <= 0) return 7;
    if (n > 24) return 24;
    return n;
}

// =======================================================================
// EFFECT: TYPEWRITER — characters appear one by one with blinking cursor
// =======================================================================

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    int numChars = charCount();
    float _textScale = textScale > 0.01 ? textScale : 1.0;
    float _kerning = kerning > 0.01 ? kerning : 1.0;

    vec3 col = bgColor.rgb;
    float alpha = transparentBg ? 0.0 : 1.0;

    vec2 p = vec2((uv.x - 0.5) * aspect + 0.5, uv.y);
    float maxW = aspect * 0.9;

    // Layout: multi-line wrap then scale-to-fit
    float charH = 0.18 * _textScale;
    float charW = charH * (5.0 / 7.0);
    float gap = charW * 0.25 * _kerning;
    float cellStep = charW + gap;

    int maxCols = int(floor((maxW + gap) / cellStep));
    if (maxCols < 1) maxCols = 1;
    if (maxCols > numChars) maxCols = numChars;
    int numRows = (numChars + maxCols - 1) / maxCols;

    float rw = float(maxCols) * cellStep - gap;
    if (rw > maxW) {
        float sc = maxW / rw;
        charH *= sc; charW = charH * (5.0 / 7.0); gap = charW * 0.25 * _kerning;
        cellStep = charW + gap;
        rw = float(maxCols) * cellStep - gap;
    }

    float lineH = charH * 1.3;
    float totalH = float(numRows) * lineH - (lineH - charH);
    float startY = 0.5 - totalH * 0.5;

    // Typewriter reveal: how many chars are visible
    float totalTime = float(numChars) / speed;
    float elapsed = loop ? mod(TIME, totalTime + 1.5) : TIME;
    int visibleChars = int(floor(elapsed * speed));
    if (visibleChars > numChars) visibleChars = numChars;

    float textMask = 0.0;
    vec3 textCol = vec3(0.0);

    int _col = 0;
    int _row = 0;
    float rowStartX = 0.5;
    float lastCursorX = 0.0;
    float lastCursorY = startY;
    for (int i = 0; i < 24; i++) {
        if (i >= numChars) break;

        if (_col == 0) {
            int charsInRow = numChars - _row * maxCols;
            if (charsInRow > maxCols) charsInRow = maxCols;
            float rwRow = float(charsInRow) * cellStep - gap;
            rowStartX = 0.5 - rwRow * 0.5;
        }

        int ch = getChar(i);
        float cx = rowStartX + float(_col) * cellStep;
        float cy = startY + float(_row) * lineH;

        // Only show if revealed
        if (i < visibleChars && ch >= 0 && ch <= 25) {
            vec2 cellUV = vec2((p.x - cx) / charW, (p.y - cy) / charH);
            if (cellUV.x >= 0.0 && cellUV.x <= 1.0 && cellUV.y >= 0.0 && cellUV.y <= 1.0) {
                float filled = sampleChar(ch, cellUV);
                if (filled > 0.05) {
                    float edgeAA = smoothstep(0.1, 0.5, filled);
                    textCol = textColor.rgb;
                    textMask = max(textMask, edgeAA);
                }
            }
        }

        // Blinking cursor at next position
        if (i == visibleChars && visibleChars < numChars) {
            float cursorOn = step(0.5, fract(TIME * cursorBlink));
            float cursorW = charW * 0.15;
            if (p.x >= cx && p.x <= cx + cursorW &&
                p.y >= cy && p.y <= cy + charH) {
                textCol = textColor.rgb;
                textMask = cursorOn;
            }
        }

        // Track position for end cursor
        lastCursorX = cx + cellStep;
        lastCursorY = cy;

        _col++;
        if (_col >= maxCols) { _col = 0; _row++; }
    }

    // Cursor at end when all chars revealed
    if (visibleChars >= numChars) {
        float cursorOn = step(0.5, fract(TIME * cursorBlink));
        float cursorW = charW * 0.15;
        if (p.x >= lastCursorX && p.x <= lastCursorX + cursorW &&
            p.y >= lastCursorY && p.y <= lastCursorY + charH) {
            textCol = textColor.rgb;
            textMask = cursorOn;
        }
    }

    col = mix(col, textCol, clamp(textMask, 0.0, 1.0));
    if (transparentBg) alpha = clamp(textMask, 0.0, 1.0);

    gl_FragColor = vec4(col, alpha);
}

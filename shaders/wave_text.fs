/*{
  "CATEGORIES": ["Generator", "Text"],
  "DESCRIPTION": "Text waving like a flag — sine wave displacement per letter",
  "INPUTS": [
    { "NAME": "msg", "TYPE": "text", "DEFAULT": "ETHEREA", "MAX_LENGTH": 12 },
    { "NAME": "speed", "TYPE": "float", "MIN": 0.2, "MAX": 5.0, "DEFAULT": 1.5 },
    { "NAME": "amplitude", "TYPE": "float", "MIN": 0.0, "MAX": 0.15, "DEFAULT": 0.06 },
    { "NAME": "frequency", "TYPE": "float", "MIN": 0.5, "MAX": 5.0, "DEFAULT": 2.0 },
    { "NAME": "textColor", "TYPE": "color", "DEFAULT": [1.0, 0.9, 0.3, 1.0] },
    { "NAME": "bgColor", "TYPE": "color", "DEFAULT": [0.0, 0.0, 0.0, 1.0] },
    { "NAME": "textScale", "TYPE": "float", "MIN": 0.3, "MAX": 2.0, "DEFAULT": 1.0 },
    { "NAME": "transparentBg", "TYPE": "bool", "DEFAULT": false }
  ]
}*/

// ---- Font engine: 5x7 bitmap font packed into two floats ----

vec2 charData(int ch) {
    if (ch == 0)  return vec2(1033777.0, 14897.0);
    if (ch == 1)  return vec2(1001022.0, 31281.0);
    if (ch == 2)  return vec2(541230.0, 14896.0);
    if (ch == 3)  return vec2(575068.0, 29265.0);
    if (ch == 4)  return vec2(999967.0, 32272.0);
    if (ch == 5)  return vec2(999952.0, 32272.0);
    if (ch == 6)  return vec2(771630.0, 14896.0);
    if (ch == 7)  return vec2(1033777.0, 17969.0);
    if (ch == 8)  return vec2(135310.0, 14468.0);
    if (ch == 9)  return vec2(68172.0, 7234.0);
    if (ch == 10) return vec2(807505.0, 18004.0);
    if (ch == 11) return vec2(541215.0, 16912.0);
    if (ch == 12) return vec2(706097.0, 18293.0);
    if (ch == 13) return vec2(640561.0, 18229.0);
    if (ch == 14) return vec2(575022.0, 14897.0);
    if (ch == 15) return vec2(999952.0, 31281.0);
    if (ch == 16) return vec2(579149.0, 14897.0);
    if (ch == 17) return vec2(1004113.0, 31281.0);
    if (ch == 18) return vec2(460334.0, 14896.0);
    if (ch == 19) return vec2(135300.0, 31876.0);
    if (ch == 20) return vec2(575022.0, 17969.0);
    if (ch == 21) return vec2(567620.0, 17969.0);
    if (ch == 22) return vec2(710513.0, 17969.0);
    if (ch == 23) return vec2(141873.0, 17962.0);
    if (ch == 24) return vec2(135300.0, 17962.0);
    if (ch == 25) return vec2(139807.0, 31778.0);
    return vec2(0.0, 0.0);
}

float charPixel(int ch, float col, float row) {
    vec2 data = charData(ch);
    float rowIdx = floor(row);
    float rowVal;
    if (rowIdx < 4.0) {
        rowVal = mod(floor(data.x / pow(32.0, rowIdx)), 32.0);
    } else {
        rowVal = mod(floor(data.y / pow(32.0, rowIdx - 4.0)), 32.0);
    }
    return mod(floor(rowVal / pow(2.0, 4.0 - floor(col))), 2.0);
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
    return int(msg_11);
}

int charCount() {
    int n = int(msg_len);
    return n > 0 ? n : 1;
}

// ---- Sample a single character at a UV position within its cell ----
float sampleChar(int ch, vec2 cellUV) {
    // Space character (26) or out-of-range renders nothing
    if (ch == 26) return 0.0;
    if (ch < 0 || ch > 25) return 0.0;

    // Map cellUV to the 5x7 grid
    float col = cellUV.x * 5.0;
    float row = cellUV.y * 7.0;

    // Bounds check
    if (col < 0.0 || col >= 5.0 || row < 0.0 || row >= 7.0) return 0.0;

    return charPixel(ch, col, row);
}

// ---- Main ----

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    float aspect = RENDERSIZE.x / RENDERSIZE.y;

    // Aspect-corrected coordinates, centered
    vec2 p = vec2((uv.x - 0.5) * aspect + 0.5, uv.y);

    int numChars = charCount();

    // Character cell dimensions
    float charW = 0.09 * textScale;
    float charH = charW * 1.5;
    float gapW = charW * 0.25;
    float cellStep = charW + gapW;

    // Total width of the text block
    float totalW = float(numChars) * cellStep - gapW;

    // Starting x position (centered)
    float startX = 0.5 - totalW * 0.5;

    // Accumulate text hits
    float mainHit = 0.0;
    float shadowHit = 0.0;

    // Shadow offset in UV space
    vec2 shadowOff = vec2(0.005, -0.005);

    for (int i = 0; i < 12; i++) {
        if (i >= numChars) break;

        int ch = getChar(i);

        // Per-letter wave phase
        float phase = float(i) * frequency + TIME * speed;

        // Vertical displacement
        float yOff = sin(phase) * amplitude;

        // Tilt from wave derivative (subtle rotation via skew)
        float tilt = cos(phase) * amplitude * 3.0;

        // Cell origin (bottom-left corner before displacement)
        float cellX = startX + float(i) * cellStep;
        float cellY = 0.5 - charH * 0.5;

        // --- Main text ---
        vec2 mainCellUV = vec2(
            (p.x - cellX) / charW,
            (p.y - (cellY + yOff)) / charH
        );

        // Apply tilt (skew x based on y position within cell)
        mainCellUV.x += (mainCellUV.y - 0.5) * tilt;

        if (mainCellUV.x >= 0.0 && mainCellUV.x <= 1.0 && mainCellUV.y >= 0.0 && mainCellUV.y <= 1.0) {
            float px = sampleChar(ch, mainCellUV);
            mainHit = max(mainHit, px);
        }

        // --- Shadow (offset behind main text) ---
        vec2 shadCellUV = vec2(
            (p.x - shadowOff.x - cellX) / charW,
            (p.y - shadowOff.y - (cellY + yOff)) / charH
        );

        // Same tilt for shadow
        shadCellUV.x += (shadCellUV.y - 0.5) * tilt;

        if (shadCellUV.x >= 0.0 && shadCellUV.x <= 1.0 && shadCellUV.y >= 0.0 && shadCellUV.y <= 1.0) {
            float px = sampleChar(ch, shadCellUV);
            shadowHit = max(shadowHit, px);
        }
    }

    // Compose final color
    vec4 bg;
    if (transparentBg) {
        bg = vec4(0.0, 0.0, 0.0, 0.0);
    } else {
        bg = bgColor;
    }

    vec4 result = bg;

    // Shadow layer: dark color at 30% opacity, behind main text
    if (shadowHit > 0.5) {
        vec4 shadColor = vec4(0.0, 0.0, 0.0, 0.3);
        result = vec4(
            mix(result.rgb, shadColor.rgb, shadColor.a),
            result.a + shadColor.a * (1.0 - result.a)
        );
    }

    // Main text layer on top
    if (mainHit > 0.5) {
        result = vec4(textColor.rgb, textColor.a);
    }

    gl_FragColor = result;
}

/*{
  "CATEGORIES": ["Generator", "Text"],
  "DESCRIPTION": "Digifade - glitch dissolve",
  "INPUTS": [
    { "NAME": "msg", "TYPE": "text", "DEFAULT": " ETHEREA", "MAX_LENGTH": 24 },
    { "NAME": "preset", "LABEL": "Style", "TYPE": "long", "VALUES": [0,1], "LABELS": ["Digifade","Digifade Glitch"], "DEFAULT": 0 },
    { "NAME": "fontFamily", "LABEL": "Font", "TYPE": "long", "VALUES": [0,1,2,3], "LABELS": ["Inter","Times New Roman","Libre Caslon","Outfit"], "DEFAULT": 0 },
    { "NAME": "speed", "LABEL": "Speed", "TYPE": "float", "MIN": 0.1, "MAX": 3.0, "DEFAULT": 0.5 },
    { "NAME": "intensity", "LABEL": "Glitch", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "density", "LABEL": "Dissolve", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "textScale", "LABEL": "Size", "TYPE": "float", "MIN": 0.3, "MAX": 2.0, "DEFAULT": 1.0 },
    { "NAME": "textColor", "LABEL": "Color", "TYPE": "color", "DEFAULT": [1.0, 1.0, 1.0, 1.0] },
    { "NAME": "bgColor", "LABEL": "Background", "TYPE": "color", "DEFAULT": [0.0, 0.0, 0.0, 1.0] },
    { "NAME": "transparentBg", "LABEL": "Transparent", "TYPE": "bool", "DEFAULT": true }
  ]
}*/

const float PI = 3.14159265;
const float TWO_PI = 6.28318530;

// Atlas-only font engine (no bitmap fallback — faster ANGLE compile)
float charPixel(int ch, float col, float row) {
    if (ch < 0 || ch > 25) return 0.0;
    vec2 uv = vec2(col / 5.0, row / 7.0);
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) return 0.0;
    return smoothstep(0.1, 0.55, texture2D(fontAtlasTex, vec2((float(ch) + uv.x) / 27.0, uv.y)).r);
}

int getChar(int slot) {
    if (slot == 0)  return int(msg_0);
    if (slot == 1)  return int(msg_1);
    if (slot == 2)  return int(msg_2);
    if (slot == 3)  return int(msg_3);
    if (slot == 4)  return int(msg_4);
    if (slot == 5)  return int(msg_5);
    if (slot == 6)  return int(msg_6);
    if (slot == 7)  return int(msg_7);
    if (slot == 8)  return int(msg_8);
    if (slot == 9)  return int(msg_9);
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
    return int(msg_23);
}

int charCount() {
    int n = int(msg_len);
    return n > 0 ? n : 1;
}

float sampleChar(int ch, vec2 uv) {
    if (ch < 0 || ch > 25) return 0.0;
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) return 0.0;
    return texture2D(fontAtlasTex, vec2((float(ch) + uv.x) / 27.0, uv.y)).r;
}

float hash(float n) { return fract(sin(n * 127.1) * 43758.5453); }

// =======================================================================
// EFFECT: DIGIFADE - glitch dissolve
// =======================================================================

vec4 effectDigifade(vec2 uv, int sub) {
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    int numChars = charCount();
    float glitchAmount = intensity;
    float sliceCount = mix(5.0, 100.0, density);

    float complexity = 1.0, sweepSpeed = 1.0, vertGlitch = 0.0, maxDisp = 0.3;
    if (sub == 1) { complexity = 2.0; sweepSpeed = 1.3; maxDisp = 0.5; vertGlitch = 0.4; }

    float t = TIME * speed * sweepSpeed;
    vec2 p = vec2((uv.x - 0.5) * aspect + 0.5, uv.y);

    // Responsive grid: fit chars into rows that fill the screen
    // Each char cell is 5:7 aspect ratio
    float maxCols = max(1.0, floor(0.9 * aspect / (textScale * (5.0/7.0) * 0.18)));
    int numCols = int(min(maxCols, float(numChars)));
    int numRows = (numChars + numCols - 1) / numCols;
    if (numRows > 6) { numCols = (numChars + 5) / 6; numRows = (numChars + numCols - 1) / numCols; }

    float cH = min(0.18 * textScale, 0.85 / float(numRows));
    float cW = cH * (5.0/7.0);
    float gW = cW * 0.2;
    float rowGap = cH * 0.2;
    float totalH = float(numRows) * cH + float(numRows - 1) * rowGap;
    float baseY = 0.5 + totalH * 0.5 - cH;

    float si = floor(uv.y * sliceCount);
    float n1 = hash(si + floor(t*2.0));
    float n2 = hash(si*3.7 + floor(t*3.0));

    float textHit = 0.0;

    for (int row = 0; row < 6; row++) {
        if (row >= numRows) break;
        // How many chars in this row
        int charsInRow = numChars - row * numCols;
        if (charsInRow > numCols) charsInRow = numCols;

        float rowW = float(charsInRow) * cW + float(charsInRow - 1) * gW;
        float startX = 0.5 - rowW * 0.5;
        float startY = baseY - float(row) * (cH + rowGap);

        float sw = sin(t*0.7)*0.5+0.5;
        float ps = smoothstep(sw-0.15, sw+0.1, (p.x-startX)/max(rowW, 0.001));

        float dx = abs(ps*n1*glitchAmount*maxDisp + ps*sin(si*0.3*complexity+t)*glitchAmount*maxDisp*0.3);
        float dy = vertGlitch > 0.01 ? ps*(n2-0.5)*vertGlitch*glitchAmount*0.06 : 0.0;

        vec2 samp = vec2(p.x - dx, p.y - dy);
        float rx = samp.x - startX, ry = samp.y - startY;

        if (rx >= 0.0 && rx <= rowW && ry >= 0.0 && ry <= cH) {
            float cs = cW + gW;
            float csF = rx / cs;
            int slot = int(floor(csF));
            float clx = fract(csF), cf = cW/cs;
            if (clx < cf && slot >= 0 && slot < charsInRow) {
                int globalIdx = row * numCols + slot;
                if (globalIdx < numChars) {
                    float gc = (clx/cf)*5.0, gr = (ry/cH)*7.0;
                    if (gc >= 0.0 && gc < 5.0 && gr >= 0.0 && gr < 7.0) {
                        int ch = getChar(globalIdx);
                        if (ch >= 0 && ch <= 25) textHit = max(textHit, charPixel(ch, gc, gr));
                    }
                }
            }
        }
    }

    vec3 fc = mix(bgColor.rgb, textColor.rgb, textHit);
    float a = 1.0;
    if (transparentBg) { a = textHit; fc = textColor.rgb; }
    return vec4(fc, a);
}

// =======================================================================
// MAIN
// =======================================================================

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    int p = int(preset);
    vec4 col = effectDigifade(uv, p);

    if (_voiceGlitch > 0.01) {
        float g = _voiceGlitch;
        float t = TIME * 17.0;
        float band = floor(uv.y * mix(8.0, 40.0, g) + t * 3.0);
        float bandNoise = fract(sin(band * 91.7 + t) * 43758.5);
        float bandActive = step(1.0 - g * 0.6, bandNoise);
        float shift = (bandNoise - 0.5) * 0.08 * g * bandActive;
        float chromaAmt = g * 0.015;
        vec2 uvR = uv + vec2(shift + chromaAmt, 0.0);
        vec2 uvB = uv + vec2(shift - chromaAmt, 0.0);
        vec2 uvG = uv + vec2(shift, chromaAmt * 0.5);
        vec4 cR = effectDigifade(uvR, p);
        vec4 cG = effectDigifade(uvG, p);
        vec4 cB = effectDigifade(uvB, p);
        vec4 glitched = vec4(cR.r, cG.g, cB.b, max(max(cR.a, cG.a), cB.a));
        float scanline = 0.95 + 0.05 * sin(uv.y * RENDERSIZE.y * 1.5 + t * 40.0);
        float blockX = floor(uv.x * 6.0);
        float blockY = floor(uv.y * 4.0);
        float blockNoise = fract(sin((blockX + blockY * 7.0) * 113.1 + floor(t * 8.0)) * 43758.5);
        float dropout = step(1.0 - g * 0.15, blockNoise);
        glitched.rgb *= scanline;
        glitched.rgb *= 1.0 - dropout;
        col = mix(col, glitched, smoothstep(0.0, 0.3, g));
    }

    gl_FragColor = col;
}

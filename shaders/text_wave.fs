/*{
  "CATEGORIES": ["Generator", "Text"],
  "DESCRIPTION": "Wave - sine displacement per letter",
  "INPUTS": [
    { "NAME": "msg", "TYPE": "text", "DEFAULT": " ETHEREA", "MAX_LENGTH": 24 },
    { "NAME": "fontFamily", "LABEL": "Font", "TYPE": "long", "VALUES": [0,1,2,3], "LABELS": ["Inter","Times New Roman","Libre Caslon","Outfit"], "DEFAULT": 0 },
    { "NAME": "speed", "LABEL": "Speed", "TYPE": "float", "MIN": 0.1, "MAX": 3.0, "DEFAULT": 0.5 },
    { "NAME": "intensity", "LABEL": "Amplitude", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "density", "LABEL": "Frequency", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "textScale", "LABEL": "Size", "TYPE": "float", "MIN": 0.3, "MAX": 2.0, "DEFAULT": 1.0 },
    { "NAME": "kerning", "LABEL": "Spacing", "TYPE": "float", "MIN": 0.0, "MAX": 3.0, "DEFAULT": 0.4 },
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
// EFFECT: WAVE - sine displacement per letter
// =======================================================================

vec4 effectWave(vec2 uv) {
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    int numChars = charCount();
    float amplitude = mix(0.0, 0.15, intensity);
    float frequency = mix(0.5, 5.0, density);

    vec2 p = vec2((uv.x - 0.5) * aspect + 0.5, uv.y);

    // Single-line layout
    float cW = 0.09 * textScale;
    float cH = cW * 1.5;
    float gW = cW * 0.25 * kerning;
    float cellStep = cW + gW;

    // Scale down to fit width if text is wider than screen
    float totalTextW = float(numChars) * cellStep - gW;
    float availW = aspect * 0.9;
    float fitScale = 1.0;
    if (totalTextW > availW) {
        fitScale = availW / totalTextW;
        cW *= fitScale;
        cH *= fitScale;
        gW *= fitScale;
        cellStep = cW + gW;
        totalTextW = float(numChars) * cellStep - gW;
    }

    float totalH = cH;
    float startY = 0.5;
    float rowStartX = 0.5 - totalTextW * 0.5;

    float mainHit = 0.0, shadowHit = 0.0;
    vec2 so = vec2(0.005, -0.005);

    for (int i = 0; i < 24; i++) {
        if (i >= numChars) break;
        int ch = getChar(i);

        float phase = float(i) * frequency + TIME * speed;
        float yOff = sin(phase) * amplitude;
        float tilt = cos(phase) * amplitude * 3.0;
        float cellX = rowStartX + float(i) * cellStep;
        float cellY = startY;

        vec2 m = vec2((p.x - cellX) / cW, (p.y - (cellY + yOff)) / cH);
        m.x += (m.y - 0.5) * tilt;
        if (m.x >= 0.0 && m.x <= 1.0 && m.y >= 0.0 && m.y <= 1.0)
            mainHit = max(mainHit, sampleChar(ch, m));

        vec2 s = vec2((p.x - so.x - cellX) / cW, (p.y - so.y - (cellY + yOff)) / cH);
        s.x += (s.y - 0.5) * tilt;
        if (s.x >= 0.0 && s.x <= 1.0 && s.y >= 0.0 && s.y <= 1.0)
            shadowHit = max(shadowHit, sampleChar(ch, s));
    }

    vec4 result = transparentBg ? vec4(0.0) : bgColor;
    if (shadowHit > 0.5)
        result = vec4(mix(result.rgb, vec3(0.0), 0.3), result.a + 0.3*(1.0-result.a));
    if (mainHit > 0.5) result = vec4(textColor.rgb, textColor.a);
    return result;
}

// =======================================================================
// MAIN
// =======================================================================

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    vec4 col = effectWave(uv);

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
        vec4 cR = effectWave(uvR);
        vec4 cG = effectWave(uvG);
        vec4 cB = effectWave(uvB);
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

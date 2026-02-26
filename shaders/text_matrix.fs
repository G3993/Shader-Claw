/*{
  "CATEGORIES": ["Generator", "Text"],
  "DESCRIPTION": "Matrix rain — falling character columns with embedded message text",
  "INPUTS": [
    { "NAME": "msg", "TYPE": "text", "DEFAULT": " ETHEREA", "MAX_LENGTH": 24 },
    { "NAME": "speed", "LABEL": "Speed", "TYPE": "float", "MIN": 0.1, "MAX": 3.0, "DEFAULT": 0.5 },
    { "NAME": "intensity", "LABEL": "Brightness", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "density", "LABEL": "Columns", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "textScale", "LABEL": "Size", "TYPE": "float", "MIN": 0.3, "MAX": 2.0, "DEFAULT": 1.0 },
    { "NAME": "textColor", "LABEL": "Color", "TYPE": "color", "DEFAULT": [0.0, 1.0, 0.3, 1.0] },
    { "NAME": "bgColor", "LABEL": "Background", "TYPE": "color", "DEFAULT": [0.0, 0.0, 0.0, 1.0] },
    { "NAME": "transparentBg", "LABEL": "Transparent", "TYPE": "bool", "DEFAULT": false }
  ]
}*/

// Atlas-only font engine (no bitmap fallback — faster ANGLE compile)
float charPixel(int ch, float col, float row) {
    if (ch < 0 || ch > 26) return 0.0;
    float u = (float(ch) + col / 5.0) / 27.0;
    float v = 1.0 - row / 7.0;
    return smoothstep(0.1, 0.55, texture2D(fontAtlasTex, vec2(u, v)).r);
}

int getChar(int slot) {
    if (slot == 0)  return int(msg_0);  if (slot == 1)  return int(msg_1);
    if (slot == 2)  return int(msg_2);  if (slot == 3)  return int(msg_3);
    if (slot == 4)  return int(msg_4);  if (slot == 5)  return int(msg_5);
    if (slot == 6)  return int(msg_6);  if (slot == 7)  return int(msg_7);
    if (slot == 8)  return int(msg_8);  if (slot == 9)  return int(msg_9);
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
    return n > 0 ? n : 1;
}

float hash(float n) { return fract(sin(n * 127.1) * 43758.5453); }

// ═══════════════════════════════════════════════════════════════════════
// MATRIX RAIN
// ═══════════════════════════════════════════════════════════════════════

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    int numChars = charCount();

    float cols = floor(mix(12.0, 50.0, density) / textScale);
    float cellW = 1.0 / cols;
    float cellH = cellW * (7.0 / 5.0) * aspect;
    float rows = ceil(1.0 / cellH);

    float flippedY = 1.0 - uv.y;
    float ci = floor(uv.x / cellW);
    float ri = floor(flippedY / cellH);
    float lx = fract(uv.x / cellW);
    float ly = fract(flippedY / cellH);

    float trailLen = mix(5.0, 30.0, intensity);
    float midCol = floor(cols * 0.5);
    float msgStartRow = floor((rows - float(numChars)) * 0.5);

    float brightness = 0.0;
    float headGlow = 0.0;

    for (int d = 0; d < 3; d++) {
        float dSeed = hash(ci * 13.7 + float(d) * 91.3);
        float dSpeed = (0.3 + dSeed * 1.0) * speed * 8.0;
        float dPhase = hash(ci * 7.3 + float(d) * 43.1) * 100.0;
        float period = rows + trailLen + 10.0;
        float dropPos = mod(TIME * dSpeed + dPhase, period);
        float dist = dropPos - ri;
        if (dist >= 0.0 && dist < trailLen) {
            float t = dist / trailLen;
            float b = 1.0 - t;
            brightness = max(brightness, b * b);
            if (dist < 1.5) {
                headGlow = max(headGlow, 1.0 - dist / 1.5);
            }
        }
    }

    brightness = max(brightness, 0.05);

    float cycleRate = brightness > 0.1
        ? mix(3.0, 12.0, brightness)
        : mix(0.5, 2.0, hash(ci * 3.1 + ri * 7.7));
    float charSeed = hash(ci * 17.3 + ri * 31.7 + floor(TIME * cycleRate) * 0.73);
    int ch = int(mod(charSeed * 26.0, 26.0));

    int msgRow = int(ri - msgStartRow);
    if (abs(ci - midCol) < 0.5 && msgRow >= 0 && msgRow < numChars) {
        ch = getChar(msgRow);
        float pulse = 0.7 + 0.3 * sin(TIME * 2.0 + float(msgRow) * 0.5);
        brightness = max(brightness, pulse);
    }

    float pixel = 0.0;
    float gc = lx * 5.0, gr = ly * 7.0;
    if (gc >= 0.0 && gc < 5.0 && gr >= 0.0 && gr < 7.0) {
        pixel = charPixel(ch, gc, gr);
    }

    vec3 charCol = textColor.rgb * brightness;
    charCol = mix(charCol, vec3(1.0), headGlow * 0.7);

    vec3 fc = transparentBg ? vec3(0.0) : bgColor.rgb;
    float alpha = transparentBg ? 0.0 : 1.0;

    if (pixel > 0.5) {
        fc = charCol;
        if (transparentBg) alpha = clamp(brightness, 0.0, 1.0);
    }

    gl_FragColor = vec4(fc, alpha);
}

/*{
  "CATEGORIES": ["Generator", "Text"],
  "DESCRIPTION": "Typewriter — characters appear one by one with blinking cursor",
  "INPUTS": [
    { "NAME": "msg", "TYPE": "text", "DEFAULT": "ETHEREA", "MAX_LENGTH": 24 },
    { "NAME": "fontFamily", "LABEL": "Font", "TYPE": "long", "VALUES": [0,1,2,3], "LABELS": ["Inter","Times New Roman","Libre Caslon","Outfit"], "DEFAULT": 0 },
    { "NAME": "speed", "LABEL": "Speed", "TYPE": "float", "MIN": 0.5, "MAX": 40.0, "DEFAULT": 12.0 },
    { "NAME": "cursorBlink", "LABEL": "Cursor Blink", "TYPE": "float", "MIN": 0.5, "MAX": 5.0, "DEFAULT": 2.0 },
    { "NAME": "textScale", "LABEL": "Size", "TYPE": "float", "MIN": 0.3, "MAX": 3.0, "DEFAULT": 1.0 },
    { "NAME": "kerning", "LABEL": "Spacing", "TYPE": "float", "MIN": 0.0, "MAX": 3.0, "DEFAULT": 0.4 },
    { "NAME": "textColor", "LABEL": "Color", "TYPE": "color", "DEFAULT": [1.0, 1.0, 1.0, 1.0] },
    { "NAME": "bgColor", "LABEL": "Background", "TYPE": "color", "DEFAULT": [0.02, 0.02, 0.04, 1.0] },
    { "NAME": "transparentBg", "LABEL": "Transparent", "TYPE": "bool", "DEFAULT": true },
    { "NAME": "loop", "LABEL": "Loop", "TYPE": "bool", "DEFAULT": false }
  ]
}*/

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

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    int numChars = charCount();
    float sc = textScale > 0.01 ? textScale : 1.0;
    float kr = kerning > 0.01 ? kerning : 1.0;

    vec3 col = bgColor.rgb;
    float alpha = transparentBg ? 0.0 : 1.0;

    // Aspect-corrected coordinates
    vec2 p = vec2((uv.x - 0.5) * aspect + 0.5, uv.y);

    // Character dimensions — fixed size, scale to fit width if needed
    float charH = 0.18 * sc;
    float charW = charH * (5.0 / 7.0);
    float gap = charW * 0.25 * kr;
    float step_ = charW + gap;

    float totalW = float(numChars) * step_ - gap;
    float maxW = aspect * 0.9;
    if (totalW > maxW) {
        float fit = maxW / totalW;
        charH *= fit;
        charW = charH * (5.0 / 7.0);
        gap = charW * 0.25 * kr;
        step_ = charW + gap;
        totalW = float(numChars) * step_ - gap;
    }

    float originX = 0.5 - totalW * 0.5;
    float originY = 0.5 - charH * 0.5;

    // How many chars revealed so far
    float typeTime = float(numChars) / speed;
    float t = TIME;
    if (loop) {
        float cycle = typeTime + 2.0; // 2s pause before restart
        t = mod(t, cycle);
    }
    int revealed = int(floor(t * speed));
    if (revealed > numChars) revealed = numChars;

    // Render characters
    float textMask = 0.0;
    vec3 textCol = vec3(0.0);

    for (int i = 0; i < 24; i++) {
        if (i >= revealed) break;
        if (i >= numChars) break;

        int ch = getChar(i);
        if (ch < 0 || ch > 25) continue;

        float cx = originX + float(i) * step_;
        vec2 cellUV = vec2((p.x - cx) / charW, (p.y - originY) / charH);

        if (cellUV.x >= 0.0 && cellUV.x <= 1.0 && cellUV.y >= 0.0 && cellUV.y <= 1.0) {
            float s = sampleChar(ch, cellUV);
            if (s > 0.05) {
                textCol = textColor.rgb;
                textMask = max(textMask, smoothstep(0.1, 0.5, s));
            }
        }
    }

    // Blinking cursor
    float cursorX = originX + float(revealed) * step_;
    if (revealed >= numChars) cursorX = originX + totalW + gap;
    float cursorOn = step(0.5, fract(TIME * cursorBlink));
    float cursorW = charW * 0.15;
    if (p.x >= cursorX && p.x <= cursorX + cursorW &&
        p.y >= originY && p.y <= originY + charH) {
        textCol = textColor.rgb;
        textMask = max(textMask, cursorOn);
    }

    col = mix(col, textCol, clamp(textMask, 0.0, 1.0));
    if (transparentBg) alpha = clamp(textMask, 0.0, 1.0);

    gl_FragColor = vec4(col, alpha);
}

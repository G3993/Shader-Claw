/*{
  "CATEGORIES": ["Generator", "Text"],
  "DESCRIPTION": "Typewriter — characters appear one by one with blinking cursor",
  "INPUTS": [
    { "NAME": "msg", "TYPE": "text", "DEFAULT": "ETHEREA", "MAX_LENGTH": 64 },
    { "NAME": "fontFamily", "LABEL": "Font", "TYPE": "long", "VALUES": [0,1,2,3], "LABELS": ["Inter","Times New Roman","Libre Caslon","Outfit"], "DEFAULT": 0 },
    { "NAME": "speed", "LABEL": "Speed", "TYPE": "float", "MIN": 0.5, "MAX": 40.0, "DEFAULT": 12.0 },
    { "NAME": "cursorBlink", "LABEL": "Cursor Blink", "TYPE": "float", "MIN": 0.5, "MAX": 5.0, "DEFAULT": 2.0 },
    { "NAME": "textScale", "LABEL": "Size", "TYPE": "float", "MIN": 0.01, "MAX": 1.0, "DEFAULT": 0.3 },
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
    if (slot == 24) return int(msg_24);
    if (slot == 25) return int(msg_25);
    if (slot == 26) return int(msg_26);
    if (slot == 27) return int(msg_27);
    if (slot == 28) return int(msg_28);
    if (slot == 29) return int(msg_29);
    if (slot == 30) return int(msg_30);
    if (slot == 31) return int(msg_31);
    if (slot == 32) return int(msg_32);
    if (slot == 33) return int(msg_33);
    if (slot == 34) return int(msg_34);
    if (slot == 35) return int(msg_35);
    if (slot == 36) return int(msg_36);
    if (slot == 37) return int(msg_37);
    if (slot == 38) return int(msg_38);
    if (slot == 39) return int(msg_39);
    if (slot == 40) return int(msg_40);
    if (slot == 41) return int(msg_41);
    if (slot == 42) return int(msg_42);
    if (slot == 43) return int(msg_43);
    if (slot == 44) return int(msg_44);
    if (slot == 45) return int(msg_45);
    if (slot == 46) return int(msg_46);
    if (slot == 47) return int(msg_47);
    if (slot == 48) return int(msg_48);
    if (slot == 49) return int(msg_49);
    if (slot == 50) return int(msg_50);
    if (slot == 51) return int(msg_51);
    if (slot == 52) return int(msg_52);
    if (slot == 53) return int(msg_53);
    if (slot == 54) return int(msg_54);
    if (slot == 55) return int(msg_55);
    if (slot == 56) return int(msg_56);
    if (slot == 57) return int(msg_57);
    if (slot == 58) return int(msg_58);
    if (slot == 59) return int(msg_59);
    if (slot == 60) return int(msg_60);
    if (slot == 61) return int(msg_61);
    if (slot == 62) return int(msg_62);
    if (slot == 63) return int(msg_63);
    return 26;
}

int charCount() {
    int n = int(msg_len);
    if (n <= 0) return 7;
    if (n > 64) return 64;
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

    vec2 p = vec2((uv.x - 0.5) * aspect + 0.5, uv.y);
    float maxW = aspect * 0.9;

    // Fixed character size
    float charH = 0.18 * sc;
    float charW = charH * (5.0 / 7.0);
    float gap = charW * 0.25 * kr;
    float cellStep = charW + gap;

    // How many chars fit on screen at this size
    int maxVisible = int(floor((maxW + gap) / cellStep));
    if (maxVisible < 1) maxVisible = 1;
    if (maxVisible > 64) maxVisible = 64;

    float originY = 0.5 - charH * 0.5;

    // Typewriter reveal
    float typeTime = float(numChars) / speed;
    float t = TIME;
    if (loop) {
        float cycle = typeTime + 2.0;
        t = mod(t, cycle);
    }
    int revealed = int(floor(t * speed));
    if (revealed > numChars) revealed = numChars;

    // Sliding window: when text exceeds screen, drop old chars off the left
    int startIdx = 0;
    if (revealed > maxVisible) startIdx = revealed - maxVisible;
    int showCount = revealed - startIdx;

    // Center visible text
    float visibleW = float(showCount) * cellStep - gap;
    if (showCount <= 0) visibleW = 0.0;
    float originX = 0.5 - visibleW * 0.5;

    // Render characters
    float textMask = 0.0;
    vec3 textCol = vec3(0.0);
    float lastX = originX;

    for (int i = 0; i < 64; i++) {
        if (i >= showCount) break;
        int charIdx = startIdx + i;
        if (charIdx >= numChars) break;

        int ch = getChar(charIdx);
        float cx = originX + float(i) * cellStep;

        if (ch >= 0 && ch <= 25) {
            vec2 cellUV = vec2((p.x - cx) / charW, (p.y - originY) / charH);
            if (cellUV.x >= 0.0 && cellUV.x <= 1.0 && cellUV.y >= 0.0 && cellUV.y <= 1.0) {
                float s = sampleChar(ch, cellUV);
                if (s > 0.05) {
                    textCol = textColor.rgb;
                    textMask = max(textMask, smoothstep(0.1, 0.5, s));
                }
            }
        }

        lastX = cx + cellStep;
    }

    // Blinking cursor after last char
    float cursorOn = step(0.5, fract(TIME * cursorBlink));
    float cursorW = charW * 0.15;
    if (p.x >= lastX && p.x <= lastX + cursorW &&
        p.y >= originY && p.y <= originY + charH) {
        textCol = textColor.rgb;
        textMask = max(textMask, cursorOn);
    }

    col = mix(col, textCol, clamp(textMask, 0.0, 1.0));
    if (transparentBg) alpha = clamp(textMask, 0.0, 1.0);

    gl_FragColor = vec4(col, alpha);
}

/*
{
  "CATEGORIES": ["Generator", "Text"],
  "DESCRIPTION": "Animated text with cycling font styles per letter",
  "INPUTS": [
    { "NAME": "msg", "TYPE": "text", "DEFAULT": "ETHEREA", "MAX_LENGTH": 12 },
    { "NAME": "cycleSpeed", "TYPE": "float", "MIN": 0.2, "MAX": 5.0, "DEFAULT": 1.5 },
    { "NAME": "textScale", "TYPE": "float", "MIN": 0.3, "MAX": 2.0, "DEFAULT": 1.0 },
    { "NAME": "bounce", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.3 },
    { "NAME": "textColor", "TYPE": "color", "DEFAULT": [1.0, 1.0, 1.0, 1.0] },
    { "NAME": "bgColor", "TYPE": "color", "DEFAULT": [0.0, 0.0, 0.0, 1.0] },
    { "NAME": "transparentBg", "TYPE": "bool", "DEFAULT": true }
  ]
}
*/

// --- 5x7 bitmap font, packed as 2 floats per character ---
// lo = row0 + row1*32 + row2*1024 + row3*32768  (rows 0-3)
// hi = row4 + row5*32 + row6*1024               (rows 4-6)
// Row bits: bit4=leftmost pixel, bit0=rightmost (5 wide)

vec2 charData(int ch) {
    // A-Z = 0-25, space = 26
    if (ch == 0)  return vec2(1033777.0, 14897.0);  // A
    if (ch == 1)  return vec2(1001022.0, 31281.0);  // B
    if (ch == 2)  return vec2(541230.0, 14896.0);   // C
    if (ch == 3)  return vec2(575068.0, 29265.0);   // D
    if (ch == 4)  return vec2(999967.0, 32272.0);   // E
    if (ch == 5)  return vec2(999952.0, 32272.0);   // F
    if (ch == 6)  return vec2(771630.0, 14896.0);   // G
    if (ch == 7)  return vec2(1033777.0, 17969.0);  // H
    if (ch == 8)  return vec2(135310.0, 14468.0);   // I
    if (ch == 9)  return vec2(68172.0, 7234.0);     // J
    if (ch == 10) return vec2(807505.0, 18004.0);   // K
    if (ch == 11) return vec2(541215.0, 16912.0);   // L
    if (ch == 12) return vec2(706097.0, 18293.0);   // M
    if (ch == 13) return vec2(640561.0, 18229.0);   // N
    if (ch == 14) return vec2(575022.0, 14897.0);   // O
    if (ch == 15) return vec2(999952.0, 31281.0);   // P
    if (ch == 16) return vec2(579149.0, 14897.0);   // Q
    if (ch == 17) return vec2(1004113.0, 31281.0);  // R
    if (ch == 18) return vec2(460334.0, 14896.0);   // S
    if (ch == 19) return vec2(135300.0, 31876.0);   // T
    if (ch == 20) return vec2(575022.0, 17969.0);   // U
    if (ch == 21) return vec2(567620.0, 17969.0);   // V
    if (ch == 22) return vec2(710513.0, 17969.0);   // W
    if (ch == 23) return vec2(141873.0, 17962.0);   // X
    if (ch == 24) return vec2(135300.0, 17962.0);   // Y
    if (ch == 25) return vec2(139807.0, 31778.0);   // Z
    return vec2(0.0, 0.0); // space
}

// Extract pixel from 5x7 bitmap
float charPixel(int ch, float col, float row) {
    vec2 data = charData(ch);
    float rowIdx = floor(row);
    float rowVal;
    if (rowIdx < 4.0) {
        rowVal = mod(floor(data.x / pow(32.0, rowIdx)), 32.0);
    } else {
        rowVal = mod(floor(data.y / pow(32.0, rowIdx - 4.0)), 32.0);
    }
    float bitPos = 4.0 - floor(col);
    return mod(floor(rowVal / pow(2.0, bitPos)), 2.0);
}

// Get character at slot index (0-11) from text uniforms
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
    return int(msg_11);
}

int charCount() {
    int n = int(msg_len);
    return n > 0 ? n : 1;
}

// --- Font style renderers ---

float styleBlock(vec2 lp) {
    return 1.0;
}

float styleDots(vec2 lp) {
    float d = length(lp - 0.5);
    return smoothstep(0.45, 0.35, d);
}

float styleOutline(vec2 lp, float col, float row, int ch) {
    float neighbors = 0.0;
    neighbors += charPixel(ch, col - 1.0, row);
    neighbors += charPixel(ch, col + 1.0, row);
    neighbors += charPixel(ch, col, row - 1.0);
    neighbors += charPixel(ch, col, row + 1.0);
    if (neighbors > 3.5) return 0.0;
    return 1.0;
}

float styleScanline(vec2 lp) {
    return step(0.35, fract(lp.y * 3.0));
}

float styleDiamond(vec2 lp) {
    vec2 c = abs(lp - 0.5);
    return smoothstep(0.5, 0.4, c.x + c.y);
}

float styleCross(vec2 lp) {
    float cx = smoothstep(0.42, 0.38, abs(lp.x - 0.5));
    float cy = smoothstep(0.42, 0.38, abs(lp.y - 0.5));
    return max(cx, cy);
}

float styleVBars(vec2 lp) {
    return smoothstep(0.42, 0.35, abs(lp.x - 0.5));
}

float styleNeon(vec2 lp) {
    float d = length(lp - 0.5);
    return exp(-d * d * 8.0) * 1.5;
}

float hash(float n) {
    return fract(sin(n * 127.1) * 43758.5453);
}

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    float aspect = RENDERSIZE.x / RENDERSIZE.y;

    vec3 col = bgColor.rgb;
    float alpha = 1.0;

    // Subtle animated background gradient
    float bgGrad = uv.y * 0.3 + 0.05 * sin(uv.x * 3.0 + TIME * 0.5);
    col += vec3(0.02, 0.01, 0.03) * bgGrad;

    // If transparent bg, start with zero alpha
    if (transparentBg) alpha = 0.0;

    // Character layout
    int numChars = charCount();

    float charW = 0.09 * textScale;
    float charH = charW * 1.5;
    float gap = charW * 0.25;
    float totalW = float(numChars) * charW + float(numChars - 1) * gap;

    float startX = 0.5 - totalW * 0.5;
    float baseY = 0.5 - charH * 0.5;

    vec2 p = vec2((uv.x - 0.5) * aspect + 0.5, uv.y);

    float textMask = 0.0;
    vec3 textCol = vec3(0.0);
    float glowAccum = 0.0;

    for (int i = 0; i < 12; i++) {
        if (i >= numChars) break;

        int ch = getChar(i);
        if (ch == 26) continue;

        float phase = float(i) * 1.3 + TIME * cycleSpeed;
        int style = int(mod(floor(phase), 8.0));

        float bouncePhase = float(i) * 0.8 + TIME * 2.5;
        float yOff = sin(bouncePhase) * 0.015 * bounce;
        float scalePulse = 1.0 + sin(bouncePhase + 1.0) * 0.05 * bounce;

        float cx = startX + float(i) * (charW + gap);
        float cy = baseY + yOff;

        vec2 cellUV = vec2(
            (p.x - cx) / (charW * scalePulse),
            (p.y - cy) / (charH * scalePulse)
        );

        if (cellUV.x < -0.1 || cellUV.x > 1.1 || cellUV.y < -0.1 || cellUV.y > 1.1) continue;

        vec2 grid = cellUV * vec2(5.0, 7.0);
        float gcol = floor(grid.x);
        float grow = floor(grid.y);

        if (cellUV.x >= 0.0 && cellUV.x <= 1.0 && cellUV.y >= 0.0 && cellUV.y <= 1.0) {
            if (gcol >= 0.0 && gcol < 5.0 && grow >= 0.0 && grow < 7.0) {
                float filled = charPixel(ch, gcol, grow);

                if (filled > 0.5) {
                    vec2 lp = fract(grid);
                    float intensity = 1.0;

                    if (style == 0) {
                        intensity = styleBlock(lp);
                    } else if (style == 1) {
                        intensity = styleDots(lp);
                    } else if (style == 2) {
                        intensity = styleOutline(lp, gcol, grow, ch);
                    } else if (style == 3) {
                        intensity = styleScanline(lp);
                    } else if (style == 4) {
                        intensity = styleDiamond(lp);
                    } else if (style == 5) {
                        intensity = styleCross(lp);
                    } else if (style == 6) {
                        intensity = styleVBars(lp);
                    } else {
                        intensity = styleNeon(lp);
                    }

                    vec3 lc = textColor.rgb;

                    if (style == 7) {
                        intensity *= 1.3;
                    }

                    textCol = max(textCol, lc * intensity);
                    textMask = max(textMask, intensity);
                }
            }
        }

        vec2 cellCenter = vec2(cx + charW * 0.5, cy + charH * 0.5);
        float glowDist = length((p - cellCenter) * vec2(1.0, 0.7));
        float glow = exp(-glowDist * glowDist / (charW * charW * 2.0)) * 0.15;
        float glowPulse = 0.8 + 0.2 * sin(phase * 2.0);
        glowAccum += glow * glowPulse;
    }

    // Composite
    col = mix(col, textCol, clamp(textMask, 0.0, 1.0));

    if (!transparentBg) {
        col += textColor.rgb * glowAccum;
    }

    // Vignette
    float vig = 1.0 - 0.3 * length((uv - 0.5) * 1.5);
    col *= vig;

    // Alpha: text + glow visible, background transparent if toggled
    if (transparentBg) {
        alpha = clamp(textMask, 0.0, 1.0);
    }

    gl_FragColor = vec4(col, alpha);
}

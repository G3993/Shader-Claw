/*
{
  "CATEGORIES": ["Generator", "Text"],
  "DESCRIPTION": "Text with moving highlight sweep — chrome-like shine effect",
  "INPUTS": [
    { "NAME": "msg", "TYPE": "text", "DEFAULT": "ETHEREA", "MAX_LENGTH": 12 },
    { "NAME": "speed", "TYPE": "float", "MIN": 0.2, "MAX": 5.0, "DEFAULT": 1.0 },
    { "NAME": "textColor", "TYPE": "color", "DEFAULT": [0.7, 0.7, 0.8, 1.0] },
    { "NAME": "shineColor", "TYPE": "color", "DEFAULT": [1.0, 1.0, 1.0, 1.0] },
    { "NAME": "bgColor", "TYPE": "color", "DEFAULT": [0.02, 0.02, 0.05, 1.0] },
    { "NAME": "textScale", "TYPE": "float", "MIN": 0.3, "MAX": 2.0, "DEFAULT": 1.0 },
    { "NAME": "shineWidth", "TYPE": "float", "MIN": 0.05, "MAX": 0.5, "DEFAULT": 0.15 },
    { "NAME": "transparentBg", "TYPE": "bool", "DEFAULT": false }
  ]
}
*/

// --- Text uniforms (ISF text type auto-generates these) ---
// uniform float msg_0 .. msg_11, msg_len

// --- Bitmap font engine (5x7 packed, A-Z + space) ---

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
    if (rowIdx < 4.0) { rowVal = mod(floor(data.x / pow(32.0, rowIdx)), 32.0); }
    else { rowVal = mod(floor(data.y / pow(32.0, rowIdx - 4.0)), 32.0); }
    return mod(floor(rowVal / pow(2.0, 4.0 - floor(col))), 2.0);
}

int getChar(int slot) {
    if (slot == 0) return int(msg_0); if (slot == 1) return int(msg_1);
    if (slot == 2) return int(msg_2); if (slot == 3) return int(msg_3);
    if (slot == 4) return int(msg_4); if (slot == 5) return int(msg_5);
    if (slot == 6) return int(msg_6); if (slot == 7) return int(msg_7);
    if (slot == 8) return int(msg_8); if (slot == 9) return int(msg_9);
    if (slot == 10) return int(msg_10); return int(msg_11);
}

int charCount() { int n = int(msg_len); return n > 0 ? n : 1; }

// --- Main ---

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    float aspect = RENDERSIZE.x / RENDERSIZE.y;

    // Aspect-corrected coordinates (centered)
    vec2 p = vec2((uv.x - 0.5) * aspect + 0.5, uv.y);

    // Character layout dimensions
    float charW = 0.09 * textScale;
    float charH = charW * 1.5;
    float gap = charW * 0.25;
    int numChars = charCount();
    float totalW = float(numChars) * charW + float(numChars - 1) * gap;

    // Center text horizontally, vertically at 0.5
    float startX = 0.5 - totalW * 0.5;
    float startY = 0.5 - charH * 0.5;

    // Determine if this pixel is on a text character and its x position in the text block
    float textMask = 0.0;
    float pixelX = 0.0;
    float pixelY = 0.0;

    for (int i = 0; i < 12; i++) {
        if (i >= numChars) break;

        int ch = getChar(i);

        // Skip space characters (26 = space)
        if (ch == 26) continue;

        float cx = startX + float(i) * (charW + gap);
        float cy = startY;

        // Check if pixel is within this character's bounding box
        if (p.x >= cx && p.x < cx + charW && p.y >= cy && p.y < cy + charH) {
            // Map to character grid (5 cols x 7 rows)
            float localX = (p.x - cx) / charW;
            float localY = (p.y - cy) / charH;

            float col = localX * 5.0;
            float row = localY * 7.0;

            if (col >= 0.0 && col < 5.0 && row >= 0.0 && row < 7.0) {
                float px = charPixel(ch, col, row);
                if (px > 0.5) {
                    textMask = 1.0;
                    pixelX = p.x - startX;
                    pixelY = (p.y - startY) / charH;
                }
            }
        }
    }

    // --- Shine calculation ---
    // The shine sweeps diagonally from left to right across the text, wrapping around
    float sweepRange = totalW + shineWidth * 2.0;
    float shinePos = mod(TIME * speed * 0.3, sweepRange) - shineWidth;

    // Diagonal shine: offset x position by y to create an angled sweep line
    float diagonalOffset = pixelY * 0.3 * totalW;
    float distToShine = abs(pixelX - shinePos + diagonalOffset);

    // Broad shine glow (falls off smoothly from the shine center)
    float shineAmount = smoothstep(shineWidth, 0.0, distToShine);

    // Tight specular highlight at the very center of the shine band
    float specWidth = shineWidth * 0.12;
    float specular = smoothstep(specWidth, 0.0, distToShine);
    specular = specular * specular; // sharpen the highlight

    // --- Compose final color ---
    vec4 finalColor;

    if (textMask > 0.5) {
        // Base text color
        vec3 col = textColor.rgb;

        // Mix in shine glow
        col = mix(col, shineColor.rgb, shineAmount * 0.7);

        // Add specular on top (additive for bright punch)
        col += shineColor.rgb * specular * 1.5;

        // Clamp to prevent exceeding valid range
        col = min(col, vec3(1.0));

        finalColor = vec4(col, textColor.a);
    } else {
        // Background
        if (transparentBg) {
            finalColor = vec4(0.0, 0.0, 0.0, 0.0);
        } else {
            finalColor = bgColor;
        }
    }

    gl_FragColor = finalColor;
}

/*{
  "CATEGORIES": ["Generator", "Text"],
  "DESCRIPTION": "Text with animated digital glitch dissolve effect",
  "INPUTS": [
    { "NAME": "msg", "TYPE": "text", "DEFAULT": "ETHEREA", "MAX_LENGTH": 12 },
    { "NAME": "preset", "TYPE": "long", "VALUES": [0,1,2,3,4,5,6], "LABELS": ["All Yours","Just OK","Not So Good","Cheer","Date","Hopes","Circle"], "DEFAULT": 0 },
    { "NAME": "speed", "TYPE": "float", "MIN": 0.1, "MAX": 3.0, "DEFAULT": 0.5 },
    { "NAME": "glitchAmount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "sliceCount", "TYPE": "float", "MIN": 5.0, "MAX": 100.0, "DEFAULT": 30.0 },
    { "NAME": "textColor", "TYPE": "color", "DEFAULT": [1.0, 1.0, 1.0, 1.0] },
    { "NAME": "bgColor", "TYPE": "color", "DEFAULT": [0.0, 0.0, 0.0, 1.0] },
    { "NAME": "textScale", "TYPE": "float", "MIN": 0.3, "MAX": 2.0, "DEFAULT": 1.0 },
    { "NAME": "transparentBg", "TYPE": "bool", "DEFAULT": false }
  ]
}*/

// -- Font engine ──────────────────────────────────────────────────────

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

// -- Pseudo-random hash ──────────────────────────────────────────────

float hash(float n) {
    return fract(sin(n * 127.1) * 43758.5453);
}

// -- Main ─────────────────────────────────────────────────────────────

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    float aspect = RENDERSIZE.x / RENDERSIZE.y;

    int numChars = charCount();
    int presetIdx = int(preset);

    // Preset parameters
    float complexity = 1.0;    // how jagged/complex the slice displacements are
    float sweepSpeed = 1.0;    // how fast the glitch boundary moves
    float vertGlitch = 0.0;    // vertical displacement component
    float maxDisp = 0.3;       // max horizontal displacement
    float sliceMult = 1.0;     // multiplier on slice count

    // Presets
    if (presetIdx == 0) {
        // All Yours: classic right-dissolve, moderate
        complexity = 1.0; sweepSpeed = 1.0; maxDisp = 0.3;
    } else if (presetIdx == 1) {
        // Just OK: gentle, subtle
        complexity = 0.5; sweepSpeed = 0.6; maxDisp = 0.15;
    } else if (presetIdx == 2) {
        // Not So Good: aggressive, both axes
        complexity = 2.0; sweepSpeed = 1.3; maxDisp = 0.5; vertGlitch = 0.4;
    } else if (presetIdx == 3) {
        // Cheer: fast, energetic
        complexity = 1.5; sweepSpeed = 2.5; maxDisp = 0.25;
    } else if (presetIdx == 4) {
        // Date: slow, dreamy
        complexity = 0.7; sweepSpeed = 0.4; maxDisp = 0.2;
    } else if (presetIdx == 5) {
        // Hopes: fine slices, detailed breakup
        complexity = 1.0; sweepSpeed = 0.8; maxDisp = 0.25; sliceMult = 2.5;
    } else if (presetIdx == 6) {
        // Circle: radial dissolve from center
        complexity = 1.0; sweepSpeed = 1.0; maxDisp = 0.3;
    }

    float t = TIME * speed * sweepSpeed;
    float effectiveSlices = sliceCount * sliceMult;

    // ── Centered text layout (no displacement yet) ──────────────────
    // Aspect-corrected coordinates centered at 0.5
    vec2 p = vec2((uv.x - 0.5) * aspect + 0.5, uv.y);

    float charH = 0.18 * textScale;
    float charW = charH * (5.0 / 7.0);
    float gapW = charW * 0.2;
    float totalW = float(numChars) * charW + float(numChars - 1) * gapW;

    float startX = 0.5 - totalW * 0.5;
    float startY = 0.5 - charH * 0.5;

    // ── Compute glitch displacement ─────────────────────────────────
    // Slice index based on actual pixel Y
    float sliceIdx = floor(uv.y * effectiveSlices);

    // Per-slice random values (change over time for animation)
    float n1 = hash(sliceIdx + floor(t * 2.0));
    float n2 = hash(sliceIdx * 3.7 + floor(t * 3.0));
    float n3 = hash(sliceIdx * 7.3 + floor(t * 1.5));

    // Glitch boundary: sweeps back and forth across the text
    // Using sin for smooth oscillation
    float sweepPos = sin(t * 0.7) * 0.5 + 0.5; // 0..1

    // How far past the sweep boundary this pixel is (in text-relative coords)
    float textRelX = (p.x - startX) / totalW; // 0..1 within text block
    float pastSweep;

    if (presetIdx == 6) {
        // Circle: radial distance from text center
        vec2 textCenter = vec2(0.5, 0.5);
        float dist = length((p - textCenter) * vec2(1.0, 1.4));
        float sweepR = sweepPos * 0.3;
        pastSweep = smoothstep(sweepR - 0.05, sweepR + 0.15, dist);
    } else {
        // Horizontal sweep from left to right
        pastSweep = smoothstep(sweepPos - 0.15, sweepPos + 0.1, textRelX);
    }

    // Displacement: slices past the sweep get shifted
    float dispX = pastSweep * n1 * glitchAmount * maxDisp;
    // Add some complexity with secondary noise
    dispX += pastSweep * sin(sliceIdx * 0.3 * complexity + t) * glitchAmount * maxDisp * 0.3;
    // Ensure displacement goes rightward (positive X)
    dispX = abs(dispX);

    float dispY = 0.0;
    if (vertGlitch > 0.01) {
        dispY = pastSweep * (n2 - 0.5) * vertGlitch * glitchAmount * 0.06;
    }

    // ── Sample text at displaced position ───────────────────────────
    // Shift the sampling point LEFT to create rightward displacement of content
    vec2 samp = vec2(p.x - dispX, p.y - dispY);

    float relX = samp.x - startX;
    float relY = samp.y - startY;

    float textHit = 0.0;

    if (relX >= 0.0 && relX <= totalW && relY >= 0.0 && relY <= charH) {
        float charStep = charW + gapW;
        float charSlotF = relX / charStep;
        int charSlot = int(floor(charSlotF));
        float cellLocalX = fract(charSlotF);
        float charFrac = charW / charStep;

        if (cellLocalX < charFrac && charSlot >= 0 && charSlot < numChars) {
            float glyphX = cellLocalX / charFrac;
            float glyphY = relY / charH;

            float gcol = glyphX * 5.0;
            float grow = glyphY * 7.0;

            if (gcol >= 0.0 && gcol < 5.0 && grow >= 0.0 && grow < 7.0) {
                int ch = -1;
                if (charSlot == 0) ch = getChar(0);
                else if (charSlot == 1) ch = getChar(1);
                else if (charSlot == 2) ch = getChar(2);
                else if (charSlot == 3) ch = getChar(3);
                else if (charSlot == 4) ch = getChar(4);
                else if (charSlot == 5) ch = getChar(5);
                else if (charSlot == 6) ch = getChar(6);
                else if (charSlot == 7) ch = getChar(7);
                else if (charSlot == 8) ch = getChar(8);
                else if (charSlot == 9) ch = getChar(9);
                else if (charSlot == 10) ch = getChar(10);
                else if (charSlot == 11) ch = getChar(11);

                if (ch >= 0 && ch <= 25) {
                    textHit = charPixel(ch, gcol, grow);
                }
            }
        }
    }

    // ── Output ──────────────────────────────────────────────────────
    vec3 finalCol = mix(bgColor.rgb, textColor.rgb, textHit);
    float alpha = 1.0;

    if (transparentBg) {
        alpha = textHit;
        finalCol = textColor.rgb;
    }

    gl_FragColor = vec4(finalCol, alpha);
}

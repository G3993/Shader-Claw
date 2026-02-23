/*{
  "CATEGORIES": ["Generator", "Text"],
  "DESCRIPTION": "Text on a waving flag surface with 3D shading and presets",
  "INPUTS": [
    { "NAME": "msg", "TYPE": "text", "DEFAULT": "ETHEREA", "MAX_LENGTH": 12 },
    { "NAME": "preset", "TYPE": "long", "VALUES": [0,1,2,3,4,5,6,7], "LABELS": ["Banner","Twist","Flat Sea","Barber","Origami","Cola Waves","B&W","Newsprint"], "DEFAULT": 0 },
    { "NAME": "speed", "TYPE": "float", "MIN": 0.1, "MAX": 3.0, "DEFAULT": 0.5 },
    { "NAME": "waveSize", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "rowCount", "TYPE": "float", "MIN": 4.0, "MAX": 20.0, "DEFAULT": 10.0 },
    { "NAME": "textColor", "TYPE": "color", "DEFAULT": [1.0, 1.0, 1.0, 1.0] },
    { "NAME": "bgColor", "TYPE": "color", "DEFAULT": [0.0, 0.0, 0.0, 1.0] },
    { "NAME": "textScale", "TYPE": "float", "MIN": 0.3, "MAX": 2.0, "DEFAULT": 1.0 },
    { "NAME": "transparentBg", "TYPE": "bool", "DEFAULT": false }
  ]
}*/

// ── Font engine ──────────────────────────────────────────────────────

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

// ── Main ─────────────────────────────────────────────────────────────

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    float aspect = RENDERSIZE.x / RENDERSIZE.y;

    int numChars = charCount();
    int presetIdx = int(preset);

    // ── Preset parameters ────────────────────────────────────────────
    float waveAmp = 1.0;      // vertical wave amplitude
    float waveFreq = 3.0;     // how many wave peaks across the flag
    float xShiftAmp = 1.0;    // horizontal displacement strength
    float rowMult = 1.0;      // multiplier on rowCount
    float shadeMult = 1.0;    // shading contrast
    float anchorAmt = 0.5;    // 0 = free, 1 = anchored at left edge
    bool sharpFold = false;
    float extraFreq = 0.0;    // secondary wave frequency

    if (presetIdx == 0) {
        // Banner: classic waving flag, anchored left
        waveAmp = 1.0; waveFreq = 3.0; xShiftAmp = 0.8; anchorAmt = 0.7;
    } else if (presetIdx == 1) {
        // Twist: strong twist, both axes
        waveAmp = 1.3; waveFreq = 2.0; xShiftAmp = 1.5; anchorAmt = 0.0;
        extraFreq = 1.5;
    } else if (presetIdx == 2) {
        // Flat Sea: gentle calm waves
        waveAmp = 0.3; waveFreq = 2.0; xShiftAmp = 0.3; anchorAmt = 0.0;
    } else if (presetIdx == 3) {
        // Barber: diagonal folds
        waveAmp = 1.0; waveFreq = 4.0; xShiftAmp = 1.2; anchorAmt = 0.0;
    } else if (presetIdx == 4) {
        // Origami: sharp angular folds
        sharpFold = true;
        waveAmp = 1.2; waveFreq = 4.0; xShiftAmp = 0.5; anchorAmt = 0.3;
    } else if (presetIdx == 5) {
        // Cola Waves: overlapping frequencies
        waveAmp = 0.8; waveFreq = 3.0; xShiftAmp = 0.7; anchorAmt = 0.0;
        extraFreq = 2.3;
    } else if (presetIdx == 6) {
        // B&W: bold, high contrast
        waveAmp = 1.0; waveFreq = 3.0; xShiftAmp = 0.8; anchorAmt = 0.5;
        rowMult = 1.5; shadeMult = 1.8;
    } else if (presetIdx == 7) {
        // Newsprint: dense, subtle
        waveAmp = 0.2; waveFreq = 2.0; xShiftAmp = 0.2; anchorAmt = 0.0;
        rowMult = 2.0;
    }

    float rows = floor(rowCount * rowMult);

    // ── Flag surface: warp UV through a sine wave field ──────────────
    // The flag waves vertically (Y displacement) as a function of X.
    // Amplitude increases from left to right (anchored pole effect).
    float flagX = uv.x;
    float flagY = uv.y;

    // Envelope: how much wave at this X position (more toward right)
    float envelope = mix(1.0, flagX, anchorAmt);
    envelope = clamp(envelope, 0.0, 1.0);

    // Primary wave: Y displacement based on X position
    float t = TIME * speed * 2.5;
    float wavePhase = flagX * waveFreq * 6.2832 - t;
    float yWave;
    if (sharpFold) {
        yWave = abs(sin(wavePhase)) * 2.0 - 1.0;
    } else {
        yWave = sin(wavePhase);
    }

    // Secondary wave for complexity
    if (extraFreq > 0.1) {
        yWave += sin(flagX * extraFreq * waveFreq * 6.2832 - t * 1.3) * 0.4;
    }

    // Add a cross-wave (slight Y-dependent wave for diagonal folds)
    if (presetIdx == 3) {
        yWave += sin(flagY * 4.0 * 6.2832 + t * 0.7) * 0.3;
    }

    float yDisp = yWave * waveSize * waveAmp * 0.15 * envelope;

    // Warp the Y coordinate
    float warpedY = flagY + yDisp;

    // X displacement based on wave derivative (horizontal ripple)
    float dWave = cos(wavePhase) * waveFreq * 6.2832;
    float xDisp = dWave * waveSize * xShiftAmp * 0.02 * envelope;
    float warpedX = flagX + xDisp;

    // ── Determine row from warped Y ──────────────────────────────────
    float rowH = 1.0 / rows;
    float rowIdx = floor(warpedY / rowH);
    rowIdx = clamp(rowIdx, 0.0, rows - 1.0);
    float localY = fract(warpedY / rowH);

    // ── 3D shading from wave surface normal ──────────────────────────
    // The derivative of Y displacement w.r.t. X gives the surface tilt
    float surfSlope = dWave * waveSize * waveAmp * 0.15 * envelope;
    // Light from the right: shade based on slope
    float shade = 0.5 + 0.5 * (surfSlope / (abs(surfSlope) + 0.3));
    shade = clamp(shade * shadeMult, 0.08, 1.0);

    // ── Character layout ─────────────────────────────────────────────
    float charH = rowH;
    float charW = charH * (5.0 / 7.0) * (1.0 / aspect) * textScale;
    float gapW = charW * 0.15;
    float wordW = float(numChars) * (charW + gapW);

    // Tile text horizontally on warped X
    float posInWord = mod(warpedX, wordW);
    if (posInWord < 0.0) posInWord += wordW;

    float charStep = charW + gapW;
    float charSlotF = posInWord / charStep;
    int charSlot = int(floor(charSlotF));

    float cellLocalX = fract(charSlotF);
    float charFrac = charW / charStep;

    float textHit = 0.0;

    if (cellLocalX < charFrac && charSlot >= 0 && charSlot < numChars) {
        float glyphX = cellLocalX / charFrac;
        float glyphY = localY;

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

    // ── Alternating row colors ───────────────────────────────────────
    bool inverted = mod(rowIdx, 2.0) < 1.0;

    vec3 fg, bg;
    if (inverted) {
        fg = bgColor.rgb;
        bg = textColor.rgb;
    } else {
        fg = textColor.rgb;
        bg = bgColor.rgb;
    }

    // Apply 3D shading
    vec3 finalCol = mix(bg * shade, fg * shade, textHit);
    float alpha = 1.0;

    if (transparentBg) {
        alpha = textHit;
        finalCol = textColor.rgb * shade;
    }

    gl_FragColor = vec4(finalCol, alpha);
}

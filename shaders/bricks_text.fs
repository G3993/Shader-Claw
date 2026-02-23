/*{
  "CATEGORIES": ["Generator", "Text"],
  "DESCRIPTION": "Screen-filling grid of text with animated per-character wave displacement",
  "INPUTS": [
    { "NAME": "msg", "TYPE": "text", "DEFAULT": "ETHEREA", "MAX_LENGTH": 12 },
    { "NAME": "preset", "TYPE": "long", "VALUES": [0,1,2,3,4,5], "LABELS": ["Stacks","Bricks","Simple Z","Complex Z","Zebra","Harlequin"], "DEFAULT": 0 },
    { "NAME": "speed", "TYPE": "float", "MIN": 0.1, "MAX": 3.0, "DEFAULT": 0.5 },
    { "NAME": "columns", "TYPE": "float", "MIN": 5.0, "MAX": 40.0, "DEFAULT": 21.0 },
    { "NAME": "rows", "TYPE": "float", "MIN": 5.0, "MAX": 30.0, "DEFAULT": 12.0 },
    { "NAME": "waveAmount", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
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

    // Preset parameters
    float waveX = 0.0, waveY = 0.0;
    float freqX = 3.0, freqY = 3.0;
    bool brickOffset = false;
    float phaseMode = 0.0; // 0=col+row, 1=row only, 2=checkerboard
    float extraWaveX = 0.0, extraWaveY = 0.0;
    float extraFreqX = 5.0, extraFreqY = 4.0;

    // Preset 0: Stacks — clean grid, no displacement
    if (presetIdx == 0) {
        waveX = 0.0; waveY = 0.0;
        freqX = 3.0; freqY = 3.0;
        brickOffset = false;
        phaseMode = 0.0;
    }
    // Preset 1: Bricks — brick offset + gentle horizontal wave
    else if (presetIdx == 1) {
        waveX = 0.3; waveY = 0.0;
        freqX = 2.5; freqY = 3.0;
        brickOffset = true;
        phaseMode = 0.0;
    }
    // Preset 2: Simple Z — moderate X and Y wave with diagonal phase
    else if (presetIdx == 2) {
        waveX = 0.5; waveY = 0.5;
        freqX = 3.0; freqY = 3.0;
        brickOffset = false;
        phaseMode = 0.0;
    }
    // Preset 3: Complex Z — stronger overlapping waves with different frequencies
    else if (presetIdx == 3) {
        waveX = 0.8; waveY = 0.6;
        freqX = 3.0; freqY = 2.0;
        brickOffset = false;
        phaseMode = 0.0;
        extraWaveX = 0.4; extraWaveY = 0.3;
        extraFreqX = 5.0; extraFreqY = 4.0;
    }
    // Preset 4: Zebra — strong horizontal wave, phase based on row only
    else if (presetIdx == 4) {
        waveX = 1.0; waveY = 0.0;
        freqX = 4.0; freqY = 3.0;
        brickOffset = false;
        phaseMode = 1.0;
    }
    // Preset 5: Harlequin — diamond pattern, both axes, checkerboard phase
    else {
        waveX = 0.6; waveY = 0.6;
        freqX = 3.0; freqY = 3.0;
        brickOffset = false;
        phaseMode = 2.0;
    }

    // Grid dimensions
    float cols = floor(columns);
    float rws = floor(rows);

    // Cell size in UV space
    float cellW = 1.0 / cols;
    float cellH = 1.0 / rws;

    // Determine grid cell
    float colIdx = floor(uv.x / cellW);
    float rowIdx = floor(uv.y / cellH);
    colIdx = clamp(colIdx, 0.0, cols - 1.0);
    rowIdx = clamp(rowIdx, 0.0, rws - 1.0);

    // Local position within cell [0..1]
    float localX = fract(uv.x / cellW);
    float localY = fract(uv.y / cellH);

    // Brick offset: shift odd rows by half a cell width
    if (brickOffset && mod(rowIdx, 2.0) > 0.5) {
        float shiftedX = uv.x + cellW * 0.5;
        colIdx = floor(shiftedX / cellW);
        colIdx = mod(colIdx, cols);
        localX = fract(shiftedX / cellW);
    }

    // Wave displacement of local position within cell
    float t = TIME * speed * 2.5;

    // Phase calculation based on preset mode
    float phase = colIdx + rowIdx; // default: diagonal
    if (phaseMode > 0.5 && phaseMode < 1.5) {
        // Row only (Zebra)
        phase = rowIdx;
    } else if (phaseMode > 1.5) {
        // Checkerboard (Harlequin)
        phase = (colIdx + rowIdx) * 3.14159;
    }

    float dispX = sin(phase * freqX + t) * waveAmount * waveX * 0.3;
    float dispY = sin(phase * freqY + t * 1.1) * waveAmount * waveY * 0.3;

    // Add secondary wave for Complex Z preset
    if (extraWaveX > 0.0 || extraWaveY > 0.0) {
        float phase2 = colIdx * 0.7 - rowIdx * 1.3;
        dispX += sin(phase2 * extraFreqX + t * 1.3) * waveAmount * extraWaveX * 0.2;
        dispY += sin(phase2 * extraFreqY + t * 0.8) * waveAmount * extraWaveY * 0.2;
    }

    // Apply displacement (shift the local UV, wrapping)
    localX = fract(localX + dispX);
    localY = fract(localY + dispY);

    // Character mapping: which character from the text
    int charIdx = int(mod(colIdx + rowIdx * cols, float(numChars)));

    // Render character in cell
    // Character aspect ratio is 5:7
    float charW = 5.0 / 7.0;
    // Scale character within cell
    float scaleX = textScale * charW;
    float scaleY = textScale;
    float marginX = (1.0 - scaleX) * 0.5;
    float marginY = (1.0 - scaleY) * 0.5;

    float textHit = 0.0;

    if (localX >= marginX && localX < 1.0 - marginX && localY >= marginY && localY < 1.0 - marginY) {
        float glyphX = (localX - marginX) / scaleX;
        float glyphY = (localY - marginY) / scaleY;
        float gcol = glyphX * 5.0;
        float grow = glyphY * 7.0;

        if (gcol >= 0.0 && gcol < 5.0 && grow >= 0.0 && grow < 7.0) {
            // Unrolled if/else chain for charIdx 0-11
            int ch = -1;
            int ci = int(mod(float(charIdx), float(numChars)));
            if (ci == 0) ch = getChar(0);
            else if (ci == 1) ch = getChar(1);
            else if (ci == 2) ch = getChar(2);
            else if (ci == 3) ch = getChar(3);
            else if (ci == 4) ch = getChar(4);
            else if (ci == 5) ch = getChar(5);
            else if (ci == 6) ch = getChar(6);
            else if (ci == 7) ch = getChar(7);
            else if (ci == 8) ch = getChar(8);
            else if (ci == 9) ch = getChar(9);
            else if (ci == 10) ch = getChar(10);
            else if (ci == 11) ch = getChar(11);

            if (ch >= 0 && ch <= 25) {
                textHit = charPixel(ch, gcol, grow);
            }
        }
    }

    // Alternating row colors
    bool inverted = mod(rowIdx, 2.0) < 1.0;

    vec3 fg, bg;
    if (inverted) {
        fg = bgColor.rgb;
        bg = textColor.rgb;
    } else {
        fg = textColor.rgb;
        bg = bgColor.rgb;
    }

    vec3 finalCol = mix(bg, fg, textHit);
    float alpha = 1.0;

    if (transparentBg) {
        alpha = textHit;
        finalCol = textColor.rgb;
    }

    gl_FragColor = vec4(finalCol, alpha);
}

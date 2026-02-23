/*{
  "CATEGORIES": ["Generator", "Text"],
  "DESCRIPTION": "Rows of text at varying depth scales creating a perspective tunnel effect",
  "INPUTS": [
    { "NAME": "msg", "TYPE": "text", "DEFAULT": "ETHEREA", "MAX_LENGTH": 12 },
    { "NAME": "preset", "TYPE": "long", "VALUES": [0,1,2,3,4,5,6], "LABELS": ["Post Space","Bridge","Beach","Moon","X","Whitney","Recede"], "DEFAULT": 0 },
    { "NAME": "speed", "TYPE": "float", "MIN": 0.0, "MAX": 3.0, "DEFAULT": 0.3 },
    { "NAME": "rowCount", "TYPE": "float", "MIN": 3.0, "MAX": 20.0, "DEFAULT": 7.0 },
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
    float minScale = 0.3;
    float maxScale = 2.5;
    float tracking = 0.15;
    float scrollMult = 1.0;
    bool mirror = false;

    // Preset 0: Post Space — moderate scale range, clean depth
    if (presetIdx == 0) {
        minScale = 0.3;
        maxScale = 2.5;
        tracking = 0.15;
        scrollMult = 1.0;
        mirror = false;
    }
    // Preset 1: Bridge — tight spacing, wide scale range, faster scroll
    if (presetIdx == 1) {
        minScale = 0.2;
        maxScale = 3.0;
        tracking = 0.05;
        scrollMult = 1.4;
        mirror = false;
    }
    // Preset 2: Beach — gentle scale, generous spacing, slow scroll
    if (presetIdx == 2) {
        minScale = 0.5;
        maxScale = 1.8;
        tracking = 0.25;
        scrollMult = 0.6;
        mirror = false;
    }
    // Preset 3: Moon — large bold text, wide scale range
    if (presetIdx == 3) {
        minScale = 0.4;
        maxScale = 3.5;
        tracking = 0.15;
        scrollMult = 0.8;
        mirror = false;
    }
    // Preset 4: X — extreme scale contrast, dramatic depth
    if (presetIdx == 4) {
        minScale = 0.1;
        maxScale = 4.0;
        tracking = 0.1;
        scrollMult = 1.2;
        mirror = false;
    }
    // Preset 5: Whitney — smooth scale, generous spacing, elegant
    if (presetIdx == 5) {
        minScale = 0.4;
        maxScale = 2.0;
        tracking = 0.2;
        scrollMult = 0.9;
        mirror = true;
    }
    // Preset 6: Recede — very small center text, strong depth
    if (presetIdx == 6) {
        minScale = 0.15;
        maxScale = 2.0;
        tracking = 0.12;
        scrollMult = 1.0;
        mirror = false;
    }

    float rws = floor(rowCount);
    float rowH = 1.0 / rws;

    // Vertical scroll
    float scrollY = uv.y + TIME * speed * scrollMult;

    // Wrap into [0..1] repeating band
    float warpedY = mod(scrollY, 1.0);

    // Which row?
    float rowIdx = floor(warpedY / rowH);
    rowIdx = clamp(rowIdx, 0.0, rws - 1.0);

    // Local Y within this row [0..1]
    float localY = fract(warpedY / rowH);

    // Scale per row: distance from center of the row stack
    // rowNorm = 0 at bottom row, 1 at top row
    float rowNorm = (rowIdx + 0.5) / rws;
    float distFromCenter = abs(rowNorm - 0.5) * 2.0; // 0 at center, 1 at edges

    // Smooth the distance curve for a more natural perspective feel
    float scaleCurve = distFromCenter * distFromCenter; // quadratic falloff
    float rowScale = mix(minScale, maxScale, scaleCurve) * textScale;

    // Character dimensions at this row's scale
    // charH is the fraction of the row this text occupies
    float charH = rowH * rowScale;
    float charW = charH * (5.0 / 7.0) * (1.0 / aspect);
    float gapW = charW * tracking;

    // Total width of one copy of the message
    float wordW = float(numChars) * (charW + gapW);
    if (wordW < 0.001) wordW = 0.001; // prevent division by zero

    // Horizontal position — mirror bottom half if enabled
    float px = uv.x;
    if (mirror && rowNorm < 0.5) {
        px = 1.0 - px;
    }

    // Tile/repeat the text horizontally
    float posInWord = mod(px, wordW);
    if (posInWord < 0.0) posInWord += wordW;

    // Which character slot?
    float charStep = charW + gapW;
    float charSlotF = posInWord / charStep;
    int charSlot = int(floor(charSlotF));

    // Position within the character cell
    float cellLocalX = fract(charSlotF);
    float charFrac = charW / charStep;

    // Vertical: center the text in the row based on scale
    float textStartY = 0.5 - rowScale * 0.5;
    float glyphY = (localY - textStartY) / rowScale;

    float textHit = 0.0;

    if (cellLocalX < charFrac && charSlot >= 0 && charSlot < numChars && glyphY >= 0.0 && glyphY <= 1.0) {
        float glyphX = cellLocalX / charFrac;

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

    // Alternating color inversion per row
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

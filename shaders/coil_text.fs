/*{
  "CATEGORIES": ["Generator", "Text"],
  "DESCRIPTION": "Text arranged along a spiral/coil path with rotating colored ring bands",
  "INPUTS": [
    { "NAME": "msg", "TYPE": "text", "DEFAULT": "ETHEREA", "MAX_LENGTH": 12 },
    { "NAME": "preset", "TYPE": "long", "VALUES": [0,1,2,3,4,5,6,7], "LABELS": ["Wide","Tight","Star","Hourglass","Lemniscate","Spacer","Dense","Pulse"], "DEFAULT": 0 },
    { "NAME": "speed", "TYPE": "float", "MIN": 0.1, "MAX": 3.0, "DEFAULT": 0.3 },
    { "NAME": "rings", "TYPE": "float", "MIN": 3.0, "MAX": 15.0, "DEFAULT": 8.0 },
    { "NAME": "textColor", "TYPE": "color", "DEFAULT": [1.0, 1.0, 1.0, 1.0] },
    { "NAME": "bgColor", "TYPE": "color", "DEFAULT": [0.0, 0.0, 0.0, 1.0] },
    { "NAME": "textScale", "TYPE": "float", "MIN": 0.3, "MAX": 2.0, "DEFAULT": 1.0 },
    { "NAME": "transparentBg", "TYPE": "bool", "DEFAULT": false }
  ]
}*/

const float PI = 3.14159265;
const float TWO_PI = 6.28318530;

// ── Font engine: 5x7 bitmap font packed into two floats ──────────────

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

// ── Sample a character at a local UV ─────────────────────────────────

float sampleChar(int ch, vec2 localUV) {
    if (ch == 26) return 0.0;
    if (ch < 0 || ch > 25) return 0.0;
    float col = localUV.x * 5.0;
    float row = localUV.y * 7.0;
    if (col < 0.0 || col >= 5.0 || row < 0.0 || row >= 7.0) return 0.0;
    return charPixel(ch, col, row);
}

// ── Main ─────────────────────────────────────────────────────────────

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    float aspect = RENDERSIZE.x / RENDERSIZE.y;

    int numChars = charCount();
    int presetIdx = int(preset);

    // ── Preset parameters ────────────────────────────────────────────
    float innerRadius = 0.1;
    float ringGap = 0.06;
    float charSpacing = 1.0;   // multiplier on gap between characters along arc
    bool shapeModulate = false;
    int shapeType = 0;        // 0=none, 1=star, 2=hourglass, 3=lemniscate
    bool doPulse = false;

    if (presetIdx == 0) {
        // Wide: standard spiral, generous spacing
        innerRadius = 0.1;
        ringGap = 0.06;
    } else if (presetIdx == 1) {
        // Tight: tighter ring spacing
        innerRadius = 0.05;
        ringGap = 0.03;
    } else if (presetIdx == 2) {
        // Star: 5-pointed star modulation
        innerRadius = 0.08;
        ringGap = 0.05;
        shapeModulate = true;
        shapeType = 1;
    } else if (presetIdx == 3) {
        // Hourglass: abs(sin(angle)) modulation
        innerRadius = 0.08;
        ringGap = 0.05;
        shapeModulate = true;
        shapeType = 2;
    } else if (presetIdx == 4) {
        // Lemniscate: sqrt(abs(cos(2*angle))) modulation
        innerRadius = 0.08;
        ringGap = 0.05;
        shapeModulate = true;
        shapeType = 3;
    } else if (presetIdx == 5) {
        // Spacer: extra gap between characters
        innerRadius = 0.1;
        ringGap = 0.06;
        charSpacing = 2.0;
    } else if (presetIdx == 6) {
        // Dense: very tight, many rings, small text
        innerRadius = 0.02;
        ringGap = 0.02;
    } else if (presetIdx == 7) {
        // Pulse: ring radius pulsates with time
        innerRadius = 0.1;
        ringGap = 0.06;
        doPulse = true;
    }

    // Apply pulse
    float effectiveRingGap = ringGap;
    if (doPulse) {
        effectiveRingGap *= 1.0 + 0.3 * sin(TIME * 2.0);
    }

    // Scale ring gap by textScale (larger text = wider rings)
    effectiveRingGap *= textScale;
    innerRadius *= textScale;

    // ── Pixel to centered, aspect-corrected coordinates ──────────────
    vec2 center = vec2(0.5 * aspect, 0.5);
    vec2 p = vec2(uv.x * aspect, uv.y) - center;

    // ── Polar coordinates ────────────────────────────────────────────
    float radius = length(p);
    float angle = atan(p.y, p.x); // -PI to PI

    // Add time rotation
    angle -= TIME * speed;
    // Keep angle in -PI..PI range
    angle = mod(angle + PI, TWO_PI) - PI;

    // ── Shape modulation ─────────────────────────────────────────────
    // For shape presets, we modulate the effective radius so the rings
    // distort into non-circular shapes
    float effectiveRadius = radius;
    if (shapeModulate) {
        float shapeFactor = 1.0;
        if (shapeType == 1) {
            // Star: modulate by cos(5*angle)
            shapeFactor = 1.0 + 0.3 * cos(5.0 * angle);
        } else if (shapeType == 2) {
            // Hourglass: modulate by abs(sin(angle))
            shapeFactor = 0.5 + 0.5 * abs(sin(angle));
        } else if (shapeType == 3) {
            // Lemniscate: modulate by sqrt(abs(cos(2*angle)))
            shapeFactor = 0.3 + 0.7 * sqrt(abs(cos(2.0 * angle)));
        }
        // Divide radius by shape factor so that the "ring" boundary
        // follows the shape (pixels at the shape boundary all map to same ring)
        effectiveRadius = radius / shapeFactor;
    }

    // ── Determine ring index ─────────────────────────────────────────
    float ringFloat = (effectiveRadius - innerRadius) / effectiveRingGap;
    float ringIdx = floor(ringFloat);
    float ringFrac = ringFloat - ringIdx; // 0..1 position within ring band

    // Clamp to valid ring range
    float maxRings = rings;
    if (ringIdx < 0.0 || ringIdx >= maxRings) {
        // Outside all rings
        if (transparentBg) {
            gl_FragColor = vec4(0.0, 0.0, 0.0, 0.0);
        } else {
            gl_FragColor = bgColor;
        }
        return;
    }

    // ── Character layout along ring arc ──────────────────────────────
    // The ring's center radius (in effective-radius space)
    float ringCenterR = innerRadius + (ringIdx + 0.5) * effectiveRingGap;

    // Character cell size in world units
    float charH = effectiveRingGap * 0.75;  // height = fraction of ring width
    float charW = charH * (5.0 / 7.0);      // aspect ratio of 5x7 font
    float gapW = charW * 0.3 * charSpacing;  // gap between characters

    // Arc length per character cell
    float cellArc = charW + gapW;

    // Total arc length of the ring circumference at this radius
    // Use the actual ring center radius for correct spacing
    float actualRingR = ringCenterR;
    if (shapeModulate) {
        // For shape modulation, the actual circumference varies, but we
        // use the effective radius for consistent character placement
        actualRingR = ringCenterR;
    }
    float circumference = TWO_PI * actualRingR;

    // How many character cells fit around the ring
    float charsAround = circumference / cellArc;

    // Number of text repetitions around the ring (at least 1)
    float textLen = float(numChars);
    float reps = max(1.0, floor(charsAround / textLen));
    float totalCharsAround = reps * textLen;

    // Recalculate cell arc to perfectly tile
    float adjustedCellArc = circumference / totalCharsAround;
    float adjustedCharW = adjustedCellArc * (charW / cellArc);

    // ── Map angle to character position ──────────────────────────────
    // Normalize angle to 0..TWO_PI
    float normAngle = mod(angle + PI, TWO_PI);

    // Add per-ring offset so rings don't all align
    normAngle = mod(normAngle + ringIdx * 0.7, TWO_PI);

    // Arc position in character units
    float arcPos = (normAngle / TWO_PI) * totalCharsAround;
    float charIdx = floor(arcPos);
    float charFrac = arcPos - charIdx;

    // Which character in the text string
    int textIdx = int(mod(charIdx, textLen));

    // ── Transform pixel into character-local rotated space ───────────
    // The character's center angle on the ring
    float charCenterArc = (charIdx + 0.5) / totalCharsAround;
    float charAngle = charCenterArc * TWO_PI - PI;
    // Undo the per-ring offset
    charAngle = charAngle - ringIdx * 0.7;
    // Undo time rotation to get back to world-space angle
    charAngle = charAngle + TIME * speed;

    // Character center position in world space (before shape modulation)
    float ca = cos(charAngle);
    float sa = sin(charAngle);

    // For shape-modulated presets, compute the actual radius at this char angle
    float charActualR = ringCenterR;
    if (shapeModulate) {
        float sf = 1.0;
        // Use original angle (before time rotation) for shape
        // Actually use charAngle which includes time rotation undone
        float shapeAngle = charAngle;
        if (shapeType == 1) {
            sf = 1.0 + 0.3 * cos(5.0 * shapeAngle);
        } else if (shapeType == 2) {
            sf = 0.5 + 0.5 * abs(sin(shapeAngle));
        } else if (shapeType == 3) {
            sf = 0.3 + 0.7 * sqrt(abs(cos(2.0 * shapeAngle)));
        }
        charActualR = ringCenterR * sf;
    }

    vec2 charCenter = vec2(ca, sa) * charActualR;

    // Pixel offset from character center
    vec2 pixelOffset = p - charCenter;

    // Rotate into character-local space
    // Tangent direction = (-sin(charAngle), cos(charAngle)) = text X axis
    // Radial direction = (cos(charAngle), sin(charAngle)) = text Y axis
    vec2 localPos;
    localPos.x = dot(pixelOffset, vec2(-sa, ca));   // along tangent (text x)
    localPos.y = dot(pixelOffset, vec2(ca, sa));     // along radial (text y)

    // ── Map local position to character cell UV ──────────────────────
    // Character cell is adjustedCharW wide, charH tall
    vec2 cellUV;
    cellUV.x = (localPos.x / adjustedCharW) + 0.5;
    cellUV.y = 1.0 - ((localPos.y / charH) + 0.5);

    // ── Sample the character ─────────────────────────────────────────
    float textHit = 0.0;

    if (cellUV.x >= 0.0 && cellUV.x <= 1.0 && cellUV.y >= 0.0 && cellUV.y <= 1.0) {
        // Get the character for this text position
        int ch = -1;
        if (textIdx == 0) ch = getChar(0);
        else if (textIdx == 1) ch = getChar(1);
        else if (textIdx == 2) ch = getChar(2);
        else if (textIdx == 3) ch = getChar(3);
        else if (textIdx == 4) ch = getChar(4);
        else if (textIdx == 5) ch = getChar(5);
        else if (textIdx == 6) ch = getChar(6);
        else if (textIdx == 7) ch = getChar(7);
        else if (textIdx == 8) ch = getChar(8);
        else if (textIdx == 9) ch = getChar(9);
        else if (textIdx == 10) ch = getChar(10);
        else if (textIdx == 11) ch = getChar(11);

        if (ch >= 0 && ch <= 25) {
            textHit = sampleChar(ch, cellUV);
        }
    }

    // ── Ring band coloring (alternating) ─────────────────────────────
    bool inverted = mod(ringIdx, 2.0) < 1.0;

    vec3 fg, bg;
    if (inverted) {
        fg = textColor.rgb;
        bg = bgColor.rgb;
    } else {
        fg = bgColor.rgb;
        bg = textColor.rgb;
    }

    vec3 finalCol = mix(bg, fg, textHit);
    float alpha = 1.0;

    if (transparentBg) {
        // In transparent mode, only show text pixels
        alpha = textHit;
        finalCol = mix(vec3(0.0), textColor.rgb, textHit);
    }

    gl_FragColor = vec4(finalCol, alpha);
}

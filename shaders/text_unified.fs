/*{
  "CATEGORIES": ["Generator", "Text"],
  "DESCRIPTION": "Unified text effects — 21 presets across 9 effect families with 3 bitmap fonts + variable font",
  "INPUTS": [
    { "NAME": "msg", "TYPE": "text", "DEFAULT": " ETHEREA", "MAX_LENGTH": 24 },
    { "NAME": "effect", "TYPE": "long",
      "VALUES": [0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20],
      "LABELS": ["James","Wave","Cascade","Digifade","Digifade Glitch",
                 "Coil Wide","Coil Star","Coil Lemniscate","Coil Pulse",
                 "Flag Banner","Flag Origami","Flag Barber","Flag Newsprint",
                 "Bricks","Bricks Harlequin","Bricks Zebra",
                 "Spacy","Spacy Bridge","Spacy Whitney","Spacy Recede",
                 "Variable Font"],
      "DEFAULT": 0 },
    { "NAME": "font", "TYPE": "long", "VALUES": [0,1,2], "LABELS": ["Block","Slim","Round"], "DEFAULT": 0 },
    { "NAME": "fontFamily", "TYPE": "long", "VALUES": [0,1,2,3], "LABELS": ["Inter","Times New Roman","Libre Caslon","Outfit"], "DEFAULT": 0 },
    { "NAME": "speed", "TYPE": "float", "MIN": 0.1, "MAX": 3.0, "DEFAULT": 0.5 },
    { "NAME": "intensity", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "density", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "textScale", "TYPE": "float", "MIN": 0.3, "MAX": 2.0, "DEFAULT": 1.0 },
    { "NAME": "textColor", "TYPE": "color", "DEFAULT": [1.0, 1.0, 1.0, 1.0] },
    { "NAME": "bgColor", "TYPE": "color", "DEFAULT": [0.0, 0.0, 0.0, 1.0] },
    { "NAME": "transparentBg", "TYPE": "bool", "DEFAULT": false }
  ]
}*/

const float PI = 3.14159265;
const float TWO_PI = 6.28318530;

// ═══════════════════════════════════════════════════════════════════════
// CONSOLIDATED FONT TABLE — single 27-branch lookup, font selected once
// ═══════════════════════════════════════════════════════════════════════

vec2 charData(int ch) {
    // Each branch loads all 3 fonts, selects by uniform at end
    vec2 b, s, r;
    if      (ch == 0)  { b=vec2(1033777.,14897.);  s=vec2(338240.,4430.);   r=vec2(1033760.,4433.);  }
    else if (ch == 1)  { b=vec2(1001022.,31281.);  s=vec2(338304.,12620.);  r=vec2(574912.,14894.);  }
    else if (ch == 2)  { b=vec2(541230.,14896.);   s=vec2(270528.,6408.);   r=vec2(541120.,14864.);  }
    else if (ch == 3)  { b=vec2(575068.,29265.);   s=vec2(338304.,12618.);  r=vec2(575872.,12881.);  }
    else if (ch == 4)  { b=vec2(999967.,32272.);   s=vec2(270784.,14604.);  r=vec2(541664.,32286.);  }
    else if (ch == 5)  { b=vec2(999952.,32272.);   s=vec2(270592.,14604.);  r=vec2(541184.,32286.);  }
    else if (ch == 6)  { b=vec2(771630.,14896.);   s=vec2(338112.,6408.);   r=vec2(640448.,14864.);  }
    else if (ch == 7)  { b=vec2(1033777.,17969.);  s=vec2(338240.,10574.);  r=vec2(575008.,17983.);  }
    else if (ch == 8)  { b=vec2(135310.,14468.);   s=vec2(135616.,14468.);  r=vec2(135616.,14468.);  }
    else if (ch == 9)  { b=vec2(68172.,7234.);     s=vec2(75904.,6210.);    r=vec2(84352.,7234.);    }
    else if (ch == 10) { b=vec2(807505.,18004.);   s=vec2(338240.,10572.);  r=vec2(674336.,19096.);  }
    else if (ch == 11) { b=vec2(541215.,16912.);   s=vec2(270784.,8456.);   r=vec2(541664.,16912.);  }
    else if (ch == 12) { b=vec2(706097.,18293.);   s=vec2(338240.,10702.);  r=vec2(575008.,18293.);  }
    else if (ch == 13) { b=vec2(640561.,18229.);   s=vec2(338240.,10698.);  r=vec2(640544.,18229.);  }
    else if (ch == 14) { b=vec2(575022.,14897.);   s=vec2(338048.,4426.);   r=vec2(574912.,14897.);  }
    else if (ch == 15) { b=vec2(999952.,31281.);   s=vec2(270592.,12620.);  r=vec2(541184.,14894.);  }
    else if (ch == 16) { b=vec2(579149.,14897.);   s=vec2(342208.,4426.);   r=vec2(706976.,14897.);  }
    else if (ch == 17) { b=vec2(1004113.,31281.);  s=vec2(338240.,12620.);  r=vec2(674336.,14894.);  }
    else if (ch == 18) { b=vec2(460334.,14896.);   s=vec2(67968.,6404.);    r=vec2(34240.,14862.);   }
    else if (ch == 19) { b=vec2(135300.,31876.);   s=vec2(135296.,14468.);  r=vec2(135296.,31876.);  }
    else if (ch == 20) { b=vec2(575022.,17969.);   s=vec2(338048.,10570.);  r=vec2(574912.,17969.);  }
    else if (ch == 21) { b=vec2(567620.,17969.);   s=vec2(331904.,10570.);  r=vec2(338048.,17969.);  }
    else if (ch == 22) { b=vec2(710513.,17969.);   s=vec2(473408.,10570.);  r=vec2(716320.,17973.);  }
    else if (ch == 23) { b=vec2(141873.,17962.);   s=vec2(141632.,10564.);  r=vec2(141856.,17732.);  }
    else if (ch == 24) { b=vec2(135300.,17962.);   s=vec2(135296.,10564.);  r=vec2(135296.,17732.);  }
    else if (ch == 25) { b=vec2(139807.,31778.);   s=vec2(139712.,14404.);  r=vec2(279520.,31782.);  }
    else               { return vec2(0.0, 0.0); }

    if (font > 1.5) return r;
    if (font > 0.5) return s;
    return b;
}

// ═══════════════════════════════════════════════════════════════════════
// FONT ENGINE — uses pre-fetched data to avoid redundant lookups
// ═══════════════════════════════════════════════════════════════════════

// Extract pixel from pre-fetched vec2 data (no charData call)
float pxFromData(vec2 data, float col, float row) {
    float ri = floor(row);
    float rv;
    if (ri < 4.0) rv = mod(floor(data.x / pow(32.0, ri)), 32.0);
    else rv = mod(floor(data.y / pow(32.0, ri - 4.0)), 32.0);
    return mod(floor(rv / pow(2.0, 4.0 - floor(col))), 2.0);
}

// Standard charPixel (calls charData once)
float charPixel(int ch, float col, float row) {
    return pxFromData(charData(ch), col, row);
}

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
    return int(msg_23);
}

int charCount() {
    int n = int(msg_len);
    return n > 0 ? n : 1;
}

float sampleChar(int ch, vec2 uv) {
    if (ch < 0 || ch > 25) return 0.0;
    float c = uv.x * 5.0, r = uv.y * 7.0;
    if (c < 0.0 || c >= 5.0 || r < 0.0 || r >= 7.0) return 0.0;
    return charPixel(ch, c, r);
}

float hash(float n) { return fract(sin(n * 127.1) * 43758.5453); }

// ═══════════════════════════════════════════════════════════════════════
// EFFECT 0: JAMES — cycling font styles per letter
// ═══════════════════════════════════════════════════════════════════════

vec4 effectJames(vec2 uv) {
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    int numChars = charCount();
    float bounce = intensity;
    float cycleSpeed = mix(0.2, 5.0, density);

    vec3 col = bgColor.rgb;
    float alpha = transparentBg ? 0.0 : 1.0;
    col += vec3(0.02, 0.01, 0.03) * (uv.y * 0.3 + 0.05 * sin(uv.x * 3.0 + TIME * 0.5));

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

    for (int i = 0; i < 24; i++) {
        if (i >= numChars) break;
        int ch = getChar(i);
        if (ch == 26) continue;

        float phase = float(i) * 1.3 + TIME * speed * cycleSpeed;
        int style = int(mod(floor(phase), 18.0));
        float bp = float(i) * 0.8 + TIME * speed * 2.5;
        float yOff = sin(bp) * 0.015 * bounce;
        float sp = 1.0 + sin(bp + 1.0) * 0.05 * bounce;
        float cx = startX + float(i) * (charW + gap);
        float cy = baseY + yOff;
        vec2 cellUV = vec2((p.x - cx) / (charW * sp), (p.y - cy) / (charH * sp));

        if (cellUV.x < -0.1 || cellUV.x > 1.1 || cellUV.y < -0.1 || cellUV.y > 1.1) continue;

        if (cellUV.x >= 0.0 && cellUV.x <= 1.0 && cellUV.y >= 0.0 && cellUV.y <= 1.0) {
            vec2 grid = cellUV * vec2(5.0, 7.0);
            float gcol = floor(grid.x);
            float grow = floor(grid.y);
            if (gcol >= 0.0 && gcol < 5.0 && grow >= 0.0 && grow < 7.0) {
                // Fetch font data ONCE for this character
                vec2 data = charData(ch);
                float filled = pxFromData(data, gcol, grow);
                if (filled > 0.5) {
                    vec2 lp = fract(grid);
                    float inten = 1.0;
                    // 0: solid block
                    if (style == 0) inten = 1.0;
                    // 1: circle dots
                    else if (style == 1) inten = smoothstep(0.45, 0.35, length(lp - 0.5));
                    // 2: outline
                    else if (style == 2) {
                        float nb = pxFromData(data, gcol-1.0, grow)
                                 + pxFromData(data, gcol+1.0, grow)
                                 + pxFromData(data, gcol, grow-1.0)
                                 + pxFromData(data, gcol, grow+1.0);
                        inten = nb > 3.5 ? 0.0 : 1.0;
                    }
                    // 3: horizontal stripes
                    else if (style == 3) inten = step(0.35, fract(lp.y * 3.0));
                    // 4: diamond
                    else if (style == 4) { vec2 c = abs(lp - 0.5); inten = smoothstep(0.5, 0.4, c.x + c.y); }
                    // 5: cross/plus
                    else if (style == 5) inten = max(smoothstep(0.42, 0.38, abs(lp.x-0.5)), smoothstep(0.42, 0.38, abs(lp.y-0.5)));
                    // 6: vertical bars
                    else if (style == 6) inten = smoothstep(0.42, 0.35, abs(lp.x - 0.5));
                    // 7: soft glow blob
                    else if (style == 7) { float d = length(lp - 0.5); inten = exp(-d*d*8.0) * 1.5; }
                    // 8: diagonal hatching
                    else if (style == 8) inten = step(0.4, fract((lp.x + lp.y) * 2.5));
                    // 9: concentric rings
                    else if (style == 9) inten = step(0.3, fract(length(lp - 0.5) * 4.0));
                    // 10: square inset
                    else if (style == 10) { vec2 c = abs(lp - 0.5); inten = smoothstep(0.45, 0.35, max(c.x, c.y)); }
                    // 11: star / 4-point
                    else if (style == 11) { vec2 c = abs(lp - 0.5); inten = smoothstep(0.35, 0.25, c.x * c.y * 8.0); }
                    // 12: scanlines (thin horizontal)
                    else if (style == 12) inten = step(0.5, fract(lp.y * 5.0));
                    // 13: checker
                    else if (style == 13) inten = mod(floor(lp.x * 2.0) + floor(lp.y * 2.0), 2.0);
                    // 14: vertical gradient fade
                    else if (style == 14) inten = lp.y;
                    // 15: radial burst
                    else if (style == 15) { float a = atan(lp.y - 0.5, lp.x - 0.5); inten = step(0.3, fract(a / PI * 3.0)); }
                    // 16: corner dots (4 small circles)
                    else if (style == 16) { float d = min(min(length(lp), length(lp - vec2(1.0, 0.0))), min(length(lp - vec2(0.0, 1.0)), length(lp - 1.0))); inten = smoothstep(0.35, 0.25, d); }
                    // 17: X slash
                    else { float d = min(abs(lp.x - lp.y), abs(lp.x - (1.0 - lp.y))); inten = smoothstep(0.2, 0.1, d); }

                    textCol = max(textCol, textColor.rgb * inten);
                    textMask = max(textMask, inten);
                }
            }
        }

        vec2 cc = vec2(cx + charW*0.5, cy + charH*0.5);
        float gd = length((p - cc) * vec2(1.0, 0.7));
        glowAccum += exp(-gd*gd/(charW*charW*2.0)) * 0.15 * (0.8 + 0.2*sin(phase*2.0));
    }

    col = mix(col, textCol, clamp(textMask, 0.0, 1.0));
    if (!transparentBg) col += textColor.rgb * glowAccum;
    col *= 1.0 - 0.3 * length((uv - 0.5) * 1.5);
    if (transparentBg) alpha = clamp(textMask, 0.0, 1.0);
    return vec4(col, alpha);
}

// ═══════════════════════════════════════════════════════════════════════
// EFFECT 1: WAVE — sine displacement per letter
// ═══════════════════════════════════════════════════════════════════════

vec4 effectWave(vec2 uv) {
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    int numChars = charCount();
    float amplitude = mix(0.0, 0.15, intensity);
    float frequency = mix(0.5, 5.0, density);

    vec2 p = vec2((uv.x - 0.5) * aspect + 0.5, uv.y);
    float cW = 0.09 * textScale, cH = cW * 1.5;
    float gW = cW * 0.25, cellStep = cW + gW;
    float totalW = float(numChars) * cellStep - gW;
    float startX = 0.5 - totalW * 0.5;

    float mainHit = 0.0, shadowHit = 0.0;
    vec2 so = vec2(0.005, -0.005);

    for (int i = 0; i < 24; i++) {
        if (i >= numChars) break;
        int ch = getChar(i);
        float phase = float(i) * frequency + TIME * speed;
        float yOff = sin(phase) * amplitude;
        float tilt = cos(phase) * amplitude * 3.0;
        float cellX = startX + float(i) * cellStep;
        float cellY = 0.5 - cH * 0.5;

        vec2 m = vec2((p.x - cellX) / cW, (p.y - (cellY + yOff)) / cH);
        m.x += (m.y - 0.5) * tilt;
        if (m.x >= 0.0 && m.x <= 1.0 && m.y >= 0.0 && m.y <= 1.0)
            mainHit = max(mainHit, sampleChar(ch, m));

        vec2 s = vec2((p.x - so.x - cellX) / cW, (p.y - so.y - (cellY + yOff)) / cH);
        s.x += (s.y - 0.5) * tilt;
        if (s.x >= 0.0 && s.x <= 1.0 && s.y >= 0.0 && s.y <= 1.0)
            shadowHit = max(shadowHit, sampleChar(ch, s));
    }

    vec4 result = transparentBg ? vec4(0.0) : bgColor;
    if (shadowHit > 0.5)
        result = vec4(mix(result.rgb, vec3(0.0), 0.3), result.a + 0.3*(1.0-result.a));
    if (mainHit > 0.5) result = vec4(textColor.rgb, textColor.a);
    return result;
}

// ═══════════════════════════════════════════════════════════════════════
// EFFECT 2: CASCADE — tiled rows with wave offsets
// ═══════════════════════════════════════════════════════════════════════

vec4 effectCascade(vec2 uv) {
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    int numChars = charCount();
    float waveAmount = intensity;
    float rows = floor(mix(5.0, 30.0, density));

    float warpedY = uv.y + sin(uv.y * TWO_PI * 1.5 + TIME * speed * 1.5) * waveAmount * 0.06;
    float rowH = 1.0 / rows;
    float rowIdx = clamp(floor(warpedY / rowH), 0.0, rows - 1.0);
    float localY = fract(warpedY / rowH);

    float cH = rowH;
    float cW = cH * (5.0/7.0) * (1.0/aspect) * textScale;
    float gW = cW * 0.15;
    float wordW = float(numChars) * (cW + gW);

    float xOff = sin(rowIdx*0.6 + TIME*speed*2.0) * waveAmount * wordW * 1.5 + TIME*speed*0.08;
    float px = mod(uv.x + xOff, wordW);
    if (px < 0.0) px += wordW;

    float cs = cW + gW;
    float csF = px / cs;
    int slot = int(floor(csF));
    float clx = fract(csF);
    float cf = cW / cs;

    float textHit = 0.0;
    if (clx < cf && slot >= 0 && slot < numChars) {
        float gc = (clx/cf) * 5.0, gr = localY * 7.0;
        if (gc >= 0.0 && gc < 5.0 && gr >= 0.0 && gr < 7.0) {
            int ch = getChar(slot);
            if (ch >= 0 && ch <= 25) textHit = charPixel(ch, gc, gr);
        }
    }

    bool inv = mod(rowIdx, 2.0) < 1.0;
    vec3 fg = inv ? bgColor.rgb : textColor.rgb;
    vec3 bg = inv ? textColor.rgb : bgColor.rgb;
    vec3 fc = mix(bg, fg, textHit);
    float a = 1.0;
    if (transparentBg) { a = textHit; fc = textColor.rgb; }
    return vec4(fc, a);
}

// ═══════════════════════════════════════════════════════════════════════
// EFFECT 3-4: DIGIFADE — glitch dissolve
// ═══════════════════════════════════════════════════════════════════════

vec4 effectDigifade(vec2 uv, int sub) {
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    int numChars = charCount();
    float glitchAmount = intensity;
    float sliceCount = mix(5.0, 100.0, density);

    float complexity = 1.0, sweepSpeed = 1.0, vertGlitch = 0.0, maxDisp = 0.3;
    if (sub == 1) { complexity = 2.0; sweepSpeed = 1.3; maxDisp = 0.5; vertGlitch = 0.4; }

    float t = TIME * speed * sweepSpeed;
    vec2 p = vec2((uv.x - 0.5) * aspect + 0.5, uv.y);
    float cH = 0.18 * textScale, cW = cH * (5.0/7.0), gW = cW * 0.2;
    float totalW = float(numChars) * cW + float(numChars - 1) * gW;
    float startX = 0.5 - totalW*0.5, startY = 0.5 - cH*0.5;

    float si = floor(uv.y * sliceCount);
    float n1 = hash(si + floor(t*2.0));
    float n2 = hash(si*3.7 + floor(t*3.0));
    float sw = sin(t*0.7)*0.5+0.5;
    float ps = smoothstep(sw-0.15, sw+0.1, (p.x-startX)/totalW);

    float dx = abs(ps*n1*glitchAmount*maxDisp + ps*sin(si*0.3*complexity+t)*glitchAmount*maxDisp*0.3);
    float dy = vertGlitch > 0.01 ? ps*(n2-0.5)*vertGlitch*glitchAmount*0.06 : 0.0;

    vec2 samp = vec2(p.x - dx, p.y - dy);
    float rx = samp.x - startX, ry = samp.y - startY;
    float textHit = 0.0;

    if (rx >= 0.0 && rx <= totalW && ry >= 0.0 && ry <= cH) {
        float cs = cW + gW;
        float csF = rx / cs;
        int slot = int(floor(csF));
        float clx = fract(csF), cf = cW/cs;
        if (clx < cf && slot >= 0 && slot < numChars) {
            float gc = (clx/cf)*5.0, gr = (ry/cH)*7.0;
            if (gc >= 0.0 && gc < 5.0 && gr >= 0.0 && gr < 7.0) {
                int ch = getChar(slot);
                if (ch >= 0 && ch <= 25) textHit = charPixel(ch, gc, gr);
            }
        }
    }

    vec3 fc = mix(bgColor.rgb, textColor.rgb, textHit);
    float a = 1.0;
    if (transparentBg) { a = textHit; fc = textColor.rgb; }
    return vec4(fc, a);
}

// ═══════════════════════════════════════════════════════════════════════
// EFFECT 5-8: COIL — text on spiral rings
// ═══════════════════════════════════════════════════════════════════════

vec4 effectCoil(vec2 uv, int sub) {
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    int numChars = charCount();
    float rings = mix(3.0, 15.0, density);
    float charSpacing = mix(0.5, 3.0, intensity);

    float innerR = 0.1, ringGap = 0.06;
    int shapeType = 0;
    bool doPulse = false;

    if (sub == 1) { innerR = 0.08; ringGap = 0.05; shapeType = 1; }
    else if (sub == 2) { innerR = 0.08; ringGap = 0.05; shapeType = 3; }
    else if (sub == 3) { doPulse = true; }

    float eRG = ringGap;
    if (doPulse) eRG *= 1.0 + 0.3*sin(TIME*speed*2.0);
    eRG *= textScale;
    innerR *= textScale;

    vec2 center = vec2(0.5*aspect, 0.5);
    vec2 p = vec2(uv.x*aspect, uv.y) - center;
    float radius = length(p);
    float angle = atan(p.y, p.x) - TIME*speed;
    angle = mod(angle + PI, TWO_PI) - PI;

    float eR = radius;
    if (shapeType == 1) eR = radius / (1.0 + 0.3*cos(5.0*angle));
    else if (shapeType == 3) eR = radius / (0.3 + 0.7*sqrt(abs(cos(2.0*angle))));

    float ringIdx = floor((eR - innerR) / eRG);
    if (ringIdx < 0.0 || ringIdx >= rings) {
        return transparentBg ? vec4(0.0) : bgColor;
    }

    float rcR = innerR + (ringIdx + 0.5)*eRG;
    float cH = eRG*0.75, cW = cH*(5.0/7.0);
    float gW = cW*0.3*charSpacing;
    float cellArc = cW + gW;
    float circ = TWO_PI * rcR;
    float tLen = float(numChars);
    float reps = max(1.0, floor(circ/cellArc/tLen));
    float tca = reps * tLen;
    float aca = circ / tca;
    float acW = aca * (cW/cellArc);

    float na = mod(angle + PI + ringIdx*0.7, TWO_PI);
    float ap = (na/TWO_PI)*tca;
    float ci = floor(ap);
    int ti = int(mod(ci, tLen));

    float ca2 = ((ci+0.5)/tca)*TWO_PI - PI - ringIdx*0.7 + TIME*speed;
    float ca = cos(ca2), sa = sin(ca2);

    float car = rcR;
    if (shapeType == 1) car = rcR*(1.0+0.3*cos(5.0*ca2));
    else if (shapeType == 3) car = rcR*(0.3+0.7*sqrt(abs(cos(2.0*ca2))));

    vec2 cc = vec2(ca, sa)*car;
    vec2 po = p - cc;
    vec2 lp = vec2(dot(po, vec2(-sa, ca)), dot(po, vec2(ca, sa)));
    vec2 cellUV = vec2(lp.x/acW + 0.5, 1.0 - (lp.y/cH + 0.5));

    float textHit = 0.0;
    if (cellUV.x >= 0.0 && cellUV.x <= 1.0 && cellUV.y >= 0.0 && cellUV.y <= 1.0) {
        int ch = getChar(ti);
        if (ch >= 0 && ch <= 25) textHit = sampleChar(ch, cellUV);
    }

    bool inv = mod(ringIdx, 2.0) < 1.0;
    vec3 fg = inv ? textColor.rgb : bgColor.rgb;
    vec3 bg = inv ? bgColor.rgb : textColor.rgb;
    vec3 fc = mix(bg, fg, textHit);
    float a = 1.0;
    if (transparentBg) { a = textHit; fc = textColor.rgb; }
    return vec4(fc, a);
}

// ═══════════════════════════════════════════════════════════════════════
// EFFECT 9-12: FLAG — waving flag surface
// ═══════════════════════════════════════════════════════════════════════

vec4 effectFlag(vec2 uv, int sub) {
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    int numChars = charCount();
    float waveSize = intensity;
    float rows = floor(mix(4.0, 20.0, density));

    float wA=1.0, wF=3.0, xSA=1.0, anch=0.5, sM=1.0, rM=1.0;
    bool sharp=false, barber=false;

    if (sub == 0) { wA=1.0; wF=3.0; xSA=0.8; anch=0.7; }
    else if (sub == 1) { sharp=true; wA=1.2; wF=4.0; xSA=0.5; anch=0.3; }
    else if (sub == 2) { wA=1.0; wF=4.0; xSA=1.2; anch=0.0; barber=true; }
    else { wA=0.2; wF=2.0; xSA=0.2; anch=0.0; rM=2.0; }

    rows = floor(rows * rM);
    float env = clamp(mix(1.0, uv.x, anch), 0.0, 1.0);
    float t = TIME*speed*2.5;
    float wp = uv.x*wF*TWO_PI - t;

    float yW = sharp ? abs(sin(wp))*2.0-1.0 : sin(wp);
    if (barber) yW += sin(uv.y*4.0*TWO_PI + t*0.7)*0.3;

    float yD = yW*waveSize*wA*0.15*env;
    float wY = uv.y + yD;
    float dW = cos(wp)*wF*TWO_PI;
    float wX = uv.x + dW*waveSize*xSA*0.02*env;

    float rH = 1.0/rows;
    float ri = clamp(floor(wY/rH), 0.0, rows-1.0);
    float ly = fract(wY/rH);
    float shade = clamp((0.5+0.5*(dW*waveSize*wA*0.15*env/(abs(dW*waveSize*wA*0.15*env)+0.3)))*sM, 0.08, 1.0);

    float cH = rH, cW = cH*(5.0/7.0)*(1.0/aspect)*textScale;
    float gW = cW*0.15;
    float wordW = float(numChars)*(cW+gW);

    float piw = mod(wX, wordW);
    if (piw < 0.0) piw += wordW;
    float cs = cW+gW, csF = piw/cs;
    int slot = int(floor(csF));
    float clx = fract(csF), cf = cW/cs;

    float textHit = 0.0;
    if (clx < cf && slot >= 0 && slot < numChars) {
        float gc = (clx/cf)*5.0, gr = ly*7.0;
        if (gc >= 0.0 && gc < 5.0 && gr >= 0.0 && gr < 7.0) {
            int ch = getChar(slot);
            if (ch >= 0 && ch <= 25) textHit = charPixel(ch, gc, gr);
        }
    }

    bool inv = mod(ri, 2.0) < 1.0;
    vec3 fg = inv ? bgColor.rgb : textColor.rgb;
    vec3 bg = inv ? textColor.rgb : bgColor.rgb;
    vec3 fc = mix(bg*shade, fg*shade, textHit);
    float a = 1.0;
    if (transparentBg) { a = textHit; fc = textColor.rgb*shade; }
    return vec4(fc, a);
}

// ═══════════════════════════════════════════════════════════════════════
// EFFECT 13-15: BRICKS — grid with animated displacement
// ═══════════════════════════════════════════════════════════════════════

vec4 effectBricks(vec2 uv, int sub) {
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    int numChars = charCount();
    float waveAmount = intensity;
    float cols = floor(mix(5.0, 40.0, density));

    float wX=0.0, wY=0.0, fX=3.0, fY=3.0, pm=0.0;
    bool brick = false;
    if (sub == 0) { wX=0.3; fX=2.5; brick=true; }
    else if (sub == 1) { wX=0.6; wY=0.6; pm=2.0; }
    else { wX=1.0; fX=4.0; pm=1.0; }

    float rws = floor(cols*(7.0/5.0)/aspect);
    float cellW = 1.0/cols, cellH = 1.0/rws;

    float ci = clamp(floor(uv.x/cellW), 0.0, cols-1.0);
    float ri = clamp(floor(uv.y/cellH), 0.0, rws-1.0);
    float lx = fract(uv.x/cellW), ly = fract(uv.y/cellH);

    if (brick && mod(ri, 2.0) > 0.5) {
        float sx = uv.x + cellW*0.5;
        ci = mod(floor(sx/cellW), cols);
        lx = fract(sx/cellW);
    }

    float t = TIME*speed*2.5;
    float phase = ci + ri;
    if (pm > 0.5 && pm < 1.5) phase = ri;
    else if (pm > 1.5) phase = (ci + ri)*PI;

    lx = fract(lx + sin(phase*fX+t)*waveAmount*wX*0.3);
    ly = fract(ly + sin(phase*fY+t*1.1)*waveAmount*wY*0.3);

    int charIdx = int(mod(ci + ri*cols, float(numChars)));
    float cWR = 5.0/7.0;
    float sX = textScale*cWR, sY = textScale;
    float mX = (1.0-sX)*0.5, mY = (1.0-sY)*0.5;

    float textHit = 0.0;
    if (lx >= mX && lx < 1.0-mX && ly >= mY && ly < 1.0-mY) {
        float gc = ((lx-mX)/sX)*5.0, gr = ((ly-mY)/sY)*7.0;
        if (gc >= 0.0 && gc < 5.0 && gr >= 0.0 && gr < 7.0) {
            int ci2 = int(mod(float(charIdx), float(numChars)));
            int ch = getChar(ci2);
            if (ch >= 0 && ch <= 25) textHit = charPixel(ch, gc, gr);
        }
    }

    bool inv = mod(ri, 2.0) < 1.0;
    vec3 fg = inv ? bgColor.rgb : textColor.rgb;
    vec3 bg = inv ? textColor.rgb : bgColor.rgb;
    vec3 fc = mix(bg, fg, textHit);
    float a = 1.0;
    if (transparentBg) { a = textHit; fc = textColor.rgb; }
    return vec4(fc, a);
}

// ═══════════════════════════════════════════════════════════════════════
// EFFECT 16-19: SPACY — perspective tunnel rows
// ═══════════════════════════════════════════════════════════════════════

vec4 effectSpacy(vec2 uv, int sub) {
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    int numChars = charCount();
    float rws = floor(mix(3.0, 20.0, density));
    float sR = mix(0.5, 1.5, intensity);

    float minS=0.3, maxS=2.5, track=0.15, scM=1.0;
    bool mirror = false;

    if (sub == 0) { minS=0.3/sR; maxS=2.5*sR; }
    else if (sub == 1) { minS=0.2/sR; maxS=3.0*sR; track=0.05; scM=1.4; }
    else if (sub == 2) { minS=0.4/sR; maxS=2.0*sR; track=0.2; scM=0.9; mirror=true; }
    else { minS=0.15/sR; maxS=2.0*sR; track=0.12; }

    float rH = 1.0/rws;
    float sY = mod(uv.y + TIME*speed*scM, 1.0);
    float ri = clamp(floor(sY/rH), 0.0, rws-1.0);
    float ly = fract(sY/rH);

    float rn = (ri+0.5)/rws;
    float dc = abs(rn-0.5)*2.0;
    float rs = mix(minS, maxS, dc*dc)*textScale;

    float cH = rH*rs;
    float cW = cH*(5.0/7.0)*(1.0/aspect);
    float gW = cW*track;
    float wordW = max(float(numChars)*(cW+gW), 0.001);

    float px = uv.x;
    if (mirror && rn < 0.5) px = 1.0 - px;

    float piw = mod(px, wordW);
    if (piw < 0.0) piw += wordW;
    float cs = cW+gW, csF = piw/cs;
    int slot = int(floor(csF));
    float clx = fract(csF), cf = cW/cs;
    float tsy = 0.5-rs*0.5;
    float gy = (ly-tsy)/rs;

    float textHit = 0.0;
    if (clx < cf && slot >= 0 && slot < numChars && gy >= 0.0 && gy <= 1.0) {
        float gc = (clx/cf)*5.0, gr = gy*7.0;
        if (gc >= 0.0 && gc < 5.0 && gr >= 0.0 && gr < 7.0) {
            int ch = getChar(slot);
            if (ch >= 0 && ch <= 25) textHit = charPixel(ch, gc, gr);
        }
    }

    bool inv = mod(ri, 2.0) < 1.0;
    vec3 fg = inv ? bgColor.rgb : textColor.rgb;
    vec3 bg = inv ? textColor.rgb : bgColor.rgb;
    vec3 fc = mix(bg, fg, textHit);
    float a = 1.0;
    if (transparentBg) { a = textHit; fc = textColor.rgb; }
    return vec4(fc, a);
}

// ═══════════════════════════════════════════════════════════════════════
// EFFECT 20: VARIABLE FONT — canvas-rendered text via texture
// ═══════════════════════════════════════════════════════════════════════

vec4 effectVarFont(vec2 uv) {
    vec4 tex = texture2D(varFontTex, uv);
    float lum = dot(tex.rgb, vec3(0.299, 0.587, 0.114));
    vec3 col = transparentBg ? textColor.rgb : mix(bgColor.rgb, textColor.rgb, lum);
    float alpha = transparentBg ? lum : 1.0;
    return vec4(col, alpha);
}

// ═══════════════════════════════════════════════════════════════════════
// MAIN DISPATCHER
// ═══════════════════════════════════════════════════════════════════════

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    int e = int(effect);

    if (e == 0)  { gl_FragColor = effectJames(uv); return; }
    if (e == 1)  { gl_FragColor = effectWave(uv); return; }
    if (e == 2)  { gl_FragColor = effectCascade(uv); return; }
    if (e == 3)  { gl_FragColor = effectDigifade(uv, 0); return; }
    if (e == 4)  { gl_FragColor = effectDigifade(uv, 1); return; }
    if (e == 5)  { gl_FragColor = effectCoil(uv, 0); return; }
    if (e == 6)  { gl_FragColor = effectCoil(uv, 1); return; }
    if (e == 7)  { gl_FragColor = effectCoil(uv, 2); return; }
    if (e == 8)  { gl_FragColor = effectCoil(uv, 3); return; }
    if (e == 9)  { gl_FragColor = effectFlag(uv, 0); return; }
    if (e == 10) { gl_FragColor = effectFlag(uv, 1); return; }
    if (e == 11) { gl_FragColor = effectFlag(uv, 2); return; }
    if (e == 12) { gl_FragColor = effectFlag(uv, 3); return; }
    if (e == 13) { gl_FragColor = effectBricks(uv, 0); return; }
    if (e == 14) { gl_FragColor = effectBricks(uv, 1); return; }
    if (e == 15) { gl_FragColor = effectBricks(uv, 2); return; }
    if (e == 16) { gl_FragColor = effectSpacy(uv, 0); return; }
    if (e == 17) { gl_FragColor = effectSpacy(uv, 1); return; }
    if (e == 18) { gl_FragColor = effectSpacy(uv, 2); return; }
    if (e == 19) { gl_FragColor = effectSpacy(uv, 3); return; }
    if (e == 20) { gl_FragColor = effectVarFont(uv); return; }
    gl_FragColor = effectSpacy(uv, 3);
}

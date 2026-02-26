/*{
  "CATEGORIES": ["Generator", "Text", "3D"],
  "DESCRIPTION": "3D extruded text — rotating block letters with Phong lighting and cycling fill patterns",
  "INPUTS": [
    { "NAME": "msg", "TYPE": "text", "DEFAULT": " ETHEREA", "MAX_LENGTH": 24 },
    { "NAME": "fontFamily", "TYPE": "long", "VALUES": [0,1,2,3], "LABELS": ["Inter","Times New Roman","Libre Caslon","Outfit"], "DEFAULT": 0 },
    { "NAME": "effect", "TYPE": "long", "VALUES": [0, 1], "LABELS": ["3D Text", "James 3D"], "DEFAULT": 0 },
    { "NAME": "font", "TYPE": "long", "VALUES": [0,1,2], "LABELS": ["Block","Slim","Round"], "DEFAULT": 0 },
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

// Atlas-only character sampling — no charData branches for fast ANGLE compile
float sampleAtlas(int ch, float col, float row) {
    if (ch < 0 || ch > 25) return 0.0;
    float u = (float(ch) + col / 5.0) / 27.0;
    float v = row / 7.0;
    return smoothstep(0.1, 0.55, texture2D(fontAtlasTex, vec2(u, v)).r);
}

int getChar(int slot) {
    if (slot == 0)  return int(msg_0);  if (slot == 1)  return int(msg_1);
    if (slot == 2)  return int(msg_2);  if (slot == 3)  return int(msg_3);
    if (slot == 4)  return int(msg_4);  if (slot == 5)  return int(msg_5);
    if (slot == 6)  return int(msg_6);  if (slot == 7)  return int(msg_7);
    if (slot == 8)  return int(msg_8);  if (slot == 9)  return int(msg_9);
    if (slot == 10) return int(msg_10); if (slot == 11) return int(msg_11);
    if (slot == 12) return int(msg_12); if (slot == 13) return int(msg_13);
    if (slot == 14) return int(msg_14); if (slot == 15) return int(msg_15);
    if (slot == 16) return int(msg_16); if (slot == 17) return int(msg_17);
    if (slot == 18) return int(msg_18); if (slot == 19) return int(msg_19);
    if (slot == 20) return int(msg_20); if (slot == 21) return int(msg_21);
    if (slot == 22) return int(msg_22); return int(msg_23);
}

int charCount() {
    int n = int(msg_len);
    if (n <= 0) return 7;
    if (n > 24) return 24;
    return n;
}

float hash(float n) { return fract(sin(n * 127.1) * 43758.5453); }

// James fill styles (10 patterns — reduced from 18 for compile speed)
float jamesStyle(int style, vec2 lp, int ch, float gcol, float grow) {
    if (style == 0) return 1.0;
    if (style == 1) return smoothstep(0.45, 0.35, length(lp - 0.5));
    if (style == 2) {
        float nb = sampleAtlas(ch, gcol - 1.0, grow)
                 + sampleAtlas(ch, gcol + 1.0, grow)
                 + sampleAtlas(ch, gcol, grow - 1.0)
                 + sampleAtlas(ch, gcol, grow + 1.0);
        return nb > 3.5 ? 0.0 : 1.0;
    }
    if (style == 3) return step(0.35, fract(lp.y * 3.0));
    if (style == 4) { vec2 c = abs(lp - 0.5); return smoothstep(0.5, 0.4, c.x + c.y); }
    if (style == 5) return max(smoothstep(0.42, 0.38, abs(lp.x - 0.5)), smoothstep(0.42, 0.38, abs(lp.y - 0.5)));
    if (style == 6) return smoothstep(0.42, 0.35, abs(lp.x - 0.5));
    if (style == 7) return step(0.4, fract((lp.x + lp.y) * 2.5));
    if (style == 8) return mod(floor(lp.x * 2.0) + floor(lp.y * 2.0), 2.0);
    return lp.y;
}

// =======================================================================
// 3D Extruded Text — raycasting with Phong lighting
// =======================================================================

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    int numChars = charCount();
    int e = int(effect);

    float rotAngle = TIME * speed;
    float tiltAmount = intensity * 0.5;
    float extrudeDepth = mix(0.05, 0.4, density);
    float cycleSpeed = mix(0.2, 5.0, density);
    int numLayers = int(mix(4.0, 12.0, density));
    if (numLayers < 2) numLayers = 2;

    vec3 ro = vec3(0.0, 0.0, 2.5);
    vec2 screen = (uv - 0.5) * vec2(aspect, 1.0);
    vec3 rd = normalize(vec3(screen, -1.5));

    float cy = cos(rotAngle), sy = sin(rotAngle);
    float cx = cos(tiltAmount), sx = sin(tiltAmount);

    vec3 r1o = vec3(ro.x, ro.y * cx + ro.z * sx, -ro.y * sx + ro.z * cx);
    vec3 r1d = vec3(rd.x, rd.y * cx + rd.z * sx, -rd.y * sx + rd.z * cx);
    vec3 lro = vec3(r1o.x * cy - r1o.z * sy, r1o.y, r1o.x * sy + r1o.z * cy);
    vec3 lrd = vec3(r1d.x * cy - r1d.z * sy, r1d.y, r1d.x * sy + r1d.z * cy);

    float charW = 0.1 * textScale;
    float charH = charW * 1.4;
    float gapW = charW * 0.2;
    float totalW = float(numChars) * (charW + gapW) - gapW;
    float halfW = totalW * 0.5;
    float halfH = charH * 0.5;
    float halfD = extrudeDepth * 0.5;

    vec3 lightDir = normalize(vec3(0.5, 0.7, 1.0));
    vec3 finalColor = transparentBg ? vec3(0.0) : bgColor.rgb;
    float finalAlpha = transparentBg ? 0.0 : 1.0;
    float layerStep = extrudeDepth / max(float(numLayers - 1), 1.0);

    for (int i = 0; i < 12; i++) {
        if (i >= numLayers) break;
        float z = -halfD + float(i) * layerStep;
        if (abs(lrd.z) < 0.0001) continue;
        float t = (z - lro.z) / lrd.z;
        if (t < 0.0) continue;
        vec3 hit = lro + t * lrd;
        float x = hit.x + halfW;
        float y = hit.y + halfH;
        if (x < 0.0 || x > totalW || y < 0.0 || y > charH) continue;
        float cellStep = charW + gapW;
        float cellPos = x / cellStep;
        int slot = int(floor(cellPos));
        float localX = fract(cellPos);
        float charFrac = charW / cellStep;
        if (localX > charFrac || slot < 0 || slot >= numChars) continue;
        float gc = (localX / charFrac) * 5.0;
        float gr = (y / charH) * 7.0;
        if (gc < 0.0 || gc >= 5.0 || gr < 0.0 || gr >= 7.0) continue;
        int ch = getChar(slot);
        if (ch < 0 || ch > 25) continue;
        float px = sampleAtlas(ch, gc, gr);
        if (px < 0.5) continue;

        bool isFront = (i == numLayers - 1);
        float depthFactor = float(i) / max(float(numLayers - 1), 1.0);
        float shade;
        if (isFront) {
            vec3 n = vec3(0.0, 0.0, 1.0);
            vec3 rn = vec3(n.x * cy + n.z * sy, n.y, -n.x * sy + n.z * cy);
            vec3 wn = vec3(rn.x, rn.y * cx - rn.z * sx, rn.y * sx + rn.z * cx);
            float diff = max(dot(wn, lightDir), 0.0);
            vec3 viewDir = normalize(-rd);
            vec3 h = normalize(lightDir + viewDir);
            float spec = pow(max(dot(wn, h), 0.0), 32.0);
            shade = 0.15 + diff * 0.7 + spec * 0.4;
            if (e > 0) {
                float phase = float(slot) * 1.3 + TIME * speed * cycleSpeed;
                int style = int(mod(floor(phase), 10.0));
                vec2 lp = fract(vec2(gc, gr));
                float inten = jamesStyle(style, lp, ch, floor(gc), floor(gr));
                shade *= inten;
            }
        } else {
            shade = mix(0.35, 0.7, depthFactor);
        }
        finalColor = textColor.rgb * clamp(shade, 0.0, 1.0);
        finalAlpha = 1.0;
    }

    gl_FragColor = vec4(finalColor, finalAlpha);
}

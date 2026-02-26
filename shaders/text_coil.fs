/*{
  "CATEGORIES": ["Generator", "Text"],
  "DESCRIPTION": "Coil - text on spiral rings",
  "INPUTS": [
    { "NAME": "msg", "TYPE": "text", "DEFAULT": " ETHEREA", "MAX_LENGTH": 24 },
    { "NAME": "preset", "LABEL": "Style", "TYPE": "long", "VALUES": [0,1,2,3], "LABELS": ["Coil Wide","Coil Star","Coil Lemniscate","Coil Pulse"], "DEFAULT": 0 },
    { "NAME": "fontFamily", "LABEL": "Font", "TYPE": "long", "VALUES": [0,1,2,3], "LABELS": ["Inter","Times New Roman","Libre Caslon","Outfit"], "DEFAULT": 0 },
    { "NAME": "speed", "LABEL": "Speed", "TYPE": "float", "MIN": 0.1, "MAX": 3.0, "DEFAULT": 0.5 },
    { "NAME": "intensity", "LABEL": "Ring Size", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "density", "LABEL": "Rings", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "textScale", "LABEL": "Size", "TYPE": "float", "MIN": 0.3, "MAX": 2.0, "DEFAULT": 1.0 },
    { "NAME": "textColor", "LABEL": "Color", "TYPE": "color", "DEFAULT": [1.0, 1.0, 1.0, 1.0] },
    { "NAME": "bgColor", "LABEL": "Background", "TYPE": "color", "DEFAULT": [0.0, 0.0, 0.0, 1.0] },
    { "NAME": "transparentBg", "LABEL": "Transparent", "TYPE": "bool", "DEFAULT": true }
  ]
}*/

const float PI = 3.14159265;
const float TWO_PI = 6.28318530;

// Atlas-only font engine (no bitmap fallback — faster ANGLE compile)
float charPixel(int ch, float col, float row) {
    if (ch < 0 || ch > 25) return 0.0;
    vec2 uv = vec2(col / 5.0, row / 7.0);
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) return 0.0;
    return smoothstep(0.1, 0.55, texture2D(fontAtlasTex, vec2((float(ch) + uv.x) / 27.0, uv.y)).r);
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
    if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0) return 0.0;
    return texture2D(fontAtlasTex, vec2((float(ch) + uv.x) / 27.0, uv.y)).r;
}

float hash(float n) { return fract(sin(n * 127.1) * 43758.5453); }

// =======================================================================
// EFFECT: COIL - text on spiral rings
// =======================================================================

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

// =======================================================================
// MAIN
// =======================================================================

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    int p = int(preset);
    vec4 col = effectCoil(uv, p);

    if (_voiceGlitch > 0.01) {
        float g = _voiceGlitch;
        float t = TIME * 17.0;
        float band = floor(uv.y * mix(8.0, 40.0, g) + t * 3.0);
        float bandNoise = fract(sin(band * 91.7 + t) * 43758.5);
        float bandActive = step(1.0 - g * 0.6, bandNoise);
        float shift = (bandNoise - 0.5) * 0.08 * g * bandActive;
        float chromaAmt = g * 0.015;
        vec2 uvR = uv + vec2(shift + chromaAmt, 0.0);
        vec2 uvB = uv + vec2(shift - chromaAmt, 0.0);
        vec2 uvG = uv + vec2(shift, chromaAmt * 0.5);
        vec4 cR = effectCoil(uvR, p);
        vec4 cG = effectCoil(uvG, p);
        vec4 cB = effectCoil(uvB, p);
        vec4 glitched = vec4(cR.r, cG.g, cB.b, max(max(cR.a, cG.a), cB.a));
        float scanline = 0.95 + 0.05 * sin(uv.y * RENDERSIZE.y * 1.5 + t * 40.0);
        float blockX = floor(uv.x * 6.0);
        float blockY = floor(uv.y * 4.0);
        float blockNoise = fract(sin((blockX + blockY * 7.0) * 113.1 + floor(t * 8.0)) * 43758.5);
        float dropout = step(1.0 - g * 0.15, blockNoise);
        glitched.rgb *= scanline;
        glitched.rgb *= 1.0 - dropout;
        col = mix(col, glitched, smoothstep(0.0, 0.3, g));
    }

    gl_FragColor = col;
}

/*{
  "CATEGORIES": ["Generator", "Text"],
  "DESCRIPTION": "Bricks - grid with animated displacement",
  "INPUTS": [
    { "NAME": "msg", "TYPE": "text", "DEFAULT": " ETHEREA", "MAX_LENGTH": 24 },
    { "NAME": "preset", "LABEL": "Style", "TYPE": "long", "VALUES": [0,1,2], "LABELS": ["Bricks","Bricks Harlequin","Bricks Zebra"], "DEFAULT": 0 },
    { "NAME": "fontFamily", "LABEL": "Font", "TYPE": "long", "VALUES": [0,1,2,3], "LABELS": ["Inter","Times New Roman","Libre Caslon","Outfit"], "DEFAULT": 0 },
    { "NAME": "speed", "LABEL": "Speed", "TYPE": "float", "MIN": 0.1, "MAX": 3.0, "DEFAULT": 0.5 },
    { "NAME": "intensity", "LABEL": "Displacement", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "density", "LABEL": "Grid Density", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
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
// EFFECT: BRICKS - grid with animated displacement
// =======================================================================

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

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    int p = int(preset);
    vec4 col = effectBricks(uv, p);

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
        vec4 cR = effectBricks(uvR, p);
        vec4 cG = effectBricks(uvG, p);
        vec4 cB = effectBricks(uvB, p);
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

/*{
    "DESCRIPTION": "Morphing 3D shape — box, prism, torus, knots with refraction and wave background",
    "CREDIT": "ISF Import by Old Salt, glslsandbox #73426, adapted for ShaderClaw",
    "CATEGORIES": ["Generator"],
    "INPUTS": [
        { "NAME": "uC1", "LABEL": "Color 1", "TYPE": "color", "DEFAULT": [0.91, 0.25, 0.34, 1.0] },
        { "NAME": "uC2", "LABEL": "Color 2", "TYPE": "color", "DEFAULT": [1.0, 1.0, 1.0, 1.0] },
        { "NAME": "uC3", "LABEL": "Color 3", "TYPE": "color", "DEFAULT": [1.0, 0.0, 0.0, 1.0] },
        { "NAME": "uZoom", "LABEL": "Zoom", "TYPE": "float", "MAX": 1.0, "MIN": -1.0, "DEFAULT": 0.0 },
        { "NAME": "uXYrotate", "LABEL": "XY Rotation", "TYPE": "float", "MAX": 180.0, "MIN": -180.0, "DEFAULT": 60.0 },
        { "NAME": "uYZrotate", "LABEL": "YZ Rotation", "TYPE": "float", "MAX": 180.0, "MIN": -180.0, "DEFAULT": 68.0 },
        { "NAME": "uXZrotate", "LABEL": "XZ Rotation", "TYPE": "float", "MAX": 180.0, "MIN": -180.0, "DEFAULT": 52.0 },
        { "NAME": "uTorThick", "LABEL": "Toroid Thickness", "TYPE": "float", "MAX": 1.0, "MIN": 0.0, "DEFAULT": 0.25 },
        { "NAME": "uDisplay", "LABEL": "Display", "TYPE": "long", "VALUES": [0, 1, 2], "LABELS": ["Object + BG", "Object Only", "Background Only"], "DEFAULT": 0 },
        { "NAME": "uColMode", "LABEL": "Color Mode", "TYPE": "long", "VALUES": [0, 1], "LABELS": ["Default", "Custom Palette"], "DEFAULT": 0 },
        { "NAME": "uIntensity", "LABEL": "Intensity", "TYPE": "float", "MAX": 4.0, "MIN": 0.0, "DEFAULT": 1.0 }
    ]
}*/

#define PI 3.141592653589
#define TwoPI 6.283185307178

mat2 rot2D(float a) { return mat2(cos(a), -sin(a), sin(a), cos(a)); }

float sdBox2d(vec2 p, vec2 s) {
    p = abs(p) - s;
    return length(max(p, 0.0)) + min(max(p.x, p.y), 0.0);
}

float sdBox(vec3 p, vec3 s) {
    p = abs(p) - s;
    return length(max(p, 0.0)) + min(max(p.x, max(p.y, p.z)), 0.0);
}

float sdTorus(vec3 p, float outRadius) {
    vec2 q = vec2(length(p.xz) - outRadius, p.y);
    return length(q) - uTorThick * outRadius;
}

float sdTorusKnots(vec3 p, float outRadius) {
    vec2 cp = vec2(length(p.xz) - outRadius, p.y);
    float a = atan(p.x, p.z);
    cp *= rot2D(a * 8.0);
    cp.y = abs(cp.y) - 0.3;
    return sdBox2d(cp, vec2(uTorThick * outRadius * 0.5, uTorThick * outRadius));
}

float sdTriPrism(vec3 p, vec2 h) {
    vec3 q = abs(p);
    return max(q.z - h.y, max(q.x * sin(PI / 3.0) + p.y * sin(PI / 6.0), -p.y) - h.x * sin(PI / 6.0));
}

float morphing(vec3 p) {
    float tm = TIME / 18.0 + audioBass * 0.5;
    int idx = int(mod(tm, 4.0));
    float a = smoothstep(0.2, 0.8, mod(tm, 1.0));
    if (idx == 0) return mix(sdTriPrism(p, vec2(1.0, 1.5)), sdBox(p, vec3(1.0)), a);
    if (idx == 1) return mix(sdBox(p, vec3(1.0)), sdTorus(p, 2.0), a);
    if (idx == 2) return mix(sdTorus(p, 2.0), sdTorusKnots(p, 2.0) * 0.4, a);
    return mix(sdTorusKnots(p, 2.0) * 0.4, sdTriPrism(p, vec2(1.0, 1.5)), a);
}

float distFunc(vec3 p) {
    // Mouse orbits the object
    float mx = (mousePos.x - 0.5) * TwoPI;
    float my = (mousePos.y - 0.5) * PI;
    p.xy *= rot2D(TIME * uXYrotate / 36.0 + mx);
    p.yz *= rot2D(TIME * uYZrotate / 36.0 + my);
    p.xz *= rot2D(TIME * uXZrotate / 36.0);
    return morphing(p);
}

vec3 getNormal(vec3 p) {
    vec2 e = vec2(0.1, 0.0);
    return normalize(vec3(
        distFunc(p + e.xyy) - distFunc(p - e.xyy),
        distFunc(p + e.yxy) - distFunc(p - e.yxy),
        distFunc(p + e.yyx) - distFunc(p - e.yyx)
    ));
}

vec3 background(vec3 rd) {
    vec3 colT = vec3(0.313, 0.816, 0.816);
    vec3 colM = vec3(0.745, 0.118, 0.243);
    vec3 colK = vec3(0.475, 0.404, 0.765);
    vec3 colH = vec3(1.0, 0.776, 0.224);
    float k = rd.y * 0.5 + 0.5;
    vec3 bg = vec3(1.0 - k);
    float a = atan(rd.x, rd.z);
    float fade = smoothstep(0.8, 0.5, k);
    bg += sin(a * 2.0 + TIME) * sin(a * 10.0 + TIME) * sin(a * 4.0) * fade * colT;
    bg += sin(a * 10.0 + TIME + 10.0) * sin(a * 2.0 + TIME + 10.0) * sin(a * 6.0 + 10.0) * fade * colM;
    bg += sin(a * 5.0 + TIME + 20.0) * sin(a * 3.0 + TIME + 30.0) * sin(a * 8.0 + 20.0) * fade * colK;
    bg += sin(a * 3.0 + TIME + 30.0) * sin(a * 5.0 + TIME + 20.0) * sin(a * 10.0 + 30.0) * fade * colH;
    return bg;
}

void main() {
    float zm = (uZoom < 0.0) ? (1.0 - abs(uZoom)) * 0.25 : (1.0 + uZoom * 3.0) * 0.25;
    vec2 uv = (gl_FragCoord.xy - RENDERSIZE * 0.5) / (RENDERSIZE.y * zm);

    vec3 camPos = vec3(0.0, 0.0, -4.0);
    vec3 forward = vec3(0.0, 0.0, 1.0);
    vec3 right = vec3(1.0, 0.0, 0.0);
    vec3 up = vec3(0.0, 1.0, 0.0);
    vec3 rd = normalize(uv.x * right + uv.y * up + forward);

    vec3 color = vec3(0.0);
    float df = 0.0;
    float d = 0.0;
    vec3 p;
    int disp = int(uDisplay);

    for (int i = 0; i < 64; i++) {
        p = camPos + rd * d;
        df = distFunc(p);
        if (df > 100.0 || df <= 0.001) break;
        d += df;
    }

    if (disp == 2) {
        color = background(rd);
    } else {
        if (df <= 0.001) {
            vec3 normal = getNormal(p);
            rd = refract(rd, normal, 0.1);
            if (disp < 2) color = mix(color, background(rd), smoothstep(0.0, 4.0, d));
        } else if (disp == 0) {
            color = mix(color, background(rd), smoothstep(0.0, 4.0, d));
        }
    }

    vec3 cOut = color;
    int cm = int(uColMode);
    if (cm == 1) {
        cOut = uC1.rgb * color.r + uC2.rgb * color.g + uC3.rgb * color.b;
    }
    cOut *= uIntensity;

    gl_FragColor = vec4(cOut, 1.0);
}

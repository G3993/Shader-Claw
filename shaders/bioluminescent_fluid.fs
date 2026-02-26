/*
{
  "DESCRIPTION": "Star constellation — drifting particles connected by thin lines, drawn to the mouse",
  "CREDIT": "ShaderClaw",
  "CATEGORIES": ["Generator", "Simulation"],
  "INPUTS": [
    { "NAME": "dotSize", "TYPE": "float", "DEFAULT": 0.004, "MIN": 0.001, "MAX": 0.015 },
    { "NAME": "lineThickness", "TYPE": "float", "DEFAULT": 0.0012, "MIN": 0.0002, "MAX": 0.004 },
    { "NAME": "connectionRange", "TYPE": "float", "DEFAULT": 0.22, "MIN": 0.05, "MAX": 0.5 },
    { "NAME": "attraction", "TYPE": "float", "DEFAULT": 0.3, "MIN": 0.0, "MAX": 2.0 },
    { "NAME": "driftSpeed", "TYPE": "float", "DEFAULT": 1.0, "MIN": 0.0, "MAX": 3.0 },
    { "NAME": "starColor", "TYPE": "color", "DEFAULT": [0.7, 0.85, 1.0, 1.0] },
    { "NAME": "lineOpacity", "TYPE": "float", "DEFAULT": 0.35, "MIN": 0.0, "MAX": 1.0 },
    { "NAME": "mouseNode", "TYPE": "float", "DEFAULT": 1.0, "MIN": 0.0, "MAX": 1.0 }
  ],
  "PASSES": [
    { "TARGET": "particleState", "PERSISTENT": true, "WIDTH": 96, "HEIGHT": 1 },
    {}
  ]
}
*/

const float N = 96.0;
const int NI = 96;

float hash(float n) { return fract(sin(n * 127.1) * 43758.5453); }
vec2 hash2(float n) { return fract(sin(vec2(n, n + 1.0)) * vec2(43758.5453, 22578.1459)); }

// =============================================
// PASS 0 — Particle state (32x1, PERSISTENT)
// xy = position, zw = velocity
// =============================================
vec4 passState() {
    float idx = floor(isf_FragNormCoord.x * RENDERSIZE.x);
    vec4 state = texture2D(particleState, isf_FragNormCoord);
    vec2 pos = state.xy;
    vec2 vel = state.zw;

    // First frame: scatter randomly
    if (FRAMEINDEX < 1) {
        pos = hash2(idx * 7.3) * 0.8 + 0.1;
        vel = (hash2(idx * 13.7) - 0.5) * 0.001;
        return vec4(pos, vel);
    }

    // Smooth organic drift unique to each particle
    float t = TIME * driftSpeed;
    float p1 = hash(idx * 1.31) * 6.28;
    float p2 = hash(idx * 2.17) * 6.28;
    float f1 = 0.25 + hash(idx * 3.71) * 0.35;
    float f2 = 0.2 + hash(idx * 4.93) * 0.3;
    vec2 drift = vec2(
        sin(t * f1 + p1) * cos(t * f2 * 0.7 + p2),
        cos(t * f1 * 0.8 + p1) * sin(t * f2 + p2)
    ) * 0.00012 * driftSpeed;

    // Gentle mouse attraction
    vec2 toMouse = mousePos - pos;
    vel += toMouse * attraction * 0.0004;

    vel += drift;
    vel *= 0.985; // damping

    // Soft edge repulsion
    float m = 0.06;
    if (pos.x < m) vel.x += (m - pos.x) * 0.04;
    if (pos.x > 1.0 - m) vel.x -= (pos.x - (1.0 - m)) * 0.04;
    if (pos.y < m) vel.y += (m - pos.y) * 0.04;
    if (pos.y > 1.0 - m) vel.y -= (pos.y - (1.0 - m)) * 0.04;

    // Speed limit
    float spd = length(vel);
    if (spd > 0.004) vel = vel / spd * 0.004;

    pos += vel;
    pos = clamp(pos, vec2(0.01), vec2(0.99));

    return vec4(pos, vel);
}

// =============================================
// PASS 1 — Render constellation
// =============================================
vec4 passRender() {
    vec2 uv = isf_FragNormCoord;
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    vec2 uvA = vec2(uv.x * aspect, uv.y);

    // Cache all particle positions
    vec2 pts[96];
    for (int i = 0; i < NI; i++) {
        pts[i] = texture2D(particleState, vec2((float(i) + 0.5) / N, 0.5)).xy;
    }

    vec3 col = vec3(0.0);

    // --- Background star dust ---
    vec2 sg = uv * 55.0;
    vec2 sc = floor(sg);
    vec2 sf = fract(sg) - 0.5;
    float sh = hash(dot(sc, vec2(127.1, 311.7)));
    if (sh > 0.92) {
        float sb = (sh - 0.92) / 0.08;
        float st = 0.5 + 0.5 * sin(TIME * (0.6 + sh * 2.0) + sh * 80.0);
        col += starColor.rgb * smoothstep(0.04, 0.0, length(sf)) * sb * st * 0.12;
    }

    // --- Constellation lines between pairs ---
    for (int i = 0; i < NI; i++) {
        for (int j = 0; j < NI; j++) {
            if (j > i) {
                float pd = distance(pts[i], pts[j]);
                if (pd < connectionRange) {
                    vec2 a = vec2(pts[i].x * aspect, pts[i].y);
                    vec2 b = vec2(pts[j].x * aspect, pts[j].y);
                    vec2 ab = b - a;
                    float ab2 = dot(ab, ab);
                    if (ab2 > 0.00001) {
                        float t = clamp(dot(uvA - a, ab) / ab2, 0.0, 1.0);
                        float ld = length(uvA - (a + ab * t));
                        float fade = 1.0 - pd / connectionRange;
                        col += starColor.rgb * smoothstep(lineThickness, 0.0, ld) * fade * fade * lineOpacity;
                    }
                }
            }
        }
    }

    // --- Lines from mouse to nearby particles ---
    if (mouseNode > 0.01) {
        vec2 mA = vec2(mousePos.x * aspect, mousePos.y);
        for (int i = 0; i < NI; i++) {
            float md = distance(mousePos, pts[i]);
            if (md < connectionRange * 1.4) {
                vec2 b = vec2(pts[i].x * aspect, pts[i].y);
                vec2 ab = b - mA;
                float ab2 = dot(ab, ab);
                if (ab2 > 0.00001) {
                    float t = clamp(dot(uvA - mA, ab) / ab2, 0.0, 1.0);
                    float ld = length(uvA - (mA + ab * t));
                    float fade = 1.0 - md / (connectionRange * 1.4);
                    col += starColor.rgb * smoothstep(lineThickness * 1.2, 0.0, ld) * fade * fade * lineOpacity * mouseNode * 0.6;
                }
            }
        }
    }

    // --- Star dots ---
    for (int i = 0; i < NI; i++) {
        vec2 diff = uvA - vec2(pts[i].x * aspect, pts[i].y);
        float d = length(diff);

        float depth = 0.6 + hash(float(i) * 3.7) * 0.4;
        float twinkle = 0.7 + 0.3 * sin(TIME * (1.2 + hash(float(i) * 1.9) * 2.5) + hash(float(i) * 5.3) * 6.28);
        float bright = twinkle * depth;

        // Sharp core
        float coreR = dotSize * 0.35 * depth;
        float core = smoothstep(coreR, 0.0, d) * bright;

        // Inner glow
        float glowR = dotSize * 1.5 * depth;
        float glow = smoothstep(glowR, 0.0, d) * 0.2 * bright;

        // Outer halo
        float haloR = dotSize * 4.0 * depth;
        float halo = smoothstep(haloR, 0.0, d) * 0.04 * bright;

        col += starColor.rgb * core * 1.4 + starColor.rgb * glow + starColor.rgb * halo;
    }

    // --- Mouse cursor dot ---
    if (mouseNode > 0.01) {
        vec2 md = uvA - vec2(mousePos.x * aspect, mousePos.y);
        float mdd = length(md);
        col += starColor.rgb * smoothstep(dotSize * 0.4, 0.0, mdd) * mouseNode * 0.7;
        col += starColor.rgb * smoothstep(dotSize * 2.5, 0.0, mdd) * mouseNode * 0.06;
    }

    // Vignette
    vec2 vig = uv * 2.0 - 1.0;
    col *= smoothstep(0.0, 0.7, 1.0 - dot(vig * 0.45, vig * 0.45));

    return vec4(col, 1.0);
}

void main() {
    if (PASSINDEX == 0) {
        gl_FragColor = passState();
    } else {
        gl_FragColor = passRender();
    }
}

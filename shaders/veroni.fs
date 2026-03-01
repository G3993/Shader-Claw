/*{
  "DESCRIPTION": "Veroni — Self-healing voronoi particles advected by fluid simulation",
  "CREDIT": "wyatt (Shadertoy MlVfDR) / ShaderClaw port",
  "CATEGORIES": ["Generator", "Simulation"],
  "INPUTS": [
    { "NAME": "speed", "LABEL": "Speed", "TYPE": "float", "DEFAULT": 1.0, "MIN": 0.0, "MAX": 3.0 },
    { "NAME": "jetStrength", "LABEL": "Jet Force", "TYPE": "float", "DEFAULT": 1.0, "MIN": 0.0, "MAX": 3.0 },
    { "NAME": "cellSize", "LABEL": "Cell Size", "TYPE": "float", "DEFAULT": 10.0, "MIN": 3.0, "MAX": 30.0 },
    { "NAME": "edgeDarken", "LABEL": "Edge Darken", "TYPE": "float", "DEFAULT": 0.1, "MIN": 0.0, "MAX": 0.5 },
    { "NAME": "colorIntensity", "LABEL": "Color Mix", "TYPE": "float", "DEFAULT": 1.0, "MIN": 0.1, "MAX": 5.0 }
  ],
  "PASSES": [
    { "TARGET": "fluid1", "PERSISTENT": true },
    { "TARGET": "fluid2", "PERSISTENT": true },
    { "TARGET": "voro",   "PERSISTENT": true },
    {}
  ]
}*/

// ==========================================
// Veroni — fluid-advected self-healing voronoi
// Based on "Fluid Mosaic" by wyatt
// ==========================================

// Distance from point to line segment
float lineDist(vec2 p, vec2 a, vec2 b) {
    vec2 ab = b - a;
    float d2 = dot(ab, ab);
    if (d2 < 0.0001) return length(p - a);
    return length(p - a - ab * clamp(dot(p - a, ab) / d2, 0.0, 1.0));
}

// Nearest-neighbor read for voronoi (avoids LINEAR bleed between cells)
vec4 voroRead(vec2 px) {
    return texture2D(voro, (floor(px) + 0.5) / RENDERSIZE);
}

// ==========================================
// PASS 0 & 1: Fluid simulation
// Pass 0 reads fluid2, writes fluid1
// Pass 1 reads fluid1, writes fluid2
// ==========================================

vec4 fluidAt(vec2 uv) {
    return (PASSINDEX == 0) ? texture2D(fluid2, uv) : texture2D(fluid1, uv);
}

float fluidStep(vec2 U0, vec2 U, vec2 U1, inout vec4 Q, vec2 r) {
    vec2 V  = U + r;
    vec2 u  = fluidAt(V / RENDERSIZE).xy;
    vec2 V0 = V - u;
    vec2 V1 = V + u;
    float P  = fluidAt(V0 / RENDERSIZE).z;
    float rr = length(r);
    Q.xy -= r * (P - Q.z) / rr / 4.0;
    return (0.5 * (length(V0 - U0) - length(V1 - U1)) + P) / 4.0;
}

vec4 passFluid(vec2 U) {
    vec2 R   = RENDERSIZE;
    vec2 vel = fluidAt(U / R).xy;
    vec2 U0  = U - vel;
    vec2 U1  = U + vel;
    vec4 Q   = fluidAt(U0 / R);
    float P  = 0.0;

    P += fluidStep(U0, U, U1, Q, vec2( 1.0,  0.0));
    P += fluidStep(U0, U, U1, Q, vec2( 0.0, -1.0));
    P += fluidStep(U0, U, U1, Q, vec2(-1.0,  0.0));
    P += fluidStep(U0, U, U1, Q, vec2( 0.0,  1.0));
    Q.z = P;

    if (FRAMEINDEX < 1) Q = vec4(0.0);

    // Boundary — zero velocity at edges
    if (U.x < 1.0 || U.y < 1.0 || R.x - U.x < 1.0 || R.y - U.y < 1.0)
        Q.xy *= 0.0;

    // Fixed jet emitters
    float s = jetStrength * speed;
    if (length(U - vec2(0.10, 0.50) * R) < 0.03 * R.y)
        Q.xy = Q.xy * 0.9 + 0.1 * s * vec2( 0.5, -0.3);
    if (length(U - vec2(0.70, 0.30) * R) < 0.03 * R.y)
        Q.xy = Q.xy * 0.9 + 0.1 * s * vec2(-0.6,  0.3);
    if (length(U - vec2(0.20, 0.20) * R) < 0.03 * R.y)
        Q.xy = Q.xy * 0.9 + 0.1 * s * vec2( 0.4,  0.6);
    if (length(U - vec2(0.70, 0.50) * R) < 0.03 * R.y)
        Q.xy = Q.xy * 0.9 + 0.1 * s * vec2(-0.1, -0.3);
    if (length(U - vec2(0.50, 0.60) * R) < 0.03 * R.y)
        Q.xy = Q.xy * 0.9 + 0.1 * s * vec2( 0.0, -0.7);

    // Mouse interaction
    if (length(mouseDelta) > 0.0001) {
        vec2 cur  = mousePos * R;
        vec2 prev = (mousePos - mouseDelta) * R;
        float l   = lineDist(U, cur, prev);
        if (l < 10.0) {
            Q.xyz += vec3(
                (10.0 - l) * (cur - prev) / R.y,
                (10.0 - l) * length(cur - prev) / R.y * 0.02
            );
        }
    }

    return Q;
}

// ==========================================
// PASS 2: Voronoi particle tracking
// Self-healing via probabilistic swap
// ==========================================

void voroSwap(vec2 U, inout vec4 Q, vec2 off) {
    vec4 p  = voroRead(U + off);
    float dl = length(U - Q.xy) - length(U - p.xy);
    // Probabilistic swap — enables self-healing
    Q = mix(Q, p, 0.5 + 0.5 * sign(floor(1e5 * dl)));
}

vec4 passVoronoi(vec2 U) {
    vec2 R = RENDERSIZE;

    // Advect lookup backwards through fluid
    U -= texture2D(fluid1, U / R).xy;

    // Read current cell + swap with closer neighbors
    vec4 Q = voroRead(U);
    voroSwap(U, Q, vec2( 1.0,  0.0));
    voroSwap(U, Q, vec2( 0.0,  1.0));
    voroSwap(U, Q, vec2( 0.0, -1.0));
    voroSwap(U, Q, vec2(-1.0,  0.0));

    // Color jets — assign unique IDs near emitters
    if (length(Q.xy - vec2(0.10, 0.50) * R) < 0.02 * R.y) Q.zw = vec2(1.0, 1.0);
    if (length(Q.xy - vec2(0.70, 0.30) * R) < 0.02 * R.y) Q.zw = vec2(3.0, 3.0);
    if (length(Q.xy - vec2(0.20, 0.20) * R) < 0.02 * R.y) Q.zw = vec2(6.0, 5.0);
    if (length(Q.xy - vec2(0.70, 0.50) * R) < 0.02 * R.y) Q.zw = vec2(2.0, 7.0);
    if (length(Q.xy - vec2(0.50, 0.60) * R) < 0.02 * R.y) Q.zw = vec2(5.0, 4.0);

    // Mouse spawns new cells
    if (length(mouseDelta) > 0.0001) {
        vec2 cur  = mousePos * R;
        vec2 prev = (mousePos - mouseDelta) * R;
        if (lineDist(U, cur, prev) < 10.0)
            Q = vec4(U, 1.0, 3.0 * sin(0.4 * TIME));
    }

    // Advect particle position forward through fluid
    Q.xy += texture2D(fluid1, Q.xy / R).xy;

    // Init: grid of seed particles
    if (FRAMEINDEX < 1) Q = vec4(floor(U / cellSize + 0.5) * cellSize, 0.2, -0.1);

    return Q;
}

// ==========================================
// PASS 3: Final render
// ==========================================

vec4 passRender(vec2 U) {
    vec4 C = voroRead(U);

    // Edge detection — compare neighbors' cell centers
    vec2 n = voroRead(U + vec2(0.0, 1.0)).xy;
    vec2 e = voroRead(U + vec2(1.0, 0.0)).xy;
    vec2 s = voroRead(U - vec2(0.0, 1.0)).xy;
    vec2 w = voroRead(U - vec2(1.0, 0.0)).xy;
    float d = (length(n - C.xy) - 1.0)
            + (length(e - C.xy) - 1.0)
            + (length(s - C.xy) - 1.0)
            + (length(w - C.xy) - 1.0);

    // Audio-reactive color modulation
    float m1 = 2.0  * texture2D(audioFFT, vec2(abs(0.3 * C.w), 0.0)).x;
    float m2 = 1.5  * texture2D(audioFFT, vec2(abs(0.3 * C.z), 0.0)).x;

    // Sinusoidal palette from cell IDs
    float ci = colorIntensity;
    vec4 col = 0.5 - 0.5 * sin(
        0.2 * ci * (1.0 + m1) * C.z * vec4(1.0)
      + 0.4 * ci * (3.0 + m2) * C.w * vec4(1.0, 3.0, 5.0, 4.0)
    );

    col *= 1.0 - clamp(edgeDarken * d, 0.0, 1.0);
    col.a = 1.0;
    return col;
}

// ==========================================
// Dispatch
// ==========================================

void main() {
    vec2 U = gl_FragCoord.xy;
    if      (PASSINDEX == 0) gl_FragColor = passFluid(U);
    else if (PASSINDEX == 1) gl_FragColor = passFluid(U);
    else if (PASSINDEX == 2) gl_FragColor = passVoronoi(U);
    else                     gl_FragColor = passRender(U);
}

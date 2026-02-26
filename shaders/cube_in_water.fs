/*
{
  "CATEGORIES": ["Generator", "Nature", "3D"],
  "DESCRIPTION": "Chrome cube bobbing on a raymarched ocean — procedural sky, wave reflections, underwater darkening",
  "INPUTS": [
    { "NAME": "iMouse", "TYPE": "point2D" },
    { "NAME": "sunElevation", "TYPE": "float", "MIN": 0.05, "MAX": 1.0, "DEFAULT": 0.15 },
    { "NAME": "sunAzimuth", "TYPE": "float", "MIN": -3.1416, "MAX": 3.1416, "DEFAULT": 0.8 },
    { "NAME": "sunColor", "TYPE": "color", "DEFAULT": [1.0, 0.95, 0.85, 1.0] },
    { "NAME": "waterColor", "TYPE": "color", "DEFAULT": [0.0, 0.12, 0.1, 1.0] },
    { "NAME": "SEA_FREQ", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.16 },
    { "NAME": "SEA_CHOPPY", "TYPE": "float", "MIN": 0.0, "MAX": 8.0, "DEFAULT": 4.0 },
    { "NAME": "SEA_HEIGHT", "TYPE": "float", "MIN": 0.0, "MAX": 3.0, "DEFAULT": 0.6 },
    { "NAME": "SEA_SPEED", "TYPE": "float", "MIN": 0.0, "MAX": 2.0, "DEFAULT": 0.8 },
    { "NAME": "cubeSize", "TYPE": "float", "MIN": 0.2, "MAX": 2.0, "DEFAULT": 0.8 },
    { "NAME": "cubeY", "TYPE": "float", "MIN": -1.0, "MAX": 2.0, "DEFAULT": 0.6 },
    { "NAME": "cubeSpin", "TYPE": "float", "MIN": 0.0, "MAX": 3.0, "DEFAULT": 0.5 },
    { "NAME": "cubeColor", "TYPE": "color", "DEFAULT": [0.8, 0.85, 0.9, 1.0] },
    { "NAME": "cubeRoughness", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.15 },
    { "NAME": "orbitSpeed", "TYPE": "float", "MIN": 0.0, "MAX": 2.0, "DEFAULT": 0.3 },
    { "NAME": "camHeight", "TYPE": "float", "MIN": 1.0, "MAX": 8.0, "DEFAULT": 3.5 },
    { "NAME": "camDist", "TYPE": "float", "MIN": 3.0, "MAX": 20.0, "DEFAULT": 8.0 },
    { "NAME": "fogDensity", "TYPE": "float", "MIN": 0.0, "MAX": 0.02, "DEFAULT": 0.004 }
  ]
}
*/

// "Cube in Water" — ShaderClaw
// Ocean based on "Seascape" by Alexander Alekseev aka TDM - 2014
// License Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.

const float PI = 3.14159265;
const float EPSILON = 1e-3;
float EPSILON_NRM = 0.1 / RENDERSIZE.x;

// Ocean iteration counts
const int ITER_GEOMETRY = 3;
const int ITER_FRAGMENT = 5;
const int NUM_STEPS = 8;

// SDF marching
const int CUBE_STEPS = 48;
const int CUBE_REFL_STEPS = 32;
const float CUBE_MAX_DIST = 40.0;
const float CUBE_SURF_DIST = 0.002;

float SEA_TIME = TIME * SEA_SPEED;
mat2 octave_m = mat2(1.6, 1.2, -1.2, 1.6);

// Sun direction from elevation + azimuth
vec3 sunDir = normalize(vec3(sin(sunAzimuth) * cos(sunElevation * PI * 0.5),
                             sin(sunElevation * PI * 0.5),
                             cos(sunAzimuth) * cos(sunElevation * PI * 0.5)));

// --- Cube transform ---
vec3 cubeCenter;
mat3 cubeRot;
mat3 cubeRotInv;

void initCube() {
    cubeCenter = vec3(0.0, cubeY + sin(TIME * 0.7) * 0.5, 0.0);

    float ay = TIME * cubeSpin;
    float ax = TIME * cubeSpin * 0.7;
    float sa = sin(ay), ca = cos(ay);
    float sb = sin(ax), cb = cos(ax);

    // Ry * Rx
    cubeRot = mat3(
        ca,       sa * sb,   sa * cb,
        0.0,      cb,       -sb,
       -sa,       ca * sb,   ca * cb
    );
    cubeRotInv = mat3(
        ca,  0.0, -sa,
        sa * sb, cb, ca * sb,
        sa * cb, -sb, ca * cb
    );
}

// --- Math ---
float hash(vec2 p) {
    float h = dot(p, vec2(127.1, 311.7));
    return fract(sin(h) * 43758.5453123);
}

float noise(in vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    return -1.0 + 2.0 * mix(
        mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x),
        mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x),
        u.y
    );
}

// --- Lighting helpers ---
float diffuseWrap(vec3 n, vec3 l, float p) {
    return pow(dot(n, l) * 0.4 + 0.6, p);
}

float specularPhong(vec3 n, vec3 l, vec3 e, float s) {
    float nrm = (s + 8.0) / (PI * 8.0);
    return pow(max(dot(reflect(e, n), l), 0.0), s) * nrm;
}

// --- SDF ---
float sdBox(vec3 p, vec3 b) {
    vec3 d = abs(p) - b;
    return min(max(d.x, max(d.y, d.z)), 0.0) + length(max(d, 0.0));
}

float cubeSDF(vec3 p) {
    vec3 local = cubeRotInv * (p - cubeCenter);
    return sdBox(local, vec3(cubeSize * 0.5));
}

vec3 cubeNormal(vec3 p) {
    vec2 e = vec2(0.001, 0.0);
    return normalize(vec3(
        cubeSDF(p + e.xyy) - cubeSDF(p - e.xyy),
        cubeSDF(p + e.yxy) - cubeSDF(p - e.yxy),
        cubeSDF(p + e.yyx) - cubeSDF(p - e.yyx)
    ));
}

// Sphere-trace the cube. Returns distance, -1.0 if miss.
float traceCube(vec3 ro, vec3 rd, int steps) {
    float t = 0.0;
    for (int i = 0; i < CUBE_STEPS; i++) {
        if (i >= steps) break;
        vec3 p = ro + rd * t;
        float d = cubeSDF(p);
        if (d < CUBE_SURF_DIST) return t;
        t += d;
        if (t > CUBE_MAX_DIST) break;
    }
    return -1.0;
}

// --- Sky ---
vec3 getSkyColor(vec3 rd) {
    float sunDot = dot(rd, sunDir);
    float y = max(rd.y, 0.0);

    // Atmosphere base — blue zenith, warm horizon
    vec3 sky = mix(vec3(0.3, 0.5, 0.8), vec3(0.1, 0.2, 0.55), y);

    // Warm near sun
    float sunAngle = acos(clamp(sunDot, -1.0, 1.0));
    float sunProx = exp(-sunAngle * 2.0);
    sky += sunColor.rgb * sunProx * 0.4;

    // Horizon haze
    float haze = exp(-y * 5.0);
    sky = mix(sky, vec3(0.7, 0.75, 0.8), haze * 0.5);

    // Sun disc
    float sunDisc = smoothstep(0.9995, 0.9999, sunDot);
    sky += sunColor.rgb * sunDisc * 3.0;

    // Below horizon — darken to match water
    if (rd.y < 0.0) {
        sky *= exp(rd.y * 3.0);
    }

    return sky;
}

// --- Ocean (from TDM Seascape) ---
float sea_octave(vec2 uv, float choppy) {
    uv += noise(uv);
    vec2 wv = 1.0 - abs(sin(uv));
    vec2 swv = abs(cos(uv));
    wv = mix(wv, swv, wv);
    return pow(1.0 - pow(wv.x * wv.y, 0.65), choppy);
}

float map(vec3 p) {
    float freq = SEA_FREQ;
    float amp = SEA_HEIGHT;
    float choppy = SEA_CHOPPY;
    vec2 uv = p.xz;
    uv.x *= 0.75;
    float d, h = 0.0;
    for (int i = 0; i < ITER_GEOMETRY; i++) {
        d = sea_octave((uv + SEA_TIME) * freq, choppy);
        d += sea_octave((uv - SEA_TIME) * freq, choppy);
        h += d * amp;
        uv *= octave_m;
        freq *= 1.9;
        amp *= 0.22;
        choppy = mix(choppy, 1.0, 0.2);
    }
    return p.y - h;
}

float map_detailed(vec3 p) {
    float freq = SEA_FREQ;
    float amp = SEA_HEIGHT;
    float choppy = SEA_CHOPPY;
    vec2 uv = p.xz;
    uv.x *= 0.75;
    float d, h = 0.0;
    for (int i = 0; i < ITER_FRAGMENT; i++) {
        d = sea_octave((uv + SEA_TIME) * freq, choppy);
        d += sea_octave((uv - SEA_TIME) * freq, choppy);
        h += d * amp;
        uv *= octave_m;
        freq *= 1.9;
        amp *= 0.22;
        choppy = mix(choppy, 1.0, 0.2);
    }
    return p.y - h;
}

vec3 getSeaNormal(vec3 p, float eps) {
    vec3 n;
    n.y = map_detailed(p);
    n.x = map_detailed(vec3(p.x + eps, p.y, p.z)) - n.y;
    n.z = map_detailed(vec3(p.x, p.y, p.z + eps)) - n.y;
    n.y = eps;
    return normalize(n);
}

float heightMapTracing(vec3 ori, vec3 dir, out vec3 p) {
    float tm = 0.0;
    float tx = 1000.0;
    float hx = map(ori + dir * tx);
    if (hx > 0.0) return tx;
    float hm = map(ori + dir * tm);
    float tmid = 0.0;
    for (int i = 0; i < NUM_STEPS; i++) {
        tmid = mix(tm, tx, hm / (hm - hx));
        p = ori + dir * tmid;
        float hmid = map(p);
        if (hmid < 0.0) {
            tx = tmid;
            hx = hmid;
        } else {
            tm = tmid;
            hm = hmid;
        }
    }
    return tmid;
}

// --- Cube shading ---
vec3 shadeCube(vec3 p, vec3 rd) {
    vec3 n = cubeNormal(p);
    float NdotL = max(dot(n, sunDir), 0.0);

    // Diffuse
    vec3 diff = cubeColor.rgb * diffuseWrap(n, sunDir, 2.0);

    // Specular — Blinn-Phong, roughness controls power
    vec3 h = normalize(sunDir - rd);
    float spec = pow(max(dot(n, h), 0.0), mix(256.0, 8.0, cubeRoughness));
    vec3 specCol = sunColor.rgb * spec * 1.5;

    // Environment reflection
    vec3 refl = reflect(rd, n);
    vec3 envRefl = getSkyColor(refl);
    float fresnel = pow(1.0 - max(dot(n, -rd), 0.0), 5.0);
    float reflAmount = mix(0.04, 1.0, fresnel) * (1.0 - cubeRoughness * 0.8);

    vec3 col = mix(diff + specCol, envRefl, reflAmount);

    // Underwater darkening — if below the approximate water surface
    float waterLine = SEA_HEIGHT * 0.5;
    if (p.y < waterLine) {
        float depth = (waterLine - p.y);
        float atten = exp(-depth * 2.0);
        col = mix(waterColor.rgb * 0.3, col, atten);
        // Subtle caustic pattern
        float caustic = 0.5 + 0.5 * sin(p.x * 8.0 + TIME * 2.0) * sin(p.z * 8.0 + TIME * 1.5);
        col += waterColor.rgb * caustic * 0.1 * (1.0 - atten);
    }

    return col;
}

// --- Fog ---
vec3 applyFog(vec3 col, float dist, vec3 rd) {
    float fog = 1.0 - exp(-dist * fogDensity);
    vec3 fogCol = getSkyColor(rd) * 0.7;
    return mix(col, fogCol, fog);
}

// --- Main ---
void main() {
    initCube();

    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    uv = uv * 2.0 - 1.0;
    uv.x *= RENDERSIZE.x / RENDERSIZE.y;

    // Camera — orbit around cube
    float angle = TIME * orbitSpeed + iMouse.x * 0.01;
    vec3 camPos = vec3(sin(angle) * camDist, camHeight, cos(angle) * camDist);
    vec3 camTarget = cubeCenter;

    // Look-at matrix
    vec3 fw = normalize(camTarget - camPos);
    vec3 rt = normalize(cross(fw, vec3(0.0, 1.0, 0.0)));
    vec3 up = cross(rt, fw);
    vec3 rd = normalize(uv.x * rt + uv.y * up + 2.0 * fw);

    // --- Trace both targets ---
    // 1. Cube sphere-trace
    float tCube = traceCube(camPos, rd, CUBE_STEPS);

    // 2. Ocean height-map trace
    vec3 seaP;
    float tSea = heightMapTracing(camPos, rd, seaP);
    // Check if ocean hit is valid (ray going downward enough)
    bool seaHit = (rd.y < 0.0) && (tSea < 999.0);

    // Determine closest hit
    bool cubeHit = tCube > 0.0;
    // If both hit, pick closer
    bool useCube = cubeHit && (!seaHit || tCube < tSea);
    bool useSea = seaHit && (!cubeHit || tSea <= tCube);

    vec3 color;

    if (useCube) {
        // --- Cube primary hit ---
        vec3 hitP = camPos + rd * tCube;
        color = shadeCube(hitP, rd);
        color = applyFog(color, tCube, rd);
    }
    else if (useSea) {
        // --- Ocean primary hit ---
        vec3 dist = seaP - camPos;
        float distLen = length(dist);
        vec3 n = getSeaNormal(seaP, dot(dist, dist) * EPSILON_NRM);

        // Fresnel
        float fresnel = pow(1.0 - max(dot(n, -rd), 0.0), 3.0) * 0.65;

        // Reflection ray
        vec3 reflDir = reflect(rd, n);
        vec3 reflected;

        // Check if reflection hits the cube
        float tReflCube = traceCube(seaP + n * 0.05, reflDir, CUBE_REFL_STEPS);
        if (tReflCube > 0.0) {
            vec3 reflHit = seaP + n * 0.05 + reflDir * tReflCube;
            reflected = shadeCube(reflHit, reflDir);
        } else {
            reflected = getSkyColor(reflDir);
        }

        // Refracted / base water color
        vec3 refracted = waterColor.rgb * 0.3 + diffuseWrap(n, sunDir, 80.0) * waterColor.rgb * 0.5;

        color = mix(refracted, reflected, fresnel);

        // Water depth tint
        float atten = max(1.0 - dot(dist, dist) * 0.001, 0.0);
        color += waterColor.rgb * (seaP.y - SEA_HEIGHT) * 0.18 * atten;

        // Sun specular on water
        color += sunColor.rgb * specularPhong(n, sunDir, rd, 60.0) * 1.5;

        // Secondary shimmer
        float shimmer = specularPhong(n, sunDir, rd, 200.0) * 0.15;
        float shimmerPattern = sin(seaP.x * 30.0 + TIME * 1.5) * sin(seaP.z * 20.0 - TIME * 0.8);
        color += sunColor.rgb * shimmer * (0.5 + 0.5 * shimmerPattern) * 0.5;

        color = applyFog(color, distLen, rd);
    }
    else {
        // --- Sky ---
        color = getSkyColor(rd);
    }

    // Tone mapping
    color = pow(color, vec3(0.75));

    // Subtle vignette
    vec2 vUV = gl_FragCoord.xy / RENDERSIZE.xy;
    float vig = 1.0 - 0.15 * length((vUV - 0.5) * 1.5);
    color *= vig;

    // _transparentBg compositing: output black for sky so runtime luminance→alpha makes it transparent
    if (_transparentBg > 0.5 && !useCube && !useSea) {
        gl_FragColor = vec4(0.0);
        return;
    }

    gl_FragColor = vec4(color, 1.0);
}

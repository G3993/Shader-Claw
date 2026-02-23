/*
{
  "CATEGORIES": ["Generator", "Nature"],
  "DESCRIPTION": "TDM Seascape with a massive 3D moon sphere — night ocean with natural reflections",
  "INPUTS": [
    { "NAME": "iMouse", "TYPE": "point2D" },
    { "NAME": "SEA_FREQ", "MIN": 0.0, "MAX": 1.0, "TYPE": "float", "DEFAULT": 0.16 },
    { "NAME": "SEA_CHOPPY", "MIN": 0.0, "MAX": 8.0, "TYPE": "float", "DEFAULT": 4.0 },
    { "NAME": "SEA_HEIGHT", "MIN": 0.0, "MAX": 3.0, "TYPE": "float", "DEFAULT": 0.6 },
    { "NAME": "SEA_SPEED", "MIN": 0.0, "MAX": 2.0, "TYPE": "float", "DEFAULT": 0.8 },
    { "NAME": "SEA_BASE", "TYPE": "color", "DEFAULT": [0.02, 0.04, 0.08, 1.0] },
    { "NAME": "SEA_WATER_COLOR", "TYPE": "color", "DEFAULT": [0.3, 0.5, 0.6, 1.0] },
    { "NAME": "moonSize", "TYPE": "float", "MIN": 0.1, "MAX": 1.2, "DEFAULT": 0.55 },
    { "NAME": "moonBrightness", "TYPE": "float", "MIN": 0.5, "MAX": 5.0, "DEFAULT": 2.0 },
    { "NAME": "moonElevation", "TYPE": "float", "MIN": 0.05, "MAX": 0.6, "DEFAULT": 0.2 },
    { "NAME": "moonColor", "TYPE": "color", "DEFAULT": [1.0, 0.95, 0.85, 1.0] },
    { "NAME": "starDensity", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "moonPhase", "TYPE": "float", "MIN": -1.0, "MAX": 1.0, "DEFAULT": 0.3 }
  ]
}
*/

// "Seascape" by Alexander Alekseev aka TDM - 2014
// Massive 3D moon sphere by ShaderClaw
// License Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.

const int NUM_STEPS = 8;
const float PI = 3.1415;
const float EPSILON = 1e-3;
float EPSILON_NRM = 0.1 / RENDERSIZE.x;

const int ITER_GEOMETRY = 3;
const int ITER_FRAGMENT = 5;
float SEA_TIME = TIME * SEA_SPEED;
mat2 octave_m = mat2(1.6, 1.2, -1.2, 1.6);

// Moon direction — always in front of camera, elevation controls height
vec3 moonDir = normalize(vec3(0.0, moonElevation * 2.0, -1.0));

// math
mat3 fromEuler(vec3 ang) {
    vec2 a1 = vec2(sin(ang.x), cos(ang.x));
    vec2 a2 = vec2(sin(ang.y), cos(ang.y));
    vec2 a3 = vec2(sin(ang.z), cos(ang.z));
    mat3 m;
    m[0] = vec3(a1.y*a3.y+a1.x*a2.x*a3.x, a1.y*a2.x*a3.x+a3.y*a1.x, -a2.y*a3.x);
    m[1] = vec3(-a2.y*a1.x, a1.y*a2.y, a2.x);
    m[2] = vec3(a3.y*a1.x*a2.x+a1.y*a3.x, a1.x*a3.x-a1.y*a3.y*a2.x, a2.y*a3.y);
    return m;
}

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

float noise01(vec2 p) {
    return noise(p) * 0.5 + 0.5;
}

float fbm(vec2 p) {
    float v = 0.0, a = 0.5;
    mat2 rot = mat2(0.8, 0.6, -0.6, 0.8);
    for (int i = 0; i < 5; i++) {
        v += a * noise01(p);
        p = rot * p * 2.0 + 100.0;
        a *= 0.5;
    }
    return v;
}

// lighting
float diffuse(vec3 n, vec3 l, float p) {
    return pow(dot(n, l) * 0.4 + 0.6, p);
}

float specular(vec3 n, vec3 l, vec3 e, float s) {
    float nrm = (s + 8.0) / (PI * 8.0);
    return pow(max(dot(reflect(e, n), l), 0.0), s) * nrm;
}

// --- Stars ---
float stars(vec3 dir) {
    vec2 sp = vec2(atan(dir.x, dir.z), asin(clamp(dir.y, -1.0, 1.0)));
    sp *= vec2(4.0, 8.0);
    float c = 0.0;
    for (float i = 1.0; i < 4.0; i += 1.0) {
        vec2 grid = sp * (20.0 + i * 15.0);
        vec2 id = floor(grid);
        vec2 gv = fract(grid) - 0.5;
        float h = hash(id + i * 73.0);
        if (h > (1.0 - starDensity * 0.12)) {
            vec2 off = vec2(hash(id * 1.3 + 10.0), hash(id * 2.7 + 20.0)) - 0.5;
            float d = length(gv - off * 0.5);
            float twinkle = 0.7 + 0.3 * sin(TIME * (1.0 + h * 4.0) + h * 6.28);
            c += smoothstep(0.03, 0.0, d) * twinkle * (0.4 + 0.6 / i);
        }
    }
    return c;
}

// --- 3D Moon Sphere ---
// Reconstructs sphere surface normal from ray direction for proper 3D shading
vec3 getMoon(vec3 dir) {
    float cosAngle = dot(normalize(dir), moonDir);
    float angle = acos(clamp(cosAngle, -1.0, 1.0));
    float diskRadius = moonSize * 0.5;

    // Tangent frame on moon
    vec3 right = normalize(cross(moonDir, vec3(0.0, 1.0, 0.001)));
    vec3 up = cross(right, moonDir);

    // Project onto moon disk plane
    vec2 moonUV = vec2(dot(dir - moonDir * cosAngle, right),
                       dot(dir - moonDir * cosAngle, up));
    // Normalize to disk radius
    vec2 diskUV = moonUV / diskRadius;
    float diskDist = length(diskUV);

    // Soft edge
    float disk = smoothstep(1.0, 0.97, diskDist);

    if (disk < 0.001) {
        // Outside disk — just glow
        float glow = 0.0;
        glow += 0.6 * exp(-angle * angle / (diskRadius * diskRadius * 2.5));
        glow += 0.3 * exp(-angle * angle / (diskRadius * diskRadius * 8.0));
        glow += 0.15 * exp(-angle * angle / (diskRadius * diskRadius * 25.0));
        glow += 0.05 * exp(-angle * angle / (diskRadius * diskRadius * 80.0));
        return moonColor.rgb * moonBrightness * glow * 0.6;
    }

    // --- Sphere normal reconstruction ---
    // diskUV maps to the visible hemisphere of a unit sphere
    float z2 = 1.0 - diskUV.x * diskUV.x - diskUV.y * diskUV.y;
    float z = sqrt(max(z2, 0.0));
    // Normal in moon's local frame
    vec3 sphereNormal = normalize(diskUV.x * right + diskUV.y * up + z * moonDir);

    // --- Sun direction for moon lighting (comes from the side, controlled by phase) ---
    vec3 sunDir = normalize(vec3(moonPhase, 0.3, -0.5));

    // Diffuse lighting on sphere — gives the 3D shading + terminator
    float NdotL = dot(sphereNormal, sunDir);
    float moonDiffuse = smoothstep(-0.08, 0.3, NdotL); // soft terminator

    // Limb darkening — edges of the sphere are darker
    float limb = pow(z, 0.4);

    // --- Surface detail (maria/craters) ---
    // Spherical UV for texture mapping
    vec2 sphereUV = vec2(
        atan(diskUV.x, z) / PI,
        atan(diskUV.y, z) / PI
    );

    // Large dark maria (lunar seas)
    float maria = fbm(sphereUV * 4.0 + 3.0);
    maria = smoothstep(0.35, 0.65, maria) * 0.25;

    // Medium craters
    float craters = 0.0;
    for (float i = 0.0; i < 3.0; i++) {
        vec2 cp = sphereUV * (8.0 + i * 12.0) + i * 7.0;
        vec2 cid = floor(cp);
        vec2 cf = fract(cp) - 0.5;
        float ch = hash(cid + i * 31.0);
        if (ch > 0.7) {
            float cd = length(cf - (vec2(hash(cid * 1.7), hash(cid * 2.3)) - 0.5) * 0.3);
            float craterSize = 0.08 + ch * 0.12;
            // Crater: dark ring with bright center
            float ring = smoothstep(craterSize, craterSize * 0.7, cd) - smoothstep(craterSize * 0.5, craterSize * 0.2, cd) * 0.5;
            craters += ring * 0.15;
        }
    }

    // Fine surface texture
    float grain = noise01(sphereUV * 40.0) * 0.06;

    // Combine surface
    float surfaceDetail = 1.0 - maria - craters + grain;
    surfaceDetail = clamp(surfaceDetail, 0.6, 1.0);

    // Final moon color
    vec3 moonCol = moonColor.rgb * surfaceDetail * moonDiffuse * limb * moonBrightness;

    // Slight ambient on dark side (earthshine)
    vec3 ambient = moonColor.rgb * 0.03 * moonBrightness * limb;
    moonCol = max(moonCol, ambient);

    // Apply disk mask
    moonCol *= disk;

    // Principle 2: Anticipation — moon glow breathes, swelling before dimming
    float breathe = 1.0 + 0.04 * sin(TIME * 0.3) * sin(TIME * 0.3); // asymmetric: holds bright, dips quickly

    // Add glow behind
    float glow = 0.0;
    glow += 0.6 * exp(-angle * angle / (diskRadius * diskRadius * 2.5));
    glow += 0.3 * exp(-angle * angle / (diskRadius * diskRadius * 8.0));
    glow += 0.15 * exp(-angle * angle / (diskRadius * diskRadius * 25.0));
    glow += 0.05 * exp(-angle * angle / (diskRadius * diskRadius * 80.0));
    vec3 glowCol = moonColor.rgb * moonBrightness * glow * 0.5 * (1.0 - disk * 0.7);

    return (moonCol + glowCol) * breathe;
}

// sky — night version with massive moon and stars
vec3 getSkyColor(vec3 e) {
    e.y = max(e.y, 0.0);

    // Dark night sky gradient
    vec3 sky = SEA_BASE.rgb * 0.5;
    sky += vec3(0.0, 0.01, 0.04) * (1.0 - e.y);

    // Moon
    vec3 moon = getMoon(e);

    // Fade stars near moon
    float moonAngle = acos(clamp(dot(e, moonDir), -1.0, 1.0));
    float starFade = smoothstep(moonSize * 0.5, moonSize * 2.0, moonAngle);
    float starLight = stars(e) * starFade;
    sky += vec3(0.7, 0.8, 1.0) * starLight;

    sky += moon;

    return sky;
}

// sea
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

vec3 getSeaColor(vec3 p, vec3 n, vec3 l, vec3 eye, vec3 dist) {
    float fresnel = 1.0 - max(dot(n, -eye), 0.0);
    fresnel = pow(fresnel, 3.0) * 0.65;

    vec3 reflected = getSkyColor(reflect(eye, n));
    vec3 refracted = SEA_BASE.rgb + diffuse(n, l, 80.0) * SEA_WATER_COLOR.rgb * 0.12;

    vec3 color = mix(refracted, reflected, fresnel);

    float atten = max(1.0 - dot(dist, dist) * 0.001, 0.0);
    color += SEA_WATER_COLOR.rgb * (p.y - SEA_HEIGHT) * 0.18 * atten;

    // Principle 10: Exaggeration — push the moon pillar brighter than physically correct
    color += moonColor.rgb * specular(n, l, eye, 40.0) * moonBrightness * 0.7;

    // Principle 8+5: Secondary Action + Follow Through — shimmer lags behind main specular
    float lagSpec = specular(n, l, eye, 200.0) * 0.15;
    float shimmerPhase = sin(p.x * 30.0 + TIME * 1.5) * sin(p.z * 20.0 - TIME * 0.8);
    color += moonColor.rgb * lagSpec * (0.5 + 0.5 * shimmerPhase) * moonBrightness * 0.3;

    return color;
}

// tracing
vec3 getNormal(vec3 p, float eps) {
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

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    uv = uv * 2.0 - 1.0;
    uv.x *= RENDERSIZE.x / RENDERSIZE.y;
    float time = TIME * 0.3 + iMouse.x * 0.01;

    // ray — gentle sway instead of full rotation so moon stays in view
    // Principle 6: Slow In/Slow Out — eased camera sway feels weighty, like a ship rocking
    float sx = sin(time * 1.5); sx = sign(sx) * pow(abs(sx), 0.6);
    float sy = sin(time * 0.7); sy = sign(sy) * pow(abs(sy), 0.6);
    float sz = sin(time * 0.4); sz = sign(sz) * pow(abs(sz), 0.6);
    vec3 ang = vec3(sx * 0.05, sy * 0.08 + 0.3, sz * 0.15);
    vec3 ori = vec3(0.0, 3.5, time * 5.0);
    vec3 dir = normalize(vec3(uv.xy, -2.0));
    dir.z += length(uv) * 0.15;
    dir = normalize(dir) * fromEuler(ang);

    // tracing
    vec3 p;
    heightMapTracing(ori, dir, p);
    vec3 dist = p - ori;
    vec3 n = getNormal(p, dot(dist, dist) * EPSILON_NRM);

    // Light comes from the moon
    vec3 light = moonDir;

    // color
    vec3 color = mix(
        getSkyColor(dir),
        getSeaColor(p, n, light, dir, dist),
        pow(smoothstep(0.0, -0.05, dir.y), 0.3)
    );

    // Slight blue-ish night tint in post
    color = pow(color, vec3(0.75));
    // subtle vignette
    vec2 vUV = gl_FragCoord.xy / RENDERSIZE.xy;
    float vig = 1.0 - 0.2 * length((vUV - 0.5) * 1.5);
    color *= vig;

    gl_FragColor = vec4(color, 1.0);
}
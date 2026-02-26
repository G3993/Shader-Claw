/*{
  "CATEGORIES": ["Generator", "Nature"],
  "DESCRIPTION": "Flocking birds — tiny origami boids made of two triangle wings, dense flocks against atmospheric sky",
  "INPUTS": [
    { "NAME": "flockCount", "TYPE": "float", "MIN": 1.0, "MAX": 8.0, "DEFAULT": 6.0 },
    { "NAME": "birdsPerFlock", "TYPE": "float", "MIN": 4.0, "MAX": 120.0, "DEFAULT": 84.0 },
    { "NAME": "sizeVariation", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "speed", "TYPE": "float", "MIN": 0.1, "MAX": 3.0, "DEFAULT": 0.6 },
    { "NAME": "birdSize", "TYPE": "float", "MIN": 0.1, "MAX": 2.0, "DEFAULT": 0.7 },
    { "NAME": "spread", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "flapSpeed", "TYPE": "float", "MIN": 0.5, "MAX": 5.0, "DEFAULT": 2.5 },
    { "NAME": "depth", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "skyTop", "TYPE": "color", "DEFAULT": [0.15, 0.3, 0.65, 1.0] },
    { "NAME": "skyBottom", "TYPE": "color", "DEFAULT": [0.85, 0.75, 0.6, 1.0] },
    { "NAME": "birdColor", "TYPE": "color", "DEFAULT": [0.05, 0.05, 0.1, 1.0] },
    { "NAME": "bgImage", "TYPE": "image" },
    { "NAME": "audioReactive", "TYPE": "bool", "DEFAULT": false }
  ]
}*/

const float PI = 3.14159265;

float hash(float n) { return fract(sin(n) * 43758.5453); }

float noise(float x) {
    float i = floor(x);
    float f = fract(x);
    f = f * f * (3.0 - 2.0 * f);
    return mix(hash(i), hash(i + 1.0), f);
}

// ─── Triangle hit test (hard edges) ───

float cross2(vec2 a, vec2 b) { return a.x * b.y - a.y * b.x; }

float inTri(vec2 p, vec2 a, vec2 b, vec2 c) {
    float d1 = cross2(b - a, p - a);
    float d2 = cross2(c - b, p - b);
    float d3 = cross2(a - c, p - c);
    bool hasNeg = (d1 < 0.0) || (d2 < 0.0) || (d3 < 0.0);
    bool hasPos = (d1 > 0.0) || (d2 > 0.0) || (d3 > 0.0);
    return (hasNeg && hasPos) ? 0.0 : 1.0;
}

// ─── Flock path ───

vec2 flockCenter(float id, float t) {
    float s = id * 17.3;
    return vec2(
        noise(s + t * 0.13) * 0.7 + noise(s + 100.0 + t * 0.31) * 0.3,
        noise(s + 50.0 + t * 0.11) * 0.55 + noise(s + 150.0 + t * 0.23) * 0.2 + 0.15
    );
}

vec2 flockHeading(float id, float t) {
    vec2 d = flockCenter(id, t + 0.05) - flockCenter(id, t - 0.05);
    float len = length(d);
    return len > 0.0001 ? d / len : vec2(1.0, 0.0);
}

// ─── Bird: two triangular wing planes meeting at a spine ───
// Like a simple paper plane / open book seen from slightly above.
// Left wing = triangle, right wing = triangle, sharing a center spine edge.
// Flap angle tilts each wing, foreshortening its width in our top-down view.
//
// Returns: x = hit (0 or 1), y = shade (0..1, different per wing face)

vec2 birdHit(vec2 p, vec2 pos, vec2 heading, float sz, float flapPhase) {
    vec2 d = p - pos;
    vec2 fwd = heading;
    vec2 rt = vec2(-fwd.y, fwd.x);
    float lx = dot(d, rt);
    float ly = dot(d, fwd);

    float s = sz * 0.004;
    lx /= s;
    ly /= s;

    // Flap: wings tilt up/down. From above, lateral spread foreshortens.
    float flap = sin(flapPhase);
    // Wing spread = full when flat (flap=0), narrower at extremes
    float wingW = 2.5 * (0.55 + 0.45 * cos(flapPhase));

    // Each wing is a triangle:
    //   Spine runs from nose (0, 1.8) to tail (0, -1.2)
    //   Wing tip at (±wingW, 0.3)
    //
    // Left wing: nose → tip → tail
    vec2 nose = vec2(0.0, 1.8);
    vec2 tail = vec2(0.0, -1.2);
    vec2 ltip = vec2(-wingW, 0.3);
    vec2 rtip = vec2( wingW, 0.3);

    float hitL = inTri(vec2(lx, ly), nose, ltip, tail);
    float hitR = inTri(vec2(lx, ly), nose, tail, rtip);

    // Shade: simulate light from above hitting angled wing planes
    // When wing tilts up, it catches more light; down = shadow
    float shadeL = 0.55 + flap * 0.2;
    float shadeR = 0.55 - flap * 0.2;

    float shade = hitL > 0.5 ? shadeL : (hitR > 0.5 ? shadeR : 0.0);
    float hit = max(hitL, hitR);

    return vec2(hit, shade);
}

// ─── Main ───

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    vec2 p = vec2(uv.x * aspect, uv.y);

    float t = TIME * speed;

    // Background: use bgImage (NDI/media) if bound, otherwise sky gradient
    vec4 bgSample = IMG_NORM_PIXEL(bgImage, uv);
    bool hasBg = (bgSample.r + bgSample.g + bgSample.b + bgSample.a) > 0.001;
    vec3 sky;
    if (hasBg) {
        sky = bgSample.rgb;
    } else {
        sky = mix(skyBottom.rgb, skyTop.rgb, uv.y);
        sky += vec3(0.12, 0.1, 0.08) * exp(-8.0 * (uv.y - 0.35) * (uv.y - 0.35));
    }

    vec3 col = sky;

    int nFlocks = int(flockCount);
    int nBirds = int(birdsPerFlock);

    for (int fi = 0; fi < 8; fi++) {
        if (fi >= nFlocks) break;
        float fid = float(fi);

        float fd = hash(fid * 7.7 + 0.5);
        fd = mix(0.15, 1.0, fd * depth + (1.0 - depth) * 0.5);

        float lScale = mix(1.8, 0.25, fd);
        float lSpeed = mix(1.3, 0.6, fd);
        float lAlpha = mix(1.0, 0.25, fd);

        vec2 center = flockCenter(fid, t * lSpeed);
        vec2 heading = flockHeading(fid, t * lSpeed);

        for (int bi = 0; bi < 120; bi++) {
            if (bi >= nBirds) break;
            float bid = float(bi);
            float seed = fid * 173.0 + bid * 7.1;

            // Per-bird size variation
            float sizeRand = hash(seed + 5.0);
            float individualSize = birdSize * (1.0 - sizeVariation * 0.6 + sizeRand * sizeVariation * 1.2);

            // Formation position relative to leader
            float lag = (hash(seed + 1.0) - 0.3) * spread * 0.22;
            float lateral = (hash(seed + 2.0) - 0.5) * spread * 0.18;

            // Wandering
            float wx = noise(seed + t * lSpeed * 0.7) * 0.03 * spread;
            float wy = noise(seed + 50.0 + t * lSpeed * 0.5) * 0.02 * spread;

            vec2 rt = vec2(-heading.y, heading.x);
            vec2 bPos = center + heading * lag + rt * lateral + vec2(wx, wy);
            vec2 sPos = vec2(bPos.x * aspect, bPos.y);

            // Heading variation
            float hNoise = (noise(seed + 200.0 + t * lSpeed * 0.9) - 0.5) * 0.3;
            float angle = atan(heading.y, heading.x) + hNoise;
            vec2 bHead = vec2(cos(angle), sin(angle));

            // Flap (smaller birds flap faster)
            float fPhase = t * flapSpeed * (5.0 + 3.0 * (1.0 - sizeRand)) + bid * 1.7 + fid * 3.1;
            if (audioReactive) fPhase += audioBass * 4.0;

            vec2 result = birdHit(p, sPos, bHead, individualSize * lScale, fPhase);

            if (result.x > 0.5) {
                vec3 bc = birdColor.rgb * (0.5 + result.y * 0.9);
                bc = mix(bc, sky * 0.6 + 0.2, fd * 0.65);
                col = bc;
            }
        }
    }

    gl_FragColor = vec4(col, 1.0);
}

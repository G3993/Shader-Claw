/*
{
  "CATEGORIES": ["Generator", "Nature"],
  "DESCRIPTION": "Milky Way galaxy — dense particle starfield with nebula clouds and galactic core",
  "INPUTS": [
    { "NAME": "bandTilt", "TYPE": "float", "MIN": -0.5, "MAX": 0.5, "DEFAULT": 0.15 },
    { "NAME": "bandWidth", "TYPE": "float", "MIN": 0.1, "MAX": 1.0, "DEFAULT": 0.35 },
    { "NAME": "starDensity", "TYPE": "float", "MIN": 0.1, "MAX": 1.0, "DEFAULT": 0.7 },
    { "NAME": "starBrightness", "TYPE": "float", "MIN": 0.5, "MAX": 3.0, "DEFAULT": 1.2 },
    { "NAME": "nebulaIntensity", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "coreBrightness", "TYPE": "float", "MIN": 0.0, "MAX": 3.0, "DEFAULT": 1.5 },
    { "NAME": "corePos", "TYPE": "point2D", "MIN": [0.0, 0.0], "MAX": [1.0, 1.0], "DEFAULT": [0.5, 0.5] },
    { "NAME": "warmColor", "TYPE": "color", "DEFAULT": [1.0, 0.85, 0.6, 1.0] },
    { "NAME": "coolColor", "TYPE": "color", "DEFAULT": [0.5, 0.6, 1.0, 1.0] },
    { "NAME": "nebulaColor1", "TYPE": "color", "DEFAULT": [0.4, 0.2, 0.6, 1.0] },
    { "NAME": "nebulaColor2", "TYPE": "color", "DEFAULT": [0.15, 0.3, 0.5, 1.0] },
    { "NAME": "starCount", "TYPE": "float", "MIN": 1.0, "MAX": 6.0, "DEFAULT": 6.0 },
    { "NAME": "showCore", "TYPE": "bool", "DEFAULT": true },
    { "NAME": "gradientOverlay", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "gradientTop", "TYPE": "color", "DEFAULT": [0.0, 0.0, 0.1, 1.0] },
    { "NAME": "gradientBottom", "TYPE": "color", "DEFAULT": [0.05, 0.0, 0.15, 1.0] },
    { "NAME": "driftSpeed", "TYPE": "float", "MIN": 0.0, "MAX": 0.5, "DEFAULT": 0.03 },
    { "NAME": "twinkleSpeed", "TYPE": "float", "MIN": 0.0, "MAX": 5.0, "DEFAULT": 1.5 }
  ]
}
*/

// --- Hash / noise ---

float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453123);
}

float hash3(vec3 p) {
    return fract(sin(dot(p, vec3(127.1, 311.7, 74.7))) * 43758.5453123);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);
    return mix(
        mix(hash(i), hash(i + vec2(1.0, 0.0)), f.x),
        mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), f.x),
        f.y
    );
}

float fbm(vec2 p, int octaves) {
    float v = 0.0, a = 0.5;
    mat2 rot = mat2(0.8, 0.6, -0.6, 0.8);
    for (int i = 0; i < 6; i++) {
        if (i >= octaves) break;
        v += a * noise(p);
        p = rot * p * 2.0 + 100.0;
        a *= 0.5;
    }
    return v;
}

// --- Star particle layer ---
// Returns: x = brightness, y = color temperature (0=warm, 1=cool)

vec2 starLayer(vec2 uv, float scale, float density, float seed) {
    vec2 grid = uv * scale;
    vec2 id = floor(grid);
    vec2 gv = fract(grid) - 0.5;

    float best = 0.0;
    float temp = 0.0;

    // Check 3x3 neighborhood for nearby stars
    for (float y = -1.0; y <= 1.0; y++) {
        for (float x = -1.0; x <= 1.0; x++) {
            vec2 neighbor = vec2(x, y);
            vec2 cellId = id + neighbor;
            float h = hash(cellId + seed);

            // Star exists?
            if (h > (1.0 - density * 0.4)) {
                // Random position within cell
                vec2 starPos = vec2(
                    hash(cellId * 1.31 + seed + 10.0),
                    hash(cellId * 2.73 + seed + 20.0)
                ) - 0.5;

                vec2 diff = gv - neighbor - starPos;
                float d = length(diff);

                // Star size varies
                float sizeHash = hash(cellId * 3.17 + seed + 30.0);
                float size = 0.01 + sizeHash * 0.03;

                // Point star with sharp core + soft halo
                float core = exp(-d * d / (size * size * 0.3)) * 1.0;
                float halo = exp(-d * d / (size * size * 3.0)) * 0.3;
                float star = core + halo;

                // Brightness variation
                float brightnessVar = 0.3 + hash(cellId * 4.91 + seed + 40.0) * 0.7;
                star *= brightnessVar;

                // Twinkle
                float twinklePhase = hash(cellId * 5.37 + seed + 50.0) * 6.28;
                float twinkleFreq = 0.5 + hash(cellId * 6.13 + seed + 60.0) * 3.0;
                // Principle 6: Slow In/Slow Out — stars linger bright, dip quickly
                float rawTwinkle = sin(TIME * twinkleSpeed * twinkleFreq + twinklePhase);
                float twinkle = 0.7 + 0.3 * sign(rawTwinkle) * pow(abs(rawTwinkle), 0.5);
                star *= twinkle;

                if (star > best) {
                    best = star;
                    temp = hash(cellId * 7.43 + seed + 70.0);
                }
            }
        }
    }

    return vec2(best, temp);
}

// --- Galactic band mask ---

float bandMask(vec2 uv, float tilt, float width) {
    // Tilted band across the screen
    float y = uv.y - 0.5 + (uv.x - 0.5) * tilt;
    // Gaussian falloff
    float band = exp(-y * y / (width * width * 0.5));
    // Add some noise to the edges for organic feel
    float edgeNoise = fbm(uv * 8.0 + TIME * driftSpeed * 0.5, 4) * 0.15;
    band *= (1.0 + edgeNoise);
    return clamp(band, 0.0, 1.0);
}

// --- Nebula / dust clouds ---

vec3 nebulaClouds(vec2 uv, float mask) {
    vec2 drift = vec2(TIME * driftSpeed * 0.3, TIME * driftSpeed * 0.1);

    // Large-scale nebula structure
    // Principle 5: Overlapping Action — each nebula layer drifts at its own pace and direction
    float n1 = fbm(uv * 3.0 + drift * vec2(1.0, 0.6), 6);
    float n2 = fbm(uv * 5.0 - drift * vec2(1.3, 0.4) + 50.0, 5);
    float n3 = fbm(uv * 8.0 + drift * vec2(0.5, 1.2) + 100.0, 4);

    // Shape the nebula into patches
    float patch1 = smoothstep(0.35, 0.65, n1);
    float patch2 = smoothstep(0.4, 0.7, n2);
    float patch3 = smoothstep(0.45, 0.7, n3);

    // Color the nebula patches
    vec3 neb = vec3(0.0);
    neb += nebulaColor1.rgb * patch1 * 0.5;
    neb += nebulaColor2.rgb * patch2 * 0.4;
    neb += mix(nebulaColor1.rgb, nebulaColor2.rgb, 0.5) * patch3 * 0.3;

    // Warm dust lanes
    float dust = fbm(uv * 12.0 + drift * 2.0 + 200.0, 4);
    dust = smoothstep(0.4, 0.55, dust) * 0.15;
    neb += warmColor.rgb * dust;

    return neb * mask * nebulaIntensity;
}

// --- Dark dust lanes (absorption) ---

float dustLanes(vec2 uv) {
    vec2 drift = vec2(TIME * driftSpeed * 0.2, 0.0);
    float d = fbm(uv * 6.0 + drift + 300.0, 5);
    // Narrow dark filaments
    float lanes = smoothstep(0.48, 0.52, d) * 0.5;
    return 1.0 - lanes * 0.4;
}

// --- Galactic core glow ---

vec3 coreGlow(vec2 uv) {
    vec2 center = corePos;
    vec2 diff = uv - center;
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    diff.x *= aspect;

    float d = length(diff);

    // Elongated core along the band
    float angle = atan(diff.y, diff.x);
    float stretch = 1.0 + 0.5 * abs(cos(angle - bandTilt));
    d *= stretch;

    // Layered glow
    float glow = 0.0;
    glow += 0.8 * exp(-d * d / 0.02);
    glow += 0.4 * exp(-d * d / 0.08);
    glow += 0.15 * exp(-d * d / 0.3);

    vec3 coreColor = mix(warmColor.rgb, vec3(1.0), 0.3);
    // Principle 2: Anticipation — core pulses like a heartbeat, swells before release
    float pulse = 1.0 + 0.06 * pow(sin(TIME * 0.4), 2.0) + 0.02 * sin(TIME * 1.1);
    return coreColor * glow * coreBrightness * pulse;
}

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    float aspect = RENDERSIZE.x / RENDERSIZE.y;

    // Slow drift
    vec2 drift = vec2(TIME * driftSpeed, TIME * driftSpeed * 0.3);

    // Deep space background
    vec3 col = vec3(0.005, 0.005, 0.015);

    // Galactic band mask
    float mask = bandMask(uv, bandTilt, bandWidth);

    // --- Star layers (starCount controls how many layers render) ---

    // Layer 1: Background faint stars (everywhere, sparse)
    vec2 bgUV = uv * vec2(aspect, 1.0) + drift * 0.1;
    vec2 s0 = starLayer(bgUV, 60.0, starDensity * 0.3, 0.0);
    col += mix(warmColor.rgb, coolColor.rgb, s0.y) * s0.x * 0.3 * starBrightness;

    // Layer 2: Mid-field stars (denser in the band)
    if (starCount > 1.5) {
        vec2 midUV = uv * vec2(aspect, 1.0) + drift * 0.2;
        vec2 s1 = starLayer(midUV, 120.0, starDensity * 0.5 * (0.3 + mask * 0.7), 100.0);
        col += mix(warmColor.rgb, coolColor.rgb, s1.y) * s1.x * 0.5 * starBrightness;
    }

    // Layer 3: Dense milky way particles (concentrated in band)
    if (starCount > 2.5) {
        vec2 denseUV = uv * vec2(aspect, 1.0) + drift * 0.3;
        vec2 s2 = starLayer(denseUV, 250.0, starDensity * 0.8 * mask, 200.0);
        col += mix(warmColor.rgb, coolColor.rgb, s2.y) * s2.x * 0.6 * starBrightness;
    }

    // Layer 4: Ultra-fine particle dust (the "milky" texture)
    if (starCount > 3.5) {
        vec2 fineUV = uv * vec2(aspect, 1.0) + drift * 0.4;
        vec2 s3 = starLayer(fineUV, 500.0, starDensity * mask, 300.0);
        col += mix(warmColor.rgb, vec3(1.0), 0.5) * s3.x * 0.3 * starBrightness;
    }

    // Layer 5: Extra ultra-fine layer for milky glow
    if (starCount > 4.5) {
        vec2 dustUV = uv * vec2(aspect, 1.0) + drift * 0.5;
        vec2 s4 = starLayer(dustUV, 900.0, starDensity * mask, 400.0);
        col += mix(warmColor.rgb, coolColor.rgb, 0.5) * s4.x * 0.2 * starBrightness;
    }

    // Layer 6: Bright foreground stars (sparse, large, with diffraction spikes)
    if (starCount > 5.5) {
        vec2 fgUV = uv * vec2(aspect, 1.0) + drift * 0.05;
        vec2 s5 = starLayer(fgUV, 30.0, starDensity * 0.15, 500.0);
        vec3 fgStars = mix(warmColor.rgb, coolColor.rgb, s5.y) * s5.x * 1.2 * starBrightness;
        if (s5.x > 0.3) {
            vec2 spUV = fgUV * 30.0;
            vec2 spGV = fract(spUV) - 0.5;
            float spikeH = exp(-abs(spGV.x) * 40.0) * exp(-spGV.y * spGV.y * 8.0);
            float spikeV = exp(-abs(spGV.y) * 40.0) * exp(-spGV.x * spGV.x * 8.0);
            fgStars += mix(warmColor.rgb, coolColor.rgb, s5.y) * (spikeH + spikeV) * s5.x * 0.3;
        }
        col += fgStars;
    }

    // --- Nebula clouds ---
    col += nebulaClouds(uv, mask);

    // --- Dark dust lanes ---
    col *= dustLanes(uv * vec2(aspect, 1.0) + drift * 0.15);

    // --- Diffuse milky glow (unresolved stars) ---
    float milkyGlow = mask * mask;
    float glowNoise = fbm(uv * 4.0 + drift * 0.2, 4);
    milkyGlow *= (0.7 + glowNoise * 0.6);
    col += mix(warmColor.rgb, coolColor.rgb, 0.4) * milkyGlow * 0.08 * starBrightness;

    // --- Galactic core (toggleable) ---
    if (showCore) {
        col += coreGlow(uv);
    }

    // Subtle vignette
    float vig = 1.0 - 0.25 * length((uv - 0.5) * 1.5);
    col *= vig;

    // Tone mapping — prevent blowout
    col = col / (1.0 + col * 0.3);

    // --- Gradient overlay mask ---
    if (gradientOverlay > 0.001) {
        vec3 grad = mix(gradientBottom.rgb, gradientTop.rgb, uv.y);
        col = mix(col, grad, gradientOverlay);
    }

    // Principle 8: Secondary Action — subtle color temperature drift adds life
    float tempShift = sin(TIME * 0.15) * 0.02;
    col.r += tempShift;
    col.b -= tempShift;

    gl_FragColor = vec4(col, 1.0);
}
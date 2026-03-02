/*{
    "DESCRIPTION": "Lava lamp — glossy metaballs rising and merging in viscous fluid",
    "CREDIT": "ShaderClaw",
    "CATEGORIES": ["Generator"],
    "INPUTS": [
        { "NAME": "blobCount", "LABEL": "Blobs", "TYPE": "float", "DEFAULT": 7.0, "MIN": 3.0, "MAX": 12.0 },
        { "NAME": "blobSize", "LABEL": "Blob Size", "TYPE": "float", "DEFAULT": 0.35, "MIN": 0.1, "MAX": 0.8 },
        { "NAME": "flowSpeed", "LABEL": "Flow Speed", "TYPE": "float", "DEFAULT": 0.4, "MIN": 0.0, "MAX": 2.0 },
        { "NAME": "glossiness", "LABEL": "Gloss", "TYPE": "float", "DEFAULT": 0.7, "MIN": 0.0, "MAX": 1.0 },
        { "NAME": "waxColor", "LABEL": "Wax Color", "TYPE": "color", "DEFAULT": [0.91, 0.25, 0.34, 1.0] },
        { "NAME": "liquidColor", "LABEL": "Liquid Color", "TYPE": "color", "DEFAULT": [0.02, 0.01, 0.06, 1.0] },
        { "NAME": "glowColor", "LABEL": "Glow Color", "TYPE": "color", "DEFAULT": [1.0, 0.5, 0.2, 1.0] }
    ]
}*/

// Hash functions for pseudo-random blob behavior
float hash(float n) { return fract(sin(n) * 43758.5453); }
vec2 hash2(float n) { return vec2(hash(n), hash(n + 7.13)); }

// Smooth minimum for metaball merging
float smin(float a, float b, float k) {
    float h = clamp(0.5 + 0.5 * (b - a) / k, 0.0, 1.0);
    return mix(b, a, h) - k * h * (1.0 - h);
}

// Blob position — lava lamp physics: rise slowly, spread at top, sink along edges
vec2 blobPos(float id, float t) {
    vec2 seed = hash2(id * 31.7);

    // Vertical cycle: slow rise, pause at top, sink along walls
    float period = 8.0 + seed.x * 6.0; // each blob has unique period
    float phase = seed.y * 6.28 + id * 1.7;
    float cycle = mod(t / period + seed.y, 1.0);

    // Y: smooth rise with pause at top and bottom
    float y = smoothstep(0.0, 0.5, cycle) * (1.0 - smoothstep(0.5, 1.0, cycle));
    y = mix(0.1, 0.9, y);
    // Add gentle wobble
    y += sin(t * 0.5 + phase) * 0.05;

    // X: drift with sinusoidal sway, wider at top
    float spread = 0.15 + 0.1 * y; // wider movement near top
    float x = 0.5 + sin(t * 0.3 + phase) * spread;
    x += cos(t * 0.17 + id * 2.3) * 0.08;

    return vec2(x, y);
}

// Blob radius — pulses with audio
float blobRadius(float id, float baseSize) {
    float seed = hash(id * 17.3);
    float size = baseSize * (0.6 + seed * 0.8);
    // Audio reactivity: bass expands blobs
    size *= 1.0 + audioBass * 0.5 + audioLevel * 0.3;
    // Gentle breathing
    size *= 0.95 + 0.05 * sin(TIME * 1.2 + id * 3.0);
    return size;
}

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    vec2 p = vec2(uv.x * aspect, uv.y);
    float t = TIME * flowSpeed;

    int N = int(blobCount);

    // Compute metaball field
    float field = 1e5;
    float totalWeight = 0.0;
    float colorWeight = 0.0;
    vec3 nearestId = vec3(0.0);
    float closestDist = 1e5;

    for (int i = 0; i < 12; i++) {
        if (i >= N) break;
        float fi = float(i);

        vec2 bpos = blobPos(fi, t);
        bpos.x *= aspect; // match aspect correction
        float brad = blobRadius(fi, blobSize) * 0.15;

        float d = length(p - bpos) - brad;

        // Track nearest blob for coloring
        if (d < closestDist) {
            closestDist = d;
            nearestId = vec3(hash(fi * 5.1), hash(fi * 11.3), hash(fi * 23.7));
        }

        // Smooth union of all metaballs
        float k = 0.12 * blobSize; // merge softness
        field = smin(field, d, k);

        // Accumulate potential for glow
        float pot = brad / max(length(p - bpos), 0.001);
        totalWeight += pot;
    }

    // Mouse interaction: attract nearby blobs
    if (mouseDown > 0.5) {
        vec2 mpos = vec2(mousePos.x * aspect, mousePos.y);
        float md = length(p - mpos) - 0.08;
        field = smin(field, md, 0.15);
        float mpot = 0.1 / max(length(p - mpos), 0.001);
        totalWeight += mpot;
    }

    // Wax vs liquid
    float isWax = 1.0 - smoothstep(-0.01, 0.01, field);
    float edge = 1.0 - smoothstep(-0.02, 0.0, field); // sharper edge
    float glow = smoothstep(0.3, 0.0, field) * 0.5; // soft outer glow

    // Fake 3D shading on wax blobs
    // Normal approximation from field gradient
    float eps = 0.003;
    float fx = field; // center value (reuse)
    // Approximate gradient for specular highlight
    vec2 lightDir = normalize(vec2(0.3, 0.6));
    float highlight = 0.0;
    if (isWax > 0.01) {
        // Fresnel-like rim based on distance to center of nearest blob
        float rimFactor = smoothstep(0.0, 0.8, closestDist / (blobSize * 0.15) + 0.3);
        // Specular: dot product with light
        float spec = pow(max(1.0 - abs(closestDist) / (blobSize * 0.12), 0.0), 8.0 + glossiness * 40.0);
        highlight = spec * glossiness;
        // Add a subtle moving highlight for that liquid glass feel
        float movingSpec = pow(max(0.0, sin(uv.x * 8.0 + uv.y * 5.0 + TIME * 0.3) * 0.5 + 0.5), 20.0);
        highlight += movingSpec * glossiness * 0.3;
    }

    // Color composition
    vec3 wax = waxColor.rgb;
    // Slight color variation per blob
    wax = mix(wax, wax * (0.8 + 0.4 * nearestId), 0.3);
    // Audio shifts hue slightly
    wax += vec3(audioBass * 0.1, -audioMid * 0.05, audioHigh * 0.08);

    vec3 liquid = liquidColor.rgb;
    // Subtle depth gradient in liquid
    liquid += vec3(0.01, 0.005, 0.02) * uv.y;

    // Combine
    vec3 col = mix(liquid, wax, isWax);
    // Add glossy highlight on wax
    col += highlight * vec3(1.0, 0.95, 0.9);
    // Add glow around wax edges
    col += glow * glowColor.rgb * (0.3 + audioBass * 0.4);
    // Audio brightness pulse
    col *= 1.0 + audioLevel * 0.15;

    // Vignette — lava lamp glass tube effect
    float vig = 1.0 - pow(abs(uv.x - 0.5) * 1.6, 4.0);
    vig *= 1.0 - pow(abs(uv.y - 0.5) * 1.3, 6.0);
    col *= 0.7 + 0.3 * vig;

    gl_FragColor = vec4(col, 1.0);
}

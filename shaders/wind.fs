/*{
  "DESCRIPTION": "Wind — minimal gradient movement with soft drifting color fields",
  "CATEGORIES": ["Generator"],
  "INPUTS": [
    { "NAME": "speed", "LABEL": "Speed", "TYPE": "float", "MIN": 0.0, "MAX": 2.0, "DEFAULT": 1.56 },
    { "NAME": "scale", "LABEL": "Scale", "TYPE": "float", "MIN": 0.5, "MAX": 8.0, "DEFAULT": 2.5 },
    { "NAME": "softness", "LABEL": "Softness", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.7 },
    { "NAME": "drift", "LABEL": "Drift", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "colorA", "LABEL": "Color A", "TYPE": "color", "DEFAULT": [0.0, 0.0, 0.0, 1.0] },
    { "NAME": "colorB", "LABEL": "Color B", "TYPE": "color", "DEFAULT": [0.25, 0.25, 0.25, 1.0] },
    { "NAME": "colorC", "LABEL": "Color C", "TYPE": "color", "DEFAULT": [0.0, 0.0, 0.0, 1.0] },
    { "NAME": "direction", "LABEL": "Direction", "TYPE": "long", "VALUES": [0,1,2,3], "LABELS": ["Right","Left","Up","Down"], "DEFAULT": 0 }
  ]
}*/

// Smooth noise without harsh edges
float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f); // smoothstep
    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));
    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

// Layered smooth noise
float fbm(vec2 p, int octaves) {
    float val = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    for (int i = 0; i < 5; i++) {
        if (i >= octaves) break;
        val += amp * noise(p * freq);
        freq *= 2.0;
        amp *= 0.5;
    }
    return val;
}

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    float t = TIME * speed;

    // Wind direction vector
    vec2 windDir;
    if (direction < 0.5) windDir = vec2(-1.0, 0.0);
    else if (direction < 1.5) windDir = vec2(1.0, 0.0);
    else if (direction < 2.5) windDir = vec2(0.0, 1.0);
    else windDir = vec2(0.0, -1.0);

    // Drift offset perpendicular to wind
    vec2 perpDir = vec2(-windDir.y, windDir.x);

    // Coordinate space with wind movement
    vec2 p = uv * scale;
    p += windDir * t * 0.6;
    p += perpDir * sin(t * 0.3) * drift * 0.4;

    // Three layered gradient fields moving at different rates
    float n1 = fbm(p * 0.8 + vec2(t * 0.1, 0.0), 3);
    float n2 = fbm(p * 1.2 + vec2(0.0, t * 0.15) + 5.0, 3);
    float n3 = fbm(p * 0.5 - vec2(t * 0.08, t * 0.05) + 10.0, 2);

    // Smooth blend factors
    float blend1 = smoothstep(0.2 - softness * 0.2, 0.6 + softness * 0.3, n1);
    float blend2 = smoothstep(0.3 - softness * 0.2, 0.7 + softness * 0.2, n2);

    // Mix three colors through smooth gradient transitions
    vec3 col = mix(colorA.rgb, colorB.rgb, blend1);
    col = mix(col, colorC.rgb, blend2 * n3);

    // Subtle luminance variation for depth
    float luma = fbm(p * 2.0 + vec2(t * 0.2), 2);
    col += (luma - 0.5) * 0.04;

    gl_FragColor = vec4(col, 1.0);
}

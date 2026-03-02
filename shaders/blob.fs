/*{
    "DESCRIPTION": "Solido Fluido 4D Monocromatico",
    "CREDIT": "Giulio Gianni",
    "ISFVSN": "2",
    "CATEGORIES": ["Generator", "3D"],
    "INPUTS": [
        {
            "NAME": "dimensione",
            "LABEL": "Size",
            "TYPE": "float",
            "MIN": 0.5,
            "MAX": 3.0,
            "DEFAULT": 1.8
        },
        {
            "NAME": "intensita_deformazione",
            "LABEL": "Deformation",
            "TYPE": "float",
            "MIN": 0.0,
            "MAX": 2.0,
            "DEFAULT": 0.4
        },
        {
            "NAME": "velocita",
            "LABEL": "Speed",
            "TYPE": "float",
            "MIN": 0.0,
            "MAX": 2.0,
            "DEFAULT": 1.0
        },
        {
            "NAME": "tonalita",
            "LABEL": "Hue",
            "TYPE": "float",
            "MIN": 0.0,
            "MAX": 1.0,
            "DEFAULT": 0.0
        },
        {
            "NAME": "opacita_fondo",
            "LABEL": "BG Opacity",
            "TYPE": "float",
            "MIN": 0.0,
            "MAX": 1.0,
            "DEFAULT": 1.0
        }
    ]
}*/

mat2 rot(float a) {
    float s = sin(a), c = cos(a);
    return mat2(c, -s, s, c);
}

vec3 hueToRGB(float h) {
    vec3 rgb = clamp(abs(mod(h * 6.0 + vec3(0.0, 4.0, 2.0), 6.0) - 3.0) - 1.0, 0.0, 1.0);
    return rgb;
}

float map(vec3 p) {
    float t = TIME * velocita;
    p.xy *= rot(t * 0.5 + p.z * 0.2);
    p.xz *= rot(t * 0.3);
    float sfera = length(p) - dimensione * (1.0 + audioLevel * 0.5);
    float dist = sin(p.x * 2.0 + t) * sin(p.y * 2.0 + t) * sin(p.z * 2.0 + t);
    return sfera + dist * intensita_deformazione * (1.0 + audioBass * 3.0);
}

void main() {
    vec2 uv = (gl_FragCoord.xy - 0.5 * RENDERSIZE.xy) / RENDERSIZE.y;
    vec3 ro = vec3(0, 0, -5);
    vec3 rd = normalize(vec3(uv, 1));

    float t = 0.0;
    for (int i = 0; i < 80; i++) {
        vec3 p = ro + rd * t;
        float d = map(p);
        t += d;
        if (d < 0.001 || t > 20.0) break;
    }

    vec3 col = vec3(0.1) * opacita_fondo;

    if (t < 20.0) {
        vec3 p = ro + rd * t;
        vec2 e = vec2(0.01, 0);
        vec3 n = normalize(map(p) - vec3(map(p-e.xyy), map(p-e.yxy), map(p-e.yyx)));

        float diff = max(dot(n, vec3(1, 1, -1)), 0.1);

        float gradienteInterno = p.y * 0.2 + p.x * 0.1;
        vec3 col1 = hueToRGB(tonalita);
        vec3 col2 = hueToRGB(tonalita + 0.15);
        vec3 baseCol = mix(col1, col2, gradienteInterno + 0.5);

        col = baseCol * diff;
    }

    gl_FragColor = vec4(col, 1.0);
}

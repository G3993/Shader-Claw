/*{
    "DESCRIPTION": "Ray marched flower — morphing organic form with polar coordinate warping",
    "CREDIT": "TLC123 / Inigo Quilez (Shadertoy), adapted for ShaderClaw",
    "CATEGORIES": ["Generator"],
    "INPUTS": [
        { "NAME": "inputImage", "LABEL": "Texture", "TYPE": "image" },
        {
            "NAME": "texMix",
            "TYPE": "float",
            "LABEL": "Texture Mix",
            "DEFAULT": 0.0,
            "MIN": 0.0,
            "MAX": 1.0
        },
        {
            "NAME": "flowerColor",
            "TYPE": "color",
            "LABEL": "Color",
            "DEFAULT": [0.7, 0.4, 0.9, 1.0]
        },
        {
            "NAME": "trigger",
            "TYPE": "float",
            "LABEL": "Bloom",
            "DEFAULT": 0.0,
            "MIN": 0.0,
            "MAX": 20.0
        },
        {
            "NAME": "draaiomas",
            "TYPE": "float",
            "LABEL": "Spin Speed",
            "DEFAULT": 1.0,
            "MIN": 0.0,
            "MAX": 5.0
        },
        {
            "NAME": "camDist",
            "TYPE": "float",
            "LABEL": "Camera Distance",
            "DEFAULT": 5.0,
            "MIN": 2.0,
            "MAX": 15.0
        }
    ]
}*/

float flower(vec3 p, float r) {
    float q = length(p);
    p -= vec3(sin(p.x * 15.1), sin(p.y * 25.1), sin(p.z * 15.0)) * 0.01;
    vec3 n = normalize(p);
    q = length(p);
    float rho = atan(length(vec2(n.x, n.z)), n.y) * 20.0 + trigger + q * 15.01;
    float theta = atan(n.x, n.z) * 6.0 + p.y * 3.0 + rho * 1.50;
    return length(p) - (r + sin(theta) * 0.3 * (1.3 - abs(dot(n, vec3(0, 1, 0))))
        + sin(rho - (draaiomas * TIME) * 2.0) * 0.3 * (1.3 - abs(dot(n, vec3(0, 1, 0)))));
}

vec2 map(in vec3 pos) {
    return vec2(flower(pos, 0.750), 15.1);
}

vec2 castRay(in vec3 ro, in vec3 rd) {
    float tmin = 0.5;
    float tmax = 20.0;
    float precis = 0.002;
    float t = tmin;
    float m = -1.0;
    for (int i = 0; i < 200; i++) {
        vec2 res = map(ro + rd * t);
        if (res.x < precis || t > tmax) break;
        t += res.x * 0.1;
        m = res.y;
    }
    if (t > tmax) m = -1.0;
    return vec2(t, m);
}

vec3 calcNormal(in vec3 pos) {
    vec3 eps = vec3(0.001, 0.0, 0.0);
    vec3 nor = vec3(
        map(pos + eps.xyy).x - map(pos - eps.xyy).x,
        map(pos + eps.yxy).x - map(pos - eps.yxy).x,
        map(pos + eps.yyx).x - map(pos - eps.yyx).x);
    return normalize(nor);
}

float calcAO(in vec3 pos, in vec3 nor) {
    float occ = 0.0;
    float sca = 1.0;
    for (int i = 0; i < 5; i++) {
        float hr = 0.05 + 0.12 * float(i) / 4.0;
        vec3 aopos = nor * hr + pos;
        float dd = map(aopos).x;
        occ += -(dd - hr) * sca;
        sca *= 0.95;
    }
    return clamp(1.0 - 3.0 * occ, 0.0, 1.0);
}

// Triplanar texture mapping
vec3 triplanar(vec3 p, vec3 n) {
    vec3 w = abs(n);
    w = w / (w.x + w.y + w.z + 0.001);
    vec3 cx = texture2D(inputImage, p.yz * 0.5 + 0.5).rgb;
    vec3 cy = texture2D(inputImage, p.xz * 0.5 + 0.5).rgb;
    vec3 cz = texture2D(inputImage, p.xy * 0.5 + 0.5).rgb;
    return cx * w.x + cy * w.y + cz * w.z;
}

vec3 render(in vec3 ro, in vec3 rd) {
    vec3 bg = vec3(0.0);
    vec2 res = castRay(ro, rd);
    float t = res.x;
    float m = res.y;
    if (m > -0.5) {
        vec3 pos = ro + t * rd;
        vec3 nor = calcNormal(pos);
        vec3 ref = reflect(rd, nor);

        // Flower surface color from color picker
        vec3 baseCol = flowerColor.rgb * (0.6 + 0.4 * sin(vec3(2.3 - pos.y * 0.5, 2.15 - pos.y * 0.25, -1.30) * (m - 1.0)));

        // Blend with texture if loaded
        vec3 texCol = triplanar(pos, nor);
        vec3 col = mix(baseCol, texCol, texMix);

        // Lighting
        float occ = calcAO(pos, nor);
        vec3 lig = normalize(vec3(-0.6, 0.7, -0.5));
        float dif = clamp(dot(nor, lig), 0.0, 1.0);
        float dom = smoothstep(-0.1, 0.1, ref.y);

        vec3 lin = vec3(0.0);
        lin += 1.20 * dif * vec3(1.00, 0.85, 0.55);
        lin += 0.20 * vec3(0.50, 0.70, 1.00) * occ;
        lin += 0.30 * dom * vec3(0.50, 0.70, 1.00) * occ;
        lin += 0.40 * 0.75 * vec3(1.00) * occ;
        col = col * lin;

        col = mix(col, vec3(0.0), 1.0 - exp(-0.002 * t * t));
        return clamp(col, 0.0, 1.0);
    }
    return bg;
}

mat3 setCamera(in vec3 ro, in vec3 ta, float cr) {
    vec3 cw = normalize(ta - ro);
    vec3 cp = vec3(sin(cr), cos(cr), 0.0);
    vec3 cu = normalize(cross(cw, cp));
    vec3 cv = normalize(cross(cu, cw));
    return mat3(cu, cv, cw);
}

void main() {
    vec2 q = gl_FragCoord.xy / RENDERSIZE.xy;
    vec2 p = -1.0 + 2.0 * q;
    p.x *= RENDERSIZE.x / RENDERSIZE.y;

    // Orbit camera — mouse drags around the flower
    float angle = (mousePos.x * 2.0 - 1.0) * 3.14159;
    float elev = mix(0.5, 3.5, mousePos.y);
    vec3 ta = vec3(0.0);
    vec3 ro = vec3(sin(angle) * camDist, elev, cos(angle) * camDist);
    mat3 ca = setCamera(ro, ta, 0.0);
    vec3 rd = ca * normalize(vec3(p.xy, 3.0));

    vec3 col = render(ro, rd);
    col = pow(col, vec3(0.4545));

    // Alpha = brightness so flower composites over other layers, background is transparent
    float a = dot(col, vec3(0.299, 0.587, 0.114));
    a = smoothstep(0.01, 0.1, a);
    gl_FragColor = vec4(col, a);
}

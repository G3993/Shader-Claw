/*{
    "DESCRIPTION": "Electric arc — simplex noise plasma with glowing discharge line",
    "CREDIT": "Port of Humus Electro demo, simplex noise by Nikita Miropolskiy",
    "CATEGORIES": ["Generator"],
    "INPUTS": [
        { "NAME": "midSize1", "LABEL": "Mid Size 1", "TYPE": "float", "DEFAULT": 0.60, "MIN": 0.0, "MAX": 2.0 },
        { "NAME": "midSize2", "LABEL": "Mid Size 2", "TYPE": "float", "DEFAULT": 0.15, "MIN": 0.0, "MAX": 0.4 },
        { "NAME": "burn", "LABEL": "Burn", "TYPE": "float", "DEFAULT": 0.35, "MIN": 0.0, "MAX": 1.0 },
        { "NAME": "wiggleAmp", "LABEL": "Wiggle Amp", "TYPE": "float", "DEFAULT": 45.0, "MIN": 0.0, "MAX": 100.0 },
        { "NAME": "freak", "LABEL": "Freak", "TYPE": "float", "DEFAULT": 0.5, "MIN": 0.5, "MAX": 10.0 },
        { "NAME": "freak2", "LABEL": "Freak 2", "TYPE": "float", "DEFAULT": 0.55, "MIN": 0.0, "MAX": 1.0 },
        { "NAME": "arcColor", "LABEL": "Color", "TYPE": "color", "DEFAULT": [0.95, 0.95, 0.95, 1.0] }
    ]
}*/

vec3 random3(vec3 c) {
    float j = 4096.0 * sin(dot(c, vec3(17.0, 59.4, 15.0)));
    vec3 r;
    r.z = fract(512.0 * j);
    j *= 0.125;
    r.x = fract(512.0 * j);
    j *= 0.125;
    r.y = fract(512.0 * j);
    return r - freak;
}

float simplex3d(vec3 p) {
    float F3 = 0.3333333;
    float G3 = 0.1666667;
    vec3 s = floor(p + dot(p, vec3(F3)));
    vec3 x = p - s + dot(s, vec3(G3));
    vec3 e = step(vec3(0.0), x - x.yzx);
    vec3 i1 = e * (1.0 - e.zxy);
    vec3 i2 = 1.0 - e.zxy * (1.0 - e);
    vec3 x1 = x - i1 + G3;
    vec3 x2 = x - i2 + 2.0 * G3;
    vec3 x3 = x - 1.0 + 3.0 * G3;
    vec4 w, d;
    w.x = dot(x, x);
    w.y = dot(x1, x1);
    w.z = dot(x2, x2);
    w.w = dot(x3, x3);
    w = max(freak2 - w, 0.0);
    d.x = dot(random3(s), x);
    d.y = dot(random3(s + i1), x1);
    d.z = dot(random3(s + i2), x2);
    d.w = dot(random3(s + 1.0), x3);
    w *= w;
    w *= w;
    d *= w;
    return dot(d, vec4(wiggleAmp));
}

float fbmNoise(vec3 m) {
    return 0.5333333 * simplex3d(m)
         + 0.2666667 * simplex3d(2.0 * m)
         + 0.1333333 * simplex3d(4.0 * m)
         + 0.0666667 * simplex3d(8.0 * m);
}

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    vec2 centered = uv * 2.0 - 1.0;

    // Guitar-string pluck: mouse crossing center amplifies wiggle
    float mouseDist = abs(mousePos.y - 0.5) * 2.0; // 0 at center, 1 at edges
    float pluck = 1.0 - smoothstep(0.0, 0.4, mouseDist); // strong near center, fades out
    float ampBoost = 1.0 + pluck * 3.0 + audioBass * 5.0; // mouse pluck + bass shakes the arc

    vec2 p = gl_FragCoord.xy / RENDERSIZE.x;
    vec3 p3 = vec3(p, TIME * 0.4);

    float intensity = fbmNoise(p3 * 12.0 + 12.0);

    float t = clamp(centered.x * -centered.x * midSize1 + midSize2, 0.0, 1.0);
    float y = abs(intensity * -t * ampBoost + centered.y);

    float g = pow(y, burn * (1.0 - audioLevel * 0.4));

    vec3 col = arcColor.rgb;
    col = col * -g + col;
    col = col * col;
    col = col * col;

    gl_FragColor = vec4(col, 1.0);
}

/*{
    "DESCRIPTION": "Bouncing ember particles on a reflective floor — orbiting camera, cycling colors, mouse orbit",
    "CREDIT": "Dying Universe by Martijn Steinrucken (BigWings), ported for ShaderClaw",
    "CATEGORIES": ["Generator"],
    "INPUTS": [
        { "NAME": "starSize", "LABEL": "Particle Size", "TYPE": "float", "DEFAULT": 0.03, "MIN": 0.005, "MAX": 0.15 },
        { "NAME": "speed", "LABEL": "Speed", "TYPE": "float", "DEFAULT": 1.0, "MIN": 0.0, "MAX": 3.0 },
        { "NAME": "bounceDecay", "LABEL": "Bounce", "TYPE": "float", "DEFAULT": 0.4, "MIN": 0.1, "MAX": 0.9 },
        { "NAME": "spread", "LABEL": "Spread", "TYPE": "float", "DEFAULT": 10.0, "MIN": 1.0, "MAX": 20.0 }
    ]
}*/

#define NUM_STARS 100
#define NUM_BOUNCES 6
#define NUM_ARCS 7
#define saturate(x) clamp(x, 0., 1.)

float DistSqr(vec3 a, vec3 b) { vec3 D = a - b; return dot(D, D); }

const vec3 up = vec3(0., 1., 0.);
const float pi = 3.141592653589793;
float time;

struct ray {
    vec3 o;
    vec3 d;
};

struct camera {
    vec3 p;
    vec3 forward;
    vec3 left;
    vec3 up;
    vec3 center;
    vec3 i;
    ray ray;
    vec3 lookAt;
    float zoom;
};
camera cam;

vec4 COOLCOLOR = vec4(0.);
vec4 HOTCOLOR = vec4(0.);
vec4 MIDCOLOR = vec4(0.);

void CameraSetup(vec2 uv, vec3 position, vec3 lookAt, float zoom) {
    cam.p = position;
    cam.lookAt = lookAt;
    cam.forward = normalize(cam.lookAt - cam.p);
    cam.left = cross(up, cam.forward);
    cam.up = cross(cam.forward, cam.left);
    cam.zoom = zoom;
    cam.center = cam.p + cam.forward * cam.zoom;
    cam.i = cam.center + cam.left * uv.x + cam.up * uv.y;
    cam.ray.o = cam.p;
    cam.ray.d = normalize(cam.i - cam.p);
}

vec4 Noise4(vec4 x) { return fract(sin(x) * 5346.1764) * 2. - 1.; }
float Noise101(float x) { return fract(sin(x) * 5346.1764); }

float PeriodicPulse(float x, float p) {
    return pow((cos(x + sin(x)) + 1.) / 2., p);
}

vec3 ClosestPoint(ray r, vec3 p) {
    return r.o + max(0., dot(p - r.o, r.d)) * r.d;
}

float BounceNorm(float t, float decay) {
    float height = 1.;
    float heights[NUM_ARCS]; heights[0] = 1.;
    float halfDurations[NUM_ARCS]; halfDurations[0] = 1.;
    float halfDuration = 0.5;
    for (int i = 1; i < NUM_ARCS; i++) {
        height *= decay;
        heights[i] = height;
        halfDurations[i] = sqrt(height);
        halfDuration += halfDurations[i];
    }
    t *= halfDuration * 2.;
    float y = 1. - t * t;
    for (int i = 1; i < NUM_ARCS; i++) {
        t -= halfDurations[i - 1] + halfDurations[i];
        y = max(y, heights[i] - t * t);
    }
    return saturate(y);
}

vec3 IntersectPlaneEx(ray r, vec4 plane) {
    vec3 n = plane.xyz;
    vec3 p0 = plane.xyz * plane.w;
    float t = dot(p0 - r.o, n) / dot(r.d, n);
    return r.o + max(0., t) * r.d;
}

vec3 IntersectGroundPlane(ray r) {
    return IntersectPlaneEx(r, vec4(0., 1., 0., 0.));
}

vec4 Star(ray r, float seed) {
    vec4 n = Noise4(vec4(seed, seed + 1., seed + 2., seed + 3.));
    float t = fract(time * 0.1 + seed) * 2.;
    float fade = smoothstep(2., 0.5, t);
    vec4 col = mix(COOLCOLOR, HOTCOLOR, fade);
    float size = (starSize + seed * 0.03) * (1.0 + audioBass * 2.0);
    size *= fade;
    float b = BounceNorm(t, bounceDecay + seed * 0.1) * 7. * (1.0 + audioLevel * 2.0);
    b += size;
    vec3 sparkPos = vec3(n.x * spread, b, n.y * spread);
    vec3 cp = ClosestPoint(r, sparkPos);
    float dist = DistSqr(cp, sparkPos) / (size * size);
    float brightness = 1. / dist;
    col *= brightness;
    return col;
}

vec4 Stars(ray r) {
    vec4 col = vec4(0.);
    float s = 0.;
    for (int i = 0; i < NUM_STARS; i++) {
        s++;
        col += Star(r, Noise101(s));
    }
    return col;
}

vec4 CalcStarPos(int idx) {
    float n = Noise101(float(idx));
    vec4 noise = Noise4(vec4(n, n + 1., n + 2., n + 3.));
    float t = fract(time * 0.1 + n) * 2.;
    float fade = smoothstep(2., 0.5, t);
    float size = starSize + n * 0.03;
    size *= fade;
    float b = BounceNorm(t, bounceDecay + n * 0.1) * 7.;
    b += size;
    vec3 sparkPos = vec3(noise.x * spread, b, noise.y * spread);
    return vec4(sparkPos, fade);
}

vec4 Ground(ray r) {
    vec4 ground = vec4(0.);
    if (r.d.y > 0.) return ground;
    vec3 I = IntersectGroundPlane(r);
    vec3 R = reflect(r.d, up);
    for (int i = 0; i < NUM_STARS; i++) {
        vec4 star = CalcStarPos(i);
        vec3 L = star.xyz - I;
        float dist = length(L);
        L /= dist;
        float lambert = saturate(dot(L, up));
        float light = lambert / dist;
        vec4 col = mix(COOLCOLOR, MIDCOLOR, star.w);
        vec4 diffuseLight = vec4(light) * 0.1 * col;
        ground += diffuseLight * (sin(time) * 0.5 + 0.6);
        // Floor specular reflections
        float spec = pow(saturate(dot(R, L)), 400.);
        float fresnel = 1. - saturate(dot(L, up));
        fresnel = pow(fresnel, 10.);
        vec4 specLight = col * spec / dist;
        specLight *= star.w;
        ground += specLight * 0.5 * fresnel;
    }
    return ground;
}

void main() {
    vec2 uv = (gl_FragCoord.xy / RENDERSIZE.xy) - 0.5;
    uv.y *= RENDERSIZE.y / RENDERSIZE.x;

    time = TIME * 0.4 * speed;

    float t = time * pi * 0.1;

    // Mouse orbit: X rotates camera, Y adjusts height
    t += (mousePos.x - 0.5) * 6.28;
    float mouseHeight = (mousePos.y - 0.5) * 4.0;

    COOLCOLOR = vec4(sin(t), cos(t * 0.23), cos(t * 0.3453), 1.) * 0.5 + 0.5;
    HOTCOLOR = vec4(sin(t * 2.), cos(t * 2. * 0.33), cos(t * 0.3453), 1.) * 0.5 + 0.5;

    float whiteFade = sin(time * 2.) * 0.5 + 0.5;
    HOTCOLOR = mix(HOTCOLOR, vec4(1.), whiteFade);
    MIDCOLOR = (HOTCOLOR + COOLCOLOR) * 0.5;

    float s = sin(t);
    float c = cos(t);
    mat3 rot = mat3(c, 0., s, 0., 1., 0., s, 0., -c);

    float camHeight = mix(3.5, 0.1, PeriodicPulse(time * 0.1, 2.)) + mouseHeight;
    vec3 pos = vec3(0., camHeight, -10.) * rot * (1. + sin(time) * 0.3);

    CameraSetup(uv, pos, vec3(0.), 0.5);

    vec4 result = Ground(cam.ray) + Stars(cam.ray);
    gl_FragColor = vec4(result.rgb, 1.0);
}

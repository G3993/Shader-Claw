/*{
    "DESCRIPTION": "Smooth-blended raymarched shapes — spheres, cube, torus melting together on a reflective plane",
    "CREDIT": "Rrrrichard (Zhehao Li), ported for ShaderClaw",
    "CATEGORIES": ["Generator"],
    "INPUTS": [
        { "NAME": "blendK", "LABEL": "Blend", "TYPE": "float", "DEFAULT": 2.0, "MIN": 0.2, "MAX": 5.0 },
        { "NAME": "animSpeed", "LABEL": "Speed", "TYPE": "float", "DEFAULT": 1.0, "MIN": 0.0, "MAX": 3.0 },
        { "NAME": "col1", "LABEL": "Sphere 1", "TYPE": "color", "DEFAULT": [0.91, 0.25, 0.34, 1.0] },
        { "NAME": "col2", "LABEL": "Sphere 2", "TYPE": "color", "DEFAULT": [0.91, 0.25, 0.34, 1.0] },
        { "NAME": "col3", "LABEL": "Cube", "TYPE": "color", "DEFAULT": [1.0, 1.0, 1.0, 1.0] },
        { "NAME": "col4", "LABEL": "Torus", "TYPE": "color", "DEFAULT": [1.0, 0.0, 0.0, 1.0] }
    ]
}*/

#define MAX_STEPS 100
#define MAX_DIST 100.
#define EPS 0.001

float aTime;
float bk;

vec3 rotateVec(vec3 v, float a, float b, float c) {
    return vec3(
        cos(b)*cos(c)*v.x
            + (sin(a)*sin(b)*cos(c) - cos(a)*sin(c))*v.y
            + (cos(a)*sin(b)*cos(c) + sin(a)*sin(c))*v.z,
        cos(b)*sin(c)*v.x
            + (sin(a)*sin(b)*sin(c) + cos(a)*cos(c))*v.y
            + (cos(a)*sin(b)*sin(c) - sin(a)*cos(c))*v.z,
        -sin(b)*v.x + sin(a)*cos(b)*v.y + cos(a)*cos(b)*v.z
    );
}

float smoothMin(float dstA, float dstB, float k) {
    float h = max(k - abs(dstA - dstB), 0.) / k;
    return min(dstA, dstB) - h*h*h*k / 6.0;
}

float sphereDist(vec3 point, vec4 sphere) {
    return length(point - sphere.xyz) - sphere.w;
}

float cubeDist(vec3 eye, vec3 centre, vec3 size) {
    eye = rotateVec(eye, 0., 0.3*sin(aTime), 0.5*cos(aTime));
    vec3 o = abs(eye - centre) - size;
    float ud = length(max(o, 0.));
    float n = max(max(min(o.x, 0.), min(o.y, 0.)), min(o.z, 0.));
    return ud + n;
}

float torusDist(vec3 eye, vec3 centre, float r1, float r2) {
    eye = rotateVec(eye, 0.2*sin(aTime), 0.02, 0.3*cos(aTime));
    vec2 q = vec2(length((eye - centre).xz) - r1, eye.y - centre.y);
    return length(q) - r2;
}

float GetSceneDistance(vec3 point, out int obj) {
    vec4 sphere = vec4(
        sin(aTime),
        1. + sin(0.5*aTime),
        6. + 3.*cos(aTime),
        0.4 + 0.2*clamp(cos(0.2*aTime), 0., 1.)
    );
    vec4 sphere2 = vec4(
        2.5*cos(aTime),
        1. + 0.5*sin(0.5*aTime),
        6. + 2.*sin(aTime),
        0.5 + 0.2*clamp(sin(0.2*aTime), 0., 1.)
    );
    vec3 cube_centre = vec3(2.*sin(aTime), 1.+sin(aTime), 10.+2.*sin(aTime));
    vec3 cube_size = vec3(1.);
    vec3 torus_centre = vec3(1.+0.2*cos(aTime), 0.7+0.2*sin(aTime), 7.+sin(aTime));

    float sphere_dist = sphereDist(point, sphere);
    float sphere2_dist = sphereDist(point, sphere2);
    float cube_dist = cubeDist(point, cube_centre, cube_size);
    float torus_dist = torusDist(point, torus_centre, 0.5, 0.2);
    float plane_dist = abs(point.y + 1.);

    float d =
        smoothMin(
        smoothMin(
        smoothMin(
        smoothMin(sphere_dist, plane_dist, bk),
            sphere2_dist, bk),
            cube_dist, bk),
            torus_dist, bk);

    float eps = 0.55;
    if (abs(sphere_dist - d) < eps)
        obj = 1;
    else if (abs(sphere2_dist - d) < eps)
        obj = 2;
    else if (abs(cube_dist - d) < eps)
        obj = 3;
    else if (abs(torus_dist - d) < eps)
        obj = 4;
    else
        obj = 0;
    return d;
}

float RayMarch(vec3 ro, vec3 rd, out int obj) {
    float d = 0.;
    for (int i = 0; i < MAX_STEPS; i++) {
        vec3 p = ro + rd * d;
        float ds = GetSceneDistance(p, obj);
        d += ds;
        if (d > MAX_DIST || ds < EPS) break;
    }
    return d;
}

vec3 GetNormal(vec3 point) {
    int obj;
    float d = GetSceneDistance(point, obj);
    vec2 e = vec2(0.001, 0.);
    vec3 n = d - vec3(
        GetSceneDistance(point - e.xyy, obj),
        GetSceneDistance(point - e.yxy, obj),
        GetSceneDistance(point - e.yyx, obj)
    );
    return normalize(n);
}

float GetLight(vec3 point) {
    vec3 light_pos = vec3(2., 6., 5.) + vec3(sin(aTime), 0., cos(aTime));
    vec3 to_light = normalize(light_pos - point);
    vec3 normal = GetNormal(point);
    float light = 0.6 * clamp(dot(to_light, normal), 0., 1.);
    int obj;
    float d = RayMarch(point + normal * 2. * EPS, to_light, obj);
    if (d < length(light_pos - point))
        light *= 0.3;
    return light;
}

vec3 getColor(vec2 uv, vec3 ro) {
    int obj;
    vec3 rd = normalize(vec3(uv, 1.));
    float d = RayMarch(ro, rd, obj);
    vec3 point = ro + d * rd;
    float diffuse = GetLight(point);
    vec3 col = vec3(diffuse);

    if (obj == 0)
        col += 0.2 + 0.3 * cos(aTime + uv.xyx + vec3(0., 2., 4.));
    else if (obj == 1)
        col += col1.rgb;
    else if (obj == 2)
        col += col2.rgb;
    else if (obj == 3)
        col += col3.rgb;
    else
        col += col4.rgb;

    return col;
}

void main() {
    vec2 uv = (gl_FragCoord.xy - 0.5 * RENDERSIZE.xy) / RENDERSIZE.x;

    float grip = clamp(pinchHold + audioBass, 0.0, 1.0);
    float eased = grip * grip * grip;
    bk = blendK + eased * 4.0;

    aTime = TIME * animSpeed * (1.0 + audioHigh * 2.0);

    // Mouse orbits the camera around the scene
    float mx = (mousePos.x - 0.5) * 6.28;
    float my = (mousePos.y - 0.5) * 3.0;
    vec3 ro = vec3(sin(mx) * 2., 1. + my, -2. + cos(mx) * 2.);

    vec3 col = getColor(uv, ro);

    gl_FragColor = vec4(col, 1.0);
}

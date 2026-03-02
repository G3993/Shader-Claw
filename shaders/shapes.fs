/*{
    "DESCRIPTION": "Raymarched icosahedral crystal with refraction — inner morphing solid, Cook-Torrance shading, orbit camera",
    "CREDIT": "Glslify icosahedron demo, ported for ShaderClaw",
    "CATEGORIES": ["Generator"],
    "INPUTS": [
        { "NAME": "rotSpeed", "LABEL": "Spin", "TYPE": "float", "DEFAULT": 0.85, "MIN": 0.0, "MAX": 2.0 },
        { "NAME": "camDist", "LABEL": "Distance", "TYPE": "float", "DEFAULT": 5.5, "MIN": 2.0, "MAX": 10.0 },
        { "NAME": "refractAmt", "LABEL": "Refraction", "TYPE": "float", "DEFAULT": 0.85, "MIN": 0.5, "MAX": 1.5 },
        { "NAME": "innerSize", "LABEL": "Inner Size", "TYPE": "float", "DEFAULT": 0.24, "MIN": 0.05, "MAX": 0.8 },
        { "NAME": "lightCol1", "LABEL": "Light 1", "TYPE": "color", "DEFAULT": [0.05, 0.05, 0.15, 1.0] },
        { "NAME": "lightCol2", "LABEL": "Light 2", "TYPE": "color", "DEFAULT": [0.91, 0.25, 0.34, 1.0] },
        { "NAME": "bgColor", "LABEL": "Background", "TYPE": "color", "DEFAULT": [0.0, 0.0, 0.0, 1.0] },
        { "NAME": "gridColor", "LABEL": "Grid Glow", "TYPE": "color", "DEFAULT": [0.91, 0.25, 0.34, 1.0] }
    ]
}*/

vec2 mapRefract(vec3 p);
vec2 mapSolid(vec3 p);

vec2 rayMarchRefract(vec3 ro, vec3 rd) {
    float latest = 0.002;
    float dist = 0.0;
    float type = -1.0;
    vec2 res = vec2(-1.0);
    for (int i = 0; i < 50; i++) {
        if (latest < 0.001 || dist > 20.0) break;
        vec2 result = mapRefract(ro + rd * dist);
        latest = result.x;
        type = result.y;
        dist += latest;
    }
    if (dist < 20.0) res = vec2(dist, type);
    return res;
}

vec2 rayMarchSolid(vec3 ro, vec3 rd) {
    float latest = 0.002;
    float dist = 0.0;
    float type = -1.0;
    vec2 res = vec2(-1.0);
    for (int i = 0; i < 60; i++) {
        if (latest < 0.001 || dist > 20.0) break;
        vec2 result = mapSolid(ro + rd * dist);
        latest = result.x;
        type = result.y;
        dist += latest;
    }
    if (dist < 20.0) res = vec2(dist, type);
    return res;
}

vec3 normalRefract(vec3 pos) {
    float eps = 0.002;
    vec3 v1 = vec3( 1.0,-1.0,-1.0);
    vec3 v2 = vec3(-1.0,-1.0, 1.0);
    vec3 v3 = vec3(-1.0, 1.0,-1.0);
    vec3 v4 = vec3( 1.0, 1.0, 1.0);
    return normalize(
        v1 * mapRefract(pos + v1 * eps).x +
        v2 * mapRefract(pos + v2 * eps).x +
        v3 * mapRefract(pos + v3 * eps).x +
        v4 * mapRefract(pos + v4 * eps).x
    );
}

vec3 normalSolid(vec3 pos) {
    float eps = 0.002;
    vec3 v1 = vec3( 1.0,-1.0,-1.0);
    vec3 v2 = vec3(-1.0,-1.0, 1.0);
    vec3 v3 = vec3(-1.0, 1.0,-1.0);
    vec3 v4 = vec3( 1.0, 1.0, 1.0);
    return normalize(
        v1 * mapSolid(pos + v1 * eps).x +
        v2 * mapSolid(pos + v2 * eps).x +
        v3 * mapSolid(pos + v3 * eps).x +
        v4 * mapSolid(pos + v4 * eps).x
    );
}

float beckmann(float x, float roughness) {
    float NdotH = max(x, 0.0001);
    float cos2Alpha = NdotH * NdotH;
    float tan2Alpha = (cos2Alpha - 1.0) / cos2Alpha;
    float roughness2 = roughness * roughness;
    float denom = 3.141592653589793 * roughness2 * cos2Alpha * cos2Alpha;
    return exp(tan2Alpha / roughness2) / denom;
}

float cookTorrance(vec3 lightDir, vec3 viewDir, vec3 normal, float roughness, float fresnel) {
    float VdotN = max(dot(viewDir, normal), 0.0);
    float LdotN = max(dot(lightDir, normal), 0.0);
    vec3 H = normalize(lightDir + viewDir);
    float NdotH = max(dot(normal, H), 0.0);
    float VdotH = max(dot(viewDir, H), 0.000001);
    float LdotH = max(dot(lightDir, H), 0.000001);
    float G1 = (2.0 * NdotH * VdotN) / VdotH;
    float G2 = (2.0 * NdotH * LdotN) / LdotH;
    float G = min(1.0, min(G1, G2));
    float D = beckmann(NdotH, roughness);
    float F = pow(1.0 - VdotN, fresnel);
    return G * F * D / max(3.14159265 * VdotN, 0.000001);
}

vec2 squareFrame(vec2 screenSize, vec2 coord) {
    vec2 position = 2.0 * (coord / screenSize) - 1.0;
    position.x *= screenSize.x / screenSize.y;
    return position;
}

mat3 lookAt(vec3 origin, vec3 target, float roll) {
    vec3 rr = vec3(sin(roll), cos(roll), 0.0);
    vec3 ww = normalize(target - origin);
    vec3 uu = normalize(cross(ww, rr));
    vec3 vv = normalize(cross(uu, ww));
    return mat3(uu, vv, ww);
}

vec3 getRay(mat3 camMat, vec2 screenPos, float lensLength) {
    return normalize(camMat * vec3(screenPos, lensLength));
}

void orbitCamera(float camAngle, float camHeight, float camD, vec2 res, out vec3 ro, out vec3 rd, vec2 coord) {
    vec2 screenPos = squareFrame(res, coord);
    vec3 target = vec3(0.0);
    ro = vec3(camD * sin(camAngle), camHeight, camD * cos(camAngle));
    mat3 camMat = lookAt(ro, target, 0.0);
    rd = getRay(camMat, screenPos, 2.0);
}

float sdBox(vec3 p, vec3 d) {
    vec3 q = abs(p) - d;
    return min(max(q.x, max(q.y, q.z)), 0.0) + length(max(q, 0.0));
}

float hash(vec2 co) {
    float dt = dot(co, vec2(12.9898, 78.233));
    float sn = mod(dt, 3.14);
    return fract(sin(sn) * 43758.5453);
}

float fogExp2(float d, float density) {
    float v = density * d;
    return 1.0 - clamp(exp2(v * v * -1.442695), 0.0, 1.0);
}

float intersectPlane(vec3 ro, vec3 rd, vec3 nor, float d) {
    float denom = dot(rd, nor);
    return -(dot(ro, nor) + d) / denom;
}

// Icosahedral face normals
vec3 n4  = vec3( 0.577,  0.577,  0.577);
vec3 n5  = vec3(-0.577,  0.577,  0.577);
vec3 n6  = vec3( 0.577, -0.577,  0.577);
vec3 n7  = vec3( 0.577,  0.577, -0.577);
vec3 n8  = vec3( 0.0,    0.357,  0.934);
vec3 n9  = vec3( 0.0,   -0.357,  0.934);
vec3 n10 = vec3( 0.934,  0.0,    0.357);
vec3 n11 = vec3(-0.934,  0.0,    0.357);
vec3 n12 = vec3( 0.357,  0.934,  0.0);
vec3 n13 = vec3(-0.357,  0.934,  0.0);

float icosahedral(vec3 p, float r) {
    float s = abs(dot(p, n4));
    s = max(s, abs(dot(p, n5)));
    s = max(s, abs(dot(p, n6)));
    s = max(s, abs(dot(p, n7)));
    s = max(s, abs(dot(p, n8)));
    s = max(s, abs(dot(p, n9)));
    s = max(s, abs(dot(p, n10)));
    s = max(s, abs(dot(p, n11)));
    s = max(s, abs(dot(p, n12)));
    s = max(s, abs(dot(p, n13)));
    return s - r;
}

vec2 rotate2D(vec2 p, float a) {
    return p * mat2(cos(a), -sin(a), sin(a), cos(a));
}

vec2 mapRefract(vec3 p) {
    return vec2(icosahedral(p, 1.0), 0.0);
}

vec2 mapSolid(vec3 p) {
    p.xz = rotate2D(p.xz, TIME * 1.25);
    p.yx = rotate2D(p.yx, TIME * 1.85);
    p.y += sin(TIME) * 0.25;
    p.x += cos(TIME) * 0.25;
    float d = length(p) - innerSize * (1.0 + audioBass * 1.5);
    float pulse = pow(sin(TIME * 2.0 + audioHigh * 10.0) * 0.5 + 0.5, 9.0) * 2.0;
    d = mix(d, sdBox(p, vec3(innerSize * 0.7)), pulse);
    return vec2(d, 1.0);
}

vec3 palette(float t, vec3 a, vec3 b, vec3 c, vec3 d) {
    return a + b * cos(6.28318 * (c * t + d));
}

vec3 bg(vec3 ro, vec3 rd) {
    vec3 col = 0.1 + palette(
        clamp((hash(rd.xz + sin(TIME * 0.1)) * 0.5 + 0.5) * 0.035 - rd.y * 0.5 + 0.35, -1.0, 1.0),
        bgColor.rgb,
        vec3(0.5, 0.5, 0.5),
        vec3(1.05, 1.0, 1.0),
        vec3(0.275, 0.2, 0.19)
    );
    float t = intersectPlane(ro, rd, vec3(0.0, 1.0, 0.0), 4.0);
    if (t > 0.0) {
        vec3 p = ro + rd * t;
        float g = 1.0 - pow(abs(sin(p.x) * cos(p.z)), 0.25);
        col += (1.0 - fogExp2(t, 0.04)) * g * gridColor.rgb * 5.0 * 0.075;
    }
    return col;
}

void main() {
    vec3 ro, rd;
    vec2 uv = squareFrame(RENDERSIZE.xy, gl_FragCoord.xy);

    // Mouse orbits the camera
    float rotation = TIME * rotSpeed + (mousePos.x - 0.5) * 6.0;
    float height = (mousePos.y - 0.5) * 5.0;

    orbitCamera(rotation, height, camDist, RENDERSIZE.xy, ro, rd, gl_FragCoord.xy);

    vec3 color = bg(ro, rd);
    vec2 t = rayMarchRefract(ro, rd);

    if (t.x > -0.5) {
        vec3 pos = ro + rd * t.x;
        vec3 nor = normalRefract(pos);
        vec3 ldir1 = normalize(vec3(0.8, 1.0, 0.0));
        vec3 ldir2 = normalize(vec3(-0.4, -1.3, 0.0));
        vec3 lcol1 = lightCol1.rgb;
        vec3 lcol2 = lightCol2.rgb * 0.7;

        vec3 ref = refract(rd, nor, refractAmt);
        vec2 u = rayMarchSolid(ro + ref * 0.1, ref);

        if (u.x > -0.5) {
            vec3 pos2 = ro + ref * u.x;
            vec3 nor2 = normalSolid(pos2);
            float spec = cookTorrance(ldir1, -ref, nor2, 0.6, 0.95) * 2.0;
            float diff1 = 0.05 + max(0.0, dot(ldir1, nor2));
            float diff2 = max(0.0, dot(ldir2, nor2));
            color = spec + (diff1 * lcol1 + diff2 * lcol2);
        } else {
            color = bg(ro + ref * 0.1, ref) * 1.1;
        }

        color += color * cookTorrance(ldir1, -rd, nor, 0.2, 0.9) * 2.0;
        color += 0.05;
    }

    float vignette = 1.0 - max(0.0, dot(uv * 0.155, uv));
    color.r = smoothstep(0.05, 0.995, color.r);
    color.b = smoothstep(-0.05, 0.95, color.b);
    color.g = smoothstep(-0.1, 0.95, color.g);
    color.b *= vignette;

    gl_FragColor = vec4(color, 1.0);
}

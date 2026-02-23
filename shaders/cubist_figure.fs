/*
{
  "CATEGORIES": ["Generator", "Art"],
  "DESCRIPTION": "Voxel human figure — precise 3D blocky character like high-res Minecraft",
  "INPUTS": [
    { "NAME": "rotSpeed", "TYPE": "float", "MIN": 0.0, "MAX": 2.0, "DEFAULT": 0.3 },
    { "NAME": "voxelSize", "TYPE": "float", "MIN": 0.03, "MAX": 0.2, "DEFAULT": 0.07 },
    { "NAME": "bgColor", "TYPE": "color", "DEFAULT": [0.08, 0.08, 0.12, 1.0] },
    { "NAME": "skinTone", "TYPE": "color", "DEFAULT": [0.82, 0.62, 0.48, 1.0] },
    { "NAME": "hairColor", "TYPE": "color", "DEFAULT": [0.12, 0.08, 0.06, 1.0] },
    { "NAME": "shirtColor", "TYPE": "color", "DEFAULT": [0.15, 0.55, 0.35, 1.0] },
    { "NAME": "pantsColor", "TYPE": "color", "DEFAULT": [0.2, 0.2, 0.35, 1.0] },
    { "NAME": "shoeColor", "TYPE": "color", "DEFAULT": [0.1, 0.1, 0.1, 1.0] },
    { "NAME": "ambientLight", "TYPE": "float", "MIN": 0.05, "MAX": 0.4, "DEFAULT": 0.15 },
    { "NAME": "gridLines", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.3 },
    { "NAME": "breathe", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.3 },
    { "NAME": "armSwing", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "zoom", "TYPE": "float", "MIN": 1.5, "MAX": 8.0, "DEFAULT": 4.0 }
  ]
}
*/

const float PI = 3.14159265;
const int MAX_STEPS = 120;
const float MAX_DIST = 30.0;
const float SURF_DIST = 0.001;

// ---- Rotation ----
mat2 rot2(float a) {
    float c = cos(a), s = sin(a);
    return mat2(c, -s, s, c);
}

// ---- Box SDF ----
float sdBox(vec3 p, vec3 b) {
    vec3 q = abs(p) - b;
    return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
}

// ---- Body shape test ----
// Returns material ID if voxel center is inside the body, -1 if outside.
// Uses simple box regions to define anatomy.
// Coordinates: Y-up, centered at feet. Head top ~2.0

float bodyTest(vec3 p) {
    // Principle 1: Squash & Stretch — torso widens on inhale, narrows and stretches on exhale
    float breathCycle = sin(TIME * 1.5);
    float br = breathCycle * 0.02 * breathe;           // horizontal expand
    float brY = -breathCycle * 0.01 * breathe;          // vertical compress (opposite)

    // Principle 6: Slow In/Slow Out — arms decelerate at swing extremes like a pendulum
    float rawSwing = sin(TIME * 2.0);
    float swing = sign(rawSwing) * pow(abs(rawSwing), 0.7) * 0.18 * armSwing;

    // All tests in local body space
    float x = p.x, y = p.y, z = p.z;
    float ax = abs(x);

    // --- HEAD (y: 1.62 - 1.95) ---
    // 8x8x8 voxels roughly
    // Principle 12: Appeal — slightly oversized head is more endearing
    if (y > 1.58 && y < 2.0 && ax < 0.24 && abs(z) < 0.24) {
        // Hair on top (y > 1.88)
        if (y > 1.88) return 4.0; // hair
        // Face front (z > 0.1)
        if (z > 0.1 && y > 1.65 && y < 1.9) {
            // Eyes
            if (y > 1.76 && y < 1.83 && ax > 0.06 && ax < 0.14) return 5.0; // eyes (white)
            // Mouth
            if (y > 1.65 && y < 1.7 && ax < 0.08) return 6.0; // mouth
            return 0.0; // skin
        }
        // Hair on sides and back
        if (y > 1.83 || z < -0.1 || ax > 0.2) return 4.0; // hair
        return 0.0; // skin
    }

    // --- NECK (y: 1.52 - 1.62) ---
    if (y > 1.52 && y < 1.62 && ax < 0.08 && abs(z) < 0.08) return 0.0;

    // --- TORSO (y: 0.85 - 1.52) ---
    float torsoW = 0.3 + br;
    float torsoH = 1.52 + brY;
    if (y > 0.85 && y < torsoH && ax < torsoW && abs(z) < 0.18) return 1.0; // shirt

    // --- ARMS ---
    // Upper arms (y: 0.95 - 1.45)
    {
        // Left arm
        vec3 la = vec3(x + 0.4, y - 1.2, z);
        la.xy *= rot2(swing);
        if (abs(la.x) < 0.1 && la.y > -0.3 && la.y < 0.25 && abs(la.z) < 0.1) {
            if (la.y > 0.1) return 1.0; // shirt sleeve
            return 0.0; // skin
        }
        // Left forearm
        vec3 lfa = vec3(x + 0.4, y - 0.7, z);
        lfa.xy *= rot2(swing * 0.5);
        if (abs(lfa.x) < 0.09 && lfa.y > -0.22 && lfa.y < 0.22 && abs(lfa.z) < 0.09) return 0.0;
        // Left hand
        vec3 lh = vec3(x + 0.4, y - 0.4, z);
        lh.xy *= rot2(swing * 0.5);
        if (abs(lh.x) < 0.07 && lh.y > -0.08 && lh.y < 0.08 && abs(lh.z) < 0.05) return 0.0;

        // Right arm
        vec3 ra = vec3(x - 0.4, y - 1.2, z);
        ra.xy *= rot2(-swing);
        if (abs(ra.x) < 0.1 && ra.y > -0.3 && ra.y < 0.25 && abs(ra.z) < 0.1) {
            if (ra.y > 0.1) return 1.0;
            return 0.0;
        }
        // Right forearm
        vec3 rfa = vec3(x - 0.4, y - 0.7, z);
        rfa.xy *= rot2(-swing * 0.5);
        if (abs(rfa.x) < 0.09 && rfa.y > -0.22 && rfa.y < 0.22 && abs(rfa.z) < 0.09) return 0.0;
        // Right hand
        vec3 rh = vec3(x - 0.4, y - 0.4, z);
        rh.xy *= rot2(-swing * 0.5);
        if (abs(rh.x) < 0.07 && rh.y > -0.08 && rh.y < 0.08 && abs(rh.z) < 0.05) return 0.0;
    }

    // --- HIPS (y: 0.7 - 0.85) ---
    if (y > 0.7 && y < 0.85 && ax < 0.28 && abs(z) < 0.16) return 2.0; // pants

    // --- LEGS ---
    {
        // Left leg
        vec3 ll = vec3(x + 0.13, y - 0.35, z);
        ll.xy *= rot2(-swing * 0.4);
        if (abs(ll.x) < 0.1 && ll.y > -0.38 && ll.y < 0.35 && abs(ll.z) < 0.1) return 2.0;
        // Left shin
        vec3 ls = vec3(x + 0.13, y + 0.05, z);
        ls.xy *= rot2(-swing * 0.2);
        if (abs(ls.x) < 0.09 && ls.y > -0.15 && ls.y < 0.12 && abs(ls.z) < 0.09) return 2.0;
        // Left foot
        if (y < 0.08 && y > -0.02 && abs(x + 0.13) < 0.1 && z > -0.08 && z < 0.14) return 3.0; // shoe

        // Right leg
        vec3 rl = vec3(x - 0.13, y - 0.35, z);
        rl.xy *= rot2(swing * 0.4);
        if (abs(rl.x) < 0.1 && rl.y > -0.38 && rl.y < 0.35 && abs(rl.z) < 0.1) return 2.0;
        // Right shin
        vec3 rs = vec3(x - 0.13, y + 0.05, z);
        rs.xy *= rot2(swing * 0.2);
        if (abs(rs.x) < 0.09 && rs.y > -0.15 && rs.y < 0.12 && abs(rs.z) < 0.09) return 2.0;
        // Right foot
        if (y < 0.08 && y > -0.02 && abs(x - 0.13) < 0.1 && z > -0.08 && z < 0.14) return 3.0;
    }

    return -1.0; // outside body
}

// ---- Voxel SDF ----
// Snap point to voxel grid, test if occupied, return cube SDF

float matId;

// Principle 5: Follow Through — head lags behind body rotation
float headLag;

float map(vec3 p) {
    float angle = TIME * rotSpeed;
    // Principle 7: Arc — rotation eases
    float easedAngle = angle + 0.02 * sin(angle * 3.0);

    p.xz *= rot2(easedAngle);

    // Head follow-through: slight counter-rotation above neck
    headLag = sin(TIME * rotSpeed * 3.0) * 0.03 * min(rotSpeed, 1.0);
    if (p.y > 1.5) {
        p.xz *= rot2(-headLag);
    }

    // Snap to voxel grid
    float vs = voxelSize;
    vec3 voxCenter = (floor(p / vs) + 0.5) * vs;

    // Test if this voxel is inside the body
    float mat = bodyTest(voxCenter);

    if (mat < 0.0) {
        // Not occupied — return distance to nearest potential voxel
        // Use a loose body bounding SDF for fast skipping
        float bx = abs(voxCenter.x), by = voxCenter.y, bz = abs(voxCenter.z);
        float bodyBound = sdBox(voxCenter - vec3(0.0, 1.0, 0.0), vec3(0.7, 1.1, 0.4));
        matId = -1.0;
        return max(bodyBound, vs * 0.5);
    }

    matId = mat;
    // SDF of the cube voxel
    vec3 local = p - voxCenter;
    float hs = vs * 0.5;
    return sdBox(local, vec3(hs));
}

// ---- Normal (box normals are axis-aligned) ----
vec3 getNormal(vec3 p) {
    vec2 e = vec2(0.001, 0.0);
    float d = map(p);
    return normalize(vec3(
        map(p + e.xyy) - map(p - e.xyy),
        map(p + e.yxy) - map(p - e.yxy),
        map(p + e.yyx) - map(p - e.yyx)
    ));
}

// ---- Raymarch ----
float raymarch(vec3 ro, vec3 rd) {
    float t = 0.0;
    for (int i = 0; i < MAX_STEPS; i++) {
        vec3 p = ro + rd * t;
        float d = map(p);
        if (d < SURF_DIST) return t;
        if (t > MAX_DIST) break;
        t += d * 0.9; // slight understep for voxel precision
    }
    return -1.0;
}

// ---- Material color ----
vec3 getMaterial(float id) {
    if (id < 0.5) return skinTone.rgb;
    if (id < 1.5) return shirtColor.rgb;
    if (id < 2.5) return pantsColor.rgb;
    if (id < 3.5) return shoeColor.rgb;
    if (id < 4.5) return hairColor.rgb;
    if (id < 5.5) return vec3(0.9, 0.9, 0.95); // eye whites
    return vec3(0.15, 0.05, 0.05);              // mouth
}

// ---- Ambient occlusion ----
float calcAO(vec3 p, vec3 n) {
    float ao = 1.0;
    for (int i = 1; i <= 4; i++) {
        float dist = float(i) * 0.06;
        float d = map(p + n * dist);
        ao -= (dist - d) * (0.4 / float(i));
    }
    return clamp(ao, 0.3, 1.0);
}

// ---- Grid lines on voxel faces ----
float voxelEdge(vec3 p, float vs) {
    vec3 f = fract(p / vs);
    vec3 edge = smoothstep(vec3(0.0), vec3(0.06), f) * smoothstep(vec3(1.0), vec3(0.94), f);
    return min(edge.x, min(edge.y, edge.z));
}

void main() {
    vec2 uv = (gl_FragCoord.xy - RENDERSIZE.xy * 0.5) / RENDERSIZE.y;

    // Camera — zoom controls distance
    vec3 ro = vec3(0.0, 1.1, zoom);
    vec3 target = vec3(0.0, 0.9, 0.0);
    vec3 fwd = normalize(target - ro);
    vec3 right = normalize(cross(fwd, vec3(0.0, 1.0, 0.0)));
    vec3 up = cross(right, fwd);
    vec3 rd = normalize(fwd * 1.8 + right * uv.x + up * uv.y);

    // Lights
    vec3 lightDir1 = normalize(vec3(0.8, 1.2, 0.6));
    vec3 lightDir2 = normalize(vec3(-0.5, 0.3, -0.8));
    vec3 lightCol1 = vec3(1.0, 0.97, 0.9);
    vec3 lightCol2 = vec3(0.3, 0.35, 0.5);

    // Raymarch
    float t = raymarch(ro, rd);

    vec3 col = bgColor.rgb;

    // Subtle gradient background
    col += vec3(0.02, 0.02, 0.04) * (1.0 - uv.y * 0.5);

    if (t > 0.0) {
        vec3 p = ro + rd * t;
        vec3 n = getNormal(p);
        float mid = matId;

        // Material
        vec3 mat = getMaterial(mid);

        // Lighting
        float diff1 = max(dot(n, lightDir1), 0.0);
        float diff2 = max(dot(n, lightDir2), 0.0);
        float spec = pow(max(dot(reflect(-lightDir1, n), -rd), 0.0), 16.0) * 0.15;

        vec3 shade = mat * (ambientLight + diff1 * 0.6 * lightCol1 + diff2 * 0.2 * lightCol2);
        shade += spec * lightCol1;

        // AO
        float ao = calcAO(p, n);
        shade *= ao;

        // Voxel grid lines
        if (gridLines > 0.01) {
            float edge = voxelEdge(p, voxelSize);
            shade *= mix(1.0, edge, gridLines * 0.4);
        }

        // Distance fog (very subtle)
        float fog = exp(-t * t * 0.005);
        shade = mix(bgColor.rgb, shade, fog);

        col = shade;
    } else {
        // Ground shadow
        float groundY = -0.02;
        if (rd.y < 0.0) {
            float tG = (groundY - ro.y) / rd.y;
            if (tG > 0.0) {
                vec3 gp = ro + rd * tG;
                // Simple shadow check
                float shadow = 1.0;
                float st = 0.05;
                for (int i = 0; i < 24; i++) {
                    float sd = map(gp + lightDir1 * st);
                    shadow = min(shadow, 6.0 * sd / st);
                    st += max(sd, 0.03);
                    if (st > 5.0) break;
                }
                shadow = clamp(shadow, 0.0, 1.0);
                float gDist = length(gp.xz);
                float gFade = smoothstep(2.5, 0.0, gDist);
                col = bgColor.rgb * (0.85 + 0.15 * shadow) * (1.0 - gFade * (1.0 - shadow) * 0.3);
            }
        }
    }

    // Vignette
    vec2 vUV = gl_FragCoord.xy / RENDERSIZE.xy;
    float vig = 1.0 - 0.25 * length((vUV - 0.5) * 1.4);
    col *= vig;

    gl_FragColor = vec4(col, 1.0);
}
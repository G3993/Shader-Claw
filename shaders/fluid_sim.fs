/*{
  "DESCRIPTION": "GPU Navier-Stokes fluid — port of Pavel Dobryakov's WebGL Fluid Simulation",
  "CREDIT": "Pavel Dobryakov / ShaderClaw",
  "CATEGORIES": ["Generator", "Simulation"],
  "INPUTS": [
    { "NAME": "splatForce", "TYPE": "float", "DEFAULT": 6000.0, "MIN": 500.0, "MAX": 20000.0 },
    { "NAME": "splatRadius", "LABEL": "Splat Radius", "TYPE": "float", "DEFAULT": 0.005, "MIN": 0.001, "MAX": 0.05 },
    { "NAME": "curlStrength", "TYPE": "float", "DEFAULT": 30.0, "MIN": 0.0, "MAX": 80.0 },
    { "NAME": "velDissipation", "LABEL": "Vel Dissipation", "TYPE": "float", "DEFAULT": 0.2, "MIN": 0.0, "MAX": 2.0 },
    { "NAME": "dyeDissipation", "LABEL": "Dye Dissipation", "TYPE": "float", "DEFAULT": 0.5, "MIN": 0.0, "MAX": 5.0 },
    { "NAME": "pressureDecay", "TYPE": "float", "DEFAULT": 0.6, "MIN": 0.0, "MAX": 1.0 },
    { "NAME": "bloomIntensity", "TYPE": "float", "DEFAULT": 0.8, "MIN": 0.0, "MAX": 3.0 },
    { "NAME": "shading", "TYPE": "bool", "DEFAULT": true },
    { "NAME": "autoSplats", "TYPE": "bool", "DEFAULT": true }
  ],
  "PASSES": [
    { "TARGET": "curlBuf",     "WIDTH": 256, "HEIGHT": 256, "PERSISTENT": true },
    { "TARGET": "velocityBuf", "WIDTH": 256, "HEIGHT": 256, "PERSISTENT": true },
    { "TARGET": "pressure0",   "WIDTH": 256, "HEIGHT": 256, "PERSISTENT": true },
    { "TARGET": "pressure1",   "WIDTH": 256, "HEIGHT": 256, "PERSISTENT": true },
    { "TARGET": "pressure2",   "WIDTH": 256, "HEIGHT": 256, "PERSISTENT": true },
    { "TARGET": "pressure3",   "WIDTH": 256, "HEIGHT": 256, "PERSISTENT": true },
    { "TARGET": "dyeBuf",      "PERSISTENT": true },
    {}
  ]
}*/

// ============================================================
// Bias-scale encoding: maps signed sim values into [0,1] so
// the simulation works with both half-float AND uint8 FBOs.
// Without this, uint8 FBOs clamp negative velocities to zero,
// completely breaking the fluid dynamics.
// ============================================================
const float VEL_RANGE  = 1000.0;  // velocity ±1000
const float P_RANGE    = 500.0;   // pressure ±500
const float CURL_RANGE = 1000.0;  // curl     ±1000
const float DYE_RANGE  = 3.0;     // dye HDR  [0, 3]

const float H  = 1.0 / 256.0;
const float DT = 0.016;

// --- Velocity encode/decode (xy channels) ---
vec2 decVel(sampler2D s, vec2 uv) {
    return (texture2D(s, uv).xy - 0.5) * 2.0 * VEL_RANGE;
}
vec4 encVel(vec2 v) {
    return vec4(clamp(v / (2.0 * VEL_RANGE) + 0.5, 0.0, 1.0), 0.0, 1.0);
}

// --- Pressure encode/decode (x channel) ---
float decP(sampler2D s, vec2 uv) {
    return (texture2D(s, uv).x - 0.5) * 2.0 * P_RANGE;
}
vec4 encP(float p) {
    return vec4(clamp(p / (2.0 * P_RANGE) + 0.5, 0.0, 1.0), 0.0, 0.0, 1.0);
}

// --- Curl encode/decode (x channel) ---
float decCurl(sampler2D s, vec2 uv) {
    return (texture2D(s, uv).x - 0.5) * 2.0 * CURL_RANGE;
}
vec4 encCurl(float c) {
    return vec4(clamp(c / (2.0 * CURL_RANGE) + 0.5, 0.0, 1.0), 0.0, 0.0, 1.0);
}

// --- Dye encode/decode (rgb, HDR into [0,1]) ---
vec3 decDye(sampler2D s, vec2 uv) {
    return texture2D(s, uv).rgb * DYE_RANGE;
}
vec4 encDye(vec3 d) {
    return vec4(clamp(d / DYE_RANGE, 0.0, 1.0), 1.0);
}

float hash(float n) {
    n = fract(n * 0.1031);
    n *= n + 33.33;
    n *= n + n;
    return fract(n);
}

vec3 hsv2rgb(vec3 c) {
    vec3 p = abs(fract(c.xxx + vec3(1.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0);
    return c.z * mix(vec3(1.0), clamp(p - 1.0, 0.0, 1.0), c.y);
}

// ============================================================
// PASS 0: Curl of velocity field
// ============================================================
vec4 passCurl() {
    vec2 uv = isf_FragNormCoord;
    float L = decVel(velocityBuf, uv - vec2(H, 0.0)).y;
    float R = decVel(velocityBuf, uv + vec2(H, 0.0)).y;
    float T = decVel(velocityBuf, uv + vec2(0.0, H)).x;
    float B = decVel(velocityBuf, uv - vec2(0.0, H)).x;
    return encCurl(0.5 * (R - L - T + B));
}

// ============================================================
// PASS 1: Velocity — advect + vorticity + forces
// ============================================================
vec4 passVelocity() {
    vec2 uv = isf_FragNormCoord;

    // Semi-Lagrangian self-advection
    vec2 oldVel = decVel(velocityBuf, uv);
    vec2 coord = uv - DT * oldVel * H;
    vec2 vel = decVel(velocityBuf, coord);

    // Pressure gradient subtract (previous frame's converged pressure)
    float pL = decP(pressure3, uv - vec2(H, 0.0));
    float pR = decP(pressure3, uv + vec2(H, 0.0));
    float pT = decP(pressure3, uv + vec2(0.0, H));
    float pB = decP(pressure3, uv - vec2(0.0, H));
    vel -= vec2(pR - pL, pT - pB);

    // Vorticity confinement
    float cL = decCurl(curlBuf, uv - vec2(H, 0.0));
    float cR = decCurl(curlBuf, uv + vec2(H, 0.0));
    float cT = decCurl(curlBuf, uv + vec2(0.0, H));
    float cB = decCurl(curlBuf, uv - vec2(0.0, H));
    float cC = decCurl(curlBuf, uv);
    vec2 vf = 0.5 * vec2(abs(cT) - abs(cB), abs(cR) - abs(cL));
    vf /= length(vf) + 0.0001;
    vf *= curlStrength * cC;
    vf.y *= -1.0;
    vel += vf * DT;

    // Mouse / hand force
    if (length(mouseDelta) > 0.0001) {
        vec2 p = uv - mousePos;
        vel += mouseDelta * splatForce * exp(-dot(p, p) / splatRadius);
    }

    // Audio-reactive force injection: bass creates centered force bursts
    if (audioBass > 0.1) {
        vec2 ap = uv - vec2(0.5);
        vec2 audioForce = normalize(ap + 0.001) * audioBass * 200.0;
        vel += audioForce * exp(-dot(ap, ap) / (splatRadius * 4.0));
    }

    // Auto-splats for initial motion
    if (autoSplats) {
        if (FRAMEINDEX < 20) {
            float seed = float(FRAMEINDEX);
            vec2 sp = vec2(hash(seed * 13.73), hash(seed * 7.31));
            vec2 sv = (vec2(hash(seed * 23.17), hash(seed * 31.71)) - 0.5) * 600.0;
            vec2 dp = uv - sp;
            vel += sv * exp(-dot(dp, dp) / (splatRadius * 2.0));
        }
        if (FRAMEINDEX >= 20) {
            float splatIdx = floor(TIME / 1.5);
            float splatAge = TIME - splatIdx * 1.5;
            if (splatAge < 0.1) {
                float seed = splatIdx * 77.0 + 100.0;
                vec2 sp = vec2(hash(seed * 13.73), hash(seed * 7.31));
                vec2 sv = (vec2(hash(seed * 23.17), hash(seed * 31.71)) - 0.5) * 400.0;
                vec2 dp = uv - sp;
                float fade = smoothstep(0.0, 0.02, splatAge) * smoothstep(0.1, 0.05, splatAge);
                vel += sv * fade * exp(-dot(dp, dp) / (splatRadius * 2.0));
            }
        }
    }

    // Dissipation + clamp
    vel /= 1.0 + velDissipation * DT;
    vel = clamp(vel, -VEL_RANGE, VEL_RANGE);

    // Boundary: reflect at edges
    if (uv.x < H) vel.x = abs(vel.x);
    if (uv.x > 1.0 - H) vel.x = -abs(vel.x);
    if (uv.y < H) vel.y = abs(vel.y);
    if (uv.y > 1.0 - H) vel.y = -abs(vel.y);

    return encVel(vel);
}

// ============================================================
// Pressure Jacobi iteration
// ============================================================
vec4 pressureJacobi(sampler2D prevP, bool withDecay) {
    vec2 uv = isf_FragNormCoord;
    if (FRAMEINDEX < 1) return encP(0.0);

    // Divergence of velocity field
    vec2 vC = decVel(velocityBuf, uv);
    float vL = decVel(velocityBuf, uv - vec2(H, 0.0)).x;
    float vR = decVel(velocityBuf, uv + vec2(H, 0.0)).x;
    float vT = decVel(velocityBuf, uv + vec2(0.0, H)).y;
    float vB = decVel(velocityBuf, uv - vec2(0.0, H)).y;

    if (uv.x - H < 0.0) vL = -vC.x;
    if (uv.x + H > 1.0) vR = -vC.x;
    if (uv.y + H > 1.0) vT = -vC.y;
    if (uv.y - H < 0.0) vB = -vC.y;

    float div = 0.5 * (vR - vL + vT - vB);

    float d = withDecay ? pressureDecay : 1.0;
    float pL = decP(prevP, uv - vec2(H, 0.0)) * d;
    float pR = decP(prevP, uv + vec2(H, 0.0)) * d;
    float pT = decP(prevP, uv + vec2(0.0, H)) * d;
    float pB = decP(prevP, uv - vec2(0.0, H)) * d;

    return encP((pL + pR + pT + pB - div) * 0.25);
}

// ============================================================
// PASS 6: Dye advection + color injection
// ============================================================
vec4 passDye() {
    vec2 uv = isf_FragNormCoord;
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    float rad = splatRadius * 2.5 * (aspect > 1.0 ? aspect : 1.0);

    // Advect dye with velocity field
    vec2 vel = decVel(velocityBuf, uv);
    vec2 coord = uv - DT * vel * H;
    vec3 dye = decDye(dyeBuf, coord);

    // Dissipation
    dye /= 1.0 + dyeDissipation * DT;

    // Mouse / hand color splat
    bool doSplat = length(mouseDelta) > 0.0001 || mouseDown > 0.5;
    if (doSplat) {
        vec2 p = uv - mousePos;
        p.x *= aspect;
        float s = exp(-dot(p, p) / rad);
        float hue = fract(TIME * 0.12 + mousePos.x * 0.5 + mousePos.y * 0.3);
        vec3 col = hsv2rgb(vec3(hue, 1.0, 1.0)) * 1.5;
        dye += col * s;
    }

    // Auto-splats dye
    if (autoSplats) {
        if (FRAMEINDEX < 20) {
            float seed = float(FRAMEINDEX);
            vec2 sp = vec2(hash(seed * 13.73), hash(seed * 7.31));
            vec3 col = hsv2rgb(vec3(hash(seed * 3.17), 1.0, 1.0)) * 2.0;
            vec2 dp = uv - sp;
            dp.x *= aspect;
            dye += col * exp(-dot(dp, dp) / rad);
        }
        if (FRAMEINDEX >= 20) {
            float splatIdx = floor(TIME / 1.5);
            float splatAge = TIME - splatIdx * 1.5;
            if (splatAge < 0.1) {
                float seed = splatIdx * 77.0 + 100.0;
                vec2 sp = vec2(hash(seed * 13.73), hash(seed * 7.31));
                vec3 col = hsv2rgb(vec3(hash(seed * 3.17), 1.0, 1.0)) * 1.5;
                vec2 dp = uv - sp;
                dp.x *= aspect;
                float fade = smoothstep(0.0, 0.02, splatAge) * smoothstep(0.1, 0.05, splatAge);
                dye += col * fade * exp(-dot(dp, dp) / rad);
            }
        }
    }

    return encDye(dye);
}

// ============================================================
// PASS 7: Display
// ============================================================
vec4 passDisplay() {
    vec2 uv = isf_FragNormCoord;
    vec3 c = decDye(dyeBuf, uv);

    // Shading — Pavel's method: surface normal from dye luminance gradient
    if (shading) {
        vec2 tx = 1.0 / RENDERSIZE;
        float dx = length(decDye(dyeBuf, uv + vec2(tx.x, 0.0)))
                 - length(decDye(dyeBuf, uv - vec2(tx.x, 0.0)));
        float dy = length(decDye(dyeBuf, uv + vec2(0.0, tx.y)))
                 - length(decDye(dyeBuf, uv - vec2(0.0, tx.y)));
        // Pavel: normalize(vec3(dx, dy, 1.0)) then shade = abs(n.z)
        float mag = sqrt(dx * dx + dy * dy + 1.0);
        c *= abs(1.0 / mag);
    }

    // Bloom: multi-scale axis-aligned samples
    if (bloomIntensity > 0.01) {
        vec2 tx = 1.0 / RENDERSIZE;
        vec3 bloom = vec3(0.0);
        bloom += decDye(dyeBuf, uv + vec2(tx.x * 4.0, 0.0));
        bloom += decDye(dyeBuf, uv - vec2(tx.x * 4.0, 0.0));
        bloom += decDye(dyeBuf, uv + vec2(0.0, tx.y * 4.0));
        bloom += decDye(dyeBuf, uv - vec2(0.0, tx.y * 4.0));
        bloom += decDye(dyeBuf, uv + vec2(tx.x * 12.0, 0.0)) * 0.7;
        bloom += decDye(dyeBuf, uv - vec2(tx.x * 12.0, 0.0)) * 0.7;
        bloom += decDye(dyeBuf, uv + vec2(0.0, tx.y * 12.0)) * 0.7;
        bloom += decDye(dyeBuf, uv - vec2(0.0, tx.y * 12.0)) * 0.7;
        bloom += decDye(dyeBuf, uv + vec2(tx.x * 28.0, 0.0)) * 0.4;
        bloom += decDye(dyeBuf, uv - vec2(tx.x * 28.0, 0.0)) * 0.4;
        bloom += decDye(dyeBuf, uv + vec2(0.0, tx.y * 28.0)) * 0.4;
        bloom += decDye(dyeBuf, uv - vec2(0.0, tx.y * 28.0)) * 0.4;
        bloom /= 12.0;
        float br = max(bloom.r, max(bloom.g, bloom.b));
        bloom *= smoothstep(0.3, 0.8, br);
        c += bloom * bloomIntensity;
    }

    // Gamma only (no tone mapping — keeps colors vivid like Pavel's)
    c = pow(clamp(c, 0.0, 1.0), vec3(1.0 / 2.2));

    float a = max(c.r, max(c.g, c.b));
    return vec4(c, a);
}

void main() {
    if      (PASSINDEX == 0) gl_FragColor = passCurl();
    else if (PASSINDEX == 1) gl_FragColor = passVelocity();
    else if (PASSINDEX == 2) gl_FragColor = pressureJacobi(pressure3, true);
    else if (PASSINDEX == 3) gl_FragColor = pressureJacobi(pressure0, false);
    else if (PASSINDEX == 4) gl_FragColor = pressureJacobi(pressure1, false);
    else if (PASSINDEX == 5) gl_FragColor = pressureJacobi(pressure2, false);
    else if (PASSINDEX == 6) gl_FragColor = passDye();
    else                     gl_FragColor = passDisplay();
}

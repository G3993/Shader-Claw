/*{
  "DESCRIPTION": "GPU Navier-Stokes fluid simulation. Faithful port of Pavel Dobryakov's WebGL-Fluid-Simulation.",
  "CREDIT": "Pavel Dobryakov / ShaderClaw",
  "CATEGORIES": ["Generator", "Simulation"],
  "INPUTS": [
    { "NAME": "splatForce", "TYPE": "float", "DEFAULT": 6000.0, "MIN": 500.0, "MAX": 20000.0 },
    { "NAME": "splatRadius", "TYPE": "float", "DEFAULT": 0.0025, "MIN": 0.0005, "MAX": 0.02 },
    { "NAME": "curlStrength", "TYPE": "float", "DEFAULT": 30.0, "MIN": 0.0, "MAX": 80.0 },
    { "NAME": "velDissipation", "TYPE": "float", "DEFAULT": 0.2, "MIN": 0.0, "MAX": 2.0 },
    { "NAME": "dyeDissipation", "TYPE": "float", "DEFAULT": 1.0, "MIN": 0.0, "MAX": 5.0 },
    { "NAME": "pressureDecay", "TYPE": "float", "DEFAULT": 0.8, "MIN": 0.0, "MAX": 1.0 },
    { "NAME": "bloomIntensity", "TYPE": "float", "DEFAULT": 0.8, "MIN": 0.0, "MAX": 2.0 },
    { "NAME": "shading", "TYPE": "bool", "DEFAULT": true },
    { "NAME": "autoSplats", "TYPE": "bool", "DEFAULT": true }
  ],
  "PASSES": [
    { "TARGET": "curlBuf", "WIDTH": 128, "HEIGHT": 128, "PERSISTENT": true },
    { "TARGET": "velocityBuf", "WIDTH": 128, "HEIGHT": 128, "PERSISTENT": true },
    { "TARGET": "pressure0", "WIDTH": 128, "HEIGHT": 128, "PERSISTENT": true },
    { "TARGET": "pressure1", "WIDTH": 128, "HEIGHT": 128, "PERSISTENT": true },
    { "TARGET": "pressure2", "WIDTH": 128, "HEIGHT": 128, "PERSISTENT": true },
    { "TARGET": "pressure3", "WIDTH": 128, "HEIGHT": 128, "PERSISTENT": true },
    { "TARGET": "dyeBuf", "PERSISTENT": true },
    {}
  ]
}*/

const float SIM_RES = 128.0;
const float H = 1.0 / 128.0;
const float DT = 0.016;

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

vec3 linearToGamma(vec3 c) {
    c = max(c, vec3(0.0));
    return max(1.055 * pow(c, vec3(0.416667)) - 0.055, vec3(0.0));
}

// ============================================================
// PASS 0: Curl
// ============================================================
vec4 passCurl() {
    vec2 uv = isf_FragNormCoord;
    float L = texture2D(velocityBuf, uv - vec2(H, 0.0)).y;
    float R = texture2D(velocityBuf, uv + vec2(H, 0.0)).y;
    float T = texture2D(velocityBuf, uv + vec2(0.0, H)).x;
    float B = texture2D(velocityBuf, uv - vec2(0.0, H)).x;
    return vec4(0.5 * (R - L - T + B), 0.0, 0.0, 1.0);
}

// ============================================================
// PASS 1: Velocity
// ============================================================
vec4 passVelocity() {
    vec2 uv = isf_FragNormCoord;

    // Self-advection
    vec2 oldVel = texture2D(velocityBuf, uv).xy;
    vec2 coord = uv - DT * oldVel * H;
    vec2 vel = texture2D(velocityBuf, coord).xy;

    // Pressure gradient subtract (from previous frame's final pressure)
    float pL = texture2D(pressure3, uv - vec2(H, 0.0)).x;
    float pR = texture2D(pressure3, uv + vec2(H, 0.0)).x;
    float pT = texture2D(pressure3, uv + vec2(0.0, H)).x;
    float pB = texture2D(pressure3, uv - vec2(0.0, H)).x;
    vel -= vec2(pR - pL, pT - pB);

    // Vorticity confinement
    float cL = texture2D(curlBuf, uv - vec2(H, 0.0)).x;
    float cR = texture2D(curlBuf, uv + vec2(H, 0.0)).x;
    float cT = texture2D(curlBuf, uv + vec2(0.0, H)).x;
    float cB = texture2D(curlBuf, uv - vec2(0.0, H)).x;
    float cC = texture2D(curlBuf, uv).x;
    vec2 vf = 0.5 * vec2(abs(cT) - abs(cB), abs(cR) - abs(cL));
    vf /= length(vf) + 0.0001;
    vf *= curlStrength * cC;
    vf.y *= -1.0;
    vel += vf * DT;

    // Mouse force
    if (length(mouseDelta) > 0.0001) {
        vec2 p = uv - mousePos;
        p.x *= RENDERSIZE.x / RENDERSIZE.y;
        vel += mouseDelta * splatForce * exp(-dot(p, p) / splatRadius);
    }

    // Auto-splats: initial burst on first 15 frames, then 1 every 3s
    if (autoSplats) {
        float aspect = RENDERSIZE.x / RENDERSIZE.y;

        // Initial burst: 1 splat per frame for first 15 frames
        if (FRAMEINDEX < 15) {
            float seed = float(FRAMEINDEX);
            vec2 sp = vec2(hash(seed * 13.73), hash(seed * 7.31));
            vec2 sv = (vec2(hash(seed * 23.17), hash(seed * 31.71)) - 0.5) * 1000.0;
            vec2 dp = uv - sp;
            dp.x *= aspect;
            vel += sv * exp(-dot(dp, dp) / splatRadius);
        }

        // Ongoing: 1 splat every 3 seconds
        if (FRAMEINDEX >= 15) {
            float splatIdx = floor(TIME / 3.0);
            float splatAge = TIME - splatIdx * 3.0;
            if (splatAge < 0.1) {
                float seed = splatIdx * 77.0 + 100.0;
                vec2 sp = vec2(hash(seed * 13.73), hash(seed * 7.31));
                vec2 sv = (vec2(hash(seed * 23.17), hash(seed * 31.71)) - 0.5) * 1000.0;
                vec2 dp = uv - sp;
                dp.x *= aspect;
                float fade = smoothstep(0.0, 0.02, splatAge) * smoothstep(0.1, 0.05, splatAge);
                vel += sv * exp(-dot(dp, dp) / splatRadius) * fade;
            }
        }
    }

    // Dissipation
    vel /= 1.0 + velDissipation * DT;
    vel = clamp(vel, -1000.0, 1000.0);

    // Boundary reflection
    if (uv.x < H) vel.x = abs(vel.x);
    if (uv.x > 1.0 - H) vel.x = -abs(vel.x);
    if (uv.y < H) vel.y = abs(vel.y);
    if (uv.y > 1.0 - H) vel.y = -abs(vel.y);

    return vec4(vel, 0.0, 1.0);
}

// ============================================================
// Pressure Jacobi (shared)
// ============================================================
vec4 pressureJacobi(sampler2D prevP, bool withDecay) {
    vec2 uv = isf_FragNormCoord;
    if (FRAMEINDEX < 1) return vec4(0.0);

    // Divergence
    vec2 vC = texture2D(velocityBuf, uv).xy;
    float vL = texture2D(velocityBuf, uv - vec2(H, 0.0)).x;
    float vR = texture2D(velocityBuf, uv + vec2(H, 0.0)).x;
    float vT = texture2D(velocityBuf, uv + vec2(0.0, H)).y;
    float vB = texture2D(velocityBuf, uv - vec2(0.0, H)).y;
    if (uv.x - H < 0.0) vL = -vC.x;
    if (uv.x + H > 1.0) vR = -vC.x;
    if (uv.y + H > 1.0) vT = -vC.y;
    if (uv.y - H < 0.0) vB = -vC.y;
    float div = 0.5 * (vR - vL + vT - vB);

    // Jacobi
    float d = withDecay ? pressureDecay : 1.0;
    float pL = texture2D(prevP, uv - vec2(H, 0.0)).x * d;
    float pR = texture2D(prevP, uv + vec2(H, 0.0)).x * d;
    float pT = texture2D(prevP, uv + vec2(0.0, H)).x * d;
    float pB = texture2D(prevP, uv - vec2(0.0, H)).x * d;
    return vec4((pL + pR + pT + pB - div) * 0.25, 0.0, 0.0, 1.0);
}

// ============================================================
// PASS 6: Dye
// ============================================================
vec4 passDye() {
    vec2 uv = isf_FragNormCoord;
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    float rad = splatRadius * (aspect > 1.0 ? aspect : 1.0);

    // Advect
    vec2 vel = texture2D(velocityBuf, uv).xy;
    vec2 coord = uv - DT * vel * H;
    vec3 dye = texture2D(dyeBuf, coord).rgb;

    // Dissipation
    dye /= 1.0 + dyeDissipation * DT;

    // Mouse color splat (intensity 0.15, matching original)
    if (length(mouseDelta) > 0.0001) {
        vec2 p = uv - mousePos;
        p.x *= aspect;
        float s = exp(-dot(p, p) / rad);
        vec3 col = hsv2rgb(vec3(fract(TIME * 0.3), 1.0, 1.0)) * 0.15;
        dye += col * s;
    }

    // Auto-splats dye
    if (autoSplats) {
        // Initial burst: bright splats (intensity 1.5, matching original * 10)
        if (FRAMEINDEX < 15) {
            float seed = float(FRAMEINDEX);
            vec2 sp = vec2(hash(seed * 13.73), hash(seed * 7.31));
            vec3 col = hsv2rgb(vec3(hash(seed * 3.17), 1.0, 1.0)) * 1.5;
            vec2 dp = uv - sp;
            dp.x *= aspect;
            dye += col * exp(-dot(dp, dp) / rad);
        }

        // Ongoing: dimmer splats (intensity 0.15)
        if (FRAMEINDEX >= 15) {
            float splatIdx = floor(TIME / 3.0);
            float splatAge = TIME - splatIdx * 3.0;
            if (splatAge < 0.1) {
                float seed = splatIdx * 77.0 + 100.0;
                vec2 sp = vec2(hash(seed * 13.73), hash(seed * 7.31));
                vec3 col = hsv2rgb(vec3(hash(seed * 3.17), 1.0, 1.0)) * 0.15;
                vec2 dp = uv - sp;
                dp.x *= aspect;
                float fade = smoothstep(0.0, 0.02, splatAge) * smoothstep(0.1, 0.05, splatAge);
                dye += col * fade * exp(-dot(dp, dp) / rad);
            }
        }
    }

    return vec4(dye, 1.0);
}

// ============================================================
// PASS 7: Display
// ============================================================
vec4 passDisplay() {
    vec2 uv = isf_FragNormCoord;
    vec3 c = texture2D(dyeBuf, uv).rgb;

    // Shading: surface normal from luminance gradient
    if (shading) {
        float tx = 1.0 / RENDERSIZE.x;
        float ty = 1.0 / RENDERSIZE.y;
        float dx = length(texture2D(dyeBuf, uv + vec2(tx, 0.0)).rgb)
                 - length(texture2D(dyeBuf, uv - vec2(tx, 0.0)).rgb);
        float dy = length(texture2D(dyeBuf, uv + vec2(0.0, ty)).rgb)
                 - length(texture2D(dyeBuf, uv - vec2(0.0, ty)).rgb);
        vec3 n = normalize(vec3(dx, dy, length(vec2(tx, ty))));
        float diffuse = clamp(dot(n, vec3(0.0, 0.0, 1.0)) + 0.7, 0.7, 1.0);
        c *= diffuse;
    }

    // Bloom: neighborhood glow (gamma-corrected, additive — matching original)
    if (bloomIntensity > 0.01) {
        vec3 bloom = vec3(0.0);
        float bx = 3.0 / RENDERSIZE.x;
        float by = 3.0 / RENDERSIZE.y;
        for (int i = -2; i <= 2; i++) {
            for (int j = -2; j <= 2; j++) {
                if (i == 0 && j == 0) continue;
                bloom += texture2D(dyeBuf, uv + vec2(float(i) * bx, float(j) * by)).rgb;
            }
        }
        bloom /= 24.0;
        // Threshold: only glow from bright areas (original threshold = 0.6)
        float br = max(bloom.r, max(bloom.g, bloom.b));
        bloom *= smoothstep(0.2, 0.6, br);
        bloom = linearToGamma(bloom);
        c += bloom * bloomIntensity;
    }

    // NO gamma on base dye (matching original — dye stays in linear space)
    float a = max(c.r, max(c.g, c.b));
    return vec4(c, a);
}

void main() {
    if (PASSINDEX == 0) {
        gl_FragColor = passCurl();
    } else if (PASSINDEX == 1) {
        gl_FragColor = passVelocity();
    } else if (PASSINDEX == 2) {
        gl_FragColor = pressureJacobi(pressure3, true);
    } else if (PASSINDEX == 3) {
        gl_FragColor = pressureJacobi(pressure0, false);
    } else if (PASSINDEX == 4) {
        gl_FragColor = pressureJacobi(pressure1, false);
    } else if (PASSINDEX == 5) {
        gl_FragColor = pressureJacobi(pressure2, false);
    } else if (PASSINDEX == 6) {
        gl_FragColor = passDye();
    } else {
        gl_FragColor = passDisplay();
    }
}

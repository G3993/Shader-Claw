/*{
  "DESCRIPTION": "Robot arm — 2-link inverse kinematics follows hand tracking or mouse. Dual arms with two hands. Pinch to grip, hold to fire laser.",
  "CREDIT": "ShaderClaw",
  "CATEGORIES": ["Generator"],
  "INPUTS": [
    { "NAME": "armMode", "LABEL": "Arms", "TYPE": "long", "DEFAULT": 1, "VALUES": [1, 2], "LABELS": ["1", "2"] },
    { "NAME": "armColor", "LABEL": "Arm", "TYPE": "color", "DEFAULT": [0.78, 0.8, 0.84, 1.0] },
    { "NAME": "accentColor", "LABEL": "Accent", "TYPE": "color", "DEFAULT": [0.4, 0.85, 1.0, 1.0] },
    { "NAME": "laserColor", "LABEL": "Laser", "TYPE": "color", "DEFAULT": [0.4, 0.9, 1.0, 1.0] },
    { "NAME": "armScale", "LABEL": "Size", "TYPE": "float", "DEFAULT": 1.0, "MIN": 0.3, "MAX": 2.0 },
    { "NAME": "segWidth", "LABEL": "Thickness", "TYPE": "float", "DEFAULT": 1.0, "MIN": 0.3, "MAX": 2.5 },
    { "NAME": "showGrid", "LABEL": "Grid", "TYPE": "bool", "DEFAULT": true },
    { "NAME": "transparentBg", "LABEL": "Transparent", "TYPE": "bool", "DEFAULT": false },
    { "NAME": "bgColor", "LABEL": "Background", "TYPE": "color", "DEFAULT": [0.035, 0.035, 0.055, 1.0] }
  ]
}*/

// ── SDF primitives ──────────────────────────────────────────

float sdCapsule(vec2 p, vec2 a, vec2 b, float r) {
    vec2 pa = p - a, ba = b - a;
    float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
    return length(pa - ba * h) - r;
}

float sdCircle(vec2 p, vec2 c, float r) {
    return length(p - c) - r;
}

// ── Pseudo-3D capsule (cylindrical shading on a 2D capsule) ──

vec3 shadeCapsule(vec2 p, vec2 a, vec2 b, float r, vec3 color, vec3 L, float px, out float mask) {
    vec2 pa = p - a, ba = b - a;
    float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
    vec2 closest = a + ba * h;
    float dist = length(p - closest);
    mask = smoothstep(r + px, r - px, dist);
    if (mask < 0.001) return vec3(0.0);

    float t = clamp(dist / r, 0.0, 1.0);
    float nz = sqrt(max(0.0, 1.0 - t * t));
    vec2 nxy = (p - closest) / max(dist, 0.0001);
    vec3 N = normalize(vec3(nxy, nz));

    float diff = max(0.0, dot(N, L));
    float spec = pow(max(0.0, dot(reflect(-L, N), vec3(0.0, 0.0, 1.0))), 80.0);
    float rim = pow(1.0 - nz, 2.5);
    // Environment reflection: top-lighter gradient
    float envRefl = 0.06 * (0.5 + 0.5 * N.y);

    return color * (0.10 + 0.60 * diff + envRefl) + vec3(0.70) * spec + color * rim * 0.25;
}

// ── Pseudo-3D sphere (for joints) ──

vec3 shadeSphere(vec2 p, vec2 c, float r, vec3 color, vec3 L, float px, out float mask) {
    float dist = length(p - c);
    mask = smoothstep(r + px, r - px, dist);
    if (mask < 0.001) return vec3(0.0);

    float t = clamp(dist / r, 0.0, 1.0);
    float nz = sqrt(max(0.0, 1.0 - t * t));
    vec2 nxy = (p - c) / max(dist, 0.0001);
    vec3 N = normalize(vec3(nxy, nz));

    float diff = max(0.0, dot(N, L));
    float spec = pow(max(0.0, dot(reflect(-L, N), vec3(0.0, 0.0, 1.0))), 100.0);
    float rim = pow(1.0 - nz, 2.0);
    float envRefl = 0.08 * (0.5 + 0.5 * N.y);

    return color * (0.08 + 0.55 * diff + envRefl) + vec3(0.90) * spec + color * rim * 0.4;
}

// ── Draw one complete arm ──────────────────────────────────

void drawArm(vec2 p, vec2 base, vec2 target, float grip, float sc, float sw,
             vec4 aCol, vec4 accCol, vec3 L, float px, float elbowSign,
             inout vec3 col, inout float armMask,
             out vec2 outWrist, out vec2 outFMid, out vec2 outFDir) {

    float L1 = 0.25 * sc;
    float L2 = 0.22 * sc;
    float w1 = 0.024 * sw;
    float w2 = 0.018 * sw;
    float jR = 0.030 * sw;
    float jR2 = 0.023 * sw;

    // ── 2-link inverse kinematics ──
    vec2 toTarget = target - base;
    float d = length(toTarget);
    float maxReach = L1 + L2 - 0.005;
    float minReach = abs(L1 - L2) + 0.005;
    d = clamp(d, minReach, maxReach);
    vec2 dir = normalize(toTarget) * d;

    float cosT2 = clamp((d * d - L1 * L1 - L2 * L2) / (2.0 * L1 * L2), -1.0, 1.0);
    float theta2 = acos(cosT2);

    float bAngle = atan(dir.y, dir.x);
    float ik_alpha = atan(L2 * sin(theta2), L1 + L2 * cos(theta2));
    float theta1 = bAngle - elbowSign * ik_alpha;
    float theta2f = elbowSign * theta2; // negate with elbow flip to keep wrist on target

    vec2 elbow = base + L1 * vec2(cos(theta1), sin(theta1));
    vec2 wrist = elbow + L2 * vec2(cos(theta1 + theta2f), sin(theta1 + theta2f));

    // Gripper fingers — always point toward target, not along forearm
    vec2 fDir_ = normalize(target - base);
    float fAngle = atan(fDir_.y, fDir_.x);
    float fLen = 0.045 * sc;
    float fW = 0.008 * sw;
    float openA = 0.38 * (1.0 - grip * 0.85);
    vec2 f1End = wrist + fLen * vec2(cos(fAngle + openA), sin(fAngle + openA));
    vec2 f2End = wrist + fLen * vec2(cos(fAngle - openA), sin(fAngle - openA));

    // Output for laser
    outWrist = wrist;
    outFMid = (f1End + f2End) * 0.5;
    outFDir = fDir_;

    // Reach circle
    float rDist = abs(length(p - base) - maxReach);
    col = mix(col, accCol.rgb, smoothstep(px * 3.0, px, rDist) * 0.06);

    // Soft glow under joints
    float gE = exp(-35.0 * length(p - elbow));
    float gW = exp(-45.0 * length(p - wrist));
    float gB = exp(-35.0 * length(p - base));
    col += accCol.rgb * (gE + gW + gB) * 0.25;

    // Target crosshair
    float dCH = sdCapsule(p, target - vec2(0.02, 0.0), target + vec2(0.02, 0.0), 0.0008);
    float dCV = sdCapsule(p, target - vec2(0.0, 0.02), target + vec2(0.0, 0.02), 0.0008);
    col = mix(col, accCol.rgb, smoothstep(px * 2.0, 0.0, min(dCH, dCV)) * 0.45);

    // Target ring
    float dRing = abs(length(p - target) - 0.012) - 0.0008;
    col = mix(col, accCol.rgb, smoothstep(px * 2.0, 0.0, dRing) * 0.3);

    // ── Draw arm elements (back → front) ──
    float mask;
    vec3 elemCol;

    // Base pedestal
    elemCol = shadeCapsule(p, base - vec2(0.045, 0.0), base + vec2(0.045, 0.0), 0.032, aCol.rgb * 0.55, L, px, mask);
    col = mix(col, elemCol, mask);
    armMask = max(armMask, mask);

    // Upper arm
    elemCol = shadeCapsule(p, base, elbow, w1, aCol.rgb, L, px, mask);
    col = mix(col, elemCol, mask);
    armMask = max(armMask, mask);

    // Shoulder joint
    vec3 shoulderCol = mix(aCol.rgb, accCol.rgb, 0.35);
    elemCol = shadeSphere(p, base, jR, shoulderCol, L, px, mask);
    col = mix(col, elemCol, mask);
    armMask = max(armMask, mask);

    // Forearm
    elemCol = shadeCapsule(p, elbow, wrist, w2, aCol.rgb * 0.95, L, px, mask);
    col = mix(col, elemCol, mask);
    armMask = max(armMask, mask);

    // Elbow joint
    vec3 elbowCol = mix(aCol.rgb, accCol.rgb, 0.5);
    elemCol = shadeSphere(p, elbow, jR, elbowCol, L, px, mask);
    col = mix(col, elemCol, mask);
    armMask = max(armMask, mask);

    // Gripper finger 1
    elemCol = shadeCapsule(p, wrist, f1End, fW, accCol.rgb * 0.75, L, px, mask);
    col = mix(col, elemCol, mask);
    armMask = max(armMask, mask);

    // Gripper finger 2
    elemCol = shadeCapsule(p, wrist, f2End, fW, accCol.rgb * 0.75, L, px, mask);
    col = mix(col, elemCol, mask);
    armMask = max(armMask, mask);

    // Wrist joint (on top of fingers)
    vec3 wristCol = mix(aCol.rgb, accCol.rgb, 0.6);
    elemCol = shadeSphere(p, wrist, jR2, wristCol, L, px, mask);
    col = mix(col, elemCol, mask);
    armMask = max(armMask, mask);

    // Bright highlights on joints
    col += accCol.rgb * exp(-90.0 * length(p - elbow)) * 0.45;
    col += accCol.rgb * exp(-100.0 * length(p - wrist)) * 0.35;
    col += accCol.rgb * exp(-90.0 * length(p - base)) * 0.25;
}

// ── Laser beam from gripper tip ────────────────────────────

void drawLaser(vec2 p, vec2 origin, vec2 dir, float grip, vec3 beamColor, float px,
               inout vec3 col, inout float armMask) {
    if (grip < 0.05) return;

    float intensity = smoothstep(0.05, 0.5, grip);
    float beamLen = mix(0.05, 0.6, intensity);

    // Pulse/flicker
    float flicker = 0.85 + 0.15 * sin(TIME * 18.0 + origin.x * 40.0);
    float pulse = 0.9 + 0.1 * sin(TIME * 7.0);
    intensity *= flicker * pulse;

    // Ray SDF: distance from point to the ray segment
    vec2 tip = origin + dir * beamLen;
    vec2 po = p - origin;
    vec2 bo = tip - origin;
    float t = clamp(dot(po, bo) / dot(bo, bo), 0.0, 1.0);
    vec2 closest = origin + bo * t;
    float d = length(p - closest);

    // Taper: thinner at the tip
    float taper = mix(0.008, 0.002, t);

    // Core beam (bright, narrow)
    float core = smoothstep(taper + px, taper * 0.3, d) * intensity;
    col += beamColor * 1.8 * core;
    armMask = max(armMask, core * 0.6);

    // Inner glow
    float glow1 = exp(-d * 120.0 * mix(0.5, 1.5, t)) * intensity;
    col += beamColor * 0.7 * glow1;

    // Outer glow (wider, dimmer)
    float glow2 = exp(-d * 40.0 * mix(0.4, 1.0, t)) * intensity * 0.4;
    col += beamColor * glow2;

    // Hot spark at origin
    float spark = exp(-80.0 * length(p - origin)) * intensity;
    col += (beamColor + vec3(0.3)) * spark * 1.5;
}

// ── Main ────────────────────────────────────────────────────

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    vec2 p = (uv - 0.5) * vec2(aspect, 1.0);
    float px = 1.5 / RENDERSIZE.y;
    vec3 L = normalize(vec3(-0.4, 0.6, 0.8));

    // ── Arm count ──
    bool dualArms = armMode > 1.5;

    // ── Bases (must be defined before targets) ──
    vec2 base1, base2;
    if (dualArms) {
        base1 = vec2(-0.32, -0.38);
        base2 = vec2( 0.32, -0.38);
    } else {
        base1 = vec2(0.0, -0.38);
        base2 = vec2(0.0, -0.38);
    }

    // ── Idle animation: gentle Lissajous drift above base ──
    float t = TIME;
    vec2 idle1 = vec2(sin(t * 0.7) * 0.08, cos(t * 0.5) * 0.06 + 0.18);
    vec2 idle2 = vec2(sin(t * 0.6 + 2.1) * 0.08, cos(t * 0.45 + 1.3) * 0.06 + 0.18);
    float activity = clamp(inputActivity, 0.0, 1.0);

    // ── Targets ──
    // mpHandPos Y is already GL-friendly (flipped in manager), X needs mirror correction
    vec2 mouseTgt = (mousePos - 0.5) * vec2(aspect, 1.0);
    vec2 hand1Tgt = (vec2(1.0 - mpHandPos.x, mpHandPos.y) - 0.5) * vec2(aspect, 1.0);
    vec2 hand2Tgt = (vec2(1.0 - mpHandPos2.x, mpHandPos2.y) - 0.5) * vec2(aspect, 1.0);

    // Sort hands by screen X: lower X → left arm, higher X → right arm
    vec2 handL = hand1Tgt;
    vec2 handR = hand2Tgt;
    if (mpHandCount >= 1.5 && handL.x > handR.x) {
        vec2 tmp = handL;
        handL = handR;
        handR = tmp;
    }

    vec2 liveTgt1, liveTgt2;
    if (dualArms) {
        // Dual: left arm tracks left hand (or mouse), right arm tracks right hand (or mirrored mouse)
        liveTgt1 = (mpHandCount >= 1.5) ? handL : mouseTgt;
        liveTgt2 = (mpHandCount >= 1.5) ? handR : vec2(-mouseTgt.x, mouseTgt.y);
    } else {
        // Single: track any hand or mouse
        liveTgt1 = (mpHandCount > 0.5) ? hand1Tgt : mouseTgt;
        liveTgt2 = liveTgt1;
    }

    // Blend: live target when active, idle animation when no input
    vec2 target1 = mix(base1 + idle1, liveTgt1, activity);
    vec2 target2 = mix(base2 + idle2, liveTgt2, activity);

    // ── Background ──
    vec3 col = bgColor.rgb;
    float armMask = 0.0;

    // Dot grid
    if (showGrid) {
        float gs = 0.06;
        vec2 gp = abs(mod(p + gs * 0.5, gs) - gs * 0.5);
        float gDot = length(gp) - 0.0015;
        col = mix(col, accentColor.rgb, smoothstep(px, 0.0, gDot) * 0.10);
    }

    // Mouse click acts as pinch on desktop
    float grip = max(pinchHold, mouseDown);

    // ── Draw left arm (base1, elbow outward = -1) ──
    vec2 wrist1, fMid1, fDir1;
    float esign1 = dualArms ? -1.0 : 1.0; // single arm uses default elbow-up
    drawArm(p, base1, target1, grip, armScale, segWidth,
            armColor, accentColor, L, px, esign1,
            col, armMask, wrist1, fMid1, fDir1);

    // ── Draw right arm (base2, elbow outward = 1) ──
    vec2 wrist2, fMid2, fDir2;
    if (dualArms) {
        drawArm(p, base2, target2, grip, armScale, segWidth,
                armColor, accentColor, L, px, 1.0,
                col, armMask, wrist2, fMid2, fDir2);
    }

    // ── Laser beams on pinch ──
    vec3 beamColor = laserColor.rgb * 1.5 + vec3(0.15);
    drawLaser(p, fMid1, fDir1, grip, beamColor, px, col, armMask);
    if (dualArms) {
        drawLaser(p, fMid2, fDir2, grip, beamColor, px, col, armMask);
    }

    // ── Output ──
    float alpha = transparentBg ? armMask : 1.0;
    gl_FragColor = vec4(col, alpha);
}

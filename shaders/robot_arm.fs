/*{
  "DESCRIPTION": "Robot arm — 2-link inverse kinematics follows hand tracking or mouse. Up to 4 arms. Pinch to grip, hold to fire laser.",
  "CREDIT": "ShaderClaw",
  "CATEGORIES": ["Generator"],
  "INPUTS": [
    { "NAME": "armMode", "LABEL": "Arms", "TYPE": "long", "DEFAULT": 1, "VALUES": [1, 2, 3, 4], "LABELS": ["1", "2", "3", "4"] },
    { "NAME": "armColor", "LABEL": "Arm", "TYPE": "color", "DEFAULT": [1.0, 0.239, 0.239, 1.0] },
    { "NAME": "accentColor", "LABEL": "Accent", "TYPE": "color", "DEFAULT": [0.91, 0.25, 0.34, 1.0] },
    { "NAME": "laserColor", "LABEL": "Laser", "TYPE": "color", "DEFAULT": [0.91, 0.25, 0.34, 1.0] },
    { "NAME": "armScale", "LABEL": "Size", "TYPE": "float", "DEFAULT": 1.0, "MIN": 0.3, "MAX": 2.0 },
    { "NAME": "segWidth", "LABEL": "Thickness", "TYPE": "float", "DEFAULT": 1.0, "MIN": 0.3, "MAX": 2.5 },
    { "NAME": "showGrid", "LABEL": "Grid", "TYPE": "bool", "DEFAULT": true },
    { "NAME": "transparentBg", "LABEL": "Transparent", "TYPE": "bool", "DEFAULT": false },
    { "NAME": "bgColor", "LABEL": "Background", "TYPE": "color", "DEFAULT": [0.035, 0.035, 0.055, 1.0] }
  ]
}*/

float sdCapsule(vec2 p, vec2 a, vec2 b, float r) {
    vec2 pa = p - a, ba = b - a;
    float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
    return length(pa - ba * h) - r;
}

vec3 shadeCapsule(vec2 p, vec2 a, vec2 b, float r, vec3 color, vec3 L, float px, out float mask) {
    vec2 pa = p - a, ba = b - a;
    float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
    vec2 closest = a + ba * h;
    float dist = length(p - closest);
    mask = smoothstep(r + px, r - px, dist);
    if (mask < 0.001) return vec3(0.0);

    float t = clamp(dist / r, 0.0, 1.0);
    float nz = sqrt(max(0.0, 1.0 - t * t));
    vec3 N = normalize(vec3((p - closest) / max(dist, 0.0001), nz));

    float diff = max(0.0, dot(N, L));
    float spec = pow(max(0.0, dot(reflect(-L, N), vec3(0.0, 0.0, 1.0))), 80.0);
    float rim = pow(1.0 - nz, 2.5);

    // Panel groove lines
    float groove = smoothstep(0.015, 0.0, abs(fract(h * 5.0 + 0.5) - 0.5) - 0.485) * 0.25;

    vec3 c = color * (1.0 - groove);
    return c * (0.12 + 0.58 * diff) + vec3(0.7) * spec + c * rim * 0.22;
}

vec3 shadeSphere(vec2 p, vec2 c, float r, vec3 color, vec3 L, float px, out float mask) {
    float dist = length(p - c);
    mask = smoothstep(r + px, r - px, dist);
    if (mask < 0.001) return vec3(0.0);

    float t = clamp(dist / r, 0.0, 1.0);
    float nz = sqrt(max(0.0, 1.0 - t * t));
    vec3 N = normalize(vec3((p - c) / max(dist, 0.0001), nz));

    float diff = max(0.0, dot(N, L));
    float spec = pow(max(0.0, dot(reflect(-L, N), vec3(0.0, 0.0, 1.0))), 100.0);

    // Single servo ring groove
    float ring = smoothstep(0.03, 0.0, abs(t - 0.55)) * 0.18;
    vec3 base = color * (1.0 - ring);

    return base * (0.10 + 0.55 * diff) + vec3(0.9) * spec + base * pow(1.0 - nz, 2.0) * 0.35;
}

void drawArm(vec2 p, vec2 base, vec2 target, float grip, float sc, float sw,
             vec4 aCol, vec4 accCol, vec3 L, float px, float elbowSign,
             inout vec3 col, inout float armMask,
             out vec2 outWrist, out vec2 outFDir,
             out vec2 outF1, out vec2 outF2, out vec2 outF3) {

    float L1 = 0.25 * sc;
    float L2 = 0.22 * sc;
    float w1 = 0.024 * sw;
    float w2 = 0.018 * sw;
    float jR = 0.028 * sw;

    // IK
    vec2 toTarget = target - base;
    float d = length(toTarget);
    float maxReach = L1 + L2 - 0.005;
    d = clamp(d, abs(L1 - L2) + 0.005, maxReach);
    vec2 dir = normalize(toTarget) * d;

    float cosT2 = clamp((d * d - L1 * L1 - L2 * L2) / (2.0 * L1 * L2), -1.0, 1.0);
    float theta2 = acos(cosT2);
    float bAngle = atan(dir.y, dir.x);
    float theta1 = bAngle - elbowSign * atan(L2 * sin(theta2), L1 + L2 * cos(theta2));
    float theta2f = elbowSign * theta2;

    vec2 elbow = base + L1 * vec2(cos(theta1), sin(theta1));
    vec2 wrist = elbow + L2 * vec2(cos(theta1 + theta2f), sin(theta1 + theta2f));

    // Claw geometry — 2-segment tapered talons
    vec2 fDir_ = normalize(target - base);
    float fAngle = atan(fDir_.y, fDir_.x);
    float clawBase = 0.032 * sc;
    float clawTip  = 0.028 * sc;
    float baseW = 0.010 * sw;
    float tipW  = 0.004 * sw;
    float openA = 0.40 * (1.0 - grip * 0.85);
    float hookA = 0.18 + grip * 0.28;

    vec2 f1Mid = wrist + clawBase * vec2(cos(fAngle + openA), sin(fAngle + openA));
    vec2 f1End = f1Mid + clawTip * vec2(cos(fAngle + openA - hookA), sin(fAngle + openA - hookA));
    vec2 f2Mid = wrist + clawBase * vec2(cos(fAngle - openA), sin(fAngle - openA));
    vec2 f2End = f2Mid + clawTip * vec2(cos(fAngle - openA + hookA), sin(fAngle - openA + hookA));
    vec2 f3Mid = wrist + 0.036 * sc * vec2(cos(fAngle), sin(fAngle));
    vec2 f3End = f3Mid + clawTip * vec2(cos(fAngle), sin(fAngle));

    outWrist = wrist; outFDir = fDir_;
    outF1 = f1End; outF2 = f2End; outF3 = f3End;

    // Piston geometry
    vec2 ax1 = normalize(elbow - base);
    vec2 perp1 = vec2(-ax1.y, ax1.x);
    vec2 pA1 = base + ax1 * L1 * 0.18 + perp1 * w1 * 0.55;
    vec2 pB1 = elbow - ax1 * L1 * 0.12 + perp1 * w1 * 0.55;
    vec2 ax2 = normalize(wrist - elbow);
    vec2 perp2 = vec2(-ax2.y, ax2.x);
    vec2 pA2 = elbow + ax2 * L2 * 0.2 + perp2 * w2 * 0.6;
    vec2 pB2 = wrist - ax2 * L2 * 0.15 + perp2 * w2 * 0.6;

    // BG effects (reach circle + glow)
    col = mix(col, accCol.rgb, smoothstep(px * 3.0, px, abs(length(p - base) - maxReach)) * 0.06);
    col += accCol.rgb * (exp(-35.0 * length(p - elbow)) + exp(-45.0 * length(p - wrist)) + exp(-35.0 * length(p - base))) * 0.2;

    // Target crosshair + ring
    float ch = min(sdCapsule(p, target - vec2(0.02, 0.0), target + vec2(0.02, 0.0), 0.0008),
                   sdCapsule(p, target - vec2(0.0, 0.02), target + vec2(0.0, 0.02), 0.0008));
    col = mix(col, accCol.rgb, smoothstep(px * 2.0, 0.0, ch) * 0.4);
    col = mix(col, accCol.rgb, smoothstep(px * 2.0, 0.0, abs(length(p - target) - 0.012) - 0.0008) * 0.3);

    float mask;
    vec3 ec;
    vec3 pistonCol = aCol.rgb * 0.6 + vec3(0.2);

    // Base mount
    ec = shadeCapsule(p, base - vec2(0.045, 0.0), base + vec2(0.045, 0.0), 0.032, aCol.rgb * 0.45, L, px, mask);
    col = mix(col, ec, mask); armMask = max(armMask, mask);

    // Upper arm piston
    ec = shadeCapsule(p, pA1, pB1, w1 * 0.18, pistonCol, L, px, mask);
    col = mix(col, ec, mask); armMask = max(armMask, mask);

    // Upper arm
    ec = shadeCapsule(p, base, elbow, w1, aCol.rgb, L, px, mask);
    col = mix(col, ec, mask); armMask = max(armMask, mask);

    // Shoulder joint
    ec = shadeSphere(p, base, jR, mix(aCol.rgb, accCol.rgb, 0.35), L, px, mask);
    col = mix(col, ec, mask); armMask = max(armMask, mask);

    // Forearm piston
    ec = shadeCapsule(p, pA2, pB2, w2 * 0.2, pistonCol, L, px, mask);
    col = mix(col, ec, mask); armMask = max(armMask, mask);

    // Forearm
    ec = shadeCapsule(p, elbow, wrist, w2, aCol.rgb * 0.95, L, px, mask);
    col = mix(col, ec, mask); armMask = max(armMask, mask);

    // Elbow joint
    ec = shadeSphere(p, elbow, jR, mix(aCol.rgb, accCol.rgb, 0.5), L, px, mask);
    col = mix(col, ec, mask); armMask = max(armMask, mask);

    // Claw bases (thicker)
    vec3 clawCol = mix(aCol.rgb, accCol.rgb, 0.45);
    ec = shadeCapsule(p, wrist, f1Mid, baseW, clawCol, L, px, mask);
    col = mix(col, ec, mask); armMask = max(armMask, mask);
    ec = shadeCapsule(p, wrist, f2Mid, baseW, clawCol, L, px, mask);
    col = mix(col, ec, mask); armMask = max(armMask, mask);
    ec = shadeCapsule(p, wrist, f3Mid, baseW, clawCol, L, px, mask);
    col = mix(col, ec, mask); armMask = max(armMask, mask);

    // Claw tips (thin, sharp)
    vec3 tipCol = accCol.rgb * 0.9;
    ec = shadeCapsule(p, f1Mid, f1End, tipW, tipCol, L, px, mask);
    col = mix(col, ec, mask); armMask = max(armMask, mask);
    ec = shadeCapsule(p, f2Mid, f2End, tipW, tipCol, L, px, mask);
    col = mix(col, ec, mask); armMask = max(armMask, mask);
    ec = shadeCapsule(p, f3Mid, f3End, tipW, tipCol, L, px, mask);
    col = mix(col, ec, mask); armMask = max(armMask, mask);

    // Wrist joint (on top of claws)
    ec = shadeSphere(p, wrist, jR * 0.75, mix(aCol.rgb, accCol.rgb, 0.6), L, px, mask);
    col = mix(col, ec, mask); armMask = max(armMask, mask);

    // Tip + joint highlights
    col += accCol.rgb * (exp(-160.0 * length(p - f1End)) + exp(-160.0 * length(p - f2End)) + exp(-160.0 * length(p - f3End))) * 0.4;
    col += accCol.rgb * (exp(-90.0 * length(p - elbow)) * 0.4 + exp(-100.0 * length(p - wrist)) * 0.3);
}

void drawLaser(vec2 p, vec2 origin, vec2 dir, float grip, vec3 beamColor, float px,
               inout vec3 col, inout float armMask) {
    if (grip < 0.05) return;

    float intensity = smoothstep(0.05, 0.5, grip);
    float beamLen = mix(0.05, 0.6, intensity) * (1.0 + audioBass * 1.5);
    intensity *= (0.85 + 0.15 * sin(TIME * (18.0 + audioHigh * 40.0) + origin.x * 40.0))
               * (0.9 + 0.1 * sin(TIME * 7.0));

    vec2 tip = origin + dir * beamLen;
    vec2 po = p - origin, bo = tip - origin;
    float t = clamp(dot(po, bo) / dot(bo, bo), 0.0, 1.0);
    float d = length(p - (origin + bo * t));
    float taper = mix(0.008, 0.002, t);

    float core = smoothstep(taper + px, taper * 0.3, d) * intensity;
    col += beamColor * 1.8 * core;
    armMask = max(armMask, core * 0.6);
    col += beamColor * (exp(-d * 120.0 * mix(0.5, 1.5, t)) * 0.7 + exp(-d * 40.0 * mix(0.4, 1.0, t)) * 0.4) * intensity;
    col += (beamColor + vec3(0.3)) * exp(-80.0 * length(p - origin)) * intensity * 1.5;
}

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    vec2 p = (uv - 0.5) * vec2(aspect, 1.0);
    float px = 1.5 / RENDERSIZE.y;
    vec3 L = normalize(vec3(-0.4, 0.6, 0.8));

    float numArms = armMode;

    vec2 base1, base2, base3, base4;
    if (numArms < 1.5) {
        base1 = vec2(0.0, -0.38);
        base2 = base1; base3 = base1; base4 = base1;
    } else if (numArms < 2.5) {
        base1 = vec2(-0.32, -0.38);
        base2 = vec2( 0.32, -0.38);
        base3 = base1; base4 = base2;
    } else if (numArms < 3.5) {
        base1 = vec2(-0.38, -0.38);
        base2 = vec2( 0.00, -0.38);
        base3 = vec2( 0.38, -0.38);
        base4 = base2;
    } else {
        // 4 arms: one in each corner, mirrored
        base1 = vec2(-0.55, -0.38);  // bottom-left
        base2 = vec2( 0.55, -0.38);  // bottom-right
        base3 = vec2(-0.55,  0.38);  // top-left
        base4 = vec2( 0.55,  0.38);  // top-right
    }

    float t = TIME;
    vec2 mouseTgt = (mousePos - 0.5) * vec2(aspect, 1.0);
    vec2 hand1Tgt = (vec2(1.0 - mpHandPos.x, mpHandPos.y) - 0.5) * vec2(aspect, 1.0);
    vec2 hand2Tgt = (vec2(1.0 - mpHandPos2.x, mpHandPos2.y) - 0.5) * vec2(aspect, 1.0);

    vec2 handL = hand1Tgt, handR = hand2Tgt;
    if (mpHandCount >= 1.5 && handL.x > handR.x) { vec2 tmp = handL; handL = handR; handR = tmp; }

    float activity = clamp(inputActivity, 0.0, 1.0);
    vec2 mm = vec2(-mouseTgt.x, mouseTgt.y);

    vec2 idle1, idle2, idle3, idle4;
    vec2 liveTgt1, liveTgt2, liveTgt3, liveTgt4;

    if (numArms < 1.5) {
        idle1 = vec2(sin(t * 0.7) * 0.08, cos(t * 0.50) * 0.06 + 0.18);
        idle2 = idle1; idle3 = idle1; idle4 = idle1;
        liveTgt1 = (mpHandCount > 0.5) ? hand1Tgt : mouseTgt;
        liveTgt2 = liveTgt1; liveTgt3 = liveTgt1; liveTgt4 = liveTgt1;
    } else if (numArms < 2.5) {
        idle1 = vec2(sin(t * 0.7) * 0.08, cos(t * 0.50) * 0.06 + 0.18);
        idle2 = vec2(sin(t * 0.6 + 2.1) * 0.08, cos(t * 0.45 + 1.3) * 0.06 + 0.18);
        idle3 = idle1; idle4 = idle2;
        liveTgt1 = (mpHandCount >= 1.5) ? handL : mouseTgt;
        liveTgt2 = (mpHandCount >= 1.5) ? handR : mm;
        liveTgt3 = liveTgt1; liveTgt4 = liveTgt2;
    } else if (numArms < 3.5) {
        idle1 = vec2(sin(t * 0.7) * 0.08, cos(t * 0.50) * 0.06 + 0.18);
        idle2 = vec2(sin(t * 0.6 + 2.1) * 0.08, cos(t * 0.45 + 1.3) * 0.06 + 0.18);
        idle3 = vec2(sin(t * 0.55 + 4.2) * 0.08, cos(t * 0.40 + 2.6) * 0.06 + 0.18);
        idle4 = idle2;
        liveTgt1 = (mpHandCount >= 1.5) ? handL : mouseTgt;
        liveTgt3 = (mpHandCount >= 1.5) ? handR : mm;
        liveTgt2 = (mpHandCount >= 1.5) ? (handL + handR) * 0.5 : mouseTgt * vec2(0.0, 1.0);
        liveTgt4 = liveTgt2;
    } else {
        // 4 arms in corners — top arms idle downward, quadrant mirror targeting
        idle1 = vec2(sin(t * 0.7) * 0.08, cos(t * 0.50) * 0.06 + 0.18);
        idle2 = vec2(sin(t * 0.6 + 2.1) * 0.08, cos(t * 0.45 + 1.3) * 0.06 + 0.18);
        idle3 = vec2(sin(t * 0.55 + 4.2) * 0.08, -(cos(t * 0.40 + 2.6) * 0.06 + 0.18));
        idle4 = vec2(sin(t * 0.65 + 5.8) * 0.08, -(cos(t * 0.48 + 3.9) * 0.06 + 0.18));
        if (mpHandCount >= 1.5) {
            // Top + bottom arms on each side converge on same hand
            liveTgt1 = handL;
            liveTgt2 = handR;
            liveTgt3 = handL;
            liveTgt4 = handR;
        } else {
            // Full quadrant mirroring with mouse
            liveTgt1 = mouseTgt;
            liveTgt2 = mm;
            liveTgt3 = vec2(mouseTgt.x, -mouseTgt.y);
            liveTgt4 = vec2(-mouseTgt.x, -mouseTgt.y);
        }
    }

    vec2 target1 = mix(base1 + idle1, liveTgt1, activity);
    vec2 target2 = mix(base2 + idle2, liveTgt2, activity);
    vec2 target3 = mix(base3 + idle3, liveTgt3, activity);
    vec2 target4 = mix(base4 + idle4, liveTgt4, activity);

    vec3 col = bgColor.rgb;
    float armMask = 0.0;

    if (showGrid) {
        vec2 gp = abs(mod(p + 0.03, 0.06) - 0.03);
        col = mix(col, accentColor.rgb, smoothstep(px, 0.0, length(gp) - 0.0015) * 0.10);
    }

    float grip = max(pinchHold, mouseDown);
    vec3 beamColor = laserColor.rgb * 1.5 + vec3(0.15);

    // Arm 1
    vec2 w1, fd1, f1a, f1b, f1c;
    drawArm(p, base1, target1, grip, armScale, segWidth,
            armColor, accentColor, L, px, (numArms < 1.5) ? 1.0 : -1.0,
            col, armMask, w1, fd1, f1a, f1b, f1c);
    float a1 = atan(fd1.y, fd1.x);
    drawLaser(p, f1c, fd1, grip, beamColor, px, col, armMask);
    drawLaser(p, f1a, vec2(cos(a1 + 0.25), sin(a1 + 0.25)), grip, beamColor * 0.8, px, col, armMask);
    drawLaser(p, f1b, vec2(cos(a1 - 0.25), sin(a1 - 0.25)), grip, beamColor * 0.8, px, col, armMask);

    // Arm 2
    if (numArms > 1.5) {
        vec2 w2, fd2, f2a, f2b, f2c;
        drawArm(p, base2, target2, grip, armScale, segWidth,
                armColor, accentColor, L, px, 1.0,
                col, armMask, w2, fd2, f2a, f2b, f2c);
        float a2 = atan(fd2.y, fd2.x);
        drawLaser(p, f2c, fd2, grip, beamColor, px, col, armMask);
        drawLaser(p, f2a, vec2(cos(a2 + 0.25), sin(a2 + 0.25)), grip, beamColor * 0.8, px, col, armMask);
        drawLaser(p, f2b, vec2(cos(a2 - 0.25), sin(a2 - 0.25)), grip, beamColor * 0.8, px, col, armMask);
    }

    // Arm 3
    if (numArms > 2.5) {
        vec2 w3, fd3, f3a, f3b, f3c;
        drawArm(p, base3, target3, grip, armScale, segWidth,
                armColor, accentColor, L, px, 1.0,
                col, armMask, w3, fd3, f3a, f3b, f3c);
        float a3 = atan(fd3.y, fd3.x);
        drawLaser(p, f3c, fd3, grip, beamColor, px, col, armMask);
        drawLaser(p, f3a, vec2(cos(a3 + 0.25), sin(a3 + 0.25)), grip, beamColor * 0.8, px, col, armMask);
        drawLaser(p, f3b, vec2(cos(a3 - 0.25), sin(a3 - 0.25)), grip, beamColor * 0.8, px, col, armMask);
    }

    // Arm 4
    if (numArms > 3.5) {
        vec2 w4, fd4, f4a, f4b, f4c;
        drawArm(p, base4, target4, grip, armScale, segWidth,
                armColor, accentColor, L, px, -1.0,
                col, armMask, w4, fd4, f4a, f4b, f4c);
        float a4 = atan(fd4.y, fd4.x);
        drawLaser(p, f4c, fd4, grip, beamColor, px, col, armMask);
        drawLaser(p, f4a, vec2(cos(a4 + 0.25), sin(a4 + 0.25)), grip, beamColor * 0.8, px, col, armMask);
        drawLaser(p, f4b, vec2(cos(a4 - 0.25), sin(a4 - 0.25)), grip, beamColor * 0.8, px, col, armMask);
    }

    gl_FragColor = vec4(col, transparentBg ? armMask : 1.0);
}

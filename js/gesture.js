// GestureProcessor — maps MediaPipe landmarks to shader parameters
// Reads from MediaPipeManager's _lastHandLandmarks, _lastHandLandmarks2,
// _lastFaceLandmarks, _lastPoseLandmarks (all Y-flipped: y=1 is top of frame)
//
// Always smooths — ease-in when tracking arrives, ease-out back to rest when lost.

class GestureProcessor {
  constructor() {
    // Rest pose: neutral center, medium scale, no glow/morph/alive
    this.rest = { x: 0.5, y: 0.5, pinch: 0.5, open: 0.0, morph: 0, boost: 1, alive: 0 };
    this.smooth = { x: 0.5, y: 0.5, pinch: 0.5, open: 0.0, morph: 0, boost: 1, alive: 0 };
    this.target = { x: 0.5, y: 0.5, pinch: 0.5, open: 0.0, morph: 0, boost: 1, alive: 0 };
    this.LERP_IN = 0.18;   // ease-in: responsive but not jittery
    this.LERP_OUT = 0.04;  // ease-out: slow graceful return to rest
    this.tracking = false;  // currently receiving data
    this.wasTracking = false;
    this.settled = true;    // true when smooth ≈ rest (nothing to apply)

    // Pinch-drag shape morph (compositor-level)
    this.morphValue = 1.0;   // 0=square, 1=full rectangle
    this._targetMorph = 1.0;
    this.textureScale = 1.0; // driven by slider
    this._wasPinching = false;
    this._pinchStartX = 0;
    this._pinchStartMorph = 1.0;

    // Derived signals — high-level gestures computed from raw landmarks, all 0-1
    this.derived = {
      // Hand
      pinchDist: 0, gripStrength: 0, fingerSpread: 0, handAngle: 0.5,
      thumbCurl: 0, indexCurl: 0, middleCurl: 0, ringCurl: 0, pinkyCurl: 0,
      // Face
      headYaw: 0.5, headPitch: 0.5, headRoll: 0.5,
      mouthOpen: 0, leftBlink: 0, rightBlink: 0, eyebrowRaise: 0,
      // Pose
      bodyLean: 0.5, leftArmAngle: 0, rightArmAngle: 0, shoulderWidth: 0,
    };
    this._derivedTarget = { ...this.derived };
    this._DERIVED_LERP = 0.15;
  }

  update(mediaPipeMgr) {
    const mpActive = mediaPipeMgr && mediaPipeMgr.active;

    // --- Determine targets from landmarks (or rest pose) ---
    let gotData = false;

    if (mpActive) {
      // Hands
      const lm = mediaPipeMgr._lastHandLandmarks;
      if (lm && lm.length > 0) {
        gotData = true;
        const palm = lm[0];
        this.target.x = palm.x;
        this.target.y = palm.y;

        const thumb = lm[4], index = lm[8];
        const pinchDist = Math.hypot(thumb.x - index.x, thumb.y - index.y, (thumb.z - index.z) * 0.5);
        this.target.pinch = Math.min(1, pinchDist * 4);

        const middle = lm[12], ring = lm[16], pinky = lm[20];
        let td = 0;
        td += Math.hypot(index.x - palm.x, index.y - palm.y);
        td += Math.hypot(middle.x - palm.x, middle.y - palm.y);
        td += Math.hypot(ring.x - palm.x, ring.y - palm.y);
        td += Math.hypot(pinky.x - palm.x, pinky.y - palm.y);
        this.target.open = Math.min(1, td / 1.2);

        // Two-hand interaction
        const lm2 = mediaPipeMgr._lastHandLandmarks2;
        if (lm2 && lm2.length > 0) {
          this.target.alive = 1;
          const palm2 = lm2[0], thumb2 = lm2[4], index2 = lm2[8];
          const pinch1 = Math.hypot(thumb.x - index.x, thumb.y - index.y);
          const pinch2 = Math.hypot(thumb2.x - index2.x, thumb2.y - index2.y);
          const bothPinch = Math.max(0, 1 - Math.max(pinch1, pinch2) * 8);
          const handDist = Math.hypot(palm.x - palm2.x, palm.y - palm2.y);
          this.target.morph = bothPinch;
          this.target.boost = 1.0 + handDist * 4.0 * bothPinch;
        } else {
          this.target.alive = 0;
          this.target.morph = 0;
          this.target.boost = 1;
        }
      }

      // Face (used for rotation if no hands)
      const face = mediaPipeMgr._lastFaceLandmarks;
      if (face && face.length > 0) {
        const noseTip = face[4], forehead = face[10], chin = face[152];
        const leftCheek = face[234], rightCheek = face[454];
        const faceCX = (leftCheek.x + rightCheek.x) / 2;
        const faceCY = (forehead.y + chin.y) / 2;

        if (!gotData) {
          gotData = true;
          this.target.x = 0.5 + (noseTip.x - faceCX) * 3.5;
          this.target.y = 0.5 + (noseTip.y - faceCY) * 3.5;
          const faceW = Math.abs(rightCheek.x - leftCheek.x);
          this.target.pinch = Math.min(1, Math.max(0, (faceW - 0.08) * 3.5));
          const upperLip = face[13], lowerLip = face[14];
          const mouthGap = Math.abs(upperLip.y - lowerLip.y);
          const faceH = Math.abs(forehead.y - chin.y);
          this.target.open = Math.min(1, mouthGap / Math.max(faceH, 0.01) * 6);
        }
      }

      // Pose (used for rotation if no hands/face)
      const pose = mediaPipeMgr._lastPoseLandmarks;
      if (pose && pose.length > 0) {
        if (!gotData) {
          gotData = true;
          const lS = pose[11], rS = pose[12];
          const lH = pose[23], rH = pose[24];
          const shoulderDz = rS.z - lS.z;
          this.target.x = 0.5 + shoulderDz * 1.5;
          const shoulderMidZ = (lS.z + rS.z) / 2;
          const hipMidZ = (lH.z + rH.z) / 2;
          this.target.y = 0.5 + (shoulderMidZ - hipMidZ) * 1.2;
          const shoulderW = Math.abs(rS.x - lS.x);
          this.target.pinch = Math.min(1, Math.max(0, shoulderW * 3));
          const lW = pose[15], rW = pose[16];
          this.target.open = Math.min(1, Math.abs(rW.x - lW.x) * 1.8);
        }
      }
    }

    this.tracking = gotData;

    // When tracking is lost, ease targets back to rest pose
    if (!gotData) {
      const r = this.rest;
      this.target.x = r.x;
      this.target.y = r.y;
      this.target.pinch = r.pinch;
      this.target.open = r.open;
      this.target.morph = r.morph;
      this.target.boost = r.boost;
      this.target.alive = r.alive;
    }

    // --- Smooth interpolation (ALWAYS runs) ---
    // Use faster LERP when tracking, slower when easing out
    const L = gotData ? this.LERP_IN : this.LERP_OUT;
    const s = this.smooth, t = this.target;
    s.x += (t.x - s.x) * L;
    s.y += (t.y - s.y) * L;
    s.pinch += (t.pinch - s.pinch) * L;
    s.open += (t.open - s.open) * L;
    s.morph += (t.morph - s.morph) * L;
    s.boost += (t.boost - s.boost) * L;
    s.alive += (t.alive - s.alive) * (gotData ? 0.1 : 0.03);

    // --- Pinch-drag shape morph (compositor-level) ---
    if (mediaPipeMgr && mediaPipeMgr.isPinching && mediaPipeMgr.handCount > 0) {
      if (!this._wasPinching) {
        this._pinchStartX = mediaPipeMgr.pinchPos[0];
        this._pinchStartMorph = this.morphValue;
      }
      const dx = mediaPipeMgr.pinchPos[0] - this._pinchStartX;
      this._targetMorph = Math.max(0, Math.min(1, this._pinchStartMorph + dx * 2.5));
      this._wasPinching = true;
    } else {
      this._wasPinching = false;
    }
    const morphDiff = this._targetMorph - this.morphValue;
    this.morphValue += morphDiff * 0.12;

    // --- Derived signals ---
    this._computeDerived(mediaPipeMgr);

    // Check if we've settled back to rest (skip applying if nothing to do)
    const r = this.rest;
    const eps = 0.002;
    this.settled = !gotData
      && Math.abs(s.x - r.x) < eps
      && Math.abs(s.y - r.y) < eps
      && Math.abs(s.pinch - r.pinch) < eps
      && Math.abs(s.open - r.open) < eps
      && Math.abs(s.morph - r.morph) < eps
      && Math.abs(s.boost - r.boost) < eps
      && Math.abs(s.alive - r.alive) < eps
      && Math.abs(morphDiff) < 0.001;
  }

  _computeDerived(mediaPipeMgr) {
    const dt = this._derivedTarget;
    const mpActive = mediaPipeMgr && mediaPipeMgr.active;

    // --- Hand signals ---
    const lm = mpActive && mediaPipeMgr._lastHandLandmarks;
    if (lm && lm.length > 20) {
      const wrist = lm[0];
      const thumbTip = lm[4], indexTip = lm[8], middleTip = lm[12], ringTip = lm[16], pinkyTip = lm[20];
      const thumbMcp = lm[2], indexMcp = lm[5], middleMcp = lm[9], ringMcp = lm[13], pinkyMcp = lm[17];
      const indexPip = lm[6], middlePip = lm[10], ringPip = lm[14], pinkyPip = lm[18];
      const thumbIp = lm[3];

      // Pinch distance: thumb tip to index tip, normalized 0-1
      dt.pinchDist = Math.min(1, Math.hypot(thumbTip.x - indexTip.x, thumbTip.y - indexTip.y) * 5);

      // Finger curl: distance from tip to MCP relative to pip-to-MCP (1=curled, 0=extended)
      const _curl = (tip, pip, mcp) => {
        const extended = Math.hypot(pip.x - mcp.x, pip.y - mcp.y);
        const tipDist = Math.hypot(tip.x - mcp.x, tip.y - mcp.y);
        return Math.max(0, Math.min(1, 1 - tipDist / Math.max(extended * 2.5, 0.01)));
      };
      dt.thumbCurl = Math.max(0, Math.min(1, 1 - Math.hypot(thumbTip.x - thumbMcp.x, thumbTip.y - thumbMcp.y) / Math.max(Math.hypot(thumbIp.x - thumbMcp.x, thumbIp.y - thumbMcp.y) * 2.5, 0.01)));
      dt.indexCurl = _curl(indexTip, indexPip, indexMcp);
      dt.middleCurl = _curl(middleTip, middlePip, middleMcp);
      dt.ringCurl = _curl(ringTip, ringPip, ringMcp);
      dt.pinkyCurl = _curl(pinkyTip, pinkyPip, pinkyMcp);

      // Grip strength: average finger curl
      dt.gripStrength = (dt.thumbCurl + dt.indexCurl + dt.middleCurl + dt.ringCurl + dt.pinkyCurl) / 5;

      // Finger spread: average distance between adjacent fingertips, normalized
      const s1 = Math.hypot(indexTip.x - middleTip.x, indexTip.y - middleTip.y);
      const s2 = Math.hypot(middleTip.x - ringTip.x, middleTip.y - ringTip.y);
      const s3 = Math.hypot(ringTip.x - pinkyTip.x, ringTip.y - pinkyTip.y);
      dt.fingerSpread = Math.min(1, (s1 + s2 + s3) / 3 * 8);

      // Hand angle: wrist-to-middle-MCP angle in frame plane, normalized 0-1
      const dx = middleMcp.x - wrist.x;
      const dy = middleMcp.y - wrist.y;
      dt.handAngle = (Math.atan2(dy, dx) / Math.PI + 1) * 0.5; // 0-1
    }

    // --- Face signals ---
    const face = mpActive && mediaPipeMgr._lastFaceLandmarks;
    if (face && face.length > 454) {
      const noseTip = face[1], leftEar = face[234], rightEar = face[454];
      const forehead = face[10], chin = face[152];
      const leftEyeInner = face[133], leftEyeOuter = face[33];
      const rightEyeInner = face[362], rightEyeOuter = face[263];
      const upperLip = face[13], lowerLip = face[14];

      const faceCX = (leftEar.x + rightEar.x) / 2;
      const faceCY = (forehead.y + chin.y) / 2;
      const faceW = Math.abs(rightEar.x - leftEar.x);
      const faceH = Math.abs(forehead.y - chin.y);

      // Head yaw: nose offset from face center, 0.5=center
      dt.headYaw = 0.5 + (noseTip.x - faceCX) / Math.max(faceW, 0.01) * 0.8;
      dt.headYaw = Math.max(0, Math.min(1, dt.headYaw));

      // Head pitch: nose offset vertically, 0.5=center
      dt.headPitch = 0.5 + (noseTip.y - faceCY) / Math.max(faceH, 0.01) * 0.8;
      dt.headPitch = Math.max(0, Math.min(1, dt.headPitch));

      // Head roll: angle between eyes, 0.5=level
      const eyeLX = (leftEyeInner.x + leftEyeOuter.x) / 2;
      const eyeLY = (leftEyeInner.y + leftEyeOuter.y) / 2;
      const eyeRX = (rightEyeInner.x + rightEyeOuter.x) / 2;
      const eyeRY = (rightEyeInner.y + rightEyeOuter.y) / 2;
      const eyeAngle = Math.atan2(eyeRY - eyeLY, eyeRX - eyeLX);
      dt.headRoll = Math.max(0, Math.min(1, 0.5 + eyeAngle / (Math.PI * 0.5)));

      // Mouth open: jaw distance normalized by face height
      const mouthGap = Math.abs(upperLip.y - lowerLip.y);
      dt.mouthOpen = Math.min(1, mouthGap / Math.max(faceH, 0.01) * 6);

      // Eye aspect ratio (EAR) for blink detection
      // Left eye: landmarks 159(top), 145(bottom), 33(outer), 133(inner)
      const _ear = (top, bottom, inner, outer) => {
        const vDist = Math.abs(top.y - bottom.y);
        const hDist = Math.hypot(outer.x - inner.x, outer.y - inner.y);
        return Math.max(0, Math.min(1, 1 - vDist / Math.max(hDist * 0.4, 0.001)));
      };
      dt.leftBlink = _ear(face[159], face[145], face[133], face[33]);
      dt.rightBlink = _ear(face[386], face[374], face[362], face[263]);

      // Eyebrow raise: distance from brow to eye, normalized
      const leftBrow = face[70], rightBrow = face[300];
      const leftEye = face[159], rightEye = face[386];
      const browDist = ((leftBrow.y - leftEye.y) + (rightBrow.y - rightEye.y)) / 2;
      dt.eyebrowRaise = Math.max(0, Math.min(1, browDist / Math.max(faceH, 0.01) * 8));
    }

    // --- Pose signals ---
    const pose = mpActive && mediaPipeMgr._lastPoseLandmarks;
    if (pose && pose.length > 28) {
      const lS = pose[11], rS = pose[12]; // shoulders
      const lE = pose[13], rE = pose[14]; // elbows
      const lW = pose[15], rW = pose[16]; // wrists
      const lH = pose[23], rH = pose[24]; // hips

      // Body lean: shoulder midpoint X relative to hip midpoint X, 0.5=centered
      const shoulderMidX = (lS.x + rS.x) / 2;
      const hipMidX = (lH.x + rH.x) / 2;
      dt.bodyLean = Math.max(0, Math.min(1, 0.5 + (shoulderMidX - hipMidX) * 4));

      // Arm angle: elbow bend (0=straight, 1=fully bent)
      const _armAngle = (shoulder, elbow, wrist) => {
        const ux = shoulder.x - elbow.x, uy = shoulder.y - elbow.y;
        const vx = wrist.x - elbow.x, vy = wrist.y - elbow.y;
        const dot = ux * vx + uy * vy;
        const magU = Math.hypot(ux, uy), magV = Math.hypot(vx, vy);
        const cosAngle = dot / Math.max(magU * magV, 0.0001);
        // cosAngle=1 means straight (180°), cosAngle=-1 means fully bent (0°)
        return Math.max(0, Math.min(1, (1 - cosAngle) * 0.5));
      };
      dt.leftArmAngle = _armAngle(lS, lE, lW);
      dt.rightArmAngle = _armAngle(rS, rE, rW);

      // Shoulder width: normalized distance between shoulders
      dt.shoulderWidth = Math.min(1, Math.hypot(rS.x - lS.x, rS.y - lS.y) * 3);
    }

    // --- Smooth all derived signals ---
    const d = this.derived;
    const lerpF = this._DERIVED_LERP;
    for (const key in d) {
      d[key] += (dt[key] - d[key]) * lerpF;
    }
  }

  getValues() {
    const s = this.smooth;
    return {
      rotationX: (s.y - 0.5) * Math.PI * 2,
      rotationY: (s.x - 0.5) * Math.PI * 2,
      shapeScale: (0.25 + s.pinch * 0.9) * s.boost,
      glow: s.open,
      morph: s.morph,
      alive: s.alive,
    };
  }

  applyToLayer(layer) {
    if (!layer || this.settled) return;
    const vals = this.getValues();
    for (const key in vals) {
      layer.inputValues[key] = vals[key];
    }
  }
}

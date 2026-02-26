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

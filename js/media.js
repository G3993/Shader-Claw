// Media Management
// Handles media inputs, texture creation, variable fonts, MediaPipe, NDI client

import { state, emit } from './state.js';

// === Media Store ===

export function addMediaEntry(entry) {
  state.mediaInputs.push(entry);
  emit('media:added', entry);
  return entry;
}

export function removeMediaEntry(id) {
  const idx = state.mediaInputs.findIndex(m => m.id === id);
  if (idx === -1) return false;
  const entry = state.mediaInputs[idx];
  // Cleanup GL resources
  if (entry.glTexture) {
    // GL texture cleanup deferred to caller who has GL context
  }
  state.mediaInputs.splice(idx, 1);
  emit('media:removed', { id });
  return true;
}

export function getNextMediaId() {
  return ++state.mediaIdCounter;
}

// === Texture Creation ===

export function createGLTexture(gl, source) {
  const tex = gl.createTexture();
  gl.bindTexture(gl.TEXTURE_2D, tex);
  gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, source);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
  gl.bindTexture(gl.TEXTURE_2D, null);
  return tex;
}

// === Media Type Detection ===

export function detectMediaType(file) {
  const ext = file.name.split('.').pop().toLowerCase();
  if (['glb', 'gltf', 'stl', 'fbx', 'obj'].includes(ext)) return 'model';
  if (['mp3', 'wav', 'ogg'].includes(ext)) return 'audio';
  if (ext === 'svg') return 'svg';
  if (file.type.startsWith('video/')) return 'video';
  return 'image';
}

export function mediaTypeIcon(type, name) {
  if (type === 'video' && name === 'Webcam') return '\u{1F4F9}';
  if (type === 'video') return '\u{1F3AC}';
  if (type === 'model') return '\u{1F9CA}';
  if (type === 'audio') return '\u{1F50A}';
  if (type === 'svg') return '\u{2712}';
  return '\u{1F5BC}';
}

// === Variable Font System ===

let _vfCanvas = null;
let _vfCtx = null;
let _vfGLTexture = null;
let _vfLastMsg = '';
let _vfWeight = 400;
let _breatheStartTime = performance.now();
let _breathLastBucket = -1;

let _fontAtlasCanvas = null;
let _fontAtlasCtx = null;
let _fontAtlasGLTexture = null;
let _fontAtlasLastKey = '';

export const fontFamilies = [
  '"Inter", "Segoe UI Variable", "SF Pro", sans-serif',
  '"Times New Roman", "Times", Georgia, serif',
  '"Libre Caslon Text", "Palatino Linotype", "Book Antiqua", serif',
  '"Outfit", "Inter", "Segoe UI Variable", sans-serif',
];

function _getFontStack(inputValues) {
  const idx = Math.round(inputValues['fontFamily'] || 0);
  return fontFamilies[idx] || fontFamilies[0];
}

// Invalidate font cache when Google Fonts finish loading
if (typeof document !== 'undefined' && document.fonts) {
  document.fonts.ready.then(() => { _vfLastMsg = ''; _fontAtlasLastKey = ''; });
}

export function updateVarFontTexture(gl, inputValues) {
  const maxLen = 24;
  let msg = '';
  const msgLen = inputValues['msg_len'];
  const len = (msgLen != null && msgLen > 0) ? Math.min(msgLen, maxLen) : 0;
  for (let i = 0; i < len; i++) {
    const code = inputValues['msg_' + i];
    if (code == null || code === 26) msg += ' ';
    else if (code >= 0 && code <= 25) msg += String.fromCharCode(65 + code);
    else msg += ' ';
  }
  msg = msg.trim() || 'ETHEREA';

  const iw = inputValues['fontWeight'];
  if (iw != null) _vfWeight = Math.max(100, Math.min(900, iw));

  const fontStack = _getFontStack(inputValues);
  const key = msg + '|' + _vfWeight + '|' + fontStack;
  if (key === _vfLastMsg && _vfGLTexture) return;
  _vfLastMsg = key;

  if (!_vfCanvas) {
    _vfCanvas = document.createElement('canvas');
    _vfCanvas.width = 2048;
    _vfCanvas.height = 512;
    _vfCtx = _vfCanvas.getContext('2d');
  }

  const c = _vfCanvas;
  const ctx = _vfCtx;
  ctx.clearRect(0, 0, c.width, c.height);
  ctx.save();
  ctx.translate(0, c.height);
  ctx.scale(1, -1);
  const w = Math.round(_vfWeight);
  ctx.font = w + ' ' + Math.round(c.height * 0.35) + 'px ' + fontStack;
  ctx.fillStyle = '#ffffff';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';
  ctx.fillText(msg, c.width / 2, c.height / 2);
  ctx.restore();

  if (!_vfGLTexture) {
    _vfGLTexture = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, _vfGLTexture);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
  } else {
    gl.bindTexture(gl.TEXTURE_2D, _vfGLTexture);
  }
  gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, c);
}

export function updateBreathingTexture(gl, inputValues) {
  const maxLen = 24;
  let msg = '';
  const msgLen = inputValues['msg_len'];
  const len = (msgLen != null && msgLen > 0) ? Math.min(msgLen, maxLen) : 0;
  for (let i = 0; i < len; i++) {
    const code = inputValues['msg_' + i];
    if (code == null || code === 26) msg += ' ';
    else if (code >= 0 && code <= 25) msg += String.fromCharCode(65 + code);
    else msg += ' ';
  }
  msg = msg.trim() || 'ETHEREA';

  const fontStack = _getFontStack(inputValues);
  const spd = inputValues['speed'] != null ? inputValues['speed'] : 0.5;
  const intens = inputValues['intensity'] != null ? inputValues['intensity'] : 0.5;
  const elapsed = (performance.now() - _breatheStartTime) / 1000;

  const baseWeight = inputValues['fontWeight'] != null ? inputValues['fontWeight'] : 400;
  const spread = intens * 400;
  const minW = Math.max(100, baseWeight - spread);
  const maxW = Math.min(900, baseWeight + spread);

  const dens = inputValues['density'] != null ? inputValues['density'] : 0.5;
  const charDelay = 0.15 + (1.0 - dens) * 0.85;

  // Throttle breathing to 30fps — skip texImage2D when same frame bucket
  const breathBucket = Math.floor(elapsed * 30);
  if (breathBucket === _breathLastBucket && _vfGLTexture) return;
  _breathLastBucket = breathBucket;

  if (!_vfCanvas) {
    _vfCanvas = document.createElement('canvas');
    _vfCanvas.width = 2048;
    _vfCanvas.height = 512;
    _vfCtx = _vfCanvas.getContext('2d');
  }

  const c = _vfCanvas;
  const ctx = _vfCtx;
  ctx.clearRect(0, 0, c.width, c.height);
  ctx.save();
  ctx.translate(0, c.height);
  ctx.scale(1, -1);

  const fontSize = Math.round(c.height * 0.35);
  ctx.fillStyle = '#ffffff';
  ctx.textAlign = 'left';
  ctx.textBaseline = 'middle';

  const midWeight = Math.round((minW + maxW) / 2);
  ctx.font = midWeight + ' ' + fontSize + 'px ' + fontStack;
  const totalWidth = ctx.measureText(msg).width;
  let x = (c.width - totalWidth) / 2;

  for (let i = 0; i < msg.length; i++) {
    const phase = elapsed * spd * Math.PI * 2 - i * charDelay;
    const t = (Math.sin(phase) + 1) / 2;
    const w = Math.round(minW + t * (maxW - minW));
    ctx.font = w + ' ' + fontSize + 'px ' + fontStack;
    ctx.fillText(msg[i], x, c.height / 2);
    x += ctx.measureText(msg[i]).width;
  }

  ctx.restore();

  if (!_vfGLTexture) {
    _vfGLTexture = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, _vfGLTexture);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
  } else {
    gl.bindTexture(gl.TEXTURE_2D, _vfGLTexture);
  }
  gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, c);
  _vfLastMsg = '';
}

export function updateFontAtlas(gl, inputValues) {
  const fontFamilyIdx = Math.round(inputValues['fontFamily'] || 0);
  if (fontFamilyIdx < 0) return;

  const fontStack = fontFamilies[fontFamilyIdx] || fontFamilies[0];
  const weight = Math.round(inputValues['fontWeight'] || 400);
  const key = fontStack + '|' + weight;
  if (key === _fontAtlasLastKey && _fontAtlasGLTexture) return;
  _fontAtlasLastKey = key;

  const cellW = 128, cellH = 180;
  const totalW = 27 * cellW;

  if (!_fontAtlasCanvas || _fontAtlasCanvas.width !== totalW) {
    _fontAtlasCanvas = document.createElement('canvas');
    _fontAtlasCanvas.width = totalW;
    _fontAtlasCanvas.height = cellH;
    _fontAtlasCtx = _fontAtlasCanvas.getContext('2d');
  }

  const c = _fontAtlasCanvas;
  const ctx = _fontAtlasCtx;
  ctx.clearRect(0, 0, c.width, c.height);
  ctx.save();
  ctx.translate(0, c.height);
  ctx.scale(1, -1);

  const fontSize = Math.round(cellH * 0.85);
  ctx.font = weight + ' ' + fontSize + 'px ' + fontStack;
  ctx.fillStyle = '#ffffff';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'middle';

  for (let i = 0; i < 26; i++) {
    ctx.fillText(String.fromCharCode(65 + i), (i + 0.5) * cellW, cellH / 2);
  }

  ctx.restore();

  if (!_fontAtlasGLTexture) {
    _fontAtlasGLTexture = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, _fontAtlasGLTexture);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
  } else {
    gl.bindTexture(gl.TEXTURE_2D, _fontAtlasGLTexture);
  }
  gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, c);
}

/**
 * Get font state object for passing to renderer
 */
export function getFontState() {
  return {
    vfGLTexture: _vfGLTexture,
    fontAtlasGLTexture: _fontAtlasGLTexture,
    fontFamilies,
    updateVarFont: updateVarFontTexture,
    updateBreathing: updateBreathingTexture,
    updateFontAtlas: updateFontAtlas,
  };
}

// === MediaPipe Manager ===

export class MediaPipeManager {
  constructor(gl) {
    this.gl = gl;
    this.active = false;
    this.modes = { hand: false, face: false, pose: false, segment: false };
    this.handLandmarker = null;
    this.faceLandmarker = null;
    this.poseLandmarker = null;
    this.imageSegmenter = null;
    this.handTex = null;
    this.faceTex = null;
    this.poseTex = null;
    this.segTex = null;
    this.handCount = 0;
    this.handPos = [0, 0, 0];
    this.isPinching = false;
    this.pinchPos = [0, 0];
    this._pinchStartPos = null;
    this._pinchAccumX = 0;
    this._pinchAccumY = 0;
    this._lastPinchPos = null;
    this._lastHandLandmarks2 = null;
    this._frameCount = 0;
    this._lastTimestamp = 0;
  }

  async init(modes) {
    if (!window.MediaPipeVision) throw new Error('MediaPipe not loaded yet');
    const { FilesetResolver, HandLandmarker, FaceLandmarker, PoseLandmarker, ImageSegmenter } = window.MediaPipeVision;
    const wasmPath = 'https://cdn.jsdelivr.net/npm/@mediapipe/tasks-vision@0.10.32/wasm';
    const vision = await FilesetResolver.forVisionTasks(wasmPath);

    this.modes = { ...this.modes, ...modes };

    if (this.modes.hand) {
      this.handLandmarker = await HandLandmarker.createFromOptions(vision, {
        baseOptions: { modelAssetPath: 'https://storage.googleapis.com/mediapipe-models/hand_landmarker/hand_landmarker/float16/1/hand_landmarker.task', delegate: 'GPU' },
        runningMode: 'VIDEO', numHands: 2
      });
      this.handTex = this._createDataTex(42, 1);
    }
    if (this.modes.face) {
      this.faceLandmarker = await FaceLandmarker.createFromOptions(vision, {
        baseOptions: { modelAssetPath: 'https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/1/face_landmarker.task', delegate: 'GPU' },
        runningMode: 'VIDEO', numFaces: 1
      });
      this.faceTex = this._createDataTex(478, 1);
    }
    if (this.modes.pose) {
      this.poseLandmarker = await PoseLandmarker.createFromOptions(vision, {
        baseOptions: { modelAssetPath: 'https://storage.googleapis.com/mediapipe-models/pose_landmarker/pose_landmarker_lite/float16/1/pose_landmarker_lite.task', delegate: 'GPU' },
        runningMode: 'VIDEO'
      });
      this.poseTex = this._createDataTex(33, 1);
    }
    if (this.modes.segment) {
      this.imageSegmenter = await ImageSegmenter.createFromOptions(vision, {
        baseOptions: { modelAssetPath: 'https://storage.googleapis.com/mediapipe-models/image_segmenter/selfie_segmenter/float16/latest/selfie_segmenter.tflite', delegate: 'GPU' },
        runningMode: 'VIDEO', outputCategoryMask: false, outputConfidenceMasks: true
      });
    }
    this.active = true;
  }

  _createDataTex(w, h) {
    const gl = this.gl;
    const tex = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, tex);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, w, h, 0, gl.RGBA, gl.UNSIGNED_BYTE, null);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    return tex;
  }

  detect(video, timestamp) {
    if (!this.active || !video || video.readyState < 2) return;
    this._frameCount++;
    if (this._frameCount % 4 !== 0) return;
    if (timestamp <= this._lastTimestamp) timestamp = this._lastTimestamp + 1;
    this._lastTimestamp = timestamp;

    const gl = this.gl;

    if (this.handLandmarker) {
      const result = this.handLandmarker.detectForVideo(video, timestamp);
      this.handCount = result.landmarks ? result.landmarks.length : 0;
      if (this.handCount > 0) {
        const lm = result.landmarks[0][9];
        this.handPos = [lm.x, 1.0 - lm.y, lm.z];

        const thumb = result.landmarks[0][4];
        const index = result.landmarks[0][8];
        const dx = thumb.x - index.x;
        const dy = thumb.y - index.y;
        const dz = thumb.z - index.z;
        const pinchDist = Math.sqrt(dx * dx + dy * dy + dz * dz);
        const wasPinching = this.isPinching;
        this.isPinching = pinchDist < 0.05;
        this.pinchPos = [(thumb.x + index.x) / 2, 1.0 - (thumb.y + index.y) / 2];

        if (this.isPinching && !wasPinching) {
          this._pinchStartPos = [...this.pinchPos];
          this._pinchAccumX = 0;
          this._pinchAccumY = 0;
        }
        if (this.isPinching && this._pinchStartPos) {
          this._pinchAccumX += (this.pinchPos[0] - (this._lastPinchPos || this.pinchPos)[0]) * Math.PI * 4;
          this._pinchAccumY += (this.pinchPos[1] - (this._lastPinchPos || this.pinchPos)[1]) * Math.PI * 4;
        }
        this._lastPinchPos = [...this.pinchPos];

        // Store raw landmarks for binding resolution
        this._lastHandLandmarks = result.landmarks[0].map(p => ({ x: p.x, y: 1.0 - p.y, z: p.z }));
        // Store second hand landmarks for two-hand gesture processing
        this._lastHandLandmarks2 = result.landmarks.length >= 2
          ? result.landmarks[1].map(p => ({ x: p.x, y: 1.0 - p.y, z: p.z }))
          : null;

        const data = new Uint8Array(42 * 4);
        for (let h = 0; h < Math.min(2, result.landmarks.length); h++) {
          for (let i = 0; i < 21; i++) {
            const idx = (h * 21 + i) * 4;
            const p = result.landmarks[h][i];
            data[idx] = Math.round(p.x * 255);
            data[idx + 1] = Math.round((1.0 - p.y) * 255);
            data[idx + 2] = Math.round((p.z + 0.5) * 255);
            data[idx + 3] = 255;
          }
        }
        gl.bindTexture(gl.TEXTURE_2D, this.handTex);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, 42, 1, 0, gl.RGBA, gl.UNSIGNED_BYTE, data);
      } else {
        this.handPos = [0, 0, 0];
        this.isPinching = false;
        this._lastHandLandmarks2 = null;
      }
    }

    if (this.faceLandmarker) {
      const result = this.faceLandmarker.detectForVideo(video, timestamp);
      if (result.faceLandmarks && result.faceLandmarks.length > 0) {
        this._lastFaceLandmarks = result.faceLandmarks[0].map(p => ({ x: p.x, y: 1.0 - p.y, z: p.z }));
        const data = new Uint8Array(478 * 4);
        for (let i = 0; i < Math.min(478, result.faceLandmarks[0].length); i++) {
          const p = result.faceLandmarks[0][i];
          data[i * 4] = Math.round(p.x * 255);
          data[i * 4 + 1] = Math.round((1.0 - p.y) * 255);
          data[i * 4 + 2] = Math.round((p.z + 0.5) * 255);
          data[i * 4 + 3] = 255;
        }
        gl.bindTexture(gl.TEXTURE_2D, this.faceTex);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, 478, 1, 0, gl.RGBA, gl.UNSIGNED_BYTE, data);
      }
    }

    if (this.poseLandmarker) {
      const result = this.poseLandmarker.detectForVideo(video, timestamp);
      if (result.landmarks && result.landmarks.length > 0) {
        this._lastPoseLandmarks = result.landmarks[0].map(p => ({ x: p.x, y: 1.0 - p.y, z: p.z }));
        const data = new Uint8Array(33 * 4);
        for (let i = 0; i < 33; i++) {
          const p = result.landmarks[0][i];
          data[i * 4] = Math.round(p.x * 255);
          data[i * 4 + 1] = Math.round((1.0 - p.y) * 255);
          data[i * 4 + 2] = Math.round((p.z + 0.5) * 255);
          data[i * 4 + 3] = Math.round((p.visibility || 0) * 255);
        }
        gl.bindTexture(gl.TEXTURE_2D, this.poseTex);
        gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, 33, 1, 0, gl.RGBA, gl.UNSIGNED_BYTE, data);
      }
    }

    if (this.imageSegmenter) {
      this.imageSegmenter.segmentForVideo(video, timestamp, (result) => {
        if (result.confidenceMasks && result.confidenceMasks.length > 0) {
          const mask = result.confidenceMasks[0];
          const w = mask.width, h = mask.height;
          const data = new Uint8Array(w * h * 4);
          const raw = mask.getAsFloat32Array();
          for (let i = 0; i < raw.length; i++) {
            const v = Math.round(raw[i] * 255);
            data[i * 4] = data[i * 4 + 1] = data[i * 4 + 2] = v;
            data[i * 4 + 3] = 255;
          }
          if (!this.segTex) this.segTex = this._createDataTex(w, h);
          gl.bindTexture(gl.TEXTURE_2D, this.segTex);
          gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, w, h, 0, gl.RGBA, gl.UNSIGNED_BYTE, data);
        }
      });
    }
  }

  getLabel() {
    const parts = [];
    if (this.modes.hand) parts.push('hand');
    if (this.modes.face) parts.push('face');
    if (this.modes.pose) parts.push('pose');
    if (this.modes.segment) parts.push('segment');
    return 'MediaPipe (' + parts.join('+') + ')';
  }

  dispose() {
    if (this.handLandmarker) this.handLandmarker.close();
    if (this.faceLandmarker) this.faceLandmarker.close();
    if (this.poseLandmarker) this.poseLandmarker.close();
    if (this.imageSegmenter) this.imageSegmenter.close();
    this.active = false;
  }
}

// === NDI Client ===

let ndiReceiveEntry = null;
let ndiReceiveCanvas = null;
let ndiReceiveCtx = null;
let ndiSendingActive = false;
let ndiSendAnimId = null;
let ndiSendFrameCount = 0;
let ndiSendWorker = null;
let ndiWsMsgId = 0;
const ndiPending = new Map();
export const FRAME_TYPE_NDI_VIDEO = 0x01;
export const FRAME_TYPE_CANVAS = 0x02;

export function ndiRequest(ws, action, params = {}) {
  return new Promise((resolve, reject) => {
    if (!ws || ws.readyState !== WebSocket.OPEN) {
      return reject(new Error('WebSocket not connected'));
    }
    const id = --ndiWsMsgId;
    const timer = setTimeout(() => {
      ndiPending.delete(id);
      reject(new Error('NDI request timeout'));
    }, 5000);
    ndiPending.set(id, { resolve, reject, timer });
    ws.send(JSON.stringify({ id, action, params }));
  });
}

export function handleNdiResponse(msg) {
  const entry = ndiPending.get(msg.id);
  if (!entry) return false;
  clearTimeout(entry.timer);
  ndiPending.delete(msg.id);
  if (msg.error) entry.reject(new Error(msg.error));
  else entry.resolve(msg.result);
  return true;
}

export function handleNdiVideoFrame(data, gl) {
  if (!ndiReceiveEntry) return;
  const view = new DataView(data);
  const width = view.getUint32(1, true);
  const height = view.getUint32(5, true);
  const pixels = new Uint8ClampedArray(data, 9);

  if (!ndiReceiveCanvas || ndiReceiveCanvas.width !== width || ndiReceiveCanvas.height !== height) {
    ndiReceiveCanvas = document.createElement('canvas');
    ndiReceiveCanvas.width = width;
    ndiReceiveCanvas.height = height;
    ndiReceiveCtx = ndiReceiveCanvas.getContext('2d');
  }

  const imageData = new ImageData(pixels, width, height);
  ndiReceiveCtx.putImageData(imageData, 0, 0);

  if (ndiReceiveEntry.glTexture && gl) {
    gl.bindTexture(gl.TEXTURE_2D, ndiReceiveEntry.glTexture);
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, ndiReceiveCanvas);
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
  }
  if (ndiReceiveEntry.threeTexture) {
    ndiReceiveEntry.threeTexture.needsUpdate = true;
  }
}

export function setNdiReceiveEntry(entry) { ndiReceiveEntry = entry; }
export function getNdiReceiveCanvas() { return ndiReceiveCanvas; }
export function isNdiSending() { return ndiSendingActive; }

export function createNdiWorker() {
  const blob = new Blob([`
    self.onmessage = async (e) => {
      const { bitmap, width, height } = e.data;
      const oc = new OffscreenCanvas(width, height);
      const ctx = oc.getContext('2d');
      ctx.drawImage(bitmap, 0, 0, width, height);
      bitmap.close();
      const imageData = ctx.getImageData(0, 0, width, height);
      const header = new Uint8Array(9);
      header[0] = 0x02;
      new DataView(header.buffer).setUint32(1, width, true);
      new DataView(header.buffer).setUint32(5, height, true);
      const msg = new Uint8Array(9 + imageData.data.length);
      msg.set(header);
      msg.set(imageData.data, 9);
      self.postMessage(msg.buffer, [msg.buffer]);
    };
  `], { type: 'application/javascript' });
  return new Worker(URL.createObjectURL(blob));
}

export function startNdiSend(ws, glCanvas) {
  if (ndiSendingActive) return;
  ndiSendingActive = true;
  ndiSendWorker = createNdiWorker();
  ndiSendWorker.onmessage = (e) => {
    if (ws && ws.readyState === WebSocket.OPEN) {
      ws.send(e.data);
    }
  };
  const sendW = 960, sendH = 540;
  function captureLoop() {
    if (!ndiSendingActive) return;
    ndiSendAnimId = requestAnimationFrame(captureLoop);
    ndiSendFrameCount++;
    if (ndiSendFrameCount % 2 !== 0) return; // ~30fps
    createImageBitmap(glCanvas, { resizeWidth: sendW, resizeHeight: sendH }).then(bitmap => {
      if (!ndiSendingActive) { bitmap.close(); return; }
      ndiSendWorker.postMessage({ bitmap, width: sendW, height: sendH }, [bitmap]);
    }).catch(() => {});
  }
  captureLoop();
}

export function stopNdiSend() {
  ndiSendingActive = false;
  if (ndiSendAnimId) cancelAnimationFrame(ndiSendAnimId);
  if (ndiSendWorker) ndiSendWorker.terminate();
  ndiSendWorker = null;
}

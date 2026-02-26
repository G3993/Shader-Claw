// WebGL Renderer — extracted from ShaderClaw monolith, adapted for 7-layer compositor
// Handles shader compilation, FBO management, layer rendering, multi-pass, and compositing

import { VERT_SHADER } from './isf.js';

// =======================================================================
// MediaPipe Body Part Registry — named parts → landmark indices
// =======================================================================
export const MP_BODY_PARTS = {
  hand: [
    { name: 'Wrist', index: 0 },
    { name: 'Thumb Tip', index: 4 },
    { name: 'Index Tip', index: 8 },
    { name: 'Middle Tip', index: 12 },
    { name: 'Ring Tip', index: 16 },
    { name: 'Pinky Tip', index: 20 },
    { name: 'Palm Center', index: 9 },
  ],
  face: [
    { name: 'Nose Tip', index: 1 },
    { name: 'Left Eye', index: 33 },
    { name: 'Right Eye', index: 263 },
    { name: 'Mouth Center', index: 13 },
    { name: 'Chin', index: 152 },
    { name: 'Forehead', index: 10 },
    { name: 'Left Ear', index: 234 },
    { name: 'Right Ear', index: 454 },
    { name: 'Left Eyebrow', index: 70 },
    { name: 'Right Eyebrow', index: 300 },
    { name: 'Upper Lip', index: 0 },
    { name: 'Lower Lip', index: 17 },
    { name: 'Left Cheek', index: 123 },
    { name: 'Right Cheek', index: 352 },
    { name: 'Nose Bridge', index: 6 },
  ],
  pose: [
    { name: 'Nose', index: 0 },
    { name: 'Left Shoulder', index: 11 },
    { name: 'Right Shoulder', index: 12 },
    { name: 'Left Elbow', index: 13 },
    { name: 'Right Elbow', index: 14 },
    { name: 'Left Wrist', index: 15 },
    { name: 'Right Wrist', index: 16 },
    { name: 'Left Hip', index: 23 },
    { name: 'Right Hip', index: 24 },
    { name: 'Left Knee', index: 25 },
    { name: 'Right Knee', index: 26 },
    { name: 'Left Ankle', index: 27 },
    { name: 'Right Ankle', index: 28 },
    { name: 'Left Eye Inner', index: 1 },
    { name: 'Left Eye', index: 2 },
    { name: 'Left Eye Outer', index: 3 },
    { name: 'Right Eye Inner', index: 4 },
    { name: 'Right Eye', index: 5 },
    { name: 'Right Eye Outer', index: 6 },
    { name: 'Left Ear', index: 7 },
    { name: 'Right Ear', index: 8 },
    { name: 'Left Pinky', index: 17 },
    { name: 'Right Pinky', index: 18 },
    { name: 'Left Index', index: 19 },
    { name: 'Right Index', index: 20 },
    { name: 'Left Thumb', index: 21 },
    { name: 'Right Thumb', index: 22 },
    { name: 'Left Heel', index: 29 },
    { name: 'Right Heel', index: 30 },
    { name: 'Left Foot Index', index: 31 },
    { name: 'Right Foot Index', index: 32 },
  ],
};

export class Renderer {
  constructor(canvas) {
    this.canvas = canvas;
    this.gl = canvas.getContext('webgl', { antialias: false, preserveDrawingBuffer: true });
    if (!this.gl) throw new Error('WebGL not supported');

    this.program = null;
    this.uniformLocs = {};
    this.inputValues = {};
    this.startTime = performance.now();
    this.frameIndex = 0;
    this.playing = true;
    this.animId = null;
    this.textures = {};
    this._bgProgram = null;
    this._bgUniformLocs = {};
    this._bgInputValues = null;

    // Mouse state
    this.mousePos = [0.5, 0.5];
    this.mouseDelta = [0, 0];
    this._lastMousePos = [0.5, 0.5];

    // Compositor
    this.compositorProgram = null;
    this.compositorLocs = {};

    this._initGeometry();
    this._initDefaultTex();
    this.resize();
  }

  _initGeometry() {
    const gl = this.gl;
    const buf = gl.createBuffer();
    gl.bindBuffer(gl.ARRAY_BUFFER, buf);
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 3, -1, -1, 3]), gl.STATIC_DRAW);
    this.posBuf = buf;
  }

  _initDefaultTex() {
    const gl = this.gl;
    this._defaultTex = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, this._defaultTex);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, 1, 1, 0, gl.RGBA, gl.UNSIGNED_BYTE, new Uint8Array([0, 0, 0, 255]));
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
  }

  resize() {
    this.canvas.width = 1920;
    this.canvas.height = 1080;
    this.gl.viewport(0, 0, 1920, 1080);
  }

  reinitGL() {
    const gl = this.gl;
    this._initGeometry();
    this._initDefaultTex();
    this.program = null;
    this.uniformLocs = {};
    this._ppFloatChecked = undefined;
    this.resize();
  }

  // === Shader Compilation ===

  compile(vertSrc, fragSrc) {
    const gl = this.gl;
    const vs = this._compileShader(gl.VERTEX_SHADER, vertSrc);
    if (!vs.shader) return { ok: false, errors: 'Vertex: ' + vs.log };

    const fs = this._compileShader(gl.FRAGMENT_SHADER, fragSrc);
    if (!fs.shader) {
      gl.deleteShader(vs.shader);
      return { ok: false, errors: this._prettyErrors(fs.log) };
    }

    const prog = gl.createProgram();
    gl.attachShader(prog, vs.shader);
    gl.attachShader(prog, fs.shader);
    gl.bindAttribLocation(prog, 0, 'position');
    gl.linkProgram(prog);
    gl.deleteShader(vs.shader);
    gl.deleteShader(fs.shader);

    if (!gl.getProgramParameter(prog, gl.LINK_STATUS)) {
      const log = gl.getProgramInfoLog(prog);
      gl.deleteProgram(prog);
      return { ok: false, errors: 'Link: ' + log };
    }

    if (this.program) gl.deleteProgram(this.program);
    this.program = prog;
    this.uniformLocs = {};
    return { ok: true, errors: null };
  }

  _compileShader(type, src) {
    const gl = this.gl;
    if (gl.isContextLost()) return { shader: null, log: 'WebGL context lost' };
    const s = gl.createShader(type);
    if (!s) return { shader: null, log: 'WebGL context lost (createShader returned null)' };
    gl.shaderSource(s, src);
    gl.compileShader(s);
    if (!gl.getShaderParameter(s, gl.COMPILE_STATUS)) {
      const log = gl.getShaderInfoLog(s);
      gl.deleteShader(s);
      return { shader: null, log };
    }
    return { shader: s, log: null };
  }

  _prettyErrors(log) {
    if (!log) return '';
    const headerLines = this._headerLines || 14;
    return log.replace(/ERROR:\s*\d+:(\d+)/g, (m, line) => {
      const adjusted = Math.max(1, parseInt(line) - headerLines);
      return `Line ${adjusted}`;
    });
  }

  _getLoc(name) {
    if (!(name in this.uniformLocs)) {
      this.uniformLocs[name] = this.gl.getUniformLocation(this.program, name);
    }
    return this.uniformLocs[name];
  }

  // === Background Shader ===

  _getBgLoc(name) {
    if (!(name in this._bgUniformLocs)) {
      this._bgUniformLocs[name] = this.gl.getUniformLocation(this._bgProgram, name);
    }
    return this._bgUniformLocs[name];
  }

  compileBg(vertSrc, fragSrc) {
    const gl = this.gl;
    if (this._bgProgram) { gl.deleteProgram(this._bgProgram); this._bgProgram = null; }
    this._bgUniformLocs = {};
    if (!fragSrc) return { ok: true };

    const vs = this._compileShader(gl.VERTEX_SHADER, vertSrc);
    if (!vs.shader) return { ok: false, errors: 'BG Vertex: ' + vs.log };
    const fs = this._compileShader(gl.FRAGMENT_SHADER, fragSrc);
    if (!fs.shader) { gl.deleteShader(vs.shader); return { ok: false, errors: 'BG: ' + fs.log }; }
    const prog = gl.createProgram();
    gl.attachShader(prog, vs.shader);
    gl.attachShader(prog, fs.shader);
    gl.bindAttribLocation(prog, 0, 'position');
    gl.linkProgram(prog);
    gl.deleteShader(vs.shader);
    gl.deleteShader(fs.shader);
    if (!gl.getProgramParameter(prog, gl.LINK_STATUS)) {
      gl.deleteProgram(prog);
      return { ok: false, errors: 'BG Link failed' };
    }
    this._bgProgram = prog;
    return { ok: true };
  }

  _renderBg(audioState) {
    const gl = this.gl;
    if (!this._bgProgram) return;

    gl.useProgram(this._bgProgram);
    const elapsed = (performance.now() - this.startTime) / 1000;
    const tLoc = this._getBgLoc('TIME');
    if (tLoc) gl.uniform1f(tLoc, elapsed);
    const rLoc = this._getBgLoc('RENDERSIZE');
    if (rLoc) gl.uniform2f(rLoc, this.canvas.width, this.canvas.height);
    const pLoc = this._getBgLoc('PASSINDEX');
    if (pLoc) gl.uniform1i(pLoc, 0);
    const fLoc = this._getBgLoc('FRAMEINDEX');
    if (fLoc) gl.uniform1i(fLoc, this.frameIndex);

    if (this._bgInputValues) {
      for (const [name, val] of Object.entries(this._bgInputValues)) {
        const loc = this._getBgLoc(name);
        if (!loc) continue;
        if (typeof val === 'number') gl.uniform1f(loc, val);
        else if (typeof val === 'boolean') gl.uniform1i(loc, val ? 1 : 0);
        else if (Array.isArray(val)) {
          if (val.length === 2) gl.uniform2f(loc, val[0], val[1]);
          else if (val.length === 4) gl.uniform4f(loc, val[0], val[1], val[2], val[3]);
        }
      }
    }

    // Audio uniforms for bg shader
    if (audioState && audioState.fftGLTexture) {
      gl.activeTexture(gl.TEXTURE0);
      gl.bindTexture(gl.TEXTURE_2D, audioState.fftGLTexture);
      const aLoc = this._getBgLoc('audioFFT');
      if (aLoc) gl.uniform1i(aLoc, 0);
    }
    if (audioState) {
      const bgAl = this._getBgLoc('audioLevel');
      if (bgAl) gl.uniform1f(bgAl, audioState.level);
      const bgAb = this._getBgLoc('audioBass');
      if (bgAb) gl.uniform1f(bgAb, audioState.bass);
      const bgAm = this._getBgLoc('audioMid');
      if (bgAm) gl.uniform1f(bgAm, audioState.mid);
      const bgAh = this._getBgLoc('audioHigh');
      if (bgAh) gl.uniform1f(bgAh, audioState.high);
    }

    gl.bindBuffer(gl.ARRAY_BUFFER, this.posBuf);
    gl.enableVertexAttribArray(0);
    gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0);
    gl.drawArrays(gl.TRIANGLES, 0, 3);
  }

  // === Animation ===

  start() {
    if (this.playing && this.animId) return;
    this.playing = true;
    const loop = () => {
      if (!this.playing) return;
      this.render();
      this.animId = requestAnimationFrame(loop);
    };
    loop();
  }

  stop() {
    this.playing = false;
    if (this.animId) cancelAnimationFrame(this.animId);
    this.animId = null;
  }

  togglePlay() {
    if (this.playing) this.stop();
    else this.start();
    return this.playing;
  }

  resetTime() {
    this.startTime = performance.now();
    this.frameIndex = 0;
  }

  // === Standalone render (used for preview) ===

  render(audioState) {
    const gl = this.gl;
    if (!this.program) return;

    if (this._bgProgram) {
      gl.disable(gl.BLEND);
      this._renderBg(audioState);
      gl.enable(gl.BLEND);
      gl.blendFunc(gl.SRC_ALPHA, gl.ONE_MINUS_SRC_ALPHA);
    } else {
      gl.disable(gl.BLEND);
    }

    gl.useProgram(this.program);

    const elapsed = (performance.now() - this.startTime) / 1000;
    const timeLoc = this._getLoc('TIME');
    if (timeLoc) gl.uniform1f(timeLoc, elapsed);
    const resLoc = this._getLoc('RENDERSIZE');
    if (resLoc) gl.uniform2f(resLoc, this.canvas.width, this.canvas.height);
    const piLoc = this._getLoc('PASSINDEX');
    if (piLoc) gl.uniform1i(piLoc, 0);
    const fiLoc = this._getLoc('FRAMEINDEX');
    if (fiLoc) gl.uniform1i(fiLoc, this.frameIndex);

    this._setInputUniforms(this.program, this.uniformLocs, this.inputValues, (n) => this._getLoc(n));

    let texUnit = 0;
    texUnit = this._bindTextures(this.textures, (n) => this._getLoc(n), texUnit);

    if (audioState) texUnit = this._bindAudioUniforms((n) => this._getLoc(n), audioState, texUnit);

    gl.bindBuffer(gl.ARRAY_BUFFER, this.posBuf);
    gl.enableVertexAttribArray(0);
    gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0);
    gl.drawArrays(gl.TRIANGLES, 0, 3);
    this.frameIndex++;
  }

  // === FBO Management ===

  createFBO(w, h) {
    const gl = this.gl;
    const fbo = gl.createFramebuffer();
    const tex = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, tex);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, w, h, 0, gl.RGBA, gl.UNSIGNED_BYTE, null);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    gl.bindFramebuffer(gl.FRAMEBUFFER, fbo);
    gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, tex, 0);
    gl.bindFramebuffer(gl.FRAMEBUFFER, null);
    return { fbo, texture: tex, width: w, height: h };
  }

  createPingPongFBO(w, h) {
    if (this._ppFloatChecked === undefined) {
      this._ppFloatChecked = true;
      this._halfFloatExt = this.gl.getExtension('OES_texture_half_float');
      this._halfFloatLinear = this.gl.getExtension('OES_texture_half_float_linear');
      this.gl.getExtension('EXT_color_buffer_half_float');
      this._useHalfFloat = false;
      if (this._halfFloatExt) {
        const test = this._createHalfFloatFBO(4, 4);
        if (test) {
          this._useHalfFloat = true;
          this.gl.deleteTexture(test.texture);
          this.gl.deleteFramebuffer(test.fbo);
        }
      }
    }
    if (this._useHalfFloat) {
      return { a: this._createHalfFloatFBO(w, h), b: this._createHalfFloatFBO(w, h), current: 0 };
    }
    return { a: this.createFBO(w, h), b: this.createFBO(w, h), current: 0 };
  }

  _createHalfFloatFBO(w, h) {
    const gl = this.gl;
    const fbo = gl.createFramebuffer();
    const tex = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, tex);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, w, h, 0, gl.RGBA, this._halfFloatExt.HALF_FLOAT_OES, null);
    const filter = this._halfFloatLinear ? gl.LINEAR : gl.NEAREST;
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, filter);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, filter);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    gl.bindFramebuffer(gl.FRAMEBUFFER, fbo);
    gl.framebufferTexture2D(gl.FRAMEBUFFER, gl.COLOR_ATTACHMENT0, gl.TEXTURE_2D, tex, 0);
    if (gl.checkFramebufferStatus(gl.FRAMEBUFFER) !== gl.FRAMEBUFFER_COMPLETE) {
      gl.deleteTexture(tex);
      gl.deleteFramebuffer(fbo);
      gl.bindFramebuffer(gl.FRAMEBUFFER, null);
      return null;
    }
    gl.bindFramebuffer(gl.FRAMEBUFFER, null);
    return { fbo, texture: tex, width: w, height: h };
  }

  destroyFBO(fboObj) {
    if (!fboObj) return;
    const gl = this.gl;
    if (fboObj.texture) gl.deleteTexture(fboObj.texture);
    if (fboObj.fbo) gl.deleteFramebuffer(fboObj.fbo);
  }

  destroyPingPongFBO(pp) {
    if (!pp) return;
    this.destroyFBO(pp.a);
    this.destroyFBO(pp.b);
  }

  // === Layer Compilation & Rendering ===

  compileForLayer(layer, vertSrc, fragSrc) {
    const gl = this.gl;
    const vs = this._compileShader(gl.VERTEX_SHADER, vertSrc);
    if (!vs.shader) return { ok: false, errors: 'Vertex: ' + vs.log };
    const fs = this._compileShader(gl.FRAGMENT_SHADER, fragSrc);
    if (!fs.shader) { gl.deleteShader(vs.shader); return { ok: false, errors: this._prettyErrors(fs.log) }; }
    const prog = gl.createProgram();
    gl.attachShader(prog, vs.shader);
    gl.attachShader(prog, fs.shader);
    gl.bindAttribLocation(prog, 0, 'position');
    gl.linkProgram(prog);
    gl.deleteShader(vs.shader);
    gl.deleteShader(fs.shader);
    if (!gl.getProgramParameter(prog, gl.LINK_STATUS)) {
      const log = gl.getProgramInfoLog(prog);
      gl.deleteProgram(prog);
      return { ok: false, errors: 'Link: ' + log };
    }
    if (layer.program) gl.deleteProgram(layer.program);
    layer.program = prog;
    layer.uniformLocs = {};
    return { ok: true, errors: null };
  }

  _getLayerLoc(layer, name) {
    if (!(name in layer.uniformLocs)) {
      layer.uniformLocs[name] = this.gl.getUniformLocation(layer.program, name);
    }
    return layer.uniformLocs[name];
  }

  /**
   * Render an ISF layer to its FBO
   * @param {object} layer - layer state object
   * @param {object} audioState - { level, bass, mid, high, fftGLTexture }
   * @param {object} mediaPipeMgr - MediaPipeManager instance
   * @param {object} fontState - { vfGLTexture, fontAtlasGLTexture, fontFamilies, updateVarFont, updateBreathing, updateFontAtlas }
   * @param {object} inputImageTexture - texture from composite of layers below (for effects layer)
   */
  renderLayerToFBO(layer, audioState, mediaPipeMgr, fontState, inputImageTexture) {
    const gl = this.gl;
    if (!layer.program || !layer.fbo) return;
    if (!layer.visible) return;

    // Multi-pass branch
    if (layer.passes && layer.passes.length > 0) {
      this._renderMultiPass(layer, audioState, mediaPipeMgr, fontState, inputImageTexture);
      return;
    }

    gl.bindFramebuffer(gl.FRAMEBUFFER, layer.fbo.fbo);
    gl.viewport(0, 0, layer.fbo.width, layer.fbo.height);
    gl.clearColor(0, 0, 0, 0);
    gl.clear(gl.COLOR_BUFFER_BIT);
    gl.disable(gl.BLEND);

    gl.useProgram(layer.program);

    const elapsed = (performance.now() - this.startTime) / 1000;
    const tLoc = this._getLayerLoc(layer, 'TIME');
    if (tLoc) gl.uniform1f(tLoc, elapsed);
    const rLoc = this._getLayerLoc(layer, 'RENDERSIZE');
    if (rLoc) gl.uniform2f(rLoc, layer.fbo.width, layer.fbo.height);
    const piLoc = this._getLayerLoc(layer, 'PASSINDEX');
    if (piLoc) gl.uniform1i(piLoc, 0);
    const fiLoc = this._getLayerLoc(layer, 'FRAMEINDEX');
    if (fiLoc) gl.uniform1i(fiLoc, this.frameIndex);

    // Transparent background flag
    const tbLoc = this._getLayerLoc(layer, '_transparentBg');
    if (tbLoc) gl.uniform1f(tbLoc, layer.transparentBg ? 1.0 : 0.0);

    // Voice decay glitch
    const vgLoc = this._getLayerLoc(layer, '_voiceGlitch');
    if (vgLoc) gl.uniform1f(vgLoc, layer._voiceGlitch || 0.0);

    // Resolve MediaPipe bindings → modify inputValues before uniform upload
    if (layer.mpBindings && layer.mpBindings.length > 0 && mediaPipeMgr && mediaPipeMgr.active) {
      this._resolveBindings(layer, mediaPipeMgr);
    }

    // Input values
    this._setInputUniforms(layer.program, layer.uniformLocs, layer.inputValues, (n) => this._getLayerLoc(layer, n));

    // Bind layer textures
    let texUnit = 0;
    texUnit = this._bindTextures(layer.textures, (n) => this._getLayerLoc(layer, n), texUnit);

    // Bind extras (audio, font, mediapipe)
    texUnit = this._bindLayerExtras(layer, audioState, mediaPipeMgr, fontState, texUnit);

    // Effects layer: bind composite of layers below as inputImage
    if (inputImageTexture) {
      const iiLoc = this._getLayerLoc(layer, 'inputImage');
      if (iiLoc) {
        gl.activeTexture(gl.TEXTURE0 + texUnit);
        gl.bindTexture(gl.TEXTURE_2D, inputImageTexture);
        gl.uniform1i(iiLoc, texUnit);
        texUnit++;
      }
    }

    // Mouse uniforms — hand-as-mouse override when enabled
    let mx = this.mousePos[0], my = this.mousePos[1];
    if (layer.handAsMouse && mediaPipeMgr && mediaPipeMgr.active && mediaPipeMgr.handCount > 0) {
      mx = mediaPipeMgr.handPos[0];
      my = mediaPipeMgr.handPos[1];
    }
    const mousePLoc = this._getLayerLoc(layer, 'mousePos');
    if (mousePLoc) gl.uniform2f(mousePLoc, mx, my);
    const mouseDLoc = this._getLayerLoc(layer, 'mouseDelta');
    if (mouseDLoc) gl.uniform2f(mouseDLoc, this.mouseDelta[0], this.mouseDelta[1]);

    gl.bindBuffer(gl.ARRAY_BUFFER, this.posBuf);
    gl.enableVertexAttribArray(0);
    gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0);
    gl.drawArrays(gl.TRIANGLES, 0, 3);

    gl.bindFramebuffer(gl.FRAMEBUFFER, null);
    gl.viewport(0, 0, this.canvas.width, this.canvas.height);
  }

  // === Shared Helpers ===

  /**
   * Resolve MediaPipe bindings: body part position → parameter value
   * Each binding: { bodyPart, group, landmarkIndex, param, axis, min, max }
   */
  _resolveBindings(layer, mediaPipeMgr) {
    for (const b of layer.mpBindings) {
      let pos = null;
      if (b.group === 'hand' && mediaPipeMgr.handCount > 0) {
        // Read from hand landmark data (stored as bytes in handTex)
        // Use convenience: for palm/wrist use handPos, for others read from _lastHandLandmarks
        if (mediaPipeMgr._lastHandLandmarks && mediaPipeMgr._lastHandLandmarks[b.landmarkIndex]) {
          pos = mediaPipeMgr._lastHandLandmarks[b.landmarkIndex];
        } else if (b.landmarkIndex === 9) {
          pos = { x: mediaPipeMgr.handPos[0], y: mediaPipeMgr.handPos[1], z: mediaPipeMgr.handPos[2] };
        }
      } else if (b.group === 'face' && mediaPipeMgr._lastFaceLandmarks && mediaPipeMgr._lastFaceLandmarks[b.landmarkIndex]) {
        pos = mediaPipeMgr._lastFaceLandmarks[b.landmarkIndex];
      } else if (b.group === 'pose' && mediaPipeMgr._lastPoseLandmarks && mediaPipeMgr._lastPoseLandmarks[b.landmarkIndex]) {
        pos = mediaPipeMgr._lastPoseLandmarks[b.landmarkIndex];
      }
      if (!pos) continue;
      // Select axis
      let v = 0;
      if (b.axis === 'x') v = pos.x;
      else if (b.axis === 'y') v = pos.y;
      else if (b.axis === 'z') v = pos.z || 0;
      // Landmarks are normalized 0-1, lerp to param min/max
      const mapped = b.min + v * (b.max - b.min);
      layer.inputValues[b.param] = mapped;
    }
  }

  _setInputUniforms(program, locs, inputValues, getLocFn) {
    const gl = this.gl;
    for (const [name, val] of Object.entries(inputValues || {})) {
      const loc = getLocFn(name);
      if (!loc) continue;
      if (typeof val === 'number') gl.uniform1f(loc, val);
      else if (typeof val === 'boolean') gl.uniform1i(loc, val ? 1 : 0);
      else if (Array.isArray(val)) {
        if (val.length === 2) gl.uniform2f(loc, val[0], val[1]);
        else if (val.length === 4) gl.uniform4f(loc, val[0], val[1], val[2], val[3]);
      }
    }
  }

  _bindTextures(textures, getLocFn, texUnit) {
    const gl = this.gl;
    for (const [name, tex] of Object.entries(textures || {})) {
      gl.activeTexture(gl.TEXTURE0 + texUnit);
      gl.bindTexture(gl.TEXTURE_2D, tex.glTexture);
      if (tex.isVideo && tex.element && (tex.element.readyState >= 2 || tex.element instanceof HTMLCanvasElement)) {
        if (tex._isNdi) gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
        if (tex.flipH || tex.flipV) {
          if (!tex._flipCanvas) { tex._flipCanvas = document.createElement('canvas'); tex._flipCtx = tex._flipCanvas.getContext('2d'); }
          const v = tex.element, fc = tex._flipCanvas;
          const vw = v.videoWidth || v.width || 640;
          const vh = v.videoHeight || v.height || 480;
          if (fc.width !== vw || fc.height !== vh) { fc.width = vw; fc.height = vh; }
          const ctx = tex._flipCtx;
          ctx.save();
          ctx.translate(tex.flipH ? fc.width : 0, tex.flipV ? fc.height : 0);
          ctx.scale(tex.flipH ? -1 : 1, tex.flipV ? -1 : 1);
          ctx.drawImage(v, 0, 0);
          ctx.restore();
          gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, fc);
        } else {
          gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, tex.element);
        }
        if (tex._isNdi) gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
      }
      gl.uniform1i(getLocFn(name), texUnit);
      texUnit++;
    }
    return texUnit;
  }

  _bindAudioUniforms(getLocFn, audioState, texUnit) {
    const gl = this.gl;
    if (!audioState) return texUnit;

    if (audioState.fftGLTexture) {
      gl.activeTexture(gl.TEXTURE0 + texUnit);
      gl.bindTexture(gl.TEXTURE_2D, audioState.fftGLTexture);
      gl.uniform1i(getLocFn('audioFFT'), texUnit);
      texUnit++;
    }
    const alLoc = getLocFn('audioLevel');
    if (alLoc) gl.uniform1f(alLoc, audioState.level);
    const abLoc = getLocFn('audioBass');
    if (abLoc) gl.uniform1f(abLoc, audioState.bass);
    const amLoc = getLocFn('audioMid');
    if (amLoc) gl.uniform1f(amLoc, audioState.mid);
    const ahLoc = getLocFn('audioHigh');
    if (ahLoc) gl.uniform1f(ahLoc, audioState.high);
    return texUnit;
  }

  _bindLayerExtras(layer, audioState, mediaPipeMgr, fontState, texUnit) {
    const gl = this.gl;
    const getLocFn = (n) => this._getLayerLoc(layer, n);

    // Audio
    texUnit = this._bindAudioUniforms(getLocFn, audioState, texUnit);

    // Audio reactivity modulation
    if (layer.inputValues && layer.inputValues['audioReactive'] && audioState) {
      const sp = layer.inputValues['speed'];
      const spLoc = getLocFn('speed');
      if (sp != null && spLoc) gl.uniform1f(spLoc, sp + audioState.level * 1.5);
      const int_ = layer.inputValues['intensity'];
      const intLoc = getLocFn('intensity');
      if (int_ != null && intLoc) gl.uniform1f(intLoc, Math.min(1, int_ + audioState.bass * 0.7));
      const ts = layer.inputValues['textScale'];
      const tsLoc = getLocFn('textScale');
      if (ts != null && tsLoc) gl.uniform1f(tsLoc, ts * (1 + audioState.bass * 0.3));
    }

    // Variable font texture (effects 20 + 22)
    if (fontState) {
      const vfLoc = getLocFn('varFontTex');
      if (vfLoc) {
        const _layerEffectIdx = Math.round((layer.inputValues || {})['effect'] || 0);
        if (_layerEffectIdx === 22) {
          fontState.updateBreathing(gl, layer.inputValues || {});
        } else {
          fontState.updateVarFont(gl, layer.inputValues || {});
        }
        if (fontState.vfGLTexture) {
          gl.activeTexture(gl.TEXTURE0 + texUnit);
          gl.bindTexture(gl.TEXTURE_2D, fontState.vfGLTexture);
          gl.uniform1i(vfLoc, texUnit);
          texUnit++;
        }
      }

      // Font atlas — bind whenever the shader uses fontAtlasTex (any fontFamily index)
      const _fontFamilyIdx = Math.round((layer.inputValues || {})['fontFamily'] || 0);
      const _useFontAtlasLoc = getLocFn('useFontAtlas');
      if (_useFontAtlasLoc) gl.uniform1f(_useFontAtlasLoc, _fontFamilyIdx > 0 ? 1.0 : 0.0);
      const faLoc = getLocFn('fontAtlasTex');
      if (faLoc) {
        fontState.updateFontAtlas(gl, layer.inputValues || {});
        if (fontState.fontAtlasGLTexture) {
          gl.activeTexture(gl.TEXTURE0 + texUnit);
          gl.bindTexture(gl.TEXTURE_2D, fontState.fontAtlasGLTexture);
          gl.uniform1i(faLoc, texUnit);
          texUnit++;
        }
      }
    }

    // MediaPipe
    if (mediaPipeMgr && mediaPipeMgr.active) {
      if (mediaPipeMgr.handTex) {
        gl.activeTexture(gl.TEXTURE0 + texUnit);
        gl.bindTexture(gl.TEXTURE_2D, mediaPipeMgr.handTex);
        gl.uniform1i(getLocFn('mpHandLandmarks'), texUnit++);
      }
      if (mediaPipeMgr.faceTex) {
        gl.activeTexture(gl.TEXTURE0 + texUnit);
        gl.bindTexture(gl.TEXTURE_2D, mediaPipeMgr.faceTex);
        gl.uniform1i(getLocFn('mpFaceLandmarks'), texUnit++);
      }
      if (mediaPipeMgr.poseTex) {
        gl.activeTexture(gl.TEXTURE0 + texUnit);
        gl.bindTexture(gl.TEXTURE_2D, mediaPipeMgr.poseTex);
        gl.uniform1i(getLocFn('mpPoseLandmarks'), texUnit++);
      }
      if (mediaPipeMgr.segTex) {
        gl.activeTexture(gl.TEXTURE0 + texUnit);
        gl.bindTexture(gl.TEXTURE_2D, mediaPipeMgr.segTex);
        gl.uniform1i(getLocFn('mpSegMask'), texUnit++);
      }
      const hcLoc = getLocFn('mpHandCount');
      if (hcLoc) gl.uniform1f(hcLoc, mediaPipeMgr.handCount);
      const hpLoc = getLocFn('mpHandPos');
      if (hpLoc) gl.uniform3f(hpLoc, mediaPipeMgr.handPos[0], mediaPipeMgr.handPos[1], mediaPipeMgr.handPos[2]);
    }

    return texUnit;
  }

  // === Multi-pass ===

  _renderMultiPass(layer, audioState, mediaPipeMgr, fontState, inputImageTexture) {
    const gl = this.gl;
    gl.useProgram(layer.program);
    gl.disable(gl.BLEND);

    const elapsed = (performance.now() - this.startTime) / 1000;
    const tLoc = this._getLayerLoc(layer, 'TIME');
    if (tLoc) gl.uniform1f(tLoc, elapsed);
    const fiLoc = this._getLayerLoc(layer, 'FRAMEINDEX');
    if (fiLoc) gl.uniform1i(fiLoc, this.frameIndex);
    const tbLoc = this._getLayerLoc(layer, '_transparentBg');
    if (tbLoc) gl.uniform1f(tbLoc, layer.transparentBg ? 1.0 : 0.0);

    const mousePLoc = this._getLayerLoc(layer, 'mousePos');
    if (mousePLoc) gl.uniform2f(mousePLoc, this.mousePos[0], this.mousePos[1]);
    const mouseDLoc = this._getLayerLoc(layer, 'mouseDelta');
    if (mouseDLoc) gl.uniform2f(mouseDLoc, this.mouseDelta[0], this.mouseDelta[1]);

    this._setInputUniforms(layer.program, layer.uniformLocs, layer.inputValues, (n) => this._getLayerLoc(layer, n));

    let texUnit = 0;
    texUnit = this._bindTextures(layer.textures, (n) => this._getLayerLoc(layer, n), texUnit);
    texUnit = this._bindLayerExtras(layer, audioState, mediaPipeMgr, fontState, texUnit);

    // inputImage for effects layer
    if (inputImageTexture) {
      const iiLoc = this._getLayerLoc(layer, 'inputImage');
      if (iiLoc) {
        gl.activeTexture(gl.TEXTURE0 + texUnit);
        gl.bindTexture(gl.TEXTURE_2D, inputImageTexture);
        gl.uniform1i(iiLoc, texUnit);
        texUnit++;
      }
    }

    // Reserve texture units for TARGETs
    const targetBaseUnit = texUnit;
    const targetUnits = {};
    layer.passes.forEach((p, i) => {
      if (p.target) {
        targetUnits[p.target] = targetBaseUnit + i;
        texUnit++;
      }
    });

    gl.bindBuffer(gl.ARRAY_BUFFER, this.posBuf);
    gl.enableVertexAttribArray(0);
    gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0);

    for (let i = 0; i < layer.passes.length; i++) {
      const pass = layer.passes[i];
      const isFinal = !pass.target;

      let outFBO;
      if (isFinal) {
        outFBO = layer.fbo;
      } else {
        const pp = pass.ppFBO;
        outFBO = pp.current === 0 ? pp.b : pp.a;
      }

      gl.bindFramebuffer(gl.FRAMEBUFFER, outFBO.fbo);
      gl.viewport(0, 0, outFBO.width, outFBO.height);

      if (!pass.persistent || isFinal) {
        gl.clearColor(0, 0, 0, 0);
        gl.clear(gl.COLOR_BUFFER_BIT);
      }

      const piLoc = this._getLayerLoc(layer, 'PASSINDEX');
      if (piLoc) gl.uniform1i(piLoc, i);
      const rLoc = this._getLayerLoc(layer, 'RENDERSIZE');
      if (rLoc) gl.uniform2f(rLoc, outFBO.width, outFBO.height);

      for (const [tName, tUnit] of Object.entries(targetUnits)) {
        const tPass = layer.passes.find(p => p.target === tName);
        if (!tPass || !tPass.ppFBO) continue;
        const readFBO = tPass.ppFBO.current === 0 ? tPass.ppFBO.a : tPass.ppFBO.b;
        gl.activeTexture(gl.TEXTURE0 + tUnit);
        gl.bindTexture(gl.TEXTURE_2D, readFBO.texture);
        gl.uniform1i(this._getLayerLoc(layer, tName), tUnit);
      }

      gl.drawArrays(gl.TRIANGLES, 0, 3);

      if (pass.persistent && pass.ppFBO) {
        pass.ppFBO.current ^= 1;
      }
    }

    gl.bindFramebuffer(gl.FRAMEBUFFER, null);
    gl.viewport(0, 0, this.canvas.width, this.canvas.height);
  }

  // === Compositor ===

  /**
   * Build dynamic compositor shader for N visible layers
   * @param {number} layerCount - total number of layers to support
   */
  initCompositor(layerCount = 7) {
    const compFrag = this._buildCompositorShader(layerCount);
    const result = this.compile(VERT_SHADER, compFrag);
    if (result.ok) {
      this.compositorProgram = this.program;
      this.compositorLocs = {};
      this.program = null;
    } else {
      console.error('Compositor shader failed:', result.errors);
    }
    return result;
  }

  _buildCompositorShader(n) {
    let src = 'precision highp float;\n';
    src += 'varying vec2 isf_FragNormCoord;\n';
    src += 'uniform vec2 RENDERSIZE;\n';
    src += 'uniform sampler2D bgTexture;\n';
    src += 'uniform float bgMode;\n';
    src += 'uniform vec3 bgColor;\n';

    for (let i = 0; i < n; i++) {
      src += `uniform sampler2D layer${i};\n`;
      src += `uniform float opacity${i}, visible${i}, blendMode${i};\n`;
      src += `uniform float flipH${i}, flipV${i};\n`;
    }

    // Overlay transform uniforms (applied to last layer)
    src += 'uniform vec2 overlayTranslate;\n';
    src += 'uniform float overlayScale;\n';
    src += 'uniform float overlayRotate;\n';

    src += `
vec2 uvForLayer(vec2 uv, float fh, float fv) {
  if (fh > 0.5) uv.x = 1.0 - uv.x;
  if (fv > 0.5) uv.y = 1.0 - uv.y;
  return uv;
}

vec2 uvForOverlay(vec2 uv, float fh, float fv) {
  uv = uvForLayer(uv, fh, fv);
  uv -= 0.5;
  uv /= max(overlayScale, 0.001);
  float cr = cos(overlayRotate);
  float sr = sin(overlayRotate);
  uv = vec2(cr * uv.x + sr * uv.y, -sr * uv.x + cr * uv.y);
  uv -= overlayTranslate;
  uv += 0.5;
  return uv;
}

vec3 blendNormal(vec3 base, vec3 top, float a) { return mix(base, top, a); }
vec3 blendAdd(vec3 base, vec3 top, float a) { return base + top * a; }
vec3 blendMultiply(vec3 base, vec3 top, float a) { return mix(base, base * top, a); }
vec3 blendScreen(vec3 base, vec3 top, float a) { vec3 s = 1.0 - (1.0 - base) * (1.0 - top); return mix(base, s, a); }
vec3 blendOverlay(vec3 base, vec3 top, float a) {
  vec3 o = vec3(
    base.r < 0.5 ? 2.0*base.r*top.r : 1.0-2.0*(1.0-base.r)*(1.0-top.r),
    base.g < 0.5 ? 2.0*base.g*top.g : 1.0-2.0*(1.0-base.g)*(1.0-top.g),
    base.b < 0.5 ? 2.0*base.b*top.b : 1.0-2.0*(1.0-base.b)*(1.0-top.b)
  );
  return mix(base, o, a);
}

vec3 applyBlend(vec3 base, vec3 top, float a, float mode) {
  if (mode < 0.5) return blendNormal(base, top, a);
  if (mode < 1.5) return blendAdd(base, top, a);
  if (mode < 2.5) return blendMultiply(base, top, a);
  if (mode < 3.5) return blendScreen(base, top, a);
  return blendOverlay(base, top, a);
}

void main() {
  vec2 uv = isf_FragNormCoord;

  // Background
  vec4 result = vec4(0.0, 0.0, 0.0, 1.0);
  if (bgMode > 0.5 && bgMode < 1.5) result = vec4(0.0, 0.0, 0.0, 0.0);
  else if (bgMode > 1.5 && bgMode < 2.5) result = vec4(bgColor, 1.0);
  else if (bgMode > 2.5) result = texture2D(bgTexture, uv);

`;

    // Composite each layer
    for (let i = 0; i < n; i++) {
      const isOverlay = (i === n - 1);
      src += `  if (visible${i} > 0.5) {\n`;
      if (isOverlay) {
        // Overlay layer uses transform UV with bounds check
        src += `    vec2 ouv${i} = uvForOverlay(uv, flipH${i}, flipV${i});\n`;
        src += `    vec4 c${i} = (ouv${i}.x >= 0.0 && ouv${i}.x <= 1.0 && ouv${i}.y >= 0.0 && ouv${i}.y <= 1.0) ? texture2D(layer${i}, ouv${i}) : vec4(0.0);\n`;
      } else {
        src += `    vec4 c${i} = texture2D(layer${i}, uvForLayer(uv, flipH${i}, flipV${i}));\n`;
      }
      src += `    float a${i} = c${i}.a * opacity${i};\n`;
      src += `    result.rgb = applyBlend(result.rgb, c${i}.rgb, a${i}, blendMode${i});\n`;
      src += `    result.a = max(result.a, a${i});\n`;
      src += `  }\n`;
    }

    src += '  gl_FragColor = result;\n}\n';
    return src;
  }

  _getCompLoc(name) {
    if (!(name in this.compositorLocs)) {
      this.compositorLocs[name] = this.gl.getUniformLocation(this.compositorProgram, name);
    }
    return this.compositorLocs[name];
  }

  /**
   * Render all layers through compositor to screen
   * @param {Array} layers - ordered array of layer objects (bottom to top)
   * @param {object} sceneTexture - GL texture from Three.js scene
   * @param {object} bgState - { mode, color, texture }
   */
  renderCompositor(layers, sceneTexture, bgState) {
    const gl = this.gl;
    if (!this.compositorProgram) return;

    gl.bindFramebuffer(gl.FRAMEBUFFER, null);
    gl.viewport(0, 0, this.canvas.width, this.canvas.height);
    gl.disable(gl.BLEND);
    gl.clearColor(0, 0, 0, 1);
    gl.clear(gl.COLOR_BUFFER_BIT);

    gl.useProgram(this.compositorProgram);

    const rLoc = this._getCompLoc('RENDERSIZE');
    if (rLoc) gl.uniform2f(rLoc, this.canvas.width, this.canvas.height);

    const blendModeMap = { normal: 0, add: 1, multiply: 2, screen: 3, overlay: 4 };

    for (let i = 0; i < layers.length; i++) {
      const layer = layers[i];
      gl.activeTexture(gl.TEXTURE0 + i);

      // 3D layer uses Three.js scene texture
      if (layer && layer.id === '3d' && sceneTexture) {
        gl.bindTexture(gl.TEXTURE_2D, sceneTexture);
      } else if (layer && layer.fbo) {
        gl.bindTexture(gl.TEXTURE_2D, layer.fbo.texture);
      } else {
        gl.bindTexture(gl.TEXTURE_2D, this._defaultTex);
      }

      gl.uniform1i(this._getCompLoc('layer' + i), i);
      gl.uniform1f(this._getCompLoc('opacity' + i), layer ? layer.opacity : 0);
      gl.uniform1f(this._getCompLoc('visible' + i), (layer && layer.visible) ? 1.0 : 0.0);
      gl.uniform1f(this._getCompLoc('blendMode' + i), blendModeMap[layer ? layer.blendMode : 'normal'] || 0);

      // Per-layer flip
      const is3D = layer && layer.id === '3d';
      gl.uniform1f(this._getCompLoc('flipH' + i), is3D && layer.sceneFlipH ? 1.0 : 0.0);
      gl.uniform1f(this._getCompLoc('flipV' + i), is3D && layer.sceneFlipV ? 1.0 : 0.0);
    }

    // Overlay transform uniforms
    const overlayIdx = layers.length - 1;
    const oLayer = layers[overlayIdx];
    const otLoc = this._getCompLoc('overlayTranslate');
    if (otLoc) gl.uniform2f(otLoc, oLayer ? (oLayer._tx || 0) : 0, oLayer ? (oLayer._ty || 0) : 0);
    const osLoc = this._getCompLoc('overlayScale');
    if (osLoc) gl.uniform1f(osLoc, oLayer ? (oLayer._scale || 1) : 1);
    const orLoc = this._getCompLoc('overlayRotate');
    if (orLoc) gl.uniform1f(orLoc, oLayer ? (oLayer._rotate || 0) : 0);

    // Background uniforms
    const bgModeMap = { none: 0, transparent: 1, color: 2, image: 3, video: 3, shader: 3, webcam: 3, ndi: 3 };
    const bgM = bgState ? (bgModeMap[bgState.mode] || 0) : 0;
    gl.uniform1f(this._getCompLoc('bgMode'), bgM);
    if (bgState && bgState.color) {
      gl.uniform3f(this._getCompLoc('bgColor'), bgState.color[0], bgState.color[1], bgState.color[2]);
    }
    const bgTexUnit = layers.length;
    gl.activeTexture(gl.TEXTURE0 + bgTexUnit);
    gl.bindTexture(gl.TEXTURE_2D, (bgState && bgState.texture) ? bgState.texture : this._defaultTex);
    gl.uniform1i(this._getCompLoc('bgTexture'), bgTexUnit);

    gl.bindBuffer(gl.ARRAY_BUFFER, this.posBuf);
    gl.enableVertexAttribArray(0);
    gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0);
    gl.drawArrays(gl.TRIANGLES, 0, 3);

    this.frameIndex++;
  }

  /**
   * Render layers 0-3 to a temporary composite FBO (for effects layer input)
   */
  renderPartialComposite(layers, sceneTexture, bgState, targetFBO) {
    const gl = this.gl;
    if (!this.compositorProgram) return;

    gl.bindFramebuffer(gl.FRAMEBUFFER, targetFBO.fbo);
    gl.viewport(0, 0, targetFBO.width, targetFBO.height);
    gl.clearColor(0, 0, 0, 0);
    gl.clear(gl.COLOR_BUFFER_BIT);
    gl.disable(gl.BLEND);

    gl.useProgram(this.compositorProgram);

    const rLoc = this._getCompLoc('RENDERSIZE');
    if (rLoc) gl.uniform2f(rLoc, targetFBO.width, targetFBO.height);

    const blendModeMap = { normal: 0, add: 1, multiply: 2, screen: 3, overlay: 4 };
    const totalSlots = 7; // compositor always has 7 uniform slots

    for (let i = 0; i < totalSlots; i++) {
      const layer = layers[i] || null;
      gl.activeTexture(gl.TEXTURE0 + i);

      if (layer && layer.id === '3d' && sceneTexture) {
        gl.bindTexture(gl.TEXTURE_2D, sceneTexture);
      } else if (layer && layer.fbo) {
        gl.bindTexture(gl.TEXTURE_2D, layer.fbo.texture);
      } else {
        gl.bindTexture(gl.TEXTURE_2D, this._defaultTex);
      }

      gl.uniform1i(this._getCompLoc('layer' + i), i);

      // Only layers passed in are visible in partial composite
      const vis = layer ? 1.0 : 0.0;
      gl.uniform1f(this._getCompLoc('opacity' + i), layer ? layer.opacity : 0);
      gl.uniform1f(this._getCompLoc('visible' + i), vis);
      gl.uniform1f(this._getCompLoc('blendMode' + i), layer ? (blendModeMap[layer.blendMode] || 0) : 0);
      gl.uniform1f(this._getCompLoc('flipH' + i), 0.0);
      gl.uniform1f(this._getCompLoc('flipV' + i), 0.0);
    }

    // Identity overlay transform for partial composite
    const otLoc2 = this._getCompLoc('overlayTranslate');
    if (otLoc2) gl.uniform2f(otLoc2, 0, 0);
    const osLoc2 = this._getCompLoc('overlayScale');
    if (osLoc2) gl.uniform1f(osLoc2, 1);
    const orLoc2 = this._getCompLoc('overlayRotate');
    if (orLoc2) gl.uniform1f(orLoc2, 0);

    const bgModeMap = { none: 0, transparent: 1, color: 2, image: 3, video: 3, shader: 3, webcam: 3, ndi: 3 };
    gl.uniform1f(this._getCompLoc('bgMode'), bgState ? (bgModeMap[bgState.mode] || 0) : 0);
    if (bgState && bgState.color) {
      gl.uniform3f(this._getCompLoc('bgColor'), bgState.color[0], bgState.color[1], bgState.color[2]);
    }
    gl.activeTexture(gl.TEXTURE0 + totalSlots);
    gl.bindTexture(gl.TEXTURE_2D, (bgState && bgState.texture) ? bgState.texture : this._defaultTex);
    gl.uniform1i(this._getCompLoc('bgTexture'), totalSlots);

    gl.bindBuffer(gl.ARRAY_BUFFER, this.posBuf);
    gl.enableVertexAttribArray(0);
    gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0);
    gl.drawArrays(gl.TRIANGLES, 0, 3);

    gl.bindFramebuffer(gl.FRAMEBUFFER, null);
    gl.viewport(0, 0, this.canvas.width, this.canvas.height);
  }
}

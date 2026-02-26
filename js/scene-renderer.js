// Three.js Scene Renderer
// Extracted from ShaderClaw monolith

export class SceneRenderer {
  constructor(canvas) {
    this.canvas = canvas;
    this.renderer = null;
    this.sceneDef = null;
    this.playing = false;
    this.animId = null;
    this.startTime = performance.now();
    this.inputValues = {};
    this.inputs = [];
    this.media = []; // { name, type, threeTexture, threeModel }
    this._shaderBg = null; // { isfRenderer, texture } when shader bg active
    this._isfGL = null; // GL context for audio uniform update
  }

  load(sceneDef) {
    this.cleanup();
    this.renderer = new THREE.WebGLRenderer({
      canvas: this.canvas,
      antialias: true,
      alpha: true,
      preserveDrawingBuffer: true,
      powerPreference: 'high-performance'
    });
    this.renderer.setClearColor(0x000000, 0);
    this.renderer.setPixelRatio(window.devicePixelRatio);
    this.resize();
    this.sceneDef = sceneDef.create(this.renderer, this.canvas, this.media);
    this.inputs = sceneDef.INPUTS || [];
    this.startTime = performance.now();
  }

  render() {
    if (!this.sceneDef || !this.renderer) return;
    // Audio analysis is already updated by renderComposition() in layers.js
    // Drive ISF offscreen render and update texture before 3D render
    if (this._shaderBg) {
      this._shaderBg.isfRenderer.render();
      this._shaderBg.texture.needsUpdate = true;
    }
    const elapsed = (performance.now() - this.startTime) / 1000;
    this.sceneDef.update(elapsed, this.inputValues, this.media);
    this.renderer.render(this.sceneDef.scene, this.sceneDef.camera);
  }

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

  resize() {
    if (!this.renderer) return;
    const parent = this.canvas.parentElement;
    if (!parent) return;
    const w = parent.clientWidth;
    const h = parent.clientHeight;
    this.renderer.setSize(w, h, false);
    this.canvas.style.width = w + 'px';
    this.canvas.style.height = h + 'px';
    if (this.sceneDef && this.sceneDef.resize) {
      this.sceneDef.resize(w, h);
    }
  }

  cleanup() {
    this.stop();
    if (this.sceneDef && this.sceneDef.dispose) {
      this.sceneDef.dispose();
    }
    if (this.renderer) {
      this.renderer.dispose();
      this.renderer = null;
    }
    this.sceneDef = null;
    this.inputs = [];
    this.inputValues = {};
  }

  resetTime() {
    this.startTime = performance.now();
  }
}

// ── ShaderClaw v2 — Centralized State + Pub/Sub ──────────────────────
//
// Single source of truth for the 7-layer compositor model.
// All mutations flow through helper functions that emit events,
// so any module can subscribe without coupling to the others.

// Layer IDs in render order (bottom to top)
const LAYER_IDS = ['background', 'media', '3d', 'av', 'effects', 'text', 'overlay'];

// Blend modes available
const BLEND_MODES = ['normal', 'add', 'multiply', 'screen', 'overlay'];

// ── Layer Factory ────────────────────────────────────────────────────

function createLayer(id) {
  return {
    id,
    visible: true,
    opacity: 1.0,
    blendMode: 'normal',
    locked: false,
    fbo: null,           // assigned by renderer
    program: null,        // compiled shader program
    uniformLocs: {},
    inputValues: {},
    inputs: [],           // ISF INPUTS metadata
    textures: {},         // bound media textures
    transparentBg: (id === 'text' || id === 'overlay'),
    _isfSource: null,     // raw ISF source for context-loss recovery
    _voiceGlitch: 0,
    passes: null,         // multi-pass metadata
    // Layer-specific config
    sourceType: null,     // for background: 'shader' | 'scene' | 'image' | 'video'
    mediaSources: [],     // for media layer
    sceneConfig: null,    // for 3d layer
    effectPreset: null,   // for effects layer
  };
}

// ── Event System (Pub/Sub) ───────────────────────────────────────────
//
// Events:
//   'layer:select'         { layerId }
//   'layer:visibility'     { layerId, visible }
//   'layer:opacity'        { layerId, opacity }
//   'layer:blend'          { layerId, blendMode }
//   'layer:compiled'       { layerId }
//   'layer:source-changed' { layerId, source }
//   'destination:change'   { destination }
//   'aspect:change'        { ratio }
//   'editor:mode'          { mode }
//   'play:toggle'          { playing }
//   'media:added'          { entry }
//   'media:removed'        { id }
//   'audio:levels'         { level, bass, mid, high }

const listeners = {};

function on(event, fn) {
  (listeners[event] || (listeners[event] = [])).push(fn);
  return () => off(event, fn);
}

function off(event, fn) {
  const arr = listeners[event];
  if (arr) listeners[event] = arr.filter(f => f !== fn);
}

function emit(event, data) {
  (listeners[event] || []).forEach(fn => fn(data));
}

// ── Application State ────────────────────────────────────────────────

const state = {
  layers: {},              // id -> layer object
  layerOrder: [...LAYER_IDS],
  selectedLayerId: 'background',
  destination: 'general',  // general|web|video|social|3d|code|live
  aspectRatio: '16:9',
  playing: true,
  editorMode: 'code',     // code|nodes|ai

  // Background settings
  background: { mode: 'none', color: [0, 0, 0], texture: null },

  // Media inputs
  mediaInputs: [],
  mediaIdCounter: 0,

  // Audio state
  audio: {
    ctx: null, analyser: null, dataArray: null,
    fftGLTexture: null, fftThreeTexture: null,
    level: 0, bass: 0, mid: 0, high: 0,
    activeEntry: null,
  },
};

// Initialize all 7 layers
for (const id of LAYER_IDS) {
  state.layers[id] = createLayer(id);
}

// ── Helper Functions ─────────────────────────────────────────────────

function getLayer(id) {
  return state.layers[id];
}

function getSelectedLayer() {
  return state.layers[state.selectedLayerId];
}

function selectLayer(id) {
  state.selectedLayerId = id;
  emit('layer:select', { layerId: id });
}

function setLayerVisibility(id, visible) {
  state.layers[id].visible = visible;
  emit('layer:visibility', { layerId: id, visible });
}

function setLayerOpacity(id, opacity) {
  state.layers[id].opacity = opacity;
  emit('layer:opacity', { layerId: id, opacity });
}

function setLayerBlend(id, blendMode) {
  state.layers[id].blendMode = blendMode;
  emit('layer:blend', { layerId: id, blendMode });
}

// ── Exports ──────────────────────────────────────────────────────────

export {
  state,
  LAYER_IDS,
  BLEND_MODES,
  on,
  off,
  emit,
  getLayer,
  getSelectedLayer,
  selectLayer,
  setLayerVisibility,
  setLayerOpacity,
  setLayerBlend,
  createLayer,
};

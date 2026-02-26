// ShaderClaw v2 — Entry Point
// Initializes all modules, wires the render loop, handles shortcuts

import { state, LAYER_IDS, on, emit, getLayer, selectLayer, setLayerVisibility } from './state.js';
import { Renderer } from './renderer.js';
import { SceneRenderer } from './scene-renderer.js';
import { parseISF, buildFragmentShader, VERT_SHADER, DEFAULT_SHADER } from './isf.js';
import { updateAudioUniforms, getAudioState, getAudioLevels, initAudioContext, connectAudioSource } from './audio.js';
import {
  createGLTexture, detectMediaType, mediaTypeIcon, MediaPipeManager,
  getFontState, updateVarFontTexture, updateBreathingTexture, updateFontAtlas, fontFamilies,
  addMediaEntry, removeMediaEntry, getNextMediaId,
  FRAME_TYPE_NDI_VIDEO, handleNdiVideoFrame, handleNdiResponse, ndiRequest,
  startNdiSend, stopNdiSend, isNdiSending, setNdiReceiveEntry, getNdiReceiveCanvas,
} from './media.js';
import { connect as mcpConnect, getWebSocket, isConnected, send as mcpSend } from './mcp-bridge.js';
import { initLayerFBOs, compileToLayer, loadShaderToLayer, renderComposition, autoBindTextures } from './layers.js';
import { initSidebar } from './ui/sidebar.js';
import { generateControls, updateControlUI } from './ui/params.js';
import { initEditor, setEditorValue, getEditorValue, getEditor } from './ui/editor.js';
import { initCanvasControls, showError } from './ui/canvas-controls.js';
import { initShaderBrowser, loadManifest, getManifest, openShaderBrowser } from './ui/modals.js';

// === Globals ===

let isfRenderer = null;
let sceneRenderer = null;
let mediaPipeMgr = null;
let tempCompositeFBO = null;
let compositionPlaying = true;
let _contextLost = false;
let lastErrors = null;

// === DOM References ===

const glCanvas = document.getElementById('gl-canvas');
const threeCanvas = document.getElementById('three-canvas');
const sidebar = document.getElementById('sidebar');
const viewport = document.getElementById('viewport');
const editorArea = document.getElementById('editor-area');

// === Initialize Renderer ===

isfRenderer = new Renderer(glCanvas);
sceneRenderer = new SceneRenderer(threeCanvas);
mediaPipeMgr = new MediaPipeManager(isfRenderer.gl);

// Initialize 7-layer compositor
isfRenderer.initCompositor(7);

// Create FBOs for all layers
initLayerFBOs(isfRenderer);

// Temp composite FBO for effects layer input
tempCompositeFBO = isfRenderer.createFBO(1920, 1080);

// === Initialize UI ===

const { layerStack, detailPanel, outputSection } = initSidebar(sidebar);
const { playBtn, errorBar } = initCanvasControls(viewport);
const { codePanel, nodesPanel, aiPanel } = initEditor(editorArea, (source) => {
  // Auto-compile on editor change
  compile(source);
});

// Resize handle for editor area
const resizeHandle = document.getElementById('resize-handle');
if (resizeHandle) {
  let dragging = false;
  let startY = 0;
  let startHeight = 0;

  resizeHandle.addEventListener('pointerdown', (e) => {
    dragging = true;
    startY = e.clientY;
    startHeight = editorArea.offsetHeight;
    e.preventDefault();
  });

  window.addEventListener('pointermove', (e) => {
    if (!dragging) return;
    const delta = startY - e.clientY;
    editorArea.style.height = Math.max(100, startHeight + delta) + 'px';
  });

  window.addEventListener('pointerup', () => { dragging = false; });
}

// === Shader Browser ===

initShaderBrowser();

// === Compile & Load ===

function compile(source) {
  const layerId = state.selectedLayerId;
  const result = compileToLayer(isfRenderer, layerId, source);

  if (result.ok) {
    lastErrors = null;
    showError(errorBar, null);
    // Regenerate parameter controls
    const layer = getLayer(layerId);
    generateControls(layer.inputs, detailPanel, (values) => {
      Object.assign(layer.inputValues, values);
    });
    autoBindTextures();
  } else {
    lastErrors = result.errors;
    showError(errorBar, result.errors);
  }
  return result;
}

function loadSource(source) {
  setEditorValue(source);
  compile(source);
}

async function loadScene(folder, file) {
  try {
    const url = `${folder}/${file}`;
    const resp = await fetch(url);
    const text = await resp.text();
    const fn = new Function('THREE', 'return (' + text.trim() + ')(THREE)');
    const sceneDef = fn(THREE);
    sceneRenderer.load(sceneDef);
    sceneRenderer._isfGL = isfRenderer.gl;

    // Generate controls for scene inputs
    const layer3d = getLayer('3d');
    layer3d.inputs = sceneDef.INPUTS || [];
    generateControls(layer3d.inputs, detailPanel, (values) => {
      Object.assign(sceneRenderer.inputValues, values);
      Object.assign(layer3d.inputValues, values);
    });
    autoBindTextures();
  } catch (e) {
    console.error('Failed to load scene:', e);
  }
}

// === Layer Selection ===

on('layer:select', ({ layerId }) => {
  const layer = getLayer(layerId);
  // Update detail panel with selected layer's params
  if (layer._isfSource) {
    setEditorValue(layer._isfSource);
  }
  generateControls(layer.inputs || [], detailPanel, (values) => {
    Object.assign(layer.inputValues, values);
  });
});

// === Composition Render Loop ===

function compositionLoop() {
  if (!compositionPlaying || _contextLost) {
    requestAnimationFrame(compositionLoop);
    return;
  }

  renderComposition(isfRenderer, sceneRenderer, mediaPipeMgr, tempCompositeFBO);

  // Mouse tracking
  const dx = isfRenderer.mousePos[0] - isfRenderer._lastMousePos[0];
  const dy = isfRenderer.mousePos[1] - isfRenderer._lastMousePos[1];
  isfRenderer.mouseDelta = [dx * 0.3, dy * 0.3];
  isfRenderer._lastMousePos = [...isfRenderer.mousePos];

  requestAnimationFrame(compositionLoop);
}

// === Mouse Tracking ===

glCanvas.addEventListener('mousemove', (e) => {
  const rect = glCanvas.getBoundingClientRect();
  const x = (e.clientX - rect.left) / rect.width;
  const y = 1.0 - (e.clientY - rect.top) / rect.height;
  isfRenderer.mousePos = [x, y];
});

// === Context Loss Recovery ===

glCanvas.addEventListener('webglcontextlost', (e) => {
  e.preventDefault();
  _contextLost = true;
  console.warn('[ShaderClaw] WebGL context lost');
});

glCanvas.addEventListener('webglcontextrestored', () => {
  _contextLost = false;
  console.log('[ShaderClaw] WebGL context restored');
  isfRenderer.reinitGL();
  isfRenderer.initCompositor(7);
  initLayerFBOs(isfRenderer);
  tempCompositeFBO = isfRenderer.createFBO(1920, 1080);
  // Recompile all layers from stored sources
  for (const id of LAYER_IDS) {
    const layer = getLayer(id);
    if (layer._isfSource) {
      compileToLayer(isfRenderer, id, layer._isfSource);
    }
  }
});

// === Resize ===

const resizeObserver = new ResizeObserver(() => {
  isfRenderer.resize();
  sceneRenderer.resize();
});
resizeObserver.observe(viewport);

// === Keyboard Shortcuts ===

document.addEventListener('keydown', (e) => {
  // 1-7: toggle layer visibility
  const num = parseInt(e.key);
  if (num >= 1 && num <= 7 && !e.ctrlKey && !e.metaKey && !e.altKey) {
    const target = e.target;
    if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA' || target.tagName === 'SELECT') return;
    const layerId = LAYER_IDS[num - 1];
    const layer = getLayer(layerId);
    setLayerVisibility(layerId, !layer.visible);
    return;
  }

  // Tab: cycle selected layer
  if (e.key === 'Tab' && !e.ctrlKey) {
    const target = e.target;
    if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA') return;
    e.preventDefault();
    const idx = LAYER_IDS.indexOf(state.selectedLayerId);
    const next = LAYER_IDS[(idx + 1) % LAYER_IDS.length];
    selectLayer(next);
    return;
  }

  // Ctrl+Enter: compile
  if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) {
    e.preventDefault();
    compile(getEditorValue());
    return;
  }

  // Ctrl+S: save shader
  if (e.key === 's' && (e.ctrlKey || e.metaKey) && !e.shiftKey) {
    e.preventDefault();
    const source = getEditorValue();
    const blob = new Blob([source], { type: 'text/plain' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'shader.fs';
    a.click();
    return;
  }

  // Ctrl+Shift+S: screenshot
  if (e.key === 'S' && (e.ctrlKey || e.metaKey) && e.shiftKey) {
    e.preventDefault();
    const link = document.createElement('a');
    link.download = 'shaderclaw.png';
    link.href = glCanvas.toDataURL('image/png');
    link.click();
    return;
  }

  // Space: play/pause
  if (e.key === ' ' && e.target.tagName !== 'INPUT' && e.target.tagName !== 'TEXTAREA') {
    e.preventDefault();
    compositionPlaying = !compositionPlaying;
    state.playing = compositionPlaying;
    if (playBtn) playBtn.textContent = compositionPlaying ? '⏸' : '▶';
    emit('play:toggle', { playing: compositionPlaying });
    return;
  }

  // F: fullscreen
  if (e.key === 'f' && e.target.tagName !== 'INPUT' && e.target.tagName !== 'TEXTAREA') {
    if (document.fullscreenElement) document.exitFullscreen();
    else viewport.requestFullscreen();
    return;
  }

  // N: toggle NDI send
  if (e.key === 'n' && e.target.tagName !== 'INPUT' && e.target.tagName !== 'TEXTAREA') {
    const ws = getWebSocket();
    if (isNdiSending()) {
      stopNdiSend();
    } else {
      startNdiSend(ws, glCanvas);
    }
    return;
  }

  // Ctrl+1/2/3: switch editor mode
  if ((e.ctrlKey || e.metaKey) && ['1', '2', '3'].includes(e.key)) {
    e.preventDefault();
    const modes = ['code', 'nodes', 'ai'];
    state.editorMode = modes[parseInt(e.key) - 1];
    emit('editor:mode', { mode: state.editorMode });
    // Update tabs
    editorArea.querySelectorAll('.editor-tab').forEach(t => t.classList.toggle('active', t.dataset.mode === state.editorMode));
    editorArea.querySelectorAll('.editor-panel').forEach(p => p.classList.toggle('active', p.dataset.mode === state.editorMode));
  }
});

// === MCP Bridge ===

function handleMcpAction(msg, respond) {
  const { action, params } = msg;

  switch (action) {
    case 'load_shader': {
      const result = compile(params.code);
      respond({
        ok: result.ok,
        errors: result.errors,
        inputs: result.ok ? getLayer(state.selectedLayerId).inputs.map(inp => ({
          name: inp.NAME, type: inp.TYPE, value: getLayer(state.selectedLayerId).inputValues[inp.NAME],
          min: inp.MIN, max: inp.MAX, default: inp.DEFAULT, values: inp.VALUES, labels: inp.LABELS, maxLength: inp.MAX_LENGTH,
        })) : [],
      });
      break;
    }

    case 'get_shader': {
      respond({ code: getEditorValue() });
      break;
    }

    case 'set_parameter': {
      const layer = getLayer(state.selectedLayerId);
      layer.inputValues[params.name] = params.value;
      updateControlUI(detailPanel, params.name, params.value);
      respond({ ok: true });
      break;
    }

    case 'get_parameters': {
      const layer = getLayer(state.selectedLayerId);
      respond({
        inputs: (layer.inputs || []).map(inp => ({
          name: inp.NAME, type: inp.TYPE, value: layer.inputValues[inp.NAME],
          min: inp.MIN, max: inp.MAX, default: inp.DEFAULT, values: inp.VALUES, labels: inp.LABELS, maxLength: inp.MAX_LENGTH,
        })),
      });
      break;
    }

    case 'screenshot': {
      respond({ dataUrl: glCanvas.toDataURL('image/png') });
      break;
    }

    case 'set_layer_visibility': {
      const layer = getLayer(params.layerId);
      if (!layer) { respond(null, `Unknown layer: ${params.layerId}`); return; }
      setLayerVisibility(params.layerId, params.visible);
      respond({ ok: true });
      break;
    }

    case 'set_layer_opacity': {
      const layer = getLayer(params.layerId);
      if (!layer) { respond(null, `Unknown layer: ${params.layerId}`); return; }
      layer.opacity = params.opacity;
      emit('layer:opacity', { layerId: params.layerId, opacity: params.opacity });
      respond({ ok: true });
      break;
    }

    case 'set_layer_blend': {
      const layer = getLayer(params.layerId);
      if (!layer) { respond(null, `Unknown layer: ${params.layerId}`); return; }
      layer.blendMode = params.blendMode;
      emit('layer:blend', { layerId: params.layerId, blendMode: params.blendMode });
      respond({ ok: true });
      break;
    }

    case 'load_shader_to_layer': {
      const result = compileToLayer(isfRenderer, params.layerId, params.code);
      respond({ ok: result.ok, errors: result.errors });
      break;
    }

    case 'get_layers': {
      const layers = LAYER_IDS.map(id => {
        const l = getLayer(id);
        return { id, visible: l.visible, opacity: l.opacity, blendMode: l.blendMode };
      });
      respond({ layers });
      break;
    }

    case 'enable_mediapipe': {
      mediaPipeMgr.init(params.modes || { hand: true }).then(() => {
        respond({ ok: true, label: mediaPipeMgr.getLabel() });
      }).catch(e => {
        respond(null, e.message);
      });
      break;
    }

    case 'get_audio_levels': {
      respond(getAudioLevels());
      break;
    }

    case 'set_destination': {
      state.destination = params.destination;
      emit('destination:change', { destination: params.destination });
      respond({ ok: true });
      break;
    }

    case 'get_destination': {
      respond({ destination: state.destination });
      break;
    }

    case 'route_media': {
      const layer = getLayer(params.layerId);
      if (!layer) { respond(null, `Unknown layer: ${params.layerId}`); return; }
      const imageInputs = (layer.inputs || []).filter(inp => inp.TYPE === 'image');
      const slot = params.slot || (imageInputs[0] && imageInputs[0].NAME);
      if (slot) {
        layer.inputValues[slot] = params.mediaId;
        autoBindTextures();
      }
      respond({ ok: true, slot });
      break;
    }

    case 'load_graph': {
      // Store graph data on the layer and compile generated GLSL
      const layer = getLayer(params.layerId);
      if (!layer) { respond(null, `Unknown layer: ${params.layerId}`); return; }
      layer._graph = params.graph;
      // Import codegen dynamically to avoid circular deps
      import('./nodes/codegen.js').then(({ graphToISF }) => {
        const isfSource = graphToISF(params.graph);
        const result = compileToLayer(isfRenderer, params.layerId, isfSource);
        respond({ ok: result.ok, errors: result.errors });
      }).catch(e => respond(null, e.message));
      break;
    }

    case 'get_graph': {
      const layer = getLayer(params.layerId);
      if (!layer) { respond(null, `Unknown layer: ${params.layerId}`); return; }
      respond({ graph: layer._graph || null });
      break;
    }

    case 'add_node': {
      const layer = getLayer(params.layerId);
      if (!layer) { respond(null, `Unknown layer: ${params.layerId}`); return; }
      if (!layer._graph) layer._graph = { nodes: [], edges: [] };
      const nodeId = 'n' + (layer._graph.nodes.length + 1);
      layer._graph.nodes.push({
        id: nodeId,
        type: params.type,
        position: params.position || [100, 100],
        params: params.params || {},
      });
      respond({ ok: true, nodeId });
      break;
    }

    case 'connect_nodes': {
      const layer = getLayer(params.layerId);
      if (!layer || !layer._graph) { respond(null, `No graph on layer: ${params.layerId}`); return; }
      layer._graph.edges.push({
        from: params.from, output: params.output,
        to: params.to, input: params.input,
      });
      respond({ ok: true });
      break;
    }

    case 'remove_node': {
      const layer = getLayer(params.layerId);
      if (!layer || !layer._graph) { respond(null, `No graph on layer: ${params.layerId}`); return; }
      layer._graph.nodes = layer._graph.nodes.filter(n => n.id !== params.nodeId);
      layer._graph.edges = layer._graph.edges.filter(e => e.from !== params.nodeId && e.to !== params.nodeId);
      respond({ ok: true });
      break;
    }

    case 'generate_variations': {
      // Store direction in state and emit for UI to handle
      state.aiDirection = params.direction;
      state.aiVariationCount = params.count || 4;
      emit('ai:generate', { direction: params.direction, count: params.count || 4, destination: params.destination });
      respond({ ok: true, message: 'Generation request sent to AI breeding UI' });
      break;
    }

    case 'evolve_variations': {
      emit('ai:evolve', { favorites: params.favorites, count: params.count || 4 });
      respond({ ok: true, message: 'Evolution request sent to AI breeding UI' });
      break;
    }

    default:
      respond(null, `Unknown action: ${action}`);
  }
}

mcpConnect(handleMcpAction, isfRenderer.gl);

// === window.shaderClaw API ===

window.shaderClaw = {
  loadSource,
  compile: () => compile(getEditorValue()),
  getSource: getEditorValue,
  getErrors: () => lastErrors,
  getInputs: () => {
    const layer = getLayer(state.selectedLayerId);
    return (layer.inputs || []).map(inp => ({
      name: inp.NAME, type: inp.TYPE, value: layer.inputValues[inp.NAME],
      min: inp.MIN, max: inp.MAX, default: inp.DEFAULT, values: inp.VALUES, labels: inp.LABELS, maxLength: inp.MAX_LENGTH,
    }));
  },
  setParameter: (name, value) => {
    const layer = getLayer(state.selectedLayerId);
    layer.inputValues[name] = value;
    updateControlUI(detailPanel, name, value);
    return { ok: true };
  },
  screenshot: () => glCanvas.toDataURL('image/png'),
  getFocusedLayer: () => state.selectedLayerId,
  setLayerVisibility: (id, v) => { setLayerVisibility(id, v); return { ok: true }; },
  setLayerOpacity: (id, o) => { getLayer(id).opacity = o; emit('layer:opacity', { layerId: id, opacity: o }); return { ok: true }; },
  getAudioLevels,
  enableMediaPipe: async (modes) => { await mediaPipeMgr.init(modes); return { ok: true, label: mediaPipeMgr.getLabel() }; },
  layers: state.layers,
  getLayer,
  loadScene,
};

// === Load Defaults ===

async function init() {
  // Load shader manifest
  const manifest = await loadManifest();

  // Load default shader to background layer
  compileToLayer(isfRenderer, 'background', DEFAULT_SHADER);

  // Load text shader to text layer
  const textEntry = manifest.find(e => e.title === 'Text' || (e.file && e.file.includes('text')));
  if (textEntry) {
    try {
      await loadShaderToLayer(isfRenderer, 'text', 'shaders', textEntry.file);
    } catch (e) {
      console.warn('Failed to load default text shader:', e);
    }
  }

  // Load default scene to 3D layer
  const sceneEntry = manifest.find(e => e.type === 'scene');
  if (sceneEntry) {
    try {
      await loadScene('scenes', sceneEntry.file);
      // Default: flip 3D layer vertically
      const layer3d = getLayer('3d');
      layer3d.sceneFlipV = true;
      const flipVBtn = document.querySelector('.scene-flip-v[data-layer="scene"]');
      if (flipVBtn) flipVBtn.classList.add('active');
    } catch (e) {
      console.warn('Failed to load default scene:', e);
    }
  }

  // Select background layer and show its source in editor
  selectLayer('background');
  setEditorValue(DEFAULT_SHADER);

  // Start composition loop
  requestAnimationFrame(compositionLoop);
}

init().catch(e => console.error('[ShaderClaw] Init failed:', e));

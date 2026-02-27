// 7-Layer Model + Per-Layer Render Logic
// Manages FBO creation, layer rendering dispatch, and the composition pipeline

import { state, LAYER_IDS, getLayer, emit } from './state.js';
import { buildFragmentShader, VERT_SHADER, parseISF } from './isf.js';
import { getAudioState, updateAudioUniforms } from './audio.js';
import { getFontState } from './media.js';

const FBO_WIDTH = 1920;
const FBO_HEIGHT = 1080;

/**
 * Initialize FBOs for all layers
 * @param {Renderer} renderer
 */
export function initLayerFBOs(renderer) {
  for (const id of LAYER_IDS) {
    const layer = getLayer(id);
    if (!layer.fbo) {
      layer.fbo = renderer.createFBO(FBO_WIDTH, FBO_HEIGHT);
    }
  }
}

/**
 * Recreate FBOs after context loss
 */
export function recreateLayerFBOs(renderer) {
  for (const id of LAYER_IDS) {
    const layer = getLayer(id);
    layer.fbo = renderer.createFBO(FBO_WIDTH, FBO_HEIGHT);
    layer.program = null;
    layer.uniformLocs = {};
    // Recompile from stored source
    if (layer._isfSource) {
      compileToLayer(renderer, id, layer._isfSource);
    }
  }
}

/**
 * Compile ISF source to a specific layer
 * @param {Renderer} renderer
 * @param {string} layerId
 * @param {string} source - ISF shader source
 * @returns {{ ok: boolean, errors: string|null, inputs: Array }}
 */
export function compileToLayer(renderer, layerId, source) {
  const layer = getLayer(layerId);
  if (!layer) return { ok: false, errors: `Unknown layer: ${layerId}` };

  // Store for context-loss recovery
  layer._isfSource = source;

  const { frag, parsed, headerLineCount } = buildFragmentShader(source);
  renderer._headerLines = headerLineCount;

  const result = renderer.compileForLayer(layer, VERT_SHADER, frag);
  if (!result.ok) return result;

  // Store parsed inputs
  layer.inputs = parsed.inputs || [];

  // Reset input values with defaults
  const oldValues = { ...layer.inputValues };
  layer.inputValues = {};
  for (const inp of layer.inputs) {
    const name = inp.NAME;
    // Preserve old values if same param exists
    if (name in oldValues) {
      layer.inputValues[name] = oldValues[name];
    } else if (inp.TYPE === 'float' || inp.TYPE === 'long') {
      layer.inputValues[name] = inp.DEFAULT != null ? inp.DEFAULT : 0;
    } else if (inp.TYPE === 'color') {
      layer.inputValues[name] = inp.DEFAULT ? [...inp.DEFAULT] : [1, 1, 1, 1];
    } else if (inp.TYPE === 'bool') {
      layer.inputValues[name] = !!inp.DEFAULT;
    } else if (inp.TYPE === 'point2D') {
      layer.inputValues[name] = inp.DEFAULT ? [...inp.DEFAULT] : [0, 0];
    } else if (inp.TYPE === 'text') {
      const maxLen = inp.MAX_LENGTH || 12;
      const def = (inp.DEFAULT || '').toUpperCase();
      for (let i = 0; i < maxLen; i++) {
        const ch = def[i];
        layer.inputValues[name + '_' + i] = (!ch || ch === ' ') ? 26 : Math.max(0, Math.min(25, ch.charCodeAt(0) - 65));
      }
      layer.inputValues[name + '_len'] = def.replace(/\s+$/, '').length;
    }
  }

  // Handle PASSES metadata for multi-pass shaders
  layer.passes = null;
  if (parsed.meta && Array.isArray(parsed.meta.PASSES) && parsed.meta.PASSES.length > 0) {
    layer.passes = parsed.meta.PASSES.map(p => {
      let pw = FBO_WIDTH, ph = FBO_HEIGHT;
      try { if (p.WIDTH) pw = eval(p.WIDTH.replace(/\$WIDTH/g, FBO_WIDTH).replace(/\$HEIGHT/g, FBO_HEIGHT)); } catch(e) {}
      try { if (p.HEIGHT) ph = eval(p.HEIGHT.replace(/\$WIDTH/g, FBO_WIDTH).replace(/\$HEIGHT/g, FBO_HEIGHT)); } catch(e) {}
      const pass = {
        target: p.TARGET || null,
        persistent: !!p.PERSISTENT,
        width: pw,
        height: ph,
        ppFBO: null,
      };
      if (pass.target) {
        pass.ppFBO = renderer.createPingPongFBO(pass.width, pass.height);
      }
      return pass;
    });
  }

  emit('layer:compiled', { layerId });
  return { ok: true, errors: null, inputs: layer.inputs };
}

/**
 * Load shader file and compile to layer
 */
export async function loadShaderToLayer(renderer, layerId, folder, file) {
  const url = `${folder}/${file}`;
  const resp = await fetch(url);
  if (!resp.ok) throw new Error(`Failed to load shader: ${url}`);
  const source = await resp.text();
  return { ...compileToLayer(renderer, layerId, source), source };
}

/**
 * The main composition render loop
 * Call this once per frame from app.js
 *
 * Render order (per spec):
 * 1. Background → FBO[0]
 * 2. Media → FBO[1]
 * 3. 3D (with ISF materials) → FBO[2]
 * 4. AV (with audio uniforms) → FBO[3]
 * 5. Composite FBO[0-3] → tempComposite (for Effects input)
 * 6. Effects (reads tempComposite) → FBO[4]
 * 7. Text → FBO[5]
 * 8. Overlay → FBO[6]
 * 9. Final composite: blend all 7 FBOs
 */
export function renderComposition(renderer, sceneRenderer, mediaPipeMgr, tempCompositeFBO) {
  const gl = renderer.gl;
  const audioState = getAudioState();
  const fontState = getFontState();

  // Update audio analysis
  updateAudioUniforms(gl);

  // Get layers in render order
  const layerOrder = LAYER_IDS;
  const orderedLayers = layerOrder.map(id => getLayer(id));

  // --- Phase 1: Render individual layers to their FBOs ---

  // Background layer (index 0)
  const bgLayer = getLayer('background');
  if (bgLayer.visible && bgLayer.program) {
    renderer.renderLayerToFBO(bgLayer, audioState, mediaPipeMgr, fontState, null);
  }

  // Media layer (index 1)
  const mediaLayer = getLayer('media');
  if (mediaLayer.visible && mediaLayer.program) {
    renderer.renderLayerToFBO(mediaLayer, audioState, mediaPipeMgr, fontState, null);
  }

  // 3D layer (index 2) - rendered by SceneRenderer
  const layer3d = getLayer('3d');
  let sceneTexture = null;
  if (layer3d.visible && sceneRenderer && sceneRenderer.sceneDef) {
    sceneRenderer.render();
    // Upload Three.js canvas to a GL texture for the compositor
    const threeCanvas = sceneRenderer.canvas;
    if (!layer3d._sceneTexture) {
      layer3d._sceneTexture = gl.createTexture();
      gl.bindTexture(gl.TEXTURE_2D, layer3d._sceneTexture);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    } else {
      gl.bindTexture(gl.TEXTURE_2D, layer3d._sceneTexture);
    }
    // Frame-skip: upload every other frame (visual 30fps, compositor stays 60fps)
    layer3d._frameCount = (layer3d._frameCount || 0) + 1;
    if (layer3d._frameCount % 2 === 0) {
      gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, threeCanvas);
    }
    sceneTexture = layer3d._sceneTexture;
  }

  // AV layer (index 3) - audio visualization
  const avLayer = getLayer('av');
  if (avLayer.visible && avLayer.program) {
    renderer.renderLayerToFBO(avLayer, audioState, mediaPipeMgr, fontState, null);
  }

  // --- Phase 2: Composite layers 0-3 for Effects input ---
  let effectsInputTexture = null;
  const effectsLayer = getLayer('effects');
  if (effectsLayer.visible && effectsLayer.program && tempCompositeFBO) {
    // Render partial composite of first 4 layers
    const partialLayers = orderedLayers.slice(0, 4);
    // Pad to 7 slots with null for the compositor shader
    while (partialLayers.length < 7) partialLayers.push(null);
    renderer.renderPartialComposite(partialLayers, sceneTexture, state.background, tempCompositeFBO);
    effectsInputTexture = tempCompositeFBO.texture;
  }

  // --- Phase 3: Effects layer (index 4) ---
  if (effectsLayer.visible && effectsLayer.program) {
    renderer.renderLayerToFBO(effectsLayer, audioState, mediaPipeMgr, fontState, effectsInputTexture);
  }

  // --- Phase 4: Text layer (index 5) ---
  const textLayer = getLayer('text');
  if (textLayer.visible && textLayer.program) {
    renderer.renderLayerToFBO(textLayer, audioState, mediaPipeMgr, fontState, null);
  }

  // --- Phase 5: Overlay layer (index 6) ---
  const overlayLayer = getLayer('overlay');
  if (overlayLayer.visible && overlayLayer.program) {
    renderer.renderLayerToFBO(overlayLayer, audioState, mediaPipeMgr, fontState, null);
  }

  // Re-upload animated GIF (throttle to 30fps — GIFs rarely exceed this)
  if (overlayLayer.visible && overlayLayer._gifElement && overlayLayer.fbo) {
    overlayLayer._gifFrameCount = (overlayLayer._gifFrameCount || 0) + 1;
    if (overlayLayer._gifFrameCount % 2 === 0) {
      const gl = renderer.gl;
      gl.bindTexture(gl.TEXTURE_2D, overlayLayer.fbo.texture);
      gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
      gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, overlayLayer._gifElement);
      gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, false);
    }
  }

  // --- Phase 6: Final composite ---
  renderer.renderCompositor(orderedLayers, sceneTexture, state.background);
}

/**
 * Auto-bind media textures to all layers that have image inputs
 */
export function autoBindTextures() {
  // Pre-filter compatible media once (not per-layer)
  const compatible = state.mediaInputs.filter(m => m.type === 'image' || m.type === 'video' || m.type === 'svg');

  for (const id of LAYER_IDS) {
    const layer = getLayer(id);
    if (!layer.inputs) continue;

    const imageInputs = layer.inputs.filter(inp => inp.TYPE === 'image');
    let imageIdx = 0;

    for (const inp of imageInputs) {
      const mediaId = layer.inputValues[inp.NAME];
      if (mediaId) {
        const media = state.mediaInputs.find(m => String(m.id) === String(mediaId));
        if (media && media.glTexture) {
          layer.textures[inp.NAME] = {
            glTexture: media.glTexture,
            isVideo: media.type === 'video',
            element: media.element,
            flipH: media._webcamFlip || false,
            flipV: media._webcamFlipV || false,
            _isNdi: media._isNdi || false,
          };
        }
      } else {
        // Auto-bind by index
        if (compatible[imageIdx] && compatible[imageIdx].glTexture) {
          layer.textures[inp.NAME] = {
            glTexture: compatible[imageIdx].glTexture,
            isVideo: compatible[imageIdx].type === 'video',
            element: compatible[imageIdx].element,
            flipH: compatible[imageIdx]._webcamFlip || false,
            flipV: compatible[imageIdx]._webcamFlipV || false,
            _isNdi: compatible[imageIdx]._isNdi || false,
          };
          layer.inputValues[inp.NAME] = compatible[imageIdx].id;
        }
        imageIdx++;
      }
    }
  }
}

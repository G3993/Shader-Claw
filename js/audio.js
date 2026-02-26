// Audio Analysis Pipeline
// Extracted from ShaderClaw monolith

import { state, emit } from './state.js';

// Audio state is stored in state.audio but managed here
let audioCtx = null;
let audioAnalyser = null;
let audioDataArray = null;
let audioFFTGLTexture = null;
let audioFFTThreeTexture = null;
let audioLevel = 0, audioBass = 0, audioMid = 0, audioHigh = 0;
let activeAudioEntry = null;

/**
 * Initialize audio context (call on user gesture)
 */
export function initAudioContext() {
  if (audioCtx) return audioCtx;
  audioCtx = new (window.AudioContext || window.webkitAudioContext)();
  audioAnalyser = audioCtx.createAnalyser();
  audioAnalyser.fftSize = 256;
  audioDataArray = new Uint8Array(audioAnalyser.frequencyBinCount); // 128 bins
  // Sync to state
  state.audio.ctx = audioCtx;
  state.audio.analyser = audioAnalyser;
  state.audio.dataArray = audioDataArray;
  return audioCtx;
}

/**
 * Connect a media element to the audio analyser
 */
export function connectAudioSource(mediaElement) {
  if (!audioCtx) initAudioContext();
  // Disconnect previous
  if (activeAudioEntry && activeAudioEntry._sourceNode) {
    try { activeAudioEntry._sourceNode.disconnect(); } catch(e) {}
  }
  const source = audioCtx.createMediaElementSource(mediaElement);
  source.connect(audioAnalyser);
  audioAnalyser.connect(audioCtx.destination);
  activeAudioEntry = { element: mediaElement, _sourceNode: source };
  state.audio.activeEntry = activeAudioEntry;
  return source;
}

/**
 * Set active audio entry directly (for mic, etc.)
 */
export function setActiveAudioEntry(entry) {
  activeAudioEntry = entry;
  state.audio.activeEntry = entry;
}

/**
 * Per-frame audio analysis — call from render loop
 * Updates FFT texture and level/bass/mid/high values
 * @param {WebGLRenderingContext} gl
 */
export function updateAudioUniforms(gl) {
  if (!audioAnalyser || !activeAudioEntry) {
    audioLevel = audioBass = audioMid = audioHigh = 0;
    _syncState();
    return;
  }
  audioAnalyser.getByteFrequencyData(audioDataArray);
  const len = audioDataArray.length; // 128 bins

  // RMS level
  let sum = 0;
  for (let i = 0; i < len; i++) sum += audioDataArray[i] * audioDataArray[i];
  audioLevel = Math.sqrt(sum / len) / 255.0;

  // Bass (0-15), Mid (16-80), High (81-127)
  let bassSum = 0, midSum = 0, highSum = 0;
  for (let i = 0; i < 16; i++) bassSum += audioDataArray[i];
  for (let i = 16; i < 81; i++) midSum += audioDataArray[i];
  for (let i = 81; i < len; i++) highSum += audioDataArray[i];
  audioBass = bassSum / (16 * 255);
  audioMid = midSum / (65 * 255);
  audioHigh = highSum / (47 * 255);

  // Upload FFT to GL texture (256x1 LUMINANCE)
  if (!audioFFTGLTexture) {
    audioFFTGLTexture = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, audioFFTGLTexture);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    state.audio.fftGLTexture = audioFFTGLTexture;
  }
  gl.bindTexture(gl.TEXTURE_2D, audioFFTGLTexture);
  gl.texImage2D(gl.TEXTURE_2D, 0, gl.LUMINANCE, len, 1, 0, gl.LUMINANCE, gl.UNSIGNED_BYTE, audioDataArray);

  // Update THREE DataTexture (if THREE is available)
  if (window.THREE) {
    if (!audioFFTThreeTexture) {
      audioFFTThreeTexture = new THREE.DataTexture(audioDataArray, len, 1, THREE.LuminanceFormat);
      audioFFTThreeTexture.needsUpdate = true;
      state.audio.fftThreeTexture = audioFFTThreeTexture;
    } else {
      audioFFTThreeTexture.needsUpdate = true;
    }
  }

  _syncState();

  // Update audio bar UI
  if (activeAudioEntry && activeAudioEntry.id) {
    const bar = document.querySelector('.audio-bar-fill[data-audio-id="' + activeAudioEntry.id + '"]');
    if (bar) bar.style.width = (audioLevel * 100) + '%';
  }
}

function _syncState() {
  state.audio.level = audioLevel;
  state.audio.bass = audioBass;
  state.audio.mid = audioMid;
  state.audio.high = audioHigh;
  state.audio.fftGLTexture = audioFFTGLTexture;
}

/**
 * Get current audio levels (for external queries)
 */
export function getAudioLevels() {
  return {
    level: audioLevel,
    bass: audioBass,
    mid: audioMid,
    high: audioHigh,
    hasAudio: !!activeAudioEntry
  };
}

/**
 * Get audio state for passing to renderer
 */
export function getAudioState() {
  return {
    level: audioLevel,
    bass: audioBass,
    mid: audioMid,
    high: audioHigh,
    fftGLTexture: audioFFTGLTexture,
    fftThreeTexture: audioFFTThreeTexture,
  };
}

/**
 * Cleanup
 */
export function disposeAudio() {
  if (activeAudioEntry && activeAudioEntry._sourceNode) {
    try { activeAudioEntry._sourceNode.disconnect(); } catch(e) {}
  }
  activeAudioEntry = null;
  if (audioCtx) {
    try { audioCtx.close(); } catch(e) {}
    audioCtx = null;
  }
  audioAnalyser = null;
  audioDataArray = null;
  audioFFTGLTexture = null;
  audioFFTThreeTexture = null;
}

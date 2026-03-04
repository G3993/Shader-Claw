// Remote GPU Renderer Mirror
// Mirrors shader compilations, parameter changes, and audio to a remote pod.
// All calls are fire-and-forget — remote failures never block local rendering.

import { state, on } from './state.js';

// State
let remoteUrl = localStorage.getItem('sc-remote-url') || '';
let connected = false;
let statusCallback = null;
let pollTimer = null;
let remoteInfo = null; // { gpu, fps, shader, resolution }

// ── Public API ────────────────────────────────────────────────────

export function getRemoteUrl() { return remoteUrl; }
export function isRemoteConnected() { return connected; }
export function getRemoteInfo() { return remoteInfo; }

export function setStatusCallback(fn) { statusCallback = fn; }

function notifyStatus(status, info) {
  if (statusCallback) statusCallback(status, info);
}

export async function connectRemote(url) {
  if (!url) return;
  // Normalize
  if (!url.startsWith('http')) url = 'http://' + url;
  url = url.replace(/\/+$/, '');
  remoteUrl = url;
  localStorage.setItem('sc-remote-url', url);

  notifyStatus('connecting', null);

  try {
    const res = await fetch(url + '/api/isf/status', { signal: AbortSignal.timeout(5000) });
    const data = await res.json();
    connected = true;
    remoteInfo = data;
    notifyStatus('connected', data);
    startPolling();
    return data;
  } catch (e) {
    connected = false;
    remoteInfo = null;
    notifyStatus('error', e.message);
    return null;
  }
}

export function disconnectRemote() {
  connected = false;
  remoteUrl = '';
  remoteInfo = null;
  localStorage.removeItem('sc-remote-url');
  stopPolling();
  notifyStatus('disconnected', null);
}

// ── Mirror Functions (fire-and-forget) ────────────────────────────

export function mirrorShaderLoad(name, source) {
  if (!connected) return;
  const body = source ? { name, source } : { name };
  post('/api/isf/load', body);
}

export function mirrorParams(params) {
  if (!connected) return;
  post('/api/isf/params', params);
}

export function mirrorAudio(levels) {
  if (!connected) return;
  post('/api/isf/audio', levels);
}

// ── Internal ──────────────────────────────────────────────────────

function post(path, body) {
  fetch(remoteUrl + path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  }).catch(() => {}); // silent
}

function startPolling() {
  stopPolling();
  pollTimer = setInterval(async () => {
    if (!connected) return;
    try {
      const res = await fetch(remoteUrl + '/api/isf/status', { signal: AbortSignal.timeout(3000) });
      remoteInfo = await res.json();
      notifyStatus('connected', remoteInfo);
    } catch {
      connected = false;
      remoteInfo = null;
      stopPolling();
      notifyStatus('error', 'Connection lost');
    }
  }, 3000);
}

function stopPolling() {
  if (pollTimer) { clearInterval(pollTimer); pollTimer = null; }
}

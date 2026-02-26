// Pixel Streaming — UE project detection, signaling server, streamer launch, status tracking
// Communicates with server.js via MCP bridge for process management

import { state, emit } from './state.js';
import { getWebSocket, isConnected } from './mcp-bridge.js';

// State
let streamStatus = 'idle'; // idle | connecting | streaming | error
let activeProject = null;
let statusPollInterval = null;

/**
 * Request available UE projects from server
 * @returns {Promise<Array<{name:string, path:string}>>}
 */
export async function listProjects() {
  const ws = getWebSocket();
  if (!ws || !isConnected()) throw new Error('Not connected to server');

  return new Promise((resolve, reject) => {
    const id = Date.now();
    const timeout = setTimeout(() => reject(new Error('Timeout listing projects')), 5000);

    const handler = (e) => {
      if (typeof e.data !== 'string') return;
      try {
        const msg = JSON.parse(e.data);
        if (msg.id !== id) return;
        ws.removeEventListener('message', handler);
        clearTimeout(timeout);
        if (msg.error) reject(new Error(msg.error));
        else resolve(msg.result.projects || []);
      } catch {}
    };
    ws.addEventListener('message', handler);
    ws.send(JSON.stringify({ id, action: 'pixel_stream_list_projects' }));
  });
}

/**
 * Start pixel streaming for a UE project
 * @param {string} project - Project name (e.g. 'VJ', 'zero', 'STUDIO')
 */
export async function startStream(project) {
  const ws = getWebSocket();
  if (!ws || !isConnected()) throw new Error('Not connected to server');

  setStatus('connecting');
  activeProject = project;

  return new Promise((resolve, reject) => {
    const id = Date.now();
    const timeout = setTimeout(() => {
      setStatus('error');
      reject(new Error('Timeout starting pixel stream'));
    }, 30000); // 30s — UE takes time to launch

    const handler = (e) => {
      if (typeof e.data !== 'string') return;
      try {
        const msg = JSON.parse(e.data);
        if (msg.id !== id) return;
        ws.removeEventListener('message', handler);
        clearTimeout(timeout);
        if (msg.error) {
          setStatus('error');
          reject(new Error(msg.error));
        } else {
          setStatus('streaming');
          startStatusPolling();
          resolve(msg.result);
        }
      } catch {}
    };
    ws.addEventListener('message', handler);
    ws.send(JSON.stringify({ id, action: 'pixel_stream_start', params: { project } }));
  });
}

/**
 * Stop pixel streaming
 */
export async function stopStream() {
  const ws = getWebSocket();
  if (!ws || !isConnected()) throw new Error('Not connected to server');

  stopStatusPolling();

  return new Promise((resolve, reject) => {
    const id = Date.now();
    const timeout = setTimeout(() => reject(new Error('Timeout stopping stream')), 10000);

    const handler = (e) => {
      if (typeof e.data !== 'string') return;
      try {
        const msg = JSON.parse(e.data);
        if (msg.id !== id) return;
        ws.removeEventListener('message', handler);
        clearTimeout(timeout);
        setStatus('idle');
        activeProject = null;
        if (msg.error) reject(new Error(msg.error));
        else resolve(msg.result);
      } catch {}
    };
    ws.addEventListener('message', handler);
    ws.send(JSON.stringify({ id, action: 'pixel_stream_stop' }));
  });
}

/**
 * Get current streaming status from server
 */
export async function getStatus() {
  const ws = getWebSocket();
  if (!ws || !isConnected()) return { status: 'disconnected' };

  return new Promise((resolve) => {
    const id = Date.now();
    const timeout = setTimeout(() => resolve({ status: streamStatus }), 3000);

    const handler = (e) => {
      if (typeof e.data !== 'string') return;
      try {
        const msg = JSON.parse(e.data);
        if (msg.id !== id) return;
        ws.removeEventListener('message', handler);
        clearTimeout(timeout);
        if (msg.result) {
          if (msg.result.status) setStatus(msg.result.status);
          resolve(msg.result);
        } else {
          resolve({ status: streamStatus });
        }
      } catch {}
    };
    ws.addEventListener('message', handler);
    ws.send(JSON.stringify({ id, action: 'pixel_stream_status' }));
  });
}

function setStatus(newStatus) {
  streamStatus = newStatus;
  emit('pixel-stream:status', { status: newStatus, project: activeProject });
}

function startStatusPolling() {
  stopStatusPolling();
  statusPollInterval = setInterval(async () => {
    try {
      await getStatus();
    } catch {}
  }, 5000);
}

function stopStatusPolling() {
  if (statusPollInterval) {
    clearInterval(statusPollInterval);
    statusPollInterval = null;
  }
}

export function getStreamStatus() { return streamStatus; }
export function getActiveProject() { return activeProject; }

// MCP Bridge — WebSocket connection to ShaderClaw server.js
// Handles message routing, NDI binary frames, and auto-reconnect

import { FRAME_TYPE_NDI_VIDEO, handleNdiVideoFrame, handleNdiResponse } from './media.js';

let ws = null;
let reconnectTimer = null;
let onActionCallback = null;
let _reconnectDelay = 2000;
let _reconnectAttempts = 0;

/**
 * Connect to the ShaderClaw MCP server
 * @param {function} onAction - callback for incoming MCP actions: (msg) => response
 * @param {WebGLRenderingContext} gl - GL context for NDI frame upload
 */
export function connect(onAction, gl) {
  onActionCallback = onAction;
  _connect(gl);
}

function _connect(gl) {
  const url = 'ws://' + location.host;
  try {
    ws = new WebSocket(url);
  } catch (e) {
    console.warn('[MCP] WebSocket creation failed:', e.message);
    _scheduleReconnect(gl);
    return;
  }

  ws.binaryType = 'arraybuffer';

  ws.onopen = () => {
    console.log('[MCP] Connected');
    _reconnectAttempts = 0;
    _reconnectDelay = 2000;
    if (reconnectTimer) { clearTimeout(reconnectTimer); reconnectTimer = null; }
  };

  ws.onclose = () => {
    ws = null;
    _scheduleReconnect(gl);
  };

  ws.onerror = () => {};

  ws.onmessage = (event) => {
    if (event.data instanceof ArrayBuffer) {
      const arr = new Uint8Array(event.data);
      if (arr[0] === FRAME_TYPE_NDI_VIDEO) {
        handleNdiVideoFrame(event.data, gl);
      }
      return;
    }

    try {
      const msg = JSON.parse(event.data);

      // Negative IDs are NDI responses
      if (msg.id < 0) {
        handleNdiResponse(msg);
        return;
      }

      // Positive IDs are MCP actions from server
      if (msg.id > 0 && msg.action && onActionCallback) {
        const respond = (result, error) => {
          if (ws && ws.readyState === WebSocket.OPEN) {
            ws.send(JSON.stringify({ id: msg.id, result, error }));
          }
        };
        onActionCallback(msg, respond);
      }
    } catch (e) {
      console.warn('[MCP] Bad message:', e);
    }
  };
}

function _scheduleReconnect(gl) {
  if (reconnectTimer) return;
  _reconnectAttempts++;
  // Back off: 2s, 4s, 8s, capped at 30s
  _reconnectDelay = Math.min(30000, 2000 * Math.pow(2, Math.min(_reconnectAttempts - 1, 4)));
  if (_reconnectAttempts <= 3) {
    console.log(`[MCP] Reconnecting in ${_reconnectDelay / 1000}s...`);
  } else if (_reconnectAttempts === 4) {
    console.log('[MCP] Server not available. Will keep retrying silently. Start server.js on port 7777 to connect.');
  }
  reconnectTimer = setTimeout(() => {
    reconnectTimer = null;
    _connect(gl);
  }, _reconnectDelay);
}

export function getWebSocket() { return ws; }

export function isConnected() {
  return ws && ws.readyState === WebSocket.OPEN;
}

export function send(data) {
  if (ws && ws.readyState === WebSocket.OPEN) {
    ws.send(typeof data === 'string' ? data : JSON.stringify(data));
  }
}

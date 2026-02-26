// AI Copilot — Chat panel for AI-assisted shader editing
// Wraps existing MCP bridge to communicate with Claude

import { state, on, getLayer, emit } from '../state.js';
import { getWebSocket, isConnected } from '../mcp-bridge.js';

export class CopilotPanel {
  constructor(containerEl) {
    this.container = containerEl;
    this.messages = [];
    this._build();
  }

  _build() {
    this.container.innerHTML = '';
    this.container.classList.add('copilot-panel');

    // Header
    const header = document.createElement('div');
    header.className = 'copilot-header';
    header.innerHTML = '<span style="font-size:10px;font-weight:600;color:var(--text-dim);letter-spacing:1px;text-transform:uppercase">AI Copilot</span>';
    this.container.appendChild(header);

    // Messages area
    this.messagesEl = document.createElement('div');
    this.messagesEl.className = 'copilot-messages';
    this.container.appendChild(this.messagesEl);

    // Input area
    const inputArea = document.createElement('div');
    inputArea.className = 'copilot-input-area';

    this.inputEl = document.createElement('textarea');
    this.inputEl.className = 'copilot-input';
    this.inputEl.placeholder = 'Describe what you want...';
    this.inputEl.rows = 2;

    const sendBtn = document.createElement('button');
    sendBtn.className = 'copilot-send';
    sendBtn.textContent = 'Send';
    sendBtn.addEventListener('click', () => this._sendMessage());

    this.inputEl.addEventListener('keydown', (e) => {
      if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        this._sendMessage();
      }
    });

    inputArea.appendChild(this.inputEl);
    inputArea.appendChild(sendBtn);
    this.container.appendChild(inputArea);
  }

  _sendMessage() {
    const text = this.inputEl.value.trim();
    if (!text) return;

    // Add user message
    this._addMessage('user', text);
    this.inputEl.value = '';

    // In the current architecture, the AI copilot communicates through
    // the MCP bridge. The actual AI processing happens on the Claude side
    // via MCP tools. This UI just shows the history and allows the user
    // to type natural language that gets sent as context.

    // For now, show a message about the MCP workflow
    this._addMessage('assistant', 'Use Claude Code with ShaderClaw MCP tools to modify shaders. This panel will show the conversation history.\n\nTry: "make the particles react to bass hits"');
  }

  _addMessage(role, content) {
    this.messages.push({ role, content, timestamp: Date.now() });

    const msgEl = document.createElement('div');
    msgEl.className = `copilot-message copilot-${role}`;

    const textEl = document.createElement('div');
    textEl.className = 'copilot-text';
    textEl.textContent = content;
    msgEl.appendChild(textEl);

    this.messagesEl.appendChild(msgEl);
    this.messagesEl.scrollTop = this.messagesEl.scrollHeight;
  }

  /**
   * Programmatically add a message (called by MCP bridge when AI makes changes)
   */
  addExternalMessage(role, content) {
    this._addMessage(role, content);
  }

  clearHistory() {
    this.messages = [];
    this.messagesEl.innerHTML = '';
  }
}

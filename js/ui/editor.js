// Editor UI — Code editor + tab switching
// Manages CodeMirror instance and Code/Nodes/AI tab navigation

import { state, on, emit } from '../state.js';

let cmEditor = null;
let compileTimer = null;

export function initEditor(editorAreaEl, onCompile) {
  // Tabs
  const tabs = document.createElement('div');
  tabs.className = 'editor-tabs';
  ['code', 'nodes', 'ai'].forEach(mode => {
    const tab = document.createElement('button');
    tab.className = 'editor-tab' + (mode === state.editorMode ? ' active' : '');
    tab.textContent = mode === 'ai' ? 'AI' : mode.charAt(0).toUpperCase() + mode.slice(1);
    tab.dataset.mode = mode;
    tab.addEventListener('click', () => {
      state.editorMode = mode;
      tabs.querySelectorAll('.editor-tab').forEach(t => t.classList.toggle('active', t.dataset.mode === mode));
      editorAreaEl.querySelectorAll('.editor-panel').forEach(p => p.classList.toggle('active', p.dataset.mode === mode));
      emit('editor:mode', { mode });
    });
    tabs.appendChild(tab);
  });
  editorAreaEl.appendChild(tabs);

  // Code panel
  const codePanel = document.createElement('div');
  codePanel.className = 'editor-panel active';
  codePanel.dataset.mode = 'code';
  const cmContainer = document.createElement('div');
  cmContainer.id = 'cm-container';
  cmContainer.style.height = '100%';
  codePanel.appendChild(cmContainer);
  editorAreaEl.appendChild(codePanel);

  // Nodes panel (placeholder)
  const nodesPanel = document.createElement('div');
  nodesPanel.className = 'editor-panel';
  nodesPanel.dataset.mode = 'nodes';
  nodesPanel.id = 'nodes-panel';
  editorAreaEl.appendChild(nodesPanel);

  // AI panel (placeholder)
  const aiPanel = document.createElement('div');
  aiPanel.className = 'editor-panel';
  aiPanel.dataset.mode = 'ai';
  aiPanel.id = 'ai-panel';
  editorAreaEl.appendChild(aiPanel);

  // Initialize CodeMirror
  if (window.CodeMirror) {
    cmEditor = CodeMirror(cmContainer, {
      value: '',
      mode: 'x-shader/x-fragment',
      theme: 'material-darker',
      lineNumbers: true,
      lineWrapping: false,
      tabSize: 2,
      indentWithTabs: false,
    });

    // Auto-compile with debounce
    cmEditor.on('change', () => {
      if (compileTimer) clearTimeout(compileTimer);
      compileTimer = setTimeout(() => {
        if (onCompile) onCompile(cmEditor.getValue());
      }, 600);
    });
  }

  return { codePanel, nodesPanel, aiPanel };
}

export function getEditor() { return cmEditor; }

export function setEditorValue(source) {
  if (cmEditor) {
    cmEditor.setValue(source);
  }
}

export function getEditorValue() {
  return cmEditor ? cmEditor.getValue() : '';
}

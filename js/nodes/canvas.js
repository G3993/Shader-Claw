// Node Graph Canvas UI
// SVG-based node editor with pan/zoom, drag/drop, and connection drawing

import { NODE_TYPES, NODE_CATEGORIES, getNodesByCategory } from './registry.js';
import { graphToISF, validateGraph } from './codegen.js';

export class NodeCanvas {
  constructor(containerEl, onGraphChange) {
    this.container = containerEl;
    this.onGraphChange = onGraphChange;

    // Graph data
    this.nodes = [];
    this.edges = [];
    this.nextNodeId = 1;

    // View state
    this.panX = 0;
    this.panY = 0;
    this.zoom = 1;

    // Interaction state
    this.selectedNodeId = null;
    this.draggingNode = null;
    this.dragOffset = { x: 0, y: 0 };
    this.drawingEdge = null; // { fromNodeId, fromOutput, startX, startY }
    this.isPanning = false;
    this.panStart = { x: 0, y: 0 };

    this._build();
    this._wireEvents();
  }

  _build() {
    this.container.innerHTML = '';
    this.container.className = 'node-canvas';

    // SVG for edges
    this.svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    this.svg.classList.add('edge-svg');
    this.container.appendChild(this.svg);

    // Inner container for nodes (transformed for pan/zoom)
    this.inner = document.createElement('div');
    this.inner.className = 'node-canvas-inner';
    this.container.appendChild(this.inner);

    // Temp edge being drawn
    this.tempEdge = document.createElementNS('http://www.w3.org/2000/svg', 'path');
    this.tempEdge.classList.add('edge', 'active');
    this.tempEdge.style.display = 'none';
    this.svg.appendChild(this.tempEdge);

    // Right-click context menu / add node palette
    this._buildPalette();

    this._updateTransform();
  }

  _buildPalette() {
    this.palette = document.createElement('div');
    this.palette.className = 'node-palette';
    this.palette.style.display = 'none';

    const categories = getNodesByCategory();
    for (const [catId, nodes] of Object.entries(categories)) {
      const catDef = NODE_CATEGORIES[catId] || { label: catId, color: '#888' };
      const catHeader = document.createElement('div');
      catHeader.style.cssText = `font-size:9px;font-weight:600;color:${catDef.color};padding:4px 8px;text-transform:uppercase;letter-spacing:1px`;
      catHeader.textContent = catDef.label;
      this.palette.appendChild(catHeader);

      nodes.forEach(n => {
        const item = document.createElement('div');
        item.style.cssText = 'padding:3px 8px 3px 16px;font-size:10px;cursor:pointer;color:var(--text)';
        item.textContent = n.label;
        item.addEventListener('mouseenter', () => item.style.background = 'var(--surface-alt)');
        item.addEventListener('mouseleave', () => item.style.background = '');
        item.addEventListener('click', () => {
          this.addNode(n.type, this._palettePos.x, this._palettePos.y);
          this.palette.style.display = 'none';
        });
        this.palette.appendChild(item);
      });
    }

    this.container.appendChild(this.palette);
    this._palettePos = { x: 200, y: 200 };
  }

  _wireEvents() {
    // Pan (middle mouse or alt+left)
    this.container.addEventListener('pointerdown', (e) => {
      if (e.button === 1 || (e.button === 0 && e.altKey)) {
        this.isPanning = true;
        this.panStart = { x: e.clientX - this.panX, y: e.clientY - this.panY };
        e.preventDefault();
      }
    });

    window.addEventListener('pointermove', (e) => {
      if (this.isPanning) {
        this.panX = e.clientX - this.panStart.x;
        this.panY = e.clientY - this.panStart.y;
        this._updateTransform();
      }

      if (this.draggingNode) {
        const node = this.nodes.find(n => n.id === this.draggingNode);
        if (node) {
          node.position[0] = (e.clientX - this.container.getBoundingClientRect().left - this.panX) / this.zoom - this.dragOffset.x;
          node.position[1] = (e.clientY - this.container.getBoundingClientRect().top - this.panY) / this.zoom - this.dragOffset.y;
          this._updateNodePosition(node);
          this._renderEdges();
        }
      }

      if (this.drawingEdge) {
        const rect = this.container.getBoundingClientRect();
        const mx = (e.clientX - rect.left - this.panX) / this.zoom;
        const my = (e.clientY - rect.top - this.panY) / this.zoom;
        this._updateTempEdge(this.drawingEdge.startX, this.drawingEdge.startY, mx, my);
      }
    });

    window.addEventListener('pointerup', (e) => {
      this.isPanning = false;

      if (this.draggingNode) {
        this.draggingNode = null;
        this._emitChange();
      }

      if (this.drawingEdge) {
        this.tempEdge.style.display = 'none';
        this.drawingEdge = null;
      }
    });

    // Zoom
    this.container.addEventListener('wheel', (e) => {
      e.preventDefault();
      const factor = e.deltaY > 0 ? 0.9 : 1.1;
      const rect = this.container.getBoundingClientRect();
      const mx = e.clientX - rect.left;
      const my = e.clientY - rect.top;

      this.panX = mx - (mx - this.panX) * factor;
      this.panY = my - (my - this.panY) * factor;
      this.zoom *= factor;
      this.zoom = Math.max(0.2, Math.min(3, this.zoom));
      this._updateTransform();
      this._renderEdges();
    });

    // Right-click: add node palette
    this.container.addEventListener('contextmenu', (e) => {
      e.preventDefault();
      const rect = this.container.getBoundingClientRect();
      this._palettePos = {
        x: (e.clientX - rect.left - this.panX) / this.zoom,
        y: (e.clientY - rect.top - this.panY) / this.zoom,
      };
      this.palette.style.left = e.clientX - rect.left + 'px';
      this.palette.style.top = e.clientY - rect.top + 'px';
      this.palette.style.display = 'block';
    });

    // Click away to close palette
    this.container.addEventListener('click', () => {
      this.palette.style.display = 'none';
    });

    // Delete selected node
    document.addEventListener('keydown', (e) => {
      if (e.key === 'Delete' && this.selectedNodeId) {
        this.removeNode(this.selectedNodeId);
      }
    });
  }

  _updateTransform() {
    this.inner.style.transform = `translate(${this.panX}px, ${this.panY}px) scale(${this.zoom})`;
    this.svg.style.transform = `translate(${this.panX}px, ${this.panY}px) scale(${this.zoom})`;
  }

  // === Node CRUD ===

  addNode(type, x = 200, y = 200) {
    const typeDef = NODE_TYPES[type];
    if (!typeDef) return null;

    const id = 'n' + (this.nextNodeId++);
    const node = {
      id,
      type,
      position: [x, y],
      params: { ...typeDef.params },
    };
    this.nodes.push(node);
    this._renderNode(node);
    this._emitChange();
    return node;
  }

  removeNode(nodeId) {
    this.nodes = this.nodes.filter(n => n.id !== nodeId);
    this.edges = this.edges.filter(e => e.from !== nodeId && e.to !== nodeId);
    const el = this.inner.querySelector(`[data-node-id="${nodeId}"]`);
    if (el) el.remove();
    if (this.selectedNodeId === nodeId) this.selectedNodeId = null;
    this._renderEdges();
    this._emitChange();
  }

  connectNodes(fromId, fromOutput, toId, toInput) {
    // Remove existing connection to this input
    this.edges = this.edges.filter(e => !(e.to === toId && e.input === toInput));
    this.edges.push({ from: fromId, output: fromOutput, to: toId, input: toInput });
    this._renderEdges();
    this._emitChange();
  }

  // === Rendering ===

  _renderNode(node) {
    const typeDef = NODE_TYPES[node.type];
    if (!typeDef) return;

    const catDef = NODE_CATEGORIES[typeDef.category] || {};
    const el = document.createElement('div');
    el.className = 'node' + (node.id === this.selectedNodeId ? ' selected' : '');
    el.dataset.nodeId = node.id;
    el.style.left = node.position[0] + 'px';
    el.style.top = node.position[1] + 'px';

    // Header
    const header = document.createElement('div');
    header.className = 'node-header';
    header.style.borderTopColor = catDef.color || '#888';
    header.textContent = typeDef.label;
    el.appendChild(header);

    // Body (ports)
    const body = document.createElement('div');
    body.className = 'node-body';

    // Input ports
    (typeDef.inputs || []).forEach(inp => {
      const row = document.createElement('div');
      row.className = 'node-row input';
      const port = document.createElement('div');
      port.className = `port port-${inp.type}`;
      port.dataset.nodeId = node.id;
      port.dataset.portName = inp.name;
      port.dataset.portDir = 'input';
      const label = document.createElement('span');
      label.className = 'port-label';
      label.textContent = inp.name;
      row.appendChild(port);
      row.appendChild(label);
      body.appendChild(row);

      // Drop target for edges
      port.addEventListener('pointerup', () => {
        if (this.drawingEdge) {
          this.connectNodes(this.drawingEdge.fromNodeId, this.drawingEdge.fromOutput, node.id, inp.name);
          this.drawingEdge = null;
          this.tempEdge.style.display = 'none';
        }
      });
    });

    // Output ports
    (typeDef.outputs || []).forEach(out => {
      const row = document.createElement('div');
      row.className = 'node-row output';
      const label = document.createElement('span');
      label.className = 'port-label';
      label.textContent = out.name;
      const port = document.createElement('div');
      port.className = `port port-${out.type}`;
      port.dataset.nodeId = node.id;
      port.dataset.portName = out.name;
      port.dataset.portDir = 'output';
      row.appendChild(label);
      row.appendChild(port);
      body.appendChild(row);

      // Start drawing edge from output port
      port.addEventListener('pointerdown', (e) => {
        e.stopPropagation();
        const portRect = port.getBoundingClientRect();
        const containerRect = this.container.getBoundingClientRect();
        const sx = (portRect.left + portRect.width / 2 - containerRect.left - this.panX) / this.zoom;
        const sy = (portRect.top + portRect.height / 2 - containerRect.top - this.panY) / this.zoom;
        this.drawingEdge = { fromNodeId: node.id, fromOutput: out.name, startX: sx, startY: sy };
        this.tempEdge.style.display = '';
      });
    });

    el.appendChild(body);

    // Node dragging
    header.addEventListener('pointerdown', (e) => {
      e.stopPropagation();
      this.selectedNodeId = node.id;
      this.inner.querySelectorAll('.node').forEach(n => n.classList.remove('selected'));
      el.classList.add('selected');

      const rect = this.container.getBoundingClientRect();
      this.dragOffset = {
        x: (e.clientX - rect.left - this.panX) / this.zoom - node.position[0],
        y: (e.clientY - rect.top - this.panY) / this.zoom - node.position[1],
      };
      this.draggingNode = node.id;
    });

    // Click to select
    el.addEventListener('click', (e) => {
      e.stopPropagation();
      this.selectedNodeId = node.id;
      this.inner.querySelectorAll('.node').forEach(n => n.classList.remove('selected'));
      el.classList.add('selected');
    });

    this.inner.appendChild(el);
  }

  _updateNodePosition(node) {
    const el = this.inner.querySelector(`[data-node-id="${node.id}"]`);
    if (el) {
      el.style.left = node.position[0] + 'px';
      el.style.top = node.position[1] + 'px';
    }
  }

  _renderEdges() {
    // Clear old edges (keep tempEdge)
    this.svg.querySelectorAll('path:not(.active)').forEach(p => p.remove());

    this.edges.forEach(edge => {
      const fromPort = this.inner.querySelector(`[data-node-id="${edge.from}"][data-port-name="${edge.output}"][data-port-dir="output"]`);
      const toPort = this.inner.querySelector(`[data-node-id="${edge.to}"][data-port-name="${edge.input}"][data-port-dir="input"]`);
      if (!fromPort || !toPort) return;

      const containerRect = this.container.getBoundingClientRect();
      const fromRect = fromPort.getBoundingClientRect();
      const toRect = toPort.getBoundingClientRect();

      const x1 = (fromRect.left + fromRect.width / 2 - containerRect.left - this.panX) / this.zoom;
      const y1 = (fromRect.top + fromRect.height / 2 - containerRect.top - this.panY) / this.zoom;
      const x2 = (toRect.left + toRect.width / 2 - containerRect.left - this.panX) / this.zoom;
      const y2 = (toRect.top + toRect.height / 2 - containerRect.top - this.panY) / this.zoom;

      const path = document.createElementNS('http://www.w3.org/2000/svg', 'path');
      path.classList.add('edge');
      const dx = Math.abs(x2 - x1) * 0.5;
      path.setAttribute('d', `M ${x1} ${y1} C ${x1 + dx} ${y1}, ${x2 - dx} ${y2}, ${x2} ${y2}`);
      this.svg.insertBefore(path, this.tempEdge);
    });
  }

  _updateTempEdge(x1, y1, x2, y2) {
    const dx = Math.abs(x2 - x1) * 0.5;
    this.tempEdge.setAttribute('d', `M ${x1} ${y1} C ${x1 + dx} ${y1}, ${x2 - dx} ${y2}, ${x2} ${y2}`);
  }

  _emitChange() {
    if (this.onGraphChange) {
      this.onGraphChange(this.getGraph());
    }
  }

  // === Graph Data ===

  getGraph() {
    return {
      nodes: this.nodes.map(n => ({ id: n.id, type: n.type, position: [...n.position], params: { ...n.params } })),
      edges: this.edges.map(e => ({ ...e })),
    };
  }

  loadGraph(graph) {
    this.inner.querySelectorAll('.node').forEach(n => n.remove());
    this.nodes = (graph.nodes || []).map(n => ({ ...n, position: [...n.position], params: { ...n.params } }));
    this.edges = (graph.edges || []).map(e => ({ ...e }));
    let maxId = 0;
    this.nodes.forEach(n => {
      const num = parseInt(n.id.replace('n', ''));
      if (num > maxId) maxId = num;
      this._renderNode(n);
    });
    this.nextNodeId = maxId + 1;
    this._renderEdges();
  }

  clear() {
    this.nodes = [];
    this.edges = [];
    this.nextNodeId = 1;
    this.selectedNodeId = null;
    this.inner.querySelectorAll('.node').forEach(n => n.remove());
    this._renderEdges();
    this._emitChange();
  }

  /**
   * Generate ISF shader from current graph
   */
  generateShader() {
    const graph = this.getGraph();
    const validation = validateGraph(graph);
    if (!validation.valid) {
      return { ok: false, errors: validation.errors.join('; '), source: '' };
    }
    const source = graphToISF(graph);
    return { ok: true, errors: null, source };
  }

  /**
   * Create a default graph with UV → Output
   */
  createDefaultGraph() {
    this.clear();
    this.addNode('uv', 100, 200);
    const colorNode = this.addNode('color_ramp', 350, 200);
    const outputNode = this.addNode('output', 600, 200);
    // Connect UV.x → ColorRamp.factor → Output.color
    this.connectNodes('n1', 'x', 'n2', 'factor');
    this.connectNodes('n2', 'color', 'n3', 'color');
  }
}

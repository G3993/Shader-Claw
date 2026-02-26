// Graph Data Model — DAG for node graph shaders
// Wraps the raw { nodes, edges } structure with convenience methods

export class Graph {
  constructor() {
    this.nodes = [];
    this.edges = [];
  }

  addNode(type, position = [0, 0], params = {}) {
    const id = 'n' + (this.nodes.length + 1) + '_' + Date.now().toString(36);
    const node = { id, type, position: [...position], params: { ...params } };
    this.nodes.push(node);
    return node;
  }

  removeNode(nodeId) {
    this.nodes = this.nodes.filter(n => n.id !== nodeId);
    this.edges = this.edges.filter(e => e.from !== nodeId && e.to !== nodeId);
  }

  getNode(nodeId) {
    return this.nodes.find(n => n.id === nodeId) || null;
  }

  connect(fromId, output, toId, input) {
    // Remove existing connection to this input
    this.edges = this.edges.filter(e => !(e.to === toId && e.input === input));
    this.edges.push({ from: fromId, output, to: toId, input });
  }

  disconnect(fromId, output, toId, input) {
    this.edges = this.edges.filter(e =>
      !(e.from === fromId && e.output === output && e.to === toId && e.input === input)
    );
  }

  getInputEdges(nodeId) {
    return this.edges.filter(e => e.to === nodeId);
  }

  getOutputEdges(nodeId) {
    return this.edges.filter(e => e.from === nodeId);
  }

  hasOutput() {
    return this.nodes.some(n => n.type === 'output');
  }

  toJSON() {
    return { nodes: this.nodes, edges: this.edges };
  }

  static fromJSON(json) {
    const g = new Graph();
    g.nodes = (json.nodes || []).map(n => ({ ...n, position: [...(n.position || [0, 0])], params: { ...(n.params || {}) } }));
    g.edges = (json.edges || []).map(e => ({ ...e }));
    return g;
  }

  clone() {
    return Graph.fromJSON(this.toJSON());
  }
}

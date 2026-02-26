// Node Graph -> GLSL Code Generator
// Topological sort the DAG, generate GLSL for each node, wrap in ISF

import { NODE_TYPES, getNodeType } from './registry.js';

/**
 * Topological sort of node graph
 * @param {Array} nodes - [{ id, type, params }]
 * @param {Array} edges - [{ from, output, to, input }]
 * @returns {Array} sorted node IDs
 */
export function topologicalSort(nodes, edges) {
  const inDegree = {};
  const adjList = {};
  const nodeMap = {};

  nodes.forEach(n => {
    inDegree[n.id] = 0;
    adjList[n.id] = [];
    nodeMap[n.id] = n;
  });

  edges.forEach(e => {
    if (adjList[e.from]) {
      adjList[e.from].push(e.to);
    }
    if (inDegree[e.to] !== undefined) {
      inDegree[e.to]++;
    }
  });

  const queue = [];
  for (const id in inDegree) {
    if (inDegree[id] === 0) queue.push(id);
  }

  const sorted = [];
  while (queue.length > 0) {
    const id = queue.shift();
    sorted.push(id);
    (adjList[id] || []).forEach(neighbor => {
      inDegree[neighbor]--;
      if (inDegree[neighbor] === 0) queue.push(neighbor);
    });
  }

  return sorted;
}

/**
 * Generate GLSL from node graph
 * @param {{ nodes: Array, edges: Array }} graph
 * @returns {{ glsl: string, isfInputs: Array }}
 */
export function generateGLSL(graph) {
  const { nodes, edges } = graph;
  const sorted = topologicalSort(nodes, edges);

  // Build edge lookup: for each node input, which output feeds it
  const inputMap = {}; // `${toId}.${inputName}` -> `${fromId}_${outputName}`
  edges.forEach(e => {
    inputMap[`${e.to}.${e.input}`] = `${e.from}_${e.output}`;
  });

  // Collect ISF inputs from custom_uniform nodes
  const isfInputs = [];

  let glslBody = '';

  sorted.forEach(nodeId => {
    const node = nodes.find(n => n.id === nodeId);
    if (!node) return;

    const typeDef = getNodeType(node.type);
    if (!typeDef) return;

    // Resolve input references
    const resolvedInputs = {};
    (typeDef.inputs || []).forEach(inp => {
      const key = `${nodeId}.${inp.name}`;
      if (inputMap[key]) {
        resolvedInputs[inp.name] = inputMap[key];
      }
      // If no connection, resolvedInputs[inp.name] stays undefined
      // and the GLSL function uses its default value
    });

    // Merge node params with type defaults
    const params = { ...typeDef.params, ...(node.params || {}) };

    // Generate GLSL
    const code = typeDef.glsl(nodeId, resolvedInputs, params);
    if (code) {
      glslBody += '\n  ' + code.split('\n').join('\n  ');
    }

    // Collect ISF inputs
    if (typeDef.isfInput) {
      isfInputs.push(typeDef.isfInput(params));
    }
  });

  return { glsl: glslBody, isfInputs };
}

/**
 * Generate a complete ISF shader from a node graph
 * @param {{ nodes: Array, edges: Array }} graph
 * @returns {string} Complete ISF shader source
 */
export function graphToISF(graph) {
  const { glsl, isfInputs } = generateGLSL(graph);

  // Build ISF metadata block
  const meta = {
    DESCRIPTION: 'Generated from node graph',
    CATEGORIES: ['Node Graph'],
    INPUTS: isfInputs,
  };

  const metaStr = JSON.stringify(meta, null, 2);

  return `/*\n${metaStr}\n*/\n\nvoid main() {\n${glsl}\n}\n`;
}

/**
 * Validate graph -- check for cycles, missing connections to output, etc.
 */
export function validateGraph(graph) {
  const errors = [];
  const { nodes, edges } = graph;

  // Check for output node
  const outputNode = nodes.find(n => n.type === 'output');
  if (!outputNode) {
    errors.push('Graph must have an Output node');
  }

  // Check for cycles (topological sort should return all nodes)
  const sorted = topologicalSort(nodes, edges);
  if (sorted.length !== nodes.length) {
    errors.push('Graph contains a cycle');
  }

  return { valid: errors.length === 0, errors };
}

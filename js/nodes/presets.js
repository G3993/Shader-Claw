// Node Graph Presets — saved node graph configurations

const GRAPHS_DIR = 'graphs';

/**
 * Save a node graph to the presets directory
 * @param {string} name - Preset name
 * @param {object} graph - { nodes, edges }
 */
export async function saveGraphPreset(name, graph) {
  const filename = name.replace(/[^a-zA-Z0-9_-]/g, '_') + '.json';
  const preset = {
    name,
    timestamp: new Date().toISOString(),
    graph,
  };
  const blob = new Blob([JSON.stringify(preset, null, 2)], { type: 'application/json' });
  const a = document.createElement('a');
  a.href = URL.createObjectURL(blob);
  a.download = filename;
  a.click();
  URL.revokeObjectURL(a.href);
}

/**
 * Load a node graph preset from a file
 * @returns {Promise<object>} - { name, graph }
 */
export function loadGraphPreset() {
  return new Promise((resolve, reject) => {
    const input = document.createElement('input');
    input.type = 'file';
    input.accept = '.json';
    input.onchange = async (e) => {
      const file = e.target.files[0];
      if (!file) return reject(new Error('No file selected'));
      try {
        const text = await file.text();
        const preset = JSON.parse(text);
        resolve({ name: preset.name || file.name, graph: preset.graph });
      } catch (err) {
        reject(new Error('Invalid graph preset file'));
      }
    };
    input.click();
  });
}

/**
 * Built-in graph presets
 */
export const BUILTIN_PRESETS = {
  'UV Gradient': {
    nodes: [
      { id: 'n1', type: 'uv', position: [50, 200] },
      { id: 'n2', type: 'output', position: [300, 200] },
    ],
    edges: [
      { from: 'n1', output: 'uv', to: 'n2', input: 'fragColor' },
    ],
  },

  'Noise Ramp': {
    nodes: [
      { id: 'n1', type: 'uv', position: [50, 200] },
      { id: 'n2', type: 'time', position: [50, 350] },
      { id: 'n3', type: 'simplex_noise', position: [250, 250], params: { scale: 3.0, octaves: 4 } },
      { id: 'n4', type: 'color_ramp', position: [450, 250], params: { colorA: [0.0, 0.0, 0.2, 1.0], colorB: [1.0, 0.4, 0.0, 1.0] } },
      { id: 'n5', type: 'output', position: [650, 250] },
    ],
    edges: [
      { from: 'n1', output: 'uv', to: 'n3', input: 'uv' },
      { from: 'n2', output: 'time', to: 'n3', input: 'offset' },
      { from: 'n3', output: 'value', to: 'n4', input: 'factor' },
      { from: 'n4', output: 'color', to: 'n5', input: 'fragColor' },
    ],
  },

  'Circle SDF': {
    nodes: [
      { id: 'n1', type: 'uv', position: [50, 200] },
      { id: 'n2', type: 'circle_sdf', position: [250, 200], params: { radius: 0.3 } },
      { id: 'n3', type: 'output', position: [450, 200] },
    ],
    edges: [
      { from: 'n1', output: 'uv', to: 'n2', input: 'uv' },
      { from: 'n2', output: 'value', to: 'n3', input: 'fragColor' },
    ],
  },

  'Audio Reactive': {
    nodes: [
      { id: 'n1', type: 'uv', position: [50, 200] },
      { id: 'n2', type: 'audio_level', position: [50, 350] },
      { id: 'n3', type: 'simplex_noise', position: [250, 250], params: { scale: 5.0 } },
      { id: 'n4', type: 'multiply', position: [450, 250] },
      { id: 'n5', type: 'output', position: [650, 250] },
    ],
    edges: [
      { from: 'n1', output: 'uv', to: 'n3', input: 'uv' },
      { from: 'n3', output: 'value', to: 'n4', input: 'a' },
      { from: 'n2', output: 'level', to: 'n4', input: 'b' },
      { from: 'n4', output: 'value', to: 'n5', input: 'fragColor' },
    ],
  },
};

export function getPresetNames() {
  return Object.keys(BUILTIN_PRESETS);
}

export function getPreset(name) {
  return BUILTIN_PRESETS[name] || null;
}

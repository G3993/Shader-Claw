// Node Type Registry
// Each node type defines inputs, outputs, default params, and GLSL code generation

// Port types: 'float', 'vec2', 'vec3', 'vec4', 'sampler2D'

export const NODE_CATEGORIES = {
  input: { label: 'Input', color: '#ff6666' },
  generate: { label: 'Generate', color: '#66cc66' },
  math: { label: 'Math', color: '#6688cc' },
  trig: { label: 'Trig', color: '#88aadd' },
  vector: { label: 'Vector', color: '#cc88ff' },
  color: { label: 'Color', color: '#ffaa44' },
  texture: { label: 'Texture', color: '#ff88aa' },
  shape: { label: 'Shape', color: '#88ddaa' },
  transform: { label: 'Transform', color: '#ddaa66' },
  time: { label: 'Time', color: '#ff8888' },
  logic: { label: 'Logic', color: '#aaaaaa' },
  output: { label: 'Output', color: '#ff2d2d' },
};

// Registry of all node types
// Each has: category, label, inputs[], outputs[], params{}, glsl(id, inputs, params)
// glsl function returns a string of GLSL statements, assigning to out_<outputName>
export const NODE_TYPES = {
  // === INPUT ===
  time: {
    category: 'input', label: 'TIME',
    inputs: [],
    outputs: [{ name: 'value', type: 'float' }],
    params: {},
    glsl: (id) => `float ${id}_value = TIME;`,
  },
  uv: {
    category: 'input', label: 'UV',
    inputs: [],
    outputs: [{ name: 'uv', type: 'vec2' }, { name: 'x', type: 'float' }, { name: 'y', type: 'float' }],
    params: {},
    glsl: (id) => `vec2 ${id}_uv = isf_FragNormCoord;\nfloat ${id}_x = isf_FragNormCoord.x;\nfloat ${id}_y = isf_FragNormCoord.y;`,
  },
  mouse: {
    category: 'input', label: 'Mouse',
    inputs: [],
    outputs: [{ name: 'pos', type: 'vec2' }, { name: 'delta', type: 'vec2' }],
    params: {},
    glsl: (id) => `vec2 ${id}_pos = mousePos;\nvec2 ${id}_delta = mouseDelta;`,
  },
  resolution: {
    category: 'input', label: 'Resolution',
    inputs: [],
    outputs: [{ name: 'size', type: 'vec2' }, { name: 'aspect', type: 'float' }],
    params: {},
    glsl: (id) => `vec2 ${id}_size = RENDERSIZE;\nfloat ${id}_aspect = RENDERSIZE.x / RENDERSIZE.y;`,
  },
  audio_level: {
    category: 'input', label: 'Audio Level',
    inputs: [],
    outputs: [{ name: 'level', type: 'float' }, { name: 'bass', type: 'float' }, { name: 'mid', type: 'float' }, { name: 'high', type: 'float' }],
    params: {},
    glsl: (id) => `float ${id}_level = audioLevel;\nfloat ${id}_bass = audioBass;\nfloat ${id}_mid = audioMid;\nfloat ${id}_high = audioHigh;`,
  },
  audio_fft: {
    category: 'input', label: 'Audio FFT',
    inputs: [],
    outputs: [{ name: 'texture', type: 'sampler2D' }],
    params: {},
    glsl: (id) => `// ${id}: audioFFT is available as sampler2D`,
  },
  custom_uniform: {
    category: 'input', label: 'Custom Float',
    inputs: [],
    outputs: [{ name: 'value', type: 'float' }],
    params: { name: 'myParam', default: 0.5, min: 0, max: 1 },
    glsl: (id, inputs, params) => `float ${id}_value = ${params.name};`,
    // Note: this generates an ISF INPUT in the wrapper
    isfInput: (params) => ({ NAME: params.name, TYPE: 'float', DEFAULT: params.default, MIN: params.min, MAX: params.max }),
  },

  // === GENERATE ===
  simplex_noise: {
    category: 'generate', label: 'Simplex Noise',
    inputs: [{ name: 'uv', type: 'vec2' }],
    outputs: [{ name: 'value', type: 'float' }],
    params: { scale: 4.0, speed: 0.5, octaves: 1 },
    glsl: (id, inputs, params) => `
// Simplex 2D noise
vec3 ${id}_mod289(vec3 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
vec2 ${id}_mod289v2(vec2 x) { return x - floor(x * (1.0 / 289.0)) * 289.0; }
vec3 ${id}_permute(vec3 x) { return ${id}_mod289(((x*34.0)+1.0)*x); }
float ${id}_snoise(vec2 v) {
  const vec4 C = vec4(0.211324865405187, 0.366025403784439, -0.577350269189626, 0.024390243902439);
  vec2 i = floor(v + dot(v, C.yy));
  vec2 x0 = v - i + dot(i, C.xx);
  vec2 i1 = (x0.x > x0.y) ? vec2(1.0, 0.0) : vec2(0.0, 1.0);
  vec4 x12 = x0.xyxy + C.xxzz;
  x12.xy -= i1;
  i = ${id}_mod289v2(i);
  vec3 p = ${id}_permute(${id}_permute(i.y + vec3(0.0, i1.y, 1.0)) + i.x + vec3(0.0, i1.x, 1.0));
  vec3 m = max(0.5 - vec3(dot(x0,x0), dot(x12.xy,x12.xy), dot(x12.zw,x12.zw)), 0.0);
  m = m*m; m = m*m;
  vec3 x = 2.0 * fract(p * C.www) - 1.0;
  vec3 h = abs(x) - 0.5;
  vec3 ox = floor(x + 0.5);
  vec3 a0 = x - ox;
  m *= 1.79284291400159 - 0.85373472095314 * (a0*a0 + h*h);
  vec3 g;
  g.x = a0.x * x0.x + h.x * x0.y;
  g.yz = a0.yz * x12.xz + h.yz * x12.yw;
  return 130.0 * dot(m, g);
}
float ${id}_value = ${id}_snoise(${inputs.uv || 'isf_FragNormCoord'} * ${params.scale.toFixed(1)} + TIME * ${params.speed.toFixed(2)}) * 0.5 + 0.5;`,
  },

  gradient_linear: {
    category: 'generate', label: 'Linear Gradient',
    inputs: [{ name: 'uv', type: 'vec2' }],
    outputs: [{ name: 'value', type: 'float' }],
    params: { angle: 0.0 },
    glsl: (id, inputs, params) => {
      const rad = (params.angle * Math.PI / 180).toFixed(4);
      return `float ${id}_value = dot(${inputs.uv || 'isf_FragNormCoord'} - 0.5, vec2(cos(${rad}), sin(${rad}))) + 0.5;`;
    },
  },

  checkerboard: {
    category: 'generate', label: 'Checkerboard',
    inputs: [{ name: 'uv', type: 'vec2' }],
    outputs: [{ name: 'value', type: 'float' }],
    params: { scale: 8.0 },
    glsl: (id, inputs, params) => `float ${id}_value = mod(floor(${inputs.uv || 'isf_FragNormCoord'}.x * ${params.scale.toFixed(1)}) + floor(${inputs.uv || 'isf_FragNormCoord'}.y * ${params.scale.toFixed(1)}), 2.0);`,
  },

  white_noise: {
    category: 'generate', label: 'White Noise',
    inputs: [{ name: 'uv', type: 'vec2' }],
    outputs: [{ name: 'value', type: 'float' }],
    params: {},
    glsl: (id, inputs) => `float ${id}_value = fract(sin(dot(${inputs.uv || 'isf_FragNormCoord'} + TIME * 0.001, vec2(12.9898, 78.233))) * 43758.5453);`,
  },

  // === MATH ===
  add: {
    category: 'math', label: 'Add',
    inputs: [{ name: 'a', type: 'float' }, { name: 'b', type: 'float' }],
    outputs: [{ name: 'result', type: 'float' }],
    params: {},
    glsl: (id, inputs) => `float ${id}_result = ${inputs.a || '0.0'} + ${inputs.b || '0.0'};`,
  },
  subtract: {
    category: 'math', label: 'Subtract',
    inputs: [{ name: 'a', type: 'float' }, { name: 'b', type: 'float' }],
    outputs: [{ name: 'result', type: 'float' }],
    params: {},
    glsl: (id, inputs) => `float ${id}_result = ${inputs.a || '0.0'} - ${inputs.b || '0.0'};`,
  },
  multiply: {
    category: 'math', label: 'Multiply',
    inputs: [{ name: 'a', type: 'float' }, { name: 'b', type: 'float' }],
    outputs: [{ name: 'result', type: 'float' }],
    params: {},
    glsl: (id, inputs) => `float ${id}_result = ${inputs.a || '1.0'} * ${inputs.b || '1.0'};`,
  },
  divide: {
    category: 'math', label: 'Divide',
    inputs: [{ name: 'a', type: 'float' }, { name: 'b', type: 'float' }],
    outputs: [{ name: 'result', type: 'float' }],
    params: {},
    glsl: (id, inputs) => `float ${id}_result = ${inputs.a || '0.0'} / max(${inputs.b || '1.0'}, 0.0001);`,
  },
  mix_lerp: {
    category: 'math', label: 'Mix/Lerp',
    inputs: [{ name: 'a', type: 'vec4' }, { name: 'b', type: 'vec4' }, { name: 'factor', type: 'float' }],
    outputs: [{ name: 'result', type: 'vec4' }],
    params: {},
    glsl: (id, inputs) => `vec4 ${id}_result = mix(${inputs.a || 'vec4(0.0)'}, ${inputs.b || 'vec4(1.0)'}, ${inputs.factor || '0.5'});`,
  },
  clamp_node: {
    category: 'math', label: 'Clamp',
    inputs: [{ name: 'value', type: 'float' }],
    outputs: [{ name: 'result', type: 'float' }],
    params: { min: 0.0, max: 1.0 },
    glsl: (id, inputs, params) => `float ${id}_result = clamp(${inputs.value || '0.0'}, ${params.min.toFixed(2)}, ${params.max.toFixed(2)});`,
  },
  smoothstep_node: {
    category: 'math', label: 'Smoothstep',
    inputs: [{ name: 'value', type: 'float' }],
    outputs: [{ name: 'result', type: 'float' }],
    params: { edge0: 0.0, edge1: 1.0 },
    glsl: (id, inputs, params) => `float ${id}_result = smoothstep(${params.edge0.toFixed(2)}, ${params.edge1.toFixed(2)}, ${inputs.value || '0.0'});`,
  },
  fract_node: {
    category: 'math', label: 'Fract',
    inputs: [{ name: 'value', type: 'float' }],
    outputs: [{ name: 'result', type: 'float' }],
    params: {},
    glsl: (id, inputs) => `float ${id}_result = fract(${inputs.value || '0.0'});`,
  },
  abs_node: {
    category: 'math', label: 'Abs',
    inputs: [{ name: 'value', type: 'float' }],
    outputs: [{ name: 'result', type: 'float' }],
    params: {},
    glsl: (id, inputs) => `float ${id}_result = abs(${inputs.value || '0.0'});`,
  },
  remap: {
    category: 'math', label: 'Remap',
    inputs: [{ name: 'value', type: 'float' }],
    outputs: [{ name: 'result', type: 'float' }],
    params: { inMin: 0, inMax: 1, outMin: 0, outMax: 1 },
    glsl: (id, inputs, params) => `float ${id}_result = ${params.outMin.toFixed(2)} + (${inputs.value || '0.0'} - ${params.inMin.toFixed(2)}) / (${params.inMax.toFixed(2)} - ${params.inMin.toFixed(2)}) * (${params.outMax.toFixed(2)} - ${params.outMin.toFixed(2)});`,
  },

  // === TRIG ===
  sin_node: {
    category: 'trig', label: 'Sin',
    inputs: [{ name: 'value', type: 'float' }],
    outputs: [{ name: 'result', type: 'float' }],
    params: {},
    glsl: (id, inputs) => `float ${id}_result = sin(${inputs.value || '0.0'});`,
  },
  cos_node: {
    category: 'trig', label: 'Cos',
    inputs: [{ name: 'value', type: 'float' }],
    outputs: [{ name: 'result', type: 'float' }],
    params: {},
    glsl: (id, inputs) => `float ${id}_result = cos(${inputs.value || '0.0'});`,
  },
  atan2_node: {
    category: 'trig', label: 'Atan2',
    inputs: [{ name: 'y', type: 'float' }, { name: 'x', type: 'float' }],
    outputs: [{ name: 'result', type: 'float' }],
    params: {},
    glsl: (id, inputs) => `float ${id}_result = atan(${inputs.y || '0.0'}, ${inputs.x || '1.0'});`,
  },
  polar_to_cart: {
    category: 'trig', label: 'Polar to Cart',
    inputs: [{ name: 'angle', type: 'float' }, { name: 'radius', type: 'float' }],
    outputs: [{ name: 'xy', type: 'vec2' }],
    params: {},
    glsl: (id, inputs) => `vec2 ${id}_xy = vec2(cos(${inputs.angle || '0.0'}), sin(${inputs.angle || '0.0'})) * ${inputs.radius || '1.0'};`,
  },

  // === VECTOR ===
  combine_xy: {
    category: 'vector', label: 'Combine XY',
    inputs: [{ name: 'x', type: 'float' }, { name: 'y', type: 'float' }],
    outputs: [{ name: 'vec', type: 'vec2' }],
    params: {},
    glsl: (id, inputs) => `vec2 ${id}_vec = vec2(${inputs.x || '0.0'}, ${inputs.y || '0.0'});`,
  },
  combine_xyz: {
    category: 'vector', label: 'Combine XYZ',
    inputs: [{ name: 'x', type: 'float' }, { name: 'y', type: 'float' }, { name: 'z', type: 'float' }],
    outputs: [{ name: 'vec', type: 'vec3' }],
    params: {},
    glsl: (id, inputs) => `vec3 ${id}_vec = vec3(${inputs.x || '0.0'}, ${inputs.y || '0.0'}, ${inputs.z || '0.0'});`,
  },
  combine_rgba: {
    category: 'vector', label: 'Combine RGBA',
    inputs: [{ name: 'r', type: 'float' }, { name: 'g', type: 'float' }, { name: 'b', type: 'float' }, { name: 'a', type: 'float' }],
    outputs: [{ name: 'color', type: 'vec4' }],
    params: {},
    glsl: (id, inputs) => `vec4 ${id}_color = vec4(${inputs.r || '0.0'}, ${inputs.g || '0.0'}, ${inputs.b || '0.0'}, ${inputs.a || '1.0'});`,
  },
  split_vec: {
    category: 'vector', label: 'Split',
    inputs: [{ name: 'vec', type: 'vec4' }],
    outputs: [{ name: 'r', type: 'float' }, { name: 'g', type: 'float' }, { name: 'b', type: 'float' }, { name: 'a', type: 'float' }],
    params: {},
    glsl: (id, inputs) => {
      const v = inputs.vec || 'vec4(0.0)';
      return `float ${id}_r = ${v}.r;\nfloat ${id}_g = ${v}.g;\nfloat ${id}_b = ${v}.b;\nfloat ${id}_a = ${v}.a;`;
    },
  },
  normalize_node: {
    category: 'vector', label: 'Normalize',
    inputs: [{ name: 'vec', type: 'vec3' }],
    outputs: [{ name: 'result', type: 'vec3' }],
    params: {},
    glsl: (id, inputs) => `vec3 ${id}_result = normalize(${inputs.vec || 'vec3(1.0)'});`,
  },
  length_node: {
    category: 'vector', label: 'Length',
    inputs: [{ name: 'vec', type: 'vec2' }],
    outputs: [{ name: 'result', type: 'float' }],
    params: {},
    glsl: (id, inputs) => `float ${id}_result = length(${inputs.vec || 'vec2(0.0)'});`,
  },
  distance_node: {
    category: 'vector', label: 'Distance',
    inputs: [{ name: 'a', type: 'vec2' }, { name: 'b', type: 'vec2' }],
    outputs: [{ name: 'result', type: 'float' }],
    params: {},
    glsl: (id, inputs) => `float ${id}_result = distance(${inputs.a || 'vec2(0.0)'}, ${inputs.b || 'vec2(0.5)'});`,
  },

  // === COLOR ===
  color_constant: {
    category: 'color', label: 'Color',
    inputs: [],
    outputs: [{ name: 'color', type: 'vec4' }],
    params: { r: 1, g: 0, b: 0, a: 1 },
    glsl: (id, inputs, params) => `vec4 ${id}_color = vec4(${params.r.toFixed(3)}, ${params.g.toFixed(3)}, ${params.b.toFixed(3)}, ${params.a.toFixed(3)});`,
  },
  color_ramp: {
    category: 'color', label: 'Color Ramp',
    inputs: [{ name: 'factor', type: 'float' }],
    outputs: [{ name: 'color', type: 'vec4' }],
    params: { colorA: [0, 0, 0, 1], colorB: [1, 1, 1, 1], midpoint: 0.5 },
    glsl: (id, inputs, params) => {
      const a = params.colorA, b = params.colorB;
      return `vec4 ${id}_color = mix(vec4(${a[0].toFixed(3)},${a[1].toFixed(3)},${a[2].toFixed(3)},${a[3].toFixed(3)}), vec4(${b[0].toFixed(3)},${b[1].toFixed(3)},${b[2].toFixed(3)},${b[3].toFixed(3)}), smoothstep(0.0, 1.0, ${inputs.factor || '0.5'}));`;
    },
  },
  brightness: {
    category: 'color', label: 'Brightness',
    inputs: [{ name: 'color', type: 'vec4' }, { name: 'amount', type: 'float' }],
    outputs: [{ name: 'result', type: 'vec4' }],
    params: {},
    glsl: (id, inputs) => `vec4 ${id}_result = vec4((${inputs.color || 'vec4(0.5)'}).rgb * (1.0 + ${inputs.amount || '0.0'}), (${inputs.color || 'vec4(0.5)'}).a);`,
  },
  invert_color: {
    category: 'color', label: 'Invert',
    inputs: [{ name: 'color', type: 'vec4' }],
    outputs: [{ name: 'result', type: 'vec4' }],
    params: {},
    glsl: (id, inputs) => `vec4 ${id}_result = vec4(1.0 - (${inputs.color || 'vec4(0.5)'}).rgb, (${inputs.color || 'vec4(0.5)'}).a);`,
  },

  // === SHAPE ===
  circle_sdf: {
    category: 'shape', label: 'Circle SDF',
    inputs: [{ name: 'uv', type: 'vec2' }],
    outputs: [{ name: 'dist', type: 'float' }, { name: 'mask', type: 'float' }],
    params: { radius: 0.3, centerX: 0.5, centerY: 0.5 },
    glsl: (id, inputs, params) => `float ${id}_dist = length(${inputs.uv || 'isf_FragNormCoord'} - vec2(${params.centerX.toFixed(3)}, ${params.centerY.toFixed(3)})) - ${params.radius.toFixed(3)};\nfloat ${id}_mask = 1.0 - smoothstep(0.0, 0.005, ${id}_dist);`,
  },
  box_sdf: {
    category: 'shape', label: 'Box SDF',
    inputs: [{ name: 'uv', type: 'vec2' }],
    outputs: [{ name: 'dist', type: 'float' }, { name: 'mask', type: 'float' }],
    params: { width: 0.4, height: 0.3, centerX: 0.5, centerY: 0.5 },
    glsl: (id, inputs, params) => {
      return `vec2 ${id}_d = abs(${inputs.uv || 'isf_FragNormCoord'} - vec2(${params.centerX.toFixed(3)}, ${params.centerY.toFixed(3)})) - vec2(${(params.width/2).toFixed(3)}, ${(params.height/2).toFixed(3)});\nfloat ${id}_dist = length(max(${id}_d, 0.0)) + min(max(${id}_d.x, ${id}_d.y), 0.0);\nfloat ${id}_mask = 1.0 - smoothstep(0.0, 0.005, ${id}_dist);`;
    },
  },

  // === TRANSFORM ===
  translate_uv: {
    category: 'transform', label: 'Translate UV',
    inputs: [{ name: 'uv', type: 'vec2' }],
    outputs: [{ name: 'uv', type: 'vec2' }],
    params: { x: 0, y: 0 },
    glsl: (id, inputs, params) => `vec2 ${id}_uv = ${inputs.uv || 'isf_FragNormCoord'} + vec2(${params.x.toFixed(3)}, ${params.y.toFixed(3)});`,
  },
  scale_uv: {
    category: 'transform', label: 'Scale UV',
    inputs: [{ name: 'uv', type: 'vec2' }],
    outputs: [{ name: 'uv', type: 'vec2' }],
    params: { scale: 1.0 },
    glsl: (id, inputs, params) => `vec2 ${id}_uv = (${inputs.uv || 'isf_FragNormCoord'} - 0.5) * ${params.scale.toFixed(3)} + 0.5;`,
  },
  rotate_uv: {
    category: 'transform', label: 'Rotate UV',
    inputs: [{ name: 'uv', type: 'vec2' }, { name: 'angle', type: 'float' }],
    outputs: [{ name: 'uv', type: 'vec2' }],
    params: {},
    glsl: (id, inputs) => `vec2 ${id}_c = ${inputs.uv || 'isf_FragNormCoord'} - 0.5;\nfloat ${id}_ca = cos(${inputs.angle || '0.0'}); float ${id}_sa = sin(${inputs.angle || '0.0'});\nvec2 ${id}_uv = vec2(${id}_c.x * ${id}_ca - ${id}_c.y * ${id}_sa, ${id}_c.x * ${id}_sa + ${id}_c.y * ${id}_ca) + 0.5;`,
  },
  tile_repeat: {
    category: 'transform', label: 'Tile/Repeat',
    inputs: [{ name: 'uv', type: 'vec2' }],
    outputs: [{ name: 'uv', type: 'vec2' }],
    params: { tilesX: 3, tilesY: 3 },
    glsl: (id, inputs, params) => `vec2 ${id}_uv = fract(${inputs.uv || 'isf_FragNormCoord'} * vec2(${params.tilesX.toFixed(1)}, ${params.tilesY.toFixed(1)}));`,
  },
  kaleidoscope: {
    category: 'transform', label: 'Kaleidoscope',
    inputs: [{ name: 'uv', type: 'vec2' }],
    outputs: [{ name: 'uv', type: 'vec2' }],
    params: { segments: 6.0 },
    glsl: (id, inputs, params) => `vec2 ${id}_p = ${inputs.uv || 'isf_FragNormCoord'} - 0.5;\nfloat ${id}_a = atan(${id}_p.y, ${id}_p.x);\nfloat ${id}_r = length(${id}_p);\nfloat ${id}_seg = ${params.segments.toFixed(1)};\n${id}_a = mod(${id}_a, 6.28318 / ${id}_seg);\n${id}_a = abs(${id}_a - 3.14159 / ${id}_seg);\nvec2 ${id}_uv = vec2(cos(${id}_a), sin(${id}_a)) * ${id}_r + 0.5;`,
  },

  // === TIME ===
  oscillate: {
    category: 'time', label: 'Oscillate',
    inputs: [],
    outputs: [{ name: 'value', type: 'float' }],
    params: { speed: 1.0, min: 0.0, max: 1.0 },
    glsl: (id, inputs, params) => `float ${id}_value = ${params.min.toFixed(3)} + (sin(TIME * ${params.speed.toFixed(3)}) * 0.5 + 0.5) * (${params.max.toFixed(3)} - ${params.min.toFixed(3)});`,
  },
  pulse: {
    category: 'time', label: 'Pulse',
    inputs: [],
    outputs: [{ name: 'value', type: 'float' }],
    params: { speed: 2.0, duty: 0.5 },
    glsl: (id, inputs, params) => `float ${id}_value = step(${params.duty.toFixed(3)}, fract(TIME * ${params.speed.toFixed(3)}));`,
  },

  // === TEXTURE ===
  texture_sample: {
    category: 'texture', label: 'Sample Texture',
    inputs: [{ name: 'uv', type: 'vec2' }],
    outputs: [{ name: 'color', type: 'vec4' }],
    params: { textureName: 'inputImage' },
    glsl: (id, inputs, params) => `vec4 ${id}_color = texture2D(${params.textureName}, ${inputs.uv || 'isf_FragNormCoord'});`,
  },

  // === LOGIC ===
  step_node: {
    category: 'logic', label: 'Step',
    inputs: [{ name: 'value', type: 'float' }],
    outputs: [{ name: 'result', type: 'float' }],
    params: { edge: 0.5 },
    glsl: (id, inputs, params) => `float ${id}_result = step(${params.edge.toFixed(3)}, ${inputs.value || '0.0'});`,
  },

  // === OUTPUT ===
  output: {
    category: 'output', label: 'Output',
    inputs: [{ name: 'color', type: 'vec4' }],
    outputs: [],
    params: {},
    glsl: (id, inputs) => `gl_FragColor = ${inputs.color || 'vec4(0.0, 0.0, 0.0, 1.0)'};`,
  },
};

export function getNodeType(type) {
  return NODE_TYPES[type] || null;
}

export function getNodesByCategory() {
  const result = {};
  for (const [type, def] of Object.entries(NODE_TYPES)) {
    const cat = def.category;
    if (!result[cat]) result[cat] = [];
    result[cat].push({ type, ...def });
  }
  return result;
}

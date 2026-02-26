// ISF Parser + Shader Builder
// Extracted from ShaderClaw monolith

/**
 * Parse ISF metadata from shader source
 * ISF format: JSON metadata block comment followed by GLSL code
 */
export function parseISF(source) {
  const match = source.match(/\/\*\s*(\{[\s\S]*?\})\s*\*\//);
  if (!match) return { meta: null, glsl: source.trim(), inputs: [] };
  try {
    const meta = JSON.parse(match[1]);
    const glsl = source.slice(source.indexOf(match[0]) + match[0].length).trim();
    return { meta, glsl, inputs: meta.INPUTS || [] };
  } catch (e) {
    return { meta: null, glsl: source.trim(), inputs: [] };
  }
}

/**
 * Convert ISF input type to GLSL uniform declaration
 */
export function isfInputToUniform(input) {
  const t = input.TYPE;
  if (t === 'float') return `uniform float ${input.NAME};`;
  if (t === 'color') return `uniform vec4 ${input.NAME};`;
  if (t === 'bool') return `uniform bool ${input.NAME};`;
  if (t === 'point2D') return `uniform vec2 ${input.NAME};`;
  if (t === 'image') return `uniform sampler2D ${input.NAME};`;
  if (t === 'long') return `uniform float ${input.NAME};`;
  if (t === 'text') {
    const maxLen = input.MAX_LENGTH || 12;
    const lines = [];
    for (let i = 0; i < maxLen; i++) lines.push(`uniform float ${input.NAME}_${i};`);
    lines.push(`uniform float ${input.NAME}_len;`);
    return lines.join('\n');
  }
  return `// unknown type: ${t} ${input.NAME}`;
}

/**
 * Build complete fragment shader from ISF source
 * Adds standard uniforms (TIME, RENDERSIZE, audio, mediapipe, etc.)
 * Handles transparent background wrapping
 * Returns { frag, parsed, headerLineCount }
 */
export function buildFragmentShader(source) {
  const parsed = parseISF(source);
  const uniformLines = (parsed.inputs || []).map(isfInputToUniform);

  const headerParts = [
    'precision highp float;',
    'uniform float TIME;',
    'uniform vec2 RENDERSIZE;',
    'uniform int PASSINDEX;',
    'uniform int FRAMEINDEX;',
    'varying vec2 isf_FragNormCoord;',
    '#define IMG_NORM_PIXEL(img, coord) texture2D(img, coord)',
    '#define IMG_PIXEL(img, coord) texture2D(img, coord / RENDERSIZE)',
    '#define IMG_THIS_PIXEL(img) texture2D(img, isf_FragNormCoord)',
    '#define IMG_THIS_NORM_PIXEL(img) texture2D(img, isf_FragNormCoord)',
    // Mouse
    'uniform vec2 mousePos;',
    'uniform vec2 mouseDelta;',
    // Audio-reactive
    'uniform sampler2D audioFFT;',
    'uniform float audioLevel;',
    'uniform float audioBass;',
    'uniform float audioMid;',
    'uniform float audioHigh;',
    // Variable font
    'uniform sampler2D varFontTex;',
    'uniform sampler2D fontAtlasTex;',
    'uniform float useFontAtlas;',
    // Voice decay
    'uniform float _voiceGlitch;',
    // MediaPipe
    'uniform sampler2D mpHandLandmarks;',
    'uniform sampler2D mpFaceLandmarks;',
    'uniform sampler2D mpPoseLandmarks;',
    'uniform sampler2D mpSegMask;',
    'uniform float mpHandCount;',
    'uniform vec3 mpHandPos;',
    // Layer compositing
    'uniform float _transparentBg;',
    // Effects layer: reads composite of layers below
    'uniform sampler2D inputImage;',
    ...uniformLines,
    ''
  ];

  // Inject TARGET sampler declarations from PASSES
  if (parsed.meta && Array.isArray(parsed.meta.PASSES)) {
    for (const pass of parsed.meta.PASSES) {
      if (pass.TARGET) {
        headerParts.push(`uniform sampler2D ${pass.TARGET};`);
      }
    }
    headerParts.push('');
  }

  const header = headerParts.join('\n');
  const cleaned = parsed.glsl.replace(/#version\s+\d+.*/g, '');

  // Wrap main() to inject transparent background support
  const shaderHandlesTransparency = (parsed.inputs || []).some(inp => inp.NAME === 'transparentBg');
  let body = header + cleaned;
  const mainRe = /void\s+main\s*\(\s*\)/;
  if (mainRe.test(body) && !shaderHandlesTransparency) {
    body = body.replace(mainRe, 'void _shaderMain()');
    body += `
void main() {
    _shaderMain();
    if (_transparentBg > 0.5) {
        float _lum = dot(gl_FragColor.rgb, vec3(0.299, 0.587, 0.114));
        gl_FragColor.a = smoothstep(0.02, 0.15, _lum);
    }
}
`;
  }

  return { frag: body, parsed, headerLineCount: headerParts.length };
}

/** Vertex shader for fullscreen triangle */
export const VERT_SHADER = `
attribute vec2 position;
varying vec2 isf_FragNormCoord;
void main() {
    isf_FragNormCoord = position * 0.5 + 0.5;
    gl_Position = vec4(position, 0.0, 1.0);
}`;

/** Default ISF shader (particle network) */
export const DEFAULT_SHADER = `/*
{
  "DESCRIPTION": "Particle network — drifting points connected by proximity lines",
  "CATEGORIES": ["Generator"],
  "INPUTS": [
    { "NAME": "particleCount", "TYPE": "float", "DEFAULT": 40.0, "MIN": 10.0, "MAX": 80.0 },
    { "NAME": "speed", "TYPE": "float", "DEFAULT": 0.4, "MIN": 0.0, "MAX": 2.0 },
    { "NAME": "connectDist", "TYPE": "float", "DEFAULT": 0.25, "MIN": 0.05, "MAX": 0.5 },
    { "NAME": "lineWidth", "TYPE": "float", "DEFAULT": 1.0, "MIN": 0.3, "MAX": 3.0 },
    { "NAME": "dotSize", "TYPE": "float", "DEFAULT": 3.0, "MIN": 1.0, "MAX": 8.0 },
    { "NAME": "color1", "TYPE": "color", "DEFAULT": [1.0, 1.0, 1.0, 1.0] },
    { "NAME": "bgColor", "TYPE": "color", "DEFAULT": [0.02, 0.02, 0.05, 1.0] }
  ]
}*/

vec2 particleHash(float id) {
    return vec2(
        fract(sin(id * 127.1 + 311.7) * 43758.5453),
        fract(sin(id * 269.5 + 183.3) * 28001.8384)
    );
}

vec2 particlePos(float id, float t) {
    vec2 seed = particleHash(id);
    vec2 vel = (particleHash(id + 100.0) - 0.5) * 0.5;
    return fract(seed + vel * t);
}

float segDist(vec2 p, vec2 a, vec2 b) {
    vec2 ab = b - a;
    float len2 = dot(ab, ab);
    if (len2 < 0.000001) return length(p - a);
    float t = clamp(dot(p - a, ab) / len2, 0.0, 1.0);
    vec2 proj = a + ab * t;
    return length(p - proj);
}

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    vec2 p = vec2(uv.x * aspect, uv.y);
    float t = TIME * speed;
    float px = 1.0 / RENDERSIZE.y;

    int N = int(particleCount);
    vec3 col = bgColor.rgb;

    float lineAccum = 0.0;
    for (int i = 0; i < 80; i++) {
        if (i >= N) break;
        vec2 pi = particlePos(float(i), t);
        pi.x *= aspect;
        for (int j = 0; j < 80; j++) {
            if (j >= N || j <= i) break;
            vec2 pj = particlePos(float(j), t);
            pj.x *= aspect;
            float d = length(pi - pj);
            if (d > connectDist) continue;
            vec2 mn = min(pi, pj) - vec2(connectDist * 0.1);
            vec2 mx = max(pi, pj) + vec2(connectDist * 0.1);
            if (p.x < mn.x || p.x > mx.x || p.y < mn.y || p.y > mx.y) continue;
            float sd = segDist(p, pi, pj);
            float lw = lineWidth * px;
            float alpha = (1.0 - d / connectDist);
            alpha *= smoothstep(lw, lw * 0.3, sd);
            lineAccum += alpha * 0.5;
        }
    }
    col += color1.rgb * min(lineAccum, 1.0);

    float dotAccum = 0.0;
    for (int i = 0; i < 80; i++) {
        if (i >= N) break;
        vec2 pi = particlePos(float(i), t);
        pi.x *= aspect;
        float d = length(p - pi);
        float r = dotSize * px;
        dotAccum += smoothstep(r, r * 0.15, d);
    }
    col += color1.rgb * min(dotAccum, 1.5);

    gl_FragColor = vec4(col, 1.0);
}`;

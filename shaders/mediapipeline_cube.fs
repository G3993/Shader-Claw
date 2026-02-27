/*{
  "DESCRIPTION": "Body Tracking Cube — SDF raymarched box/octahedron blend, driven by gesture parameters",
  "CATEGORIES": ["Generator"],
  "INPUTS": [
    { "NAME": "texture", "LABEL": "Texture", "TYPE": "image" },
    { "NAME": "texMix", "LABEL": "Texture Mix", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.8 },
    { "NAME": "rotationX", "LABEL": "Rotation X", "TYPE": "float", "MIN": -3.14159, "MAX": 3.14159, "DEFAULT": 0.0 },
    { "NAME": "rotationY", "LABEL": "Rotation Y", "TYPE": "float", "MIN": -3.14159, "MAX": 3.14159, "DEFAULT": 0.0 },
    { "NAME": "glow", "LABEL": "Glow", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.5 },
    { "NAME": "morph", "LABEL": "Morph", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 },
    { "NAME": "alive", "LABEL": "Alive", "TYPE": "float", "MIN": 0.0, "MAX": 1.0, "DEFAULT": 0.0 }
  ]
}*/

mat3 rotateX(float a) {
  float c = cos(a), s = sin(a);
  return mat3(1, 0, 0, 0, c, -s, 0, s, c);
}

mat3 rotateY(float a) {
  float c = cos(a), s = sin(a);
  return mat3(c, 0, s, 0, 1, 0, -s, 0, c);
}

float sdBox(vec3 p, vec3 b) {
  vec3 q = abs(p) - b;
  return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
}

float sdOctahedron(vec3 p, float s) {
  p = abs(p);
  return (p.x + p.y + p.z - s) * 0.57735027;
}

float smin(float a, float b, float k) {
  float h = clamp(0.5 + 0.5 * (b - a) / k, 0.0, 1.0);
  return mix(b, a, h) - k * h * (1.0 - h);
}

// Rotate point into local space (shared by map and texturing)
vec3 toLocal(vec3 p) {
  p = rotateX(rotationX) * rotateY(rotationY) * p;
  p += alive * 0.025 * vec3(
    sin(TIME * 4.0 + p.y * 3.0),
    sin(TIME * 3.3 + p.z * 3.0),
    sin(TIME * 3.7 + p.x * 3.0)
  );
  p += morph * 0.14 * vec3(
    sin(TIME * 23.0 + p.y * 5.0),
    cos(TIME * 19.0 + p.z * 5.0),
    sin(TIME * 29.0 + p.x * 5.0)
  );
  return p;
}

float map(vec3 p) {
  p = toLocal(p);
  float al = alive * 0.03 * sin(TIME * 3.0);
  float sc = 0.7;
  float b = sdBox(p, vec3(0.55 * sc, 0.55 * sc + al, 0.55 * sc));
  float o = sdOctahedron(p, 0.9 * sc);
  float blend = alive * 0.3 + morph * 0.6;
  float k = 0.05 + blend * 0.5;
  float s = mix(b, smin(b, o, k), blend);
  s += sin(p.x * 5.0 + TIME) * sin(p.y * 5.0 + TIME * 0.7) * sin(p.z * 5.0 + TIME * 1.3) * 0.02 * (1.0 + morph * 2.0);
  return s;
}

vec3 calcNormal(vec3 p) {
  vec2 e = vec2(0.001, 0.0);
  return normalize(vec3(
    map(p + e.xyy) - map(p - e.xyy),
    map(p + e.yxy) - map(p - e.yxy),
    map(p + e.yyx) - map(p - e.yyx)
  ));
}

// Triplanar texture mapping — projects texture from all 3 axes
vec3 triplanar(vec3 p, vec3 n) {
  vec3 w = abs(n);
  w = w / (w.x + w.y + w.z + 0.001);
  vec2 uvX = p.yz * 0.5 + 0.5;
  vec2 uvY = p.xz * 0.5 + 0.5;
  vec2 uvZ = p.xy * 0.5 + 0.5;
  vec3 cx = texture2D(texture, uvX).rgb;
  vec3 cy = texture2D(texture, uvY).rgb;
  vec3 cz = texture2D(texture, uvZ).rgb;
  return cx * w.x + cy * w.y + cz * w.z;
}

void main() {
  vec2 uv = (gl_FragCoord.xy - 0.5 * RENDERSIZE) / min(RENDERSIZE.x, RENDERSIZE.y);

  vec3 ro = vec3(0.0, 0.0, 3.0);
  vec3 rd = normalize(vec3(uv, -1.2));

  float t = 0.0, d;
  for (int i = 0; i < 80; i++) {
    vec3 p = ro + rd * t;
    d = map(p);
    if (d < 0.001 || t > 10.0) break;
    t += d;
  }

  vec3 col = vec3(0.02, 0.02, 0.04);

  if (t < 10.0) {
    vec3 p = ro + rd * t;
    vec3 n = calcNormal(p);
    vec3 ld = normalize(vec3(0.5, 0.8, 0.6));

    float diff = max(dot(n, ld), 0.0);
    float spec = pow(max(dot(reflect(-ld, n), -rd), 0.0), 32.0);
    float fres = pow(1.0 - max(dot(n, -rd), 0.0), 3.0);

    // Base color: procedural gradient or texture
    vec3 bc = mix(vec3(1.0), vec3(0.9, 0.9, 1.0), fres);

    // Texture mapping: triplanar projection in rotated local space
    vec3 localP = toLocal(p);
    vec3 localN = normalize(toLocal(p + n * 0.01) - localP);
    vec3 texCol = triplanar(localP / 0.7, localN);
    bc = mix(bc, texCol, texMix);

    // Lighting: keep texture colors true, just add subtle shading
    float light = diff * 0.3 + 0.7;  // 70% ambient floor — texture stays vibrant
    col = bc * light;
    col += vec3(1.0) * spec * 0.2;
    col += bc * fres * glow * 0.3;
  }

  // Ambient glow
  col += vec3(1.0) * exp(-length(uv) * 2.0) * glow * 0.1;

  // Subtle vignette only
  col *= 1.0 - dot(uv * 0.6, uv * 0.6);

  gl_FragColor = vec4(col, 1.0);
}

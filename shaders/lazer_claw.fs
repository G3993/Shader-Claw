/*
{
  "CATEGORIES": [
    "Generator"
  ],
  "INPUTS": [
    {
      "NAME": "speed",
      "LABEL": "Speed",
      "TYPE": "float",
      "DEFAULT": 0.5,
      "MIN": 0.0,
      "MAX": 3.0
    },
    {
      "NAME": "intensity",
      "LABEL": "Glow",
      "TYPE": "float",
      "DEFAULT": 0.012,
      "MIN": 0.001,
      "MAX": 0.06
    },
    {
      "NAME": "clawSize",
      "LABEL": "Size",
      "TYPE": "float",
      "DEFAULT": 0.7,
      "MIN": 0.1,
      "MAX": 2.0
    },
    {
      "NAME": "curvature",
      "LABEL": "Curve",
      "TYPE": "float",
      "DEFAULT": 0.3,
      "MIN": -1.0,
      "MAX": 1.0
    },
    {
      "NAME": "spread",
      "LABEL": "Spread",
      "TYPE": "float",
      "DEFAULT": 0.22,
      "MIN": 0.05,
      "MAX": 0.8
    },
    {
      "NAME": "slashAngle",
      "LABEL": "Angle",
      "TYPE": "float",
      "DEFAULT": 0.4,
      "MIN": -1.57,
      "MAX": 1.57
    },
    {
      "NAME": "color1",
      "LABEL": "Claw 1",
      "TYPE": "color",
      "DEFAULT": [0.91, 0.25, 0.34, 1.0]
    },
    {
      "NAME": "color2",
      "LABEL": "Claw 2",
      "TYPE": "color",
      "DEFAULT": [1.0, 1.0, 1.0, 1.0]
    },
    {
      "NAME": "color3",
      "LABEL": "Claw 3",
      "TYPE": "color",
      "DEFAULT": [1.0, 0.0, 0.0, 1.0]
    }
  ]
}
*/

// Lazer Claw — 3 neon claw slashes with glow

#ifdef GL_ES
precision highp float;
#endif

vec2 rot2d(vec2 p, float a) {
    float c = cos(a), s = sin(a);
    return vec2(p.x * c - p.y * s, p.x * s + p.y * c);
}

// SDF for a single claw mark — curved tapered slash with razor tips
float clawSDF(vec2 p, float len, float w, float curve) {
    // Bend the space along x^2
    p.y -= curve * p.x * p.x;
    // Normalized position along claw length
    float t = clamp(p.x / len, -1.0, 1.0);
    // Sharp taper: thick center, razor-thin tips
    float taper = w * pow(1.0 - t * t, 0.7);
    // Distance field
    float dy = abs(p.y) - taper;
    float dx = abs(p.x) - len;
    return length(max(vec2(dx, dy), 0.0)) + min(max(dx, dy), 0.0);
}

// Turbulent energy along the claw (organic shimmer)
float clawNoise(vec2 p, float t) {
    return 0.5 + 0.5 * sin(p.x * 12.0 + t * 3.0)
               * sin(p.y * 8.0 - t * 2.3)
               * sin((p.x + p.y) * 6.0 + t * 1.7);
}

void main(void) {
    vec2 uv = (gl_FragCoord.xy / RENDERSIZE.xy) * 2.0 - 1.0;
    uv.x *= RENDERSIZE.x / RENDERSIZE.y;

    float t = TIME * speed * (1.0 + audioHigh * 1.5);

    // Global rotation — user angle + gentle sway
    float globalRot = slashAngle + sin(t * 0.3) * 0.08;
    uv = rot2d(uv, globalRot);

    // Scale
    uv /= clawSize;

    // Animated claw parameters
    float w = 0.05 + 0.012 * sin(t * 2.5);
    float len = 0.85 + 0.08 * sin(t * 0.8);
    float crv = curvature + 0.06 * sin(t * 0.6);

    // Fan angle — claws splay outward slightly
    float fan = 0.07 + 0.02 * sin(t * 0.4);

    // 3 claw marks: fanned, staggered, each slightly unique
    vec2 p1 = rot2d(uv + vec2(-0.06, spread), fan);
    vec2 p2 = uv + vec2(0.0, 0.0);
    vec2 p3 = rot2d(uv + vec2(0.06, -spread), -fan);

    float d1 = clawSDF(p1, len * 0.95, w * 0.9, crv * 1.1);
    float d2 = clawSDF(p2, len, w * 1.2, crv * 0.9);
    float d3 = clawSDF(p3, len * 0.95, w * 0.9, crv);

    // Neon glow — inverse square falloff per claw
    float glow = intensity * (1.0 + audioBass * 5.0);

    vec3 col = vec3(0.0);
    col += color1.rgb * glow / (d1 * d1 + glow);
    col += color2.rgb * glow / (d2 * d2 + glow);
    col += color3.rgb * glow / (d3 * d3 + glow);

    // Swipe flash — bright pulse racing along the claw
    float swipeSpeed = 0.35 + audioLevel * 0.5;
    float swipePos = fract(t * swipeSpeed) * 4.0 - 2.0;
    float swipeFlash = exp(-5.0 * pow(uv.x - swipePos, 2.0));
    col *= 1.0 + swipeFlash * 0.8;

    // Shimmer: organic turbulence along the glow
    float shimmer = clawNoise(uv, t);
    col *= 0.85 + 0.15 * shimmer;

    // Hot core bloom — saturate bright areas
    col += col * col * 0.4;

    // Pinch boost
    col *= 1.0 + pinchHold * 2.0;

    gl_FragColor = vec4(col, 1.0);
}

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
      "DEFAULT": 0.3,
      "MIN": 0.0,
      "MAX": 5.0
    },
    {
      "NAME": "intensity",
      "LABEL": "Intensity",
      "TYPE": "float",
      "DEFAULT": 0.005,
      "MIN": 0.001,
      "MAX": 0.05
    },
    {
      "NAME": "zoom",
      "LABEL": "Scale",
      "TYPE": "float",
      "DEFAULT": 4.0,
      "MIN": 0.2,
      "MAX": 4.0
    },
    {
      "NAME": "color1",
      "LABEL": "Color A",
      "TYPE": "color",
      "DEFAULT": [0.91, 0.25, 0.34, 1.0]
    },
    {
      "NAME": "color2",
      "LABEL": "Color B",
      "TYPE": "color",
      "DEFAULT": [1.0, 1.0, 1.0, 1.0]
    },
    {
      "NAME": "color3",
      "LABEL": "Color C",
      "TYPE": "color",
      "DEFAULT": [1.0, 0.0, 0.0, 1.0]
    }
  ]
}
*/

// RGB Laser Globe — by @paulofalcao

#ifdef GL_ES
precision highp float;
#endif

float makePoint(float x, float y, float fx, float fy, float sx, float sy, float t) {
   float xx = x * cos(t * fx);
   float yy = y * sin(t * fy);
   return 1.0 / (sqrt(length(xx + yy) + length(xx * yy)));
}

vec3 laserCluster(vec2 p, float t) {
   float x = p.x;
   float y = p.y;

   float a =
       makePoint(x, y, 3.3, 2.9, 0.3, 0.3, t);
   a = a + makePoint(x, y, 1.9, 2.0, 0.4, 0.4, t);
   a = a + makePoint(x, y, 0.8, 0.7, 0.4, 0.5, t);
   a = a + makePoint(x, y, 2.3, 0.1, 0.6, 0.3, t);
   a = a + makePoint(x, y, 0.8, 1.7, 0.5, 0.4, t);
   a = a + makePoint(x, y, 0.3, 1.0, 0.4, 0.4, t);
   a = a + makePoint(x, y, 1.4, 1.7, 0.4, 0.5, t);
   a = a + makePoint(x, y, 1.3, 2.1, 0.6, 0.3, t);
   a = a + makePoint(x, y, 1.8, 1.7, 0.5, 0.4, t);

   float b =
       makePoint(x, y, 1.2, 1.9, 0.3, 0.3, t);
   b = b + makePoint(x, y, 0.7, 2.7, 0.4, 0.4, t);
   b = b + makePoint(x, y, 1.4, 0.6, 0.4, 0.5, t);
   b = b + makePoint(x, y, 2.6, 0.9, 0.6, 0.3, t);
   b = b + makePoint(x, y, 0.7, 1.4, 0.5, 0.4, t);
   b = b + makePoint(x, y, 0.7, 1.7, 0.4, 0.4, t);
   b = b + makePoint(x, y, 0.8, 0.5, 0.4, 0.5, t);
   b = b + makePoint(x, y, 1.4, 0.7, 0.6, 0.3, t);
   b = b + makePoint(x, y, 0.7, 1.3, 0.5, 0.4, t);

   float c =
       makePoint(x, y, 3.7, 0.3, 0.3, 0.3, t);
   c = c + makePoint(x, y, 1.9, 1.3, 0.4, 0.4, t);
   c = c + makePoint(x, y, 0.8, 0.9, 0.4, 0.5, t);
   c = c + makePoint(x, y, 1.2, 1.7, 0.6, 0.3, t);
   c = c + makePoint(x, y, 0.3, 0.6, 0.5, 0.4, t);
   c = c + makePoint(x, y, 0.3, 0.3, 0.4, 0.4, t);
   c = c + makePoint(x, y, 1.4, 0.8, 0.4, 0.5, t);
   c = c + makePoint(x, y, 0.2, 0.6, 0.6, 0.3, t);
   c = c + makePoint(x, y, 1.3, 0.5, 0.5, 0.4, t);

   return a * color1.rgb + b * color2.rgb + c * color3.rgb;
}

// Map a 0–1 hand/mouse position into p-space
vec2 toCenter(vec2 pos) {
   return (pos * 2.0 - 1.0) * vec2(1.0, RENDERSIZE.y / RENDERSIZE.x) * zoom;
}

void main(void) {
   vec2 p = (gl_FragCoord.xy / RENDERSIZE.x) * 2.0 - vec2(1.0, RENDERSIZE.y / RENDERSIZE.x);
   p *= zoom;
   float t = TIME * speed * (1.0 + audioHigh * 2.0);

   // Smooth sinusoidal drift — ease-in/ease-out parallax feel
   vec2 drift = 0.03 * vec2(sin(t * 0.17), cos(t * 0.23));

   // First cluster: follows mouse (or primary hand via hand-as-mouse)
   vec3 d = laserCluster(p - toCenter(mousePos) + drift, t);

   // Second cluster: when two hands are detected, mirror X for webcam flip
   if (mpHandCount >= 2.0) {
      vec2 hand2 = vec2(1.0 - mpHandPos2.x, mpHandPos2.y);
      d += laserCluster(p - toCenter(hand2), t);
   }

   // Pinch boost: holding pinch ramps intensity up over time
   float boost = 1.0 + pinchHold * 3.0 + audioBass * 5.0;
   d *= intensity * boost;

   gl_FragColor = vec4(d, 1.0);
}

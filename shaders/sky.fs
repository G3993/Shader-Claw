/*{
  "CATEGORIES": [
    "Generator",
    "Nature"
  ],
  "DESCRIPTION": "Starfield with rotating layers, customizable colors and speed",
  "INPUTS": [
    {
      "NAME": "skyColor",
      "TYPE": "color",
      "DEFAULT": [0.0, 0.0, 0.3, 1.0]
    },
    {
      "NAME": "starColor",
      "TYPE": "color",
      "DEFAULT": [1.0, 1.0, 1.0, 1.0]
    },
    {
      "NAME": "speed",
      "TYPE": "float",
      "MIN": 0.0,
      "MAX": 3.0,
      "DEFAULT": 1.0
    },
    {
      "NAME": "starSize",
      "TYPE": "float",
      "MIN": 0.1,
      "MAX": 2.0,
      "DEFAULT": 0.5
    },
    {
      "NAME": "starLayers",
      "TYPE": "float",
      "MIN": 1.0,
      "MAX": 8.0,
      "DEFAULT": 5.0
    }
  ]
}*/

const float PI = 3.141592654;
float zoom = fract(TIME * speed)/4.;

vec4 star(vec2 uv, float zoom, float seed)
{
	uv *= zoom;
	vec2 s = floor(uv);
	vec2 f = fract(uv);
	vec2 p = .5 + .440 * (sin(s + PI)) * sin(11. * fract(sin((s + seed) * mat2(7.5, 3.3, 6.2, 5.4)) * 55.)) - f;
	float d = length(p);
	float k = smoothstep(d*.9, d, 0.025 * starSize);
	float shades = 2.0;
	vec4 color = vec4(
		(shades/(shades-1.0))*mod(floor(shades*uv.y)/shades, 1.0),
		(shades/(shades-1.0))*mod(floor(shades*uv.x)/shades, 1.0),
		(shades/(shades-1.0))*mod(floor(shades*uv.x)/shades, 1.0), 
		1.
	);
    color = vec4(starColor.rgb, 1.0);
    return vec4(k * color.r, k * color.g, k * color.b, k);
}

void main(void)
{
	float phase = (fract(TIME * speed)/5.) * PI;
	float blue = 1.-((isf_FragNormCoord.x/2.) + (isf_FragNormCoord.y));
	float opacity = (isf_FragNormCoord.x + isf_FragNormCoord.y);
	vec2 uv = (gl_FragCoord.xy*2.-RENDERSIZE.xy) / min(RENDERSIZE.x,RENDERSIZE.y); 
	uv *= mat2(cos(phase), -sin(phase), sin(phase), cos(phase));
	vec4 c = vec4(skyColor.rgb * blue, opacity);
	for(float i = 0.; i < 8.; i += 1.)
	{
		if (i >= starLayers) break;
		vec4 dust = star(uv, mod(starLayers + i - zoom * starLayers, starLayers), i * 5.);
		c = mix(c, dust, dust.a*c.a);
	}
	gl_FragColor = vec4(c);
}

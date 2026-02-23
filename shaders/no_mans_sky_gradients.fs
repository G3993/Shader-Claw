/*{
	"DESCRIPTION": "Animated soft-light gradient with 3 drifting color stops",
	"CREDIT": "by you",
	"CATEGORIES": [
		"Generator"
	],
	"INPUTS": [

	{
			"NAME": "Color1",
			"TYPE": "color",
			"DEFAULT": [
				1.0,
				0.0,
				0.0,
				1.0
			]
		},
		{
			"NAME": "Color1_Radius",
			"TYPE": "float",
			"MIN": 0.0,
			"MAX": 1.0,
			"DEFAULT": 1.0
		},
		{
			"NAME": "Color1_Y",
			"TYPE": "float",
			"MIN": 0.0,
			"MAX": 1.0,
			"DEFAULT": 0.0
		},
		{
			"NAME": "Color2",
			"TYPE": "color",
			"DEFAULT": [
				0.0,
				1.0,
				0.0,
				1.0
			]
		},
		{
			"NAME": "Color2_Radius",
			"TYPE": "float",
			"MIN": 0.0,
			"MAX": 1.0,
			"DEFAULT": 1.0
		},
		{
			"NAME": "Color2_Y",
			"TYPE": "float",
			"MIN": 0.0,
			"MAX": 1.0,
			"DEFAULT": 0.5
		},
		{
			"NAME": "Color3",
			"TYPE": "color",
			"DEFAULT": [
				0.0,
				0.0,
				1.0,
				1.0
			]
		},
		{
			"NAME": "Color3_Radius",
			"TYPE": "float",
			"MIN": 0.0,
			"MAX": 1.0,
			"DEFAULT": 1.0
		},
		{
			"NAME": "Color3_Y",
			"TYPE": "float",
			"MIN": 0.0,
			"MAX": 1.0,
			"DEFAULT": 1.0
		},
		{
			"NAME": "CanvasColor",
			"TYPE": "color",
			"DEFAULT": [
				0.0,
				0.0,
				0.0,
				1.0
			]
		},
		{
			"NAME": "shape",
			"TYPE": "long",
			"VALUES": [0,1,2,3,4],
			"LABELS": ["Linear","Radial","Diamond","Star","Rings"],
			"DEFAULT": 0
		},
		{
			"NAME": "VERTICAL",
			"TYPE": "bool",
			"DEFAULT": 0.0
		},
		{
			"NAME": "speed",
			"TYPE": "float",
			"MIN": 0.0,
			"MAX": 2.0,
			"DEFAULT": 0.5
		}

	]
}*/

// Adapted from "RGB Soft Lights" by BitOfGold: https://www.shadertoy.com/view/llVGz1

vec3 iResolution = vec3(RENDERSIZE, 1.);

float shapeDist(vec2 delta, int shapeIdx) {
    if (shapeIdx == 1) {
        // Radial: euclidean distance
        return length(delta);
    } else if (shapeIdx == 2) {
        // Diamond: manhattan distance
        return abs(delta.x) + abs(delta.y);
    } else if (shapeIdx == 3) {
        // Star: 4-pointed star distance
        float a = abs(delta.x) + abs(delta.y);
        float b = max(abs(delta.x), abs(delta.y)) * 1.4;
        return min(a, b);
    } else if (shapeIdx == 4) {
        // Rings: radial with sine modulation
        float d = length(delta);
        return d + sin(d * 25.0) * 0.03;
    }
    // Linear (0): just Y distance
    return abs(delta.y);
}

vec3 softLight(vec3 canvas, vec2 uv, vec2 center, float r, vec3 color, int shapeIdx) {
    float d = clamp(1.0 - shapeDist(center - uv, shapeIdx) / r, 0.0, 1.0);
    return(canvas + d * color);
}

void mainImage( out vec4 fragColor, in vec2 fragCoord )
{
	vec2 uv = fragCoord.xy / RENDERSIZE.xy;
	float ASPECT = (RENDERSIZE.x / RENDERSIZE.y);
	int shapeIdx = int(shape);

	if (shapeIdx == 0) {
		// Linear mode: collapse to 1D
		if (VERTICAL) { uv.y = uv.x; }
		uv.x = uv.y;
	} else {
		// 2D shape modes: aspect-correct and use 2D centers
		uv.x *= ASPECT;
		if (VERTICAL) {
			float tmp = uv.x;
			uv.x = uv.y;
			uv.y = tmp;
		}
	}

	// Animate Y positions with sin offsets at different phases
	float y1 = Color1_Y + sin(TIME * speed) * 0.3;
	float y2 = Color2_Y + sin(TIME * speed * 1.3 + 2.0) * 0.25;
	float y3 = Color3_Y + sin(TIME * speed * 0.7 + 4.0) * 0.35;

	// Gently pulse the radii
	float r1 = Color1_Radius * (1.0 + 0.1 * sin(TIME * speed * 0.5));
	float r2 = Color2_Radius * (1.0 + 0.1 * sin(TIME * speed * 0.5 + 2.0));
	float r3 = Color3_Radius * (1.0 + 0.1 * sin(TIME * speed * 0.5 + 4.0));

	// Centers: for 2D modes use aspect-corrected X center
	float cx = (shapeIdx == 0) ? 0.5 : ASPECT * 0.5;

	vec3 canvas = vec3(CanvasColor.rgb);
    canvas = softLight(canvas, uv, vec2(cx, y1), r1, vec3(Color1.rgb), shapeIdx);
    canvas = softLight(canvas, uv, vec2(cx, y2), r2, vec3(Color2.rgb), shapeIdx);
    canvas = softLight(canvas, uv, vec2(cx, y3), r3, vec3(Color3.rgb), shapeIdx);
	fragColor = vec4(canvas,1.0);
}


void main(void) {
    mainImage(gl_FragColor, gl_FragCoord.xy);
}

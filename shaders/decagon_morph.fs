/*{
	"CREDIT": "by lennyjpg / ShaderClaw",
	"DESCRIPTION": "Morphable wireframe decagon sculpture — 10 vertices connected by moving internal lines",
	"CATEGORIES": ["Generator"],
	"INPUTS": [
		{
			"NAME": "mode",
			"TYPE": "long",
			"LABEL": "Control Mode",
			"VALUES": [0, 1, 2],
			"LABELS": ["Manual", "Audio", "Hand Tracking"],
			"DEFAULT": 1
		},
		{
			"NAME": "v0", "TYPE": "point2D", "LABEL": "V0", "DEFAULT": [0.5, 0.0], "MIN": [-1.0, -1.0], "MAX": [1.0, 1.0]
		},
		{
			"NAME": "v1", "TYPE": "point2D", "LABEL": "V1", "DEFAULT": [0.405, 0.294], "MIN": [-1.0, -1.0], "MAX": [1.0, 1.0]
		},
		{
			"NAME": "v2", "TYPE": "point2D", "LABEL": "V2", "DEFAULT": [0.155, 0.476], "MIN": [-1.0, -1.0], "MAX": [1.0, 1.0]
		},
		{
			"NAME": "v3", "TYPE": "point2D", "LABEL": "V3", "DEFAULT": [-0.155, 0.476], "MIN": [-1.0, -1.0], "MAX": [1.0, 1.0]
		},
		{
			"NAME": "v4", "TYPE": "point2D", "LABEL": "V4", "DEFAULT": [-0.405, 0.294], "MIN": [-1.0, -1.0], "MAX": [1.0, 1.0]
		},
		{
			"NAME": "v5", "TYPE": "point2D", "LABEL": "V5", "DEFAULT": [-0.5, 0.0], "MIN": [-1.0, -1.0], "MAX": [1.0, 1.0]
		},
		{
			"NAME": "v6", "TYPE": "point2D", "LABEL": "V6", "DEFAULT": [-0.405, -0.294], "MIN": [-1.0, -1.0], "MAX": [1.0, 1.0]
		},
		{
			"NAME": "v7", "TYPE": "point2D", "LABEL": "V7", "DEFAULT": [-0.155, -0.476], "MIN": [-1.0, -1.0], "MAX": [1.0, 1.0]
		},
		{
			"NAME": "v8", "TYPE": "point2D", "LABEL": "V8", "DEFAULT": [0.155, -0.476], "MIN": [-1.0, -1.0], "MAX": [1.0, 1.0]
		},
		{
			"NAME": "v9", "TYPE": "point2D", "LABEL": "V9", "DEFAULT": [0.405, -0.294], "MIN": [-1.0, -1.0], "MAX": [1.0, 1.0]
		},
		{
			"NAME": "baseRadius", "TYPE": "float", "LABEL": "Base Radius",
			"DEFAULT": 0.35, "MIN": 0.05, "MAX": 0.8
		},
		{
			"NAME": "audioGain", "TYPE": "float", "LABEL": "Audio Gain",
			"DEFAULT": 0.8, "MIN": 0.0, "MAX": 2.0
		},
		{
			"NAME": "lineWidth", "TYPE": "float", "LABEL": "Line Width",
			"DEFAULT": 0.002, "MIN": 0.0005, "MAX": 0.01
		},
		{
			"NAME": "glowAmount", "TYPE": "float", "LABEL": "Glow",
			"DEFAULT": 0.15, "MIN": 0.0, "MAX": 0.5
		},
		{
			"NAME": "rotationSpeed", "TYPE": "float", "LABEL": "Rotation Speed",
			"DEFAULT": 0.3, "MIN": -2.0, "MAX": 2.0
		},
		{
			"NAME": "driftSpeed", "TYPE": "float", "LABEL": "Drift Speed",
			"DEFAULT": 1.0, "MIN": 0.0, "MAX": 3.0
		},
		{
			"NAME": "driftAmount", "TYPE": "float", "LABEL": "Drift Amount",
			"DEFAULT": 0.08, "MIN": 0.0, "MAX": 0.3
		},
		{
			"NAME": "innerLines", "TYPE": "float", "LABEL": "Inner Lines",
			"DEFAULT": 1.0, "MIN": 0.0, "MAX": 1.0
		},
		{
			"NAME": "bgColor", "TYPE": "color", "LABEL": "Background",
			"DEFAULT": [0.02, 0.02, 0.03, 1.0]
		}
	]
}*/

#ifdef GL_ES
precision highp float;
#endif

#define PI 3.14159265359
#define TWO_PI 6.28318530718
#define N 10

mat2 rot(float a) {
	float s = sin(a), c = cos(a);
	return mat2(c, -s, s, c);
}

float sdSeg(vec2 p, vec2 a, vec2 b) {
	vec2 pa = p - a, ba = b - a;
	float h = clamp(dot(pa, ba) / dot(ba, ba), 0.0, 1.0);
	return length(pa - ba * h);
}

// Draw a single line with glow — returns additive white intensity
float lineBrightness(vec2 p, vec2 a, vec2 b, float w, float glow) {
	float d = sdSeg(p, a, b);
	float sharp = smoothstep(w, w * 0.2, d);
	float soft = glow * 0.004 / (d + 0.001);
	return sharp + clamp(soft, 0.0, 0.6);
}

float getFFT(float pos) {
	return texture2D(audioFFT, vec2(pos, 0.0)).x;
}

void main() {
	vec2 uv = isf_FragNormCoord.xy;
	float aspect = RENDERSIZE.x / RENDERSIZE.y;
	vec2 p = (uv - 0.5);
	p.x *= aspect;

	p *= rot(TIME * rotationSpeed);

	// Default decagon on unit circle
	vec2 dv[10];
	for (int i = 0; i < N; i++) {
		float a = TWO_PI * float(i) / float(N) - PI * 0.5;
		dv[i] = vec2(cos(a), sin(a));
	}

	// Per-vertex organic drift (unique phase per vertex)
	float t = TIME * driftSpeed;

	vec2 verts[10];
	int m = int(mode);

	if (m == 0) {
		// Manual
		for (int i = 0; i < N; i++) {
			float fi = float(i);
			vec2 drift = vec2(
				sin(t * 1.3 + fi * 2.1) * cos(t * 0.7 + fi * 1.4),
				cos(t * 1.1 + fi * 1.7) * sin(t * 0.9 + fi * 2.3)
			) * driftAmount;
			// Can't index v0-v9 by i, so apply drift to default positions
			verts[i] = dv[i] * baseRadius + drift;
		}
		// Override with manual positions if they've been moved
		verts[0] = v0 + vec2(sin(t*1.3+0.0), cos(t*1.1+0.0)) * driftAmount;
		verts[1] = v1 + vec2(sin(t*1.3+2.1), cos(t*1.1+1.7)) * driftAmount;
		verts[2] = v2 + vec2(sin(t*1.3+4.2), cos(t*1.1+3.4)) * driftAmount;
		verts[3] = v3 + vec2(sin(t*1.3+6.3), cos(t*1.1+5.1)) * driftAmount;
		verts[4] = v4 + vec2(sin(t*1.3+8.4), cos(t*1.1+6.8)) * driftAmount;
		verts[5] = v5 + vec2(sin(t*1.3+10.5), cos(t*1.1+8.5)) * driftAmount;
		verts[6] = v6 + vec2(sin(t*1.3+12.6), cos(t*1.1+10.2)) * driftAmount;
		verts[7] = v7 + vec2(sin(t*1.3+14.7), cos(t*1.1+11.9)) * driftAmount;
		verts[8] = v8 + vec2(sin(t*1.3+16.8), cos(t*1.1+13.6)) * driftAmount;
		verts[9] = v9 + vec2(sin(t*1.3+18.9), cos(t*1.1+15.3)) * driftAmount;

	} else if (m == 1) {
		// Audio — fast, reactive
		for (int i = 0; i < N; i++) {
			float fi = float(i);
			float fftVal = getFFT(fi / float(N));
			float disp = baseRadius + fftVal * audioGain;
			// Organic drift layered on top
			vec2 drift = vec2(
				sin(t * 1.3 + fi * 2.1) * cos(t * 0.7 + fi * 1.4),
				cos(t * 1.1 + fi * 1.7) * sin(t * 0.9 + fi * 2.3)
			) * driftAmount;
			// Audio also pushes drift harder
			drift *= (1.0 + fftVal * 2.0);
			verts[i] = dv[i] * disp + drift;
		}

	} else {
		// Hand tracking
		verts[0] = (v0 - 0.5) * 2.0;
		verts[1] = (v1 - 0.5) * 2.0;
		verts[2] = (v2 - 0.5) * 2.0;
		verts[3] = (v3 - 0.5) * 2.0;
		verts[4] = (v4 - 0.5) * 2.0;
		verts[5] = (v5 - 0.5) * 2.0;
		verts[6] = (v6 - 0.5) * 2.0;
		verts[7] = (v7 - 0.5) * 2.0;
		verts[8] = (v8 - 0.5) * 2.0;
		verts[9] = (v9 - 0.5) * 2.0;
	}

	// ─── Draw all lines ───
	float brightness = 0.0;

	// Perimeter edges (always full brightness)
	brightness += lineBrightness(p, verts[0], verts[1], lineWidth, glowAmount);
	brightness += lineBrightness(p, verts[1], verts[2], lineWidth, glowAmount);
	brightness += lineBrightness(p, verts[2], verts[3], lineWidth, glowAmount);
	brightness += lineBrightness(p, verts[3], verts[4], lineWidth, glowAmount);
	brightness += lineBrightness(p, verts[4], verts[5], lineWidth, glowAmount);
	brightness += lineBrightness(p, verts[5], verts[6], lineWidth, glowAmount);
	brightness += lineBrightness(p, verts[6], verts[7], lineWidth, glowAmount);
	brightness += lineBrightness(p, verts[7], verts[8], lineWidth, glowAmount);
	brightness += lineBrightness(p, verts[8], verts[9], lineWidth, glowAmount);
	brightness += lineBrightness(p, verts[9], verts[0], lineWidth, glowAmount);

	// Inner diagonals — skip-2 connections (pentagram-like)
	float iw = lineWidth * 0.7;
	float ig = glowAmount * 0.6;
	float inner = innerLines;
	brightness += lineBrightness(p, verts[0], verts[2], iw, ig) * inner;
	brightness += lineBrightness(p, verts[1], verts[3], iw, ig) * inner;
	brightness += lineBrightness(p, verts[2], verts[4], iw, ig) * inner;
	brightness += lineBrightness(p, verts[3], verts[5], iw, ig) * inner;
	brightness += lineBrightness(p, verts[4], verts[6], iw, ig) * inner;
	brightness += lineBrightness(p, verts[5], verts[7], iw, ig) * inner;
	brightness += lineBrightness(p, verts[6], verts[8], iw, ig) * inner;
	brightness += lineBrightness(p, verts[7], verts[9], iw, ig) * inner;
	brightness += lineBrightness(p, verts[8], verts[0], iw, ig) * inner;
	brightness += lineBrightness(p, verts[9], verts[1], iw, ig) * inner;

	// Inner diagonals — skip-3 (deeper star)
	float dw = lineWidth * 0.5;
	float dg = glowAmount * 0.4;
	brightness += lineBrightness(p, verts[0], verts[3], dw, dg) * inner;
	brightness += lineBrightness(p, verts[1], verts[4], dw, dg) * inner;
	brightness += lineBrightness(p, verts[2], verts[5], dw, dg) * inner;
	brightness += lineBrightness(p, verts[3], verts[6], dw, dg) * inner;
	brightness += lineBrightness(p, verts[4], verts[7], dw, dg) * inner;
	brightness += lineBrightness(p, verts[5], verts[8], dw, dg) * inner;
	brightness += lineBrightness(p, verts[6], verts[9], dw, dg) * inner;
	brightness += lineBrightness(p, verts[7], verts[0], dw, dg) * inner;
	brightness += lineBrightness(p, verts[8], verts[1], dw, dg) * inner;
	brightness += lineBrightness(p, verts[9], verts[2], dw, dg) * inner;

	// Cross diagonals — skip-4 (opposite connections)
	float cw = lineWidth * 0.35;
	float cg = glowAmount * 0.25;
	brightness += lineBrightness(p, verts[0], verts[4], cw, cg) * inner;
	brightness += lineBrightness(p, verts[1], verts[5], cw, cg) * inner;
	brightness += lineBrightness(p, verts[2], verts[6], cw, cg) * inner;
	brightness += lineBrightness(p, verts[3], verts[7], cw, cg) * inner;
	brightness += lineBrightness(p, verts[4], verts[8], cw, cg) * inner;
	brightness += lineBrightness(p, verts[5], verts[9], cw, cg) * inner;
	brightness += lineBrightness(p, verts[6], verts[0], cw, cg) * inner;
	brightness += lineBrightness(p, verts[7], verts[1], cw, cg) * inner;
	brightness += lineBrightness(p, verts[8], verts[2], cw, cg) * inner;
	brightness += lineBrightness(p, verts[9], verts[3], cw, cg) * inner;

	// Vertex dots — white with glow
	for (int i = 0; i < N; i++) {
		float d = length(p - verts[i]);
		brightness += smoothstep(0.008, 0.002, d) * 1.2;
		brightness += clamp(0.0006 / (d + 0.0005), 0.0, 0.5);
	}

	// Center dot
	brightness += smoothstep(0.005, 0.001, length(p)) * 0.5;

	// Final: white on dark
	vec3 finalColor = bgColor.rgb + vec3(brightness);

	gl_FragColor = vec4(finalColor, 1.0);
}

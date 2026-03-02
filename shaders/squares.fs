/*{
    "DESCRIPTION": "Cellular/Voronoi noise with moving centers",
    "CREDIT": "Based on @patriciogv — The Book of Shaders",
    "ISFVSN": "2",
    "CATEGORIES": ["Generator"],
    "INPUTS": [
        {
            "NAME": "scale",
            "LABEL": "Scale",
            "TYPE": "float",
            "DEFAULT": 3.0,
            "MIN": 1.0,
            "MAX": 20.0
        },
        {
            "NAME": "speed",
            "LABEL": "Speed",
            "TYPE": "float",
            "DEFAULT": 0.5,
            "MIN": 0.0,
            "MAX": 3.0
        },
        {
            "NAME": "moveAmount",
            "LABEL": "Movement",
            "TYPE": "float",
            "DEFAULT": 0.5,
            "MIN": 0.0,
            "MAX": 1.0
        },
        {
            "NAME": "moveFrequency",
            "LABEL": "Move Freq",
            "TYPE": "float",
            "DEFAULT": 1.0,
            "MIN": 0.1,
            "MAX": 5.0
        },
        {
            "NAME": "distanceField",
            "LABEL": "Distance",
            "TYPE": "float",
            "DEFAULT": 1.0,
            "MIN": 0.0,
            "MAX": 2.0
        },
        {
            "NAME": "showCenters",
            "LABEL": "Show Centers",
            "TYPE": "bool",
            "DEFAULT": false
        },
        {
            "NAME": "centerSize",
            "LABEL": "Center Size",
            "TYPE": "float",
            "DEFAULT": 0.02,
            "MIN": 0.01,
            "MAX": 0.2
        },
        {
            "NAME": "showIsolines",
            "LABEL": "Isolines",
            "TYPE": "bool",
            "DEFAULT": false
        },
        {
            "NAME": "isolineFrequency",
            "LABEL": "Iso Freq",
            "TYPE": "float",
            "DEFAULT": 27.0,
            "MIN": 5.0,
            "MAX": 50.0
        },
        {
            "NAME": "isolineThreshold",
            "LABEL": "Iso Threshold",
            "TYPE": "float",
            "DEFAULT": 0.7,
            "MIN": 0.1,
            "MAX": 0.9
        },
        {
            "NAME": "invert",
            "LABEL": "Invert",
            "TYPE": "bool",
            "DEFAULT": true
        },
        {
            "NAME": "backgroundColor",
            "LABEL": "BG Color",
            "TYPE": "color",
            "DEFAULT": [0.0, 0.0, 0.0, 1.0]
        },
        {
            "NAME": "foregroundColor",
            "LABEL": "FG Color",
            "TYPE": "color",
            "DEFAULT": [0.91, 0.25, 0.34, 1.0]
        }
    ]
}*/

vec2 random2(vec2 p) {
    return fract(sin(vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)))) * 43758.5453);
}

float hash(vec2 p) {
    p = fract(p * vec2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return fract(p.x * p.y);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    f = f * f * (3.0 - 2.0 * f);

    float a = hash(i);
    float b = hash(i + vec2(1.0, 0.0));
    float c = hash(i + vec2(0.0, 1.0));
    float d = hash(i + vec2(1.0, 1.0));

    return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

void main() {
    vec2 st = isf_FragNormCoord.xy;
    st.x *= RENDERSIZE.x / RENDERSIZE.y;

    vec3 color = backgroundColor.rgb;

    st *= scale;

    vec2 i_st = floor(st);
    vec2 f_st = fract(st);

    vec2 point = random2(i_st);

    float time = TIME * speed;
    vec2 noiseOffset = vec2(
        noise(i_st + vec2(time * moveFrequency, 0.0)),
        noise(i_st + vec2(0.0, time * moveFrequency))
    );

    point += (noiseOffset - 0.5) * moveAmount * (1.0 + audioBass * 3.0);

    vec2 diff = point - f_st;
    float dist = length(diff);

    float field = dist * distanceField * (1.0 + audioLevel);

    if (invert) {
        field = 1.0 - field;
    }

    color = mix(backgroundColor.rgb, foregroundColor.rgb, field);

    if (showCenters) {
        float center = 1.0 - step(centerSize, dist);
        color = mix(color, foregroundColor.rgb, center);
    }

    if (showIsolines) {
        float isolines = step(isolineThreshold, abs(sin(isolineFrequency * dist))) * 0.5;
        color -= isolines;
    }

    gl_FragColor = vec4(color, 1.0);
}

/*{
    "CATEGORIES": ["Filter", "Generator"],
    "CREDIT": "Florian Berger (flockaroo) — Shadertoy MsGSRd",
    "DESCRIPTION": "Single-pass computational fluid dynamics with specular lighting",
    "INPUTS": [
        {
            "NAME": "inputImage",
            "TYPE": "image"
        },
        {
            "NAME": "inputImageAmount",
            "LABEL": "Input image amount",
            "TYPE": "float",
            "DEFAULT": 0.01,
            "MIN": 0,
            "MAX": 1
        },
        {
            "NAME": "fluidSpeed",
            "LABEL": "Fluid speed",
            "TYPE": "float",
            "DEFAULT": 2,
            "MIN": 0,
            "MAX": 10
        },
        {
            "NAME": "fluidHeight",
            "LABEL": "Fluid height",
            "TYPE": "float",
            "DEFAULT": 650,
            "MIN": 0,
            "MAX": 1000
        },
        {
            "NAME": "spread",
            "LABEL": "Spread",
            "TYPE": "float",
            "DEFAULT": 1,
            "MIN": 1,
            "MAX": 7
        },
        {
            "NAME": "specularReflectionAmount",
            "LABEL": "Specular",
            "TYPE": "float",
            "DEFAULT": 1,
            "MIN": 0,
            "MAX": 1
        },
        {
            "NAME": "motorLocation",
            "LABEL": "Motor location",
            "TYPE": "point2D",
            "DEFAULT": [0.5, 0.5],
            "MIN": [0, 0],
            "MAX": [1, 1]
        },
        {
            "NAME": "motorSize",
            "LABEL": "Motor size",
            "TYPE": "float",
            "DEFAULT": 0.01,
            "MIN": 0,
            "MAX": 1
        },
        {
            "NAME": "motorAttenuation",
            "LABEL": "Motor attenuation",
            "TYPE": "float",
            "DEFAULT": 0.3,
            "MIN": 0,
            "MAX": 1
        },
        {
            "NAME": "dripSize",
            "LABEL": "Drip size",
            "TYPE": "float",
            "DEFAULT": 0.7,
            "MIN": 0,
            "MAX": 1
        },
        {
            "NAME": "lightRadius",
            "LABEL": "Light distance",
            "TYPE": "float",
            "DEFAULT": 2.4494897428,
            "MIN": 0,
            "MAX": 10
        },
        {
            "NAME": "lightPhi",
            "LABEL": "Light phi",
            "TYPE": "float",
            "DEFAULT": 35.2643896828,
            "MIN": 0,
            "MAX": 180
        },
        {
            "NAME": "lightTheta",
            "LABEL": "Light theta",
            "TYPE": "float",
            "DEFAULT": 45,
            "MIN": 0,
            "MAX": 360
        },
        {
            "NAME": "agitation",
            "LABEL": "Agitation",
            "TYPE": "float",
            "DEFAULT": 2,
            "MIN": 0,
            "MAX": 10
        }
    ],
    "ISFVSN": "2",
    "PASSES": [
        {
            "TARGET": "mainPass",
            "PERSISTENT": true
        },
        {}
    ]
}*/

#define PI 3.1415926535897932384626433832795
#define TWO_PI 6.2831853071795864769252867665590
#define DEG2RAD (PI / 180.0)

vec2 polar2cart(in vec2 polar) {
    return vec2(cos(polar.x), sin(polar.x)) * polar.y;
}

vec3 polar2cart(in float r, in float phi, in float theta) {
    float x = r * cos(theta) * sin(phi);
    float y = r * sin(theta) * sin(phi);
    float z = r * cos(phi);
    return vec3(x, y, z);
}

mat2 rotate2d(const in float r) {
    float c = cos(r);
    float s = sin(r);
    return mat2(c, s, -s, c);
}

float hash11(float p) {
    p = fract(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return fract(p);
}

void main() {
    vec2 pos = gl_FragCoord.xy;
    vec2 inverseSize = 1.0 / RENDERSIZE;
    vec2 uv = pos * inverseSize;

    if (PASSINDEX == 0) {
        int RotNum = 2 * int(agitation) + 1;
        float ang = TWO_PI / float(RotNum);
        mat2 m = rotate2d(ang);

        float rnd = hash11(TIME / RENDERSIZE.x);
        vec2 b = polar2cart(vec2(ang * rnd, 1));
        vec2 v = vec2(0);

        float bbMax = dripSize * RENDERSIZE.y;
        bbMax *= bbMax;

        for (int l = 0; l < 20; l++) {
            if (dot(b, b) > bbMax) break;

            vec2 p = b;

            for (int i = 0; i < 5; i++) {
                vec2 pos_plus_p = pos + p;
                vec2 rotated_b = b;
                float rotated_b_magnitude_squared = dot(rotated_b, rotated_b);

                float rot = 0.0;
                for (int j = 0; j < 5; j++) {
                    rot += dot(
                        IMG_NORM_PIXEL(mainPass, fract((pos_plus_p + rotated_b) / RENDERSIZE)).xy - vec2(0.5),
                        rotated_b.yx * vec2(1, -1)
                    );
                    rotated_b = m * rotated_b;
                }
                float rotation = rot / float(RotNum) / rotated_b_magnitude_squared;

                v += p.yx * rotation;
                p = m * p;
            }

            b *= 2.0;
        }

        gl_FragColor = IMG_NORM_PIXEL(mainPass, fract((pos + fluidSpeed * vec2(-1, 1) * v) / RENDERSIZE));

        // Motor
        vec2 scr = 2.0 * (uv - motorLocation);
        gl_FragColor.xy += motorSize * (1.0 + audioBass * 5.0) * scr / (10.0 * dot(scr, scr) + motorAttenuation);

        gl_FragColor = (1.0 - inputImageAmount) * gl_FragColor + inputImageAmount * IMG_PIXEL(inputImage, pos);

        if (FRAMEINDEX < 5) {
            gl_FragColor = IMG_PIXEL(inputImage, pos);
        }
    }
    else {
        vec2 d = vec2(inverseSize.y, 0);
        vec3 n = vec3(
            (length(IMG_NORM_PIXEL(mainPass, uv + d.xy).xyz) - length(IMG_NORM_PIXEL(mainPass, uv - d.xy).xyz)) * RENDERSIZE.y,
            (length(IMG_NORM_PIXEL(mainPass, uv + d.yx).xyz) - length(IMG_NORM_PIXEL(mainPass, uv - d.yx).xyz)) * RENDERSIZE.y,
            1000.0 - fluidHeight
        );

        n = normalize(n);

        vec3 light = normalize(polar2cart(lightRadius, lightPhi * DEG2RAD, lightTheta * DEG2RAD));
        float diff = clamp(dot(n, light), 0.5, 1.0);
        float spec = clamp(dot(reflect(light, n), vec3(0, 0, -1)), 0.0, 1.0);
        spec = pow(spec, 36.0) * 2.5;

        gl_FragColor = IMG_NORM_PIXEL(mainPass, uv) * vec4(diff) + specularReflectionAmount * vec4(spec);
    }
}

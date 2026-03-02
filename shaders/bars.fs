/*{
    "DESCRIPTION": "Gradient Bars",
    "CREDIT": "Joshua Batty",
    "ISFVSN": "2",
    "CATEGORIES": [
        "Generator"
    ],
    "INPUTS": [
        {
            "NAME": "easing_type",
            "TYPE": "long",
            "DEFAULT": 0,
            "VALUES": [
                0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10,
                11, 12, 13, 14, 15, 16, 17, 18, 19, 20,
                21, 22, 23, 24, 25, 26, 27, 28, 29
            ],
            "LABELS": [
                "Ease In Sine", "Ease Out Sine", "Ease InOut Sine",
                "Ease In Quad", "Ease Out Quad", "Ease InOut Quad",
                "Ease In Cubic", "Ease Out Cubic", "Ease InOut Cubic",
                "Ease In Quart", "Ease Out Quart", "Ease InOut Quart",
                "Ease In Quint", "Ease Out Quint", "Ease InOut Quint",
                "Ease In Expo", "Ease Out Expo", "Ease InOut Expo",
                "Ease In Circ", "Ease Out Circ", "Ease InOut Circ",
                "Ease In Back", "Ease Out Back", "Ease InOut Back",
                "Ease In Elastic", "Ease Out Elastic", "Ease InOut Elastic",
                "Ease In Bounce", "Ease Out Bounce", "Ease InOut Bounce"
            ],
            "LABEL": "Easing Type"
        },
        {
            "NAME": "gradient_pow",
            "TYPE": "float",
            "DEFAULT": 0.2,
            "MIN": 0.0,
            "MAX": 1.0,
            "LABEL": "Gradient Power"
        },
        {
            "NAME": "balance",
            "TYPE": "float",
            "DEFAULT": 0.15,
            "MIN": 0.0,
            "MAX": 1.0,
            "LABEL": "Balance"
        },
        {
            "NAME": "speed",
            "TYPE": "float",
            "DEFAULT": 0.1,
            "MIN": 0.0,
            "MAX": 1.0,
            "LABEL": "Speed"
        },
        {
            "NAME": "invert_speed",
            "TYPE": "float",
            "DEFAULT": 0.2,
            "MIN": 0.0,
            "MAX": 1.0,
            "LABEL": "Invert Speed"
        },
        {
            "NAME": "offset",
            "TYPE": "float",
            "DEFAULT": 4.0,
            "MIN": 0.0,
            "MAX": 16.0,
            "LABEL": "Offset"
        },
        {
            "NAME": "use_odd_dirs",
            "TYPE": "float",
            "DEFAULT": 0.0,
            "MIN": 0.0,
            "MAX": 1.0,
            "LABEL": "Use Odd Directions"
        },
        {
            "NAME": "phase_iter",
            "TYPE": "float",
            "DEFAULT": 2.0,
            "MIN": 1.0,
            "MAX": 16.0,
            "LABEL": "Phase Iterations"
        },
        {
            "NAME": "num_columns",
            "TYPE": "float",
            "DEFAULT": 8.0,
            "MIN": 1.0,
            "MAX": 32.0,
            "LABEL": "Number of Columns"
        },
        {
            "NAME": "x_iter",
            "TYPE": "float",
            "DEFAULT": 2.0,
            "MIN": 1.0,
            "MAX": 2.0,
            "LABEL": "X Iterations"
        },
        {
            "NAME": "use_columns",
            "TYPE": "bool",
            "DEFAULT": 1,
            "LABEL": "Columns / Rows"
        }
    ]
}*/

#ifdef GL_ES
precision highp float;
#endif

const float PI = 3.14159265358979;

float easeInSine(float x) {
    return 1. - cos((x * PI) / 2.);
}

float easeOutSine(float x) {
    return sin((x * PI) / 2.);
}

float easeInOutSine(float x) {
    return -(cos(PI * x) - 1.) / 2.;
}

float easeInCubic(float x) {
    return x * x * x;
}

float easeOutCubic(float x) {
    return 1. - pow(1. - x, 3.);
}

float easeInOutCubic(float x) {
    return x < .5 ? 4. * x * x * x : 1. - pow(-2. * x + 2., 3.) / 2.;
}

float easeInQuint(float x) {
    return x * x * x * x * x;
}

float easeOutQuint(float x) {
    return 1. - pow(1. - x, 5.);
}

float easeInOutQuint(float x) {
    return x < .5 ? 16. * x * x * x * x * x : 1. - pow(-2. * x + 2., 5.) / 2.;
}

float easeInCirc(float x) {
    return 1. - sqrt(abs(1. - pow(x, 2.)));
}

float easeOutCirc(float x) {
    return sqrt(abs(1. - pow(x - 1., 2.)));
}

float easeInOutCirc(float x) {
    return x < .5
      ? (1. - sqrt(1. - pow(2. * x, 2.))) / 2.
      : (sqrt(1. - pow(-2. * x + 2., 2.)) + 1.) / 2.;
}

float easeInElastic(float x) {
    float c4 = (2. * PI) / 3.;
    return x == 0.
      ? 0.
      : x == 1.
      ? 1.
      : -pow(2., 10. * x - 10.) * sin((x * 10. - 10.75) * c4);
}

float easeOutElastic(float x) {
    float c4 = (2. * PI) / 3.;
    return x == 0.
      ? 0.
      : x == 1.
      ? 1.
      : pow(2., -10. * x) * sin((x * 10. - .75) * c4) + 1.;
}

float easeInOutElastic(float x) {
    float c5 = (2. * PI) / 4.5;
    return x == 0.
      ? 0.
      : x == 1.
      ? 1.
      : x < .5
      ? -(pow(2., 20. * x - 10.) * sin((20. * x - 11.125) * c5)) / 2.
      : (pow(2., -20. * x + 10.) * sin((20. * x - 11.125) * c5)) / 2. + 1.;
}

float easeInQuad(float x) {
    return x * x;
}

float easeOutQuad(float x) {
    return 1. - (1. - x) * (1. - x);
}

float easeInOutQuad(float x) {
    return x < .5 ? 2. * x * x : 1. - pow(-2. * x + 2., 2.) / 2.;
}

float easeInQuart(float x) {
    return x * x * x * x;
}

float easeOutQuart(float x) {
    return 1. - pow(1. - x, 4.);
}

float easeInOutQuart(float x) {
    return x < .5 ? 8. * x * x * x * x : 1. - pow(-2. * x + 2., 4.) / 2.;
}

float easeInExpo(float x) {
    return x == 0. ? 0. : pow(2., 10. * x - 10.);
}

float easeOutExpo(float x) {
    return x == 1. ? 1. : 1. - pow(2., -10. * x);
}

float easeInOutExpo(float x) {
    return x == 0.
      ? 0.
      : x == 1.
      ? 1.
      : x < .5 ? pow(2., 20. * x - 10.) / 2.
      : (2. - pow(2., -20. * x + 10.)) / 2.;
}

float easeInBack(float x) {
    float c1 = 1.70158;
    float c3 = c1 + 1.;
    return c3 * x * x * x - c1 * x * x;
}

float easeOutBack(float x) {
    float c1 = 1.70158;
    float c3 = c1 + 1.;
    return 1. + c3 * pow(x - 1., 3.) + c1 * pow(x - 1., 2.);
}

float easeInOutBack(float x) {
    float c1 = 1.70158;
    float c2 = c1 * 1.525;
    return x < .5
      ? (pow(2. * x, 2.) * ((c2 + 1.) * 2. * x - c2)) / 2.
      : (pow(2. * x - 2., 2.) * ((c2 + 1.) * (x * 2. - 2.) + c2) + 2.) / 2.;
}

float easeOutBounce(float x) {
    float n1 = 7.5625;
    float d1 = 2.75;
    if (x < 1. / d1) {
        return n1 * x * x;
    } else if (x < 2. / d1) {
        return n1 * (x -= 1.5 / d1) * x + 0.75;
    } else if (x < 2.5 / d1) {
        return n1 * (x -= 2.25 / d1) * x + 0.9375;
    } else {
        return n1 * (x -= 2.625 / d1) * x + 0.984375;
    }
}

float easeInBounce(float x) {
    return 1. - easeOutBounce(1. - x);
}

float easeInOutBounce(float x) {
    return x < .5
      ? (1. - easeOutBounce(1. - 2. * x)) / 2.
      : (1. + easeOutBounce(2. * x - 1.)) / 2.;
}

float fun(float phase, float id) {
    int i = int(id);
    if (i == 0) return easeInSine(phase);
    else if (i == 1) return easeOutSine(phase);
    else if (i == 2) return easeInOutSine(phase);
    else if (i == 3) return easeInQuad(phase);
    else if (i == 4) return easeOutQuad(phase);
    else if (i == 5) return easeInOutQuad(phase);
    else if (i == 6) return easeInCubic(phase);
    else if (i == 7) return easeOutCubic(phase);
    else if (i == 8) return easeInOutCubic(phase);
    else if (i == 9) return easeInQuart(phase);
    else if (i == 10) return easeOutQuart(phase);
    else if (i == 11) return easeInOutQuart(phase);
    else if (i == 12) return easeInQuint(phase);
    else if (i == 13) return easeOutQuint(phase);
    else if (i == 14) return easeInOutQuint(phase);
    else if (i == 15) return easeInExpo(phase);
    else if (i == 16) return easeOutExpo(phase);
    else if (i == 17) return easeInOutExpo(phase);
    else if (i == 18) return easeInCirc(phase);
    else if (i == 19) return easeOutCirc(phase);
    else if (i == 20) return easeInOutCirc(phase);
    else if (i == 21) return easeInBack(phase);
    else if (i == 22) return easeOutBack(phase);
    else if (i == 23) return easeInOutBack(phase);
    else if (i == 24) return easeInElastic(phase);
    else if (i == 25) return easeOutElastic(phase);
    else if (i == 26) return easeInOutElastic(phase);
    else if (i == 27) return easeInBounce(phase);
    else if (i == 28) return easeOutBounce(phase);
    else if (i == 29) return easeInOutBounce(phase);
    else return 0.0;
}

float adjust_balance(float value, float b) {
    float gamma = exp(mix(-2.0, 2.0, b));
    return pow(value, gamma);
}

void main() {
    vec2 uv = isf_FragNormCoord.xy;
    float primary = use_columns ? uv.x : uv.y;
    float secondary = use_columns ? uv.y : uv.x;

    float mirrored_primary = abs(primary - 0.5) * x_iter;
    float stripe_index = ceil(mirrored_primary * num_columns);

    float phase_offset = stripe_index * (1.0 / (num_columns * max(offset, 0.001)));
    float phase = fract(TIME * speed * (1.0 + audioBass * 2.0) + phase_offset);
    float lfo = fun(phase, easing_type) * phase_iter - (phase_iter / 2.0);

    bool is_even_stripe = mod(stripe_index, 2.0) < 1.0;
    float secondary_adjusted = is_even_stripe ? secondary : 1.0 - secondary;
    secondary = mix(secondary, secondary_adjusted, use_odd_dirs);

    float gradient = pow(secondary, gradient_pow * (1.0 - audioLevel * 0.5));
    gradient = mix(gradient, 1.0 - gradient, 0.5 + sin(TIME * invert_speed) * 0.5);

    float animated_coord = fract(lfo + gradient);
    float col = 0.5 + 0.5 * sin(animated_coord * 6.28318530718);
    col = adjust_balance(col, balance);

    gl_FragColor = vec4(vec3(1.0 - col), 1.0);
}

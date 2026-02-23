
/*{
	"DESCRIPTION": "Gradient Flow forked from https://www.shadertoy.com/view/wdyczG",
	"CREDIT": "",
	"ISFVSN": "2",
	"CATEGORIES": [
		"XXX"
	],
	"INPUTS": [
		{
			"NAME": "Color_A",
			"TYPE": "color",
			"DEFAULT": [
				0.957, 0.804, 0.623,
				1.0
			]
		},{
			"NAME": "Color_B",
			"TYPE": "color",
			"DEFAULT": [
				0.192, 0.384, 0.933,
				1.0
			]
		},{
			"NAME": "Color_C",
			"TYPE": "color",
			"DEFAULT": [
				0.910, 0.510, 0.8,
				1.0
			]
		}, {
			"NAME": "Color_D",
			"TYPE": "color",
			"DEFAULT": [
				0.350, 0.71, 0.953,
				1.0
			]
		}, {
			"NAME": "Frequency",
			"TYPE": "float",
			"DEFAULT": 5.0,
			"MIN": 0.0,
			"MAX": 100.0
		}, {
			"NAME": "Amplitude",
			"TYPE": "float",
			"DEFAULT": 30.0,
			"MIN": 0.0,
			"MAX": 100.0
		}, {
			"NAME": "Speed",
			"TYPE": "float",
			"DEFAULT": 2.0,
			"MIN": 0.0,
			"MAX": 10.0
		}
	]
}*/

#define S(a,b,t) smoothstep(a,b,t)

mat2 Rot(float a)
{
    float s = sin(a);
    float c = cos(a);
    return mat2(c, -s, s, c);
}


// Created by inigo quilez - iq/2014
// License Creative Commons Attribution-NonCommercial-ShareAlike 3.0 Unported License.
vec2 hash( vec2 p )
{
    p = vec2( dot(p,vec2(2127.1,81.17)), dot(p,vec2(1269.5,283.37)) );
	return fract(sin(p)*43758.5453);
}

float noise( in vec2 p )
{
    vec2 i = floor( p );
    vec2 f = fract( p );
	
	vec2 u = f*f*(3.0-2.0*f);

    float n = mix( mix( dot( -1.0+2.0*hash( i + vec2(0.0,0.0) ), f - vec2(0.0,0.0) ), 
                        dot( -1.0+2.0*hash( i + vec2(1.0,0.0) ), f - vec2(1.0,0.0) ), u.x),
                   mix( dot( -1.0+2.0*hash( i + vec2(0.0,1.0) ), f - vec2(0.0,1.0) ), 
                        dot( -1.0+2.0*hash( i + vec2(1.0,1.0) ), f - vec2(1.0,1.0) ), u.x), u.y);
	return 0.5 + 0.5*n;
}


void main()	{
    vec2 uv = gl_FragCoord.xy/RENDERSIZE.xy;
    float ratio = RENDERSIZE.x / RENDERSIZE.y;

    vec2 tuv = uv;
    tuv -= .5;
    
    float t = TIME;

    // rotate with Noise
    float degree = noise(vec2(t*.1, tuv.x*tuv.y));

    tuv.y *= 1./ratio;
    tuv *= Rot(radians((degree-.5)*720.+180.));
	tuv.y *= ratio;

    
    // Wave warp with sin
    float speed = t * Speed;
    tuv.x += sin(tuv.y*Frequency+speed)/Amplitude;
   	tuv.y += sin(tuv.x*Frequency*1.5+speed)/(Amplitude*.5);
    
    
    // draw the image
    vec3 layer1 = mix(Color_A.rgb, Color_B.rgb, S(-.3, .2, (tuv*Rot(radians(-5.))).x));
    
    vec3 layer2 = mix(Color_C.rgb, Color_D.rgb, S(-.3, .2, (tuv*Rot(radians(-5.))).x));
    
    vec3 finalComp = mix(layer1, layer2, S(.5, -.3, tuv.y));
    
    vec3 col = finalComp;
    
    gl_FragColor = vec4(col,1.0);
}
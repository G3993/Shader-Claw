
/*{
	"DESCRIPTION": "",
	"CREDIT": "",
	"ISFVSN": "2",
	"CATEGORIES": [
		"XXX"
	],
	"INPUTS": [
		{
			"NAME": "inputImage",
			"TYPE": "image"
		},
		{
			"NAME": "baseColor",
			"TYPE": "color",
			"DEFAULT": [
				1.0,
				1.0,
				0.0,
				1.0
			]
		},
		{
			"NAME": "targetColor",
			"TYPE": "color",
			"DEFAULT": [
				1.0,
				0.0,
				0.0,
				1.0
			]
		},
		{
			"NAME": "targetColorPoint",
			"TYPE": "float",
			"DEFAULT": 0.5
		},
		{
			"NAME": "targetColorRange",
			"TYPE": "float",
			"DEFAULT": 0.3
		},
		{
			"NAME": "showGradient",
			"TYPE": "bool",
			"DEFAULT": true
		},
		{
			"NAME": "showSolarized",
			"TYPE": "bool",
			"DEFAULT": false
		}
	],
	"PASSES": [
		{
			"TARGET":"bufferVariableNameA",
			"WIDTH": "$WIDTH/16.0",
			"HEIGHT": "$HEIGHT/16.0"
		},
		{
			"DESCRIPTION": "this empty pass is rendered at the same rez as whatever you are running the ISF filter at- the previous step rendered an image at one-sixteenth the res, so this step ensures that the output is full-size"
		}
	]
	
}*/
vec3 rgb2hsl( in vec3 c ){
  float h = 0.0;
	float s = 0.0;
	float l = 0.0;
	float r = c.r;
	float g = c.g;
	float b = c.b;
	float cMin = min( r, min( g, b ) );
	float cMax = max( r, max( g, b ) );

	l = ( cMax + cMin ) / 2.0;
	if ( cMax > cMin ) {
		float cDelta = cMax - cMin;
        
        //s = l < .05 ? cDelta / ( cMax + cMin ) : cDelta / ( 2.0 - ( cMax + cMin ) ); Original
		s = l < .0 ? cDelta / ( cMax + cMin ) : cDelta / ( 2.0 - ( cMax + cMin ) );
        
		if ( r == cMax ) {
			h = ( g - b ) / cDelta;
		} else if ( g == cMax ) {
			h = 2.0 + ( b - r ) / cDelta;
		} else {
			h = 4.0 + ( r - g ) / cDelta;
		}

		if ( h < 0.0) {
			h += 6.0;
		}
		h = h / 6.0;
	}
	return vec3( h, s, l );
}



vec3 hsl2rgb( in vec3 c )
{
    vec3 rgb = clamp( abs(mod(c.x*6.0+vec3(0.0,4.0,2.0),6.0)-3.0)-1.0, 0.0, 1.0 );

    return c.z + c.y * (rgb-0.5)*(1.0-abs(2.0*c.z-1.0));
}

float solarize(in float baseLuminosity,in float point,in float range){
    float area=clamp(1.-abs(baseLuminosity-point)/range,0.,1.);
    return area;
}

void main()	{
	vec4		inputPixelColor;
	//	both of these are the same
	inputPixelColor = IMG_THIS_PIXEL(inputImage);
// 	inputPixelColor = IMG_PIXEL(inputImage, gl_FragCoord.xy);
	
	//	both of these are also the same
// 	inputPixelColor = IMG_NORM_PIXEL(inputImage, isf_FragNormCoord.xy);
// 	inputPixelColor = IMG_THIS_NORM_PIXEL(inputImage);
	
    float l=(inputPixelColor.r+inputPixelColor.g+inputPixelColor.b)/3.;
    
    //gradientを表示する場合、上の方に表示する
    float gradientColor=mix(l,isf_FragNormCoord.x,step(0.9,isf_FragNormCoord.y));
    l=mix(l,gradientColor,float(showGradient));
    
	vec3 baseHsl=rgb2hsl(baseColor.rgb);
	vec3 baseColored=hsl2rgb(vec3(baseHsl.xy,l));
	vec3 targetHsl=rgb2hsl(targetColor.rgb);
	vec3 targetColored=hsl2rgb(vec3(targetHsl.xy,l));
	
	float solarized=solarize(l,targetColorPoint,targetColorRange);
	vec4 fullColor=vec4(mix(baseColored,targetColored,solarized),1.);
	vec4 solarizedColor=vec4(solarized,solarized,solarized,1.);
// 	gl_FragColor=vec4(baseColored,1.);
	gl_FragColor=mix(fullColor,solarizedColor,float(showSolarized));
}

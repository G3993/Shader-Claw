/*{
	"DESCRIPTION": "Moves a multicolor (up to 5) gradient across the view from and to off view",
	"CREDIT": "by Clutchplate",
	"ISFVSN": "2.0",
	"CATEGORIES": [
		"TEST-GLSL"
	],
	"INPUTS": [
		{
			"LABEL": "Color",
			"NAME": "color1",
			"TYPE": "color",
			"DEFAULT": [0.0,0.0,1.0]
		},
		{
			"LABEL": "Color",
			"NAME": "color2",
			"TYPE": "color",
			"DEFAULT": [1.0,0.0,1.0]
		},
		{
			"LABEL": "Color",
			"NAME": "color3",
			"TYPE": "color",
			"DEFAULT": [1.0,1.0,0.0]
		},
		{
			"LABEL": "Color",
			"NAME": "color4",
			"TYPE": "color",
			"DEFAULT": [0.0,1.0,1.0]
		},
		{
			"LABEL": "Color",
			"NAME": "color5",
			"TYPE": "color",
			"DEFAULT": [0.0,1.0,0.0]
		},
		{
			"LABEL": "Color",
			"NAME": "color6",
			"TYPE": "color",
			"DEFAULT": [0.0,0.0,0.5]
		},
		{
			"LABEL": "Color",
			"NAME": "color7",
			"TYPE": "color",
			"DEFAULT": [0.5,0.4,0.0]
		},
		{
			"LABEL": "Color",
			"NAME": "color8",
			"TYPE": "color",
			"DEFAULT": [0.2,0.6,0.5]
		},
		{
			"LABEL": "Thickness",
			"NAME": "thickness",
			"TYPE": "float",
			"DEFAULT": 1.0,
			"MIN": 0.01,
			"MAX": 1.0
		},
		{
			"LABEL": "Falloff",
			"NAME": "falloff",
			"TYPE": "float",
			"DEFAULT": 0.2,
			"MIN": 0.001,
			"MAX": 0.3
		},
		{
			"LABEL": "Rotation",
			"NAME": "angle",
			"TYPE": "float",
			"DEFAULT": 0.0,
			"MIN": 0.0,
			"MAX": 360.0
		},
		{
			"LABEL": "Cycles",
			"NAME": "cycles",
			"TYPE": "float",
			"DEFAULT": 1.0,
			"MIN": 1.0,
			"MAX": 50.0
		},
		{
			"LABEL": "Time",
			"NAME": "efftime",
			"TYPE": "float",
			"DEFAULT": 0.5,
			"MIN": 0.0,
			"MAX": 1.0
		},
		{
			"LABEL": "Start Offscreen",
			"NAME": "offscreen",
			"TYPE": "bool",
			"DEFAULT": true
		}
	]
}*/
int NUMCOLORS = 3;
float  reps = 0.5;
float XL_DURATION = 4.0;
vec4 black = vec4(0.0, 0.0, 0.0, 1.0);

float frac(float f)
{
    return f<0.0 ? 1.0-(-f-floor(-f))  : (f-floor(f));
}

/*
float thickness  = 0.2;
bool dual = true;


// loc is -0.5 to 0.5
float mixcolor(float loc){
    float MYTIME=(TIME-floor(TIME));
    float normTimeInEffect = frac(MYTIME*cycles/XL_DURATION) - 0.5;
    //float actTime = barPos/2.0; //2.0*((barPos/2.0)-0.5);
    // actTime maps to -0.5 to 0.5 now

    float actTime = normTimeInEffect;

    float midc=(1.0-2.0*abs((loc-actTime)*(1.0/thickness)));
    return midc;
}
*/

vec4 mymix(vec4 x, vec4 y, float a) {
    return a * x + (1.0-a) * y;
}

void main()	{
    float duration = XL_DURATION;
    float time = efftime*XL_DURATION;
    
    float MYTIME=(time/XL_DURATION-floor(time/XL_DURATION));
    MYTIME = frac(MYTIME*cycles);
    
    if (offscreen) {
        time = MYTIME * (1.0+thickness) - 1.0;
    } 
    else
    {
        time=frac(MYTIME);
    }
//    float normTimeInEffect = frac(MYTIME*cycles);
    float normTimeInEffect = time;
    
    float p2x = isf_FragNormCoord.x-0.5;
    float p2y = isf_FragNormCoord.y-0.5;
    float rads = angle*3.1415927/180.0;
    float px = p2x*sin(rads)+p2y*cos(rads);
    float py = p2x*cos(rads)-p2y*sin(rads);    

    float loc = (normTimeInEffect+px+0.5) / thickness;

    int numColors= NUMCOLORS;
    vec4 fragcolor;
    vec4 lastColor = color1;
    if (numColors==2)
    {
         lastColor = color2;
    }
    else if (numColors==3)
    {
         lastColor = color3;
    }
    else if (numColors==4)
    {
         lastColor = color4;
    }
    else if (numColors==5)
    {
         lastColor = color5;
    }
    else if (numColors==6)
    {
         lastColor = color6;
    }
    else if (numColors==7)
    {
         lastColor = color7;
    }
    else if (numColors==8)
    {
         lastColor = color8;
    }
    
    if  ((loc > 1.0) || (loc < 0.0)) 
    {
        fragcolor = black;
    } 
    else if (loc <= falloff)
    {
        fragcolor = mix(black, color1, loc/falloff);
    } 
    else if (loc > 1.0-falloff) 
    {
        fragcolor = mix(lastColor, black, (loc-1.0+falloff)/falloff);
    }
    else 
    {
        loc= (loc - falloff) / (1.0 - falloff * 2.0);
        if (numColors == 1) 
        {
            fragcolor = color1;
        }   
        else if (numColors == 2) 
        {
            fragcolor = mix(color1, color2, loc);
        }
        else if (numColors == 3) 
        {
            if (loc < 0.5) 
            {
                fragcolor = mix(color1, color2, loc * 2.0);
            } 
            else 
            {
                fragcolor = mix(color2, color3, (loc - 0.5) * 2.0);
            }
        }
        else if (numColors == 4) 
        {
            if (loc < 0.33) 
            {
                fragcolor = mix(color1, color2, loc * 3.0);
            } 
            else if (loc < 0.66) 
            {
                fragcolor = mix(color2, color3, (loc - 0.33) * 3.0);
            } 
            else 
            {
                fragcolor = mix(color3, color4, (loc - 0.66) * 3.0);
            }
        }
        else if (numColors == 5) 
        {
            if (loc < 0.25) 
            {
                fragcolor = mix(color1, color2, loc * 4.0);
            } 
            else if (loc < 0.5) 
            {
                fragcolor = mix(color2, color3, (loc - 0.25) * 4.0);
            } 
            else if (loc < 0.75) 
            {
                fragcolor = mix(color3, color4, (loc - 0.5) * 4.0);
            } 
            else 
            {
                fragcolor = mix(color4, color5, (loc - 0.75) * 4.0);
            }
        }
        else if (numColors == 6) 
        {
            if (loc < 0.2) 
            {
                fragcolor = mix(color1, color2, loc * 5.0);
            } 
            else if (loc < 0.4) 
            {
                fragcolor = mix(color2, color3, (loc - 0.2) * 5.0);
            } 
            else if (loc < 0.6) 
            {
                fragcolor = mix(color3, color4, (loc - 0.4) * 5.0);
            } 
            else if (loc < 0.8) 
            {
                fragcolor = mix(color4, color5, (loc - 0.6) * 5.0);
            } 
            else 
            {
                fragcolor = mix(color5, color6, (loc - 0.8) * 5.0);
            }
        }
        else if (numColors == 7) 
        {
            if (loc < 0.1667) 
            {
                fragcolor = mix(color1, color2, loc * 6.0);
            } 
            else if (loc < 0.3333) 
            {
                fragcolor = mix(color2, color3, (loc - 0.1667) * 6.0);
            } 
            else if (loc < 0.5) 
            {
                fragcolor = mix(color3, color4, (loc - 0.3333) * 6.0);
            } 
            else if (loc < 0.6667) 
            {
                fragcolor = mix(color4, color5, (loc - 0.5) * 6.0);
            } 
            else if (loc < 0.8333) 
            {
                fragcolor = mix(color5, color6, (loc - 0.6667) * 6.0);
            } 
            else 
            {
                fragcolor = mix(color6, color7, (loc - 0.8333) * 6.0);
            }
        }
        else if (numColors == 8) 
        {
            if (loc < 0.1429) 
            {
                fragcolor = mix(color1, color2, loc * 7.0);
            } 
            else if (loc < 0.2857) 
            {
                fragcolor = mix(color2, color3, (loc - 0.1429) * 7.0);
            } 
            else if (loc < 0.4286) 
            {
                fragcolor = mix(color3, color4, (loc - 0.2857) * 7.0);
            } 
            else if (loc < 0.5714) 
            {
                fragcolor = mix(color4, color5, (loc - 0.4286) * 7.0);
            } 
            else if (loc < 0.7143) 
            {
                fragcolor = mix(color5, color6, (loc - 0.5714) * 7.0);
            } 
            else if (loc < 0.8571) 
            {
                fragcolor = mix(color6, color7, (loc - 0.7143) * 7.0);
            } 
            else 
            {
                fragcolor = mix(color7, color8, (loc - 0.8571) * 7.0);
            }
        }
    }
    
    gl_FragColor = vec4(fragcolor[0], fragcolor[1],fragcolor[2], 1.0);
}

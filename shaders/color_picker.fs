/*{
    "DESCRIPTION": "Text / Logo Color Picker",
    "CREDIT": "ChatGPT",
    "ISFVSN": "2",
    "CATEGORIES": [ "Color" ],
    "INPUTS": [
        {"NAME":"inputImage","TYPE":"image"},
        {
            "NAME":"color",
            "TYPE":"color",
            "DEFAULT":[1.0,0.0,0.0,1.0]
        },
        {
            "NAME":"intensity",
            "TYPE":"float",
            "DEFAULT":1.0,
            "MIN":0.0,
            "MAX":2.0
        },
        {
            "NAME":"mixAmount",
            "TYPE":"float",
            "DEFAULT":1.0,
            "MIN":0.0,
            "MAX":1.0
        }
    ]
}*/

void main() {
    vec2 uv = isf_FragNormCoord.xy;

    vec4 img = IMG_NORM_PIXEL(inputImage, uv);

    // Apply color tint
    vec3 tinted = img.rgb * color.rgb * intensity;

    // Blend original vs tinted
    vec3 finalColor = mix(img.rgb, tinted, mixAmount);

    gl_FragColor = vec4(finalColor, img.a);
}
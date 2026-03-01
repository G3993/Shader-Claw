/*{
    "DESCRIPTION": "Morphing stained glass window — 3D lead cames, luminous glass panels, dramatic mouse interaction",
    "CATEGORIES": ["Generator"],
    "INPUTS": [
        { "NAME": "cells", "LABEL": "Cells", "TYPE": "float", "DEFAULT": 16.0, "MIN": 3.0, "MAX": 40.0 },
        { "NAME": "speed", "LABEL": "Speed", "TYPE": "float", "DEFAULT": 0.3, "MIN": 0.0, "MAX": 2.0 },
        { "NAME": "borderWidth", "LABEL": "Lead Width", "TYPE": "float", "DEFAULT": 0.05, "MIN": 0.01, "MAX": 0.15 },
        { "NAME": "warp", "LABEL": "Warp", "TYPE": "float", "DEFAULT": 0.6, "MIN": 0.0, "MAX": 2.0 },
        { "NAME": "hueShift", "LABEL": "Hue Shift", "TYPE": "float", "DEFAULT": 0.0, "MIN": 0.0, "MAX": 1.0 },
        { "NAME": "saturation", "LABEL": "Saturation", "TYPE": "float", "DEFAULT": 0.0, "MIN": 0.0, "MAX": 1.0 },
        { "NAME": "brightness", "LABEL": "Brightness", "TYPE": "float", "DEFAULT": 0.7, "MIN": 0.1, "MAX": 1.5 },
        { "NAME": "glassZoom", "LABEL": "Zoom", "TYPE": "float", "DEFAULT": 1.0, "MIN": 0.3, "MAX": 5.0 },
        { "NAME": "drift", "LABEL": "Drift", "TYPE": "float", "DEFAULT": 0.4, "MIN": 0.0, "MAX": 1.5 }
    ]
}*/

vec3 hsv2rgb(vec3 c) {
    vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

vec2 cellHash(float n) {
    return fract(sin(vec2(n * 127.1, n * 269.5)) * 43758.5453);
}

float cellHash1(float n) {
    return fract(sin(n * 113.1) * 43758.5453);
}

void main() {
    vec2 uv = gl_FragCoord.xy / RENDERSIZE.xy;
    float aspect = RENDERSIZE.x / RENDERSIZE.y;
    vec2 p = (uv - 0.5) * vec2(aspect, 1.0) * glassZoom;

    float t = TIME * speed;

    // Domain warp — organic flowing shapes
    vec2 w = p;
    w += warp * vec2(
        sin(p.y * 1.8 + t * 0.7) * 0.25 + sin(p.y * 0.6 + t * 0.3) * 0.15 + sin(p.x * 1.2 - t * 0.5) * 0.1,
        cos(p.x * 1.8 + t * 0.5) * 0.25 + cos(p.x * 0.7 + t * 0.4) * 0.15 + cos(p.y * 1.3 + t * 0.6) * 0.1
    );

    // Mouse — dramatic repulsion field
    vec2 mp = (mousePos - 0.5) * vec2(aspect, 1.0) * glassZoom;
    vec2 toMouse = w - mp;
    float md = dot(toMouse, toMouse) + 0.1;
    w += toMouse * 1.5 / (md * 3.0 + 0.3);

    // Voronoi
    float d1 = 100.0;
    float d2 = 100.0;
    float cId = 0.0;
    vec2 nearest = vec2(0.0);
    vec2 secondNearest = vec2(0.0);
    int nc = int(cells);

    for (int i = 0; i < 40; i++) {
        if (i >= nc) break;
        float fi = float(i);
        vec2 base = cellHash(fi * 17.3) * 2.0 - 1.0;
        base *= glassZoom * 0.55;
        float sx = cellHash1(fi * 31.7);
        float sy = cellHash1(fi * 47.3);
        base += drift * vec2(
            sin(t * (0.4 + sx * 0.4) + fi * 1.3) * 0.35,
            cos(t * (0.35 + sy * 0.5) + fi * 0.9) * 0.35
        );
        float d = length(w - base);
        if (d < d1) {
            d2 = d1; secondNearest = nearest;
            d1 = d; nearest = base; cId = fi;
        } else if (d < d2) {
            d2 = d; secondNearest = base;
        }
    }

    // Edge distance and direction
    float edge = d2 - d1;
    float edgeNorm = clamp(edge / borderWidth, 0.0, 1.0);

    // Direction along the came (perpendicular to the edge bisector)
    vec2 edgeMid = (nearest + secondNearest) * 0.5;
    vec2 edgeVec = normalize(secondNearest - nearest + 0.0001);
    // How far along the came cross-section (0 = center of came, 1 = glass edge)
    float cameCross = edgeNorm;

    // ========== 3D LEAD CAME ==========
    // Cross-section: rounded ridge profile
    // At cameCross=0 (boundary center) the came is at max height
    // At cameCross=1 (glass edge) the came meets the glass surface
    float cameHeight = sqrt(max(1.0 - cameCross * cameCross, 0.0));

    // Surface normal of the came (points outward and upward)
    // X,Y: slopes away from boundary center toward glass
    // Z: upward component from the rounded profile
    float slopeXY = -cameCross / (cameHeight + 0.001);
    vec2 slopeDir = normalize(w - edgeMid + 0.0001);
    vec3 cameNormal = normalize(vec3(slopeDir * slopeXY * 0.8, 1.0));

    // Lighting
    vec3 lightDir = normalize(vec3(-0.3, 0.5, 0.9));
    vec3 viewDir = vec3(0.0, 0.0, 1.0);
    vec3 halfVec = normalize(lightDir + viewDir);

    float diff = max(dot(cameNormal, lightDir), 0.0);
    float spec = pow(max(dot(cameNormal, halfVec), 0.0), 40.0);
    // Fresnel — edges of came catch more light
    float fresnel = pow(1.0 - max(dot(cameNormal, viewDir), 0.0), 3.0);

    // Lead metal: dark pewter/gunmetal base
    vec3 metalBase = vec3(0.06, 0.065, 0.07);
    vec3 metalHighlight = vec3(0.4, 0.42, 0.45);
    vec3 cameColor = metalBase * (0.3 + 0.7 * diff)
                   + metalHighlight * spec * 1.2
                   + metalBase * fresnel * 0.5;

    // Came-to-glass transition
    float cameMask = 1.0 - smoothstep(0.0, 1.0, edgeNorm);

    // ========== GRIP: pinch or mouse click drives stained glass intensity ==========
    float grip = clamp(max(pinchHold, mouseDown), 0.0, 1.0);
    float glassiness = smoothstep(0.0, 0.6, grip); // 0 = metallic, 1 = full stained glass

    // ========== PANEL BASE (metallic → colorful glass) ==========
    float panelTone = fract(cId * 0.618033988749 + hueShift);
    float baseVal = brightness * (0.15 + 0.7 * panelTone);

    // Gradient across each panel
    vec2 gradDir = normalize(vec2(sin(cId * 4.1 + t * 0.3), cos(cId * 3.3 + t * 0.2)));
    vec2 toPixel = w - nearest;
    float gradPos = dot(toPixel, gradDir) / (d2 + 0.001);
    float gradient = smoothstep(-0.5, 0.5, gradPos);
    float lum = mix(baseVal * 0.2, baseVal * 1.4, gradient);
    lum = clamp(lum, 0.0, 1.0);

    // Metallic base (grayscale + brush grain)
    vec3 metalCol = vec3(lum);
    float brushAngle = cId * 2.4;
    float bx = w.x * cos(brushAngle) + w.y * sin(brushAngle);
    float brushGrain = 0.85 + 0.15 * sin(bx * 80.0 + cId * 13.0);
    float brushFine = 0.93 + 0.07 * sin(bx * 200.0 + cId * 7.0);
    metalCol *= brushGrain * brushFine;

    // Stained glass color: rich saturated hues per cell
    float hue = fract(panelTone + hueShift + cId * 0.13);
    float sat = mix(0.6, 0.95, fract(cId * 0.37));
    float val = brightness * mix(0.6, 1.2, gradient);
    vec3 glassCol = hsv2rgb(vec3(hue, mix(sat, sat * 0.85, saturation), val));
    // Beer-Lambert: thicker toward center, lighter at edges
    float thickness = 1.0 - edgeNorm * 0.3;
    glassCol *= thickness;
    // Backlight glow — stained glass is lit from behind
    float backlight = 0.6 + 0.4 * (1.0 - length(uv - 0.5) * 0.8);
    glassCol *= backlight * 1.3;

    // Blend between metallic and stained glass based on grip
    vec3 baseCol = mix(metalCol, glassCol, glassiness);

    // Panel normal — tilt per cell for varied reflections
    float tiltX = sin(cId * 3.7) * 0.2;
    float tiltY = cos(cId * 5.1) * 0.2;
    tiltX += gradDir.x * gradient * 0.15;
    tiltY += gradDir.y * gradient * 0.15;
    vec3 panelNormal = normalize(vec3(tiltX, tiltY, 1.0));

    // Metallic reflection (fades out as glass takes over)
    float panelDiff = max(dot(panelNormal, lightDir), 0.0);
    float panelSpec = pow(max(dot(panelNormal, halfVec), 0.0), 60.0);
    float panelFresnel = pow(1.0 - max(dot(panelNormal, viewDir), 0.0), 4.0);

    float metalInfluence = 1.0 - glassiness * 0.7;
    vec3 glass = baseCol * (0.3 + 0.7 * panelDiff * metalInfluence)
               + vec3(1.0) * panelSpec * 0.9 * metalInfluence
               + baseCol * panelFresnel * 0.5 * metalInfluence;

    // In glass mode, add luminous glow from within each pane
    glass += glassCol * glassiness * 0.3;

    // Global light falloff
    float windowLight = 1.0 - length(uv - 0.5) * 0.5;
    windowLight = max(windowLight, 0.4);
    glass *= windowLight;

    // Shadow from raised came (deeper in glass mode)
    float cameShadow = smoothstep(0.0, borderWidth * 2.0, edge);
    float shadowDepth = mix(0.55, 0.35, glassiness);
    glass *= shadowDepth + (1.0 - shadowDepth) * cameShadow;

    // ========== COMPOSITE ==========
    // Darken cames more in glass mode for contrast
    vec3 finalCame = mix(cameColor, cameColor * 0.5, glassiness);
    vec3 col = mix(glass, finalCame, cameMask);

    gl_FragColor = vec4(col, 1.0);
}

(function(THREE) {
    const INPUTS = [
        { NAME: "geometry", TYPE: "long", DEFAULT: 1, VALUES: [0,1,2,3,4,5,6,7], LABELS: ["Fullscreen Quad","Sphere","Cube","Torus","Cylinder","Cone","Plane","Custom Model"] },
        { NAME: "materialType", TYPE: "long", DEFAULT: 0, VALUES: [0,1], LABELS: ["ShaderMaterial","RawShaderMaterial"] },
        { NAME: "transparent", TYPE: "bool", DEFAULT: false },
        { NAME: "doubleSide", TYPE: "bool", DEFAULT: false },
        { NAME: "wireframe", TYPE: "bool", DEFAULT: false },
        { NAME: "transparentBg", TYPE: "bool", DEFAULT: true },
        { NAME: "bgColor", TYPE: "color", DEFAULT: [0.035, 0.035, 0.059, 1.0] },
        { NAME: "camDist", TYPE: "float", DEFAULT: 3.0, MIN: 0.5, MAX: 15.0 },
        { NAME: "autoRotate", TYPE: "float", DEFAULT: 0.5, MIN: 0.0, MAX: 3.0 },
        { NAME: "orbitSpeed", TYPE: "float", DEFAULT: 0.6, MIN: 0.0, MAX: 2.0 },
        { NAME: "texture0", TYPE: "image" }
    ];

    // Built-in uniform names — skipped during auto-parse
    var BUILTIN_NAMES = [
        'uTime', 'uResolution', 'uMouse', 'uAudioLevel', 'uAudioBass',
        'uAudioMid', 'uAudioHigh', 'uAudioFFT', 'uTexture0',
        // Three.js built-ins (ShaderMaterial auto-injects these)
        'modelMatrix', 'modelViewMatrix', 'projectionMatrix', 'viewMatrix',
        'normalMatrix', 'cameraPosition',
        // Common Three.js attributes treated as uniforms in some contexts
        'position', 'normal', 'uv'
    ];

    // Default vertex shader (ShaderMaterial — Three.js injects uniforms)
    var DEFAULT_VERT_SM = [
        'varying vec2 vUv;',
        'varying vec3 vNormal;',
        'varying vec3 vPosition;',
        'void main() {',
        '  vUv = uv;',
        '  vNormal = normalize(normalMatrix * normal);',
        '  vPosition = (modelViewMatrix * vec4(position, 1.0)).xyz;',
        '  gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);',
        '}'
    ].join('\n');

    // Default fragment shader (ShaderMaterial)
    var DEFAULT_FRAG_SM = [
        'uniform float uTime;',
        'varying vec2 vUv;',
        'varying vec3 vNormal;',
        'void main() {',
        '  vec3 n = normalize(vNormal) * 0.5 + 0.5;',
        '  float pulse = sin(uTime * 2.0) * 0.15 + 0.85;',
        '  gl_FragColor = vec4(n * pulse, 1.0);',
        '}'
    ].join('\n');

    // Default vertex shader (RawShaderMaterial — must declare everything)
    var DEFAULT_VERT_RAW = [
        'precision highp float;',
        'attribute vec3 position;',
        'attribute vec3 normal;',
        'attribute vec2 uv;',
        'uniform mat4 modelViewMatrix;',
        'uniform mat4 projectionMatrix;',
        'uniform mat3 normalMatrix;',
        'varying vec2 vUv;',
        'varying vec3 vNormal;',
        'varying vec3 vPosition;',
        'void main() {',
        '  vUv = uv;',
        '  vNormal = normalize(normalMatrix * normal);',
        '  vPosition = (modelViewMatrix * vec4(position, 1.0)).xyz;',
        '  gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);',
        '}'
    ].join('\n');

    var DEFAULT_FRAG_RAW = [
        'precision highp float;',
        'uniform float uTime;',
        'varying vec2 vUv;',
        'varying vec3 vNormal;',
        'void main() {',
        '  vec3 n = normalize(vNormal) * 0.5 + 0.5;',
        '  float pulse = sin(uTime * 2.0) * 0.15 + 0.85;',
        '  gl_FragColor = vec4(n * pulse, 1.0);',
        '}'
    ].join('\n');

    // Fullscreen quad vertex shaders
    var QUAD_VERT_SM = [
        'varying vec2 vUv;',
        'void main() {',
        '  vUv = uv;',
        '  gl_Position = vec4(position, 1.0);',
        '}'
    ].join('\n');

    var QUAD_VERT_RAW = [
        'precision highp float;',
        'attribute vec3 position;',
        'attribute vec2 uv;',
        'varying vec2 vUv;',
        'void main() {',
        '  vUv = uv;',
        '  gl_Position = vec4(position, 1.0);',
        '}'
    ].join('\n');

    function makeGeometry(id) {
        switch (id) {
            case 0: return new THREE.PlaneGeometry(2, 2); // fullscreen quad
            case 2: return new THREE.BoxGeometry(1, 1, 1);
            case 3: return new THREE.TorusGeometry(0.45, 0.2, 24, 48);
            case 4: return new THREE.CylinderGeometry(0.5, 0.5, 1, 32);
            case 5: return new THREE.ConeGeometry(0.5, 1, 32);
            case 6: return new THREE.PlaneGeometry(2, 2, 32, 32);
            default: return new THREE.SphereGeometry(0.6, 64, 64); // 1 = sphere
        }
    }

    // Parse uniform declarations from GLSL source, skip builtins
    function parseUniformsFromGLSL(src) {
        if (!src) return {};
        var uniforms = {};
        var re = /uniform\s+(float|vec2|vec3|vec4|int|bool|sampler2D)\s+(\w+)\s*;/g;
        var m;
        while ((m = re.exec(src)) !== null) {
            var type = m[1], name = m[2];
            if (BUILTIN_NAMES.indexOf(name) >= 0) continue;
            switch (type) {
                case 'float': uniforms[name] = { type: 'float', default: 0.5, min: 0, max: 1 }; break;
                case 'vec2': uniforms[name] = { type: 'vec2', default: [0, 0] }; break;
                case 'vec3': uniforms[name] = { type: 'color', default: [1, 1, 1, 1] }; break;
                case 'vec4': uniforms[name] = { type: 'color', default: [1, 1, 1, 1] }; break;
                case 'int': uniforms[name] = { type: 'int', default: 0, values: [0,1,2,3], labels: ['0','1','2','3'] }; break;
                case 'bool': uniforms[name] = { type: 'bool', default: false }; break;
                case 'sampler2D': uniforms[name] = { type: 'sampler2D' }; break;
            }
        }
        return uniforms;
    }

    function create(renderer, canvas, media) {
        var scene = new THREE.Scene();
        var _bgColor = new THREE.Color(0x09090f);
        scene.background = null;

        // Perspective camera (used for 3D geometries)
        var perspCam = new THREE.PerspectiveCamera(60, canvas.width / canvas.height, 0.1, 100);
        perspCam.position.set(0, 1.2, 3.5);
        perspCam.lookAt(0, 0, 0);

        // Orthographic camera (used for fullscreen quad)
        var orthoCam = new THREE.OrthographicCamera(-1, 1, 1, -1, 0.1, 10);
        orthoCam.position.set(0, 0, 1);

        var activeCamera = perspCam;

        // Lights (for non-custom-shader models)
        var ambient = new THREE.AmbientLight(0x404060, 0.6);
        scene.add(ambient);
        var dirLight = new THREE.DirectionalLight(0xffffff, 0.9);
        dirLight.position.set(3, 4, 2);
        scene.add(dirLight);

        var pivot = new THREE.Group();
        scene.add(pivot);

        // State
        var currentGeomId = 1;
        var geometry = makeGeometry(1);
        var customUniforms = {}; // user-defined uniform values
        var customUniformDefs = {}; // user-defined uniform definitions
        var isRaw = false;
        var currentVertSrc = DEFAULT_VERT_SM;
        var currentFragSrc = DEFAULT_FRAG_SM;
        var customModel = null;

        // Build Three.js uniforms object from builtins + custom defs
        function buildUniforms(defs) {
            var u = {
                uTime: { value: 0 },
                uResolution: { value: new THREE.Vector2(1920, 1080) },
                uMouse: { value: new THREE.Vector2(0.5, 0.5) },
                uAudioLevel: { value: 0 },
                uAudioBass: { value: 0 },
                uAudioMid: { value: 0 },
                uAudioHigh: { value: 0 },
                uAudioFFT: { value: null },
                uTexture0: { value: null }
            };
            if (defs) {
                var keys = Object.keys(defs);
                for (var i = 0; i < keys.length; i++) {
                    var name = keys[i];
                    var def = defs[name];
                    if (BUILTIN_NAMES.indexOf(name) >= 0) continue;
                    switch (def.type) {
                        case 'float': u[name] = { value: def.default != null ? def.default : 0.5 }; break;
                        case 'color': u[name] = { value: def.default ? new THREE.Vector4(def.default[0], def.default[1], def.default[2], def.default[3] || 1) : new THREE.Vector4(1,1,1,1) }; break;
                        case 'bool': u[name] = { value: !!def.default }; break;
                        case 'int': case 'long': u[name] = { value: def.default || 0 }; break;
                        case 'vec2': u[name] = { value: def.default ? new THREE.Vector2(def.default[0], def.default[1]) : new THREE.Vector2(0,0) }; break;
                        case 'sampler2D': case 'image': u[name] = { value: null }; break;
                        default: u[name] = { value: def.default != null ? def.default : 0.5 };
                    }
                }
            }
            return u;
        }

        var threeUniforms = buildUniforms(null);

        function createMaterial(vert, frag, raw, defs) {
            threeUniforms = buildUniforms(defs);
            var opts = {
                uniforms: threeUniforms,
                vertexShader: vert,
                fragmentShader: frag,
                transparent: false,
                side: THREE.FrontSide,
                wireframe: false
            };
            return raw ? new THREE.RawShaderMaterial(opts) : new THREE.ShaderMaterial(opts);
        }

        var material = createMaterial(DEFAULT_VERT_SM, DEFAULT_FRAG_SM, false, null);
        var mesh = new THREE.Mesh(geometry, material);
        pivot.add(mesh);

        // setShader: hot-swap vertex/fragment shaders + rebuild material
        function setShader(vert, frag, uniformDefs) {
            // If no explicit defs provided, auto-parse from fragment source
            var defs = uniformDefs || parseUniformsFromGLSL(frag);
            customUniformDefs = defs;

            var geomId = currentGeomId;
            var isQuad = geomId === 0;

            // Determine shader sources
            if (isRaw) {
                currentVertSrc = vert || (isQuad ? QUAD_VERT_RAW : DEFAULT_VERT_RAW);
                currentFragSrc = frag || DEFAULT_FRAG_RAW;
            } else {
                currentVertSrc = vert || (isQuad ? QUAD_VERT_SM : DEFAULT_VERT_SM);
                currentFragSrc = frag || DEFAULT_FRAG_SM;
            }

            material.dispose();
            material = createMaterial(currentVertSrc, currentFragSrc, isRaw, defs);
            mesh.material = material;
        }

        function getCustomUniforms() {
            return customUniformDefs;
        }

        return {
            scene: scene,
            camera: activeCamera,
            setShader: setShader,
            getCustomUniforms: getCustomUniforms,

            update: function(time, values, mediaList) {
                var sz = renderer.getSize(new THREE.Vector2());
                var dpr = renderer.getPixelRatio();

                // Background
                var wantTransparent = values.transparentBg != null ? !!values.transparentBg : true;
                if (wantTransparent) {
                    scene.background = null;
                } else {
                    if (values.bgColor) {
                        var bg = values.bgColor;
                        _bgColor.setRGB(bg[0], bg[1], bg[2]);
                    }
                    scene.background = _bgColor;
                }

                // Material properties
                material.transparent = values.transparent != null ? !!values.transparent : false;
                material.side = (values.doubleSide != null && values.doubleSide) ? THREE.DoubleSide : THREE.FrontSide;
                material.wireframe = values.wireframe != null ? !!values.wireframe : false;

                // Material type switch
                var wantRaw = (values.materialType === 1);
                if (wantRaw !== isRaw) {
                    isRaw = wantRaw;
                    setShader(null, null, customUniformDefs);
                }

                // Geometry switch
                var geomId = values.geometry != null ? values.geometry : 1;
                if (geomId !== currentGeomId) {
                    var isQuad = (geomId === 0);
                    var wasQuad = (currentGeomId === 0);

                    if (geomId === 7) {
                        // Custom model
                        mesh.visible = false;
                        var modelMedia = mediaList && mediaList.find(function(e) { return e.type === 'model' && e.threeModel; });
                        if (modelMedia && modelMedia.threeModel !== (customModel && customModel._sourceModel)) {
                            if (customModel) pivot.remove(customModel);
                            customModel = modelMedia.threeModel.clone();
                            customModel._sourceModel = modelMedia.threeModel;
                            var box = new THREE.Box3().setFromObject(customModel);
                            var center = box.getCenter(new THREE.Vector3());
                            var extent = box.getSize(new THREE.Vector3()).length();
                            var s = extent > 0 ? 2.0 / extent : 1;
                            customModel.scale.multiplyScalar(s);
                            customModel.position.copy(center).multiplyScalar(-s);
                            customModel.traverse(function(child) {
                                if (child.isMesh) child.material = material;
                            });
                            pivot.add(customModel);
                        }
                    } else {
                        if (customModel) {
                            pivot.remove(customModel);
                            customModel = null;
                        }
                        mesh.visible = true;
                        geometry.dispose();
                        geometry = makeGeometry(geomId);
                        mesh.geometry = geometry;
                    }

                    // Switch camera for quad mode
                    if (isQuad && !wasQuad) {
                        activeCamera = orthoCam;
                        // Rebuild shader with quad vertex if using defaults
                        setShader(null, currentFragSrc, customUniformDefs);
                    } else if (!isQuad && wasQuad) {
                        activeCamera = perspCam;
                        setShader(null, currentFragSrc, customUniformDefs);
                    }
                    // Expose camera switch to renderer
                    this.camera = activeCamera;

                    currentGeomId = geomId;
                }

                // Camera orbit (skip for fullscreen quad)
                if (currentGeomId !== 0) {
                    var camDist = values.camDist != null ? values.camDist : 3.0;
                    var autoRot = values.autoRotate != null ? values.autoRotate : 0.5;
                    var orbit = values.orbitSpeed != null ? values.orbitSpeed : 0.6;
                    var mp = values._mousePos || [0.5, 0.5];
                    var mx = (mp[0] - 0.5) * 2.0;
                    var my = (mp[1] - 0.5) * 2.0;

                    // Auto-rotate pivot
                    pivot.rotation.y = time * autoRot;

                    // Orbit camera
                    var ot = time * 0.25 * orbit;
                    var angle = ot + mx * 3.14 + 0.4 * Math.sin(ot * 0.7);
                    var dist = camDist + 0.6 * Math.sin(ot * 0.5) * orbit;
                    var camY = 1.2 + my * 2.0 + 0.8 * Math.sin(ot * 0.35) * orbit;
                    perspCam.position.set(Math.sin(angle) * dist, camY, Math.cos(angle) * dist);
                    perspCam.lookAt(0, 0, 0);
                }

                // Update built-in uniforms
                if (threeUniforms.uTime) threeUniforms.uTime.value = time;
                if (threeUniforms.uResolution) threeUniforms.uResolution.value.set(sz.x * dpr, sz.y * dpr);
                if (threeUniforms.uMouse) {
                    var mpos = values._mousePos || [0.5, 0.5];
                    threeUniforms.uMouse.value.set(mpos[0], mpos[1]);
                }

                // Audio uniforms
                if (threeUniforms.uAudioLevel) threeUniforms.uAudioLevel.value = values._audioLevel || 0;
                if (threeUniforms.uAudioBass) threeUniforms.uAudioBass.value = values._audioBass || 0;
                if (threeUniforms.uAudioMid) threeUniforms.uAudioMid.value = values._audioMid || 0;
                if (threeUniforms.uAudioHigh) threeUniforms.uAudioHigh.value = values._audioHigh || 0;
                if (threeUniforms.uAudioFFT && values._audioFFTTexture) {
                    threeUniforms.uAudioFFT.value = values._audioFFTTexture;
                }

                // Texture0 from media
                var texId = values.texture0;
                if (texId && mediaList) {
                    var m = mediaList.find(function(e) { return String(e.id) === String(texId); });
                    if (m && m.threeTexture && threeUniforms.uTexture0) {
                        threeUniforms.uTexture0.value = m.threeTexture;
                        if (m.threeTexture.isVideoTexture) m.threeTexture.needsUpdate = true;
                    }
                } else if (threeUniforms.uTexture0) {
                    threeUniforms.uTexture0.value = null;
                }

                // Update custom uniforms from inputValues
                var keys = Object.keys(customUniformDefs);
                for (var i = 0; i < keys.length; i++) {
                    var name = keys[i];
                    if (!threeUniforms[name]) continue;
                    var val = values[name];
                    if (val == null) continue;
                    var def = customUniformDefs[name];
                    if (def.type === 'color' && Array.isArray(val)) {
                        threeUniforms[name].value.set(val[0], val[1], val[2], val[3] || 1);
                    } else if (def.type === 'vec2' && Array.isArray(val)) {
                        threeUniforms[name].value.set(val[0], val[1]);
                    } else if (def.type === 'sampler2D' || def.type === 'image') {
                        // Bind from media
                        if (val && mediaList) {
                            var tm = mediaList.find(function(e) { return String(e.id) === String(val); });
                            if (tm && tm.threeTexture) {
                                threeUniforms[name].value = tm.threeTexture;
                                if (tm.threeTexture.isVideoTexture) tm.threeTexture.needsUpdate = true;
                            }
                        }
                    } else {
                        threeUniforms[name].value = val;
                    }
                }
            },

            resize: function(w, h) {
                perspCam.aspect = w / h;
                perspCam.updateProjectionMatrix();
            },

            dispose: function() {
                geometry.dispose();
                material.dispose();
            }
        };
    }

    return { INPUTS: INPUTS, create: create };
})

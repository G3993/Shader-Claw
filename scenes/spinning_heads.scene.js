(function(THREE) {
    var COUNTS = [1, 3, 5, 7, 9];

    var INPUTS = [
        { NAME: "mode", TYPE: "long", DEFAULT: 1, VALUES: [0, 1], LABELS: ["All Same", "Material Parade"] },
        { NAME: "material", TYPE: "long", DEFAULT: 0, VALUES: [0,1,2,3,4,5,6], LABELS: ["Standard","Wireframe","Flat","Chrome","Glass","X-Ray","Normal"] },
        { NAME: "count", TYPE: "long", DEFAULT: 1, VALUES: [0,1,2,3,4], LABELS: ["1","3","5","7","9"] },
        { NAME: "layout", TYPE: "long", DEFAULT: 1, VALUES: [0,1], LABELS: ["Line","Circle"] },
        { NAME: "speed", TYPE: "float", DEFAULT: 0.3, MIN: -3.0, MAX: 3.0 },
        { NAME: "headSpin", TYPE: "float", DEFAULT: 0.2, MIN: -2.0, MAX: 2.0 },
        { NAME: "tilt", TYPE: "float", DEFAULT: 0.0, MIN: -1.5, MAX: 1.5 },
        { NAME: "color1", TYPE: "color", DEFAULT: [0.85, 0.72, 0.62, 1.0] },
        { NAME: "color2", TYPE: "color", DEFAULT: [0.15, 0.5, 1.0, 1.0] },
        { NAME: "metalness", TYPE: "float", DEFAULT: 0.15, MIN: 0.0, MAX: 1.0 },
        { NAME: "roughness", TYPE: "float", DEFAULT: 0.45, MIN: 0.0, MAX: 1.0 },
        { NAME: "bgColor", TYPE: "color", DEFAULT: [0.02, 0.02, 0.05, 1.0] },
        { NAME: "floor", TYPE: "bool", DEFAULT: true },
        { NAME: "texture", TYPE: "image" }
    ];

    // ── Material factory ──────────────────────────────────────────
    function makeMaterial(type, c1, c2, metal, rough) {
        var col1 = new THREE.Color(c1[0], c1[1], c1[2]);
        var col2 = new THREE.Color(c2[0], c2[1], c2[2]);
        switch (type) {
            case 0: // Standard PBR
                return new THREE.MeshStandardMaterial({
                    color: col1, metalness: metal, roughness: rough
                });
            case 1: // Wireframe
                return new THREE.MeshBasicMaterial({
                    color: col1, wireframe: true
                });
            case 2: // Flat shading
                return new THREE.MeshStandardMaterial({
                    color: col1, metalness: metal, roughness: rough, flatShading: true
                });
            case 3: // Chrome
                return new THREE.MeshStandardMaterial({
                    color: 0xffffff, metalness: 1.0, roughness: 0.05
                });
            case 4: // Glass
                return new THREE.MeshPhysicalMaterial({
                    color: col2, metalness: 0.0, roughness: 0.05,
                    transmission: 0.92, thickness: 1.5, ior: 1.5,
                    transparent: true
                });
            case 5: // X-Ray
                return new THREE.MeshPhongMaterial({
                    color: col2, emissive: col2.clone().multiplyScalar(0.15),
                    transparent: true, opacity: 0.35,
                    side: THREE.DoubleSide, depthWrite: false
                });
            case 6: // Normal rainbow
                return new THREE.MeshNormalMaterial();
            default:
                return new THREE.MeshStandardMaterial({
                    color: col1, metalness: metal, roughness: rough
                });
        }
    }

    // ── Scene factory ─────────────────────────────────────────────
    function create(renderer, canvas, media) {
        var scene = new THREE.Scene();
        scene.background = new THREE.Color(0.02, 0.02, 0.05);

        var camera = new THREE.PerspectiveCamera(50, canvas.width / canvas.height, 0.1, 100);
        camera.position.set(0, 0.5, 5);
        camera.lookAt(0, 0, 0);

        // ── Lighting rig ──────────────────────────────────────────
        scene.add(new THREE.AmbientLight(0x404060, 0.6));

        var keyLight = new THREE.DirectionalLight(0xffeedd, 1.3);
        keyLight.position.set(5, 5, 5);
        scene.add(keyLight);

        var fillLight = new THREE.DirectionalLight(0x8899bb, 0.5);
        fillLight.position.set(-4, 2, -1);
        scene.add(fillLight);

        var rimLight = new THREE.DirectionalLight(0x4ecdc4, 0.6);
        rimLight.position.set(-2, 3, -5);
        scene.add(rimLight);

        var bottomLight = new THREE.DirectionalLight(0x3344aa, 0.25);
        bottomLight.position.set(0, -3, 2);
        scene.add(bottomLight);

        // ── Floor ─────────────────────────────────────────────────
        var gridHelper = new THREE.GridHelper(10, 20, 0x1e1e3e, 0x1e1e3e);
        gridHelper.position.y = -1.8;
        scene.add(gridHelper);

        // ── Turntable ─────────────────────────────────────────────
        var turntable = new THREE.Group();
        scene.add(turntable);

        // ── Geometry state ────────────────────────────────────────
        var fallbackGeo = new THREE.IcosahedronGeometry(0.85, 4);
        var sourceGeo = null;       // set when model loads
        var headSlots = [];         // { group, mesh, material, matType }
        var needsRebuild = true;
        var prevCount = -1, prevMode = -1, prevMat = -1, prevLayout = -1;
        var prevModelId = null;
        var currentTexture = null;

        // ── Load LeePerrySmith from Three.js CDN ─────────────────
        try {
            var loader = new THREE.GLTFLoader();
            loader.load(
                'https://threejs.org/examples/models/gltf/LeePerrySmith/LeePerrySmith.glb',
                function(gltf) {
                    gltf.scene.traverse(function(child) {
                        if (child.isMesh && child.geometry && !sourceGeo) {
                            sourceGeo = child.geometry.clone();
                            sourceGeo.computeBoundingBox();
                            var box = sourceGeo.boundingBox;
                            var center = box.getCenter(new THREE.Vector3());
                            var sz = box.getSize(new THREE.Vector3());
                            var maxDim = Math.max(sz.x, sz.y, sz.z);
                            var s = 2.0 / maxDim;
                            sourceGeo.translate(-center.x, -center.y, -center.z);
                            sourceGeo.scale(s, s, s);
                            needsRebuild = true;
                        }
                    });
                },
                undefined,
                function(err) { console.warn('Head model fallback – using icosahedron:', err); }
            );
        } catch(e) { /* GLTFLoader unavailable */ }

        // ── Screen-space texture projection ───────────────────────
        var _resolution = new THREE.Vector2(1, 1);
        var _texScale = { value: 1.0 };
        var _screenSpaceChunk = [
            '#ifdef USE_MAP',
            '  vec2 screenUV = gl_FragCoord.xy / screenRes;',
            '  screenUV.y = 1.0 - screenUV.y;',
            '  screenUV = fract(screenUV * texScale);',
            '  vec4 texelColor = texture2D( map, screenUV );',
            '  texelColor = mapTexelToLinear( texelColor );',
            '  diffuseColor *= texelColor;',
            '#endif'
        ].join('\n');

        function patchScreenSpace(mat) {
            if (!mat || mat._patched || mat.type === 'MeshNormalMaterial') return;
            mat._patched = true;
            mat.onBeforeCompile = function(shader) {
                shader.uniforms.screenRes = { value: _resolution };
                shader.uniforms.texScale = _texScale;
                shader.fragmentShader = 'uniform vec2 screenRes;\nuniform float texScale;\n' + shader.fragmentShader;
                shader.fragmentShader = shader.fragmentShader.replace('#include <map_fragment>', _screenSpaceChunk);
            };
        }

        // ── Build / arrange heads ─────────────────────────────────
        function rebuildHeads(countIdx, mode, matType, c1, c2, metal, rough) {
            // Dispose old
            for (var i = 0; i < headSlots.length; i++) {
                turntable.remove(headSlots[i].group);
                if (headSlots[i].material) headSlots[i].material.dispose();
            }
            headSlots = [];

            var geo = sourceGeo || fallbackGeo;
            var n = COUNTS[countIdx] || 1;

            for (var i = 0; i < n; i++) {
                var mIdx = (mode === 1) ? (i % 7) : matType;
                var mat = makeMaterial(mIdx, c1, c2, metal, rough);
                patchScreenSpace(mat);
                // Re-apply current texture if active
                if (currentTexture && mat.type !== 'MeshNormalMaterial') {
                    mat.map = currentTexture;
                    mat.needsUpdate = true;
                }
                var mesh = new THREE.Mesh(geo, mat);
                var group = new THREE.Group();
                group.add(mesh);
                headSlots.push({ group: group, mesh: mesh, material: mat, matType: mIdx });
                turntable.add(group);
            }
        }

        function arrangeHeads(layout) {
            var n = headSlots.length;
            if (n === 0) return;
            if (n === 1) {
                headSlots[0].group.position.set(0, 0, 0);
                headSlots[0].group.rotation.set(0, 0, 0);
                return;
            }

            if (layout === 0) { // Line
                var spacing = Math.min(2.8, 10.0 / n);
                var total = spacing * (n - 1);
                for (var i = 0; i < n; i++) {
                    headSlots[i].group.position.set(-total / 2 + i * spacing, 0, 0);
                    headSlots[i].group.rotation.set(0, 0, 0);
                }
            } else { // Circle
                var radius = Math.max(2.2, n * 0.55);
                for (var i = 0; i < n; i++) {
                    var angle = (i / n) * Math.PI * 2;
                    var x = Math.sin(angle) * radius;
                    var z = Math.cos(angle) * radius;
                    headSlots[i].group.position.set(x, 0, z);
                    // Face outward from center
                    headSlots[i].group.rotation.set(0, angle, 0);
                }
            }
        }

        // ── Update loop ───────────────────────────────────────────
        return {
            scene: scene,
            camera: camera,

            update: function(time, values, mediaList) {
                // Screen resolution for texture projection
                var sz = renderer.getSize(new THREE.Vector2());
                var dpr = renderer.getPixelRatio();
                _resolution.set(sz.x * dpr, sz.y * dpr);
                _texScale.value = (values.texScale != null) ? values.texScale : 1.0;

                // Background
                if (values.bgColor && scene.background && scene.background.isColor) {
                    scene.background.setRGB(values.bgColor[0], values.bgColor[1], values.bgColor[2]);
                }

                // Floor
                gridHelper.visible = values.floor != null ? !!values.floor : true;

                // Check for user-imported model (overrides CDN head)
                if (mediaList) {
                    var modelMedia = mediaList.find(function(e) { return e.type === 'model' && e.threeModel; });
                    if (modelMedia && modelMedia.id !== prevModelId) {
                        modelMedia.threeModel.traverse(function(child) {
                            if (child.isMesh && child.geometry) {
                                if (sourceGeo && sourceGeo !== fallbackGeo) sourceGeo.dispose();
                                sourceGeo = child.geometry.clone();
                                sourceGeo.computeBoundingBox();
                                var box = sourceGeo.boundingBox;
                                var center = box.getCenter(new THREE.Vector3());
                                var szV = box.getSize(new THREE.Vector3());
                                var maxD = Math.max(szV.x, szV.y, szV.z);
                                var s = 2.0 / maxD;
                                sourceGeo.translate(-center.x, -center.y, -center.z);
                                sourceGeo.scale(s, s, s);
                            }
                        });
                        prevModelId = modelMedia.id;
                        needsRebuild = true;
                    }
                }

                // Read values
                var countIdx = (values.count != null) ? values.count : 1;
                var mode     = (values.mode != null) ? values.mode : 1;
                var matType  = (values.material != null) ? values.material : 0;
                var layout   = (values.layout != null) ? values.layout : 1;
                var spd      = (values.speed != null) ? values.speed : 0.3;
                var hSpin    = (values.headSpin != null) ? values.headSpin : 0.2;
                var tilt     = (values.tilt != null) ? values.tilt : 0.0;
                var c1       = values.color1 || [0.85, 0.72, 0.62, 1.0];
                var c2       = values.color2 || [0.15, 0.5, 1.0, 1.0];
                var metal    = (values.metalness != null) ? values.metalness : 0.15;
                var rough    = (values.roughness != null) ? values.roughness : 0.45;

                // Rebuild heads if config changed
                if (needsRebuild || countIdx !== prevCount || mode !== prevMode || (mode === 0 && matType !== prevMat)) {
                    rebuildHeads(countIdx, mode, matType, c1, c2, metal, rough);
                    arrangeHeads(layout);
                    needsRebuild = false;
                    prevCount = countIdx;
                    prevMode = mode;
                    prevMat = matType;
                    prevLayout = layout;
                }

                // Re-arrange if layout changed
                if (layout !== prevLayout) {
                    arrangeHeads(layout);
                    prevLayout = layout;
                }

                // Live-update material properties (no rebuild needed)
                for (var i = 0; i < headSlots.length; i++) {
                    var slot = headSlots[i];
                    var mt = slot.matType;
                    if (mt === 0 || mt === 2) { // Standard / Flat
                        if (!slot.material.map) slot.material.color.setRGB(c1[0], c1[1], c1[2]);
                        slot.material.metalness = metal;
                        slot.material.roughness = rough;
                    } else if (mt === 1) { // Wireframe
                        slot.material.color.setRGB(c1[0], c1[1], c1[2]);
                    } else if (mt === 4 || mt === 5) { // Glass / X-Ray
                        slot.material.color.setRGB(c2[0], c2[1], c2[2]);
                    }
                }

                // Texture from media
                var texId = values.texture;
                if (texId && mediaList) {
                    var m = mediaList.find(function(e) { return String(e.id) === String(texId); });
                    if (m && m.threeTexture) {
                        if (currentTexture !== m.threeTexture) {
                            m.threeTexture.wrapS = THREE.ClampToEdgeWrapping;
                            m.threeTexture.wrapT = THREE.ClampToEdgeWrapping;
                            m.threeTexture.minFilter = THREE.LinearFilter;
                            m.threeTexture.magFilter = THREE.LinearFilter;
                            m.threeTexture.needsUpdate = true;
                            currentTexture = m.threeTexture;
                        }
                        for (var i = 0; i < headSlots.length; i++) {
                            var mat = headSlots[i].material;
                            if (mat.map !== m.threeTexture && mat.type !== 'MeshNormalMaterial') {
                                mat.map = m.threeTexture;
                                mat.color.setRGB(1, 1, 1);
                                mat.needsUpdate = true;
                            }
                        }
                        // Keep video textures alive
                        if (m.threeTexture.isVideoTexture) {
                            m.threeTexture.needsUpdate = true;
                            var vid = m.threeTexture.image;
                            if (vid && (vid.paused || vid.ended)) {
                                vid.play().catch(function() {});
                            }
                        }
                    }
                } else if (!texId && currentTexture) {
                    // Remove texture
                    for (var i = 0; i < headSlots.length; i++) {
                        var mat = headSlots[i].material;
                        if (mat.map) {
                            mat.map = null;
                            mat.needsUpdate = true;
                        }
                    }
                    currentTexture = null;
                }

                // ── Animation ─────────────────────────────────────
                turntable.rotation.y = time * spd;
                turntable.rotation.x = tilt;

                // Individual head spin
                for (var i = 0; i < headSlots.length; i++) {
                    headSlots[i].mesh.rotation.y = time * hSpin;
                }

                // Adaptive camera distance
                var numHeads = COUNTS[countIdx] || 1;
                var targetZ;
                if (numHeads === 1) {
                    targetZ = 4;
                } else if (layout === 1) { // Circle
                    targetZ = Math.max(5, numHeads * 1.1 + 2);
                } else { // Line
                    targetZ = Math.max(5, numHeads * 1.4 + 1);
                }
                camera.position.z += (targetZ - camera.position.z) * 0.04;
                camera.position.y += (0.3 - camera.position.y) * 0.04;
                camera.lookAt(0, 0, 0);
            },

            resize: function(w, h) {
                camera.aspect = w / h;
                camera.updateProjectionMatrix();
            },

            dispose: function() {
                fallbackGeo.dispose();
                if (sourceGeo && sourceGeo !== fallbackGeo) sourceGeo.dispose();
                for (var i = 0; i < headSlots.length; i++) {
                    if (headSlots[i].material) headSlots[i].material.dispose();
                }
            }
        };
    }

    return { INPUTS: INPUTS, create: create };
})
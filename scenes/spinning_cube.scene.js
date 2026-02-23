(function(THREE) {
    const INPUTS = [
        { NAME: "cubeColor", TYPE: "color", DEFAULT: [1.0, 1.0, 1.0, 1.0] },
        { NAME: "bgColor", TYPE: "color", DEFAULT: [0.035, 0.035, 0.059, 1.0] },
        { NAME: "shape", TYPE: "long", DEFAULT: 0, VALUES: [0,1,2,3,4,5,6], LABELS: ["Cube","Sphere","Torus","Cylinder","Cone","Dodecahedron","Custom"] },
        { NAME: "floor", TYPE: "bool", DEFAULT: true },
        { NAME: "speed", TYPE: "float", DEFAULT: 1.0, MIN: 0.0, MAX: 5.0 },
        { NAME: "rotX", TYPE: "float", DEFAULT: 0.7, MIN: -3.0, MAX: 3.0 },
        { NAME: "rotY", TYPE: "float", DEFAULT: 1.0, MIN: -3.0, MAX: 3.0 },
        { NAME: "rotZ", TYPE: "float", DEFAULT: 0.0, MIN: -3.0, MAX: 3.0 },
        { NAME: "size", TYPE: "float", DEFAULT: 1.0, MIN: 0.2, MAX: 3.0 },
        { NAME: "texture", TYPE: "image" },
        { NAME: "texScale", TYPE: "float", DEFAULT: 1.0, MIN: 0.1, MAX: 10.0 }
    ];

    function makeGeometry(shapeId) {
        switch (shapeId) {
            case 1: return new THREE.SphereGeometry(0.6, 64, 64);
            case 2: return new THREE.TorusGeometry(0.45, 0.2, 24, 48);
            case 3: return new THREE.CylinderGeometry(0.5, 0.5, 1, 32);
            case 4: return new THREE.ConeGeometry(0.5, 1, 32);
            case 5: return new THREE.DodecahedronGeometry(0.6);
            default: return new THREE.BoxGeometry(1, 1, 1);
        }
    }

    function create(renderer, canvas, media) {
        const scene = new THREE.Scene();
        scene.background = new THREE.Color(0x09090f);

        const camera = new THREE.PerspectiveCamera(60, canvas.width / canvas.height, 0.1, 100);
        camera.position.set(0, 1.2, 3.5);
        camera.lookAt(0, 0, 0);

        // Lights
        const ambient = new THREE.AmbientLight(0x404060, 0.6);
        scene.add(ambient);

        const dirLight = new THREE.DirectionalLight(0xffffff, 0.9);
        dirLight.position.set(3, 4, 2);
        scene.add(dirLight);

        const rimLight = new THREE.DirectionalLight(0x4ecdc4, 0.3);
        rimLight.position.set(-2, 1, -3);
        scene.add(rimLight);

        // Pivot group for rotation/scale
        const pivot = new THREE.Group();
        scene.add(pivot);

        // Mesh
        let currentShape = 0;
        let geometry = makeGeometry(0);
        const material = new THREE.MeshStandardMaterial({
            color: new THREE.Color(1.0, 1.0, 1.0),
            roughness: 0.35,
            metalness: 0.15
        });
        // Screen-space texture projection — texture fills viewport, tiles when scaled
        const _resolution = new THREE.Vector2(1, 1);
        const _texScale = { value: 1.0 };
        const _screenSpaceChunk = [
            '#ifdef USE_MAP',
            '  vec2 screenUV = gl_FragCoord.xy / screenRes;',
            '  screenUV.y = 1.0 - screenUV.y;',
            '  screenUV = fract(screenUV * texScale);',
            '  vec4 texelColor = texture2D( map, screenUV );',
            '  texelColor = mapTexelToLinear( texelColor );',
            '  diffuseColor *= texelColor;',
            '#endif'
        ].join('\n');
        material.onBeforeCompile = function(shader) {
            shader.uniforms.screenRes = { value: _resolution };
            shader.uniforms.texScale = _texScale;
            shader.fragmentShader = 'uniform vec2 screenRes;\nuniform float texScale;\n' + shader.fragmentShader;
            shader.fragmentShader = shader.fragmentShader.replace('#include <map_fragment>', _screenSpaceChunk);
        };
        const mesh = new THREE.Mesh(geometry, material);
        pivot.add(mesh);

        // Custom model state
        let customModel = null;

        // Ground grid
        const gridHelper = new THREE.GridHelper(6, 12, 0x1e1e2e, 0x1e1e2e);
        gridHelper.position.y = -1.2;
        scene.add(gridHelper);

        let currentTexId = null;
        var lastVideoTime = -1;
        var stallFrames = 0;

        // Apply screen-space projection to any MeshStandardMaterial
        function patchMaterialForScreenSpace(mat) {
            if (mat._screenPatched) return;
            mat._screenPatched = true;
            mat.onBeforeCompile = function(shader) {
                shader.uniforms.screenRes = { value: _resolution };
                shader.uniforms.texScale = _texScale;
                shader.fragmentShader = 'uniform vec2 screenRes;\nuniform float texScale;\n' + shader.fragmentShader;
                shader.fragmentShader = shader.fragmentShader.replace('#include <map_fragment>', _screenSpaceChunk);
            };
        }

        return {
            scene,
            camera,
            update(time, values, mediaList) {
                // Update screen resolution for screen-space projection
                var _sz = renderer.getSize(new THREE.Vector2());
                var _dpr = renderer.getPixelRatio();
                _resolution.set(_sz.x * _dpr, _sz.y * _dpr);

                const spd = (values.speed != null) ? values.speed : 1.0;
                const sz = (values.size != null) ? values.size : 1.0;
                const rx = (values.rotX != null) ? values.rotX : 0.7;
                const ry = (values.rotY != null) ? values.rotY : 1.0;
                const rz = (values.rotZ != null) ? values.rotZ : 0.0;

                // Texture scale (tiles when > 1)
                _texScale.value = (values.texScale != null) ? values.texScale : 1.0;

                // Background color (only if background is a Color, not a texture)
                if (values.bgColor && scene.background && scene.background.isColor) {
                    const bg = values.bgColor;
                    scene.background.setRGB(bg[0], bg[1], bg[2]);
                }

                // Floor toggle
                gridHelper.visible = values.floor != null ? !!values.floor : true;

                // Shape switching
                const shapeId = (values.shape != null) ? values.shape : 0;
                if (shapeId !== currentShape) {
                    if (shapeId === 6) {
                        // Custom model — hide default mesh, show model from media
                        mesh.visible = false;
                        var modelMedia = mediaList && mediaList.find(function(e) { return e.type === 'model' && e.threeModel; });
                        if (modelMedia && modelMedia.threeModel !== (customModel && customModel._sourceModel)) {
                            if (customModel) pivot.remove(customModel);
                            customModel = modelMedia.threeModel.clone();
                            customModel._sourceModel = modelMedia.threeModel;
                            // Normalize: scale to fit, then center at origin
                            var box = new THREE.Box3().setFromObject(customModel);
                            var center = box.getCenter(new THREE.Vector3());
                            var extent = box.getSize(new THREE.Vector3()).length();
                            var s = extent > 0 ? 2.0 / extent : 1;
                            customModel.scale.multiplyScalar(s);
                            customModel.position.copy(center).multiplyScalar(-s);
                            pivot.add(customModel);
                        } else if (!modelMedia && customModel) {
                            pivot.remove(customModel);
                            customModel = null;
                            mesh.visible = true;
                        }
                    } else {
                        // Built-in shape — hide custom model, show default mesh
                        if (customModel) {
                            pivot.remove(customModel);
                            customModel = null;
                        }
                        mesh.visible = true;
                        geometry.dispose();
                        geometry = makeGeometry(shapeId);
                        mesh.geometry = geometry;
                    }
                    currentShape = shapeId;
                }

                // If custom shape, check for new/changed model each frame
                if (shapeId === 6 && mediaList) {
                    var modelMedia = mediaList.find(function(e) { return e.type === 'model' && e.threeModel; });
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
                        pivot.add(customModel);
                        mesh.visible = false;
                    } else if (!modelMedia && customModel) {
                        pivot.remove(customModel);
                        customModel = null;
                        mesh.visible = true;
                    }
                }

                pivot.rotation.x = time * spd * rx;
                pivot.rotation.y = time * spd * ry;
                pivot.rotation.z = time * spd * rz;
                pivot.scale.setScalar(sz);

                if (values.cubeColor && !material.map) {
                    const c = values.cubeColor;
                    material.color.setRGB(c[0], c[1], c[2]);
                }

                // Apply texture from media input
                const texId = values.texture;
                if (texId && mediaList) {
                    const m = mediaList.find(function(e) { return String(e.id) === String(texId); });
                    if (m && m.threeTexture) {
                        // Apply to default mesh material
                        if (material.map !== m.threeTexture) {
                            m.threeTexture.wrapS = THREE.ClampToEdgeWrapping;
                            m.threeTexture.wrapT = THREE.ClampToEdgeWrapping;
                            m.threeTexture.minFilter = THREE.LinearFilter;
                            m.threeTexture.magFilter = THREE.LinearFilter;
                            m.threeTexture.anisotropy = renderer.capabilities.getMaxAnisotropy();
                            m.threeTexture.needsUpdate = true;
                            material.map = m.threeTexture;
                            material.color.setRGB(1, 1, 1);
                            material.needsUpdate = true;
                            material._vfcBound = false;
                            material._hasNewFrame = true;
                            currentTexId = texId;
                        }
                        // Apply to all materials in custom model
                        if (customModel) {
                            customModel.traverse(function(child) {
                                if (child.isMesh && child.material && child.material.map !== m.threeTexture) {
                                    patchMaterialForScreenSpace(child.material);
                                    child.material.map = m.threeTexture;
                                    child.material.color.setRGB(1, 1, 1);
                                    child.material.needsUpdate = true;
                                }
                            });
                        }
                        if (m.threeTexture.isVideoTexture) {
                            m.threeTexture.needsUpdate = true;
                            var vid = m.threeTexture.image;
                            if (vid) {
                                // Restart if paused or ended
                                if (vid.paused || vid.ended) {
                                    vid.play().catch(function() {});
                                    stallFrames = 0;
                                }
                                // Detect stall: video reports playing but currentTime frozen
                                if (!vid.paused && vid.readyState >= 2) {
                                    if (vid.currentTime === lastVideoTime) {
                                        stallFrames++;
                                        if (stallFrames > 120) { // ~2 sec at 60fps
                                            vid.currentTime += 0.01;
                                            vid.play().catch(function() {});
                                            stallFrames = 0;
                                        }
                                    } else {
                                        stallFrames = 0;
                                    }
                                    lastVideoTime = vid.currentTime;
                                }
                            }
                        }
                    }
                } else if (!texId && (material.map || customModel)) {
                    material.map = null;
                    currentTexId = null;
                    if (values.cubeColor) {
                        const c = values.cubeColor;
                        material.color.setRGB(c[0], c[1], c[2]);
                    }
                    material.needsUpdate = true;
                    // Remove texture from custom model materials
                    if (customModel) {
                        customModel.traverse(function(child) {
                            if (child.isMesh && child.material && child.material.map) {
                                child.material.map = null;
                                child.material.needsUpdate = true;
                            }
                        });
                    }
                }
            },
            resize(w, h) {
                camera.aspect = w / h;
                camera.updateProjectionMatrix();
            },
            dispose() {
                geometry.dispose();
                material.dispose();
            }
        };
    }

    return { INPUTS, create };
})

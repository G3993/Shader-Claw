(function(THREE) {
    const INPUTS = [
        { NAME: "cubeColor", TYPE: "color", DEFAULT: [1.0, 1.0, 1.0, 1.0] },
        { NAME: "bgColor", TYPE: "color", DEFAULT: [0.035, 0.035, 0.059, 1.0] },
        { NAME: "shape", TYPE: "long", DEFAULT: 0, VALUES: [0,1,2,3,4,5], LABELS: ["Cube","Sphere","Torus","Cylinder","Cone","Dodecahedron"] },
        { NAME: "floor", TYPE: "bool", DEFAULT: true },
        { NAME: "speed", TYPE: "float", DEFAULT: 1.0, MIN: 0.0, MAX: 5.0 },
        { NAME: "rotX", TYPE: "float", DEFAULT: 0.7, MIN: -3.0, MAX: 3.0 },
        { NAME: "rotY", TYPE: "float", DEFAULT: 1.0, MIN: -3.0, MAX: 3.0 },
        { NAME: "rotZ", TYPE: "float", DEFAULT: 0.0, MIN: -3.0, MAX: 3.0 },
        { NAME: "size", TYPE: "float", DEFAULT: 1.0, MIN: 0.2, MAX: 3.0 },
        { NAME: "texture", TYPE: "image" }
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

        // Mesh
        let currentShape = 0;
        let geometry = makeGeometry(0);
        const material = new THREE.MeshStandardMaterial({
            color: new THREE.Color(1.0, 1.0, 1.0),
            roughness: 0.35,
            metalness: 0.15
        });
        const mesh = new THREE.Mesh(geometry, material);
        scene.add(mesh);

        // Ground grid
        const gridHelper = new THREE.GridHelper(6, 12, 0x1e1e2e, 0x1e1e2e);
        gridHelper.position.y = -1.2;
        scene.add(gridHelper);

        let currentTexId = null;

        return {
            scene,
            camera,
            update(time, values, mediaList) {
                const spd = (values.speed != null) ? values.speed : 1.0;
                const sz = (values.size != null) ? values.size : 1.0;
                const rx = (values.rotX != null) ? values.rotX : 0.7;
                const ry = (values.rotY != null) ? values.rotY : 1.0;
                const rz = (values.rotZ != null) ? values.rotZ : 0.0;

                // Background color
                if (values.bgColor) {
                    const bg = values.bgColor;
                    scene.background.setRGB(bg[0], bg[1], bg[2]);
                }

                // Floor toggle
                gridHelper.visible = values.floor != null ? !!values.floor : true;

                // Shape switching
                const shapeId = (values.shape != null) ? values.shape : 0;
                if (shapeId !== currentShape) {
                    geometry.dispose();
                    geometry = makeGeometry(shapeId);
                    mesh.geometry = geometry;
                    currentShape = shapeId;
                }

                mesh.rotation.x = time * spd * rx;
                mesh.rotation.y = time * spd * ry;
                mesh.rotation.z = time * spd * rz;
                mesh.scale.setScalar(sz);

                if (values.cubeColor && !material.map) {
                    const c = values.cubeColor;
                    material.color.setRGB(c[0], c[1], c[2]);
                }

                // Apply texture from media input
                const texId = values.texture;
                if (texId && mediaList) {
                    const m = mediaList.find(function(e) { return String(e.id) === String(texId); });
                    if (m && m.threeTexture) {
                        if (material.map !== m.threeTexture) {
                            m.threeTexture.wrapS = THREE.RepeatWrapping;
                            m.threeTexture.wrapT = THREE.ClampToEdgeWrapping;
                            m.threeTexture.minFilter = THREE.LinearFilter;
                            m.threeTexture.magFilter = THREE.LinearFilter;
                            m.threeTexture.anisotropy = renderer.capabilities.getMaxAnisotropy();
                            m.threeTexture.needsUpdate = true;
                            material.map = m.threeTexture;
                            material.color.setRGB(1, 1, 1);
                            material.needsUpdate = true;
                            currentTexId = texId;
                        }
                        // Video textures need needsUpdate every frame
                        if (m.threeTexture.isVideoTexture) {
                            m.threeTexture.needsUpdate = true;
                        }
                    }
                } else if (!texId && material.map) {
                    material.map = null;
                    currentTexId = null;
                    if (values.cubeColor) {
                        const c = values.cubeColor;
                        material.color.setRGB(c[0], c[1], c[2]);
                    }
                    material.needsUpdate = true;
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

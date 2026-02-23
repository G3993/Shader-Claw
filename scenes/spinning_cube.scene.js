(function(THREE) {
    const INPUTS = [
        { NAME: "speed", TYPE: "float", DEFAULT: 1.0, MIN: 0.0, MAX: 5.0 },
        { NAME: "cubeColor", TYPE: "color", DEFAULT: [0.9, 0.22, 0.27, 1.0] },
        { NAME: "size", TYPE: "float", DEFAULT: 1.0, MIN: 0.2, MAX: 3.0 }
    ];

    function create(renderer, canvas) {
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

        // Cube
        const geometry = new THREE.BoxGeometry(1, 1, 1);
        const material = new THREE.MeshStandardMaterial({
            color: new THREE.Color(0.9, 0.22, 0.27),
            roughness: 0.35,
            metalness: 0.15
        });
        const cube = new THREE.Mesh(geometry, material);
        scene.add(cube);

        // Subtle ground grid
        const gridHelper = new THREE.GridHelper(6, 12, 0x1e1e2e, 0x1e1e2e);
        gridHelper.position.y = -1.2;
        scene.add(gridHelper);

        return {
            scene,
            camera,
            update(time, values) {
                const spd = (values.speed != null) ? values.speed : 1.0;
                const sz = (values.size != null) ? values.size : 1.0;

                cube.rotation.x = time * spd * 0.7;
                cube.rotation.y = time * spd;
                cube.scale.setScalar(sz);

                if (values.cubeColor) {
                    const c = values.cubeColor;
                    material.color.setRGB(c[0], c[1], c[2]);
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
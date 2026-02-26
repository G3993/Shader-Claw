(function(THREE) {
    const INPUTS = [
        { NAME: "rotXW", TYPE: "float", DEFAULT: 0.5, MIN: -3.0, MAX: 3.0 },
        { NAME: "rotYW", TYPE: "float", DEFAULT: 0.3, MIN: -3.0, MAX: 3.0 },
        { NAME: "rotZW", TYPE: "float", DEFAULT: 0.2, MIN: -3.0, MAX: 3.0 },
        { NAME: "rotXY", TYPE: "float", DEFAULT: 0.1, MIN: -3.0, MAX: 3.0 },
        { NAME: "projDist", TYPE: "float", DEFAULT: 2.0, MIN: 1.2, MAX: 5.0 },
        { NAME: "speed", TYPE: "float", DEFAULT: 1.0, MIN: 0.0, MAX: 5.0 },
        { NAME: "edgeColor", TYPE: "color", DEFAULT: [1.0, 1.0, 1.0, 1.0] },
        { NAME: "innerColor", TYPE: "color", DEFAULT: [1.0, 1.0, 1.0, 1.0] },
        { NAME: "connectColor", TYPE: "color", DEFAULT: [1.0, 1.0, 1.0, 1.0] },
        { NAME: "edgeThickness", TYPE: "float", DEFAULT: 2.0, MIN: 0.5, MAX: 8.0 },
        { NAME: "movement", TYPE: "float", DEFAULT: 0.6, MIN: 0.0, MAX: 2.0 },
        { NAME: "transparentBg", TYPE: "bool", DEFAULT: true },
        { NAME: "bgColor", TYPE: "color", DEFAULT: [0.035, 0.035, 0.059, 1.0] }
    ];

    // Generate 16 vertices: all combinations of ±1 in 4D
    function makeVertices() {
        var verts = [];
        for (var i = 0; i < 16; i++) {
            verts.push([
                (i & 1) ? 1 : -1,
                (i & 2) ? 1 : -1,
                (i & 4) ? 1 : -1,
                (i & 8) ? 1 : -1
            ]);
        }
        return verts;
    }

    // Generate 32 edges: connect vertices that differ in exactly 1 coordinate
    function makeEdges(verts) {
        var edges = [];
        for (var i = 0; i < 16; i++) {
            for (var j = i + 1; j < 16; j++) {
                var diff = 0;
                for (var k = 0; k < 4; k++) {
                    if (verts[i][k] !== verts[j][k]) diff++;
                }
                if (diff === 1) edges.push([i, j]);
            }
        }
        return edges;
    }

    // Classify edges: inner cube (w=-1), outer cube (w=+1), connecting (bridge w)
    function classifyEdge(verts, edge) {
        var a = verts[edge[0]], b = verts[edge[1]];
        if (a[3] === -1 && b[3] === -1) return 0; // inner cube
        if (a[3] === 1  && b[3] === 1)  return 1; // outer cube
        return 2; // connecting edge
    }

    // 4D rotation in a plane defined by axes a,b
    function rotate4D(v, a, b, angle) {
        var c = Math.cos(angle), s = Math.sin(angle);
        var va = v[a], vb = v[b];
        v[a] = va * c - vb * s;
        v[b] = va * s + vb * c;
    }

    // Stereographic projection from 4D to 3D
    function project4Dto3D(v4, dist) {
        var w = 1.0 / (dist - v4[3]);
        return [v4[0] * w, v4[1] * w, v4[2] * w];
    }

    function create(renderer, canvas, media) {
        var scene = new THREE.Scene();
        var _bgColor = new THREE.Color(0x09090f);
        scene.background = null;

        var camera = new THREE.PerspectiveCamera(60, canvas.width / canvas.height, 0.1, 100);
        camera.position.set(0, 1.5, 5);
        camera.lookAt(0, 0, 0);

        var verts4D = makeVertices();
        var edges = makeEdges(verts4D);

        // Classify edges for coloring
        var edgeClasses = [];
        for (var i = 0; i < edges.length; i++) {
            edgeClasses.push(classifyEdge(verts4D, edges[i]));
        }

        // Create 3 LineSegments groups (inner, outer, connecting) for different colors
        var groups = [];
        for (var g = 0; g < 3; g++) {
            var count = 0;
            for (var i = 0; i < edges.length; i++) {
                if (edgeClasses[i] === g) count++;
            }
            var positions = new Float32Array(count * 6); // 2 verts * 3 coords per edge
            var geom = new THREE.BufferGeometry();
            geom.setAttribute('position', new THREE.BufferAttribute(positions, 3));
            var mat = new THREE.LineBasicMaterial({ color: 0xffffff, linewidth: 2, transparent: true, opacity: 0.9 });
            var lines = new THREE.LineSegments(geom, mat);
            scene.add(lines);
            groups.push({ geometry: geom, material: mat, lines: lines, edgeIndices: [] });
        }

        // Map edges to their group
        for (var i = 0; i < edges.length; i++) {
            groups[edgeClasses[i]].edgeIndices.push(i);
        }

        // Vertex dots
        var dotGeom = new THREE.BufferGeometry();
        var dotPositions = new Float32Array(16 * 3);
        dotGeom.setAttribute('position', new THREE.BufferAttribute(dotPositions, 3));
        var dotMat = new THREE.PointsMaterial({ color: 0xffffff, size: 4, sizeAttenuation: false, transparent: true, opacity: 0.8 });
        var dots = new THREE.Points(dotGeom, dotMat);
        scene.add(dots);

        return {
            scene: scene,
            camera: camera,
            update: function(time, values, mediaList) {
                var spd = (values.speed != null) ? values.speed : 1.0;
                var t = time * spd;
                var rxw = (values.rotXW != null) ? values.rotXW : 0.5;
                var ryw = (values.rotYW != null) ? values.rotYW : 0.3;
                var rzw = (values.rotZW != null) ? values.rotZW : 0.2;
                var rxy = (values.rotXY != null) ? values.rotXY : 0.1;
                var pDist = (values.projDist != null) ? values.projDist : 2.0;

                // Transparent background toggle
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

                // Apply 4D rotations and project to 3D
                var projected = [];
                for (var i = 0; i < 16; i++) {
                    var v = verts4D[i].slice(); // copy original
                    rotate4D(v, 0, 3, t * rxw); // XW plane
                    rotate4D(v, 1, 3, t * ryw); // YW plane
                    rotate4D(v, 2, 3, t * rzw); // ZW plane
                    rotate4D(v, 0, 1, t * rxy); // XY plane
                    projected.push(project4Dto3D(v, pDist));
                }

                // Update vertex dot positions
                var dp = dotGeom.attributes.position.array;
                for (var i = 0; i < 16; i++) {
                    dp[i * 3]     = projected[i][0];
                    dp[i * 3 + 1] = projected[i][1];
                    dp[i * 3 + 2] = projected[i][2];
                }
                dotGeom.attributes.position.needsUpdate = true;

                // Update edge positions per group
                for (var g = 0; g < 3; g++) {
                    var grp = groups[g];
                    var pos = grp.geometry.attributes.position.array;
                    var idx = 0;
                    for (var ei = 0; ei < grp.edgeIndices.length; ei++) {
                        var edge = edges[grp.edgeIndices[ei]];
                        var a = projected[edge[0]], b = projected[edge[1]];
                        pos[idx++] = a[0]; pos[idx++] = a[1]; pos[idx++] = a[2];
                        pos[idx++] = b[0]; pos[idx++] = b[1]; pos[idx++] = b[2];
                    }
                    grp.geometry.attributes.position.needsUpdate = true;
                }

                // Apply colors
                var ec = values.edgeColor || [0.3, 0.8, 0.77, 1.0];
                var ic = values.innerColor || [1.0, 0.4, 0.2, 1.0];
                var cc = values.connectColor || [0.5, 0.5, 0.7, 1.0];
                groups[0].material.color.setRGB(ic[0], ic[1], ic[2]); // inner
                groups[1].material.color.setRGB(ec[0], ec[1], ec[2]); // outer
                groups[2].material.color.setRGB(cc[0], cc[1], cc[2]); // connecting

                var thick = (values.edgeThickness != null) ? values.edgeThickness : 2.0;
                for (var g = 0; g < 3; g++) {
                    groups[g].material.linewidth = thick;
                }

                // Auto-orbit camera
                var orbit = (values.movement != null) ? values.movement : 0.6;
                if (orbit > 0.001) {
                    var ot = time * 0.2 * orbit;
                    var angle = ot + 0.4 * Math.sin(ot * 0.7) + 0.2 * Math.sin(ot * 1.3);
                    var dist = 5 + 0.8 * Math.sin(ot * 0.5) * orbit;
                    var camY = 1.5 + 1.0 * Math.sin(ot * 0.35) * orbit;
                    camera.position.set(
                        Math.sin(angle) * dist,
                        camY,
                        Math.cos(angle) * dist
                    );
                    camera.lookAt(0, 0, 0);
                }
            },
            resize: function(w, h) {
                camera.aspect = w / h;
                camera.updateProjectionMatrix();
            },
            dispose: function() {
                for (var g = 0; g < 3; g++) {
                    groups[g].geometry.dispose();
                    groups[g].material.dispose();
                }
                dotGeom.dispose();
                dotMat.dispose();
            }
        };
    }

    return { INPUTS: INPUTS, create: create };
})

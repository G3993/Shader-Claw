// 3D Viewport Controls — gizmos, orbit, selection
// Activates when the 3D layer is selected

import { state, on, getLayer } from '../state.js';

let orbitControls = null;
let transformControls = null;
let raycaster = null;
let selectedObject = null;

export function initViewport(threeCanvas, sceneRenderer) {
  // Initialize when 3D layer is selected
  on('layer:select', ({ layerId }) => {
    if (layerId === '3d' && sceneRenderer && sceneRenderer.sceneDef) {
      enableOrbitControls(threeCanvas, sceneRenderer);
    } else {
      disableOrbitControls();
    }
  });
}

function enableOrbitControls(canvas, sceneRenderer) {
  if (!window.THREE || !THREE.OrbitControls || !sceneRenderer.sceneDef) return;
  if (orbitControls) return; // already enabled

  const camera = sceneRenderer.sceneDef.camera;
  orbitControls = new THREE.OrbitControls(camera, canvas);
  orbitControls.enableDamping = true;
  orbitControls.dampingFactor = 0.05;

  raycaster = new THREE.Raycaster();
}

function disableOrbitControls() {
  if (orbitControls) {
    orbitControls.dispose();
    orbitControls = null;
  }
}

export function getSelectedObject() { return selectedObject; }

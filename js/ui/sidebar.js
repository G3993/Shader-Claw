// Sidebar UI
// Brand, destination pills, aspect ratio, 7-layer stack, detail panel, output section

import { state, LAYER_IDS, BLEND_MODES, on, emit, getLayer, selectLayer, setLayerVisibility, setLayerOpacity, setLayerBlend } from '../state.js';

const LAYER_LABELS = {
  background: 'Background',
  media: 'Media',
  '3d': '3D',
  av: 'AV',
  effects: 'Effects',
  text: 'Text',
  overlay: 'Overlay',
};

const DESTINATIONS = [
  { id: 'general', label: 'General', icon: '◆' },
  { id: 'web', label: 'Web', icon: '◻' },
  { id: 'video', label: 'Video', icon: '▶' },
  { id: 'social', label: 'Social', icon: '◎' },
  { id: '3d', label: '3D', icon: '△' },
  { id: 'code', label: 'Code', icon: '⌨' },
  { id: 'live', label: 'Live', icon: '♫' },
];

const ASPECT_RATIOS = ['16:9', '9:16', '4:3', '1:1', '21:9', '3:2'];

export function initSidebar(sidebarEl) {
  const scroll = document.createElement('div');
  scroll.id = 'sidebar-scroll';
  sidebarEl.appendChild(scroll);

  // Brand
  const brand = document.createElement('div');
  brand.className = 'brand';
  brand.innerHTML = 'SHADER<span>CLAW</span>';
  scroll.appendChild(brand);

  // Destination section
  const destSection = document.createElement('div');
  destSection.innerHTML = '<div class="section-title">DESTINATION</div>';
  const destPills = document.createElement('div');
  destPills.className = 'dest-pills';
  DESTINATIONS.forEach(d => {
    const pill = document.createElement('button');
    pill.className = 'dest-pill' + (d.id === state.destination ? ' active' : '');
    pill.textContent = d.icon;
    pill.title = d.label;
    pill.dataset.dest = d.id;
    pill.addEventListener('click', () => {
      state.destination = d.id;
      destPills.querySelectorAll('.dest-pill').forEach(p => p.classList.toggle('active', p.dataset.dest === d.id));
      emit('destination:change', { destination: d.id });
    });
    destPills.appendChild(pill);
  });
  destSection.appendChild(destPills);
  scroll.appendChild(destSection);

  // Aspect ratio
  const ratioSection = document.createElement('div');
  ratioSection.style.padding = '0 12px 8px';
  const ratioSelect = document.createElement('select');
  ratioSelect.className = 'ratio-select';
  ratioSelect.style.width = '100%';
  ASPECT_RATIOS.forEach(r => {
    const opt = document.createElement('option');
    opt.value = r;
    opt.textContent = r;
    if (r === state.aspectRatio) opt.selected = true;
    ratioSelect.appendChild(opt);
  });
  ratioSelect.addEventListener('change', () => {
    state.aspectRatio = ratioSelect.value;
    emit('aspect:change', { ratio: ratioSelect.value });
  });
  ratioSection.appendChild(ratioSelect);
  scroll.appendChild(ratioSection);

  // Layer stack
  const layerSection = document.createElement('div');
  layerSection.innerHTML = '<div class="section-title">LAYERS</div>';
  const layerStack = document.createElement('div');
  layerStack.className = 'layer-stack';
  layerStack.id = 'layer-stack';

  // Render layers top-to-bottom (overlay first, background last)
  const reversed = [...LAYER_IDS].reverse();
  reversed.forEach(id => {
    const layer = getLayer(id);
    const card = document.createElement('div');
    card.className = 'layer-card' + (id === state.selectedLayerId ? ' selected' : '');
    card.dataset.layerId = id;

    const header = document.createElement('div');
    header.className = 'layer-header';

    // Visibility toggle
    const visBtn = document.createElement('button');
    visBtn.className = 'layer-vis' + (layer.visible ? '' : ' hidden');
    visBtn.textContent = layer.visible ? '\u{1F441}' : '\u{1F441}';
    visBtn.addEventListener('click', (e) => {
      e.stopPropagation();
      setLayerVisibility(id, !layer.visible);
      visBtn.classList.toggle('hidden', !layer.visible);
    });
    header.appendChild(visBtn);

    // Name
    const name = document.createElement('span');
    name.className = 'layer-name';
    name.textContent = LAYER_LABELS[id] || id;
    header.appendChild(name);

    // Opacity slider (compact)
    const opSlider = document.createElement('input');
    opSlider.type = 'range';
    opSlider.className = 'layer-opacity';
    opSlider.min = 0;
    opSlider.max = 1;
    opSlider.step = 0.01;
    opSlider.value = layer.opacity;
    opSlider.addEventListener('input', (e) => {
      e.stopPropagation();
      setLayerOpacity(id, parseFloat(opSlider.value));
    });
    header.appendChild(opSlider);

    // Blend mode select (compact)
    const blendSelect = document.createElement('select');
    blendSelect.className = 'layer-blend';
    BLEND_MODES.forEach(m => {
      const opt = document.createElement('option');
      opt.value = m;
      opt.textContent = m.charAt(0).toUpperCase() + m.slice(1, 3);
      if (m === layer.blendMode) opt.selected = true;
      blendSelect.appendChild(opt);
    });
    blendSelect.addEventListener('change', (e) => {
      e.stopPropagation();
      setLayerBlend(id, blendSelect.value);
    });
    header.appendChild(blendSelect);

    header.addEventListener('click', () => {
      selectLayer(id);
    });

    card.appendChild(header);
    layerStack.appendChild(card);
  });

  layerSection.appendChild(layerStack);
  scroll.appendChild(layerSection);

  // Layer detail panel (changes based on selected layer)
  const detailPanel = document.createElement('div');
  detailPanel.className = 'layer-detail';
  detailPanel.id = 'layer-detail';
  detailPanel.innerHTML = '<div class="detail-section-title">No layer selected</div>';
  scroll.appendChild(detailPanel);

  // Output section
  const outputSection = document.createElement('div');
  outputSection.className = 'output-section';
  outputSection.innerHTML = `
    <div class="section-title">OUTPUT</div>
    <div class="output-row">
      <span style="flex:1;font-size:10px;color:var(--text-dim)">NDI</span>
      <button class="output-btn" id="ndi-send-btn">Send</button>
      <span class="status-dot" id="ndi-status"></span>
    </div>
    <div class="output-row">
      <span style="flex:1;font-size:10px;color:var(--text-dim)">UE</span>
      <button class="output-btn" id="ue-stream-btn">Stream</button>
      <span class="status-dot" id="ue-status"></span>
    </div>
    <div class="output-row">
      <span style="flex:1;font-size:10px;color:var(--text-dim)">Record</span>
      <button class="output-btn" id="rec-btn">REC</button>
    </div>
    <div class="export-row">
      <button class="output-btn" id="save-btn">Save</button>
      <button class="output-btn" id="shot-btn">Shot</button>
      <button class="output-btn" id="copy-btn">Copy</button>
    </div>
  `;
  scroll.appendChild(outputSection);

  // Wire layer selection updates
  on('layer:select', ({ layerId }) => {
    layerStack.querySelectorAll('.layer-card').forEach(card => {
      card.classList.toggle('selected', card.dataset.layerId === layerId);
    });
  });

  on('layer:visibility', ({ layerId, visible }) => {
    const card = layerStack.querySelector(`[data-layer-id="${layerId}"]`);
    if (card) {
      const btn = card.querySelector('.layer-vis');
      if (btn) btn.classList.toggle('hidden', !visible);
    }
  });

  return { layerStack, detailPanel, outputSection };
}

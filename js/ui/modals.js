// Modals — shader browser, NDI picker, project picker, confirmations

import { state } from '../state.js';

// === Shader Browser ===

let shaderBrowserEl = null;
let shaderBrowserBackdrop = null;
let shaderManifest = [];
let onShaderSelect = null;

export function initShaderBrowser() {
  shaderBrowserBackdrop = document.createElement('div');
  shaderBrowserBackdrop.id = 'shader-browser-backdrop';
  shaderBrowserBackdrop.addEventListener('click', closeShaderBrowser);
  document.body.appendChild(shaderBrowserBackdrop);

  shaderBrowserEl = document.createElement('div');
  shaderBrowserEl.id = 'shader-browser';
  shaderBrowserEl.innerHTML = `
    <div class="browser-header">
      <input class="browser-search" placeholder="Search shaders..." type="text">
      <button class="browser-close">&times;</button>
    </div>
    <div class="browser-body"></div>
  `;
  document.body.appendChild(shaderBrowserEl);

  shaderBrowserEl.querySelector('.browser-close').addEventListener('click', closeShaderBrowser);
  shaderBrowserEl.querySelector('.browser-search').addEventListener('input', (e) => {
    filterShaders(e.target.value);
  });
}

export async function loadManifest() {
  try {
    const resp = await fetch('shaders/manifest.json');
    shaderManifest = await resp.json();
  } catch (e) {
    console.warn('Failed to load shader manifest:', e);
    shaderManifest = [];
  }
  return shaderManifest;
}

export function getManifest() { return shaderManifest; }

export function openShaderBrowser(callback) {
  onShaderSelect = callback;
  populateBrowser();
  shaderBrowserEl.classList.add('visible');
  shaderBrowserBackdrop.classList.add('visible');
  shaderBrowserEl.querySelector('.browser-search').focus();
}

export function closeShaderBrowser() {
  if (shaderBrowserEl) shaderBrowserEl.classList.remove('visible');
  if (shaderBrowserBackdrop) shaderBrowserBackdrop.classList.remove('visible');
}

function populateBrowser() {
  const body = shaderBrowserEl.querySelector('.browser-body');
  body.innerHTML = '';

  const categories = {};
  shaderManifest.forEach(entry => {
    if (entry.type === 'scene') return; // skip scenes in shader browser
    const cats = entry.categories || ['Uncategorized'];
    cats.forEach(cat => {
      if (!categories[cat]) categories[cat] = [];
      categories[cat].push(entry);
    });
  });

  for (const [catName, entries] of Object.entries(categories)) {
    const catEl = document.createElement('div');
    catEl.className = 'browser-category';
    catEl.innerHTML = `<div class="browser-category-title">${catName}</div>`;
    const grid = document.createElement('div');
    grid.className = 'browser-grid';

    entries.forEach(entry => {
      const item = document.createElement('div');
      item.className = 'browser-item';
      item.textContent = entry.title;
      item.addEventListener('click', () => {
        if (onShaderSelect) onShaderSelect(entry);
        closeShaderBrowser();
      });
      grid.appendChild(item);
    });

    catEl.appendChild(grid);
    body.appendChild(catEl);
  }
}

function filterShaders(query) {
  const q = query.toLowerCase();
  const items = shaderBrowserEl.querySelectorAll('.browser-item');
  items.forEach(item => {
    item.style.display = item.textContent.toLowerCase().includes(q) ? '' : 'none';
  });
  // Hide empty categories
  shaderBrowserEl.querySelectorAll('.browser-category').forEach(cat => {
    const visible = cat.querySelectorAll('.browser-item:not([style*="display: none"])');
    cat.style.display = visible.length > 0 ? '' : 'none';
  });
}

// === NDI Source Picker ===

let ndiPickerEl = null;

export function initNdiPicker() {
  ndiPickerEl = document.createElement('div');
  ndiPickerEl.id = 'ndi-picker';
  ndiPickerEl.style.display = 'none';
  ndiPickerEl.innerHTML = `
    <div style="padding:12px;background:var(--surface);border:1px solid var(--border);border-radius:var(--radius);min-width:240px">
      <div style="display:flex;justify-content:space-between;align-items:center;margin-bottom:8px">
        <span style="font-size:10px;font-weight:600;color:var(--text-dim);text-transform:uppercase;letter-spacing:1px">NDI Sources</span>
        <button class="ndi-refresh" style="font-size:10px;background:none;border:none;color:var(--text-dim);cursor:pointer">Refresh</button>
      </div>
      <div class="ndi-sources"></div>
    </div>
  `;
  document.body.appendChild(ndiPickerEl);
}

export function showNdiPicker(sources, onSelect) {
  const container = ndiPickerEl.querySelector('.ndi-sources');
  container.innerHTML = '';
  if (sources.length === 0) {
    container.innerHTML = '<div style="color:var(--text-dim);font-size:10px">No sources found</div>';
  }
  sources.forEach(s => {
    const item = document.createElement('div');
    item.style.cssText = 'padding:4px 8px;cursor:pointer;font-size:10px;border-radius:2px;margin-bottom:2px';
    item.textContent = s.name;
    item.addEventListener('mouseenter', () => item.style.background = 'var(--surface-alt)');
    item.addEventListener('mouseleave', () => item.style.background = '');
    item.addEventListener('click', () => {
      onSelect(s);
      ndiPickerEl.style.display = 'none';
    });
    container.appendChild(item);
  });
  ndiPickerEl.style.display = 'block';
}

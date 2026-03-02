// Parameter Controls Generator
// Creates UI controls for ISF shader inputs

import { state } from '../state.js';
import { mediaTypeIcon } from '../media.js';

/**
 * Generate UI controls for ISF inputs
 * @param {Array} inputs - ISF INPUTS array
 * @param {HTMLElement} container - DOM element to fill
 * @param {Function} onChange - callback with (values) on change
 * @param {object} [options] - optional context { gl, onBgSource(name, source) }
 * @returns {object} values - initial values object
 */
export function generateControls(inputs, container, onChange, options = {}) {
  container.innerHTML = '';
  if (!inputs || inputs.length === 0) {
    container.innerHTML = '<div style="color:var(--text-dim);font-size:9px;padding:4px 0">No parameters</div>';
    return {};
  }

  const values = {};
  let imageInputIdx = 0;

  inputs.forEach(inp => {
    const row = document.createElement('div');
    row.className = 'control-row';
    row.dataset.paramName = inp.NAME;

    const label = document.createElement('label');
    label.textContent = inp.LABEL || inp.NAME;
    row.appendChild(label);

    if (inp.TYPE === 'float') {
      const def = inp.DEFAULT != null ? inp.DEFAULT : 0.5;
      const min = inp.MIN != null ? inp.MIN : 0;
      const max = inp.MAX != null ? inp.MAX : 1;
      values[inp.NAME] = def;

      const range = document.createElement('input');
      range.type = 'range';
      range.min = min;
      range.max = max;
      range.step = (max - min) / 200;
      range.value = def;

      const valSpan = document.createElement('span');
      valSpan.className = 'val';
      valSpan.textContent = Number(def).toFixed(2);

      range.addEventListener('input', () => {
        const v = parseFloat(range.value);
        values[inp.NAME] = v;
        valSpan.textContent = v.toFixed(2);
        onChange(values);
      });

      row.appendChild(range);
      row.appendChild(valSpan);

    } else if (inp.TYPE === 'color' && inp.NAME === 'bgColor') {
      // Rich background source panel
      const def = inp.DEFAULT || [0, 0, 0, 1];
      values[inp.NAME] = [...def];
      row.classList.add('bg-source-row');

      const panel = _buildBgSourcePanel(inp, def, values, onChange, options);
      row.appendChild(panel);

    } else if (inp.TYPE === 'color') {
      const def = inp.DEFAULT || [1, 1, 1, 1];
      values[inp.NAME] = [...def];

      const hex = rgbToHex(def[0], def[1], def[2]);
      const picker = document.createElement('input');
      picker.type = 'color';
      picker.value = hex;

      picker.addEventListener('input', () => {
        const rgb = hexToRgb(picker.value);
        values[inp.NAME] = [rgb[0], rgb[1], rgb[2], def[3] || 1];
        onChange(values);
      });

      row.appendChild(picker);

    } else if (inp.TYPE === 'bool') {
      const def = !!inp.DEFAULT;
      values[inp.NAME] = def;

      const cb = document.createElement('input');
      cb.type = 'checkbox';
      cb.checked = def;
      cb.style.accentColor = 'var(--accent)';

      cb.addEventListener('change', () => {
        values[inp.NAME] = cb.checked;
        onChange(values);
      });

      row.appendChild(cb);

    } else if (inp.TYPE === 'long') {
      const vals = inp.VALUES || [];
      const labels = inp.LABELS || vals.map(String);
      const def = inp.DEFAULT != null ? inp.DEFAULT : (vals[0] || 0);
      values[inp.NAME] = def;

      const select = document.createElement('select');
      for (let i = 0; i < vals.length; i++) {
        const opt = document.createElement('option');
        opt.value = vals[i];
        opt.textContent = labels[i] || vals[i];
        if (vals[i] === def) opt.selected = true;
        select.appendChild(opt);
      }

      select.addEventListener('change', () => {
        values[inp.NAME] = parseFloat(select.value);
        onChange(values);
      });

      row.appendChild(select);

    } else if (inp.TYPE === 'text') {
      const maxLen = inp.MAX_LENGTH || 12;
      const def = (inp.DEFAULT || '').toUpperCase();

      function charToCode(ch) {
        if (!ch || ch === ' ') return 26;
        const c = ch.toUpperCase().charCodeAt(0) - 65;
        return (c >= 0 && c <= 25) ? c : 26;
      }

      for (let i = 0; i < maxLen; i++) {
        values[inp.NAME + '_' + i] = charToCode(def[i]);
      }
      values[inp.NAME + '_len'] = def.replace(/\s+$/, '').length;

      const textInput = document.createElement('input');
      textInput.type = 'text';
      textInput.maxLength = maxLen;
      textInput.value = def;
      textInput.spellcheck = false;

      textInput.addEventListener('input', () => {
        const str = textInput.value.toUpperCase();
        for (let i = 0; i < maxLen; i++) {
          values[inp.NAME + '_' + i] = charToCode(str[i]);
        }
        values[inp.NAME + '_len'] = str.replace(/\s+$/, '').length;
        onChange(values);
      });

      row.appendChild(textInput);

    } else if (inp.TYPE === 'image') {
      values[inp.NAME] = null;

      const select = document.createElement('select');
      select.dataset.imageInput = inp.NAME;
      select.classList.add('image-input-select');

      function _populate() {
        const prev = select.value;
        select.innerHTML = '';
        const noneOpt = document.createElement('option');
        noneOpt.value = '';
        noneOpt.textContent = '(none)';
        select.appendChild(noneOpt);
        const compatible = state.mediaInputs.filter(m => m.type === 'image' || m.type === 'video' || m.type === 'svg');
        compatible.forEach(m => {
          const opt = document.createElement('option');
          opt.value = m.id;
          opt.textContent = mediaTypeIcon(m.type) + ' ' + m.name;
          select.appendChild(opt);
        });
        if (prev && compatible.find(m => String(m.id) === String(prev))) {
          select.value = prev;
        }
      }
      _populate();

      const compatible = state.mediaInputs.filter(m => m.type === 'image' || m.type === 'video' || m.type === 'svg');
      const autoIdx = Math.min(imageInputIdx, compatible.length - 1);
      if (compatible.length > 0 && compatible[autoIdx]) {
        select.value = compatible[autoIdx].id;
        values[inp.NAME] = compatible[autoIdx].id;
      }
      imageInputIdx++;

      select.addEventListener('change', () => {
        values[inp.NAME] = select.value || null;
        onChange(values);
      });

      select._refreshOptions = _populate;
      row.appendChild(select);

    } else if (inp.TYPE === 'point2D') {
      const def = inp.DEFAULT || [0, 0];
      const min = inp.MIN || [-1, -1];
      const max = inp.MAX || [1, 1];
      values[inp.NAME] = [...def];

      for (let axis = 0; axis < 2; axis++) {
        const range = document.createElement('input');
        range.type = 'range';
        range.min = min[axis];
        range.max = max[axis];
        range.step = (max[axis] - min[axis]) / 200;
        range.value = def[axis];
        range.style.flex = '1';

        range.addEventListener('input', () => {
          values[inp.NAME][axis] = parseFloat(range.value);
          onChange(values);
        });

        row.appendChild(range);
      }
    }

    container.appendChild(row);
  });

  return values;
}

export function updateControlUI(container, name, value) {
  const row = container.querySelector(`.control-row[data-param-name="${name}"]`);
  if (!row) return;

  const range = row.querySelector('input[type="range"]');
  const valSpan = row.querySelector('.val');
  if (range && typeof value === 'number') {
    range.value = value;
    if (valSpan) valSpan.textContent = value.toFixed(2);
  }
  const checkbox = row.querySelector('input[type="checkbox"]');
  if (checkbox && typeof value === 'boolean') {
    checkbox.checked = value;
  }
  // Handle bg source panel swatch + picker
  if (row.classList.contains('bg-source-row') && Array.isArray(value)) {
    const swatch = row.querySelector('.bg-source-swatch');
    if (swatch) swatch.style.background = rgbToHex(value[0], value[1], value[2]);
    const picker = row.querySelector('.bg-source-detail input[type="color"]');
    if (picker) picker.value = rgbToHex(value[0], value[1], value[2]);
    return;
  }
  const colorPicker = row.querySelector('input[type="color"]');
  if (colorPicker && Array.isArray(value)) {
    colorPicker.value = rgbToHex(value[0], value[1], value[2]);
  }
}

// === Background Source Panel ===

const BG_SOURCE_TYPES = [
  { value: 'color', label: 'Color' },
  { value: 'gradient', label: 'Gradient' },
  { value: 'image', label: 'Image' },
  { value: 'video', label: 'Video' },
  { value: 'webcam', label: 'Webcam' },
];

function _buildBgSourcePanel(inp, def, values, onChange, options) {
  const panel = document.createElement('div');
  panel.className = 'bg-source-panel';

  // State
  let sourceType = 'color';
  let gradColor1 = [...def];
  let gradColor2 = [1, 1, 1, 1];
  let gradDirection = 'horizontal';
  let gradTex = null;

  // Header: swatch + type select
  const header = document.createElement('div');
  header.className = 'bg-source-header';

  const swatch = document.createElement('div');
  swatch.className = 'bg-source-swatch';
  swatch.style.background = rgbToHex(def[0], def[1], def[2]);

  const typeSelect = document.createElement('select');
  typeSelect.className = 'bg-source-type';
  BG_SOURCE_TYPES.forEach(t => {
    const opt = document.createElement('option');
    opt.value = t.value;
    opt.textContent = t.label;
    typeSelect.appendChild(opt);
  });

  header.appendChild(swatch);
  header.appendChild(typeSelect);
  panel.appendChild(header);

  // Detail area (expandable)
  const detail = document.createElement('div');
  detail.className = 'bg-source-detail';
  panel.appendChild(detail);

  function notifyBgSource(source) {
    if (options.onBgSource) options.onBgSource(inp.NAME, source);
  }

  function updateDetail() {
    detail.innerHTML = '';

    if (sourceType === 'color') {
      const picker = document.createElement('input');
      picker.type = 'color';
      picker.value = rgbToHex(values[inp.NAME][0], values[inp.NAME][1], values[inp.NAME][2]);
      picker.addEventListener('input', () => {
        const rgb = hexToRgb(picker.value);
        values[inp.NAME] = [rgb[0], rgb[1], rgb[2], def[3] || 1];
        swatch.style.background = picker.value;
        onChange(values);
        notifyBgSource({ type: 'color' });
      });
      detail.appendChild(picker);

    } else if (sourceType === 'gradient') {
      const gradRow = document.createElement('div');
      gradRow.className = 'bg-grad-row';

      const picker1 = document.createElement('input');
      picker1.type = 'color';
      picker1.value = rgbToHex(gradColor1[0], gradColor1[1], gradColor1[2]);

      const arrow = document.createElement('span');
      arrow.className = 'bg-grad-arrow';
      arrow.textContent = '\u2192';

      const picker2 = document.createElement('input');
      picker2.type = 'color';
      picker2.value = rgbToHex(gradColor2[0], gradColor2[1], gradColor2[2]);

      gradRow.appendChild(picker1);
      gradRow.appendChild(arrow);
      gradRow.appendChild(picker2);
      detail.appendChild(gradRow);

      const dirSelect = document.createElement('select');
      dirSelect.className = 'bg-grad-dir';
      ['horizontal', 'vertical', 'diagonal', 'radial'].forEach(d => {
        const opt = document.createElement('option');
        opt.value = d;
        opt.textContent = d.charAt(0).toUpperCase() + d.slice(1);
        if (d === gradDirection) opt.selected = true;
        dirSelect.appendChild(opt);
      });
      detail.appendChild(dirSelect);

      function updateGradient() {
        if (!options.gl) return;
        gradTex = _createGradientTexture(options.gl, gradColor1, gradColor2, gradDirection, gradTex);
        // Update swatch preview
        const deg = gradDirection === 'vertical' ? '180deg' : gradDirection === 'diagonal' ? '135deg' : '90deg';
        const hex1 = rgbToHex(gradColor1[0], gradColor1[1], gradColor1[2]);
        const hex2 = rgbToHex(gradColor2[0], gradColor2[1], gradColor2[2]);
        if (gradDirection === 'radial') {
          swatch.style.background = `radial-gradient(circle, ${hex1}, ${hex2})`;
        } else {
          swatch.style.background = `linear-gradient(${deg}, ${hex1}, ${hex2})`;
        }
        notifyBgSource({ type: 'gradient', texture: gradTex });
      }

      picker1.addEventListener('input', () => {
        gradColor1 = [...hexToRgb(picker1.value), 1];
        updateGradient();
      });
      picker2.addEventListener('input', () => {
        gradColor2 = [...hexToRgb(picker2.value), 1];
        updateGradient();
      });
      dirSelect.addEventListener('change', () => {
        gradDirection = dirSelect.value;
        updateGradient();
      });

      updateGradient();

    } else if (sourceType === 'image' || sourceType === 'video') {
      const select = document.createElement('select');
      select.className = 'bg-media-select';
      const noneOpt = document.createElement('option');
      noneOpt.value = '';
      noneOpt.textContent = '(none)';
      select.appendChild(noneOpt);

      const filterTypes = sourceType === 'image' ? ['image', 'svg'] : ['video'];
      const compatible = state.mediaInputs.filter(m => filterTypes.includes(m.type));
      compatible.forEach(m => {
        const opt = document.createElement('option');
        opt.value = m.id;
        opt.textContent = mediaTypeIcon(m.type) + ' ' + m.name;
        select.appendChild(opt);
      });

      select.addEventListener('change', () => {
        const mediaId = select.value;
        if (mediaId) {
          const media = state.mediaInputs.find(m => String(m.id) === String(mediaId));
          if (media && media.glTexture) {
            swatch.style.background = 'var(--surface-alt)';
            notifyBgSource({
              type: sourceType,
              texture: media.glTexture,
              isVideo: media.type === 'video',
              element: media.element,
            });
          }
        } else {
          swatch.style.background = rgbToHex(values[inp.NAME][0], values[inp.NAME][1], values[inp.NAME][2]);
          notifyBgSource({ type: 'color' });
        }
      });

      detail.appendChild(select);

    } else if (sourceType === 'webcam') {
      const hint = document.createElement('div');
      hint.className = 'bg-webcam-hint';
      hint.textContent = 'Uses active webcam feed';
      detail.appendChild(hint);

      // Check if webcam is available in media inputs
      const webcam = state.mediaInputs.find(m => m._webcamFlip !== undefined || m.type === 'webcam');
      if (webcam && webcam.glTexture) {
        swatch.style.background = 'var(--surface-alt)';
        notifyBgSource({
          type: 'webcam',
          texture: webcam.glTexture,
          isVideo: true,
          element: webcam.element,
        });
      } else {
        hint.textContent = 'No webcam active — add one via Media panel';
      }
    }
  }

  typeSelect.addEventListener('change', () => {
    sourceType = typeSelect.value;
    if (sourceType === 'color') {
      swatch.style.background = rgbToHex(values[inp.NAME][0], values[inp.NAME][1], values[inp.NAME][2]);
      notifyBgSource({ type: 'color' });
    }
    updateDetail();
  });

  updateDetail();
  return panel;
}

function _createGradientTexture(gl, color1, color2, direction, existingTex) {
  const size = 256;
  const canvas = document.createElement('canvas');
  canvas.width = size;
  canvas.height = size;
  const ctx = canvas.getContext('2d');

  let grad;
  switch (direction) {
    case 'vertical':
      grad = ctx.createLinearGradient(0, 0, 0, size);
      break;
    case 'diagonal':
      grad = ctx.createLinearGradient(0, 0, size, size);
      break;
    case 'radial':
      grad = ctx.createRadialGradient(size / 2, size / 2, 0, size / 2, size / 2, size / 2);
      break;
    default: // horizontal
      grad = ctx.createLinearGradient(0, 0, size, 0);
  }

  const hex1 = rgbToHex(color1[0], color1[1], color1[2]);
  const hex2 = rgbToHex(color2[0], color2[1], color2[2]);
  grad.addColorStop(0, hex1);
  grad.addColorStop(1, hex2);
  ctx.fillStyle = grad;
  ctx.fillRect(0, 0, size, size);

  const tex = existingTex || gl.createTexture();
  gl.bindTexture(gl.TEXTURE_2D, tex);
  gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, canvas);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
  gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);

  return tex;
}

// === Color Utilities ===

function rgbToHex(r, g, b) {
  const c = (v) => Math.round(Math.max(0, Math.min(1, v)) * 255).toString(16).padStart(2, '0');
  return '#' + c(r) + c(g) + c(b);
}

function hexToRgb(hex) {
  const r = parseInt(hex.slice(1, 3), 16) / 255;
  const g = parseInt(hex.slice(3, 5), 16) / 255;
  const b = parseInt(hex.slice(5, 7), 16) / 255;
  return [r, g, b];
}

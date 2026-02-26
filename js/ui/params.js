// Parameter Controls Generator
// Creates UI controls for ISF shader inputs

import { state } from '../state.js';
import { mediaTypeIcon } from '../media.js';

/**
 * Generate UI controls for ISF inputs
 * @param {Array} inputs - ISF INPUTS array
 * @param {HTMLElement} container - DOM element to fill
 * @param {Function} onChange - callback with (values) on change
 * @returns {object} values - initial values object
 */
export function generateControls(inputs, container, onChange) {
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
  const colorPicker = row.querySelector('input[type="color"]');
  if (colorPicker && Array.isArray(value)) {
    colorPicker.value = rgbToHex(value[0], value[1], value[2]);
  }
}

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

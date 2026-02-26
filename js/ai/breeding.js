// Shader Breeding — Generative variation + evolution UI
// Generates multiple shader variants, lets user favorite and evolve

import { state, getLayer, emit } from '../state.js';
import { Renderer } from '../renderer.js';
import { buildFragmentShader, VERT_SHADER, DEFAULT_SHADER } from '../isf.js';

export class ShaderLab {
  constructor(containerEl) {
    this.container = containerEl;
    this.variants = []; // { id, source, canvas, renderer, favorited }
    this.generation = 0;
    this._build();
  }

  _build() {
    this.container.innerHTML = '';
    this.container.classList.add('shader-lab');

    // Header
    const header = document.createElement('div');
    header.className = 'shader-lab-header';
    header.innerHTML = '<span style="font-size:10px;font-weight:600;color:var(--text-dim);letter-spacing:1px;text-transform:uppercase">Shader Lab</span>';
    this.container.appendChild(header);

    // Direction input
    const dirRow = document.createElement('div');
    dirRow.style.cssText = 'padding:8px;display:flex;gap:4px';
    this.dirInput = document.createElement('input');
    this.dirInput.type = 'text';
    this.dirInput.placeholder = 'Direction: "cosmic purple nebula"';
    this.dirInput.style.cssText = 'flex:1;background:var(--bg);color:var(--text);border:1px solid var(--border);border-radius:var(--radius);padding:4px 8px;font-size:10px;font-family:var(--font)';
    dirRow.appendChild(this.dirInput);
    this.container.appendChild(dirRow);

    // Controls bar
    const controls = document.createElement('div');
    controls.className = 'shader-lab-controls';
    controls.style.cssText = 'padding:0 8px 8px;display:flex;gap:4px';

    const genBtn = document.createElement('button');
    genBtn.className = 'output-btn';
    genBtn.textContent = 'Generate';
    genBtn.addEventListener('click', () => this.generate());
    controls.appendChild(genBtn);

    const evolveBtn = document.createElement('button');
    evolveBtn.className = 'output-btn';
    evolveBtn.textContent = 'Evolve Favorites';
    evolveBtn.addEventListener('click', () => this.evolve());
    controls.appendChild(evolveBtn);

    const applyBtn = document.createElement('button');
    applyBtn.className = 'output-btn';
    applyBtn.textContent = 'Apply to Layer';
    applyBtn.addEventListener('click', () => this.applyToLayer());
    controls.appendChild(applyBtn);

    this.container.appendChild(controls);

    // Grid for variants
    this.grid = document.createElement('div');
    this.grid.className = 'shader-grid';
    this.container.appendChild(this.grid);

    // Empty state
    this.emptyState = document.createElement('div');
    this.emptyState.className = 'shader-lab-empty';
    this.emptyState.textContent = 'Enter a direction and click Generate';
    this.grid.appendChild(this.emptyState);
  }

  /**
   * Generate shader variants from direction
   * In the full implementation, this would call Claude via MCP
   * For now, creates parameter variations of the current shader
   */
  generate() {
    const direction = this.dirInput.value.trim() || 'abstract generative';
    this.generation++;
    this.emptyState.style.display = 'none';

    // Generate 4 variations by modifying the default shader parameters
    const baseSource = getLayer(state.selectedLayerId)?._isfSource || DEFAULT_SHADER;
    this.variants = [];

    for (let i = 0; i < 4; i++) {
      const variant = this._createVariant(baseSource, i);
      this.variants.push(variant);
    }

    this._renderGrid();
  }

  _createVariant(baseSource, index) {
    const id = `gen${this.generation}_${index}`;

    // Create mini canvas for preview
    const canvas = document.createElement('canvas');
    canvas.width = 320;
    canvas.height = 180;

    // Create mini renderer
    let renderer = null;
    try {
      const gl = canvas.getContext('webgl', { antialias: false, preserveDrawingBuffer: true });
      if (gl) {
        renderer = { gl, canvas };
        // Compile the base shader with slight modifications
        const { frag } = buildFragmentShader(baseSource);
        // Simple compilation for preview
        const vs = gl.createShader(gl.VERTEX_SHADER);
        gl.shaderSource(vs, VERT_SHADER);
        gl.compileShader(vs);
        const fs = gl.createShader(gl.FRAGMENT_SHADER);
        gl.shaderSource(fs, frag);
        gl.compileShader(fs);

        if (gl.getShaderParameter(fs, gl.COMPILE_STATUS)) {
          const prog = gl.createProgram();
          gl.attachShader(prog, vs);
          gl.attachShader(prog, fs);
          gl.bindAttribLocation(prog, 0, 'position');
          gl.linkProgram(prog);

          if (gl.getProgramParameter(prog, gl.LINK_STATUS)) {
            renderer.program = prog;
            // Setup geometry
            const buf = gl.createBuffer();
            gl.bindBuffer(gl.ARRAY_BUFFER, buf);
            gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1,-1, 3,-1, -1,3]), gl.STATIC_DRAW);
            renderer.buf = buf;
            renderer.startTime = performance.now() + index * 1000; // offset time per variant
          }
        }
        gl.deleteShader(vs);
        gl.deleteShader(fs);
      }
    } catch (e) {
      // Mini renderer creation failed, that's ok
    }

    return {
      id,
      source: baseSource,
      canvas,
      renderer,
      favorited: false,
    };
  }

  _renderGrid() {
    this.grid.innerHTML = '';

    this.variants.forEach(v => {
      const card = document.createElement('div');
      card.className = 'shader-variant' + (v.favorited ? ' selected' : '');
      card.dataset.variantId = v.id;

      // Canvas preview
      card.appendChild(v.canvas);

      // Label
      const label = document.createElement('div');
      label.className = 'shader-variant-label';
      label.textContent = v.id;
      card.appendChild(label);

      // Actions
      const actions = document.createElement('div');
      actions.className = 'shader-variant-actions';

      const favBtn = document.createElement('button');
      favBtn.className = 'output-btn' + (v.favorited ? ' active' : '');
      favBtn.textContent = v.favorited ? '♥' : '♡';
      favBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        v.favorited = !v.favorited;
        favBtn.textContent = v.favorited ? '♥' : '♡';
        favBtn.classList.toggle('active', v.favorited);
        card.classList.toggle('selected', v.favorited);
      });
      actions.appendChild(favBtn);

      card.appendChild(actions);
      this.grid.appendChild(card);
    });

    // Start preview animation
    this._animateVariants();
  }

  _animateVariants() {
    const animate = () => {
      if (!this.variants.length) return;

      this.variants.forEach(v => {
        if (!v.renderer || !v.renderer.program) return;
        const { gl, program, buf, startTime, canvas } = v.renderer;
        if (!gl || gl.isContextLost()) return;

        gl.viewport(0, 0, canvas.width, canvas.height);
        gl.clearColor(0, 0, 0, 1);
        gl.clear(gl.COLOR_BUFFER_BIT);
        gl.useProgram(program);

        const elapsed = (performance.now() - startTime) / 1000;
        const tLoc = gl.getUniformLocation(program, 'TIME');
        if (tLoc) gl.uniform1f(tLoc, elapsed);
        const rLoc = gl.getUniformLocation(program, 'RENDERSIZE');
        if (rLoc) gl.uniform2f(rLoc, canvas.width, canvas.height);
        const piLoc = gl.getUniformLocation(program, 'PASSINDEX');
        if (piLoc) gl.uniform1i(piLoc, 0);

        gl.bindBuffer(gl.ARRAY_BUFFER, buf);
        gl.enableVertexAttribArray(0);
        gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0);
        gl.drawArrays(gl.TRIANGLES, 0, 3);
      });

      this._animFrame = requestAnimationFrame(animate);
    };

    if (this._animFrame) cancelAnimationFrame(this._animFrame);
    animate();
  }

  evolve() {
    const favorites = this.variants.filter(v => v.favorited);
    if (favorites.length === 0) {
      // If nothing favorited, use all as base
      this.generate();
      return;
    }
    // In full implementation, send favorited shader code to Claude for evolution
    // For now, regenerate with favorites as base
    this.generation++;
    const newVariants = [];
    for (let i = 0; i < 4; i++) {
      const base = favorites[i % favorites.length];
      newVariants.push(this._createVariant(base.source, i));
    }
    this.variants = newVariants;
    this._renderGrid();
  }

  applyToLayer() {
    const favorites = this.variants.filter(v => v.favorited);
    const target = favorites.length > 0 ? favorites[0] : this.variants[0];
    if (target) {
      // Load the variant's shader into the selected layer
      window.shaderClaw?.loadSource(target.source);
    }
  }

  dispose() {
    if (this._animFrame) cancelAnimationFrame(this._animFrame);
    this.variants.forEach(v => {
      if (v.renderer && v.renderer.gl) {
        const gl = v.renderer.gl;
        const ext = gl.getExtension('WEBGL_lose_context');
        if (ext) ext.loseContext();
      }
    });
    this.variants = [];
  }
}

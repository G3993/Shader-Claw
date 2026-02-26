# ShaderClaw UI Refactor

You are refactoring the ShaderClaw UI. ShaderClaw is a real-time shader compositor at `C:\Users\nofun\Documents\ShaderClaw`. The entire UI currently lives in a single `index.html` (~4,540 lines) — HTML, CSS, and all JS in one file. The rendering engine (WebGL compositor, Three.js scene renderer) is solid and stays. You're redesigning the **chrome around it** — the panels, controls, workflow, and layout.

## Why

ShaderClaw just gained 6 "output destination" rule files that teach Claude where a shader is going: web apps, video production, social media, 3D compositions, code visualization, and live performance. The UI doesn't know about destinations yet. This refactor makes the UI destination-aware and fixes longstanding UX issues while we're in there.

Read the skill files to understand what ShaderClaw teaches Claude:
- `C:\Users\nofun\.claude\skills\shaderclaw\SKILL.md` — manifest of all 17 rule files
- `C:\Users\nofun\.claude\skills\shaderclaw\rules\prompt-pipeline.md` — the prompt-to-graphics pipeline
- `C:\Users\nofun\.claude\skills\shaderclaw\rules\design-language.md` — mood system + destination modifiers
- `C:\Users\nofun\.claude\skills\shaderclaw\rules\use-web.md` through `use-live.md` — the 6 destination files

These skills define what Claude knows. The UI should make the same concepts tangible for a human user.

## Current Architecture (what exists)

```
index.html (4,540 lines, everything in one file)
├── <style> block (~800 lines of CSS)
├── <body>
│   ├── #app (CSS grid: 1fr + 260px sidebar)
│   │   ├── #main (flex column)
│   │   │   ├── #preview (canvas container)
│   │   │   │   ├── #gl-canvas (WebGL — shader/ISF rendering)
│   │   │   │   ├── #three-canvas (Three.js — 3D scenes)
│   │   │   │   ├── .canvas-controls (params toggle, play/pause, fullscreen, copy, download)
│   │   │   │   └── #error-bar (compilation errors)
│   │   │   └── #editor-area (CodeMirror + toolbar)
│   │   └── #panels-container (floating, draggable, 280px)
│   │       ├── #inputs-panel (media import grid + media list + mask + bg select)
│   │       └── #params-panel (ISF parameter controls, generated dynamically)
│   └── #sidebar (260px right panel)
│       ├── .brand (logo + "SHADER CLAW")
│       ├── #shader-list (categorized: 3D Scenes | Shaders | Text)
│       └── #layer-panel (3 hardcoded cards: text/shader/scene)
│           └── .layer-card (visibility toggle, name, opacity slider, blend select)
├── #ndi-source-picker (modal overlay for NDI source selection)
└── <script> block (~3,700 lines of JS)
```

### Key state:
- `layers[]` — 3 layers (scene/shader/text), each with visibility, opacity, blendMode, FBO
- `mediaInputs[]` — imported media (images, videos, models, audio, webcam, NDI)
- `currentInputValues{}` — ISF parameter values
- `activeMode` — 'shader' or 'scene'
- Audio: `audioLevel`, `audioBass`, `audioMid`, `audioHigh`, `audioFFT` texture
- CSS vars: `--bg: #09090f`, `--panel: #111119`, `--accent: #e63946`, etc.

### Key functions:
- `loadSource(source)` — parse ISF, compile, generate param UI
- `loadScene(folder, file)` — load Three.js scene
- `generateControls(inputs, container, onChange)` — build parameter UI from ISF INPUTS
- `renderMediaList()` — rebuild media item DOM
- `renderCompositor(layers, sceneTexture)` — blend all layers
- `addMediaFromFile/Webcam/DataUrl()` — media import pipeline

## What to Build

### 1. Output Destination Selector

Add a destination concept to the UI. When the user picks a destination, the UI adapts:

**Destinations** (from the skill files):
| ID | Label | Icon idea | Key constraint |
|----|-------|-----------|----------------|
| `general` | General | ◆ | No constraints (default) |
| `web` | Web App | ◻ | Subtle, performant, exposes `subtle` uniform |
| `video` | Video | ▶ | Broadcast-safe colors, aspect ratio presets |
| `social` | Social | ◎ | Bold colors, phone-first, platform presets |
| `3d` | 3D Scene | △ | Background-object pairing guidance |
| `code` | Code Viz | ⌨ | IDE palettes, dark themes |
| `live` | Live / VJ | ♫ | Audio-reactive, NDI, high contrast |

**Where it goes:** Top of the sidebar, above the shader browser. Small horizontal pill buttons or a dropdown. Picking a destination should:
- Filter/reorder the shader browser to show relevant templates first
- Set canvas aspect ratio presets (show a ratio picker: 16:9, 9:16, 1:1, 4:5, 21:9)
- Show destination-specific warnings in the parameter panel (e.g., "speed > 0.5 not recommended for web")
- For `live`: auto-show audio controls and NDI panel
- For `video`: add a `transparentBg` quick toggle near the canvas
- For `social`: show platform sub-selector (TikTok, YouTube, Instagram, Twitter)

### 2. Aspect Ratio System

The canvas currently hardcodes 1920×1080. Add aspect ratio awareness:

- Ratio presets tied to destination (see `use-video.md` and `use-social.md` for the tables)
- Visual ratio picker near the canvas controls (small buttons: 16:9, 9:16, 1:1, 4:5)
- When ratio changes: resize the internal render resolution, update `RENDERSIZE` uniform, adjust canvas CSS
- The preview area should letterbox/pillarbox to show the actual output frame

### 3. Redesigned Sidebar

Current sidebar: brand header → flat shader list → layer cards (bottom).

New sidebar structure:
```
┌─────────────────────┐
│ SHADERCLAW          │  ← brand (smaller, tighter)
├─────────────────────┤
│ [destination pills] │  ← NEW: General | Web | Video | Social | ...
├─────────────────────┤
│ [aspect ratio]      │  ← NEW: 16:9 | 9:16 | 1:1 | 4:5
├─────────────────────┤
│ TEMPLATES           │  ← renamed from shader list, filterable
│ ┌─ 3D Scenes ─────┐│
│ │ Spinning Cube    ││
│ │ ...              ││
│ ├─ Backgrounds ────┤│  ← renamed from "Shaders"
│ │ Galaxy           ││
│ │ Gradient         ││
│ │ ...              ││
│ ├─ Text Effects ───┤│
│ │ Wave             ││
│ │ Digifade         ││
│ │ ...              ││
│ └──────────────────┘│
├─────────────────────┤
│ LAYERS              │  ← same 3 layer cards, cleaned up
│ ┌ Text ──── ○ 1.0 ┐│
│ ├ Shader ── ○ 1.0 ┤│
│ └ Scene ─── ○ 1.0 ┘│
├─────────────────────┤
│ EXPORT              │  ← NEW: destination-aware output
│ [NDI Send] [Save]   │
│ [Record] [Copy]     │
└─────────────────────┘
```

### 4. Inputs Panel Redesign

The floating `#inputs-panel` (media import grid + media list) should become a collapsible sidebar section or a better-organized floating panel:

- **Import grid**: Keep the tile buttons but group them better. Hide irrelevant tiles based on destination (e.g., hide NDI tile when destination is `web`)
- **Media list**: Each media item needs better controls — especially audio items (the play/pause + level bar is good, keep it)
- **Audio section**: When destination is `live`, promote audio controls to top-level visibility instead of buried in the media list

### 5. Parameter Panel Improvements

The `#params-panel` generates controls dynamically from ISF INPUTS via `generateControls()`. Improve it:

- **Destination warnings**: If the user sets a parameter outside the destination's recommended range (e.g., speed > 0.5 for web), show a subtle yellow warning under that control
- **Quick presets**: For each destination, offer 2-3 preset buttons that set multiple parameters at once (from the "Recipes" in each use-case file)
- **Group parameters**: ISF shaders can have many inputs. Group them: Core (speed, color, scale) → Style (pattern-specific) → Advanced (usually hidden)

### 6. Export / Output Section

Currently scattered: copy button in canvas controls, download in toolbar, NDI in a modal. Consolidate into an Export section:

- **For `general`**: Save shader (.fs), copy to clipboard, screenshot
- **For `web`**: Same + "Copy as CSS background" (generates a data URL or instructions)
- **For `video`**: Save as video loop (if we add recording), screenshot at specific frame, ProRes color warning
- **For `social`**: Platform-specific export with correct resolution
- **For `live`**: NDI send controls front and center, not buried in a modal

### 7. Editor Area

The CodeMirror editor at the bottom is fine but could be improved:

- Make it resizable (drag the divider between preview and editor)
- Add a "split view" option: editor on the left, preview on the right (instead of stacked)
- Keep the auto-compile toggle and compile button

## Technical Approach

### DO extract from the monolith:
- **CSS** → `style.css` (or a few CSS files if you want to split by component)
- **UI modules** → Separate JS files loaded as ES modules or bundled. Suggested split:
  - `ui/sidebar.js` — destination selector, template browser, layer cards
  - `ui/panels.js` — inputs panel, params panel, export section
  - `ui/editor.js` — CodeMirror setup, toolbar
  - `ui/canvas-controls.js` — play/pause, fullscreen, aspect ratio
  - `ui/ndi-picker.js` — NDI source modal
  - `ui/destination.js` — destination state, constraint logic, warnings
  - `state.js` — centralized state (layers, media, current values, destination)

### DO NOT touch:
- The `Renderer` class (WebGL compositor) — it works
- The `SceneRenderer` class (Three.js) — it works
- The ISF parser (`parseISF`, `buildFragmentShader`) — it works
- The audio analysis pipeline — it works
- The MCP tool interface (`window.shaderClaw.*`) — Claude needs this API stable
- `server.js` — the backend is fine

### State management:
You don't need React or a framework. A simple pub/sub pattern works:
```javascript
// state.js
const state = {
  destination: 'general',
  aspectRatio: '16:9',
  layers: [...],
  media: [...],
  currentShader: null,
  params: {}
};

function emit(event, data) { /* notify subscribers */ }
function on(event, callback) { /* subscribe */ }
```

UI modules subscribe to state changes and update DOM. This replaces the current pattern of scattered `getElementById` calls.

### CSS approach:
- Keep the existing CSS variables (`--bg`, `--panel`, `--accent`, etc.)
- Use CSS custom properties for destination-specific theming if desired
- The dark theme is good — keep it
- Tighten spacing, improve typography hierarchy

## Design Principles

1. **The canvas is the hero.** Every UI element exists to serve the preview. Minimize chrome.
2. **Destination shapes the workspace.** Picking "Live / VJ" should feel different from picking "Web App" — different controls visible, different defaults, different energy.
3. **Don't hide the code.** The editor is a first-class citizen, not an afterthought. Shader authors want to see and edit GLSL.
4. **Progressive disclosure.** Show the most important controls first. Advanced parameters behind a toggle. Destination warnings only when relevant.
5. **Match the skill files.** The UI vocabulary should match what Claude uses: "Templates" not "Shaders", "Destination" not "Output Format", mood names from `design-language.md`.

## Order of Operations

1. **Extract CSS** into `style.css` — mechanical, low risk
2. **Add destination state + selector UI** — the conceptual foundation
3. **Add aspect ratio system** — needed before layout changes
4. **Redesign sidebar** — destination pills, template browser, layer cards, export
5. **Redesign panels** — inputs, params with destination awareness
6. **Modularize JS** — extract UI logic into modules
7. **Add destination-specific behaviors** — warnings, presets, filtered templates
8. **Polish** — transitions, keyboard shortcuts, responsive tweaks

Screenshot the result after each major step. The MCP `screenshot` tool works — use it.

## Reference Files

| File | What to read it for |
|------|-------------------|
| `C:\Users\nofun\Documents\ShaderClaw\index.html` | The entire current codebase |
| `C:\Users\nofun\Documents\ShaderClaw\server.js` | Backend API (don't modify, but understand the bridge) |
| `C:\Users\nofun\Documents\ShaderClaw\shaders\manifest.json` | Available shader templates |
| `C:\Users\nofun\.claude\skills\shaderclaw\rules\use-web.md` | Web destination constraints |
| `C:\Users\nofun\.claude\skills\shaderclaw\rules\use-video.md` | Video destination constraints |
| `C:\Users\nofun\.claude\skills\shaderclaw\rules\use-social.md` | Social destination constraints |
| `C:\Users\nofun\.claude\skills\shaderclaw\rules\use-3d.md` | 3D destination constraints |
| `C:\Users\nofun\.claude\skills\shaderclaw\rules\use-code.md` | Code viz destination constraints |
| `C:\Users\nofun\.claude\skills\shaderclaw\rules\use-live.md` | Live destination constraints |
| `C:\Users\nofun\.claude\skills\shaderclaw\rules\design-language.md` | Mood system + destination modifiers |

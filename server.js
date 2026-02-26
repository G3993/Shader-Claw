// ShaderClaw MCP Server
// stdio MCP server + HTTP static server + WebSocket browser bridge
// Single process, single port.

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { createServer } from "http";
import { readFile, readdir, writeFile, mkdir, unlink } from "fs/promises";
import { join, extname, basename } from "path";
import { WebSocketServer } from "ws";
import { fileURLToPath } from "url";
import { dirname } from "path";
import { spawn } from "child_process";
import { existsSync } from "fs";
import grandi from "grandi";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const PORT = parseInt(process.env.PORT || process.env.SHADERCLAW_PORT || "7777", 10);

const log = (...args) => process.stderr.write(`[ShaderClaw] ${args.join(" ")}\n`);

// v2 layer IDs
const LAYER_IDS = ['background', 'media', '3d', 'av', 'effects', 'text', 'overlay'];
const LAYER_ID_ENUM = z.enum(LAYER_IDS);
const BLEND_MODES = ['normal', 'add', 'multiply', 'screen', 'overlay'];

// ============================================================
// MIME types for static serving
// ============================================================

const MIME = {
  ".html": "text/html",
  ".js": "application/javascript",
  ".json": "application/json",
  ".css": "text/css",
  ".fs": "text/plain",
  ".vs": "text/plain",
  ".png": "image/png",
  ".jpg": "image/jpeg",
  ".svg": "image/svg+xml",
};

// ============================================================
// Browser Bridge — WS connection + request/response with correlation IDs
// ============================================================

class BrowserBridge {
  constructor() {
    this.ws = null;
    this.nextId = 1;
    this.pending = new Map(); // id -> { resolve, reject, timer }
  }

  get connected() {
    return this.ws !== null && this.ws.readyState === 1; // WebSocket.OPEN
  }

  attach(ws) {
    // First-tab-wins: if already connected, close the old one
    if (this.ws && this.ws.readyState === 1) {
      log("New tab connected, replacing previous connection");
      this.ws.close();
    }

    this.ws = ws;
    log("Browser connected");

    ws.on("message", (data, isBinary) => {
      // Skip binary frames (handled separately for NDI)
      if (isBinary) return;
      try {
        const msg = JSON.parse(data.toString());

        // Handle NDI action requests from browser
        if (msg.action && msg.action.startsWith("ndi_")) {
          this._handleNdiAction(ws, msg);
          return;
        }

        // Handle pixel streaming requests from browser
        if (msg.action && msg.action.startsWith("pixel_stream_")) {
          this._handlePixelStreamAction(ws, msg);
          return;
        }

        const entry = this.pending.get(msg.id);
        if (!entry) return;

        clearTimeout(entry.timer);
        this.pending.delete(msg.id);

        if (msg.error) {
          entry.reject(new Error(msg.error));
        } else {
          entry.resolve(msg.result);
        }
      } catch (e) {
        log("Bad message from browser:", e.message);
      }
    });

    ws.on("close", () => {
      log("Browser disconnected");
      if (this.ws === ws) this.ws = null;
      // Reject all pending requests
      for (const [id, entry] of this.pending) {
        clearTimeout(entry.timer);
        entry.reject(new Error("Browser disconnected"));
      }
      this.pending.clear();
    });

    ws.on("error", (err) => {
      log("WS error:", err.message);
    });
  }

  async _handleNdiAction(ws, msg) {
    const { id, action, params } = msg;
    let result = null;
    let error = null;

    try {
      switch (action) {
        case "ndi_find_sources":
          result = { sources: await ndiGetSources() };
          break;
        case "ndi_receive_start":
          await ndiStartReceive(params.sourceName);
          result = { ok: true, sourceName: params.sourceName };
          break;
        case "ndi_receive_stop":
          ndiStopReceive();
          result = { ok: true };
          break;
        case "ndi_send_start":
          await ndiStartSend(params.name || "ShaderClaw", params.width || 1920, params.height || 1080);
          result = { ok: true };
          break;
        case "ndi_send_stop":
          ndiStopSend();
          result = { ok: true };
          break;
        case "ndi_send_tally":
          result = ndiGetTally();
          break;
        default:
          error = `Unknown NDI action: ${action}`;
      }
    } catch (e) {
      error = e.message;
    }
    ws.send(JSON.stringify({ id, result, error }));
  }

  async _handlePixelStreamAction(ws, msg) {
    const { id, action, params } = msg;
    let result = null;
    let error = null;

    try {
      switch (action) {
        case "pixel_stream_list_projects":
          result = { projects: await detectUEProjects() };
          break;
        case "pixel_stream_start":
          result = await startPixelStream(params.project);
          break;
        case "pixel_stream_stop":
          stopPixelStream();
          result = { ok: true };
          break;
        case "pixel_stream_status":
          result = pixelStreamState;
          break;
        default:
          error = `Unknown pixel stream action: ${action}`;
      }
    } catch (e) {
      error = e.message;
    }
    ws.send(JSON.stringify({ id, result, error }));
  }

  send(action, params = {}, timeoutMs = 10000) {
    return new Promise((resolve, reject) => {
      if (!this.connected) {
        return reject(new Error("No browser connected. Open http://localhost:" + PORT));
      }

      const id = this.nextId++;
      const timer = setTimeout(() => {
        this.pending.delete(id);
        reject(new Error(`Timeout waiting for browser response (action: ${action})`));
      }, timeoutMs);

      this.pending.set(id, { resolve, reject, timer });
      this.ws.send(JSON.stringify({ id, action, params }));
    });
  }
}

const bridge = new BrowserBridge();

// ============================================================
// NDI — find, receive, send via grandi
// ============================================================

let ndiFinder = null;
let ndiReceiver = null;
let ndiReceiverPump = false;
let ndiSender = null;
let ndiSendActive = false;

// Binary frame protocol constants
const FRAME_TYPE_NDI_VIDEO = 0x01; // server → browser
const FRAME_TYPE_CANVAS    = 0x02; // browser → server

async function ndiGetSources() {
  if (!ndiFinder) {
    ndiFinder = await grandi.find({ showLocalSources: true });
  }
  const sources = ndiFinder.sources();
  return sources.map(s => ({ name: s.name, urlAddress: s.urlAddress }));
}

async function ndiStartReceive(sourceName) {
  // Stop existing receiver
  ndiStopReceive();

  const sources = await ndiGetSources();
  const source = sources.find(s => s.name === sourceName);
  if (!source) throw new Error(`NDI source not found: ${sourceName}`);

  ndiReceiver = await grandi.receive({
    source: { name: source.name, urlAddress: source.urlAddress },
    colorFormat: grandi.COLOR_FORMAT_RGBX_RGBA,
    allowVideoFields: false,
  });

  ndiReceiverPump = true;
  log(`NDI receiving from: ${sourceName}`);

  // Start frame pump
  const pumpReceiver = ndiReceiver; // capture ref to detect destroy
  (async function pump() {
    while (ndiReceiverPump && ndiReceiver === pumpReceiver) {
      try {
        const frame = await pumpReceiver.video(100); // 100ms timeout
        if (!ndiReceiverPump || ndiReceiver !== pumpReceiver) break;
        if (frame && frame.data && bridge.connected && bridge.ws) {
          const header = Buffer.alloc(9);
          header[0] = FRAME_TYPE_NDI_VIDEO;
          header.writeUInt32LE(frame.xres, 1);
          header.writeUInt32LE(frame.yres, 5);
          const msg = Buffer.concat([header, Buffer.from(frame.data)]);
          try {
            bridge.ws.send(msg);
          } catch (e) {
            // WebSocket send error — browser might be gone
          }
        }
      } catch (e) {
        // Timeout is normal — just means no frame this interval
        if (!ndiReceiverPump || ndiReceiver !== pumpReceiver) break;
      }
    }
  })();
}

function ndiStopReceive() {
  ndiReceiverPump = false;
  if (ndiReceiver) {
    try { ndiReceiver.destroy(); } catch (e) {}
    ndiReceiver = null;
    log("NDI receiver stopped");
  }
}

async function ndiStartSend(name = "ShaderClaw", width = 1920, height = 1080) {
  ndiStopSend();
  ndiSender = await grandi.send({ name });
  ndiSendActive = true;
  ndiSender._width = width;
  ndiSender._height = height;
  log(`NDI sending as: ${name} (${width}x${height})`);
}

function ndiStopSend() {
  ndiSendActive = false;
  if (ndiSender) {
    try { ndiSender.destroy(); } catch (e) {}
    ndiSender = null;
    log("NDI sender stopped");
  }
}

let _ndiFrameCount = 0;
function ndiHandleCanvasFrame(data) {
  if (!ndiSender || !ndiSendActive) return;
  if (++_ndiFrameCount % 30 === 1) log(`NDI send: frame ${_ndiFrameCount}, ${data.length} bytes`);
  // data is raw buffer: [0x02][width LE 4][height LE 4][RGBA pixels]
  const width = data.readUInt32LE(1);
  const height = data.readUInt32LE(5);
  const pixels = data.slice(9);

  try {
    ndiSender.video({
      data: pixels,
      fourCC: grandi.FOURCC_RGBA,
      xres: width,
      yres: height,
      frameRateN: 30000,
      frameRateD: 1001,
      lineStrideBytes: width * 4,
      pictureAspectRatio: width / height,
      frameFormatType: grandi.FORMAT_TYPE_PROGRESSIVE,
    });
  } catch (e) {
    // send error
  }
}

function ndiGetTally() {
  if (!ndiSender) return { onProgram: false, onPreview: false };
  try {
    const tally = ndiSender.tally(0); // non-blocking
    return tally || { onProgram: false, onPreview: false };
  } catch (e) {
    return { onProgram: false, onPreview: false };
  }
}

// Cleanup NDI on process exit
function ndiCleanup() {
  ndiStopReceive();
  ndiStopSend();
  if (ndiFinder) {
    try { ndiFinder.destroy(); } catch (e) {}
    ndiFinder = null;
  }
}
process.on("exit", ndiCleanup);

// ============================================================
// HTTP Static Server
// ============================================================

const httpServer = createServer(async (req, res) => {
  let urlPath = req.url.split("?")[0];
  if (urlPath === "/") urlPath = "/index.html";

  const filePath = join(__dirname, urlPath);

  // Security: prevent path traversal
  if (!filePath.startsWith(__dirname)) {
    res.writeHead(403);
    res.end("Forbidden");
    return;
  }

  try {
    const data = await readFile(filePath);
    const ext = extname(filePath);
    res.writeHead(200, { "Content-Type": MIME[ext] || "application/octet-stream", "Cache-Control": "no-cache, no-store, must-revalidate" });
    res.end(data);
  } catch {
    res.writeHead(404);
    res.end("Not found");
  }
});

// ============================================================
// WebSocket Server — attach to HTTP server
// ============================================================

const wss = new WebSocketServer({ server: httpServer });

wss.on("connection", (ws) => {
  bridge.attach(ws);

  // Handle binary frames from browser (NDI send)
  ws.on("message", (data, isBinary) => {
    if (isBinary && data.length > 9 && data[0] === FRAME_TYPE_CANVAS) {
      ndiHandleCanvasFrame(data);
    }
  });

  // Refresh dynamic tools when browser connects
  setTimeout(async () => {
    if (!bridge.connected) return;
    try {
      const result = await bridge.send("get_parameters");
      if (result.inputs) registerDynamicTools(result.inputs);
    } catch (e) {
      log("Auto-refresh tools on connect failed:", e.message);
    }
  }, 500);
});

// ============================================================
// Controllability Scoring
// ============================================================

function scoreControllability(inputs) {
  if (!inputs || inputs.length === 0) return { score: 0, breakdown: { count: 0, diversity: 0, rangeQuality: 0, naming: 0 } };

  // Count (0-3): number of ISF inputs
  const count = Math.min(3, inputs.length);

  // Diversity (0-3): distinct input types used
  const types = new Set(inputs.map((i) => i.TYPE));
  const diversity = Math.min(3, types.size);

  // Range quality (0-2): floats with min/max/default
  const floats = inputs.filter((i) => i.TYPE === "float");
  let rangeQuality = 0;
  if (floats.length > 0) {
    const wellDefined = floats.filter(
      (f) => f.MIN != null && f.MAX != null && f.DEFAULT != null
    ).length;
    rangeQuality = Math.round((wellDefined / floats.length) * 2);
  } else {
    rangeQuality = 1; // neutral if no floats
  }

  // Naming (0-2): descriptive parameter names (>2 chars)
  const wellNamed = inputs.filter((i) => i.NAME && i.NAME.length > 2).length;
  const naming = inputs.length > 0 ? Math.round((wellNamed / inputs.length) * 2) : 0;

  const score = count + diversity + rangeQuality + naming;
  return { score, breakdown: { count, diversity, rangeQuality, naming } };
}

// ============================================================
// Helper: read manifest from disk
// ============================================================

async function readManifest() {
  const data = await readFile(join(__dirname, "shaders", "manifest.json"), "utf-8");
  return JSON.parse(data);
}

// ============================================================
// Dynamic Tools + Presets
// ============================================================

let currentInputs = [];
const dynamicToolNames = new Set();
const PRESETS_DIR = join(__dirname, "presets");

async function ensurePresetsDir() {
  try { await mkdir(PRESETS_DIR, { recursive: true }); } catch {}
}

// Late-bound ref — assigned after mcp is created
let mcp;

function registerDynamicTools(inputs) {
  // Remove old dynamic tools
  for (const name of dynamicToolNames) {
    delete mcp._registeredTools[name];
  }
  dynamicToolNames.clear();

  currentInputs = inputs || [];

  for (const input of currentInputs) {
    const toolName = `param_${input.name}`;
    let schema, description;

    switch (input.type) {
      case 'float': {
        let val = z.number();
        if (input.min != null) val = val.min(input.min);
        if (input.max != null) val = val.max(input.max);
        schema = { value: val.describe(`${input.min ?? 0}..${input.max ?? 1}, default: ${input.default}`) };
        description = `Set ${input.name} (${input.min ?? 0} to ${input.max ?? 1}, default ${input.default})`;
        break;
      }
      case 'bool':
        schema = { value: z.boolean() };
        description = `Toggle ${input.name} on/off`;
        break;
      case 'color':
        schema = { value: z.array(z.number()).length(4).describe('[r, g, b, a] each 0.0-1.0') };
        description = `Set ${input.name} color [r,g,b,a]`;
        break;
      case 'long': {
        const labels = input.labels || [];
        const values = input.values || [];
        const enumDesc = values.map((v, i) => `${v}=${labels[i] || v}`).join(', ');
        schema = { value: z.union([z.number(), z.string()]).describe(`Numeric value or label name. Options: ${enumDesc}`) };
        description = `Set ${input.name} (${labels.join(' | ')})`;
        break;
      }
      case 'text':
        schema = { value: z.string().max(input.maxLength || 256).describe(`Text, max ${input.maxLength || 256} chars`) };
        description = `Set ${input.name} text`;
        break;
      default:
        continue;
    }

    const capturedInput = { ...input };
    dynamicToolNames.add(toolName);

    mcp.tool(toolName, description, schema, async ({ value }) => {
      try {
        let resolvedValue = value;
        if (capturedInput.type === 'long' && typeof value === 'string') {
          const idx = (capturedInput.labels || []).findIndex(l => l.toLowerCase() === value.toLowerCase());
          if (idx >= 0) {
            resolvedValue = capturedInput.values[idx];
          } else {
            return { content: [{ type: "text", text: `Unknown label: "${value}". Available: ${(capturedInput.labels || []).join(', ')}` }], isError: true };
          }
        }
        const result = await bridge.send("set_parameter", { name: capturedInput.name, value: resolvedValue });
        return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
      } catch (e) {
        return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
      }
    });
  }

  // Notify client that tool list changed
  try { mcp.sendToolListChanged(); } catch (e) {
    log("Could not send tools/list_changed:", e.message);
  }
}

// ============================================================
// MCP Server
// ============================================================

mcp = new McpServer({
  name: "shaderclaw",
  version: "1.0.0",
});

// --- load_shader ---
mcp.tool(
  "load_shader",
  "Push an ISF shader to the browser editor, compile it, and return status + errors + inputs",
  { code: z.string().describe("Full ISF shader source code (metadata JSON block + GLSL body)") },
  async ({ code }) => {
    try {
      const result = await bridge.send("load_shader", { code });
      if (result.inputs) registerDynamicTools(result.inputs);
      return {
        content: [{ type: "text", text: JSON.stringify(result, null, 2) }],
      };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- get_shader ---
mcp.tool(
  "get_shader",
  "Read the current shader source code from the browser editor",
  {},
  async () => {
    try {
      const result = await bridge.send("get_shader");
      return { content: [{ type: "text", text: result.code }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- set_parameter ---
mcp.tool(
  "set_parameter",
  "Adjust a shader uniform in real-time (float, color, bool, point2D)",
  {
    name: z.string().describe("Parameter name as defined in ISF INPUTS"),
    value: z.union([
      z.number(),
      z.boolean(),
      z.array(z.number()),
    ]).describe("Value: number for float, boolean for bool, [r,g,b,a] for color, [x,y] for point2D"),
  },
  async ({ name, value }) => {
    try {
      const result = await bridge.send("set_parameter", { name, value });
      return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- get_parameters ---
mcp.tool(
  "get_parameters",
  "List all ISF inputs with current values, types, and ranges. Also refreshes per-parameter tools.",
  {},
  async () => {
    try {
      const result = await bridge.send("get_parameters");
      if (result.inputs) registerDynamicTools(result.inputs);
      return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- screenshot ---
mcp.tool(
  "screenshot",
  "Capture the WebGL canvas as a base64 PNG image. Returns an image content block that Claude can see directly.",
  {},
  async () => {
    try {
      const result = await bridge.send("screenshot");
      // result.dataUrl is "data:image/png;base64,..."
      const base64 = result.dataUrl.replace(/^data:image\/png;base64,/, "");
      return {
        content: [
          { type: "image", data: base64, mimeType: "image/png" },
        ],
      };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- list_templates ---
mcp.tool(
  "list_templates",
  "List all built-in ISF shader templates (reads manifest from disk, no browser needed)",
  {},
  async () => {
    try {
      const manifest = await readManifest();
      const list = manifest.map((item) => ({
        id: item.id,
        title: item.title,
        description: item.description,
        type: item.type,
        categories: item.categories,
      }));
      return { content: [{ type: "text", text: JSON.stringify(list, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- load_template ---
mcp.tool(
  "load_template",
  "Load a built-in template shader by title or ID into the browser",
  {
    name: z.union([z.string(), z.number()]).describe("Template title (case-insensitive) or numeric ID"),
  },
  async ({ name }) => {
    try {
      const manifest = await readManifest();
      let entry;

      if (typeof name === "number") {
        entry = manifest.find((m) => m.id === name);
      } else {
        // Try exact match first, then case-insensitive
        entry = manifest.find((m) => m.title === name) ||
                manifest.find((m) => m.title.toLowerCase() === name.toLowerCase());
      }

      if (!entry) {
        return { content: [{ type: "text", text: `Template not found: ${name}` }], isError: true };
      }

      const code = await readFile(join(__dirname, "shaders", entry.file), "utf-8");
      const result = await bridge.send("load_shader", { code });
      if (result.inputs) registerDynamicTools(result.inputs);
      return {
        content: [{
          type: "text",
          text: JSON.stringify({ template: entry.title, ...result }, null, 2),
        }],
      };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- get_errors ---
mcp.tool(
  "get_errors",
  "Get current compilation errors from the browser, if any",
  {},
  async () => {
    try {
      const result = await bridge.send("get_errors");
      return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- set_layer_visibility ---
mcp.tool(
  "set_layer_visibility",
  "Toggle visibility of a composition layer",
  {
    layerId: LAYER_ID_ENUM.describe("Layer to toggle"),
    visible: z.boolean().describe("Whether the layer should be visible"),
  },
  async ({ layerId, visible }) => {
    try {
      const result = await bridge.send("set_layer_visibility", { layerId, visible });
      return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- set_layer_opacity ---
mcp.tool(
  "set_layer_opacity",
  "Set the opacity (0-1) of a composition layer",
  {
    layerId: LAYER_ID_ENUM.describe("Layer to adjust"),
    opacity: z.number().min(0).max(1).describe("Opacity value 0.0 to 1.0"),
  },
  async ({ layerId, opacity }) => {
    try {
      const result = await bridge.send("set_layer_opacity", { layerId, opacity });
      return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- set_layer_blend ---
mcp.tool(
  "set_layer_blend",
  "Set the blend mode of a composition layer",
  {
    layerId: LAYER_ID_ENUM.describe("Layer to adjust"),
    blendMode: z.enum(BLEND_MODES).describe("Blend mode"),
  },
  async ({ layerId, blendMode }) => {
    try {
      const result = await bridge.send("set_layer_blend", { layerId, blendMode });
      return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- load_shader_to_layer ---
mcp.tool(
  "load_shader_to_layer",
  "Load an ISF shader to a specific layer. The shader compiles and renders in that layer's FBO.",
  {
    layerId: LAYER_ID_ENUM.describe("Target layer"),
    code: z.string().describe("Full ISF shader source code"),
  },
  async ({ layerId, code }) => {
    try {
      const result = await bridge.send("load_shader_to_layer", { layerId, code });
      if (result.inputs) registerDynamicTools(result.inputs);
      return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- get_layers ---
mcp.tool(
  "get_layers",
  "Get all 7 composition layers with their current state (visibility, opacity, blend mode, shader name)",
  {},
  async () => {
    try {
      const result = await bridge.send("get_layers");
      return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- enable_mediapipe ---
mcp.tool(
  "enable_mediapipe",
  "Enable Google MediaPipe ML detection (hand, face, pose, segmentation). Auto-enables webcam. Results available as shader uniforms (mpHandPos, mpHandLandmarks, etc.)",
  {
    modes: z.object({
      hand: z.boolean().optional().describe("Enable hand landmark detection"),
      face: z.boolean().optional().describe("Enable face landmark detection"),
      pose: z.boolean().optional().describe("Enable pose landmark detection"),
      segment: z.boolean().optional().describe("Enable selfie segmentation"),
    }).describe("Which MediaPipe modes to enable"),
  },
  async ({ modes }) => {
    try {
      const result = await bridge.send("enable_mediapipe", { modes }, 60000); // 60s timeout for model download
      return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- evaluate ---
mcp.tool(
  "evaluate",
  "Evaluate the current shader: returns controllability score (0-10) + screenshot for visual assessment. Claude can judge prompt adherence and aesthetic quality from the image.",
  {
    description: z.string().optional().describe("What the shader is supposed to look like (for adherence evaluation)"),
  },
  async ({ description }) => {
    try {
      // Get parameters for controllability scoring
      const params = await bridge.send("get_parameters");
      const controllability = scoreControllability(params.inputs);

      // Get screenshot
      const screenshotResult = await bridge.send("screenshot");
      const base64 = screenshotResult.dataUrl.replace(/^data:image\/png;base64,/, "");

      const evalText = {
        controllability,
        description: description || "(no description provided)",
        parameterCount: params.inputs ? params.inputs.length : 0,
        parameterTypes: params.inputs ? [...new Set(params.inputs.map((i) => i.type))].join(", ") : "none",
      };

      return {
        content: [
          { type: "text", text: JSON.stringify(evalText, null, 2) },
          { type: "image", data: base64, mimeType: "image/png" },
        ],
      };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- ndi_send_start ---
mcp.tool(
  "ndi_send_start",
  "Start broadcasting the ShaderClaw canvas as an NDI source visible to OBS, vMix, Resolume, etc.",
  {
    name: z.string().optional().describe("NDI source name (default: 'ShaderClaw')"),
  },
  async ({ name }) => {
    try {
      await ndiStartSend(name || "ShaderClaw");
      return { content: [{ type: "text", text: JSON.stringify({ ok: true, name: name || "ShaderClaw" }, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- ndi_send_stop ---
mcp.tool(
  "ndi_send_stop",
  "Stop NDI output",
  {},
  async () => {
    try {
      ndiStopSend();
      return { content: [{ type: "text", text: JSON.stringify({ ok: true }, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- ndi_list_sources ---
mcp.tool(
  "ndi_list_sources",
  "Discover NDI sources on the network (cameras, OBS, vMix, other apps)",
  {},
  async () => {
    try {
      const sources = await ndiGetSources();
      return { content: [{ type: "text", text: JSON.stringify({ ok: true, sources }, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- ndi_receive ---
mcp.tool(
  "ndi_receive",
  "Start receiving an NDI source as a media input (becomes a texture uniform in shaders)",
  {
    sourceName: z.string().describe("NDI source name from ndi_list_sources"),
  },
  async ({ sourceName }) => {
    try {
      await ndiStartReceive(sourceName);
      return { content: [{ type: "text", text: JSON.stringify({ ok: true, sourceName }, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- ndi_receive_stop ---
mcp.tool(
  "ndi_receive_stop",
  "Stop receiving NDI source",
  {},
  async () => {
    try {
      ndiStopReceive();
      return { content: [{ type: "text", text: JSON.stringify({ ok: true }, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- configure (bulk parameter set) ---
mcp.tool(
  "configure",
  "Set multiple shader parameters at once. Accepts parameter names as keys. For enum (long) params, you can pass the label string instead of a number.",
  {
    params: z.record(z.string(), z.union([z.number(), z.boolean(), z.string(), z.array(z.number())])).describe("Object mapping parameter names to values"),
  },
  async ({ params }) => {
    const results = [];
    for (const [name, value] of Object.entries(params)) {
      try {
        let resolvedValue = value;
        const input = currentInputs.find(i => i.name === name);
        if (input && input.type === 'long' && typeof value === 'string') {
          const idx = (input.labels || []).findIndex(l => l.toLowerCase() === value.toLowerCase());
          if (idx >= 0) resolvedValue = input.values[idx];
          else { results.push({ name, error: `Unknown label "${value}"` }); continue; }
        }
        await bridge.send("set_parameter", { name, value: resolvedValue });
        results.push({ name, ok: true });
      } catch (e) {
        results.push({ name, error: e.message });
      }
    }
    return { content: [{ type: "text", text: JSON.stringify(results, null, 2) }] };
  }
);

// --- save_preset ---
mcp.tool(
  "save_preset",
  "Save current shader parameters as a named preset to disk",
  {
    name: z.string().describe("Preset name"),
    description: z.string().optional().describe("Optional description of the look/effect"),
  },
  async ({ name, description }) => {
    try {
      await ensurePresetsDir();
      const params = await bridge.send("get_parameters");
      const preset = {
        name,
        description: description || "",
        timestamp: new Date().toISOString(),
        parameters: {},
      };
      for (const input of (params.inputs || [])) {
        preset.parameters[input.name] = input.value;
      }
      const filename = name.replace(/[^a-zA-Z0-9_-]/g, '_') + '.json';
      await writeFile(join(PRESETS_DIR, filename), JSON.stringify(preset, null, 2));
      return { content: [{ type: "text", text: `Preset "${name}" saved (${Object.keys(preset.parameters).length} parameters)` }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- load_preset ---
mcp.tool(
  "load_preset",
  "Load a saved preset, restoring all parameter values",
  {
    name: z.string().describe("Preset name"),
  },
  async ({ name }) => {
    try {
      await ensurePresetsDir();
      const filename = name.replace(/[^a-zA-Z0-9_-]/g, '_') + '.json';
      const data = await readFile(join(PRESETS_DIR, filename), 'utf-8');
      const preset = JSON.parse(data);
      const results = [];
      for (const [paramName, value] of Object.entries(preset.parameters)) {
        try {
          await bridge.send("set_parameter", { name: paramName, value });
          results.push({ name: paramName, ok: true });
        } catch (e) {
          results.push({ name: paramName, error: e.message });
        }
      }
      return { content: [{ type: "text", text: `Loaded preset "${preset.name}": ${results.filter(r => r.ok).length}/${results.length} params set` }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- list_presets ---
mcp.tool(
  "list_presets",
  "List all saved parameter presets",
  {},
  async () => {
    try {
      await ensurePresetsDir();
      const files = await readdir(PRESETS_DIR);
      const presets = [];
      for (const file of files) {
        if (!file.endsWith('.json')) continue;
        try {
          const data = await readFile(join(PRESETS_DIR, file), 'utf-8');
          const p = JSON.parse(data);
          presets.push({ name: p.name, description: p.description, timestamp: p.timestamp });
        } catch {}
      }
      return { content: [{ type: "text", text: presets.length > 0 ? JSON.stringify(presets, null, 2) : "No presets saved yet." }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- delete_preset ---
mcp.tool(
  "delete_preset",
  "Delete a saved preset",
  {
    name: z.string().describe("Preset name to delete"),
  },
  async ({ name }) => {
    try {
      const filename = name.replace(/[^a-zA-Z0-9_-]/g, '_') + '.json';
      await unlink(join(PRESETS_DIR, filename));
      return { content: [{ type: "text", text: `Preset "${name}" deleted` }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- create_parameter ---
mcp.tool(
  "create_parameter",
  "Add a new ISF input parameter to the current shader. The parameter becomes available as a GLSL uniform immediately.",
  {
    name: z.string().describe("Parameter name (camelCase, e.g. 'waveAmount')"),
    type: z.enum(["float", "bool", "color", "long", "text", "point2D"]).describe("ISF parameter type"),
    defaultValue: z.union([z.number(), z.boolean(), z.array(z.number()), z.string()]).describe("Default value"),
    min: z.number().optional().describe("Minimum (float only)"),
    max: z.number().optional().describe("Maximum (float only)"),
    values: z.array(z.number()).optional().describe("Allowed numeric values (long/enum only)"),
    labels: z.array(z.string()).optional().describe("Display labels for values (long/enum only)"),
  },
  async ({ name, type, defaultValue, min, max, values, labels }) => {
    try {
      const shaderResult = await bridge.send("get_shader");
      const source = shaderResult.code;
      const match = source.match(/^\/\*(\{[\s\S]*?\})\*\//);
      if (!match) return { content: [{ type: "text", text: "Could not parse ISF header" }], isError: true };

      const header = JSON.parse(match[1]);
      if (header.INPUTS.some(i => i.NAME === name)) {
        return { content: [{ type: "text", text: `Parameter "${name}" already exists` }], isError: true };
      }

      const newInput = { NAME: name, TYPE: type, DEFAULT: defaultValue };
      if (type === 'float') {
        if (min != null) newInput.MIN = min;
        if (max != null) newInput.MAX = max;
      }
      if (type === 'long') {
        if (values) newInput.VALUES = values;
        if (labels) newInput.LABELS = labels;
      }
      if (type === 'text' && typeof defaultValue === 'string') {
        newInput.MAX_LENGTH = Math.max(defaultValue.length * 2, 24);
      }
      header.INPUTS.push(newInput);

      const newSource = `/*${JSON.stringify(header, null, 2)}*/` + source.slice(match[0].length);
      const loadResult = await bridge.send("load_shader", { code: newSource });
      if (loadResult.inputs) registerDynamicTools(loadResult.inputs);

      return { content: [{ type: "text", text: `Parameter "${name}" (${type}) added. Use uniform ${name} in GLSL.` }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// ============================================================
// Node Graph Tools
// ============================================================

// --- load_graph ---
mcp.tool(
  "load_graph",
  "Load a node graph JSON to a layer. The graph is compiled to ISF/GLSL and replaces the layer's shader.",
  {
    layerId: LAYER_ID_ENUM.describe("Target layer"),
    graph: z.object({
      nodes: z.array(z.any()),
      edges: z.array(z.any()),
    }).describe("Node graph DAG: { nodes: [...], edges: [...] }"),
  },
  async ({ layerId, graph }) => {
    try {
      const result = await bridge.send("load_graph", { layerId, graph });
      return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- get_graph ---
mcp.tool(
  "get_graph",
  "Get the current node graph for a layer (if using node graph mode)",
  {
    layerId: LAYER_ID_ENUM.describe("Layer to query"),
  },
  async ({ layerId }) => {
    try {
      const result = await bridge.send("get_graph", { layerId });
      return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- add_node ---
mcp.tool(
  "add_node",
  "Add a node to the node graph of a layer",
  {
    layerId: LAYER_ID_ENUM.describe("Target layer"),
    type: z.string().describe("Node type (e.g. 'simplex_noise', 'color_ramp', 'add', 'output')"),
    position: z.array(z.number()).length(2).optional().describe("[x, y] position on canvas"),
    params: z.record(z.string(), z.any()).optional().describe("Node parameters"),
  },
  async ({ layerId, type, position, params }) => {
    try {
      const result = await bridge.send("add_node", { layerId, type, position, params });
      return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- connect_nodes ---
mcp.tool(
  "connect_nodes",
  "Connect two nodes in the node graph",
  {
    layerId: LAYER_ID_ENUM.describe("Target layer"),
    from: z.string().describe("Source node ID"),
    output: z.string().describe("Output port name"),
    to: z.string().describe("Destination node ID"),
    input: z.string().describe("Input port name"),
  },
  async ({ layerId, from, output, to, input }) => {
    try {
      const result = await bridge.send("connect_nodes", { layerId, from, output, to, input });
      return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- remove_node ---
mcp.tool(
  "remove_node",
  "Remove a node from the node graph",
  {
    layerId: LAYER_ID_ENUM.describe("Target layer"),
    nodeId: z.string().describe("Node ID to remove"),
  },
  async ({ layerId, nodeId }) => {
    try {
      const result = await bridge.send("remove_node", { layerId, nodeId });
      return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// ============================================================
// Pixel Streaming Tools
// ============================================================

const UE_PROJECTS_DIR = "C:\\Users\\nofun\\Documents\\Unreal Projects";
const PIXEL_STREAMING_INFRA = "C:\\Users\\nofun\\Documents\\PixelStreamingInfrastructure";

let signalingProcess = null;
let ueProcess = null;
let pixelStreamState = { status: 'idle', project: null };

async function detectUEProjects() {
  const projects = [];
  try {
    const entries = await readdir(UE_PROJECTS_DIR, { withFileTypes: true });
    for (const entry of entries) {
      if (!entry.isDirectory()) continue;
      const projDir = join(UE_PROJECTS_DIR, entry.name);
      const files = await readdir(projDir);
      const uproject = files.find(f => f.endsWith('.uproject'));
      if (uproject) {
        projects.push({
          name: entry.name,
          path: join(projDir, uproject),
        });
      }
    }
  } catch (e) {
    log("Error detecting UE projects:", e.message);
  }
  return projects;
}

async function startPixelStream(projectName) {
  const projects = await detectUEProjects();
  const project = projects.find(p => p.name.toLowerCase() === projectName.toLowerCase());
  if (!project) throw new Error(`UE project not found: ${projectName}. Available: ${projects.map(p => p.name).join(', ')}`);

  // Stop any existing stream
  stopPixelStream();

  pixelStreamState = { status: 'connecting', project: projectName };

  // Start signaling server
  const signalingScript = join(PIXEL_STREAMING_INFRA, "SignallingWebServer", "dist", "index.js");
  const wwwRoot = join(PIXEL_STREAMING_INFRA, "SignallingWebServer", "www");

  if (!existsSync(signalingScript)) {
    throw new Error(`Signaling server not found at ${signalingScript}`);
  }

  signalingProcess = spawn("node", [
    signalingScript,
    "--serve",
    "--http_root", wwwRoot,
    "--player_port", "8080",
    "--streamer_port", "8888",
  ], {
    stdio: ['ignore', 'pipe', 'pipe'],
    detached: false,
  });

  signalingProcess.stdout.on('data', d => log("[Signaling]", d.toString().trim()));
  signalingProcess.stderr.on('data', d => log("[Signaling]", d.toString().trim()));
  signalingProcess.on('exit', (code) => {
    log(`Signaling server exited with code ${code}`);
    if (pixelStreamState.status !== 'idle') {
      pixelStreamState.status = 'error';
    }
  });

  // Wait for signaling to be ready
  await new Promise(resolve => setTimeout(resolve, 2000));

  // Find UE executable
  const uePaths = [
    "C:\\Program Files\\Epic Games\\UE_5.7\\Engine\\Binaries\\Win64\\UnrealEditor.exe",
    "C:\\Program Files\\Epic Games\\UE_5.5\\Engine\\Binaries\\Win64\\UnrealEditor.exe",
  ];
  const ueExe = uePaths.find(p => existsSync(p));
  if (!ueExe) {
    throw new Error("Unreal Engine executable not found");
  }

  // Launch UE with pixel streaming
  ueProcess = spawn(ueExe, [
    project.path,
    "-game",
    "-PixelStreamingURL=ws://127.0.0.1:8888",
    "-RenderOffscreen",
  ], {
    stdio: 'ignore',
    detached: true,
  });

  ueProcess.unref();
  ueProcess.on('exit', (code) => {
    log(`UE process exited with code ${code}`);
    if (pixelStreamState.status !== 'idle') {
      pixelStreamState.status = 'idle';
    }
  });

  pixelStreamState = { status: 'streaming', project: projectName };
  log(`Pixel streaming started for ${projectName}`);
  return { ok: true, project: projectName, signalingPort: 8080 };
}

function stopPixelStream() {
  if (ueProcess) {
    try { ueProcess.kill(); } catch {}
    ueProcess = null;
  }
  if (signalingProcess) {
    try { signalingProcess.kill(); } catch {}
    signalingProcess = null;
  }
  pixelStreamState = { status: 'idle', project: null };
  log("Pixel streaming stopped");
}

// --- pixel_stream_list_projects ---
mcp.tool(
  "pixel_stream_list_projects",
  "List available Unreal Engine projects for pixel streaming",
  {},
  async () => {
    try {
      const projects = await detectUEProjects();
      return { content: [{ type: "text", text: JSON.stringify({ projects }, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- pixel_stream_start ---
mcp.tool(
  "pixel_stream_start",
  "Start pixel streaming with a UE project. Launches signaling server + UE in -game mode.",
  {
    project: z.string().describe("UE project name (e.g. 'VJ', 'zero', 'STUDIO')"),
  },
  async ({ project }) => {
    try {
      const result = await startPixelStream(project);
      return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- pixel_stream_stop ---
mcp.tool(
  "pixel_stream_stop",
  "Stop pixel streaming (kills signaling server + UE process)",
  {},
  async () => {
    try {
      stopPixelStream();
      return { content: [{ type: "text", text: JSON.stringify({ ok: true }, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- pixel_stream_status ---
mcp.tool(
  "pixel_stream_status",
  "Get current pixel streaming status",
  {},
  async () => {
    return { content: [{ type: "text", text: JSON.stringify(pixelStreamState, null, 2) }] };
  }
);

// ============================================================
// Media Routing Tools
// ============================================================

// --- route_media ---
mcp.tool(
  "route_media",
  "Route a media input (webcam, NDI, video, image) to a specific layer's image input slot",
  {
    mediaId: z.union([z.string(), z.number()]).describe("Media input ID"),
    layerId: LAYER_ID_ENUM.describe("Target layer"),
    slot: z.string().optional().describe("Image input name on the layer (defaults to first available)"),
  },
  async ({ mediaId, layerId, slot }) => {
    try {
      const result = await bridge.send("route_media", { mediaId, layerId, slot });
      return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// ============================================================
// Destination Tools
// ============================================================

// --- set_destination ---
mcp.tool(
  "set_destination",
  "Set the output destination which shapes UI constraints and shader guidelines (general, web, video, social, 3d, code, live)",
  {
    destination: z.enum(["general", "web", "video", "social", "3d", "code", "live"]).describe("Output destination"),
  },
  async ({ destination }) => {
    try {
      const result = await bridge.send("set_destination", { destination });
      return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- get_destination ---
mcp.tool(
  "get_destination",
  "Get the current output destination setting",
  {},
  async () => {
    try {
      const result = await bridge.send("get_destination");
      return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// ============================================================
// AI Breeding Tools
// ============================================================

// --- generate_variations ---
mcp.tool(
  "generate_variations",
  "Generate shader variations from a creative direction. Returns the direction to the browser which triggers the AI breeding UI.",
  {
    direction: z.string().describe("Creative direction (e.g. 'cosmic purple nebula', 'glitch art cyberpunk')"),
    count: z.number().min(2).max(8).optional().describe("Number of variations (default: 4)"),
    destination: z.enum(["general", "web", "video", "social", "3d", "code", "live"]).optional().describe("Output destination constraints"),
  },
  async ({ direction, count, destination }) => {
    try {
      const result = await bridge.send("generate_variations", { direction, count: count || 4, destination });
      return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// --- evolve_variations ---
mcp.tool(
  "evolve_variations",
  "Evolve favorited shader variations. Takes the favorited shader code and generates new variations based on them.",
  {
    favorites: z.array(z.string()).describe("Array of ISF shader source code strings (the favorited variants)"),
    count: z.number().min(2).max(8).optional().describe("Number of new variations (default: 4)"),
  },
  async ({ favorites, count }) => {
    try {
      const result = await bridge.send("evolve_variations", { favorites, count: count || 4 });
      return { content: [{ type: "text", text: JSON.stringify(result, null, 2) }] };
    } catch (e) {
      return { content: [{ type: "text", text: `Error: ${e.message}` }], isError: true };
    }
  }
);

// Cleanup pixel streaming on exit
process.on("exit", () => { stopPixelStream(); });
process.on("SIGINT", () => { stopPixelStream(); ndiCleanup(); process.exit(0); });
process.on("SIGTERM", () => { stopPixelStream(); ndiCleanup(); process.exit(0); });

// ============================================================
// Start everything
// ============================================================

async function main() {
  // Start HTTP + WS server
  httpServer.listen(PORT, () => {
    log(`HTTP + WS server listening on http://localhost:${PORT}`);
  });

  httpServer.on("error", (err) => {
    if (err.code === "EADDRINUSE") {
      log(`Port ${PORT} already in use. Set SHADERCLAW_PORT env var to use a different port.`);
      process.exit(1);
    }
    throw err;
  });

  // Start MCP server on stdio
  const transport = new StdioServerTransport();
  await mcp.connect(transport);
  log("MCP server connected on stdio");
}

main().catch((err) => {
  log("Fatal:", err.message);
  process.exit(1);
});


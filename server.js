// ShaderClaw MCP Server
// stdio MCP server + HTTP static server + WebSocket browser bridge
// Single process, single port.

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { createServer } from "http";
import { readFile, readdir } from "fs/promises";
import { join, extname } from "path";
import { WebSocketServer } from "ws";
import { fileURLToPath } from "url";
import { dirname } from "path";
import grandi from "grandi";

const __filename = fileURLToPath(import.meta.url);
const __dirname = dirname(__filename);
const PORT = parseInt(process.env.PORT || process.env.SHADERCLAW_PORT || "7777", 10);

const log = (...args) => process.stderr.write(`[ShaderClaw] ${args.join(" ")}\n`);

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
        const frame = await pumpReceiver.video(100); // 100ms timeout, returns Promise
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
        // Timeout (code 4040) is normal — just means no frame this interval
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
process.on("SIGINT", () => { ndiCleanup(); process.exit(0); });
process.on("SIGTERM", () => { ndiCleanup(); process.exit(0); });

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
    res.writeHead(200, { "Content-Type": MIME[ext] || "application/octet-stream" });
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
// MCP Server
// ============================================================

const mcp = new McpServer({
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
  "List all ISF inputs with current values, types, and ranges",
  {},
  async () => {
    try {
      const result = await bridge.send("get_parameters");
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

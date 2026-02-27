// ShaderClaw Remote Control — send commands via HTTP API
// Usage: node rc.mjs <action> [paramsJSON] [timeoutMs]
// Example: node rc.mjs enable_mediapipe '{"modes":{"hand":true,"face":true}}' 60000
// Example: node rc.mjs screenshot
// Example: node rc.mjs set_layer_visibility '{"layerId":"text","visible":false}'
// Example: node rc.mjs get_layers

const PORT = process.env.SHADERCLAW_PORT || 7777;
const action = process.argv[2];
const params = process.argv[3] ? JSON.parse(process.argv[3]) : {};
const timeout = parseInt(process.argv[4] || "10000", 10);

if (!action) {
  console.error('Usage: node rc.mjs <action> [paramsJSON] [timeoutMs]');
  process.exit(1);
}

const body = JSON.stringify({ action, params, timeout });

const res = await fetch(`http://localhost:${PORT}/api/rc`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body,
  signal: AbortSignal.timeout(timeout + 5000),
});

const data = await res.json();

if (!data.ok) {
  console.error('Error:', data.error);
  process.exit(1);
}

if (action === 'screenshot' && data.result?.image) {
  const fs = await import('fs');
  const buf = Buffer.from(data.result.image, 'base64');
  const path = `screenshot_${Date.now()}.png`;
  fs.writeFileSync(path, buf);
  console.log(`Saved: ${path} (${buf.length} bytes)`);
} else {
  console.log(JSON.stringify(data.result, null, 2));
}

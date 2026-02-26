const fs = require('fs');
const src = fs.readFileSync('shaders/text_james.fs', 'utf8');
const glslStart = src.indexOf('*/');
const glsl = glslStart >= 0 ? src.slice(glslStart + 2) : src;
const cleaned = glsl.replace(/#version\s+\d+.*/g, '');
const frag = 'precision highp float;\nuniform float TIME;\n' + cleaned;

const re = /vec2 charData\(int ch\)\s*\{[\s\S]*?\n\}/;
const match = frag.match(re);
console.log('Match found:', match ? 'YES' : 'NO');
if (match) {
  const opt = frag.replace(re, 'vec2 charData(int ch) { return vec2(0.0); }');
  const remaining = (opt.match(/else if/g) || []).length;
  console.log('Remaining else-if branches:', remaining);
  console.log('Size reduction:', frag.length - opt.length, 'chars');
  // Check key functions still exist
  console.log('Has main():', /void\s+main\s*\(/.test(opt));
  console.log('Has effectJames:', /effectJames/.test(opt));
  console.log('Has charPixel:', /charPixel/.test(opt));
  console.log('Has sampleChar:', /sampleChar/.test(opt));
  console.log('Has getChar:', /getChar/.test(opt));
}

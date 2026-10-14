'use strict';
// Unity needs every MonoBehaviour that is saved in a scene/prefab to live in a file with the same name.
// This moves each top-level `class X : MonoBehaviour` out of the given files into X.cs (same usings,
// same namespace). Other types stay where they are.
//   node Tools/split-monobehaviours.js <file.cs> [...]
const fs = require('fs');
const path = require('path');

function splitFile(file) {
  const src = fs.readFileSync(file, 'utf8').replace(/\r\n/g, '\n');
  const nsMatch = /^namespace\s+([\w.]+)\s*\n\{\n/m.exec(src);
  if (!nsMatch) throw new Error('no namespace block in ' + file);
  const header = src.slice(0, nsMatch.index);
  const bodyStart = nsMatch.index + nsMatch[0].length;
  const bodyEnd = src.lastIndexOf('}');
  const body = src.slice(bodyStart, bodyEnd);

  // Find top-level members (4-space indented declarations) with their leading comments/attributes.
  const lines = body.split('\n');
  const blocks = [];
  let i = 0;
  while (i < lines.length) {
    let start = i;
    while (i < lines.length && /^    (\/\/\/|\[|\/\/)/.test(lines[i])) i++;
    if (i >= lines.length) break;
    const decl = lines[i];
    if (!/^    \S/.test(decl)) { i++; continue; }
    // brace matching from the declaration line
    let depth = 0, seenOpen = false, j = i;
    for (; j < lines.length; j++) {
      for (const ch of lines[j]) {
        if (ch === '{') { depth++; seenOpen = true; }
        else if (ch === '}') depth--;
      }
      if (seenOpen && depth === 0) break;
      if (!seenOpen && /;\s*$/.test(lines[j])) break;
    }
    blocks.push({ text: lines.slice(start, j + 1).join('\n'), decl });
    i = j + 1;
  }

  const keep = [], moved = [];
  for (const b of blocks) {
    const m = /class\s+(\w+)\s*:\s*MonoBehaviour/.exec(b.decl);
    if (m) moved.push({ name: m[1], text: b.text });
    else keep.push(b.text);
  }
  if (moved.length === 0) return;
  const dir = path.dirname(file);
  for (const mv of moved) {
    const out = path.join(dir, mv.name + '.cs');
    if (path.resolve(out) === path.resolve(file)) { keep.unshift(mv.text); continue; }
    fs.writeFileSync(out, `${header}namespace ${nsMatch[1]}\n{\n${mv.text}\n}\n`);
    console.log('wrote', out);
  }
  if (keep.length) fs.writeFileSync(file, `${header}namespace ${nsMatch[1]}\n{\n${keep.join('\n\n')}\n}\n`);
  else { fs.unlinkSync(file); if (fs.existsSync(file + '.meta')) fs.unlinkSync(file + '.meta'); console.log('removed', file); }
}

for (const f of process.argv.slice(2)) splitFile(f);

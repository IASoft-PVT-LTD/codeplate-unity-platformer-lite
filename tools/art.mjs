// Art helpers that turn a pack's sheets into the strips the game reads (docs/recipes/reskin-and-rebrand.md).
//
//   npm run art -- grid <sheet.png> <cols>x<rows> <slot> [order=<i,j,...>] [cell=<w>x<h>]
//
// grid: cuts a sheet of cols x rows equal cells (read left to right, top to bottom) into one
// strip, frames side by side, and writes it to Assets/Resources/Art/<slot>.png. Without order=,
// cell k becomes frame k. With order=, the list gives the frame each cell becomes, in the same
// reading order: for a 4x4 terrain sheet that is the autotile mask of each cell
// (N=1, E=2, S=4, W=8; a bit is set when that neighbour is also solid). cell= cuts cells of
// that size from the top-left corner (for sheets with a margin at the right or bottom).
//   npm run art -- grid pack/walls.png 4x4 tiles/terrain_w1
//   npm run art -- grid pack/walls.png 4x4 tiles/terrain_w1 order=6,14,12,4,7,15,13,5,3,11,9,1,2,10,8,0
// (packs lay their 16 tiles out differently: look at each cell, write down its mask, then check
// the result in a screenshot: npm run shots -- level=1).
// It prints the width, height and frames to put in assets.json for the slot.
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { PATHS, PROJECT } from '../checks/layout.mjs';
import { blank, blit, decodePng, encodePng } from './lib/png.mjs';

/**
 * Cuts `sheet` (decoded PNG) into a strip. `order[k]` is the frame cell k becomes (default k).
 * Returns { strip, width, height, frames }.
 */
export function gridToStrip(sheet, cols, rows, { order = null, cellW = 0, cellH = 0 } = {}) {
  const n = cols * rows;
  const w = cellW || sheet.width / cols;
  const h = cellH || sheet.height / rows;
  if (!Number.isInteger(w) || !Number.isInteger(h) || w < 1 || h < 1) {
    throw new Error(`a ${sheet.width}x${sheet.height} sheet doesn't split into ${cols}x${rows} equal cells: give the cell size (cell=16x16)`);
  }
  if (w * cols > sheet.width || h * rows > sheet.height) throw new Error(`${cols}x${rows} cells of ${w}x${h} don't fit in a ${sheet.width}x${sheet.height} sheet`);
  const frames = order ?? [...Array(n).keys()];
  if (frames.length !== n) throw new Error(`order= has ${frames.length} entries; the sheet has ${n} cells`);
  const seen = new Set();
  for (const f of frames) {
    if (!Number.isInteger(f) || f < 0 || f >= n) throw new Error(`order=: ${f} is not a frame number from 0 to ${n - 1}`);
    if (seen.has(f)) throw new Error(`order=: frame ${f} is listed twice`);
    seen.add(f);
  }
  const strip = blank(w * n, h);
  frames.forEach((frame, cell) => blit(sheet, (cell % cols) * w, Math.floor(cell / cols) * h, strip, frame * w, 0, w, h));
  return { strip, width: w, height: h, frames: n };
}

function main(argv) {
  const [tool, ...rest] = argv;
  const named = Object.fromEntries(rest.filter((a) => a.includes('=')).map((a) => [a.slice(0, a.indexOf('=')), a.slice(a.indexOf('=') + 1)]));
  const plain = rest.filter((a) => !a.includes('='));
  if (tool !== 'grid' || plain.length !== 3) {
    console.log('usage: npm run art -- grid <sheet.png> <cols>x<rows> <slot> [order=<i,j,...>] [cell=<w>x<h>]');
    return 1;
  }
  const [file, size, slot] = plain;
  const m = size.match(/^(\d+)x(\d+)$/);
  if (!m) {
    console.log(`"${size}" should be <cols>x<rows>, like 4x4`);
    return 1;
  }
  if (slot.split(/[\\/]/).includes('..')) {
    console.log(`the slot is a path under ${PATHS.art}/, like tiles/terrain_w1`);
    return 1;
  }
  const cell = named.cell?.match(/^(\d+)x(\d+)$/);
  const source = resolve(process.env.INIT_CWD ?? process.cwd(), file);
  if (!existsSync(source)) {
    console.log(`${source} doesn't exist`);
    return 1;
  }
  try {
    const sheet = decodePng(readFileSync(source));
    const order = named.order ? named.order.split(',').map((s) => Number(s.trim())) : null;
    const r = gridToStrip(sheet, Number(m[1]), Number(m[2]), { order, cellW: cell ? Number(cell[1]) : 0, cellH: cell ? Number(cell[2]) : 0 });
    const out = join(PROJECT, PATHS.art, `${slot.replace(/\.png$/, '')}.png`);
    mkdirSync(dirname(out), { recursive: true });
    writeFileSync(out, encodePng(r.strip));
    console.log(`wrote ${out}: ${r.frames} frames of ${r.width}x${r.height}`);
    console.log(`assets.json for ${PATHS.art}/${slot.replace(/\.png$/, '')}.png: "width": ${r.width}, "height": ${r.height}, "frames": ${r.frames}`);
    return 0;
  } catch (e) {
    console.log(`art grid: ${e.message}`);
    return 1;
  }
}

if (process.argv[1] && fileURLToPath(import.meta.url) === resolve(process.argv[1])) process.exitCode = main(process.argv.slice(2));

// Reads which characters a TrueType/OpenType font has glyphs for (its cmap table), for the
// glyph-coverage rule in checks/rules.mjs. Pure Node.

/** The set of code points `buf` (a .ttf/.otf file) maps to a glyph. Throws on a file it can't read. */
export function fontCharacters(buf) {
  if (buf.length < 12) throw new Error('not a font file');
  const tables = buf.readUInt16BE(4);
  let cmap = -1;
  for (let i = 0; i < tables; i++) {
    const at = 12 + i * 16;
    if (buf.toString('latin1', at, at + 4) === 'cmap') cmap = buf.readUInt32BE(at + 8);
  }
  if (cmap < 0) throw new Error('the font has no cmap table');
  // Pick the best subtable: full Unicode (format 12) over the basic plane (format 4).
  const subtables = [];
  for (let i = 0; i < buf.readUInt16BE(cmap + 2); i++) {
    const at = cmap + 4 + i * 8;
    const platform = buf.readUInt16BE(at), encoding = buf.readUInt16BE(at + 2);
    const offset = cmap + buf.readUInt32BE(at + 4);
    const format = buf.readUInt16BE(offset);
    const unicode = platform === 0 || (platform === 3 && (encoding === 1 || encoding === 10));
    if (unicode && (format === 4 || format === 12)) subtables.push({ format, offset });
  }
  subtables.sort((a, b) => b.format - a.format);
  const sub = subtables[0];
  if (!sub) throw new Error('the font has no Unicode cmap (format 4 or 12)');
  const chars = new Set();
  if (sub.format === 12) {
    const groups = buf.readUInt32BE(sub.offset + 12);
    for (let g = 0; g < groups; g++) {
      const at = sub.offset + 16 + g * 12;
      const start = buf.readUInt32BE(at), end = buf.readUInt32BE(at + 4), glyph = buf.readUInt32BE(at + 8);
      for (let c = start; c <= end; c++) if (glyph + (c - start) !== 0) chars.add(c);
    }
    return chars;
  }
  const o = sub.offset;
  const segs = buf.readUInt16BE(o + 6) / 2;
  const ends = o + 14, starts = ends + segs * 2 + 2, deltas = starts + segs * 2, ranges = deltas + segs * 2;
  for (let s = 0; s < segs; s++) {
    const end = buf.readUInt16BE(ends + s * 2), start = buf.readUInt16BE(starts + s * 2);
    const delta = buf.readInt16BE(deltas + s * 2), range = buf.readUInt16BE(ranges + s * 2);
    for (let c = start; c <= end && c !== 0xffff; c++) {
      let glyph;
      if (range === 0) glyph = (c + delta) & 0xffff;
      else {
        const at = ranges + s * 2 + range + (c - start) * 2;
        glyph = buf.readUInt16BE(at);
        if (glyph !== 0) glyph = (glyph + delta) & 0xffff;
      }
      if (glyph !== 0) chars.add(c);
    }
  }
  return chars;
}

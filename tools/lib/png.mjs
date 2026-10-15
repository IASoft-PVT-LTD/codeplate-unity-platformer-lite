// A small PNG reader and writer on Node's zlib, for the art tools (tools/art.mjs) and the
// project rules (checks/rules.mjs). Reads non-interlaced PNGs: grayscale, RGB, palette (1, 2, 4
// or 8 bits), grayscale + alpha and RGBA, 8 bits per channel (16-bit files are read at 8 bits).
// Pixels come back as RGBA, 4 bytes each, row by row.
import { inflateSync, deflateSync } from 'node:zlib';

const SIGNATURE = Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]);
const CHANNELS = { 0: 1, 2: 3, 3: 1, 4: 2, 6: 4 };

/** Decodes a PNG file's bytes into { width, height, data } (data: RGBA bytes). Throws with a plain message on what it can't read. */
export function decodePng(buf) {
  if (buf.length < 8 || !SIGNATURE.equals(buf.subarray(0, 8))) throw new Error('not a PNG file');
  let width = 0, height = 0, depth = 0, type = 0, interlace = 0;
  let palette = null, trns = null;
  const idat = [];
  for (let at = 8; at + 8 <= buf.length;) {
    const len = buf.readUInt32BE(at);
    const kind = buf.toString('latin1', at + 4, at + 8);
    const body = buf.subarray(at + 8, at + 8 + len);
    if (kind === 'IHDR') {
      width = body.readUInt32BE(0);
      height = body.readUInt32BE(4);
      depth = body[8];
      type = body[9];
      interlace = body[12];
    } else if (kind === 'PLTE') palette = body;
    else if (kind === 'tRNS') trns = body;
    else if (kind === 'IDAT') idat.push(body);
    else if (kind === 'IEND') break;
    at += 12 + len;
  }
  if (!(type in CHANNELS)) throw new Error(`unknown PNG color type ${type}`);
  if (interlace) throw new Error('interlaced PNGs are not supported: save it without interlacing');
  if (type === 3 ? ![1, 2, 4, 8].includes(depth) : ![8, 16].includes(depth)) throw new Error(`${depth}-bit PNGs of color type ${type} are not supported`);
  const channels = CHANNELS[type];
  const bitsPerPixel = channels * depth;
  const stride = Math.ceil((width * bitsPerPixel) / 8);
  const bpp = Math.max(1, bitsPerPixel >> 3);
  const raw = inflateSync(Buffer.concat(idat));
  const rows = Buffer.alloc(stride * height);
  let prev = Buffer.alloc(stride);
  for (let y = 0; y < height; y++) {
    const filter = raw[y * (stride + 1)];
    const line = raw.subarray(y * (stride + 1) + 1, (y + 1) * (stride + 1));
    const out = rows.subarray(y * stride, (y + 1) * stride);
    for (let i = 0; i < stride; i++) {
      const a = i >= bpp ? out[i - bpp] : 0;
      const b = prev[i];
      const c = i >= bpp ? prev[i - bpp] : 0;
      let v = line[i];
      if (filter === 1) v += a;
      else if (filter === 2) v += b;
      else if (filter === 3) v += (a + b) >> 1;
      else if (filter === 4) {
        const p = a + b - c, pa = Math.abs(p - a), pb = Math.abs(p - b), pc = Math.abs(p - c);
        v += pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
      } else if (filter !== 0) throw new Error(`bad PNG filter ${filter} on row ${y}`);
      out[i] = v & 0xff;
    }
    prev = out;
  }
  const data = Buffer.alloc(width * height * 4);
  const sample = (row, x, ch) => {
    if (depth === 16) return rows[row + (x * channels + ch) * 2];
    if (depth === 8) return rows[row + x * channels + ch];
    const bit = x * depth;
    return (rows[row + (bit >> 3)] >> (8 - depth - (bit & 7))) & ((1 << depth) - 1);
  };
  for (let y = 0; y < height; y++) {
    const row = y * stride;
    for (let x = 0; x < width; x++) {
      const o = (y * width + x) * 4;
      if (type === 3) {
        const i = sample(row, x, 0);
        data[o] = palette?.[i * 3] ?? 0;
        data[o + 1] = palette?.[i * 3 + 1] ?? 0;
        data[o + 2] = palette?.[i * 3 + 2] ?? 0;
        data[o + 3] = trns && i < trns.length ? trns[i] : 255;
      } else if (type === 0 || type === 4) {
        const g = sample(row, x, 0);
        data[o] = data[o + 1] = data[o + 2] = g;
        data[o + 3] = type === 4 ? sample(row, x, 1) : 255;
      } else {
        data[o] = sample(row, x, 0);
        data[o + 1] = sample(row, x, 1);
        data[o + 2] = sample(row, x, 2);
        data[o + 3] = type === 6 ? sample(row, x, 3) : 255;
      }
    }
  }
  return { width, height, data };
}

const CRC_TABLE = Array.from({ length: 256 }, (_, n) => {
  let c = n;
  for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
  return c >>> 0;
});

function crc32(buf) {
  let c = 0xffffffff;
  for (const b of buf) c = CRC_TABLE[(c ^ b) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

function chunk(kind, body) {
  const out = Buffer.alloc(12 + body.length);
  out.writeUInt32BE(body.length, 0);
  out.write(kind, 4, 'latin1');
  body.copy(out, 8);
  out.writeUInt32BE(crc32(out.subarray(4, 8 + body.length)), 8 + body.length);
  return out;
}

/** Encodes { width, height, data (RGBA) } as an RGBA PNG. */
export function encodePng({ width, height, data }) {
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(width, 0);
  ihdr.writeUInt32BE(height, 4);
  ihdr[8] = 8;
  ihdr[9] = 6;
  const raw = Buffer.alloc((width * 4 + 1) * height);
  for (let y = 0; y < height; y++) data.copy(raw, y * (width * 4 + 1) + 1, y * width * 4, (y + 1) * width * 4);
  return Buffer.concat([SIGNATURE, chunk('IHDR', ihdr), chunk('IDAT', deflateSync(raw, { level: 9 })), chunk('IEND', Buffer.alloc(0))]);
}

/** An empty (transparent) RGBA image. */
export function blank(width, height) {
  return { width, height, data: Buffer.alloc(width * height * 4) };
}

/** Copies a w x h block from (sx, sy) of `src` to (dx, dy) of `dst`. */
export function blit(src, sx, sy, dst, dx, dy, w, h) {
  for (let y = 0; y < h; y++) {
    src.data.copy(dst.data, ((dy + y) * dst.width + dx) * 4, ((sy + y) * src.width + sx) * 4, ((sy + y) * src.width + sx + w) * 4);
  }
}

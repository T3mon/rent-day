"""Reading and writing 8-bit PNGs with the standard library only."""
import struct, zlib


def png(path, w, h, pixels):
    """pixels: list of rows of (r, g, b, a)."""
    raw = b''.join(b'\x00' + bytes(c for px in row for c in px) for row in pixels)
    def chunk(t, d):
        return struct.pack('>I', len(d)) + t + d + struct.pack('>I', zlib.crc32(t + d) & 0xffffffff)
    data = b'\x89PNG\r\n\x1a\n' + chunk(b'IHDR', struct.pack('>IIBBBBB', w, h, 8, 6, 0, 0, 0)) + chunk(b'IDAT', zlib.compress(raw, 9)) + chunk(b'IEND', b'')
    open(path, 'wb').write(data)


def read_png(path):
    """(w, h, rows of (r, g, b, a)) for 8-bit RGB/RGBA/palette PNGs."""
    data = open(path, 'rb').read()
    pos, idat, plte, trns = 8, b'', None, None
    while pos < len(data):
        n, t = struct.unpack('>I4s', data[pos:pos + 8])
        body = data[pos + 8:pos + 8 + n]
        if t == b'IHDR':
            w, h, depth, ctype = struct.unpack('>IIBB', body[:10])
        elif t == b'PLTE':
            plte = [tuple(body[i:i + 3]) for i in range(0, n, 3)]
        elif t == b'tRNS':
            trns = body
        elif t == b'IDAT':
            idat += body
        pos += 12 + n
    assert depth == 8, depth
    bpp = {2: 3, 6: 4, 3: 1, 0: 1, 4: 2}[ctype]
    raw = zlib.decompress(idat)
    stride = w * bpp
    rows, prev = [], bytearray(stride)
    for y in range(h):
        f = raw[y * (stride + 1)]
        line = bytearray(raw[y * (stride + 1) + 1:(y + 1) * (stride + 1)])
        for i in range(stride):
            a = line[i - bpp] if i >= bpp else 0
            b = prev[i]
            c = prev[i - bpp] if i >= bpp else 0
            if f == 1: line[i] = (line[i] + a) & 255
            elif f == 2: line[i] = (line[i] + b) & 255
            elif f == 3: line[i] = (line[i] + (a + b) // 2) & 255
            elif f == 4:
                p = a + b - c; pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                line[i] = (line[i] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        prev = line
        px = []
        for x in range(w):
            v = line[x * bpp:(x + 1) * bpp]
            if ctype == 6: px.append(tuple(v))
            elif ctype == 2: px.append((v[0], v[1], v[2], 255))
            elif ctype == 3: px.append(plte[v[0]] + ((trns[v[0]] if trns and v[0] < len(trns) else 255),))
            elif ctype == 0: px.append((v[0], v[0], v[0], 255))
            else: px.append((v[0], v[0], v[0], v[1]))
        rows.append(px)
    return w, h, rows

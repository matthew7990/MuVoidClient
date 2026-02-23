#!/usr/bin/env python3
"""
Convierte archivos OZJ (MU Online - JPEG comprimido) a OZT (formato TGA interno).

Formato OZJ: 24 bytes header + datos JPEG
Formato OZT: 22 bytes header + pixels BGRA (32 bits)
  - Bytes 16-17: width (int16 LE)
  - Bytes 18-19: height (int16 LE)
  - Byte 20: 32 (bits per pixel)
  - Bytes 22+: BGRA, filas de arriba a abajo

Uso:
  python ozj-to-ozt.py <archivo.ozj> [salida.ozt]
  python ozj-to-ozt.py --batch <carpeta_origen> <carpeta_destino>
"""

import argparse
import io
import struct
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    print("Error: Se necesita Pillow. Instalar con: pip install Pillow")
    sys.exit(1)


OZJ_HEADER_SIZE = 24
OZT_HEADER_SIZE = 22


def ozj_to_rgb(ozj_path: Path) -> tuple[bytes, int, int]:
    """Lee OZJ y devuelve (pixels_rgb, width, height)."""
    data = ozj_path.read_bytes()
    if len(data) <= OZJ_HEADER_SIZE:
        raise ValueError(f"Archivo OZJ demasiado pequeño: {ozj_path}")
    jpeg_data = data[OZJ_HEADER_SIZE:]
    img = Image.open(io.BytesIO(jpeg_data))
    if img.mode != "RGB":
        img = img.convert("RGB")
    return img.tobytes(), img.width, img.height


def rgb_to_bgra(rgb: bytes) -> bytes:
    """Convierte RGB a BGRA (alpha=255)."""
    n = len(rgb) // 3
    bgra = bytearray(n * 4)
    for i in range(n):
        r, g, b = rgb[i * 3], rgb[i * 3 + 1], rgb[i * 3 + 2]
        bgra[i * 4 : i * 4 + 4] = (b, g, r, 255)
    return bytes(bgra)


def write_ozt(out_path: Path, width: int, height: int, bgra: bytes) -> None:
    """Escribe archivo OZT con el formato esperado por el motor S6."""
    header = bytearray(OZT_HEADER_SIZE)
    struct.pack_into("<h", header, 16, width)
    struct.pack_into("<h", header, 18, height)
    header[20] = 32
    out_path.parent.mkdir(parents=True, exist_ok=True)
    with open(out_path, "wb") as f:
        f.write(header)
        f.write(bgra)


def convert_one(ozj_path: Path, ozt_path: Path = None) -> Path:
    ozt_path = ozt_path or ozj_path.with_suffix(".ozt")
    rgb, w, h = ozj_to_rgb(ozj_path)
    bgra = rgb_to_bgra(rgb)
    write_ozt(ozt_path, w, h, bgra)
    return ozt_path


def main():
    parser = argparse.ArgumentParser(description="Convierte OZJ a OZT (formato TGA MU)")
    parser.add_argument("input", nargs="?", help="Archivo OZJ de entrada")
    parser.add_argument("output", nargs="?", help="Archivo OZT de salida (opcional)")
    parser.add_argument("--batch", nargs=2, metavar=("ORIGEN", "DESTINO"),
                        help="Convertir todos los .ozj de una carpeta")
    args = parser.parse_args()

    if args.batch:
        src_dir = Path(args.batch[0])
        dst_dir = Path(args.batch[1])
        if not src_dir.is_dir():
            print(f"Error: Origen no es carpeta: {src_dir}")
            sys.exit(1)
        dst_dir.mkdir(parents=True, exist_ok=True)
        for ozj in src_dir.glob("*.ozj"):
            ozt = dst_dir / ozj.with_suffix(".ozt").name
            try:
                convert_one(ozj, ozt)
                print(f"OK: {ozj.name} -> {ozt}")
            except Exception as e:
                print(f"Error {ozj.name}: {e}")
        return

    if not args.input:
        parser.print_help()
        sys.exit(0)
    ozj_path = Path(args.input)
    if not ozj_path.exists():
        print(f"Error: No existe {ozj_path}")
        sys.exit(1)
    ozt_path = Path(args.output) if args.output else None
    try:
        out = convert_one(ozj_path, ozt_path)
        print(f"OK: {out}")
    except Exception as e:
        print(f"Error: {e}")
        sys.exit(1)


if __name__ == "__main__":
    main()

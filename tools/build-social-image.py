# SPDX-License-Identifier: GPL-3.0-or-later
# Copyright (C) 2026 Kumquat contributors
"""
Genera docs/images/social-preview.png (1280x640): la imagen que muestran GitHub, LinkedIn y otras
redes al compartir el enlace del repositorio. Usa el logo, Inter y la captura real de la rueda
(docs/images/rueda-oscura.png, que regeneran los tests de Kumquat.Interaction).
Uso: python tools/build-social-image.py   (requiere Pillow)
En GitHub se sube a mano: Settings -> General -> Social preview.
"""
import os

from PIL import Image, ImageDraw, ImageFilter, ImageFont

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
W, H = 1280, 640
ORANGE = (255, 149, 0)


def font(weight, size):
    return ImageFont.truetype(os.path.join(REPO, "assets", "fonts", f"Inter-{weight}.ttf"), size)


def gradient():
    top, bottom = (36, 36, 44), (16, 18, 24)
    image = Image.new("RGB", (W, H))
    draw = ImageDraw.Draw(image)
    for y in range(H):
        t = y / (H - 1)
        draw.line([(0, y), (W, y)], fill=tuple(int(a + (b - a) * t) for a, b in zip(top, bottom)))
    return image.convert("RGBA")


def glow(image, center, radius, color, alpha):
    layer = Image.new("RGBA", image.size, (0, 0, 0, 0))
    x, y = center
    ImageDraw.Draw(layer).ellipse([x - radius, y - radius, x + radius, y + radius], fill=color + (alpha,))
    return Image.alpha_composite(image, layer.filter(ImageFilter.GaussianBlur(radius / 2)))


def main():
    image = gradient()
    image = glow(image, (990, 330), 300, ORANGE, 70)
    image = glow(image, (180, 120), 220, (120, 90, 255), 35)

    wheel = Image.open(os.path.join(REPO, "docs", "images", "rueda-oscura.png")).convert("RGBA")
    wheel = wheel.resize((int(wheel.width * 0.62), int(wheel.height * 0.62)), Image.LANCZOS)
    image.alpha_composite(wheel, (W - wheel.width - 40, (H - wheel.height) // 2 + 6))

    logo = Image.open(os.path.join(REPO, "assets", "logo", "kumquat.png")).convert("RGBA")
    logo = logo.resize((96, int(96 * logo.height / logo.width)), Image.LANCZOS)
    image.alpha_composite(logo, (80, 118))

    draw = ImageDraw.Draw(image)
    draw.text((192, 118), "Kumquat", font=font("Bold", 84), fill=(245, 245, 247))
    draw.text((82, 250), "Convierte archivos", font=font("SemiBold", 46), fill=(245, 245, 247))
    draw.text((82, 304), "sin abrir nada.", font=font("SemiBold", 46), fill=ORANGE)
    draw.text((82, 382), "Arrastra un archivo, presiona Shift y suéltalo", font=font("Regular", 25), fill=(200, 200, 210))
    draw.text((82, 414), "sobre el formato o la herramienta que quieras.", font=font("Regular", 25), fill=(200, 200, 210))

    # Píldoras translúcidas: en una capa aparte (dibujar con alfa sobre la imagen reemplaza, no mezcla).
    chips = Image.new("RGBA", image.size, (0, 0, 0, 0))
    chip_draw = ImageDraw.Draw(chips)
    x = 82
    f = font("Medium", 20)
    labels = ("100 % local", "Código abierto", "Windows 10 / 11")
    positions = []
    for label in labels:
        width = draw.textlength(label, font=f)
        chip_draw.rounded_rectangle([x, 478, x + width + 32, 518], radius=20, fill=(255, 255, 255, 26), outline=(255, 255, 255, 60))
        positions.append((x + 16, 487))
        x += width + 48
    image = Image.alpha_composite(image, chips)
    draw = ImageDraw.Draw(image)
    for label, position in zip(labels, positions):
        draw.text(position, label, font=f, fill=(235, 235, 240))

    draw.text((82, 566), "github.com/MartinQuiri/kumquat", font=font("Medium", 22), fill=(150, 150, 160))

    out = os.path.join(REPO, "docs", "images", "social-preview.png")
    image.convert("RGB").save(out, optimize=True)
    print("->", out)


if __name__ == "__main__":
    main()

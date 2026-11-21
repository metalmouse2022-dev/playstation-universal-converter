"""Build the local resource bundle from separately obtained tool distributions."""
import argparse
import hashlib
import os
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile

from PIL import Image, ImageDraw


ROOT = Path(__file__).resolve().parent
ASSETS = ROOT / "assets"
DEPS = ROOT / "dependencies"


def require_file(path):
    if not path.is_file():
        raise SystemExit(f"Missing build input: {path}. See the build instructions in the README.")
    return path


def files_for_bundle(dotnet, runtime_version):
    for name in ("chdman.exe", "COPYING", "psxpackager/psxpackager.exe", "psxpackager/LICENSE"):
        yield name, require_file(DEPS / name)
    resources = DEPS / "psxpackager/Resources"
    if not resources.is_dir() or not any(path.is_file() for path in resources.rglob("*")):
        raise SystemExit(f"Missing PSXPackager CLI resources: {resources}")
    for path in sorted(resources.rglob("*")):
        if path.is_file():
            yield path.relative_to(DEPS).as_posix(), path

    for name in ("dotnet.exe", "LICENSE.txt", "ThirdPartyNotices.txt",
                 f"host/fxr/{runtime_version}/hostfxr.dll"):
        yield f"dotnet/{name}", require_file(dotnet / name)
    runtime = dotnet / f"shared/Microsoft.NETCore.App/{runtime_version}"
    require_file(runtime / "coreclr.dll")
    for path in sorted(runtime.rglob("*")):
        if path.is_file():
            yield "dotnet/" + path.relative_to(dotnet).as_posix(), path


def prepare_art():
    """Use original geometric defaults if the user has supplied no local art."""
    background = ASSETS / "character.png"
    if not background.exists():
        image = Image.new("RGB", (960, 800), "#102032")
        draw = ImageDraw.Draw(image)
        for radius in range(540, 70, -3):
            blend = 1 - (radius - 70) / 470
            color = tuple(round(base + amount * blend) for base, amount in zip((16, 32, 50), (34, 71, 82)))
            draw.ellipse((730 - radius, 270 - radius, 730 + radius, 270 + radius), fill=color)
        for radius in (96, 148, 220):
            draw.ellipse((736 - radius, 270 - radius, 736 + radius, 270 + radius), outline="#4e8394", width=2)
        image.save(background)

    logo = ASSETS / "playstation.png"
    if not logo.exists():
        image = Image.new("RGBA", (256, 256))
        draw = ImageDraw.Draw(image)
        draw.rounded_rectangle((4, 4, 252, 252), radius=54, fill="#132337")
        draw.ellipse((53, 53, 203, 203), outline="#83d6ed", width=16)
        draw.ellipse((110, 110, 146, 146), fill="#83d6ed")
        draw.line((76, 180, 110, 146), fill="#83d6ed", width=9)
        image.save(logo)

    with Image.open(logo) as source:
        source = source.convert("RGBA")
        source.thumbnail((256, 256), Image.Resampling.LANCZOS)
        icon = Image.new("RGBA", (256, 256))
        icon.alpha_composite(source, ((256 - source.width) // 2, (256 - source.height) // 2))
        icon.save(ASSETS / "app.ico", sizes=[(16, 16), (32, 32), (48, 48), (128, 128), (256, 256)])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    default_dotnet = Path(os.environ.get("ProgramFiles", "C:/Program Files")) / "dotnet"
    parser.add_argument("--dotnet-root", type=Path, default=default_dotnet)
    parser.add_argument("--runtime-version", default="8.0.30")
    args = parser.parse_args()
    files = list(files_for_bundle(args.dotnet_root, args.runtime_version))
    ASSETS.mkdir(exist_ok=True)
    prepare_art()
    manifest = []
    with ZipFile(ASSETS / "tools.zip", "w", compression=ZIP_DEFLATED, compresslevel=9) as archive:
        for name, path in sorted(files):
            data = path.read_bytes()
            archive.writestr(name, data)
            manifest.append(f"{hashlib.sha256(data).hexdigest()} {name}")
    (ASSETS / "tools.manifest").write_text("\n".join(manifest) + "\n", encoding="utf-8", newline="\n")
    print(f"Prepared {len(files)} local tool and runtime files.")


if __name__ == "__main__":
    main()

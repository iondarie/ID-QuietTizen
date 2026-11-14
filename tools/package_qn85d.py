"""Package the QN85D build with repository assets and optional local artwork."""

import argparse
import shutil
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("assembly", type=Path)
    parser.add_argument("--local-assets", type=Path)
    parser.add_argument("--output", type=Path, default=Path(".local/package-qn85d"))
    args = parser.parse_args()
    repository = Path(__file__).resolve().parents[1]
    if not args.assembly.is_file() or args.assembly.name != "AdPlayer.dll":
        parser.error("assembly must point to the built AdPlayer.dll")
    if args.local_assets and not args.local_assets.is_dir():
        parser.error("--local-assets must point to a directory")
    args.output.mkdir(parents=True, exist_ok=False)
    assets = args.output / "quiet-home"
    shutil.copytree(repository / "launcher/qn85d/assets", assets)
    shutil.copytree(repository / "launcher/assets/fonts", assets / "fonts")
    if args.local_assets:
        shutil.copytree(args.local_assets, assets, dirs_exist_ok=True)
    shutil.copy2(args.assembly, args.output)


if __name__ == "__main__":
    main()

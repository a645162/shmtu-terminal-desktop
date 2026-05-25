#!/usr/bin/env python3
"""运行 shmtu-terminal-desktop 桌面应用"""

import subprocess
import sys
import os

PROJECT_DIR = os.path.join(os.path.dirname(__file__), "..", "shmtu-terminal-desktop")


def main():
    action = sys.argv[1] if len(sys.argv) > 1 else "run"

    if action == "run":
        subprocess.run(
            ["dotnet", "run", "--project", PROJECT_DIR],
            cwd=os.path.dirname(__file__),
        )
    elif action == "build":
        subprocess.run(
            ["dotnet", "build", PROJECT_DIR],
            cwd=os.path.dirname(__file__),
        )
    elif action == "publish":
        rid = sys.argv[2] if len(sys.argv) > 2 else "linux-x64"
        out = os.path.join(PROJECT_DIR, "bin", "Publish")
        subprocess.run(
            [
                "dotnet", "publish", PROJECT_DIR,
                "-c", "Release",
                "-r", rid,
                "--self-contained", "true",
                "-o", out,
            ],
            cwd=os.path.dirname(__file__),
        )
    else:
        print(f"用法: {sys.argv[0]} [run|build|publish [rid]]")
        print("  run     - 编译并运行（默认）")
        print("  build   - 仅编译")
        print("  publish - 发布（可选 rid，默认 linux-x64）")


if __name__ == "__main__":
    main()

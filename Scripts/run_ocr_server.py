#!/usr/bin/env python3
"""运行 shmtu-ocr-onnx-server OCR Web 服务"""

import subprocess
import sys
import os

PROJECT_DIR = os.path.join(
    os.path.dirname(__file__), "..", "shmtu-dotnet-lib", "ocr", "shmtu-ocr-onnx-server"
)

DEFAULT_PORT = "5000"
DEFAULT_HOST = "0.0.0.0"


def main():
    action = sys.argv[1] if len(sys.argv) > 1 else "run"

    if action == "run":
        port = sys.argv[2] if len(sys.argv) > 2 else DEFAULT_PORT
        host = sys.argv[3] if len(sys.argv) > 3 else DEFAULT_HOST
        subprocess.run(
            [
                "dotnet", "run", "--project", PROJECT_DIR,
                "--urls", f"http://{host}:{port}",
            ],
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
        print(f"用法: {sys.argv[0]} [run|build|publish] [port] [host]")
        print("  run     - 编译并运行（默认端口 5000）")
        print("  build   - 仅编译")
        print("  publish - 发布（可选 rid，默认 linux-x64）")
        print()
        print("示例:")
        print(f"  {sys.argv[0]} run 8080 0.0.0.0")
        print(f"  {sys.argv[0]} publish win-x64")


if __name__ == "__main__":
    main()

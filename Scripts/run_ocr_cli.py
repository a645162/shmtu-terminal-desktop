#!/usr/bin/env python3
"""运行 shmtu-ocr-cli OCR 命令行工具"""

import subprocess
import sys
import os

PROJECT_DIR = os.path.join(
    os.path.dirname(__file__), "..", "shmtu-dotnet-lib", "ocr", "shmtu-ocr-cli"
)


def main():
    action = sys.argv[1] if len(sys.argv) > 1 else "run"

    if action == "run":
        extra_args = sys.argv[2:] if len(sys.argv) > 2 else []
        subprocess.run(
            ["dotnet", "run", "--project", PROJECT_DIR, "--"] + extra_args,
            cwd=os.path.dirname(__file__),
        )
    elif action == "build":
        subprocess.run(
            ["dotnet", "build", PROJECT_DIR],
            cwd=os.path.dirname(__file__),
        )
    else:
        print(f"用法: {sys.argv[0]} [run|build] [args...]")
        print("  run   - 编译并运行（默认）")
        print("  build - 仅编译")
        print()
        print("示例:")
        print(f"  {sys.argv[0]} run --image captcha.png")


if __name__ == "__main__":
    main()

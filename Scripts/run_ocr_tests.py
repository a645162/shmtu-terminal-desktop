#!/usr/bin/env python3
"""运行 shmtu-ocr-onnx-tests 单元测试"""

import subprocess
import sys
import os

PROJECT_DIR = os.path.join(
    os.path.dirname(__file__), "..", "shmtu-dotnet-lib", "ocr", "shmtu-ocr-onnx-tests"
)


def main():
    action = sys.argv[1] if len(sys.argv) > 1 else "test"

    if action == "test":
        extra_args = sys.argv[2:] if len(sys.argv) > 2 else []
        subprocess.run(
            ["dotnet", "test", PROJECT_DIR] + extra_args,
            cwd=os.path.dirname(__file__),
        )
    elif action == "build":
        subprocess.run(
            ["dotnet", "build", PROJECT_DIR],
            cwd=os.path.dirname(__file__),
        )
    else:
        print(f"用法: {sys.argv[0]} [test|build] [args...]")
        print("  test  - 运行测试（默认）")
        print("  build - 仅编译")


if __name__ == "__main__":
    main()

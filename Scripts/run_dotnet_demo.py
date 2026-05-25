#!/usr/bin/env python3
"""运行 shmtu-dotnet-demo 控制台演示"""

import subprocess
import sys
import os

PROJECT_DIR = os.path.join(
    os.path.dirname(__file__), "..", "shmtu-dotnet-lib", "Core", "shmtu-dotnet-demo"
)


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
    else:
        print(f"用法: {sys.argv[0]} [run|build]")
        print("  run   - 编译并运行（默认）")
        print("  build - 仅编译")


if __name__ == "__main__":
    main()

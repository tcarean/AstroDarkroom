"""Count code files and lines in the current folder, recursively.

Folders whose name starts with "." (.git, .claude, .vs, ...) are skipped.
"""

import os
import sys
from collections import defaultdict

CODE_EXTENSIONS = {
    ".py", ".pyw",
    ".cs", ".csx", ".fs", ".vb",
    ".xaml", ".axaml", ".razor", ".cshtml",
    ".c", ".h", ".cpp", ".cc", ".cxx", ".hpp", ".hh", ".hxx",
    ".java", ".kt", ".kts", ".scala", ".groovy",
    ".js", ".jsx", ".mjs", ".cjs", ".ts", ".tsx", ".vue", ".svelte",
    ".html", ".htm", ".css", ".scss", ".sass", ".less",
    ".go", ".rs", ".swift", ".m", ".mm", ".dart",
    ".rb", ".php", ".pl", ".pm", ".lua", ".r", ".jl",
    ".sh", ".bash", ".zsh", ".ps1", ".psm1", ".bat", ".cmd",
    ".sql", ".ssc",
}


def count_lines(path):
    with open(path, "rb") as f:
        data = f.read()
    if not data:
        return 0
    return data.count(b"\n") + (0 if data.endswith(b"\n") else 1)


def main():
    root = os.getcwd()
    this_script = os.path.abspath(__file__)
    per_ext = defaultdict(lambda: [0, 0])  # extension -> [files, lines]

    for dirpath, dirnames, filenames in os.walk(root):
        dirnames[:] = [d for d in dirnames if not d.startswith(".")]
        for name in filenames:
            ext = os.path.splitext(name)[1].lower()
            if ext not in CODE_EXTENSIONS:
                continue
            path = os.path.join(dirpath, name)
            if os.path.abspath(path) == this_script:
                continue
            try:
                lines = count_lines(path)
            except OSError as e:
                print(f"Nu pot citi {path}: {e}")
                continue
            per_ext[ext][0] += 1
            per_ext[ext][1] += lines

    total_files = sum(v[0] for v in per_ext.values())
    total_lines = sum(v[1] for v in per_ext.values())

    print(f"Folder: {root}\n")
    if per_ext:
        print(f"{'Extensie':<10}{'Fisiere':>10}{'Linii':>12}")
        for ext, (files, lines) in sorted(per_ext.items(), key=lambda kv: -kv[1][1]):
            print(f"{ext:<10}{files:>10}{lines:>12}")
        print("-" * 32)
    print(f"{'Total':<10}{total_files:>10}{total_lines:>12}")


def wait_for_key():
    if not sys.stdin.isatty():
        return
    print("\nApasa o tasta pentru a iesi...", flush=True)
    try:
        import msvcrt
        msvcrt.getch()
    except ImportError:
        input()


if __name__ == "__main__":
    try:
        main()
    finally:
        wait_for_key()
    sys.exit(0)

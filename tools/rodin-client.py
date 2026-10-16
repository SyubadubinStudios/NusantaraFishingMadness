"""Call the workspace's Rodin MCP over stdio; no third-party Python packages required."""
import argparse
import json
from pathlib import Path
import queue
import subprocess
import threading


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("tool")
    parser.add_argument("--arguments", help="Path to JSON tool arguments; omit for tools without arguments")
    parser.add_argument("--output", required=True, help="Path for the MCP response")
    args = parser.parse_args()
    root = Path(__file__).resolve().parent.parent
    config = json.loads((root / ".mcp.json").read_text())["mcpServers"]["rodin"]
    process = subprocess.Popen([config["command"], *config.get("args", [])],
                               stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                               stderr=subprocess.DEVNULL, text=True, encoding="utf-8")
    messages = queue.Queue()

    def read():
        for line in process.stdout:
            try:
                messages.put(json.loads(line))
            except ValueError:
                pass

    threading.Thread(target=read, daemon=True).start()

    def request(identifier, method, params):
        payload = {"jsonrpc": "2.0", "method": method, "params": params}
        if identifier is not None:
            payload["id"] = identifier
        process.stdin.write(json.dumps(payload) + "\n")
        process.stdin.flush()
        if identifier is not None:
            while True:
                result = messages.get(timeout=900)
                if result.get("id") == identifier:
                    return result

    try:
        request(1, "initialize", {"protocolVersion": "2024-11-05", "capabilities": {},
                                   "clientInfo": {"name": "NusantaraAssets", "version": "1.0"}})
        request(None, "notifications/initialized", {})
        result = request(2, "tools/list", {}) if args.tool == "list" else request(2, "tools/call", {"name": args.tool,
                                           "arguments": json.loads(Path(args.arguments).read_text(encoding="utf-8-sig")) if args.arguments else {}})
        target = Path(args.output)
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(result, indent=2), encoding="utf-8")
        print("Rodin response saved to", target)
        if "error" in result or result.get("result", {}).get("isError"):
            raise SystemExit(1)
    finally:
        process.terminate()
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.kill()


if __name__ == "__main__":
    main()

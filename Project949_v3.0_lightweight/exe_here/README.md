# Project949_v3.0_lightweight

这是 Project949 的轻量化分发版本。该文件夹是独立版本，不依赖旧版本文件夹。

## 首次使用

1. 先运行 `setup_base.exe`。
2. 必选项会自动处理：Node.js/npm/npx 与 MCP server-memory。
3. 可选项按需勾选：Ollama、llama3.2:latest。
4. 安装完成后运行 `Project949_v3.0_lightweight.exe`。

## setup_base 说明

- 必选：Node.js / npm / npx。MCP server-memory 需要它们运行。
- 必选：`@modelcontextprotocol/server-memory`。
- 可选：Ollama。本地部署模型需要。
- 可选：`llama3.2:latest`。需要先有 Ollama。

setup_base 会优先使用 winget 安装 Node.js LTS 与 Ollama；如果电脑没有 winget，会在日志里提示手动安装。

## 文件说明

- `Project949_v3.0_lightweight.exe`：主程序。
- `setup_base.exe`：首次使用安装器。
- `Program.cs`：主程序源码。
- `setup_base.cs`：安装器源码。
- `build-exe.ps1`：仅重编译本文件夹内两个 exe。
- `settings.json`：干净默认配置，不包含私人 API Key。
- `mcp-memory.txt`：可读可编辑的轻量记忆文件。
- `mcp-memory.jsonl`：MCP 内部同步文件。
- `tietu`：贴图资源。

## 转发

转发时发送整个 `Project949_v3.0_lightweight` 文件夹。
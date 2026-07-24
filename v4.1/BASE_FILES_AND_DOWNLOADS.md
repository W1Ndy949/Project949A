# Project949 v4.0 base files and downloads

Use the whole v4.0 folder as one isolated version.

## Included base files
- Project949_v4.0.exe : main program : OK
- setup_base.exe : setup and detection tool : OK
- settings.json : settings and preset storage : OK
- mcp-memory.txt : MCP text memory : OK
- mcp-memory.jsonl : MCP JSONL memory : OK
- app.ico : app icon : OK
- README.md : version notes : OK
- tietu\icon.png : tray icon texture : OK
- tietu\idle.png : idle sprite : OK
- tietu\speaking.png : speaking sprite : OK
- tietu\emotions\angry.png : emotion sprite : OK
- tietu\emotions\crying.png : emotion sprite : OK
- tietu\emotions\focused.png : emotion sprite : OK
- tietu\emotions\panic.png : emotion sprite : OK
- tietu\emotions\shocked.png : emotion sprite : OK
- tietu\emotions\shy.png : emotion sprite : OK
- tietu\emotions\speechless.png : emotion sprite : OK

## Manual downloads
1. .NET Framework 4.8 Runtime
   URL: https://dotnet.microsoft.com/download/dotnet-framework/net48
   Put in base_files as: ndp48-web.exe or ndp48-x86-x64-allos-enu.exe

2. Node.js LTS
   URL: https://nodejs.org/en/download
   Put in base_files as: node-v*-x64.msi

3. MCP server-memory
   Install after Node.js: npm install -g @modelcontextprotocol/server-memory
   URL: https://www.npmjs.com/package/@modelcontextprotocol/server-memory

4. Ollama (optional)
   URL: https://ollama.com/download
   Put in base_files as: OllamaSetup.exe

5. llama3.2:latest (optional)
   Install after Ollama: ollama pull llama3.2:latest
   URL: https://ollama.com/library/llama3.2

6. Serper API Key (optional)
   URL: https://serper.dev/

7. DeepSeek API Key (optional)
   URL: https://platform.deepseek.com/api_keys

## base_files
Download installers manually into v4.0\base_files, then click Run local installers.
Recognized names: node*.msi, node*.exe, ndp48*.exe, dotnet*.exe, OllamaSetup*.exe, ollama*.exe.

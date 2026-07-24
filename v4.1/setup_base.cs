using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Project949SetupBase
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupForm());
        }
    }

    sealed class SetupForm : Form
    {
        const string AppExeName = "Project949_v4.0.exe";

        readonly List<string> downloadUrls = new List<string>();
        CheckBox installNode;
        CheckBox installMcp;
        CheckBox installOllama;
        CheckBox pullLlama;
        Button detectButton;
        Button copyButton;
        Button pagesButton;
        Button baseFolderButton;
        Button localInstallButton;
        Button autoInstallButton;
        TextBox listBox;
        TextBox logBox;
        Label statusLabel;

        string RootDir { get { return AppDomain.CurrentDomain.BaseDirectory; } }
        string BaseFilesDir { get { return Path.Combine(RootDir, "base_files"); } }
        string ManifestPath { get { return Path.Combine(RootDir, "BASE_FILES_AND_DOWNLOADS.md"); } }

        public SetupForm()
        {
            Text = "Project949 v4.0 setup";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(680, 520);
            Size = new Size(980, 760);
            Font = new Font("Microsoft YaHei UI", 9f);
            PrepareDownloadUrls();
            BuildUi();
            EnsureBaseFilesFolder();
            WriteManifest();
            listBox.Text = BuildChecklistText();
            Shown += async delegate { await DetectEnvironment(); };
        }

        void PrepareDownloadUrls()
        {
            downloadUrls.Add("https://dotnet.microsoft.com/download/dotnet-framework/net48");
            downloadUrls.Add("https://nodejs.org/en/download");
            downloadUrls.Add("https://www.npmjs.com/package/@modelcontextprotocol/server-memory");
            downloadUrls.Add("https://ollama.com/download");
            downloadUrls.Add("https://ollama.com/library/llama3.2");
            downloadUrls.Add("https://serper.dev/");
            downloadUrls.Add("https://platform.deepseek.com/api_keys");
        }

        void BuildUi()
        {
            var scrollHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = SystemColors.Control
            };
            Controls.Add(scrollHost);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                RowCount = 5,
                ColumnCount = 1,
                Padding = new Padding(14),
                MinimumSize = new Size(640, 0)
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 84));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 286));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 174));
            scrollHost.Controls.Add(root);

            statusLabel = new Label { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(0, 6, 0, 8), TextAlign = ContentAlignment.MiddleLeft, Text = "Ready. Manual downloads are often faster; see the checklist below." };
            root.Controls.Add(statusLabel, 0, 0);

            var options = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, RowCount = 4, ColumnCount = 1, Padding = new Padding(0, 2, 0, 8) };
            options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            for (int i = 0; i < 4; i++) options.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            installNode = new CheckBox { Text = "Auto install Node.js LTS", Checked = false, AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(3, 4, 3, 4) };
            installMcp = new CheckBox { Text = "Auto install MCP server-memory (requires npm/npx)", Checked = false, AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(3, 4, 3, 4) };
            installOllama = new CheckBox { Text = "Auto install Ollama (optional, local models only)", Checked = false, AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(3, 4, 3, 4) };
            pullLlama = new CheckBox { Text = "Auto pull llama3.2:latest (optional, requires Ollama)", Checked = false, AutoSize = true, Dock = DockStyle.Top, Margin = new Padding(3, 4, 3, 4) };
            options.Controls.Add(installNode, 0, 0);
            options.Controls.Add(installMcp, 0, 1);
            options.Controls.Add(installOllama, 0, 2);
            options.Controls.Add(pullLlama, 0, 3);
            root.Controls.Add(options, 0, 1);

            var actions = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 3, Margin = new Padding(0, 4, 0, 4) };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.334f));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            actions.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
            detectButton = new Button { Text = "Detect", Dock = DockStyle.Fill };
            copyButton = new Button { Text = "Copy list", Dock = DockStyle.Fill };
            pagesButton = new Button { Text = "Open pages", Dock = DockStyle.Fill };
            baseFolderButton = new Button { Text = "Open base_files", Dock = DockStyle.Fill };
            localInstallButton = new Button { Text = "Run local installers", Dock = DockStyle.Fill };
            autoInstallButton = new Button { Text = "Auto install selected", Dock = DockStyle.Fill };
            detectButton.Click += async delegate { await DetectEnvironment(); };
            copyButton.Click += delegate { Clipboard.SetText(BuildChecklistText()); MessageBox.Show(this, "Checklist copied.", "Project949 setup"); };
            pagesButton.Click += delegate { OpenDownloadPages(); };
            baseFolderButton.Click += delegate { OpenFolder(BaseFilesDir); };
            localInstallButton.Click += delegate { RunLocalInstallers(); };
            autoInstallButton.Click += async delegate { await AutoInstallSelected(); };
            actions.Controls.Add(detectButton, 0, 0);
            actions.Controls.Add(copyButton, 1, 0);
            actions.Controls.Add(pagesButton, 2, 0);
            actions.Controls.Add(baseFolderButton, 0, 1);
            actions.Controls.Add(localInstallButton, 1, 1);
            actions.Controls.Add(autoInstallButton, 2, 1);
            root.Controls.Add(actions, 0, 2);

            listBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.White, Font = new Font("Consolas", 9f) };
            root.Controls.Add(listBox, 0, 3);

            logBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.White, Font = new Font("Consolas", 9f) };
            root.Controls.Add(logBox, 0, 4);
        }

        string BuildChecklistText()
        {
            var b = new StringBuilder();
            b.AppendLine("# Project949 v4.0 base files and downloads");
            b.AppendLine();
            b.AppendLine("Use the whole v4.0 folder as one isolated version.");
            b.AppendLine();
            b.AppendLine("## Included base files");
            AppendFile(b, AppExeName, "main program");
            AppendFile(b, "setup_base.exe", "setup and detection tool");
            AppendFile(b, "settings.json", "settings and preset storage");
            AppendFile(b, "mcp-memory.txt", "MCP text memory");
            AppendFile(b, "mcp-memory.jsonl", "MCP JSONL memory");
            AppendFile(b, "app.ico", "app icon");
            AppendFile(b, "README.md", "version notes");
            AppendFile(b, @"tietu\icon.png", "tray icon texture");
            AppendFile(b, @"tietu\idle.png", "idle sprite");
            AppendFile(b, @"tietu\speaking.png", "speaking sprite");
            AppendFile(b, @"tietu\emotions\angry.png", "emotion sprite");
            AppendFile(b, @"tietu\emotions\crying.png", "emotion sprite");
            AppendFile(b, @"tietu\emotions\focused.png", "emotion sprite");
            AppendFile(b, @"tietu\emotions\panic.png", "emotion sprite");
            AppendFile(b, @"tietu\emotions\shocked.png", "emotion sprite");
            AppendFile(b, @"tietu\emotions\shy.png", "emotion sprite");
            AppendFile(b, @"tietu\emotions\speechless.png", "emotion sprite");
            b.AppendLine();
            b.AppendLine("## Manual downloads");
            b.AppendLine("1. .NET Framework 4.8 Runtime");
            b.AppendLine("   URL: https://dotnet.microsoft.com/download/dotnet-framework/net48");
            b.AppendLine("   Put in base_files as: ndp48-web.exe or ndp48-x86-x64-allos-enu.exe");
            b.AppendLine();
            b.AppendLine("2. Node.js LTS");
            b.AppendLine("   URL: https://nodejs.org/en/download");
            b.AppendLine("   Put in base_files as: node-v*-x64.msi");
            b.AppendLine();
            b.AppendLine("3. MCP server-memory");
            b.AppendLine("   Install after Node.js: npm install -g @modelcontextprotocol/server-memory");
            b.AppendLine("   URL: https://www.npmjs.com/package/@modelcontextprotocol/server-memory");
            b.AppendLine();
            b.AppendLine("4. Ollama (optional)");
            b.AppendLine("   URL: https://ollama.com/download");
            b.AppendLine("   Put in base_files as: OllamaSetup.exe");
            b.AppendLine();
            b.AppendLine("5. llama3.2:latest (optional)");
            b.AppendLine("   Install after Ollama: ollama pull llama3.2:latest");
            b.AppendLine("   URL: https://ollama.com/library/llama3.2");
            b.AppendLine();
            b.AppendLine("6. Serper API Key (optional)");
            b.AppendLine("   URL: https://serper.dev/");
            b.AppendLine();
            b.AppendLine("7. DeepSeek API Key (optional)");
            b.AppendLine("   URL: https://platform.deepseek.com/api_keys");
            b.AppendLine();
            b.AppendLine("## base_files");
            b.AppendLine("Download installers manually into v4.0\\base_files, then click Run local installers.");
            b.AppendLine("Recognized names: node*.msi, node*.exe, ndp48*.exe, dotnet*.exe, OllamaSetup*.exe, ollama*.exe.");
            return b.ToString();
        }

        void AppendFile(StringBuilder b, string relativePath, string description)
        {
            b.Append("- ").Append(relativePath).Append(" : ").Append(description).Append(" : ");
            b.AppendLine(File.Exists(Path.Combine(RootDir, relativePath)) ? "OK" : "MISSING");
        }

        void WriteManifest()
        {
            try { File.WriteAllText(ManifestPath, BuildChecklistText(), new UTF8Encoding(false)); }
            catch (Exception ex) { Log("[WARN] Cannot write checklist: " + ex.Message); }
        }

        void EnsureBaseFilesFolder()
        {
            try { Directory.CreateDirectory(BaseFilesDir); }
            catch (Exception ex) { Log("[WARN] Cannot create base_files: " + ex.Message); }
        }

        async Task DetectEnvironment()
        {
            SetBusy(true);
            try
            {
                logBox.Clear();
                Log("=== Local file check ===");
                DetectPackageFile(AppExeName);
                DetectPackageFile("settings.json");
                DetectPackageFile(@"tietu\idle.png");
                DetectPackageFile(@"tietu\speaking.png");
                DetectPackageFile(@"tietu\icon.png");
                DetectPackageFile(@"tietu\emotions\focused.png");
                DetectLocalInstallers();

                Log("");
                Log("=== Environment check ===");
                await DetectCommand("node", "node --version");
                await DetectCommand("npm", "npm.cmd --version");
                await DetectCommand("npx", "npx.cmd --version");
                await DetectCommand("MCP server-memory", "npm.cmd list -g @modelcontextprotocol/server-memory --depth=0");
                await DetectCommand("winget", "winget --version");
                await DetectCommand("ollama", "ollama --version");
                await DetectCommand("ollama list", "ollama list");
                Log("Detection complete.");
            }
            finally
            {
                SetBusy(false);
            }
        }

        void DetectPackageFile(string relativePath)
        {
            var path = Path.Combine(RootDir, relativePath);
            Log((File.Exists(path) ? "[OK] " : "[MISSING] ") + relativePath);
        }

        void DetectLocalInstallers()
        {
            EnsureBaseFilesFolder();
            Log("");
            Log("=== base_files installer check ===");
            Log(FindFirstInstaller("node*.msi", "node*.exe") == null ? "[MISSING] Node.js installer" : "[OK] Node.js installer");
            Log(FindFirstInstaller("ndp48*.exe", "dotnet*.exe") == null ? "[MISSING] .NET Framework installer" : "[OK] .NET Framework installer");
            Log(FindFirstInstaller("OllamaSetup*.exe", "ollama*.exe") == null ? "[MISSING] Ollama installer" : "[OK] Ollama installer");
        }

        async Task DetectCommand(string name, string command)
        {
            var result = await RunCmd(command, 20000, false);
            if (result.ExitCode == 0) Log("[OK] " + name + ": " + OneLine(result.Output));
            else Log("[MISSING/UNAVAILABLE] " + name + ": " + OneLine(result.Output + " " + result.Error));
        }

        void OpenDownloadPages()
        {
            foreach (var url in downloadUrls)
            {
                try { Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true }); }
                catch (Exception ex) { Log("[WARN] Cannot open " + url + ": " + ex.Message); }
            }
        }

        void OpenFolder(string folder)
        {
            EnsureBaseFilesFolder();
            try { Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Open folder failed"); }
        }

        void RunLocalInstallers()
        {
            EnsureBaseFilesFolder();
            var found = false;
            StartInstaller(FindFirstInstaller("ndp48*.exe", "dotnet*.exe"), ref found);
            StartInstaller(FindFirstInstaller("node*.msi", "node*.exe"), ref found);
            StartInstaller(FindFirstInstaller("OllamaSetup*.exe", "ollama*.exe"), ref found);
            if (!found) MessageBox.Show(this, "No recognized installer was found in base_files.", "Project949 setup");
        }

        void StartInstaller(string file, ref bool found)
        {
            if (string.IsNullOrWhiteSpace(file)) return;
            found = true;
            try
            {
                Log("Starting installer: " + file);
                Process.Start(new ProcessStartInfo { FileName = file, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                Log("[FAILED] " + file + ": " + ex.Message);
            }
        }

        string FindFirstInstaller(params string[] patterns)
        {
            if (!Directory.Exists(BaseFilesDir)) return null;
            foreach (var pattern in patterns)
            {
                var files = Directory.GetFiles(BaseFilesDir, pattern, SearchOption.TopDirectoryOnly);
                if (files.Length > 0) return files[0];
            }
            return null;
        }

        async Task AutoInstallSelected()
        {
            SetBusy(true);
            try
            {
                Log("=== Auto install selected ===");
                if (installNode.Checked) await EnsureNodeAndNpx();
                if (installMcp.Checked) await EnsureMcpMemory();
                if (installOllama.Checked || pullLlama.Checked) await EnsureOllama();
                if (pullLlama.Checked) await PullLlama32();
                Log("=== Done ===");
                statusLabel.Text = "Install flow complete. You can run " + AppExeName + ".";
            }
            catch (Exception ex)
            {
                Log("[FAILED] " + ex.Message);
                statusLabel.Text = "Install failed. See log.";
            }
            finally
            {
                SetBusy(false);
            }
        }

        async Task EnsureNodeAndNpx()
        {
            RefreshKnownPaths();
            var npx = await RunCmd("npx.cmd --version", 20000, false);
            if (npx.ExitCode == 0)
            {
                Log("[OK] npx available: " + OneLine(npx.Output));
                return;
            }

            var winget = await RunCmd("winget --version", 20000, false);
            if (winget.ExitCode != 0) throw new InvalidOperationException("npx and winget were not found. Install Node.js LTS manually.");

            Log("Installing Node.js LTS via winget...");
            var install = await RunCmd("winget install OpenJS.NodeJS.LTS -e --accept-package-agreements --accept-source-agreements", 20 * 60 * 1000, true);
            Log(install.Output);
            if (install.ExitCode != 0) throw new InvalidOperationException("Node.js LTS install failed: " + OneLine(install.Error));
            RefreshKnownPaths();
        }

        async Task EnsureMcpMemory()
        {
            RefreshKnownPaths();
            var npm = await RunCmd("npm.cmd --version", 20000, false);
            if (npm.ExitCode != 0) throw new InvalidOperationException("npm is unavailable. Install Node.js LTS first.");

            Log("Installing MCP server-memory...");
            var install = await RunCmd("npm.cmd install -g @modelcontextprotocol/server-memory", 10 * 60 * 1000, true);
            Log(install.Output);
            if (install.ExitCode != 0) throw new InvalidOperationException("server-memory install failed: " + OneLine(install.Error));
            Log("[OK] MCP server-memory installed.");
        }

        async Task EnsureOllama()
        {
            RefreshKnownPaths();
            var ollama = await RunCmd("ollama --version", 20000, false);
            if (ollama.ExitCode == 0)
            {
                Log("[OK] Ollama available: " + OneLine(ollama.Output));
                return;
            }

            var winget = await RunCmd("winget --version", 20000, false);
            if (winget.ExitCode != 0) throw new InvalidOperationException("Ollama and winget were not found. Install Ollama manually.");

            Log("Installing Ollama via winget...");
            var install = await RunCmd("winget install Ollama.Ollama -e --accept-package-agreements --accept-source-agreements", 20 * 60 * 1000, true);
            Log(install.Output);
            if (install.ExitCode != 0) throw new InvalidOperationException("Ollama install failed: " + OneLine(install.Error));
            RefreshKnownPaths();
        }

        async Task PullLlama32()
        {
            RefreshKnownPaths();
            Log("Pulling llama3.2:latest. First download can take a while.");
            var result = await RunCmd("ollama pull llama3.2:latest", 60 * 60 * 1000, true);
            Log(result.Output);
            if (result.ExitCode != 0) throw new InvalidOperationException("llama3.2 pull failed: " + OneLine(result.Error));
            Log("[OK] llama3.2:latest ready.");
        }

        async Task<RunResult> RunCmd(string command, int timeoutMs, bool verbose)
        {
            return await Task.Run(delegate
            {
                var start = new ProcessStartInfo();
                start.FileName = "cmd.exe";
                start.Arguments = "/c " + command;
                start.UseShellExecute = false;
                start.RedirectStandardOutput = true;
                start.RedirectStandardError = true;
                start.CreateNoWindow = true;
                start.StandardOutputEncoding = Encoding.UTF8;
                start.StandardErrorEncoding = Encoding.UTF8;

                using (var process = new Process())
                {
                    process.StartInfo = start;
                    if (verbose) Log("$ " + command);
                    process.Start();
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errorTask = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(timeoutMs))
                    {
                        try { process.Kill(); } catch { }
                        return new RunResult { ExitCode = -1, Output = outputTask.Result, Error = "Command timed out." };
                    }
                    return new RunResult { ExitCode = process.ExitCode, Output = outputTask.Result, Error = errorTask.Result };
                }
            });
        }

        void RefreshKnownPaths()
        {
            var current = Environment.GetEnvironmentVariable("PATH") ?? "";
            AddPath(ref current, Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + @"\nodejs");
            AddPath(ref current, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Programs\Ollama");
            Environment.SetEnvironmentVariable("PATH", current);
        }

        void AddPath(ref string path, string candidate)
        {
            if (Directory.Exists(candidate) && path.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) < 0) path = candidate + ";" + path;
        }

        void SetBusy(bool busy)
        {
            detectButton.Enabled = !busy;
            copyButton.Enabled = !busy;
            pagesButton.Enabled = !busy;
            baseFolderButton.Enabled = !busy;
            localInstallButton.Enabled = !busy;
            autoInstallButton.Enabled = !busy;
            statusLabel.Text = busy ? "Working..." : "Ready.";
        }

        void Log(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(Log), text);
                return;
            }
            logBox.AppendText(text.Replace("\r\n", "\n").Replace("\n", "\r\n") + "\r\n");
        }

        static string OneLine(string text)
        {
            text = (text ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length > 180 ? text.Substring(0, 180) + "..." : text;
        }

        sealed class RunResult
        {
            public int ExitCode;
            public string Output;
            public string Error;
        }
    }
}

using System;
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
        CheckBox nodeRequired;
        CheckBox mcpRequired;
        CheckBox ollamaOptional;
        CheckBox llamaOptional;
        Button detectButton;
        Button installButton;
        Button folderButton;
        TextBox logBox;
        Label statusLabel;

        public SetupForm()
        {
            Text = "Project949 setup_base";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(760, 560);
            Size = new Size(820, 620);
            Font = new Font("Microsoft YaHei UI", 9f);
            BuildUi();
        }

        void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, Padding = new Padding(14) };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            statusLabel = new Label { Dock = DockStyle.Fill, Text = "首次使用安装器：必选项会自动安装，可选项按需勾选。", TextAlign = ContentAlignment.MiddleLeft };
            root.Controls.Add(statusLabel, 0, 0);

            var options = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1 };
            nodeRequired = new CheckBox { Text = "必选：Node.js / npm / npx（MCP server-memory 需要）", Checked = true, Enabled = false, AutoSize = true };
            mcpRequired = new CheckBox { Text = "必选：@modelcontextprotocol/server-memory（MCP 记忆服务）", Checked = true, Enabled = false, AutoSize = true };
            ollamaOptional = new CheckBox { Text = "可选：安装 Ollama（本地模型运行器）", Checked = false, AutoSize = true };
            llamaOptional = new CheckBox { Text = "可选：拉取 llama3.2:latest（需要 Ollama）", Checked = false, AutoSize = true };
            options.Controls.Add(nodeRequired, 0, 0);
            options.Controls.Add(mcpRequired, 0, 1);
            options.Controls.Add(ollamaOptional, 0, 2);
            options.Controls.Add(llamaOptional, 0, 3);
            root.Controls.Add(options, 0, 1);

            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            detectButton = new Button { Text = "检测环境", Width = 110, Height = 34 };
            installButton = new Button { Text = "开始安装", Width = 110, Height = 34 };
            folderButton = new Button { Text = "打开文件夹", Width = 110, Height = 34 };
            detectButton.Click += async delegate { await DetectEnvironment(); };
            installButton.Click += async delegate { await InstallSelected(); };
            folderButton.Click += delegate { Process.Start(new ProcessStartInfo { FileName = AppDomain.CurrentDomain.BaseDirectory, UseShellExecute = true }); };
            actions.Controls.AddRange(new Control[] { detectButton, installButton, folderButton });
            root.Controls.Add(actions, 0, 2);

            logBox = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.White };
            root.Controls.Add(logBox, 0, 3);
        }

        async Task DetectEnvironment()
        {
            SetBusy(true);
            try
            {
                Log("=== 环境检测 ===");
                await DetectCommand("node", "node --version");
                await DetectCommand("npm", "npm.cmd --version");
                await DetectCommand("npx", "npx.cmd --version");
                await DetectCommand("winget", "winget --version");
                await DetectCommand("ollama", "ollama --version");
                await DetectCommand("ollama list", "ollama list");
                Log("检测完成。");
            }
            finally
            {
                SetBusy(false);
            }
        }

        async Task DetectCommand(string name, string command)
        {
            var result = await RunCmd(command, 20000, false);
            if (result.ExitCode == 0) Log("[OK] " + name + ": " + OneLine(result.Output));
            else Log("[缺失/不可用] " + name + ": " + OneLine(result.Output + " " + result.Error));
        }

        async Task InstallSelected()
        {
            SetBusy(true);
            try
            {
                Log("=== 开始安装 ===");
                await EnsureNodeAndNpx();
                await EnsureMcpMemory();
                if (ollamaOptional.Checked || llamaOptional.Checked) await EnsureOllama();
                if (llamaOptional.Checked) await PullLlama32();
                Log("=== 完成 ===");
                statusLabel.Text = "安装流程完成。可运行 Project949_v3.0_lightweight.exe。";
            }
            catch (Exception ex)
            {
                Log("[失败] " + ex.Message);
                statusLabel.Text = "安装遇到问题，请看日志。";
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
                Log("[OK] npx 已可用：" + OneLine(npx.Output));
                return;
            }

            var winget = await RunCmd("winget --version", 20000, false);
            if (winget.ExitCode != 0) throw new InvalidOperationException("未检测到 npx，也未检测到 winget。请先手动安装 Node.js LTS。官方下载：https://nodejs.org/");

            Log("正在通过 winget 安装 Node.js LTS...");
            var install = await RunCmd("winget install OpenJS.NodeJS.LTS -e --accept-package-agreements --accept-source-agreements", 20 * 60 * 1000, true);
            Log(install.Output);
            if (install.ExitCode != 0) throw new InvalidOperationException("Node.js LTS 安装失败：" + OneLine(install.Error));
            RefreshKnownPaths();
        }

        async Task EnsureMcpMemory()
        {
            RefreshKnownPaths();
            var npm = await RunCmd("npm.cmd --version", 20000, false);
            if (npm.ExitCode != 0) throw new InvalidOperationException("npm 不可用，无法安装 server-memory。请重新打开 setup_base 或重启电脑后再试。");

            Log("正在安装必选 MCP server-memory...");
            var install = await RunCmd("npm.cmd install -g @modelcontextprotocol/server-memory", 10 * 60 * 1000, true);
            Log(install.Output);
            if (install.ExitCode != 0) throw new InvalidOperationException("server-memory 安装失败：" + OneLine(install.Error));
            Log("[OK] MCP server-memory 已安装。程序仍会使用 npx.cmd --yes @modelcontextprotocol/server-memory 启动。 ");
        }

        async Task EnsureOllama()
        {
            RefreshKnownPaths();
            var ollama = await RunCmd("ollama --version", 20000, false);
            if (ollama.ExitCode == 0)
            {
                Log("[OK] Ollama 已可用：" + OneLine(ollama.Output));
                return;
            }

            var winget = await RunCmd("winget --version", 20000, false);
            if (winget.ExitCode != 0) throw new InvalidOperationException("未检测到 Ollama，也未检测到 winget。请手动安装 Ollama：https://ollama.com/download");

            Log("正在通过 winget 安装 Ollama...");
            var install = await RunCmd("winget install Ollama.Ollama -e --accept-package-agreements --accept-source-agreements", 20 * 60 * 1000, true);
            Log(install.Output);
            if (install.ExitCode != 0) throw new InvalidOperationException("Ollama 安装失败：" + OneLine(install.Error));
            RefreshKnownPaths();
        }

        async Task PullLlama32()
        {
            RefreshKnownPaths();
            Log("准备拉取 llama3.2:latest。首次下载可能较久。 ");
            var result = await RunCmd("ollama pull llama3.2:latest", 60 * 60 * 1000, true);
            Log(result.Output);
            if (result.ExitCode != 0) throw new InvalidOperationException("llama3.2 拉取失败：" + OneLine(result.Error));
            Log("[OK] llama3.2:latest 已准备好。 ");
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
                        return new RunResult { ExitCode = -1, Output = outputTask.Result, Error = "命令超时。" };
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
            AddPath(ref current, Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData) + @"\Microsoft\WinGet\Packages");
            Environment.SetEnvironmentVariable("PATH", current);
        }

        void AddPath(ref string path, string candidate)
        {
            if (Directory.Exists(candidate) && path.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) < 0) path = candidate + ";" + path;
        }

        void SetBusy(bool busy)
        {
            detectButton.Enabled = !busy;
            installButton.Enabled = !busy;
            folderButton.Enabled = !busy;
            statusLabel.Text = busy ? "正在执行，请稍等..." : "就绪。";
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
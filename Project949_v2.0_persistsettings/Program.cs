using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Speech.Recognition;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace DesktopCyberPetNative
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var appState = new PetState();
            Application.Run(new PetForm(appState));
        }
    }

    sealed class PetState
    {
        public readonly Settings Settings = Settings.Load();
        public readonly List<ChatMessage> Messages = new List<ChatMessage>();
        public ChatForm ChatForm;
        public string LastBubble;

        public PetState()
        {
            LastBubble = Settings.PetName + "在桌面上待命。双击我打开控制台。";
        }

        public void ShowChat(Form owner)
        {
            if (ChatForm == null || ChatForm.IsDisposed)
            {
                ChatForm = new ChatForm(this);
            }
            ChatForm.Show();
            ChatForm.WindowState = FormWindowState.Normal;
            ChatForm.Activate();
        }
    }
    sealed class Settings
    {
        public string PetName = "Cyber Pet";
        public string Provider = "local-ollama-llama32";
        public string Protocol = "http";
        public string Host = "127.0.0.1";
        public string Port = "11434";
        public string Path = "/api/chat";
        public string Model = "llama3.2:latest";
        public string ApiKey = "";
        public string Persona = "你是一个住在用户桌面上的 cyber pet 智能体。你说中文，语气亲切、机灵、简洁。";
        public string ChatMemory = "";
        public string UserOpinion = "";
        public bool MemoryEnabled = true;
        public string LocalSpeed = "快速";
        public double Temperature = 0.7;
        public double SpriteScale = 1.0;
        public List<ApiPresetRecord> ApiPresets = new List<ApiPresetRecord>();

        static string PortableConfigPath
        {
            get { return System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json"); }
        }

        static string RoamingConfigPath
        {
            get
            {
                var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DesktopCyberPet");
                Directory.CreateDirectory(dir);
                return System.IO.Path.Combine(dir, "settings.json");
            }
        }

        static string ReadConfigPath()
        {
            if (File.Exists(PortableConfigPath)) return PortableConfigPath;
            if (File.Exists(RoamingConfigPath)) return RoamingConfigPath;
            return PortableConfigPath;
        }

        static void WriteConfigFile(string json)
        {
            try
            {
                File.WriteAllText(PortableConfigPath, json, Encoding.UTF8);
            }
            catch
            {
                File.WriteAllText(RoamingConfigPath, json, Encoding.UTF8);
            }
        }

        public string Endpoint()
        {
            var cleanPath = Path.StartsWith("/") ? Path : "/" + Path;
            return Protocol + "://" + Host + ":" + Port + cleanPath;
        }

        public ApiPresetRecord FindApiPreset(string provider)
        {
            if (ApiPresets == null) ApiPresets = new List<ApiPresetRecord>();
            foreach (var preset in ApiPresets)
            {
                if (preset.Provider == provider) return preset;
            }
            return null;
        }

        public void SaveApiPreset(string provider)
        {
            if (ApiPresets == null) ApiPresets = new List<ApiPresetRecord>();
            var preset = FindApiPreset(provider);
            if (preset == null)
            {
                preset = new ApiPresetRecord();
                preset.Provider = provider;
                ApiPresets.Add(preset);
            }
            preset.Protocol = Protocol;
            preset.Host = Host;
            preset.Port = Port;
            preset.Path = Path;
            preset.Model = Model;
            preset.ApiKey = ApiKey;
        }

        public bool LoadApiPreset(string provider)
        {
            var preset = FindApiPreset(provider);
            if (preset == null) return false;
            Protocol = preset.Protocol;
            Host = preset.Host;
            Port = preset.Port;
            Path = preset.Path;
            Model = preset.Model;
            ApiKey = preset.ApiKey;
            return true;
        }

        public void Save()
        {
            var serializer = new JavaScriptSerializer();
            SaveApiPreset(Provider);
            WriteConfigFile(serializer.Serialize(this));
        }

        public static Settings Load()
        {
            try
            {
                var path = ReadConfigPath();
                if (!File.Exists(path)) return new Settings();
                var serializer = new JavaScriptSerializer();
                var raw = File.ReadAllText(path, Encoding.UTF8);
                var settings = serializer.Deserialize<Settings>(raw) ?? new Settings();
                if (settings.ApiPresets == null) settings.ApiPresets = new List<ApiPresetRecord>();
                if (string.IsNullOrWhiteSpace(settings.PetName)) settings.PetName = "Cyber Pet";
                if (settings.Persona == null) settings.Persona = "";
                if (settings.ChatMemory == null) settings.ChatMemory = "";
                if (settings.UserOpinion == null) settings.UserOpinion = "";
                if (string.IsNullOrWhiteSpace(settings.LocalSpeed)) settings.LocalSpeed = "快速";
                if (raw.IndexOf("\"MemoryEnabled\"", StringComparison.OrdinalIgnoreCase) < 0) settings.MemoryEnabled = true;
                if (settings.SpriteScale <= 0) settings.SpriteScale = 1.0;
                settings.SpriteScale = Math.Max(0.4, Math.Min(1.0, settings.SpriteScale));
                return settings;
            }
            catch
            {
                return new Settings();
            }
        }
    }

    sealed class ApiPresetRecord
    {
        public string Provider { get; set; }
        public string Protocol { get; set; }
        public string Host { get; set; }
        public string Port { get; set; }
        public string Path { get; set; }
        public string Model { get; set; }
        public string ApiKey { get; set; }
    }
    sealed class ChatMessage
    {
        public string role { get; set; }
        public object content { get; set; }

        public ChatMessage() { }

        public ChatMessage(string role, object content)
        {
            this.role = role;
            this.content = content;
        }
    }

    sealed class PetForm : Form
    {
        readonly PetState state;
        readonly Timer animationTimer = new Timer();
        Point dragOffset;
        bool dragging;
        int tick;
        bool thinking;
        int speechPulse;
        int speechJitter;
        static readonly Color TransparentFill = Color.FromArgb(1, 2, 3);
        Image idleSprite;
        Image speakingSprite;
        readonly Dictionary<string, Image> emotionSprites = new Dictionary<string, Image>();
        string expressionName;
        int expressionTicks;

        public PetForm(PetState state)
        {
            this.state = state;
            Text = state.Settings.PetName;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            Size = new Size(260, 260);
            Location = new Point(Screen.PrimaryScreen.WorkingArea.Right - Width - 36, Screen.PrimaryScreen.WorkingArea.Bottom - Height - 36);
            TopMost = true;
            ShowInTaskbar = false;
            BackColor = TransparentFill;
            TransparencyKey = TransparentFill;
            DoubleBuffered = true;
            LoadSpriteImages();

            animationTimer.Interval = 167;
            animationTimer.Tick += delegate
            {
                tick++;
                if (speechPulse > 0) speechPulse--;
                if (expressionTicks > 0) expressionTicks--;
                Invalidate();
            };
            animationTimer.Start();

            MouseDown += PetMouseDown;
            MouseMove += PetMouseMove;
            MouseUp += delegate { dragging = false; };
            DoubleClick += delegate { state.ShowChat(this); };

            var menu = new ContextMenuStrip();
            menu.Items.Add("打开控制台", null, delegate { state.ShowChat(this); });
            menu.Items.Add("保持置顶: 开", null, delegate
            {
                TopMost = !TopMost;
                ((ToolStripMenuItem)menu.Items[1]).Text = "保持置顶: " + (TopMost ? "开" : "关");
            });
            menu.Items.Add("退出", null, delegate { Application.Exit(); });
            ContextMenuStrip = menu;
        }

        public void SetThinking(bool value)
        {
            thinking = value;
            if (!value)
            {
                speechPulse = 0;
            }
            Invalidate();
        }

        public void PulseSpeaking()
        {
            thinking = true;
            speechPulse = 2;
            speechJitter++;
            Invalidate();
        }

        public void SetExpression(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                expressionName = null;
                expressionTicks = 0;
                Invalidate();
                return;
            }

            name = name.Trim().ToLowerInvariant();
            if (!emotionSprites.ContainsKey(name)) return;
            expressionName = name;
            expressionTicks = 42;
            Invalidate();
        }
        public void RefreshFromSettings()
        {
            Text = state.Settings.PetName;
            Invalidate();
        }

        void PetMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left) return;
            dragging = true;
            dragOffset = e.Location;
        }

        void PetMouseMove(object sender, MouseEventArgs e)
        {
            if (!dragging) return;
            Location = new Point(Location.X + e.X - dragOffset.X, Location.Y + e.Y - dragOffset.Y);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            DrawBubble(g);
            DrawSprite(g);
            DrawPetName(g);
        }

        void DrawBubble(Graphics g)
        {
            var text = state.LastBubble ?? "";
            if (text.Length > 58) text = text.Substring(0, 58) + "...";
            var rect = new Rectangle(12, 8, Width - 24, 54);
            using (var path = Rounded(rect, 10))
            using (var fill = new SolidBrush(Color.FromArgb(235, 28, 36, 46)))
            using (var pen = new Pen(Color.FromArgb(160, 94, 224, 184)))
            using (var textBrush = new SolidBrush(Color.FromArgb(238, 245, 241)))
            using (var font = new Font("Microsoft YaHei UI", 9f))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
                g.DrawString(text, font, textBrush, rect, sf);
            }
        }

        void DrawPetName(Graphics g)
        {
            var name = string.IsNullOrWhiteSpace(state.Settings.PetName) ? "Cyber Pet" : state.Settings.PetName;
            var rect = new Rectangle(30, 236, Width - 60, 20);
            using (var brush = new SolidBrush(Color.FromArgb(238, 245, 241)))
            using (var font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter })
            {
                g.DrawString(name, font, brush, rect, sf);
            }
        }
        void LoadSpriteImages()
        {
            var exeDir = AppDomain.CurrentDomain.BaseDirectory;
            var bundledDir = System.IO.Path.Combine(exeDir, "tietu");
            var desktopDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "tietu");
            var dir = Directory.Exists(bundledDir) ? bundledDir : desktopDir;
            idleSprite = LoadSpriteImage(System.IO.Path.Combine(dir, "idle.png"));
            speakingSprite = LoadSpriteImage(System.IO.Path.Combine(dir, "speaking.png"));
            LoadEmotionSprites(System.IO.Path.Combine(dir, "emotions"));
        }

        void LoadEmotionSprites(string dir)
        {
            if (!Directory.Exists(dir)) return;
            foreach (var file in Directory.GetFiles(dir, "*.png"))
            {
                var key = System.IO.Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                if (emotionSprites.ContainsKey(key)) continue;
                var image = LoadSpriteImage(file);
                if (image != null) emotionSprites[key] = image;
            }
        }
        static Image LoadSpriteImage(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var bytes = File.ReadAllBytes(path);
                using (var stream = new MemoryStream(bytes))
                using (var image = Image.FromStream(stream))
                {
                    return new Bitmap(image);
                }
            }
            catch
            {
                return null;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                animationTimer.Dispose();
                if (idleSprite != null) idleSprite.Dispose();
                if (speakingSprite != null) speakingSprite.Dispose();
                foreach (var sprite in emotionSprites.Values) sprite.Dispose();
                emotionSprites.Clear();
            }
            base.Dispose(disposing);
        }

        void DrawShadow(Graphics g)
        {
            var rect = new RectangleF(72, 218, 116, 18);
            using (var brush = new SolidBrush(Color.FromArgb(80, 0, 0, 0)))
            {
                g.FillEllipse(brush, rect);
            }
        }

        void DrawSprite(Graphics g)
        {
            Image sprite = null;
            if (expressionTicks > 0 && !string.IsNullOrWhiteSpace(expressionName) && emotionSprites.ContainsKey(expressionName))
            {
                sprite = emotionSprites[expressionName];
            }
            if (sprite == null) sprite = thinking && speakingSprite != null ? speakingSprite : idleSprite;
            if (sprite == null && speakingSprite != null) sprite = speakingSprite;
            if (sprite != null)
            {
                DrawSpriteImage(g, sprite);
                return;
            }
            DrawVectorSprite(g);
        }

        float SpriteScale()
        {
            return (float)Math.Max(0.4, Math.Min(1.0, state.Settings.SpriteScale));
        }
        void DrawSpriteImage(Graphics g, Image sprite)
        {
            float baseWidth = 216f * SpriteScale();
            float baseHeight = baseWidth * sprite.Height / sprite.Width;
            float stretch = thinking ? 0f : (float)Math.Sin(tick / 4.0) * 0.045f;
            int width = Math.Max(1, (int)Math.Round(baseWidth));
            int height = Math.Max(1, (int)Math.Round(baseHeight * (1f + stretch)));
            float bottom = 230f;
            float y = bottom - height;

            if (thinking)
            {
                y += speechPulse > 0 ? (speechJitter % 2 == 0 ? -8f * SpriteScale() : 6f * SpriteScale()) : 0f;
            }

            int x = (int)Math.Round((Width - width) / 2f);
            using (var frame = BuildKeyedSpriteFrame(sprite, width, height))
            {
                g.DrawImageUnscaled(frame, x, (int)Math.Round(y));
            }
        }

        static Bitmap BuildKeyedSpriteFrame(Image sprite, int width, int height)
        {
            var frame = new Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var frameGraphics = Graphics.FromImage(frame))
            {
                frameGraphics.Clear(Color.Transparent);
                frameGraphics.CompositingMode = CompositingMode.SourceOver;
                frameGraphics.CompositingQuality = CompositingQuality.HighQuality;
                frameGraphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                frameGraphics.SmoothingMode = SmoothingMode.AntiAlias;
                frameGraphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                frameGraphics.DrawImage(sprite, 0, 0, width, height);
            }

            for (int y = 0; y < frame.Height; y++)
            {
                for (int x = 0; x < frame.Width; x++)
                {
                    var pixel = frame.GetPixel(x, y);
                    if (pixel.A < 150)
                    {
                        frame.SetPixel(x, y, TransparentFill);
                    }
                    else
                    {
                        frame.SetPixel(x, y, Color.FromArgb(255, pixel.R, pixel.G, pixel.B));
                    }
                }
            }
            return frame;
        }

        void DrawVectorSprite(Graphics g)
        {
            float offset = thinking && speechPulse > 0 ? (speechJitter % 2 == 0 ? -8f * SpriteScale() : 6f * SpriteScale()) : 0f;
            float stretch = thinking ? 0f : (float)Math.Sin(tick / 4.0) * 0.045f;
            g.TranslateTransform(0, offset);
            g.TranslateTransform(130, 230);
            g.ScaleTransform(1f, 1f + stretch);
            g.TranslateTransform(-130, -230);
            using (var panel = new SolidBrush(Color.FromArgb(31, 53, 61)))
            using (var line = new Pen(Color.FromArgb(130, 238, 245, 241), 2f))
            using (var glow = new SolidBrush(thinking ? Color.FromArgb(255, 209, 102) : Color.FromArgb(94, 224, 184)))
            using (var accentPen = new Pen(thinking ? Color.FromArgb(255, 209, 102) : Color.FromArgb(94, 224, 184), 3f))
            using (var mastBrush = new SolidBrush(Color.FromArgb(120, 238, 245, 241)))
            using (var antennaBrush = new SolidBrush(Color.FromArgb(255, 209, 102)))
            {
                g.FillRectangle(mastBrush, 128, 76, 4, 28);
                g.FillEllipse(antennaBrush, 121, 62, 18, 18);
                g.FillPie(panel, 64, 78, 52, 54, 198, 212);
                g.FillPie(panel, 144, 78, 52, 54, 130, 212);
                g.DrawPie(line, 64, 78, 52, 54, 198, 212);
                g.DrawPie(line, 144, 78, 52, 54, 130, 212);

                using (var headPath = Rounded(new Rectangle(52, 92, 156, 118), 30))
                using (var headBrush = new LinearGradientBrush(new Rectangle(52, 92, 156, 118), Color.FromArgb(38, 60, 68), Color.FromArgb(10, 16, 22), 90f))
                {
                    g.FillPath(headBrush, headPath);
                    g.DrawPath(line, headPath);
                }

                using (var screen = Rounded(new Rectangle(76, 124, 108, 48), 8))
                using (var screenBrush = new SolidBrush(Color.FromArgb(6, 16, 15)))
                using (var screenPen = new Pen(Color.FromArgb(180, 94, 224, 184), 1.5f))
                {
                    g.FillPath(screenBrush, screen);
                    g.DrawPath(screenPen, screen);
                }

                int eyeHeight = thinking ? 8 : 20;
                int eyeY = thinking ? 143 : 135;
                g.FillEllipse(glow, 96, eyeY, 16, eyeHeight);
                g.FillEllipse(glow, 148, eyeY, 16, eyeHeight);
                g.DrawArc(accentPen, 117, 154, 28, thinking ? 6 : 18, 8, 164);
                g.FillEllipse(antennaBrush, 125, 184, 12, 12);
            }
            g.ResetTransform();
        }

        static GraphicsPath Rounded(Rectangle rect, int radius)
        {
            var path = new GraphicsPath();
            int d = radius * 2;
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    sealed class ChatForm : Form
    {
        readonly PetState state;
        ComboBox host;
        ComboBox petName;
        ComboBox port;
        ComboBox path;
        ComboBox protocol;
        ComboBox providerPreset;
        ComboBox modePreset;
        ComboBox model;
        ComboBox apiKey;
        TextBox persona;
        TrackBar temperature;
        TrackBar spriteSize;
        TextBox transcript;
        TextBox memoryChat;
        TextBox memoryOpinion;
        CheckBox memoryEnabled;
        TextBox input;
                readonly Timer autoSaveTimer = new Timer();
        ListBox fileList;
        Button sendButton;
        Button stopButton;
        Button micButton;
        SpeechRecognitionEngine recognizer;
        bool listening;
        bool loadingSettings;
        volatile bool cancelRequested;
        HttpWebRequest activeRequest;
        ChatMessage activeAssistantMessage;
        string lastExpressionCue;
        bool autoExpressionUsed;
        int lastAutoExpressionAt;
        volatile bool expressionSuppressed;
        readonly List<string> files = new List<string>();

        public ChatForm(PetState state)
        {
            this.state = state;
            Text = "Cyber Pet 控制台";
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(960, 720);
            Size = new Size(1080, 780);
            Font = new Font("Microsoft YaHei UI", 9f);
            autoSaveTimer.Interval = 1000;
            autoSaveTimer.Tick += delegate { autoSaveTimer.Stop(); SaveSettingsFromUi(); };
            BuildUi();
            LoadSettingsToUi();
            RenderMessages();
            AttachAutoSaveHandlers();
        }

        void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(12) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 320));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(root);

            var settings = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 19, ColumnCount = 2, AutoScroll = true };
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.Controls.Add(settings, 0, 0);

            modePreset = EditableCombo("API模式", "本地部署模式");
            modePreset.SelectedIndexChanged += delegate { RefreshProviderPresetsForMode(); ApplySelectedProviderPreset(false); };
            providerPreset = EditableCombo();
            providerPreset.SelectedIndexChanged += delegate { ApplySelectedProviderPreset(false); };
            protocol = EditableCombo("http", "https");
            host = EditableCombo("api.deepseek.com", "192.168.112.1", "127.0.0.1", "localhost");
            petName = EditableCombo("Cyber Pet", "NN", "Project949");
            port = EditableCombo("443", "11434", "80", "8000", "5000");
            path = EditableCombo("/chat/completions", "/api/chat", "/v1/chat/completions");
            model = EditableCombo("deepseek-chat", "deepseek-reasoner", "hf.co/HauhauCS/Qwen3.6-35B-A3B-Uncensored-HauhauCS-Aggressive:IQ3_M", "llama3.2:latest");
            apiKey = EditableCombo();
            persona = new TextBox { Multiline = true, Height = 120, ScrollBars = ScrollBars.Vertical };
            temperature = new TrackBar { Minimum = 0, Maximum = 15, TickFrequency = 3, Value = 7 };
            spriteSize = new TrackBar { Minimum = 40, Maximum = 100, TickFrequency = 10, Value = 100 };

            memoryEnabled = new CheckBox { Text = "启用外部记忆", Checked = true, AutoSize = true };
            AddRow(settings, "模式", modePreset);
            AddRow(settings, "AI入口", providerPreset);
            AddRow(settings, "宠物名", petName);
            AddRow(settings, "协议", protocol);
            AddRow(settings, "主机", host);
            AddRow(settings, "端口", port);
            AddRow(settings, "路径", path);
            AddRow(settings, "模型", model);
            AddRow(settings, "API Key", apiKey);
            AddRow(settings, "Persona", persona);
            AddRow(settings, "温度", temperature);
            AddRow(settings, "贴图大小", spriteSize);

            AddRow(settings, "记忆", memoryEnabled);
            var localPreset = new Button { Text = "套用所选 AI 入口", Height = 34, Dock = DockStyle.Top };
            localPreset.Click += delegate { ApplySelectedProviderPreset(true); };
            settings.Controls.Add(localPreset, 0, 14);
            settings.SetColumnSpan(localPreset, 2);

            var save = new Button { Text = "保存配置", Height = 34, Dock = DockStyle.Top };
            save.Click += delegate { SaveSettingsFromUi(); MessageBox.Show(this, "配置已保存。", "Cyber Pet"); };
            var test = new Button { Text = "测试连接", Height = 34, Dock = DockStyle.Top };
            test.Click += async delegate { await TestConnection(); };
            settings.Controls.Add(save, 0, 15);
            settings.SetColumnSpan(save, 2);
            settings.Controls.Add(test, 0, 16);
            settings.SetColumnSpan(test, 2);

            var chat = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1 };
            chat.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            chat.RowStyles.Add(new RowStyle(SizeType.Absolute, 168));
            chat.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            chat.RowStyles.Add(new RowStyle(SizeType.Absolute, 72));
            chat.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            root.Controls.Add(chat, 1, 0);

            transcript = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, BackColor = Color.White };
            var memoryGroup = new GroupBox { Dock = DockStyle.Fill, Text = "外部记忆" };
            var memoryLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, Padding = new Padding(6) };
            memoryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            memoryLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            memoryLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
            memoryLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            memoryLayout.Controls.Add(new Label { Text = "聊天内容记忆", Dock = DockStyle.Fill }, 0, 0);
            memoryLayout.Controls.Add(new Label { Text = "情感/情绪（停用）", Dock = DockStyle.Fill }, 1, 0);
            memoryChat = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = false, ScrollBars = ScrollBars.Vertical };
            memoryOpinion = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = false, ScrollBars = ScrollBars.Vertical };
            memoryChat.TextChanged += delegate { AutoSaveSettings(); };
            memoryOpinion.TextChanged += delegate { AutoSaveSettings(); };
            memoryLayout.Controls.Add(memoryChat, 0, 1);
            memoryLayout.Controls.Add(memoryOpinion, 1, 1);
            memoryGroup.Controls.Add(memoryLayout);

            input = new TextBox { Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical };
            input.KeyDown += async delegate(object sender, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter && e.Control)
                {
                    e.SuppressKeyPress = true;
                    await SendMessage();
                }
            };

            fileList = new ListBox { Dock = DockStyle.Fill };
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            var addFile = new Button { Text = "添加文件", Width = 92, Height = 32 };
            addFile.Click += delegate { AddFiles(); };
            var clearFiles = new Button { Text = "清空文件", Width = 92, Height = 32 };
            clearFiles.Click += delegate { files.Clear(); RenderFiles(); };
            var clearChat = new Button { Text = "清空对话", Width = 92, Height = 32 };
            clearChat.Click += delegate { state.Messages.Clear(); RenderMessages(); };
            micButton = new Button { Text = "语音输入", Width = 86, Height = 32 };
            micButton.Click += delegate { ToggleSpeechInput(); };
            stopButton = new Button { Text = "打断", Width = 86, Height = 32, Enabled = false };
            stopButton.Click += delegate { StopGeneration(); };
            sendButton = new Button { Text = "发送", Width = 86, Height = 32 };
            sendButton.Click += async delegate { await SendMessage(); };
            actions.WrapContents = false;
            actions.AutoScroll = true;
            actions.Controls.AddRange(new Control[] { addFile, clearFiles, clearChat, micButton, stopButton, sendButton });

            chat.Controls.Add(transcript, 0, 0);
            chat.Controls.Add(memoryGroup, 0, 1);
            chat.Controls.Add(input, 0, 2);
            chat.Controls.Add(fileList, 0, 3);
            chat.Controls.Add(actions, 0, 4);
        }

        static ComboBox EditableCombo(params object[] items)
        {
            var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDown };
            combo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            combo.AutoCompleteSource = AutoCompleteSource.ListItems;
            if (items != null && items.Length > 0) combo.Items.AddRange(items);
            return combo;
        }

        bool IsLocalMode()
        {
            var text = modePreset == null ? "" : (modePreset.Text ?? "");
            return text.IndexOf("本地", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("local", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void RefreshProviderPresetsForMode()
        {
            if (providerPreset == null || modePreset == null) return;
            var oldText = providerPreset.Text;
            var wasLoading = loadingSettings;
            loadingSettings = true;
            try
            {
                providerPreset.Items.Clear();
                if (IsLocalMode())
                {
                    providerPreset.Items.AddRange(new object[] { "本地 Ollama - Qwen IQ3_M", "本地 Ollama - llama3.2" });
                    if (ProviderIndexFromText(oldText) == 0) providerPreset.Text = "本地 Ollama - Qwen IQ3_M";
                    else if (ProviderIndexFromText(oldText) == 1) providerPreset.Text = "本地 Ollama - llama3.2";
                    else providerPreset.SelectedIndex = 0;
                }
                else
                {
                    providerPreset.Items.AddRange(new object[] { "DeepSeek - chat", "DeepSeek - reasoner", "自定义 OpenAI-compatible" });
                    if (ProviderIndexFromText(oldText) == 3) providerPreset.Text = "DeepSeek - reasoner";
                    else if (ProviderIndexFromText(oldText) == 4) providerPreset.Text = "自定义 OpenAI-compatible";
                    else providerPreset.SelectedIndex = 0;
                }
            }
            finally
            {
                loadingSettings = wasLoading;
            }
        }

        string ProviderLabelForId(string provider)
        {
            if (provider == "local-ollama-qwen") return "本地 Ollama - Qwen IQ3_M";
            if (provider == "local-ollama-llama32") return "本地 Ollama - llama3.2";
            if (provider == "deepseek-reasoner" || provider == "deepseek-v4-pro") return "DeepSeek - reasoner";
            if (provider == "custom-openai-compatible") return "自定义 OpenAI-compatible";
            return "DeepSeek - chat";
        }
        static void AddRow(TableLayoutPanel panel, string label, Control control)
        {
            int row = panel.Controls.Count / 2;
            panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            panel.Controls.Add(new Label { Text = label, AutoSize = true, Dock = DockStyle.Top, Padding = new Padding(0, 7, 0, 0) }, 0, row);
            control.Dock = DockStyle.Top;
            panel.Controls.Add(control, 1, row);
        }

        void ApplySelectedProviderPreset(bool showMessage)
        {
            if (providerPreset == null || loadingSettings) return;

            var selected = ProviderIndexFromText(providerPreset.Text);
            if (selected < 0)
            {
                var index = providerPreset.SelectedIndex;
                if (IsLocalMode()) selected = index == 1 ? 1 : 0;
                else selected = index == 1 ? 3 : (index == 2 ? 4 : 2);
            }

            var currentKey = apiKey == null ? "" : apiKey.Text;
            var selectedProvider = ProviderId(selected);
            var saved = state.Settings.FindApiPreset(selectedProvider);
            var savedKey = saved == null ? "" : (saved.ApiKey ?? "");
            var keyToKeep = string.IsNullOrWhiteSpace(currentKey) ? savedKey : currentKey;

            loadingSettings = true;
            if (selected == 0)
            {
                protocol.Text = "http";
                host.Text = DetectOllamaHost();
                port.Text = "11434";
                path.Text = "/api/chat";
                model.Text = "hf.co/HauhauCS/Qwen3.6-35B-A3B-Uncensored-HauhauCS-Aggressive:IQ3_M";
                apiKey.Text = "";
                state.LastBubble = "已套用本地 Ollama Qwen IQ3_M。";
            }
            else if (selected == 1)
            {
                protocol.Text = "http";
                host.Text = DetectOllamaHost();
                port.Text = "11434";
                path.Text = "/api/chat";
                model.Text = "llama3.2:latest";
                apiKey.Text = "";
                state.LastBubble = "已套用本地 Ollama llama3.2。";
            }
            else if (selected == 2)
            {
                protocol.Text = "https";
                host.Text = "api.deepseek.com";
                port.Text = "443";
                path.Text = "/chat/completions";
                model.Text = "deepseek-chat";
                apiKey.Text = keyToKeep;
                state.LastBubble = "已套用 DeepSeek chat。";
            }
            else if (selected == 3)
            {
                protocol.Text = "https";
                host.Text = "api.deepseek.com";
                port.Text = "443";
                path.Text = "/chat/completions";
                model.Text = "deepseek-reasoner";
                apiKey.Text = keyToKeep;
                state.LastBubble = "已套用 DeepSeek reasoner。";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(protocol.Text)) protocol.Text = "https";
                if (string.IsNullOrWhiteSpace(path.Text)) path.Text = "/chat/completions";
                state.LastBubble = "已切换到自定义 OpenAI-compatible。";
            }

            loadingSettings = false;
            SaveSettingsFromUi();
            if (showMessage)
            {
                var keyHint = selected == 0 || selected == 1 ? "本地 Ollama 不需要 API Key。" : "DeepSeek 需要在 API Key 输入框填入你自己的密钥。";
                MessageBox.Show(this, "已套用固定预设，已自动修正协议/主机/端口/路径/模型。\n\n" + BuildEndpointPreview() + "\n\n" + keyHint, "AI 入口");
            }
        }        void SaveCurrentApiPresetFor(string provider)
        {
            if (string.IsNullOrWhiteSpace(provider) || protocol == null || host == null) return;
            state.Settings.Provider = provider;
            state.Settings.Protocol = CurrentProtocolText();
            state.Settings.Host = host.Text.Trim();
            state.Settings.Port = port.Text.Trim();
            state.Settings.Path = path.Text.Trim();
            state.Settings.Model = model.Text.Trim();
            state.Settings.ApiKey = apiKey.Text;
            state.Settings.SaveApiPreset(provider);
        }

        static string DetectOllamaHost()
        {
            try
            {
                var listeners = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpListeners();
                string firstPortMatch = null;
                foreach (var endpoint in listeners)
                {
                    if (endpoint.Port != 11434) continue;
                    var address = endpoint.Address.ToString();
                    if (address == "127.0.0.1" || address == "::1") return "127.0.0.1";
                    if (address == "0.0.0.0" || address == "::") return "127.0.0.1";
                    if (firstPortMatch == null) firstPortMatch = address;
                }
                if (!string.IsNullOrEmpty(firstPortMatch)) return firstPortMatch;
            }
            catch
            {
            }
            return "127.0.0.1";
        }
        void LoadSettingsToUi()
        {
            var s = state.Settings;
            loadingSettings = true;
            modePreset.Text = ProviderIndex(s.Provider) <= 1 ? "本地部署模式" : "API模式";
            RefreshProviderPresetsForMode();
            providerPreset.Text = ProviderLabelForId(s.Provider);
            protocol.Text = string.IsNullOrWhiteSpace(s.Protocol) ? "http" : s.Protocol;
            petName.Text = s.PetName;
            host.Text = s.Host;
            port.Text = s.Port;
            path.Text = s.Path;
            model.Text = s.Model;
            apiKey.Text = s.ApiKey;
            persona.Text = s.Persona;
            memoryChat.Text = s.ChatMemory;
            memoryOpinion.Text = s.UserOpinion;
            if (memoryEnabled != null) memoryEnabled.Checked = s.MemoryEnabled;
            temperature.Value = Math.Max(0, Math.Min(15, (int)Math.Round(s.Temperature * 10)));
            spriteSize.Value = Math.Max(40, Math.Min(100, (int)Math.Round(s.SpriteScale * 100)));
            Text = s.PetName + " 控制台";
            loadingSettings = false;
            SetMemoryEditable(false);
        }

        static int ProviderIndex(string provider)
        {
            if (provider == "local-ollama-qwen") return 0;
            if (provider == "local-ollama-llama32") return 1;
            if (provider == "deepseek-chat" || provider == "deepseek-v4-flash") return 2;
            if (provider == "deepseek-reasoner" || provider == "deepseek-v4-pro") return 3;
            if (provider == "custom-openai-compatible") return 4;
            return 0;
        }

        static string ProviderId(int index)
        {
            if (index == 1) return "local-ollama-llama32";
            if (index == 2) return "deepseek-chat";
            if (index == 3) return "deepseek-reasoner";
            if (index == 4) return "custom-openai-compatible";
            return "local-ollama-qwen";
        }

        static int ProviderIndexFromText(string text)
        {
            text = (text ?? "").Trim().ToLowerInvariant();
            if (text.Contains("qwen") || text.Contains("iq3")) return 0;
            if (text.Contains("llama")) return 1;
            if (text.Contains("flash") || text.Contains("chat")) return 2;
            if (text.Contains("pro") || text.Contains("reasoner")) return 3;
            if (text.Contains("custom") || text.Contains("自定义")) return 4;
            return -1;
        }

        string CurrentProviderId()
        {
            var selected = providerPreset == null ? -1 : ProviderIndexFromText(providerPreset.Text);
            if (selected < 0 && providerPreset != null)
            {
                var index = providerPreset.SelectedIndex;
                if (IsLocalMode()) selected = index == 1 ? 1 : 0;
                else selected = index == 1 ? 3 : (index == 2 ? 4 : 2);
            }
            return selected < 0 ? "custom-openai-compatible" : ProviderId(selected);
        }

        string CurrentProtocolText()
        {
            var value = protocol == null ? "" : (protocol.Text ?? "").Trim().ToLowerInvariant();
            return string.IsNullOrWhiteSpace(value) ? "http" : value;
        }

        string BuildEndpointPreview()
        {
            var cleanPath = string.IsNullOrWhiteSpace(path.Text) ? "/chat/completions" : path.Text.Trim();
            if (!cleanPath.StartsWith("/")) cleanPath = "/" + cleanPath;
            return CurrentProtocolText() + "://" + host.Text.Trim() + ":" + port.Text.Trim() + cleanPath + "\nmodel: " + model.Text.Trim();
        }

        void SaveSettingsFromUi()
        {
            if (loadingSettings || providerPreset == null) return;
            var s = state.Settings;
            s.Provider = CurrentProviderId();
            s.PetName = string.IsNullOrWhiteSpace(petName.Text) ? "Cyber Pet" : petName.Text.Trim();
            s.Protocol = CurrentProtocolText();
            s.Host = host.Text.Trim();
            s.Port = port.Text.Trim();
            s.Path = path.Text.Trim();
            s.Model = model.Text.Trim();
            s.ApiKey = apiKey.Text;
            s.Persona = persona.Text;
            s.ChatMemory = memoryChat == null ? s.ChatMemory : memoryChat.Text;
            s.UserOpinion = memoryOpinion == null ? s.UserOpinion : memoryOpinion.Text;
            s.MemoryEnabled = memoryEnabled == null ? s.MemoryEnabled : memoryEnabled.Checked;
            s.Temperature = temperature.Value / 10.0;
            s.SpriteScale = spriteSize == null ? s.SpriteScale : spriteSize.Value / 100.0;
            Text = s.PetName + " 控制台";
            s.Save();
            RefreshPetForms();
        }

        void AutoSaveSettings()
        {
            if (loadingSettings || providerPreset == null) return;
            autoSaveTimer.Stop();
            autoSaveTimer.Start();
        }

        void SaveMemoryFromUi()
        {
            if (memoryChat == null || memoryOpinion == null) return;
            state.Settings.ChatMemory = memoryChat.Text;
            state.Settings.UserOpinion = memoryOpinion.Text;
            state.Settings.Save();
        }

        void SetMemoryEditable(bool editable)
        {
            if (memoryChat != null) memoryChat.ReadOnly = false;
            if (memoryOpinion != null) memoryOpinion.ReadOnly = false;
        }

        void AttachAutoSaveHandlers()
        {
            Control[] boxes = new Control[] { petName, host, port, path, model, apiKey, persona };
            foreach (var box in boxes)
            {
                if (box != null) box.TextChanged += delegate { AutoSaveSettings(); };
            }
            protocol.SelectedIndexChanged += delegate { AutoSaveSettings(); };
            protocol.TextChanged += delegate { AutoSaveSettings(); };
            providerPreset.TextChanged += delegate { AutoSaveSettings(); };
            modePreset.TextChanged += delegate { AutoSaveSettings(); };
            temperature.ValueChanged += delegate { AutoSaveSettings(); };
            spriteSize.ValueChanged += delegate { AutoSaveSettings(); };
            if (memoryEnabled != null) memoryEnabled.CheckedChanged += delegate { AutoSaveSettings(); };
        }

        void ToggleSpeechInput()
        {
            try
            {
                if (recognizer == null)
                {
                    recognizer = CreateRecognizer();
                    recognizer.SpeechRecognized += delegate(object sender, SpeechRecognizedEventArgs e)
                    {
                        if (e.Result == null || e.Result.Confidence < 0.35) return;
                        var text = e.Result.Text.Trim();
                        if (text.Length == 0) return;
                        if (input.TextLength > 0 && !input.Text.EndsWith(" ")) input.AppendText(" ");
                        input.AppendText(text);
                    };
                    recognizer.RecognizeCompleted += delegate
                    {
                        listening = false;
                        if (micButton != null) micButton.Text = "语音输入";
                    };
                }

                if (listening)
                {
                    recognizer.RecognizeAsyncStop();
                    listening = false;
                    micButton.Text = "语音输入";
                }
                else
                {
                    recognizer.SetInputToDefaultAudioDevice();
                    recognizer.RecognizeAsync(RecognizeMode.Multiple);
                    listening = true;
                    micButton.Text = "停止听写";
                }
            }
            catch (Exception ex)
            {
                listening = false;
                if (micButton != null) micButton.Text = "语音输入";
                MessageBox.Show(this, "无法启动语音识别：" + ex.Message, "语音输入");
            }
        }

        SpeechRecognitionEngine CreateRecognizer()
        {
            var recognizers = SpeechRecognitionEngine.InstalledRecognizers();
            if (recognizers.Count == 0) throw new InvalidOperationException("系统没有安装 Windows 语音识别器。");

            RecognizerInfo selected = null;
            foreach (var info in recognizers)
            {
                if (info.Culture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase))
                {
                    selected = info;
                    break;
                }
            }
            if (selected == null) selected = recognizers[0];

            var engine = new SpeechRecognitionEngine(selected);
            engine.LoadGrammar(new DictationGrammar());
            return engine;
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            autoSaveTimer.Stop();
            SaveSettingsFromUi();
            base.OnFormClosing(e);
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (recognizer != null)
            {
                try { recognizer.RecognizeAsyncCancel(); } catch { }
                recognizer.Dispose();
                recognizer = null;
            }
            autoSaveTimer.Dispose();
            base.OnFormClosed(e);
        }

        async Task TestConnection()
        {
            SaveSettingsFromUi();
            sendButton.Enabled = false;
            try
            {
                var messages = new List<ChatMessage>
                {
                    new ChatMessage("system", "你只用一句中文纯文本回复连接成功，不要使用 Markdown。"),
                    new ChatMessage("user", "ping")
                };
                var reply = await Task.Run(() => ApiClient.Send(state.Settings, messages));
                state.LastBubble = Short(reply);
                MessageBox.Show(this, reply, "连接成功");
            }
            catch (Exception ex)
            {
                state.LastBubble = "API 连接失败";
                MessageBox.Show(this, ex.Message, "连接失败");
            }
            finally
            {
                sendButton.Enabled = true;
            }
        }

        async Task SendMessage()
        {
            var text = input.Text.Trim();
            if (text.Length == 0) return;
            SaveSettingsFromUi();
            var originalChatMemory = state.Settings.ChatMemory ?? "";
            var originalUserOpinion = state.Settings.UserOpinion ?? "";
            var memoryEnabledForTurn = state.Settings.MemoryEnabled;
            cancelRequested = false;
            lastExpressionCue = "";
            autoExpressionUsed = false;
            lastAutoExpressionAt = 0;
            expressionSuppressed = false;
            activeRequest = null;
            sendButton.Enabled = false;
            if (stopButton != null) stopButton.Enabled = true;
            input.Clear();
            state.Messages.Add(new ChatMessage("user", text));
            RenderMessages();
            SetPetThinking(false);

            ChatMessage assistantMessage = null;
            activeAssistantMessage = null;
            var replyBuilder = new StringBuilder();

            try
            {
                var userContent = BuildUserContent(text);
                var apiMessages = new List<ChatMessage>();
                apiMessages.Add(new ChatMessage("system", state.Settings.Persona + PlainTextInstruction()));
                apiMessages.Add(new ChatMessage("system", ExpressionInstruction()));
                if (memoryEnabledForTurn) apiMessages.Add(new ChatMessage("system", BuildMemoryPrompt(originalChatMemory, originalUserOpinion)));
                for (int i = 0; i < state.Messages.Count - 1; i++)
                {
                    apiMessages.Add(state.Messages[i]);
                }
                apiMessages.Add(new ChatMessage("user", userContent));

                assistantMessage = new ChatMessage("assistant", "");
                activeAssistantMessage = assistantMessage;
                state.Messages.Add(assistantMessage);
                RenderMessages();

                await Task.Run(delegate
                {
                    ApiClient.SendStreaming(state.Settings, apiMessages, delegate(string delta)
                    {
                        if (string.IsNullOrEmpty(delta) || cancelRequested) return;
                        foreach (char ch in delta)
                        {
                            if (cancelRequested) return;
                            if (IsDisposed || !IsHandleCreated) return;
                            Invoke(new Action(delegate
                            {
                                if (cancelRequested) return;
                                replyBuilder.Append(ch);
                                var cleaned = CleanPlainText(ApplyExpressionCues(replyBuilder.ToString()));
                                assistantMessage.content = cleaned;
                                state.LastBubble = FormatPetSpeech(cleaned);
                                RenderMessages();
                                PulsePetSpeaking();
                            }));
                            System.Threading.Thread.Sleep(18);
                        }
                    }, delegate { return cancelRequested; }, delegate(HttpWebRequest request) { activeRequest = request; });
                });

                if (!cancelRequested)
                {
                    var finalReply = Convert.ToString(assistantMessage.content ?? "");
                    SetPetThinking(false);
                    if (memoryEnabledForTurn)
                    {
                        expressionSuppressed = true;
                        SetPetExpression("");
                        try
                        {
                            await Task.Run(delegate { UpdateMemoryAfterConversation(text, finalReply, originalChatMemory, originalUserOpinion); });
                        }
                        finally
                        {
                            expressionSuppressed = false;
                        }
                    }
                    files.Clear();
                    RenderFiles();
                }
            }
            catch (WebException ex)
            {
                if (cancelRequested)
                {
                    MarkInterrupted(assistantMessage);
                }
                else
                {
                    var msg = "连接失败：" + ex.Message;
                    if (assistantMessage == null) state.Messages.Add(new ChatMessage("assistant", msg));
                    else assistantMessage.content = msg;
                    state.LastBubble = msg;
                }
            }
            catch (Exception ex)
            {
                var msg = cancelRequested ? "发言已打断。" : "连接失败：" + ex.Message;
                if (assistantMessage == null) state.Messages.Add(new ChatMessage("assistant", msg));
                else assistantMessage.content = msg;
                state.LastBubble = msg;
                if (cancelRequested) SetMemoryEditable(true);
            }
            finally
            {
                activeRequest = null;
                activeAssistantMessage = null;
                RenderMessages();
                SetPetThinking(false);
                sendButton.Enabled = true;
                if (stopButton != null) stopButton.Enabled = false;
            }
        }

        void StopGeneration()
        {
            cancelRequested = true;
            try
            {
                if (activeRequest != null) activeRequest.Abort();
            }
            catch
            {
            }
            MarkInterrupted(activeAssistantMessage);
            SetMemoryEditable(true);
            SetPetThinking(false);
            if (stopButton != null) stopButton.Enabled = false;
            if (sendButton != null) sendButton.Enabled = true;
            state.LastBubble = "发言已打断，记忆已可编辑。";
            RenderMessages();
        }

        void MarkInterrupted(ChatMessage assistantMessage)
        {
            var msg = "发言已打断。";
            if (assistantMessage == null)
            {
                state.Messages.Add(new ChatMessage("assistant", msg));
            }
            else
            {
                var current = Convert.ToString(assistantMessage.content ?? "").Trim();
                if (current.Contains(msg)) return;
                assistantMessage.content = current.Length == 0 ? msg : current + "\r\n" + msg;
            }
            state.LastBubble = msg;
            SetMemoryEditable(true);
        }

        string BuildMemoryPrompt(string originalChatMemory, string originalUserOpinion)
        {
            var builder = new StringBuilder();
            builder.AppendLine("外部记忆模块。下面是你在本轮对话前必须参考的持久聊天记忆。只把它当作背景，不要逐字复述，除非用户询问。");
            builder.AppendLine("情感与情绪模块已暂时停用：本轮回复不要参考长期情感、当前情绪或情感占比。");
            builder.AppendLine("聊天内容记忆:");
            builder.AppendLine(string.IsNullOrWhiteSpace(originalChatMemory) ? "暂无。" : originalChatMemory);
            return builder.ToString();
        }
        void UpdateMemoryAfterConversation(string userText, string assistantText, string originalChatMemory, string originalUserOpinion)
        {
            if (string.IsNullOrWhiteSpace(userText) && string.IsNullOrWhiteSpace(assistantText)) return;

            try
            {
                var messages = new List<ChatMessage>();
                messages.Add(new ChatMessage("system", "你是桌面AI宠物的外部记忆管理器。情感与情绪模块已暂时停用，你只能更新聊天内容记忆，绝对不要输出或更新情感、情绪、情感占比。只输出纯文本，严格使用段落标题：聊天内容记忆:。不要使用 Markdown。重要规则：不要无限追加；必须压缩旧记忆，删除过期、重复、琐碎或低价值细节，合并同类信息。聊天内容记忆最多约1200个中文字符，只保留事实、偏好、正在进行的任务和重要上下文。"));
                var prompt = "原聊天内容记忆:\n" + originalChatMemory + "\n\n本轮用户:\n" + userText + "\n\n本轮AI:\n" + assistantText + "\n\n请基于原聊天内容记忆和本轮新对话，输出压缩后的聊天内容记忆。不要输出情感或情绪。";
                messages.Add(new ChatMessage("user", prompt));
                var updated = CleanPlainText(ApiClient.Send(state.Settings, messages));
                var chat = ExtractSection(updated, "聊天内容记忆:", null);
                if (string.IsNullOrWhiteSpace(chat)) chat = updated;
                if (string.IsNullOrWhiteSpace(chat)) chat = BuildChatFallback(userText, assistantText, originalChatMemory);
                chat = CompactMemoryText(chat.Trim(), 1200);
                if (SameText(chat, originalChatMemory)) chat = BuildChatFallback(userText, assistantText, originalChatMemory);
                SetMemoryTextSafe(chat, originalUserOpinion);
            }
            catch
            {
                SetMemoryTextSafe(BuildChatFallback(userText, assistantText, originalChatMemory), originalUserOpinion);
            }
        }
        void ApplyMemoryText(string text, string userText, string assistantText, string originalChatMemory, string originalUserOpinion)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                AppendMemoryFallback(userText, assistantText, originalChatMemory, originalUserOpinion);
                return;
            }

            var chat = ExtractSection(text, "聊天内容记忆:", "情感与情绪占比:");
            if (string.IsNullOrWhiteSpace(chat)) chat = ExtractSection(text, "聊天内容记忆:", "情感占比:");
            var opinion = ExtractSection(text, "情感与情绪占比:", null);
            if (string.IsNullOrWhiteSpace(opinion)) opinion = ExtractSection(text, "情感占比:", null);
            if (string.IsNullOrWhiteSpace(opinion)) opinion = ExtractSection(text, "对用户的看法:", null);

            if (string.IsNullOrWhiteSpace(chat)) chat = BuildChatFallback(userText, assistantText, originalChatMemory);
            if (string.IsNullOrWhiteSpace(opinion)) opinion = BuildOpinionFallback(userText, assistantText, originalUserOpinion);

            chat = CompactMemoryText(chat.Trim(), 1200);
            opinion = NormalizeAffectState(opinion.Trim(), userText, originalUserOpinion);

            if (SameText(chat, originalChatMemory)) chat = BuildChatFallback(userText, assistantText, originalChatMemory);
            if (SameText(opinion, originalUserOpinion)) opinion = BuildOpinionFallback(userText, assistantText, originalUserOpinion);

            SetMemoryTextSafe(chat, opinion);
        }
        static bool SameText(string left, string right)
        {
            return string.Equals((left ?? "").Trim(), (right ?? "").Trim(), StringComparison.Ordinal);
        }

        static string ExtractSection(string text, string start, string next)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("：", ":");
            start = start.Replace("：", ":");
            if (next != null) next = next.Replace("：", ":");
            var startIndex = text.IndexOf(start, StringComparison.OrdinalIgnoreCase);
            if (startIndex < 0) return "";
            startIndex += start.Length;
            var endIndex = next == null ? -1 : text.IndexOf(next, startIndex, StringComparison.OrdinalIgnoreCase);
            if (endIndex < 0) endIndex = text.Length;
            return text.Substring(startIndex, endIndex - startIndex).Trim();
        }

        void AppendMemoryFallback(string userText, string assistantText, string originalChatMemory, string originalUserOpinion)
        {
            SetMemoryTextSafe(BuildChatFallback(userText, assistantText, originalChatMemory), BuildOpinionFallback(userText, assistantText, originalUserOpinion));
        }

        string BuildChatFallback(string userText, string assistantText, string originalChatMemory)
        {
            var line = DateTime.Now.ToString("yyyy-MM-dd HH:mm") + " 本轮对话: 用户说 " + Short(userText) + "；AI回应 " + Short(assistantText);
            var merged = string.IsNullOrWhiteSpace(originalChatMemory) ? line : originalChatMemory.TrimEnd() + "\r\n" + line;
            return CompactMemoryText(KeepRecentLines(merged, 8), 1200);
        }

        string BuildOpinionFallback(string userText, string assistantText, string originalUserOpinion)
        {
            return BuildAffectFallback(userText, originalUserOpinion);
        }

        string BuildAffectFallback(string userText, string originalAffectMemory)
        {
            var longTerm = ExtractAffectSubsection(originalAffectMemory, "长期情感:", "当前情绪:");
            if (string.IsNullOrWhiteSpace(longTerm) && !string.IsNullOrWhiteSpace(originalAffectMemory) && originalAffectMemory.Contains("%"))
            {
                longTerm = NormalizeRatioLines(originalAffectMemory);
            }
            if (string.IsNullOrWhiteSpace(longTerm))
            {
                longTerm = "喜好 35%\r\n信任 30%\r\n亲近 20%\r\n警惕 15%";
            }

            var lower = (userText ?? "").ToLowerInvariant();
            string currentMood;
            if (ContainsAny(lower, new string[] { "急", "烦", "崩", "失败", "bug", "无法", "不能", "错误", "焦虑", "担心" }))
            {
                currentMood = "烦躁 40%\r\n焦虑 35%\r\n专注 25%";
            }
            else if (ContainsAny(lower, new string[] { "讨厌", "厌烦", "烦死", "别", "滚", "生气", "火大" }))
            {
                currentMood = "厌烦 45%\r\n不满 35%\r\n警惕 20%";
            }
            else if (ContainsAny(lower, new string[] { "开心", "喜欢", "不错", "成功", "好了", "棒", "满意" }))
            {
                currentMood = "开心 45%\r\n满意 35%\r\n期待 20%";
            }
            else if (ContainsAny(lower, new string[] { "为什么", "如何", "能否", "想", "希望", "调整", "修改", "加入" }))
            {
                currentMood = "专注 45%\r\n好奇 35%\r\n期待 20%";
            }
            else
            {
                currentMood = "平静 45%\r\n专注 35%\r\n期待 20%";
            }

            return CompactMemoryText("长期情感:\r\n" + CompactMemoryText(longTerm, 320) + "\r\n当前情绪:\r\n" + currentMood, 700);
        }

        static bool ContainsAny(string text, string[] needles)
        {
            foreach (var needle in needles)
            {
                if (text.Contains(needle)) return true;
            }
            return false;
        }

        string NormalizeAffectState(string text, string userText, string originalAffectMemory)
        {
            if (string.IsNullOrWhiteSpace(text)) return BuildAffectFallback(userText, originalAffectMemory);

            var longTerm = ExtractAffectSubsection(text, "长期情感:", "当前情绪:");
            var currentMood = ExtractAffectSubsection(text, "当前情绪:", null);

            if (string.IsNullOrWhiteSpace(longTerm) && !string.IsNullOrWhiteSpace(text) && text.Contains("%"))
            {
                longTerm = text;
            }
            longTerm = NormalizeRatioLines(longTerm);
            currentMood = NormalizeRatioLines(currentMood);

            if (string.IsNullOrWhiteSpace(longTerm))
            {
                var originalLongTerm = ExtractAffectSubsection(originalAffectMemory, "长期情感:", "当前情绪:");
                if (string.IsNullOrWhiteSpace(originalLongTerm) && !string.IsNullOrWhiteSpace(originalAffectMemory) && originalAffectMemory.Contains("%"))
                {
                    originalLongTerm = originalAffectMemory;
                }
                longTerm = NormalizeRatioLines(originalLongTerm);
            }

            if (string.IsNullOrWhiteSpace(currentMood))
            {
                currentMood = ExtractAffectSubsection(BuildAffectFallback(userText, originalAffectMemory), "当前情绪:", null);
            }

            if (string.IsNullOrWhiteSpace(longTerm) || string.IsNullOrWhiteSpace(currentMood))
            {
                return BuildAffectFallback(userText, originalAffectMemory);
            }

            return CompactMemoryText("长期情感:\r\n" + longTerm + "\r\n当前情绪:\r\n" + currentMood, 700);
        }

        static string ExtractAffectSubsection(string text, string start, string next)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var normalized = text.Replace("：", ":");
            var startIndex = normalized.IndexOf(start, StringComparison.OrdinalIgnoreCase);
            if (startIndex < 0) return "";
            startIndex += start.Length;
            var endIndex = next == null ? -1 : normalized.IndexOf(next, startIndex, StringComparison.OrdinalIgnoreCase);
            if (endIndex < 0) endIndex = normalized.Length;
            return normalized.Substring(startIndex, endIndex - startIndex).Trim();
        }

        static string NormalizeRatioLines(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("：", " ").Replace(":", " ");
            var lines = text.Replace("\r\n", "\n").Split(new[] { '\n', ';', '；', ',', '，' }, StringSplitOptions.RemoveEmptyEntries);
            var builder = new StringBuilder();
            foreach (var raw in lines)
            {
                var line = raw.Trim();
                if (line.Length == 0) continue;
                var match = Regex.Match(line, @"([\u4e00-\u9fa5A-Za-z]+)\s*([0-9]{1,3})\s*%?");
                if (!match.Success) continue;
                var emotion = match.Groups[1].Value.Trim();
                var pct = Math.Max(0, Math.Min(100, int.Parse(match.Groups[2].Value)));
                if (builder.Length > 0) builder.AppendLine();
                builder.Append(emotion).Append(" ").Append(pct).Append("%");
            }
            var normalized = builder.ToString().Trim();
            return string.IsNullOrWhiteSpace(normalized) ? "" : CompactMemoryText(normalized, 320);
        }
        static string KeepRecentLines(string text, int maxLines)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            var lines = text.Replace("\r\n", "\n").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var start = Math.Max(0, lines.Length - maxLines);
            var builder = new StringBuilder();
            for (int i = start; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0) continue;
                if (builder.Length > 0) builder.AppendLine();
                builder.Append(line);
            }
            return builder.ToString();
        }

        static string CompactMemoryText(string text, int maxChars)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = Regex.Replace(text.Trim(), @"\r?\n{3,}", "\r\n\r\n");
            if (text.Length <= maxChars) return text;
            var lines = text.Replace("\r\n", "\n").Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var builder = new StringBuilder();
            for (int i = lines.Length - 1; i >= 0; i--)
            {
                var line = lines[i].Trim();
                if (line.Length == 0) continue;
                if (builder.Length + line.Length + 2 > maxChars) break;
                builder.Insert(0, line + "\r\n");
            }
            var compacted = builder.ToString().Trim();
            if (compacted.Length == 0)
            {
                compacted = text.Substring(Math.Max(0, text.Length - maxChars), Math.Min(maxChars, text.Length)).Trim();
            }
            return compacted.Length > maxChars ? compacted.Substring(compacted.Length - maxChars).Trim() : compacted;
        }
        void SetMemoryTextSafe(string chat, string opinion)
        {
            if (IsDisposed || !IsHandleCreated) return;
            Invoke(new Action(delegate
            {
                loadingSettings = true;
                memoryChat.Text = CompactMemoryText(chat ?? "", 1200);
                memoryOpinion.Text = CompactMemoryText(opinion ?? "", 700);
                state.Settings.ChatMemory = memoryChat.Text;
                state.Settings.UserOpinion = memoryOpinion.Text;
                state.Settings.Save();
                loadingSettings = false;
            }));
        }

        object BuildUserContent(string text)
        {
            if (files.Count == 0) return text;
            var parts = new List<Dictionary<string, object>>();
            var textPart = new Dictionary<string, object>();
            textPart["type"] = "text";
            var builder = new StringBuilder(text);

            foreach (var file in files)
            {
                var info = new FileInfo(file);
                var ext = info.Extension.ToLowerInvariant();
                if (IsImage(ext))
                {
                    parts.Add(new Dictionary<string, object>
                    {
                        { "type", "image_url" },
                        { "image_url", new Dictionary<string, object> { { "url", DataUrl(file) } } }
                    });
                }
                else if (IsText(ext))
                {
                    var content = File.ReadAllText(file, Encoding.UTF8);
                    if (content.Length > 16000) content = content.Substring(0, 16000) + "\n[文件过长，已截断]";
                    builder.AppendLine().AppendLine().Append("[文件: ").Append(info.Name).AppendLine("]").AppendLine(content);
                }
                else
                {
                    builder.AppendLine().AppendLine()
                        .Append("[用户上传了文件: ").Append(info.Name)
                        .Append(", 类型: ").Append(ext)
                        .Append(", 大小: ").Append(info.Length)
                        .Append(" bytes。当前 exe 版会直接读取图片和文本；PDF、Word、视频和音频需要后端解析能力。]");
                }
            }

            textPart["text"] = builder.ToString();
            parts.Insert(0, textPart);
            return parts.Count == 1 ? (object)builder.ToString() : parts;
        }

        static bool IsImage(string ext)
        {
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".webp" || ext == ".bmp";
        }

        static bool IsText(string ext)
        {
            return ext == ".txt" || ext == ".md" || ext == ".json" || ext == ".csv" || ext == ".log" || ext == ".xml" ||
                   ext == ".html" || ext == ".css" || ext == ".js" || ext == ".ts" || ext == ".py" || ext == ".cs";
        }

        static string DataUrl(string file)
        {
            var ext = System.IO.Path.GetExtension(file).ToLowerInvariant().TrimStart('.');
            if (ext == "jpg") ext = "jpeg";
            var bytes = File.ReadAllBytes(file);
            return "data:image/" + ext + ";base64," + Convert.ToBase64String(bytes);
        }

        void AddFiles()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Multiselect = true;
                dialog.Filter = "所有支持文件|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp;*.txt;*.md;*.json;*.csv;*.log;*.xml;*.html;*.css;*.js;*.ts;*.py;*.cs;*.pdf;*.doc;*.docx;*.mp4;*.mov;*.mp3;*.wav|所有文件|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                files.AddRange(dialog.FileNames);
                RenderFiles();
            }
        }

        void RenderFiles()
        {
            fileList.Items.Clear();
            foreach (var file in files)
            {
                var info = new FileInfo(file);
                fileList.Items.Add(info.Name + " (" + Math.Round(info.Length / 1024.0, 1) + " KB)");
            }
        }

        string ApplyExpressionCues(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var pattern = @"(?:(?:\[\[\s*|\[\s*|\{\{\s*|\{\s*)(?:emotion|表情)\s*[:：]\s*([A-Za-z0-9_\-\u4e00-\u9fff]+)\s*(?:\]\]|\]|\}\}|\}))|(?:\{\s*([A-Za-z0-9_\-\u4e00-\u9fff]+)\s*\})";
            if (expressionSuppressed)
            {
                return Regex.Replace(text, pattern, "", RegexOptions.IgnoreCase).TrimStart();
            }
            var matches = Regex.Matches(text, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                var emotion = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                emotion = NormalizeEmotionName(emotion);
                if (emotion.Length == 0 || emotion == lastExpressionCue) continue;
                lastExpressionCue = emotion;
                SetPetExpression(emotion);
            }

            var cleaned = Regex.Replace(text, pattern, "", RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, @"(?:\[\[?\s*|\{\{?\s*)(?:emotion|表情)?\s*[:：]?\s*[A-Za-z0-9_\-\u4e00-\u9fff]*$", "", RegexOptions.IgnoreCase);
            MaybeApplyAutomaticExpression(cleaned);
            return cleaned.TrimStart();
        }

        static string NormalizeEmotionName(string emotion)
        {
            emotion = (emotion ?? "").Trim().ToLowerInvariant();
            if (emotion == "angry" || emotion == "生气" || emotion == "愤怒" || emotion == "恼火") return "angry";
            if (emotion == "crying" || emotion == "哭" || emotion == "哭泣" || emotion == "难过" || emotion == "伤心" || emotion == "委屈") return "crying";
            if (emotion == "focused" || emotion == "专注" || emotion == "认真" || emotion == "思考" || emotion == "聚焦") return "focused";
            if (emotion == "panic" || emotion == "慌张" || emotion == "惊慌" || emotion == "恐慌" || emotion == "紧张") return "panic";
            if (emotion == "shocked" || emotion == "震惊" || emotion == "惊讶" || emotion == "吃惊") return "shocked";
            if (emotion == "shy" || emotion == "害羞" || emotion == "羞" || emotion == "脸红") return "shy";
            if (emotion == "speechless" || emotion == "无语" || emotion == "沉默" || emotion == "语塞") return "speechless";
            return "";
        }

        void MaybeApplyAutomaticExpression(string text)
        {
            if (expressionSuppressed) return;
            if (string.IsNullOrWhiteSpace(text)) return;
            var trimmed = text.Trim();
            if (trimmed.Length < 4) return;
            if (autoExpressionUsed && trimmed.Length - lastAutoExpressionAt < 36) return;
            var recent = trimmed.Length > 90 ? trimmed.Substring(trimmed.Length - 90) : trimmed;
            var emotion = InferExpressionFromText(recent);
            if (string.IsNullOrWhiteSpace(emotion) && !autoExpressionUsed && trimmed.Length >= 14) emotion = InferExpressionFromText(trimmed);
            if (string.IsNullOrWhiteSpace(emotion) && !autoExpressionUsed && trimmed.Length >= 24) emotion = "focused";
            if (string.IsNullOrWhiteSpace(emotion) || emotion == lastExpressionCue) return;
            autoExpressionUsed = true;
            lastAutoExpressionAt = trimmed.Length;
            lastExpressionCue = emotion;
            SetPetExpression(emotion);
        }

        static string InferExpressionFromText(string text)
        {
            text = (text ?? "").ToLowerInvariant();
            if (ContainsExpressionKeyword(text, "生气", "愤怒", "恼火", "气死", "讨厌", "烦死", "不爽", "可恶", "angry", "hate", "annoyed")) return "angry";
            if (ContainsExpressionKeyword(text, "哭", "难过", "伤心", "委屈", "心疼", "抱歉", "对不起", "遗憾", "cry", "sad", "sorry")) return "crying";
            if (ContainsExpressionKeyword(text, "慌", "糟糕", "救命", "紧张", "恐慌", "完了", "出错", "失败", "panic", "danger", "bad request")) return "panic";
            if (ContainsExpressionKeyword(text, "震惊", "惊讶", "竟然", "不会吧", "什么", "居然", "原来", "哇", "？！", "!?", "shocked", "surprise")) return "shocked";
            if (ContainsExpressionKeyword(text, "害羞", "脸红", "不好意思", "羞", "可爱", "喜欢", "谢谢", "shy", "cute")) return "shy";
            if (ContainsExpressionKeyword(text, "无语", "沉默", "语塞", "呃", "额", "speechless", "awkward")) return "speechless";
            if (ContainsExpressionKeyword(text, "分析", "检查", "排查", "修复", "代码", "认真", "专注", "确认", "定位", "优化", "测试", "编译", "focused", "debug", "fix")) return "focused";
            return "";
        }

        static bool ContainsExpressionKeyword(string text, params string[] needles)
        {
            foreach (var needle in needles)
            {
                if (!string.IsNullOrEmpty(needle) && text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
        }

        void SetPetExpression(string emotion)
        {
            if (expressionSuppressed && !string.IsNullOrWhiteSpace(emotion)) return;
            foreach (Form form in Application.OpenForms)
            {
                var pet = form as PetForm;
                if (pet != null) pet.SetExpression(emotion);
            }
        }
        string PetDisplayName()
        {
            return string.IsNullOrWhiteSpace(state.Settings.PetName) ? "Cyber Pet" : state.Settings.PetName.Trim();
        }

        string FormatPetSpeech(string text)
        {
            var shortText = Short(text);
            return shortText.Length == 0 ? PetDisplayName() + "：" : PetDisplayName() + "：" + shortText;
        }

        void PulsePetSpeaking()
        {
            foreach (Form form in Application.OpenForms)
            {
                var pet = form as PetForm;
                if (pet != null) pet.PulseSpeaking();
            }
        }
        void RenderMessages()
        {
            var builder = new StringBuilder();
            foreach (var message in state.Messages)
            {
                builder.Append(message.role == "user" ? "你：" : PetDisplayName() + "：");
                builder.AppendLine(Convert.ToString(message.content));
                builder.AppendLine();
            }
            transcript.Text = builder.ToString();
            transcript.SelectionStart = transcript.TextLength;
            transcript.ScrollToCaret();
        }

        void SetPetThinking(bool value)
        {
            foreach (Form form in Application.OpenForms)
            {
                var pet = form as PetForm;
                if (pet != null) pet.SetThinking(value);
            }
        }

        void RefreshPetForms()
        {
            foreach (Form form in Application.OpenForms)
            {
                var pet = form as PetForm;
                if (pet != null) pet.RefreshFromSettings();
            }
        }
        static string ExpressionInstruction()
        {
            return "表情管理：你应当在回复情绪明确、语气变化、道歉、惊讶、专注排查、害羞感谢或无语时输出一个隐藏控制标记切换桌面贴图。可用表情：angry、crying、focused、panic、shocked、shy、speechless。每次回复优先输出 1 个，格式推荐 [[emotion:focused]]；程序也能识别 {emotion:focused}、{focused}、{表情:害羞} 或 {害羞}。这个标记会被程序隐藏，不是给用户看的；不要解释它。非常平淡的短回复可以不输出。";
        }
        static string PlainTextInstruction()
        {
            return "\n\n输出格式要求：只输出纯文本。不要使用 Markdown。不要使用标题井号、项目符号、编号列表、表格、代码块、反引号、粗体或斜体标记。";
        }

        static string CleanPlainText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            text = text.Replace("```", "").Replace("`", "");
            text = Regex.Replace(text, @"\[(.*?)\]\((.*?)\)", "$1");
            text = Regex.Replace(text, @"^\s{0,3}#{1,6}\s*", "", RegexOptions.Multiline);
            text = Regex.Replace(text, @"\*\*(.*?)\*\*", "$1");
            text = Regex.Replace(text, @"__(.*?)__", "$1");
            text = Regex.Replace(text, @"^\s*[-*+]\s+", "", RegexOptions.Multiline);
            text = Regex.Replace(text, @"^\s*\d+\.\s+", "", RegexOptions.Multiline);
            text = text.Replace("|", " ");
            return text;
        }
        static string Short(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return "";
            text = text.Replace("\r", " ").Replace("\n", " ").Trim();
            return text.Length > 58 ? text.Substring(0, 58) + "..." : text;
        }
    }

    static class ApiClient
    {
        sealed class TimeoutWebClient : WebClient
        {
            readonly int timeoutMs;
            public TimeoutWebClient(int timeoutMs) { this.timeoutMs = timeoutMs; }
            protected override WebRequest GetWebRequest(Uri address)
            {
                var request = base.GetWebRequest(address);
                if (request != null)
                {
                    request.Timeout = timeoutMs;
                    var http = request as HttpWebRequest;
                    if (http != null) http.ReadWriteTimeout = timeoutMs;
                }
                return request;
            }
        }

        static Dictionary<string, object> BuildPayload(Settings settings, List<ChatMessage> messages, bool stream)
        {
            var payload = new Dictionary<string, object>();
            payload["model"] = settings.Model;
            payload["messages"] = IsOllamaNativeChat(settings) ? BuildOllamaMessages(messages) : (object)messages;
            payload["stream"] = stream;
            ApplyProviderPayloadExtras(payload, settings);
            return payload;
        }

        static object[] BuildOllamaMessages(List<ChatMessage> messages)
        {
            var result = new List<object>();
            var systemBuilder = new StringBuilder();
            foreach (var message in messages)
            {
                var role = message == null || string.IsNullOrWhiteSpace(message.role) ? "user" : message.role;
                var content = FlattenContent(message == null ? null : message.content);
                if (string.IsNullOrWhiteSpace(content)) continue;
                if (role.Equals("system", StringComparison.OrdinalIgnoreCase))
                {
                    if (systemBuilder.Length > 0) systemBuilder.AppendLine().AppendLine();
                    systemBuilder.Append(content.Trim());
                    continue;
                }
                result.Add(new Dictionary<string, object>
                {
                    { "role", role },
                    { "content", content }
                });
            }
            if (systemBuilder.Length > 0)
            {
                result.Insert(0, new Dictionary<string, object>
                {
                    { "role", "system" },
                    { "content", systemBuilder.ToString() }
                });
            }
            return result.ToArray();
        }

        static string FlattenContent(object content)
        {
            if (content == null) return "";
            var text = content as string;
            if (text != null) return text;
            var array = content as object[];
            if (array != null)
            {
                var builder = new StringBuilder();
                foreach (var item in array)
                {
                    var dict = item as Dictionary<string, object>;
                    if (dict != null && dict.ContainsKey("text")) builder.AppendLine(Convert.ToString(dict["text"]));
                    else if (item != null) builder.AppendLine(Convert.ToString(item));
                }
                return builder.ToString().Trim();
            }
            return Convert.ToString(content);
        }

        public static string Send(Settings settings, List<ChatMessage> messages)
        {
            var payload = BuildPayload(settings, messages, false);

            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var json = serializer.Serialize(payload);

            using (var client = new TimeoutWebClient(120000))
            {
                client.Encoding = Encoding.UTF8;
                client.Headers[HttpRequestHeader.ContentType] = "application/json";
                if (!string.IsNullOrWhiteSpace(settings.ApiKey))
                {
                    client.Headers[HttpRequestHeader.Authorization] = "Bearer " + settings.ApiKey.Trim();
                }
                var response = client.UploadString(settings.Endpoint(), "POST", json);
                return ParseReply(serializer, response);
            }
        }

        public static void SendStreaming(Settings settings, List<ChatMessage> messages, Action<string> onDelta, Func<bool> shouldCancel, Action<HttpWebRequest> onRequestCreated)
        {
            var payload = BuildPayload(settings, messages, true);

            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };
            var json = serializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);

            var request = (HttpWebRequest)WebRequest.Create(settings.Endpoint());
            if (onRequestCreated != null) onRequestCreated(request);
            request.Method = "POST";
            request.ContentType = "application/json";
            request.Accept = "text/event-stream, application/json";
            request.ContentLength = bytes.Length;
            request.Timeout = 120000;
            request.ReadWriteTimeout = 120000;
            if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                request.Headers[HttpRequestHeader.Authorization] = "Bearer " + settings.ApiKey.Trim();
            }

            if (shouldCancel != null && shouldCancel()) return;
            using (var requestStream = request.GetRequestStream())
            {
                requestStream.Write(bytes, 0, bytes.Length);
            }

            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var stream = response.GetResponseStream())
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (shouldCancel != null && shouldCancel()) break;
                        if (line.Length == 0) continue;
                        if (line.StartsWith("data:")) line = line.Substring(5).Trim();
                        if (line == "[DONE]") break;

                        var delta = ParseStreamingDelta(serializer, line);
                        if (!string.IsNullOrEmpty(delta) && (shouldCancel == null || !shouldCancel())) onDelta(delta);
                    }
                }
            }
            catch (WebException)
            {
                if (!IsOllamaNativeChat(settings) || (shouldCancel != null && shouldCancel())) throw;
                var reply = Send(settings, messages);
                if (!string.IsNullOrEmpty(reply) && (shouldCancel == null || !shouldCancel())) onDelta(reply);
            }
        }

        static void ApplyProviderPayloadExtras(Dictionary<string, object> payload, Settings settings)
        {
            if (IsOllamaNativeChat(settings))
            {
                payload["think"] = false;
                payload["options"] = new Dictionary<string, object>
                {
                    { "num_ctx", 4096 },
                    { "num_predict", 512 },
                    { "temperature", settings.Temperature }
                };
                return;
            }

            payload["temperature"] = settings.Temperature;
            if (IsLocalOllama(settings))
            {
                payload["options"] = new Dictionary<string, object> { { "num_ctx", 4096 }, { "num_predict", 512 }, { "temperature", settings.Temperature } };
                payload["max_tokens"] = 512;
            }
            // DeepSeek uses OpenAI-compatible chat/completions. Keep payload minimal for compatibility.
        }

        static bool IsLocalOllama(Settings settings)
        {
            var provider = settings.Provider ?? "";
            var host = settings.Host ?? "";
            var port = settings.Port ?? "";
            return provider.StartsWith("local-ollama", StringComparison.OrdinalIgnoreCase) || port == "11434" || host == "127.0.0.1" || host.Equals("localhost", StringComparison.OrdinalIgnoreCase) || host.StartsWith("192.168.", StringComparison.OrdinalIgnoreCase);
        }

        static bool IsOllamaNativeChat(Settings settings)
        {
            var path = settings.Path ?? "";
            return IsLocalOllama(settings) && path.IndexOf("/api/chat", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        static string ParseReply(JavaScriptSerializer serializer, string response)
        {
            var root = serializer.DeserializeObject(response) as Dictionary<string, object>;
            if (root == null) return response;

            if (root.ContainsKey("choices"))
            {
                var choices = root["choices"] as object[];
                if (choices != null && choices.Length > 0)
                {
                    var first = choices[0] as Dictionary<string, object>;
                    if (first != null && first.ContainsKey("message"))
                    {
                        var message = first["message"] as Dictionary<string, object>;
                        if (message != null && message.ContainsKey("content")) return Convert.ToString(message["content"]);
                    }
                    if (first != null && first.ContainsKey("text")) return Convert.ToString(first["text"]);
                }
            }
            if (root.ContainsKey("response")) return Convert.ToString(root["response"]);
            if (root.ContainsKey("message"))
            {
                var message = root["message"] as Dictionary<string, object>;
                if (message != null && message.ContainsKey("content")) return Convert.ToString(message["content"]);
                return Convert.ToString(root["message"]);
            }
            return response;
        }

        static string ParseStreamingDelta(JavaScriptSerializer serializer, string jsonLine)
        {
            try
            {
                var root = serializer.DeserializeObject(jsonLine) as Dictionary<string, object>;
                if (root == null) return "";

                if (root.ContainsKey("choices"))
                {
                    var choices = root["choices"] as object[];
                    if (choices != null && choices.Length > 0)
                    {
                        var first = choices[0] as Dictionary<string, object>;
                        if (first != null && first.ContainsKey("delta"))
                        {
                            var delta = first["delta"] as Dictionary<string, object>;
                            if (delta != null && delta.ContainsKey("content")) return Convert.ToString(delta["content"]);
                        }
                        if (first != null && first.ContainsKey("message"))
                        {
                            var message = first["message"] as Dictionary<string, object>;
                            if (message != null && message.ContainsKey("content")) return Convert.ToString(message["content"]);
                        }
                        if (first != null && first.ContainsKey("text")) return Convert.ToString(first["text"]);
                    }
                }

                if (root.ContainsKey("response")) return Convert.ToString(root["response"]);
                if (root.ContainsKey("message"))
                {
                    var message = root["message"] as Dictionary<string, object>;
                    if (message != null && message.ContainsKey("content")) return Convert.ToString(message["content"]);
                    return Convert.ToString(root["message"]);
                }
            }
            catch
            {
            }
            return "";
        }
    }
}































































































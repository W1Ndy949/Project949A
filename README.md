# Project949A
Desktop AI agent to assist and provide emotional support

## 直接运行

双击当前文件夹内的：

```text
Project949_v1.0.exe
```

转发给别人时，请发送整个 `Project949_v1.0` 文件夹，不要只发送 exe。

## 文件结构

```text
Project949_v1.0.exe
app.ico
Program.cs
build-exe.ps1
README.md
PACKAGE_README.txt
tietu/
  icon.png
  idle.png
  speaking.png
```

## 贴图和图标

程序会优先读取当前文件夹内的 `tietu`：

```text
tietu\idle.png
tietu\speaking.png
```

`idle.png` 用于待机形态，`speaking.png` 用于 AI 逐字输出时的说话形态。`icon.png` 是 exe 图标来源，构建时会转换为 `app.ico` 并编入 exe。

## 当前版本特性

- 桌面 sprite 自动置顶，可拖动。
- idle 状态为底部固定、上下拉伸动画，约 6 FPS。
- speaking 状态会随 AI 每输出一个字符上下抖动，抖动幅度跟随贴图大小变化。
- 控制台支持修改宠物名，AI 发言格式为 `名称：内容`。
- 左侧可调整 AI 入口、宠物名、persona、温度、贴图大小。
- 支持本地 Ollama llama3.2、DeepSeek 预设和自定义 OpenAI-compatible API。
- 支持文本输入、Windows 语音听写、图片上传、文本文件上传。
- 情感/情绪模块暂时停用：对话时不参考，回复结束后不更新第二记忆区。
- AI 正文输出完后会立即切回 idle，再后台更新聊天记忆。

## AI 配置

如果使用本地 Ollama：

```text
http://127.0.0.1:11434/v1/chat/completions
model: llama3.2:latest
```

如果使用 DeepSeek 或其他在线 API，请在控制台中填写你自己的 API Key。不要把个人 API Key 写进源码或随文件夹转发。

## 重新构建

在项目根目录运行：

```powershell
powershell -ExecutionPolicy Bypass -File .\DesktopCyberPetNative\build-exe.ps1
```

构建脚本会输出并刷新：

```text
DesktopCyberPetNative\versions\Project949_v1.0\Project949_v1.0.exe
```

同时会把桌面 `tietu` 文件夹中的 `idle.png`、`speaking.png`、`icon.png` 复制进版本文件夹。

## 注意

- 目标电脑仍需自行配置 API 或本地 Ollama。
- Windows 语音听写依赖系统已安装的语音识别器。
- PDF、Word、视频、音频当前只发送文件信息，不解析真实内容。

# Project949 v4.0

这是基于 v3.9.1 重新生成的 v4.0，放置在 `P949\v4.0`。完整保留 v3.9.1 的设置、API Key、Serper Key、Persona、MCP 记忆与贴图，不包含旧 v4.0 的实时读屏和独立视觉模型改动。

## 本版变更

### DeepSeek V4

- 删除固定的 `deepseek-chat` 和 `deepseek-reasoner` 入口，统一使用 `deepseek-v4-flash`。
- 旧 DeepSeek 官方或 DeepSeek API 自定义预设会在首次启动时迁移，API Key 和 Serper Key 保留。
- 新增 `thinking` 开关；开启时向 DeepSeek V4 发送 `thinking.type=enabled`，关闭时发送 `disabled`。
- 本地 Ollama 入口使用对应的 `think` 布尔参数。

### MCP 检索与表情修复

- MCP 记忆检索 Top-K 当前为 `9`，高于要求的 `5`，因此保持不变。
- 表情控制标记由可选改为每轮必选；普通回复使用中性的 `speaking` 表情。
- 未收到模型标记时，程序会在完整回复生成后根据回复语义自动选择表情，并保证至少使用正常说话表情。
- 自动判断不再基于流式输出的半句话过早触发，减少表情与完整语境不一致。
- 写入 MCP 记忆时不再清空刚显示的表情。

### setup 顶部布局修复

- 顶部安装选项不再使用固定 96px 高度。
- 四个安装复选项会随系统 DPI、字体和文字高度自动扩展。
- setup 整页保留滚动能力，小窗口下也能查看全部选项和按钮。

### Persona 最高优先级补丁

- Persona 是应用层唯一的当前角色身份来源，姓名、是否为人类、职业、背景和自我描述都以 Persona 为准。
- 模型预训练身份、服务商默认信息、旧回复、对话历史、MCP 记忆、网页和文件都不能覆盖 Persona。
- Persona 在上下文首部注入，并在历史消息之后、当前用户消息之前再次注入，降低旧身份与模型默认身份的干扰。
- Ollama 消息不再把全部 system 内容合并并前移，最终 Persona 检查会保留在历史消息之后。
- MCP 记忆被降为事实参考，记忆摘要器不再自行写入“AI、模型、机器人或助手”等身份判断。

### 同版本 Serper 开关修正

- 开启 Serper 选项后，每条用户消息都调用一次常规网页搜索。
- 关闭 Serper 选项后，任何消息都不会调用搜索。
- 不再根据关键词切换新闻接口、时间范围或搜索模式。

1. 简化回复语言处理

- 删除生成后的语言检测、判错和二次重写。
- 每轮仅在正式生成时，根据用户当前消息自动选择一种回复语言。
- 普通文本只生成该语言；网址、代码、命令、产品名和必要专有名词保持原文。

2. Persona 身份刷新

- 当前 Persona 在每轮回答中作为权威身份优先传入。
- Persona 或宠物名修改后，旧消息仍保留显示，但不再作为新身份的模型上下文。
- MCP 记忆中的旧名字或旧身份与当前 Persona 冲突时会被忽略。

3. 保留 v3.8 功能

- “联网”默认关闭；勾选后每条消息使用 Serper 搜索。
- 保留表情语境补丁、API/Serper Key 自动保存和密码输入框。
- setup 保留滚动布局、基础文件清单和下载入口。

## 主程序

```text
Project949_v4.0.exe
```

## setup

```text
setup_base.exe
```

## 构建

```powershell
powershell -ExecutionPolicy Bypass -File .\build-exe.ps1
```

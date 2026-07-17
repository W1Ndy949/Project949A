# Project949_v2.0_persistsettings

当前版本在 v2.0 基础上继续修复自动保存，不新建版本号。

## 当前修复

- 用户输入的宠物名称、Persona 提示词、两块记忆、记忆开关、sprite 大小会自动保存。
- 输入变化后约 1 秒自动落盘；关闭控制台、测试连接、发送消息前会立即保存。
- 配置优先保存到 exe 同目录的 `settings.json`，方便整个版本文件夹转发。
- 如果 exe 同目录没有配置，会兼容读取旧的 `%APPDATA%\DesktopCyberPet\settings.json`，之后保存到当前 exe 文件夹。

## 转发方式

转发时发送整个 `Project949_v2.0_persistsettings` 文件夹即可。运行后生成的 `settings.json` 也在这个文件夹内。

## 同版本补丁

- 修复打开控制台时下拉框初始化触发默认预设，导致已保存 settings 被覆盖的问题。
- 控制台加载配置期间会保持 loading 状态，不再自动套用默认 AI 入口。



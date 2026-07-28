# AGENTS.md - YuanShenTools

## 项目概述
- **类型：** .NET 8.0 WPF 桌面应用 (C#)
- **用途：** 原神跟跑工具 - 透明悬浮覆盖层，嵌入 B 站 WebView2 播放器
- **核心功能：** 全局热键、自动跳过 B 站弹窗、书签管理、DWM 特效

## 构建与运行
```bash
# 构建
dotnet build

# 运行
dotnet run

# 还原包（如需要）
dotnet restore
```

## 架构
- **入口：** `App.xaml` → `MainWindow`
- **UI 框架：** WPF，使用 `AllowsTransparency` + `WindowStyle=None`（无边框窗口）
- **WebView：** Microsoft.Web.WebView2 嵌入式浏览器
- **热键：** Win32 全局热键，通过 `RegisterHotKey` P/Invoke（无修饰键，系统级）
- **配置：** JSON 持久化存储于 `%LOCALAPPDATA%/YuanShenTools/config.json`

## 关键文件
- `MainWindow.xaml` / `MainWindow.xaml.cs` - 主界面与逻辑
- `HotKeyManager.cs` - 全局热键注册
- `AutoSkipService.cs` - B 站弹窗自动跳过（JS 注入）
- `Config.cs` - 配置持久化
- `ShortcutHelper.cs` - 桌面快捷方式创建

## 开发说明
- **目标框架：** `net8.0-windows` (WinExe)
- **依赖：** 仅 `Microsoft.Web.WebView2` v1.0.4078.44
- **无测试：** 项目无测试框架和测试文件
- **无 CI：** 无 GitHub Actions 或 CI 工作流
- **无代码风格工具：** 未配置 linter 或格式化工具

## 热键系统
全局热键（系统级，无修饰键）：
- `` ` ``: 播放/暂停
- `5`: 后退 10s，`6`: 前进 10s
- `7`/`8`: 透明度降低/升高
- `9`: 隐藏/显示，`0`: 沉浸模式切换
- `-`: 自动跳过对话，`+`: 书签面板

## 注意事项
- 窗口使用 `AllowsTransparency="True"` + `WindowStyle="None"` - 自定义缩放边框通过 Rectangle 实现
- WebView2 需要目标机器安装运行时
- 热键为全局生效（原神获得焦点时也能响应）- 注意按键冲突
- 配置文件路径为用户级（`%LOCALAPPDATA%`）

## OpenSpec 插件
- 使用 `@devcxl/opencode-spec` 插件（见 `opencode.json`）
- 变更记录在 `openspec/changes/` 目录

## 对话语言
- 使用中文回复用户

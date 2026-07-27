# 设计：原神覆盖浏览器助手

## 概述

单个 WPF 窗口（`MainWindow`）嵌入 WebView2 控件。窗口设置 `Topmost="True"`，通过 P/Invoke `RegisterHotKey` / `UnregisterHotKey` 订阅全局热键。WM_HOTKEY 消息在 `WndProc` 中处理。通过 `WebView2.ExecuteScriptAsync` 注入 JavaScript 控制嵌入浏览器的视频播放。

## 目标

- 热键按下与视频操作之间延迟最小化。
- 零干扰原神输入处理。
- 简洁代码库——一个主窗口、一个全局热键帮助类、一个游戏快捷方式帮助类。

## 约束

- 仅 Windows（WPF + Win32 API）。
- 需要 WebView2 运行时（常青版或固定版）。
- 需要 .NET 8 / 9 桌面运行时。

## 技术方案

### 项目结构

```
YuanShenTools/
├── App.xaml / .cs            — 应用入口
├── MainWindow.xaml / .cs     — 主覆盖窗口
├── HotKeyManager.cs          — 全局热键注册/分发
├── ShortcutHelper.cs         — 创建无边框启动快捷方式
├── BookmarkWindow.xaml/.cs   — 书签弹窗子窗口
├── AutoSkipService.cs        — 基于定时器的对话跳过注入
└── Config.cs                 — 持久化窗口位置/尺寸/透明度
```

### HotKeyManager

- 封装 `user32.dll` 的 `RegisterHotKey` / `UnregisterHotKey`。
- 为每个按键组合分配唯一的 `WM_HOTKEY` ID。
- 暴露 `HotkeyPressed` 事件；`MainWindow` 订阅并分派到对应操作。
- 使用的按键（修饰符 = `ModKey.None` 针对简单键）：
  - VK_5、VK_6、VK_OEM_3（反引号）、VK_7、VK_8、VK_9、VK_0、VK_OEM_MINUS
- 生命周期：在 `OnSourceInitialized` 注册，在 `OnClosed` 注销。

### 通过 ExecuteScriptAsync 控制视频

| 操作 | JavaScript 片段 |
|------|----------------|
| 播放/暂停 | `document.querySelector('video')?.paused ? document.querySelector('video')?.play() : document.querySelector('video')?.pause()` |
| 快进 10 秒 | `document.querySelector('video')?.__vjs_getProperty('currentTime').then(t => document.querySelector('video')?.__vjs_setProperty('currentTime', t + 10))` |
| 快退 10 秒 | `document.querySelector('video')?.__vjs_getProperty('currentTime').then(t => document.querySelector('video')?.__vjs_setProperty('currentTime', t - 10))` |

### AutoSkipService

- `DispatcherTimer`，间隔 2 秒。
- 启用时执行：`document.querySelector('.bili-dialog, .bili-popup')?.remove()` 通过 `ExecuteScriptAsync`。
- 通过 `-` 键切换；状态显示在窗口标题栏或小指示器中。

### 无边框快捷方式（ShortcutHelper）

- 首次启动时检查 `%USERPROFILE%\Desktop\原神（无边框）.lnk`。
- 如不存在，创建指向 `GenshinImpact.exe` 的快捷方式，附带命令行参数 `-popupwindow`。
- 目标路径从注册表发现或用户首次运行时选择（v1 中使用常见安装路径）。

### 配置持久化

- `Properties.Settings` 或 `%LOCALAPPDATA%\YuanShenTools` 中的 JSON 文件。
- 存储：窗口 `Left`、`Top`、`Width`、`Height`、`Opacity`、上次 URL、书签列表。

## 影响文件/模块

- `MainWindow.xaml` / `.cs` — UI 布局、WebView2 设置、热键分派。
- `HotKeyManager.cs` — 新文件，全局热键封装。
- `ShortcutHelper.cs` — 新文件，快捷方式创建。
- `BookmarkWindow.xaml` / `.cs` — 新文件，书签弹窗。
- `AutoSkipService.cs` — 新文件，对话跳过定时器。
- `Config.cs` — 新文件，设置持久化。

## 备选方案

- **Chromium Embedded Framework (CEF)**：比 WebView2 更重、设置更复杂。WebView2 更轻量且随 Windows 11 内置。
- **`SendKeys` / 输入模拟**：不如 `ExecuteScriptAsync`，因为它不依赖焦点或键盘布局。
- **DXR 覆盖层**：技术上可行但对简单的浏览器覆盖层来说过于复杂。

# TOIREVU 🎛️

A beautiful, physically-simulated retro analog VU meter for your desktop. 

TOIREVU 是一款跨平台的桌面复古 VU 表（音量计）。内置了真实的物理模拟算法，模拟硬件表针的弹性和惯性感。


<img width="370" height="300" alt="image" src="https://github.com/user-attachments/assets/0365e6e8-bc8e-469e-885b-340b5f5f569e" />


## ✨ 特性 (Features)

* **物理弹道 (Analog Ballistics)**：模拟表针运动，还原模拟硬件的阻尼感与回弹动作。
* **可调节内部增益 (Adjustable Gain)**：直观的增益调节与重置按钮（点击表针底部图钉重置），适配不同的系统音量习惯，且会自动记住你的设置。
* **Peak 峰值指示灯 (Peak Indicator)**：内置高灵敏度的红色削波指示灯，精准捕捉瞬间的高音量峰值。
* **置顶模式 (Always on Top)**：点击右上角的图钉图标，即可让 VU 表悬浮在所有窗口之上，作为你的桌面环境氛围装饰。
* **轻量 (Lightweight)**：得益于底层的优化，资源占用极小，可以安静地在后台运行。
* **跨平台支持 (Cross-Platform)**：原生支持 Windows、macOS (Apple Silicon & Intel) 以及 Linux。

## 🚀 下载与运行 (Download)

请前往 [Releases 页面](你的仓库Release页面链接) 下载适用于你操作系统的最新版本。

TOIREVU 提供了单文件/便携式免安装版本：

* **Windows**: 下载 `.zip` 压缩包，解压后双击 `RetroVU.exe` 即可运行。
* **macOS**: 下载适用于您芯片类型（Apple M 系列芯片下载 `ARM64` / Intel 芯片下载 `x64`）的 `.zip` 文件，解压后直接运行 `RetroVU` 应用程序。
* **Linux**: 下载 `.zip` 压缩包，解压后在终端赋予执行权限（`chmod +x RetroVU`），即可双击或通过终端运行。

## 🕹️ 如何使用 (Usage)

* **移动窗口**：按住表盘任意空白区域即可拖动窗口。
* **调节灵敏度**：点击表盘下方黑色图钉两侧的 `-` / `+` 按钮，调节内部增益，让表针在当前系统音量下能达到跳动幅度。
* **重置增益**：直接点击表盘正下方中心的 **红色圆环（图钉）**，即可一键将增益重置为默认完美数值。
* **窗口置顶**：点击右上角的 📌 图钉图标，图标变亮即代表已固定在桌面最顶层。
* **退出应用**：点击右上角的 ❌ 关闭按钮。

## 🛠️ 技术栈 (Tech Stack)

* **C# / .NET 9.0**
* **Avalonia UI** (强大的跨平台 UI 框架)

## 📄 许可证 (License)

本项目采用 [MIT License](LICENSE) 开源许可证。

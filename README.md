# Nexus

<div align="center">

![Avalonia](https://img.shields.io/badge/Avalonia-11.3.11-0E4D92?style=flat-square)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square)
![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?style=flat-square)
![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)

**红旗中学智慧校园桌面客户端**

基于 Avalonia 的跨平台考勤管理系统

</div>

---

## 📖 项目简介

Nexus 是一款专为红旗中学智慧校园系统设计的桌面客户端应用，提供设备绑定、考勤管理、实时通信等功能。采用现代化的 Avalonia UI 框架开发，支持 Windows 平台运行。

### ✨ 核心功能

| 功能模块 | 描述 |
|---------|------|
| 🔐 **设备绑定** | 通过二维码扫描快速绑定设备到指定班级 |
| 📊 **概览面板** | 实时显示设备状态、考勤概览、系统通知 |
| 📡 **WebSocket通信** | 增强型连接管理，支持消息队列、心跳检测、自动重连 |
| 🖥️ **系统托盘** | 最小化到托盘，后台运行不影响正常使用 |
| 🔄 **自动更新** | 支持在线检测更新，一键升级 |
| ⚙️ **设置管理** | 设备解绑、配置管理、日志查看 |

---

## 🛠️ 技术栈

- **框架**: [Avalonia UI 11.3](https://avaloniaui.net/) - 跨平台 XAML 框架
- **运行时**: .NET 8.0
- **UI组件**: [FluentAvaloniaUI](https://github.com/amwx/FluentAvalonia) - Fluent Design 风格组件
- **MVVM**: [CommunityToolkit.Mvvm](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/)
- **通信**: [SocketIOClient](https://github.com/doghappy/socket.io-client-csharp) - WebSocket 客户端
- **二维码**: [QRCoder](https://github.com/codebude/QRCoder/) - 二维码生成

---

## 📦 安装与运行

### 环境要求

- Windows 10/11 (x64)
- .NET 8.0 Runtime

### 从源码构建

```bash
# 克隆仓库
git clone https://github.com/chenxingpengs/Nexus.git
cd Nexus

# 还原依赖
dotnet restore

# 构建项目
dotnet build

# 运行项目
dotnet run
```

### 发布版本

```bash
# 发布 Windows x64 单文件版本
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## 🏗️ 项目结构

```
Nexus/
├── App.axaml.cs              # 应用入口，启动流程控制
├── Program.cs                # 主程序，单实例控制
│
├── Models/                   # 数据模型
│   ├── AppConfig.cs          # 应用配置模型
│   └── UpdateInfo.cs         # 更新信息模型
│
├── ViewModels/               # 视图模型 (MVVM)
│   ├── MainViewModel.cs      # 主界面视图模型
│   ├── MainWindowViewModel.cs # 绑定窗口视图模型
│   ├── SplashScreenViewModel.cs # 启动页视图模型
│   └── Pages/
│       ├── DashboardViewModel.cs
│       ├── SettingsViewModel.cs
│       └── UpdateViewModel.cs
│
├── Views/                    # 视图界面
│   ├── MainView.axaml        # 主界面
│   ├── MainWindow.axaml      # 绑定窗口
│   ├── SplashScreen.axaml    # 启动页
│   └── Pages/
│       ├── DashboardPage.axaml
│       ├── SettingsPage.axaml
│       ├── UpdatePage.axaml
│       └── AboutPage.axaml
│
└── Services/                 # 服务层
    ├── AuthService.cs        # 认证服务
    ├── ConfigService.cs      # 配置存储服务 (DPAPI加密)
    ├── DeviceIdentifier.cs   # 设备标识服务
    ├── QRCodeService.cs      # 二维码生成服务
    ├── TrayService.cs        # 系统托盘服务
    ├── UpdateService.cs      # 更新服务
    ├── PowerControlService.cs # 电源控制服务
    ├── WolService.cs         # Wake-on-LAN 服务
    └── WebSocket/            # WebSocket 增强服务
        ├── EnhancedSocketIOService.cs
        ├── MessageQueueManager.cs
        ├── AckManager.cs
        ├── SmartHeartbeatManager.cs
        ├── ConnectionQualityMonitor.cs
        ├── FlowController.cs
        └── ...
```

---

## 🔧 核心功能详解

### 启动流程

```
应用启动
    │
    ├─ 检查单实例运行
    │
    ▼
检查配置文件
    │
    ├─ 已绑定 ──▶ 验证Token ──▶ 进入主界面
    │                              │
    │                              └─ 验证失败 ──▶ 显示绑定界面
    │
    └─ 未绑定 ──▶ 显示启动页 ──▶ 进入绑定界面
```

### WebSocket 增强服务

提供企业级的 WebSocket 连接管理：

| 组件 | 功能 |
|------|------|
| **MessageQueueManager** | 消息队列管理，支持优先级和TTL |
| **AckManager** | 消息确认机制，超时重发 |
| **SmartHeartbeatManager** | 智能心跳，根据网络质量动态调整 |
| **ExponentialBackoffStrategy** | 指数退避重连策略 |
| **ConnectionQualityMonitor** | 连接质量监控 |
| **FlowController** | 流量控制，防止消息洪泛 |
| **StateRecoveryManager** | 状态恢复管理 |
| **SequenceNumberManager** | 消息序列号管理 |

### 配置存储

- 存储位置: `%LocalAppData%\Nexus\config.json`
- Token 加密: Windows DPAPI
- 安全措施: 设备ID绑定验证

---

## 📸 界面预览

### 主界面布局

```
┌─────────────────────────────────────────────────────────┐
│  Nexus                              [─] [□] [×]         │
├──────────────┬──────────────────────────────────────────┤
│              │                                          │
│   📊 概览    │           主内容区                        │
│              │                                          │
│   ⚙️ 设置    │                                          │
│              │                                          │
│   🔄 更新    │                                          │
│              │                                          │
│   ℹ️ 关于    │                                          │
│              │                                          │
├──────────────┴──────────────────────────────────────────┤
│  状态栏: 连接状态 | 设备ID: device_xxx                   │
└─────────────────────────────────────────────────────────┘
```

---

## 🔌 后端 API 要求

项目需要配合后端服务使用，主要接口：

| 接口 | 方法 | 描述 |
|------|------|------|
| `/desktop/bind/qrcode` | GET | 获取绑定二维码 |
| `/desktop/bind/verify` | POST | 验证绑定状态 |
| `/desktop/device/verify` | GET | 验证设备Token |
| `/desktop/update/check` | GET | 检查更新 |

---

## 📝 开发说明

### 单实例运行

应用使用 Mutex 确保单实例运行，防止多开。

### 系统托盘

关闭窗口时最小化到托盘，通过托盘菜单可以：
- 显示主窗口
- 查看连接状态
- 退出应用

### 自动更新

支持检测新版本并下载更新包，更新包格式：

```
Nexus-{version}-win-x64.exe
```

---

## 📄 许可证

本项目采用 MIT 许可证。

---

## 🤝 贡献

欢迎提交 Issue 和 Pull Request！

---

<div align="center">

**技术支持：陈星鹏**

</div>

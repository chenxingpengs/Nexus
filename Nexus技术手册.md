# Nexus 智慧校园桌面客户端技术手册

<div align="center">

![Avalonia](https://img.shields.io/badge/Avalonia-11.3.11-0E4D92?style=flat-square)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square)
![Platform](https://img.shields.io/badge/Platform-Windows-0078D6?style=flat-square)
![License](https://img.shields.io/badge/License-MIT-green?style=flat-square)

**红旗中学智慧校园桌面客户端**

版本：3.0.0 | 更新日期：2026年4月

</div>

---

## 目录

1. [项目概述](#一项目概述)
2. [系统架构](#二系统架构)
3. [技术栈详解](#三技术栈详解)
4. [核心功能模块](#四核心功能模块)
5. [API 接口文档](#五api-接口文档)
6. [WebSocket 通信协议](#六websocket-通信协议)
7. [插件系统](#七插件系统)
8. [部署与运维](#八部署与运维)
9. [用户操作指南](#九用户操作指南)
10. [故障排除](#十故障排除)
11. [开发指南](#十一开发指南)

---

## 一、项目概述

### 1.1 项目简介

Nexus 是一款专为红旗中学智慧校园系统设计的桌面客户端应用，提供设备绑定、考勤管理、实时通信、灾害预警等功能。采用 Avalonia UI 框架开发，支持 Windows 平台运行。

### 1.2 系统定位

Nexus 作为智慧校园系统的**教室终端客户端**，与以下系统协同工作：

| 系统 | 技术栈 | 功能定位 |
|------|--------|----------|
| **Nexus** | Avalonia + .NET 8 | 教室桌面终端 |
| **Flask 后端** | Python Flask + SocketIO | API 服务 & 实时通信 |
| **微信小程序** | Vue3 + uni-app | 移动端考勤录入 |
| **管理后台** | Vue3 + Element Plus | 系统管理 & 数据统计 |

### 1.3 核心功能

| 功能模块 | 描述 |
|---------|------|
| 设备绑定 | 通过二维码扫描快速绑定设备到指定班级 |
| 概览面板 | 实时显示设备状态、考勤概览、系统通知 |
| 考勤小组件 | 桌面小组件实时显示考勤状态，支持时段自动显示/隐藏 |
| WebSocket通信 | 增强型连接管理，支持消息队列、心跳检测、自动重连 |
| 灾害预警 | 支持地震预警、防空警报、火灾警报等紧急通知 |
| 系统托盘 | 最小化到托盘，后台运行不影响正常使用 |
| 自动更新 | 支持在线检测更新，一键升级 |
| 插件系统 | 支持动态加载插件，扩展功能 |

---

## 二、系统架构

### 2.1 整体架构图

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           智慧校园系统架构                                │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐                 │
│  │  微信小程序  │    │   管理后台   │    │   Nexus    │                 │
│  │  (移动端)    │    │  (Web端)    │    │  (桌面端)   │                 │
│  └──────┬──────┘    └──────┬──────┘    └──────┬──────┘                 │
│         │                  │                  │                         │
│         └──────────────────┼──────────────────┘                         │
│                            │                                            │
│                            ▼                                            │
│              ┌─────────────────────────────┐                           │
│              │      Flask 后端服务          │                           │
│              │  ┌───────────────────────┐  │                           │
│              │  │   REST API (HTTP)     │  │                           │
│              │  ├───────────────────────┤  │                           │
│              │  │   WebSocket (SocketIO)│  │                           │
│              │  └───────────────────────┘  │                           │
│              └──────────────┬──────────────┘                           │
│                             │                                           │
│         ┌───────────────────┼───────────────────┐                       │
│         ▼                   ▼                   ▼                       │
│  ┌─────────────┐    ┌─────────────┐    ┌─────────────┐                 │
│  │   MySQL     │    │   Redis     │    │  文件存储    │                 │
│  │  (数据库)   │    │  (缓存)     │    │  (上传文件)  │                 │
│  └─────────────┘    └─────────────┘    └─────────────┘                 │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 2.2 Nexus 内部架构

```
┌─────────────────────────────────────────────────────────────────────────┐
│                            Nexus 应用架构                                │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                        表现层 (Views)                            │   │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────────────┐   │   │
│  │  │MainView  │ │Dashboard │ │Settings  │ │DisasterWarning   │   │   │
│  │  │          │ │  Page    │ │  Page    │ │    Window        │   │   │
│  │  └──────────┘ └──────────┘ └──────────┘ └──────────────────┘   │   │
│  │  ┌──────────┐ ┌──────────┐ ┌──────────────────────────────┐   │   │
│  │  │SplashScreen│ │BindWindow│ │   DesktopWidgetWindow      │   │   │
│  │  └──────────┘ └──────────┘ └──────────────────────────────┘   │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                    │                                    │
│                                    ▼                                    │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                      视图模型层 (ViewModels)                     │   │
│  │  ┌──────────────┐ ┌──────────────┐ ┌──────────────────────┐   │   │
│  │  │MainViewModel │ │DashboardVM   │ │AttendanceCardVM      │   │   │
│  │  └──────────────┘ └──────────────┘ └──────────────────────┘   │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                    │                                    │
│                                    ▼                                    │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                        服务层 (Services)                         │   │
│  │  ┌────────────┐ ┌────────────┐ ┌────────────┐ ┌────────────┐   │   │
│  │  │AuthService │ │ConfigService│ │ToastService│ │TrayService │   │   │
│  │  └────────────┘ └────────────┘ └────────────┘ └────────────┘   │   │
│  │  ┌────────────┐ ┌────────────┐ ┌────────────┐ ┌────────────┐   │   │
│  │  │WidgetService│ │Attendance  │ │UpdateService│ │SoundService│   │   │
│  │  │            │ │  Service   │ │            │ │            │   │   │
│  │  └────────────┘ └────────────┘ └────────────┘ └────────────┘   │   │
│  │  ┌────────────────────────────────────────────────────────┐   │   │
│  │  │              EnhancedSocketIOService                   │   │   │
│  │  │  ┌──────────────┐ ┌──────────────┐ ┌────────────────┐ │   │   │
│  │  │  │MessageQueue  │ │AckManager    │ │HeartbeatManager│ │   │   │
│  │  │  └──────────────┘ └──────────────┘ └────────────────┘ │   │   │
│  │  │  ┌──────────────┐ ┌──────────────┐ ┌────────────────┐ │   │   │
│  │  │  │Reconnect     │ │StateRecovery │ │QualityMonitor  │ │   │   │
│  │  │  │  Strategy    │ │   Manager    │ │                │ │   │   │
│  │  │  └──────────────┘ └──────────────┘ └────────────────┘ │   │   │
│  │  └────────────────────────────────────────────────────────┘   │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                    │                                    │
│                                    ▼                                    │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                        数据层 (Models)                           │   │
│  │  ┌────────────┐ ┌────────────┐ ┌────────────┐ ┌────────────┐   │   │
│  │  │ AppConfig  │ │Attendance  │ │ WidgetConfig│ │ Notification│   │   │
│  │  │            │ │  Models    │ │            │ │            │   │   │
│  │  └────────────┘ └────────────┘ └────────────┘ └────────────┘   │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                        插件系统 (Plugins)                        │   │
│  │  ┌────────────┐ ┌────────────┐ ┌────────────┐ ┌────────────┐   │   │
│  │  │ PluginHost │ │PluginService│ │PluginUISvc │ │WebSocketBrg│   │   │
│  │  └────────────┘ └────────────┘ └────────────┘ └────────────┘   │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 2.3 项目目录结构

```
Nexus/
├── App.axaml.cs              # 应用入口，启动流程控制
├── Program.cs                # 主程序，单实例控制
│
├── Models/                   # 数据模型
│   ├── AppConfig.cs          # 应用配置模型
│   ├── ApiResponse.cs        # API 响应模型
│   ├── Notification.cs       # 通知模型
│   ├── Widget/               # 小组件模型
│   │   ├── AttendanceCardModel.cs
│   │   ├── WeatherCardModel.cs
│   │   ├── WidgetConfig.cs
│   │   └── ...
│   ├── Attendance/           # 考勤数据模型
│   │   └── AttendanceModels.cs
│   └── Schedule/             # 排班数据模型
│       └── ScheduleModels.cs
│
├── ViewModels/               # 视图模型 (MVVM)
│   ├── MainViewModel.cs
│   ├── MainWindowViewModel.cs
│   ├── SplashScreenViewModel.cs
│   ├── Pages/
│   │   ├── DashboardViewModel.cs
│   │   ├── SettingsViewModel.cs
│   │   └── UpdateViewModel.cs
│   └── Widget/
│       └── Cards/
│           └── AttendanceCardViewModel.cs
│
├── Views/                    # 视图界面
│   ├── MainView.axaml
│   ├── MainWindow.axaml
│   ├── SplashScreen.axaml
│   ├── DisasterWarningWindow.axaml
│   ├── Pages/
│   │   ├── DashboardPage.axaml
│   │   ├── SettingsPage.axaml
│   │   └── UpdatePage.axaml
│   └── Widget/
│       └── DesktopWidgetWindow.axaml
│
├── Services/                 # 服务层
│   ├── AuthService.cs        # 认证服务
│   ├── ConfigService.cs      # 配置存储服务
│   ├── ToastService.cs       # Toast 通知服务
│   ├── TrayService.cs        # 系统托盘服务
│   ├── UpdateService.cs      # 自动更新服务
│   ├── SoundService.cs       # 音频播放服务
│   ├── TTS.cs                # 语音合成服务
│   ├── Attendance/
│   │   └── AttendanceService.cs
│   ├── Widget/
│   │   ├── WidgetService.cs
│   │   ├── WeatherService.cs
│   │   └── CitySearchService.cs
│   ├── WebSocket/
│   │   ├── EnhancedSocketIOService.cs
│   │   ├── MessageQueueManager.cs
│   │   ├── AckManager.cs
│   │   ├── SmartHeartbeatManager.cs
│   │   └── ...
│   └── Http/
│       ├── HttpService.cs
│       └── PluginApiService.cs
│
├── Plugins/                  # 插件系统
│   ├── Contracts/
│   │   ├── IPlugin.cs
│   │   ├── IPluginContext.cs
│   │   └── ...
│   ├── Core/
│   │   ├── PluginHost.cs
│   │   ├── PluginBase.cs
│   │   └── ...
│   └── Services/
│       ├── PluginService.cs
│       └── WebSocketBridgeService.cs
│
├── ExternalPlugins/          # 外部插件目录
│   └── ExampleAttendancePlugin/
│
├── Assets/                   # 资源文件
│   ├── Sounds/               # 音频文件
│   │   ├── air_raid_attack.mp3
│   │   ├── earthquake_warning.mp3
│   │   └── fire_alarm.mp3
│   └── hqzx.ico
│
└── Data/
    └── Cities.json           # 城市数据
```

---

## 三、技术栈详解

### 3.1 核心框架

| 技术 | 版本 | 用途 |
|------|------|------|
| **Avalonia UI** | 11.3.11 | 跨平台 XAML UI 框架 |
| **.NET** | 8.0 | 运行时框架 |
| **CommunityToolkit.Mvvm** | 最新 | MVVM 模式支持 |

### 3.2 UI 组件库

| 组件 | 用途 |
|------|------|
| **FluentAvalonia** | Fluent Design 风格组件 |
| **Avalonia.Controls.DataGrid** | 数据表格组件 |
| **Avalonia.Controls.ColorPicker** | 颜色选择器 |

### 3.3 通信与网络

| 库 | 用途 |
|------|------|
| **SocketIOClient** | WebSocket 客户端，实时通信 |
| **System.Net.Http** | HTTP 请求 |

### 3.4 工具库

| 库 | 用途 |
|------|------|
| **QRCoder** | 二维码生成 |
| **NAudio** | 音频播放 |
| **Edge_tts_sharp** | Edge TTS 语音合成 |
| **System.Text.Json** | JSON 序列化 |

### 3.5 配置存储

- **存储位置**: `%LocalAppData%\Nexus\config.json`
- **加密方式**: Windows DPAPI（Data Protection API）
- **安全措施**: 设备 ID 绑定验证

---

## 四、核心功能模块

### 4.1 设备绑定流程

```
┌─────────────────────────────────────────────────────────────────────────┐
│                           设备绑定流程                                   │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌──────────┐     ┌──────────┐     ┌──────────┐     ┌──────────┐      │
│  │ 应用启动  │────▶│ 检查配置 │────▶│ 已绑定？ │────▶│ 验证Token│      │
│  └──────────┘     └──────────┘     └────┬─────┘     └────┬─────┘      │
│                                          │                │            │
│                                          │ 否             │ 失败       │
│                                          ▼                ▼            │
│                                   ┌──────────┐     ┌──────────┐       │
│                                   │ 显示启动 │     │ 显示绑定 │       │
│                                   │   页面   │     │   页面   │       │
│                                   └────┬─────┘     └────┬─────┘       │
│                                        │                │             │
│                                        ▼                ▼             │
│                                   ┌──────────┐     ┌──────────┐       │
│                                   │ 生成绑定 │     │ 微信扫码 │       │
│                                   │  二维码  │◀────│   确认   │       │
│                                   └────┬─────┘     └──────────┘       │
│                                        │                               │
│                                        ▼                               │
│                                   ┌──────────┐                         │
│                                   │ 绑定成功 │                         │
│                                   │ 进入主界面│                         │
│                                   └──────────┘                         │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

**绑定流程说明**：

1. **生成绑定 Token**
   - 客户端调用 `/desktop/bind/token` 接口
   - 服务端生成临时 Token（有效期 5 分钟）
   - Token 存储在 Redis 中

2. **生成二维码**
   - 客户端使用 QRCoder 生成二维码
   - 二维码内容：`nexus://bind?token={token}`

3. **微信扫码确认**
   - 教师使用微信小程序扫描二维码
   - 小程序解析 Token，调用后端确认绑定
   - 选择要绑定的班级

4. **WebSocket 推送**
   - 后端通过 WebSocket 推送绑定成功通知
   - 客户端收到通知，获取 Access Token
   - 保存配置，进入主界面

### 4.2 考勤小组件

#### 4.2.1 功能特性

| 功能 | 描述 |
|------|------|
| 时段自动显示 | 进入考勤时段自动显示小组件，离开时段自动隐藏 |
| 非考勤时段提示 | 非考勤时段显示"非考勤时段"提示 |
| 停课/调课显示 | 显示停课原因或调课信息 |
| 出勤率统计 | 实时显示应到、实到、请假、缺勤人数 |
| WebSocket 实时同步 | 微信小程序提交考勤后自动更新小组件数据 |

#### 4.2.2 小组件界面

```
┌─────────────────────────────────────┐
│  📅 今日考勤        初三(5)班        │
├─────────────────────────────────────┤
│           出勤率                    │
│           96.00%                    │
│       ████████████░░░░              │
├─────────────────────────────────────┤
│  👥 应到: 50    ✓ 实到: 48          │
│  ⏰ 请假: 2     ✗ 缺勤: 0           │
├─────────────────────────────────────┤
│          [查看详情]                  │
└─────────────────────────────────────┘
```

#### 4.2.3 时段监控机制

```csharp
// AttendanceService.cs
public void StartMonitoring()
{
    _monitorTimer = new Timer(CheckTimeSlot, null, 0, 60000);
}

private async Task CheckTimeSlotAsync()
{
    // 每分钟检查当前时段
    var currentSlot = GetCurrentTimeSlot();
    if (currentSlot != null)
    {
        // 进入考勤时段，显示小组件
        TimeSlotChanged?.Invoke(this, currentSlot);
    }
    else
    {
        // 离开考勤时段，隐藏小组件
        LeaveAttendanceTime?.Invoke(this, EventArgs.Empty);
    }
}
```

### 4.3 灾害预警系统

#### 4.3.1 支持的预警类型

| 类型 | 子类型 | 说明 |
|------|--------|------|
| **地震预警** | early_warning | 地震预警（倒计时） |
| | arrival | 地震到达报 |
| **防空警报** | pre_warning | 预先警报 |
| | air_raid | 空袭警报 |
| | all_clear | 解除警报 |
| **火灾警报** | - | 火灾警报 |

#### 4.3.2 预警窗口功能

- **全屏显示**: 覆盖整个屏幕
- **闪烁效果**: 根据预警类型显示不同颜色闪烁
- **倒计时**: 地震预警显示到达倒计时
- **声音警报**: 自动播放对应警报声音
- **TTS 语音**: 自动播报预警内容

#### 4.3.3 预警数据结构

```json
{
  "id": "notification_xxx",
  "type": "earthquake_warning",
  "alertSubtype": "early_warning",
  "title": "地震预警",
  "content": "预计地震波将在30秒后到达",
  "magnitude": "5.2级",
  "etaSeconds": 30,
  "flashColor": "#FF0000",
  "soundFile": "earthquake_warning.mp3"
}
```

### 4.4 WebSocket 增强服务

#### 4.4.1 架构组件

| 组件 | 功能 |
|------|------|
| **MessageQueueManager** | 消息队列管理，支持优先级和 TTL |
| **AckManager** | 消息确认机制，超时重发 |
| **SmartHeartbeatManager** | 智能心跳，根据网络质量动态调整 |
| **ExponentialBackoffStrategy** | 指数退避重连策略 |
| **ConnectionQualityMonitor** | 连接质量监控 |
| **FlowController** | 流量控制，防止消息洪泛 |
| **StateRecoveryManager** | 状态恢复管理 |
| **SequenceNumberManager** | 序列号管理，防止重复消息 |

#### 4.4.2 连接状态

```csharp
public enum ConnectionStatus
{
    Disconnected,    // 未连接
    Connecting,      // 连接中
    Connected,       // 已连接
    Reconnecting,    // 重连中
    Error            // 连接错误
}
```

#### 4.4.3 消息发送选项

```csharp
public class SendOptions
{
    public int Priority { get; set; } = 0;           // 优先级 (0-10)
    public bool RequiresAck { get; set; } = false;   // 是否需要确认
    public bool PersistOffline { get; set; } = true; // 离线时是否持久化
    public TimeSpan? Timeout { get; set; }           // 超时时间
}
```

#### 4.4.4 连接质量报告

```csharp
public class QualityReport
{
    public ConnectionQuality Quality { get; set; }  // Excellent/Good/Fair/Poor
    public double AverageLatencyMs { get; set; }    // 平均延迟
    public double SuccessRate { get; set; }         // 成功率
    public int SampleCount { get; set; }            // 采样数量
}
```

### 4.5 系统托盘

#### 4.5.1 托盘功能

- 显示主窗口
- 查看连接状态
- 退出应用（需要密码验证）

#### 4.5.2 托盘图标状态

| 状态 | 图标颜色 |
|------|----------|
| 已连接 | 绿色 |
| 连接中 | 黄色 |
| 断开连接 | 红色 |

### 4.6 自动更新

#### 4.6.1 更新流程

```
┌──────────┐     ┌──────────┐     ┌──────────┐     ┌──────────┐
│ 检查更新  │────▶│ 发现新版本│────▶│ 下载更新  │────▶│ 安装更新  │
└──────────┘     └──────────┘     └──────────┘     └──────────┘
```

#### 4.6.2 更新包格式

```
Nexus-{version}-win-x64.exe
```

---

## 五、API 接口文档

### 5.1 基础 URL

| 环境 | URL |
|------|-----|
| 生产环境 | `https://api.hqzx.me` |
| 开发环境 | `http://localhost:5000` |

### 5.2 认证方式

所有需要认证的接口使用 Bearer Token：

```
Authorization: Bearer {access_token}
```

### 5.3 设备绑定接口

#### 生成绑定 Token

```
GET /desktop/bind/token
```

**请求参数**：

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| device_id | string | 是 | 设备唯一标识 |
| device_name | string | 否 | 设备名称 |
| device_type | string | 否 | 设备类型，默认 classroom_terminal |
| app_version | string | 否 | 应用版本 |
| mac_address | string | 否 | MAC 地址 |
| ip_address | string | 否 | IP 地址 |

**响应示例**：

```json
{
  "code": 200,
  "msg": "success",
  "data": {
    "token": "abc123..."
  }
}
```

#### 验证设备

```
GET /desktop/device/verify
```

**请求参数**：

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| device_id | string | 是 | 设备唯一标识 |
| device_type | string | 否 | 设备类型 |
| app_version | string | 否 | 应用版本 |

**响应示例**：

```json
{
  "code": 200,
  "msg": "success",
  "data": {
    "bound": true,
    "class_id": 1,
    "class_name": "初三(5)班",
    "access_token": "eyJ...",
    "token_expires_at": "2026-07-12T00:00:00Z"
  }
}
```

### 5.4 考勤接口

#### 获取当前考勤数据

```
GET /desktop/attendance/current
Authorization: Bearer {token}
```

**请求参数**：

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| class_id | int | 是 | 班级 ID |
| date | string | 否 | 查询日期，默认今天 |
| time_slot_id | int | 否 | 时段 ID |

**响应示例**：

```json
{
  "code": 200,
  "msg": "success",
  "data": {
    "currentTimeSlot": {
      "id": 1,
      "name": "午托",
      "startTime": "12:00:00",
      "endTime": "13:30:00"
    },
    "isAttendanceTime": true,
    "schedule": {
      "id": 1,
      "classId": 1,
      "className": "初三(5)班",
      "grade": "初三",
      "timeSlotId": 1,
      "timeSlotName": "午托",
      "shouldAttend": 50,
      "actualAttend": 48,
      "leaveCount": 2,
      "absentCount": 0,
      "completed": true
    },
    "message": ""
  }
}
```

#### 获取时段列表

```
GET /desktop/attendance/time-slots
```

**响应示例**：

```json
{
  "code": 200,
  "msg": "success",
  "data": [
    {
      "id": 1,
      "name": "午托",
      "startTime": "12:00:00",
      "endTime": "13:30:00"
    },
    {
      "id": 2,
      "name": "晚托",
      "startTime": "17:30:00",
      "endTime": "19:00:00"
    }
  ]
}
```

#### 获取考勤详情

```
GET /desktop/attendance/detail
Authorization: Bearer {token}
```

**请求参数**：

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| schedule_id | int | 是 | 排班 ID |

**响应示例**：

```json
{
  "code": 200,
  "msg": "success",
  "data": {
    "scheduleId": 1,
    "classId": 1,
    "className": "初三(5)班",
    "grade": "初三",
    "shouldAttend": 50,
    "actualAttend": 48,
    "leaveCount": 2,
    "absentCount": 0,
    "leaveStudents": [
      {
        "studentId": "1",
        "studentName": "张三",
        "reason": "病假"
      }
    ],
    "absentStudents": [],
    "completed": true
  }
}
```

### 5.5 更新接口

#### 检查更新

```
GET /desktop/update/check
```

**请求参数**：

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| current_version | string | 是 | 当前版本号 |

**响应示例**：

```json
{
  "code": 200,
  "msg": "success",
  "data": {
    "hasUpdate": true,
    "latestVersion": "3.1.0",
    "downloadUrl": "https://api.hqzx.me/downloads/Nexus-3.1.0-win-x64.exe",
    "releaseNotes": "1. 新增功能A\n2. 修复问题B",
    "forceUpdate": false
  }
}
```

---

## 六、WebSocket 通信协议

### 6.1 连接建立

**连接 URL**：

```
wss://api.hqzx.me/socket.io/?token={token}&device_id={device_id}&device_type={device_type}
```

**命名空间**：`/desktop`

### 6.2 事件类型

#### 客户端发送事件

| 事件 | 说明 | 数据格式 |
|------|------|----------|
| `ping` | 心跳 | `{ time: timestamp }` |
| `ack` | 消息确认 | `{ message_id: string }` |

#### 服务端推送事件

| 事件 | 说明 | 数据格式 |
|------|------|----------|
| `connect_response` | 连接响应 | `{ status: "ok" }` |
| `bind_notification` | 绑定通知 | `{ type, class_id, class_name }` |
| `attendance_update` | 考勤更新 | `{ classId, scheduleId, timeSlotId }` |
| `power_control` | 电源控制 | `{ action: "shutdown/restart" }` |
| `disaster_warning` | 灾害预警 | `{ type, title, content, ... }` |
| `page_call` | 寻人传呼 | `{ studentName, message }` |
| `notification` | 通用通知 | `{ title, content, type }` |

### 6.3 消息格式

```json
{
  "type": "event_name",
  "data": { ... },
  "seq": 12345,
  "timestamp": 1712880000000,
  "_message_id": "msg_xxx"
}
```

### 6.4 消息确认机制

1. 发送方发送消息，携带 `_message_id`
2. 接收方收到消息后，发送 `ack` 事件确认
3. 发送方超时未收到确认，自动重发

---

## 七、插件系统

### 7.1 插件架构

```
┌─────────────────────────────────────────────────────────────────────────┐
│                            插件系统架构                                  │
├─────────────────────────────────────────────────────────────────────────┤
│                                                                         │
│  ┌─────────────────────────────────────────────────────────────────┐   │
│  │                        PluginHost                                │   │
│  │  - 插件生命周期管理                                              │   │
│  │  - 依赖注入容器                                                  │   │
│  │  - 事件总线                                                      │   │
│  └─────────────────────────────────────────────────────────────────┘   │
│                                    │                                    │
│         ┌──────────────────────────┼──────────────────────────┐        │
│         ▼                          ▼                          ▼        │
│  ┌─────────────┐          ┌─────────────┐          ┌─────────────┐    │
│  │PluginService│          │PluginUISvc  │          │WebSocketBrg │    │
│  │             │          │             │          │             │    │
│  │ - 插件加载  │          │ - UI 扩展   │          │ - WebSocket │    │
│  │ - 配置管理  │          │ - 菜单注册  │          │   桥接      │    │
│  │ - API 代理  │          │ - 视图注册  │          │             │    │
│  └─────────────┘          └─────────────┘          └─────────────┘    │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
```

### 7.2 插件接口

```csharp
public interface IPlugin
{
    string Id { get; }           // 插件唯一标识
    string Name { get; }         // 插件名称
    string Version { get; }      // 插件版本
    string Description { get; }  // 插件描述
    string Author { get; }       // 作者
    string[] Dependencies { get; } // 依赖的其他插件
    
    void Initialize(IPluginContext context, IServiceCollection services);
    void OnStartup(IServiceProvider serviceProvider);
    void OnShutdown();
    string ConfigFolder { get; }
}
```

### 7.3 插件清单文件

```json
{
  "id": "example.plugin",
  "name": "示例插件",
  "version": "1.0.0",
  "description": "这是一个示例插件",
  "author": "开发者",
  "entryPoint": "ExamplePlugin.Plugin",
  "dependencies": [],
  "permissions": [
    "network",
    "filesystem"
  ]
}
```

### 7.4 插件开发示例

```csharp
public class ExamplePlugin : PluginBase
{
    public override string Id => "example.plugin";
    public override string Name => "示例插件";
    public override string Version => "1.0.0";
    
    public override void Initialize(IPluginContext context, IServiceCollection services)
    {
        // 注册服务
        services.AddSingleton<IExampleService, ExampleService>();
    }
    
    public override void OnStartup(IServiceProvider serviceProvider)
    {
        // 插件启动逻辑
        var service = serviceProvider.GetRequiredService<IExampleService>();
        service.Start();
    }
    
    public override void OnShutdown()
    {
        // 插件关闭逻辑
    }
}
```

---

## 八、部署与运维

### 8.1 环境要求

| 项目 | 要求 |
|------|------|
| 操作系统 | Windows 10/11 (x64) |
| 运行时 | .NET 8.0 Runtime |
| 网络 | 能够访问 `https://api.hqzx.me` |

### 8.2 安装方式

#### 方式一：安装包安装

1. 下载 `Nexus-{version}-win-x64.exe`
2. 双击运行安装程序
3. 按提示完成安装

#### 方式二：便携版

1. 下载 `Nexus-{version}-win-x64.zip`
2. 解压到任意目录
3. 运行 `Nexus.exe`

### 8.3 开机自启

应用会自动注册开机自启：

- 注册表路径：`HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`
- 键名：`Nexus`

### 8.4 配置文件

**位置**：`%LocalAppData%\Nexus\config.json`

```json
{
  "deviceId": "device_xxx",
  "deviceName": "教室终端-1",
  "deviceType": "classroom_terminal",
  "macAddress": "00:11:22:33:44:55",
  "accessToken": "eyJ...",
  "tokenExpiresAt": "2026-07-12T00:00:00Z",
  "bindInfo": {
    "classId": 1,
    "className": "初三(5)班",
    "bindTime": "2026-01-01T00:00:00Z"
  },
  "serverUrl": "https://api.hqzx.me",
  "widgetConfig": {
    "isEnabled": true,
    "opacity": 0.9,
    "position": "BottomRight"
  }
}
```

### 8.5 日志查看

应用使用 `System.Diagnostics.Debug` 输出日志，可通过以下方式查看：

1. 使用 DebugView 工具
2. Visual Studio 调试输出窗口

### 8.6 远程开机 (WOL)

应用支持 Wake-on-LAN 远程开机功能：

1. 自动检测并保存本机 MAC 地址
2. 后端可通过 MAC 地址发送唤醒包

---

## 九、用户操作指南

### 9.1 首次使用

#### 步骤一：启动应用

双击桌面快捷方式或开始菜单中的 Nexus 图标启动应用。

#### 步骤二：设备绑定

1. 应用启动后显示欢迎页面
2. 点击"开始绑定"按钮
3. 使用微信小程序扫描屏幕上的二维码
4. 在小程序中选择要绑定的班级
5. 等待绑定成功

#### 步骤三：排班设置（可选）

如果班级没有排班数据，会提示进行排班设置：

1. 选择排班时段（午托/晚托）
2. 设置值班教师
3. 确认保存

### 9.2 主界面操作

#### 概览页面

- 查看设备状态
- 查看当前考勤信息
- 查看系统通知

#### 设置页面

- 设备解绑
- 小组件设置
- 天气位置设置
- 系统设置

#### 更新页面

- 检查更新
- 下载并安装更新

### 9.3 小组件操作

#### 显示/隐藏小组件

- 通过托盘菜单切换
- 通过设置页面控制

#### 查看考勤详情

点击小组件上的"查看详情"按钮，显示完整考勤信息。

### 9.4 灾害预警响应

当收到灾害预警时：

1. 全屏预警窗口自动弹出
2. 自动播放警报声音
3. 自动播报预警内容
4. 根据预警类型采取相应措施
5. 点击"我已知晓"关闭预警窗口

### 9.5 退出应用

1. 右键点击托盘图标
2. 选择"退出"
3. 输入管理员密码确认退出

---

## 十、故障排除

### 10.1 常见问题

#### Q1：无法连接服务器

**可能原因**：
- 网络连接问题
- 服务器维护中
- 防火墙阻止连接

**解决方案**：
1. 检查网络连接
2. 检查防火墙设置
3. 联系管理员确认服务器状态

#### Q2：设备绑定失败

**可能原因**：
- 二维码已过期
- 微信小程序未登录
- 设备已被其他班级绑定

**解决方案**：
1. 重新生成二维码
2. 确保微信小程序已登录
3. 联系管理员解绑设备

#### Q3：考勤数据不显示

**可能原因**：
- 当前非考勤时段
- 班级无排班数据
- 网络连接问题

**解决方案**：
1. 确认当前是否为考勤时段
2. 检查排班设置
3. 刷新数据

#### Q4：小组件不显示

**可能原因**：
- 小组件功能已禁用
- 当前非考勤时段

**解决方案**：
1. 检查设置中的小组件开关
2. 确认是否为考勤时段

#### Q5：更新失败

**可能原因**：
- 网络下载失败
- 权限不足

**解决方案**：
1. 检查网络连接
2. 以管理员身份运行应用

### 10.2 错误代码

| 代码 | 说明 | 解决方案 |
|------|------|----------|
| 401 | Token 无效或已过期 | 重新绑定设备 |
| 403 | 权限不足 | 联系管理员 |
| 404 | 资源不存在 | 检查请求参数 |
| 500 | 服务器内部错误 | 联系管理员 |

---

## 十一、开发指南

### 11.1 开发环境搭建

#### 安装依赖

1. 安装 .NET 8.0 SDK
2. 安装 Visual Studio 2022 或 Rider
3. 安装 Avalonia VS 扩展

#### 克隆项目

```bash
git clone https://github.com/chenxingpengs/Nexus.git
cd Nexus
```

#### 还原依赖

```bash
dotnet restore
```

### 11.2 项目构建

#### Debug 构建

```bash
dotnet build
```

#### Release 构建

```bash
dotnet build -c Release
```

### 11.3 发布

#### 发布单文件版本

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

#### 发布便携版

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

### 11.4 调试技巧

#### 查看 Debug 输出

使用 DebugView 工具查看应用日志：

```bash
# 下载 DebugView
https://learn.microsoft.com/en-us/sysinternals/downloads/debugview
```

#### 断点调试

在 Visual Studio 中设置断点，按 F5 启动调试。

### 11.5 代码规范

#### 命名规范

| 类型 | 命名风格 | 示例 |
|------|----------|------|
| 类 | PascalCase | `AttendanceService` |
| 方法 | PascalCase | `GetCurrentAttendance` |
| 属性 | PascalCase | `IsConnected` |
| 字段 | _camelCase | `_currentClassId` |
| 参数 | camelCase | `classId` |
| 常量 | UPPER_CASE | `MAX_RETRY_COUNT` |

#### 文件组织

```csharp
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
// 其他 using

namespace Nexus.Services
{
    public class ExampleService
    {
        // 常量
        private const int MAX_RETRY = 3;
        
        // 字段
        private readonly ConfigService _configService;
        
        // 属性
        public bool IsRunning { get; private set; }
        
        // 构造函数
        public ExampleService(ConfigService configService)
        {
            _configService = configService;
        }
        
        // 公共方法
        public async Task DoSomethingAsync()
        {
            // 实现
        }
        
        // 私有方法
        private void HelperMethod()
        {
            // 实现
        }
    }
}
```

---

## 附录

### A. 版本历史

| 版本 | 日期 | 更新内容 |
|------|------|----------|
| 3.0.0 | 2026-04 | 重构插件系统，优化 WebSocket 连接 |
| 2.0.0 | 2026-01 | 新增灾害预警功能 |
| 1.0.0 | 2025-09 | 初始版本 |

### B. 相关链接

- [Avalonia UI 官方文档](https://docs.avaloniaui.net/)
- [.NET 8.0 文档](https://learn.microsoft.com/dotnet/)
- [SocketIOClient 库](https://github.com/doghappy/socket.io-client-csharp)

### C. 联系方式

**技术支持**：陈星鹏

---

<div align="center">

**© 2026 红旗中学智慧校园系统**

</div>

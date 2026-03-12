# Nexus 应用架构重构计划

## 概述

将 Nexus 从单一的绑定界面重构为完整的桌面应用，包含：
- 配置持久化（加密存储Token）
- 启动流程优化（检查绑定状态）
- 主界面（左侧导航 + 右侧内容区）
- 系统托盘
- 关于页面

---

## 一、配置存储方案

### 1.1 配置文件结构

存储位置：`%LocalAppData%\Nexus\config.json`

```json
{
  "deviceId": "device_xxxxxxxxxxxxxxxx",
  "deviceName": "PC-NAME",
  "token": "encrypted_token_string",
  "bindInfo": {
    "className": "高一(1)班",
    "bindTime": "2024-01-15T10:30:00Z"
  },
  "serverUrl": "https://api.hqzx.me"
}
```

### 1.2 安全措施

- 使用 Windows DPAPI 加密 Token
- 配置文件存储在用户目录，权限受限
- Token 绑定设备ID，双重验证

### 1.3 新增服务

创建 `ConfigService.cs`：
- `LoadConfig()` - 加载配置
- `SaveConfig()` - 保存配置
- `EncryptToken()` / `DecryptToken()` - DPAPI 加解密
- `ClearConfig()` - 清除配置（解绑时使用）

---

## 二、启动流程

### 2.1 新启动流程

```
应用启动
    │
    ▼
检查配置文件是否存在
    │
    ├── 存在 ──▶ 读取Token ──▶ 验证Token有效性
    │                              │
    │                              ├── 有效 ──▶ 进入主界面
    │                              │
    │                              └── 无效 ──▶ 显示绑定界面
    │
    └── 不存在 ──▶ 显示启动页（点击后进入绑定界面）
```

### 2.2 Token验证接口

需要后端提供验证接口（如不存在需新增）：
- `GET /desktop/bind/verify?device_id={deviceId}&token={token}`
- 返回：绑定状态、班级信息等

### 2.3 修改文件

- `App.axaml.cs` - 修改启动逻辑
- 新增 `AuthService.cs` - 处理认证逻辑

---

## 三、主界面设计

### 3.1 界面布局

使用 FluentAvaloniaUI 的 `NavigationView` 组件作为主框架：

```
┌─────────────────────────────────────────────────────────┐
│  Nexus                              [─] [□] [×]         │
├──────────────┬──────────────────────────────────────────┤
│              │                                          │
│   📊 概览    │           主内容区                        │
│              │     (Frame 控件切换页面)                  │
│   📅 考勤    │                                          │
│              │    ┌─────────────────────────────────┐   │
│   ⚙️ 设置    │    │                                 │   │
│              │    │         Page Content            │   │
│   ℹ️ 关于    │    │                                 │   │
│              │    └─────────────────────────────────┘   │
│              │                                          │
│              │    ┌─────────────────────────────────┐   │
│              │    │ 设备状态 │ 考勤概览 │ 通知公告  │   │
│              │    └─────────────────────────────────┘   │
├──────────────┴──────────────────────────────────────────┤
│  状态栏: 连接状态 | 设备ID: device_xxx                   │
└─────────────────────────────────────────────────────────┘
```

**使用的 FluentAvaloniaUI 组件**：
- `NavigationView` - 左侧导航栏
- `NavigationViewItem` - 导航项
- `Frame` - 页面导航容器
- `InfoBar` - 通知提示
- `CardExpander` / `SettingsExpander` - 设置项
- `ProgressRing` - 加载动画

### 3.2 导航菜单

使用 FluentAvaloniaUI 自带的 `NavigationView` 组件和 `SymbolIcon` 图标：

| 菜单项 | SymbolIcon | 页面 |
|--------|------------|------|
| 概览 | ViewDashboard | DashboardPage |
| 考勤 | Calendar | AttendancePage |
| 设置 | Setting | SettingsPage |
| 关于 | Info | AboutPage |

**FluentAvaloniaUI 常用图标**：
- ViewDashboard, Home, Calendar, Setting, Info, People, Document, Mail, Alert, Check, etc.

### 3.3 右下角信息面板

显示内容：
- **设备状态**：设备名称、绑定班级、运行时间
- **考勤概览**：今日签到人数、异常数等
- **通知公告**：系统通知列表

### 3.4 新增文件

**视图**：
- `Views/MainView.axaml` - 主界面容器
- `Views/Pages/DashboardPage.axaml` - 概览页
- `Views/Pages/AttendancePage.axaml` - 考勤页
- `Views/Pages/SettingsPage.axaml` - 设置页
- `Views/Pages/AboutPage.axaml` - 关于页

**视图模型**：
- `ViewModels/MainViewModel.cs` - 主界面VM
- `ViewModels/Pages/DashboardViewModel.cs`
- `ViewModels/Pages/AttendanceViewModel.cs`
- `ViewModels/Pages/SettingsViewModel.cs`
- `ViewModels/Pages/AboutViewModel.cs`

---

## 四、系统托盘

### 4.1 功能

- 应用最小化到托盘
- 托盘图标显示应用状态
- 右键菜单：
  - 显示主窗口
  - 考勤状态
  - 退出

### 4.2 技术方案

使用 `Avalonia.Controls.SystemTrayIcon` 或第三方库 `TrayIcon.Avalonia`

### 4.3 新增文件

- `Services/TrayService.cs` - 托盘管理服务

---

## 五、关于页面

### 5.1 内容

使用 FluentAvaloniaUI 组件构建：

```
┌────────────────────────────────────┐
│         (SymbolIcon: Info)         │
│                                    │
│            Nexus                   │
│      红旗中学智慧校园系统           │
│                                    │
│         版本 1.0.0                 │
│                                    │
│  ────────────────────────────────  │
│                                    │
│  技术支持：珠海市红旗中学信息中心    │
│  官网：https://hqzx.me             │
│                                    │
│  [HyperlinkButton: 检查更新]       │
│  [HyperlinkButton: 查看日志]       │
│                                    │
└────────────────────────────────────┘
```

**使用的组件**：
- `SymbolIcon` - 应用图标
- `TextBlock` - 文字显示
- `HyperlinkButton` - 链接按钮
- `CardExpander` - 可展开的信息卡片

---

## 六、实施步骤

### 阶段一：基础架构（优先）

1. 创建 `ConfigService.cs` - 配置存储服务
2. 创建 `AuthService.cs` - 认证服务
3. 修改启动流程 - 检查配置并决定显示哪个界面

### 阶段二：主界面重构

4. 创建主界面布局 `MainView.axaml`
5. 创建导航系统
6. 创建各页面（先实现关于页面）

### 阶段三：系统托盘

7. 创建 `TrayService.cs`
8. 实现最小化到托盘
9. 实现托盘菜单

### 阶段四：完善功能

10. 实现概览页数据展示
11. 实现设置页（解绑功能）
12. 完善错误处理

---

## 七、文件变更清单

### 新增文件

```
Services/
├── ConfigService.cs       # 配置存储服务
├── AuthService.cs         # 认证服务
└── TrayService.cs         # 系统托盘服务

Views/
├── MainView.axaml         # 主界面
├── MainView.axaml.cs
└── Pages/
    ├── DashboardPage.axaml
    ├── DashboardPage.axaml.cs
    ├── AttendancePage.axaml
    ├── AttendancePage.axaml.cs
    ├── SettingsPage.axaml
    ├── SettingsPage.axaml.cs
    ├── AboutPage.axaml
    └── AboutPage.axaml.cs

ViewModels/
├── MainViewModel.cs
└── Pages/
    ├── DashboardViewModel.cs
    ├── AttendanceViewModel.cs
    ├── SettingsViewModel.cs
    └── AboutViewModel.cs

Models/
└── AppConfig.cs           # 配置模型
```

### 修改文件

```
App.axaml.cs               # 修改启动流程
MainWindowViewModel.cs     # 绑定成功后保存配置
Nexus.csproj              # 添加必要依赖
```

---

## 八、依赖项

项目已包含 `FluentAvaloniaUI`，无需额外添加 NuGet 包。

系统托盘可使用：
- Avalonia 内置的 `TrayIcon` 类（推荐）
- 或 `TrayIcon.Avalonia` 第三方库

```xml
<!-- 如需第三方托盘库 -->
<PackageReference Include="TrayIcon.Avalonia" Version="1.2.0" />
```

---

## 九、后端 API 修改

### 9.1 现状分析

当前后端绑定流程：
- 临时Token有效期仅 **5分钟**
- 绑定成功后 **没有返回长期Token**
- devices表存储了 device_id、class_id、bind_user_id，但无长期Token字段

### 9.2 需要新增的接口

#### 9.2.1 设备Token验证接口

**路由**: `GET /desktop/device/verify`

**功能**: 验证设备是否已绑定，返回设备信息

**请求参数**:
```
device_id: 设备唯一标识
```

**响应**:
```json
{
  "code": 200,
  "msg": "success",
  "data": {
    "bound": true,
    "class_id": 1,
    "class_name": "高一(1)班",
    "bind_time": "2024-01-15T10:30:00Z"
  }
}
```

#### 9.2.2 设备Token获取接口（可选）

**路由**: `POST /desktop/device/auth`

**功能**: 设备使用 device_id 获取访问Token

**请求体**:
```json
{
  "device_id": "device_xxxxxxxxxxxxxxxx"
}
```

**响应**:
```json
{
  "code": 200,
  "msg": "success",
  "data": {
    "token": "长期访问Token",
    "expires_at": "2024-02-15T10:30:00Z"
  }
}
```

### 9.3 数据库修改

#### devices 表新增字段

```sql
ALTER TABLE devices ADD COLUMN access_token VARCHAR(255);
ALTER TABLE devices ADD COLUMN token_expires_at DATETIME;
ALTER TABLE devices ADD COLUMN last_active_at DATETIME;
```

### 9.4 新增后端文件

```
desktop/
└── device.py              # 设备认证接口
```

### 9.5 修改现有文件

```
app/attendance/bind.py     # 绑定成功后生成长期Token
utils/auth_utils.py        # 新增设备Token验证函数
```

### 9.6 后端实现步骤

1. **新增 `desktop/device.py`**：
   - `/device/verify` - 验证设备绑定状态
   - `/device/auth` - 获取设备访问Token

2. **修改 `app/attendance/bind.py`**：
   - 绑定成功后生成长期Token
   - 存储到 devices 表

3. **修改 `utils/auth_utils.py`**：
   - 新增 `device_token_required` 装饰器
   - 支持设备Token验证

---

## 十、注意事项

1. **DPAPI**：仅 Windows 平台支持，跨平台需要其他方案
2. **托盘图标**：需要准备 .ico 格式的图标文件
3. **Token有效期**：建议后端设置30天有效期，支持自动续期
4. **安全性**：设备Token存储加密，后端验证时检查设备ID匹配

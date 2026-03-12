# PC端自动更新功能实现计划

## 一、需求概述

为 Nexus（Avalonia UI 桌面应用）实现完整的自动更新功能，包括：

* 独立的"系统更新"菜单页面

* 启动时自动检查更新

* 定时后台检查更新

* 手动检查更新

* 全量下载 + 静默安装

***

## 二、技术方案

### 2.1 更新源：GitHub Releases

使用 GitHub Releases 作为更新源，无需自建后端接口。

**GitHub API 调用：**

```
GET https://api.github.com/repos/{owner}/{repo}/releases/latest
```

**响应格式（简化版）：**

```json
{
  "tag_name": "v1.1.0",
  "name": "Nexus v1.1.0",
  "body": "## 更新内容\n- 修复若干问题\n- 新增自动更新功能",
  "published_at": "2026-03-12T10:00:00Z",
  "assets": [
    {
      "name": "Nexus-1.1.0-win-x64.exe",
      "browser_download_url": "https://github.com/xxx/releases/download/v1.1.0/Nexus-1.1.0-win-x64.exe",
      "size": 52428800,
      "content_type": "application/octet-stream"
    }
  ]
}
```

**配置项（ConfigService）：**

```csharp
public class UpdateConfig
{
    public string GitHubOwner { get; set; } = "your-github-username";
    public string GitHubRepo { get; set; } = "Nexus";
    public bool AutoCheckOnStartup { get; set; } = true;
    public int CheckIntervalHours { get; set; } = 4;
}
```

### 2.2 前端架构设计

```
新增文件：
├── Services/
│   └── UpdateService.cs           # 更新服务（核心逻辑）
├── ViewModels/Pages/
│   └── UpdateViewModel.cs         # 更新页面 ViewModel
├── Views/Pages/
│   └── UpdatePage.axaml           # 更新页面 UI
└── Models/
    └── UpdateInfo.cs              # 更新信息模型

修改文件：
├── ViewModels/MainViewModel.cs    # 添加菜单项 + 启动检查
├── Views/MainView.axaml           # （可能需要调整）
├── App.axaml.cs                   # 版本号管理优化
└── Services/ConfigService.cs      # 添加更新配置项
```

***

## 三、实现步骤

### 步骤 1：创建数据模型 (Models/UpdateInfo.cs)

定义更新信息的数据结构：

* `UpdateInfo` - 版本检查响应

* `UpdateProgress` - 下载进度状态

* `UpdateStatus` - 更新状态枚举

### 步骤 2：创建更新服务 (Services/UpdateService.cs)

核心功能：

* `CheckForUpdateAsync()` - 检查更新

* `DownloadUpdateAsync()` - 下载更新包（带进度回调）

* `InstallUpdate()` - 静默安装更新

* `GetCurrentVersion()` - 获取当前版本

* `CompareVersions()` - 版本比较

依赖：

* `HttpClient` - HTTP 请求

* `ConfigService` - 获取服务器地址

* 文件校验（SHA256）

### 步骤 3：创建更新页面 ViewModel (ViewModels/Pages/UpdateViewModel.cs)

功能：

* 当前版本显示

* 检查更新按钮

* 更新状态显示（检查中/有新版本/已是最新/下载中/准备安装）

* 下载进度条

* 更新说明显示

* 立即更新按钮

### 步骤 4：创建更新页面 UI (Views/Pages/UpdatePage.axaml)

UI 元素：

* 当前版本卡片

* 更新状态区域

* 下载进度条

* 更新说明区域

* 操作按钮区

### 步骤 5：修改 MainViewModel 添加菜单项

* 在 `NavigationItems` 中添加"系统更新"菜单项

* 在 `NavigateToPage` 中添加路由

* 添加启动时自动检查更新逻辑

* 添加定时后台检查更新逻辑（每4小时）

### 步骤 6：修改 App.axaml.cs

* 统一版本号管理

* 添加版本常量

### 步骤 7：修改 ConfigService

* 添加更新检查间隔配置

* 添加上次检查时间记录

### 步骤 8：实现静默安装逻辑

Windows 平台静默安装方案：

* NSIS 安装包：`/S` 参数

* Inno Setup：`/VERYSILENT /SUPPRESSMSGBOXES` 参数

* MSI：`/quiet /norestart` 参数

***

## 四、更新流程图

```
应用启动
    │
    ▼
检查是否需要检查更新（距上次检查超过间隔？）
    │
    ├── 否 → 跳过
    │
    └── 是 → 调用版本检查 API
              │
              ├── 无新版本 → 记录检查时间，继续启动
              │
              └── 有新版本 → 显示更新提示
                              │
                              ├── 用户点击更新 → 下载 → 验证 → 静默安装 → 重启
                              │
                              └── 用户稍后更新 → 记录检查时间，继续启动
```

***

## 五、定时检查逻辑

```
启动后延迟 5 分钟开始第一次定时检查
之后每隔 4 小时检查一次
检查到更新时弹出通知提示
```

***

## 六、文件清单

| 操作 | 文件路径                                | 说明            |
| -- | ----------------------------------- | ------------- |
| 新建 | Models/UpdateInfo.cs                | 更新信息模型        |
| 新建 | Services/UpdateService.cs           | 更新服务          |
| 新建 | ViewModels/Pages/UpdateViewModel.cs | 更新页 ViewModel |
| 新建 | Views/Pages/UpdatePage.axaml        | 更新页 UI        |
| 修改 | ViewModels/MainViewModel.cs         | 添加菜单和启动检查     |
| 修改 | App.axaml.cs                        | 版本号管理         |
| 修改 | Services/ConfigService.cs           | 更新配置项         |

***

## 七、已确认事项

| 事项    | 确认结果                |
| ----- | ------------------- |
| 更新方式  | 全量更新                |
| 检查时机  | 启动检查 + 定时检查 + 手动检查  |
| 更新源   | GitHub Releases     |
| 安装方式  | 静默安装                |
| 安装包工具 | 待确定（后续根据实际工具调整静默参数） |

***

## 八、风险点

1. **静默安装权限**：可能需要管理员权限
2. **杀毒软件拦截**：静默安装可能被安全软件拦截
3. **网络异常**：下载中断需要支持断点续传或重新下载
4. **版本回退**：需要考虑安装失败的情况

***

## 九、后续优化（可选）

* [ ] 断点续传支持

* [ ] 增量更新支持

* [ ] 多平台支持（macOS/Linux）

* [ ] 更新日志富文本显示


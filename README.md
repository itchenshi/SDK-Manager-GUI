# SDK Manager GUI

[简体中文](README.md) | [繁體中文](README.zh-TW.md) | [English](README.en.md)

![MIT License](https://img.shields.io/badge/license-MIT-blue.svg) ![Version](https://img.shields.io/badge/version-1.0.0.0-green.svg) ![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-purple.svg) ![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)

## 简介

SDK Manager GUI 是一款基于 WPF 的多语言 SDK 桌面管理工具，支持 Node.js、Java、Python 和 Maven 的下载、安装、版本切换与环境变量配置。通过统一的图形界面，开发者可以高效管理多种开发工具链的版本，免去手动下载、解压、配置环境变量的繁琐流程。内置多镜像源智能调度、多版本共存切换、自动环境变量配置等特性，特别适合需要在多版本之间频繁切换的开发场景。

- 版本：1.0.0.0
- 协议：MIT License
- 版权：Copyright © 2026

## 功能特性

1. **多 SDK 统一管理** — 在一个界面中管理 Node.js、Java、Python 和 Maven 四种 SDK
2. **多镜像源智能下载** — 内置官方源和国内镜像源，自动按连接效率排序（优先级 → 延迟 → 成功率 → 失败次数），故障自动转移
3. **环境变量自动配置** — 安装后自动配置 PATH 和语言专属环境变量（NODE_HOME / JAVA_HOME / PYTHON_HOME / MAVEN_HOME），支持用户级和系统级
4. **多版本共存与切换** — 每个版本独立目录安装（`{basePath}/{SDK名称}/{version}/`），支持快速切换激活版本
5. **下载任务管理** — 支持多任务并行、暂停 / 恢复 / 取消、自动重试，实时显示下载进度和速度
6. **镜像源管理** — 支持添加 / 删除 / 编辑镜像源、延迟测试、优先级排序，Python / Node.js 支持包管理器镜像源配置，Maven 支持依赖镜像源和本地仓库配置
7. **多语言界面** — 支持简体中文、繁體中文、English 三种界面语言，首次启动自动检测系统显示语言，运行时可动态切换
8. **仪表盘与日志** — Dashboard 一览所有 SDK 安装状态，内置日志查看器，支持自动清理过期日志

## 支持的 SDK

| SDK | 来源 | 版本分类 | 环境变量 | 镜像源支持 |
|-----|------|----------|----------|------------|
| **Node.js** | nodejs.org / 淘宝镜像 | LTS / Current | NODE_HOME、PATH | npm 镜像源配置 |
| **Java (Eclipse Temurin)** | Adoptium API | LTS 标识（JDK 8/11/17/21） | JAVA_HOME、PATH | — |
| **Python** | python.org / 镜像 | 嵌入式版本 | PYTHON_HOME、PATH | pip 镜像源配置（get-pip.py 自动配置） |
| **Maven** | Apache 官方 / 镜像 | — | MAVEN_HOME、PATH | 依赖镜像源（settings.xml）、本地仓库（localRepository） |

> Java 版本名称会自动去除 `+N` 构建后缀和 `.LTS` 标签，便于版本识别与切换。

## 截图

<!-- 截图 -->

> 截图将在后续版本补充。如有需要，可先从 [Releases](../../releases) 页面下载体验版查看实际界面。

## 技术栈

| 类别 | 选型 | 版本 |
|------|------|------|
| 运行框架 | .NET Framework | 4.8 |
| UI 框架 | WPF (Windows Presentation Foundation) | - |
| 架构模式 | MVVM (Model-View-ViewModel) | - |
| 依赖注入 | Microsoft.Extensions.DependencyInjection | 8.0.1 |
| JSON 序列化 | Newtonsoft.Json | 13.0.3 |
| 日志框架 | Serilog + Serilog.Sinks.File | 3.1.1 |
| 数据访问 | Dapper + System.Data.SQLite | 2.1.35 / 1.0.118 |
| 导航模式 | ViewModel-First 自定义导航 | - |
| 消息通信 | 自定义 WeakMessenger（弱引用） | - |

## 系统要求

- 操作系统：Windows 10（版本 1803+）/ Windows 11
- 运行时：.NET Framework 4.8
- 权限：普通用户权限可运行（系统级环境变量配置需管理员权限）

## 快速开始

### 方式一：下载 Release

1. 前往 [Releases](../../releases) 页面下载最新版本的 `SDK-Manager-GUI.zip`
2. 解压到任意目录
3. 运行 `SDK-Manager-GUI.exe`

### 方式二：从源码编译

1. 确保已安装 Visual Studio 2022（或更高版本）和 .NET Framework 4.8 开发工具包
2. 克隆仓库：

   ```bash
   git clone <仓库地址>
   ```

3. 使用 Visual Studio 打开 `SDK-Manager-GUI.slnx` 解决方案文件
4. 还原 NuGet 包
5. 编译项目（Debug 或 Release 配置）
6. 运行或从 `bin\Debug\` 或 `bin\Release\` 目录启动 `SDK-Manager-GUI.exe`

也可使用 MSBuild 命令行编译：

```bash
msbuild SDK-Manager-GUI\SDK-Manager-GUI.csproj /p:Configuration=Release
```

## 项目结构

```
SDK-Manager-GUI/
├── SDK-Manager-GUI/              # 主项目
│   ├── Models/                   # 数据模型
│   │   ├── Entities/             #   实体类 (AppConfig, Sdk, InstalledSdk...)
│   │   ├── Enums/                #   枚举 (SdkLanguage, DownloadStatus...)
│   │   ├── Messages/             #   Messenger 消息定义
│   │   └── DTOs/                 #   ViewModel 展示模型
│   ├── Services/                 # 服务层
│   │   ├── Interfaces/           #   接口定义 (ISdkProvider, IDownloadEngine...)
│   │   ├── Providers/            #   SDK 提供者 (NodeJs/Java/Python)
│   │   ├── SdkManagerService.cs  #   核心业务编排
│   │   ├── DownloadEngine.cs     #   下载引擎
│   │   ├── EnvironmentManager.cs #   环境变量管理
│   │   ├── MirrorProvider.cs     #   镜像源管理
│   │   ├── MavenService.cs       #   Maven 管理服务
│   │   ├── LanguageService.cs    #   多语言服务
│   │   ├── ConfigService.cs      #   配置管理
│   │   └── ...
│   ├── ViewModels/               # 视图模型 (MVVM)
│   ├── Views/                    # 视图 (XAML)
│   ├── Converters/               # 值转换器
│   ├── Languages/                # 多语言资源文件
│   │   ├── zh-CN.xaml            #   简体中文
│   │   ├── zh-TW.xaml            #   繁體中文
│   │   └── en.xaml               #   English
│   ├── App.xaml                  # 应用入口 + DataTemplate 注册
│   ├── App.xaml.cs               # DI 容器配置、服务注册
│   └── MainWindow.xaml           # 主窗口（无边框自定义窗口）
├── docs/                         # 项目文档
│   └── requirements.md           #   需求分析文档
└── SDK-Manager-GUI.slnx          # 解决方案文件
```

## 配置说明

应用配置存储在 `config/config.json` 文件中，包含以下设置项：

| 配置项 | 说明 | 默认值 |
|--------|------|--------|
| DefaultInstallPath | SDK 默认安装根路径 | C:\SDK-Manager |
| MaxConcurrentDownloads | 最大并发下载数 | 3 |
| MaxRetryCount | 下载失败最大重试次数 | 3 |
| AutoCleanLogs | 是否自动清理过期日志 | true |
| LogKeepDays | 日志保留天数 | 30 |
| Language | 界面语言 (zh-CN / zh-TW / en) | 首次启动自动检测系统语言 |

镜像源配置文件：

- `config/mirrors.json` — SDK 下载镜像源列表
- `config/maven-download-mirrors.json` — Maven 下载镜像源列表

## 多语言支持

| 语言代码 | 语言 | 说明 |
|----------|------|------|
| zh-CN | 简体中文 | 默认语言（简体中文系统） |
| zh-TW | 繁體中文 | 繁体中文系统自动选择 |
| en | English | 其他语言系统默认选择 |

- 首次启动时自动检测系统显示语言并持久化偏好
- 之后可在「设置」页面随时切换，切换即时生效，无需重启
- 语言资源文件位于 `Languages/` 目录，使用 WPF ResourceDictionary + DynamicResource 实现运行时动态切换

## 开源协议

本项目基于 [MIT License](LICENSE) 开源。

Copyright © 2026

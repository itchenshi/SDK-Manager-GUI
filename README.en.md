# SDK Manager GUI

[简体中文](README.md) | [繁體中文](README.zh-TW.md) | [English](README.en.md)

!\[MIT License](https://img.shields.io/badge/license-MIT-blue.svg) !\[Version](https://img.shields.io/badge/version-1.0.0.0-green.svg) !\[.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-purple.svg) !\[Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)

## Introduction

SDK Manager GUI is a WPF-based desktop tool for managing multiple SDKs, supporting download, installation, version switching, and environment variable configuration for Node.js, Java, Python, and Maven. Through a unified graphical interface, developers can efficiently manage versions of various development toolchains, eliminating the tedious process of manually downloading, extracting, and configuring environment variables. Built-in features include smart mirror source scheduling, multi-version coexistence switching, and automatic environment variable configuration, making it especially suitable for development scenarios that require frequent switching between versions.

* Version: 1.0.0.0
* License: MIT License
* Copyright: Copyright © 2026

## Features

1. **Unified Multi-SDK Management** — Manage Node.js, Java, Python, and Maven SDKs in a single interface
2. **Smart Mirror Source Download** — Built-in official and regional mirror sources, automatically sorted by connection efficiency (priority → latency → success rate → failure count), with automatic failover
3. **Automatic Environment Variable Configuration** — Automatically configures PATH and language-specific environment variables (NODE\_HOME / JAVA\_HOME / PYTHON\_HOME / MAVEN\_HOME) after installation, supporting both user-level and system-level
4. **Multi-Version Coexistence and Switching** — Each version installed in an independent directory (`{basePath}/{SDKName}/{version}/`), with quick activation version switching
5. **Download Task Management** — Supports parallel tasks, pause / resume / cancel, automatic retry, with real-time download progress and speed display
6. **Mirror Source Management** — Supports adding / deleting / editing mirror sources, latency testing, priority sorting. Python / Node.js support package manager mirror configuration, Maven supports dependency mirror and local repository configuration
7. **Multilingual Interface** — Supports Simplified Chinese, Traditional Chinese, and English interface languages, with automatic system language detection on first launch and runtime dynamic switching
8. **Dashboard and Logging** — Dashboard provides an overview of all SDK installation status, built-in log viewer with automatic expired log cleanup

## Supported SDKs

|SDK|Source|Version Classification|Environment Variables|Mirror Source Support|
|-|-|-|-|-|
|**Node.js**|nodejs.org / Taobao Mirror|LTS / Current|NODE\_HOME, PATH|npm mirror configuration|
|**Java (Eclipse Temurin)**|Adoptium API|LTS labels (JDK 8/11/17/21)|JAVA\_HOME, PATH|—|
|**Python**|python.org / Mirror|Embedded version|PYTHON\_HOME, PATH|pip mirror configuration (get-pip.py auto-setup)|
|**Maven**|Apache Official / Mirror|—|MAVEN\_HOME, PATH|Dependency mirror (settings.xml), local repository (localRepository)|

> Java version names automatically strip `+N` build suffixes and `.LTS` tags for easier version identification and switching.

## Screenshots

<!-- Screenshots -->

> Screenshots will be added in future versions. You can download a preview build from the \[Releases](../../releases) page to see the actual interface.

## Tech Stack

|Category|Technology|Version|
|-|-|-|
|Runtime Framework|.NET Framework|4.8|
|UI Framework|WPF (Windows Presentation Foundation)|-|
|Architecture Pattern|MVVM (Model-View-ViewModel)|-|
|Dependency Injection|Microsoft.Extensions.DependencyInjection|8.0.1|
|JSON Serialization|Newtonsoft.Json|13.0.3|
|Logging Framework|Serilog + Serilog.Sinks.File|3.1.1|
|Data Access|Dapper + System.Data.SQLite|2.1.35 / 1.0.118|
|Navigation Pattern|ViewModel-First Custom Navigation|-|
|Messaging|Custom WeakMessenger (Weak Reference)|-|

## System Requirements

* Operating System: Windows 10 (Version 1803+) / Windows 11
* Runtime: .NET Framework 4.8
* Permissions: Standard user privileges (administrator privileges required for system-level environment variable configuration)

## Quick Start

### Option 1: Download Release

1. Go to the [Releases](../../releases) page and download the latest version of `SDK-Manager-GUI.zip`
2. Extract to any directory
3. Run `SDK-Manager-GUI.exe`

### Option 2: Build from Source

1. Ensure Visual Studio 2022 (or later) and .NET Framework 4.8 SDK are installed
2. Clone the repository:

```bash
   git clone <repository-url>
   ```

3. Open the `SDK-Manager-GUI.slnx` solution file with Visual Studio
4. Restore NuGet packages
5. Build the project (Debug or Release configuration)
6. Run or launch `SDK-Manager-GUI.exe` from the `bin\\Debug\\` or `bin\\Release\\` directory

Alternatively, build using MSBuild command line:

```bash
msbuild SDK-Manager-GUI\\SDK-Manager-GUI.csproj /p:Configuration=Release
```

## Project Structure

```
SDK-Manager-GUI/
├── SDK-Manager-GUI/              # Main project
│   ├── Models/                   # Data models
│   │   ├── Entities/             #   Entity classes (AppConfig, Sdk, InstalledSdk...)
│   │   ├── Enums/                #   Enumerations (SdkLanguage, DownloadStatus...)
│   │   ├── Messages/             #   Messenger message definitions
│   │   └── DTOs/                 #   ViewModel display models
│   ├── Services/                 # Service layer
│   │   ├── Interfaces/           #   Interface definitions (ISdkProvider, IDownloadEngine...)
│   │   ├── Providers/            #   SDK providers (NodeJs/Java/Python)
│   │   ├── SdkManagerService.cs  #   Core business orchestration
│   │   ├── DownloadEngine.cs     #   Download engine
│   │   ├── EnvironmentManager.cs #   Environment variable management
│   │   ├── MirrorProvider.cs     #   Mirror source management
│   │   ├── MavenService.cs       #   Maven management service
│   │   ├── LanguageService.cs    #   Multilingual service
│   │   ├── ConfigService.cs      #   Configuration management
│   │   └── ...
│   ├── ViewModels/               # View models (MVVM)
│   ├── Views/                    # Views (XAML)
│   ├── Converters/               # Value converters
│   ├── Languages/                # Language resource files
│   │   ├── zh-CN.xaml            #   Simplified Chinese
│   │   ├── zh-TW.xaml            #   Traditional Chinese
│   │   └── en.xaml               #   English
│   ├── App.xaml                  # Application entry + DataTemplate registration
│   ├── App.xaml.cs               # DI container configuration, service registration
│   └── MainWindow.xaml           # Main window (borderless custom window)
├── docs/                         # Project documentation
│   └── requirements.md           #   Requirements analysis document
└── SDK-Manager-GUI.slnx          # Solution file
```

## Configuration

Application configuration is stored in `config/config.json` with the following settings:

|Setting|Description|Default Value|
|-|-|-|
|DefaultInstallPath|SDK default installation root path|C:\\SDK-Manager|
|MaxConcurrentDownloads|Maximum concurrent downloads|3|
|MaxRetryCount|Maximum download retry count|3|
|AutoCleanLogs|Whether to automatically clean expired logs|true|
|LogKeepDays|Log retention days|30|
|Language|Interface language (zh-CN / zh-TW / en)|Auto-detect system language on first launch|

Mirror source configuration files:

* `config/mirrors.json` — SDK download mirror source list
* `config/maven-download-mirrors.json` — Maven download mirror source list

## Multilingual Support

|Language Code|Language|Description|
|-|-|-|
|zh-CN|Simplified Chinese|Default language (Simplified Chinese systems)|
|zh-TW|Traditional Chinese|Auto-selected for Traditional Chinese systems|
|en|English|Default for other language systems|

* Automatically detects system display language on first launch and persists the preference
* Can be switched at any time in the "Settings" page, with instant effect — no restart required
* Language resource files are located in the `Languages/` directory, using WPF ResourceDictionary + DynamicResource for runtime dynamic switching

## License

This project is open-sourced under the [MIT License](LICENSE).

Copyright © 2026


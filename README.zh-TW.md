# SDK Manager GUI

[简体中文](README.md) | [繁體中文](README.zh-TW.md) | [English](README.en.md)

![MIT License](https://img.shields.io/badge/license-MIT-blue.svg) ![Version](https://img.shields.io/badge/version-1.0.0.0-green.svg) ![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-purple.svg) ![Platform](https://img.shields.io/badge/platform-Windows-lightgrey.svg)

## 簡介

SDK Manager GUI 是一款基於 WPF 的多語言 SDK 桌面管理工具，支援 Node.js、Java、Python 和 Maven 的下載、安裝、版本切換與環境變數設定。透過統一的圖形介面，開發者可以高效管理多種開發工具鏈的版本，免除手動下載、解壓縮、設定環境變數的繁瑣流程。內建多鏡像源智慧調度、多版本共存切換、自動環境變數設定等特性，特別適合需要在多版本之間頻繁切換的開發場景。

- 版本：1.0.0.0
- 授權：MIT License
- 版權：Copyright © 2026

## 功能特性

1. **多 SDK 統一管理** — 在一個介面中管理 Node.js、Java、Python 和 Maven 四種 SDK
2. **多鏡像源智慧下載** — 內建官方源和國內鏡像源，自動按連線效率排序（優先級 → 延遲 → 成功率 → 失敗次數），故障自動轉移
3. **環境變數自動設定** — 安裝後自動設定 PATH 和語言專屬環境變數（NODE_HOME / JAVA_HOME / PYTHON_HOME / MAVEN_HOME），支援使用者級和系統級
4. **多版本共存與切換** — 每個版本獨立目錄安裝（`{basePath}/{SDK名稱}/{version}/`），支援快速切換啟用版本
5. **下載任務管理** — 支援多任務並行、暫停 / 恢復 / 取消、自動重試，即時顯示下載進度和速度
6. **鏡像源管理** — 支援新增 / 刪除 / 編輯鏡像源、延遲測試、優先級排序，Python / Node.js 支援套件管理器鏡像源設定，Maven 支援依賴鏡像源和本地倉庫設定
7. **多語言介面** — 支援簡體中文、繁體中文、English 三種介面語言，首次啟動自動偵測系統顯示語言，執行時可動態切換
8. **儀表板與日誌** — Dashboard 一覽所有 SDK 安裝狀態，內建日誌檢視器，支援自動清理過期日誌

## 支援的 SDK

| SDK | 來源 | 版本分類 | 環境變數 | 鏡像源支援 |
|-----|------|----------|----------|------------|
| **Node.js** | nodejs.org / 淘寶鏡像 | LTS / Current | NODE_HOME、PATH | npm 鏡像源設定 |
| **Java (Eclipse Temurin)** | Adoptium API | LTS 標識（JDK 8/11/17/21） | JAVA_HOME、PATH | — |
| **Python** | python.org / 鏡像 | 嵌入式版本 | PYTHON_HOME、PATH | pip 鏡像源設定（get-pip.py 自動設定） |
| **Maven** | Apache 官方 / 鏡像 | — | MAVEN_HOME、PATH | 依賴鏡像源（settings.xml）、本地倉庫（localRepository） |

> Java 版本名稱會自動去除 `+N` 建置後綴和 `.LTS` 標籤，便於版本識別與切換。

## 截圖

<!-- 截圖 -->

> 截圖將在後續版本補充。如有需要，可先從 [Releases](../../releases) 頁面下載體驗版查看實際介面。

## 技術棧

| 類別 | 選型 | 版本 |
|------|------|------|
| 執行框架 | .NET Framework | 4.8 |
| UI 框架 | WPF (Windows Presentation Foundation) | - |
| 架構模式 | MVVM (Model-View-ViewModel) | - |
| 依賴注入 | Microsoft.Extensions.DependencyInjection | 8.0.1 |
| JSON 序列化 | Newtonsoft.Json | 13.0.3 |
| 日誌框架 | Serilog + Serilog.Sinks.File | 3.1.1 |
| 資料存取 | Dapper + System.Data.SQLite | 2.1.35 / 1.0.118 |
| 導航模式 | ViewModel-First 自訂導航 | - |
| 訊息通訊 | 自訂 WeakMessenger（弱參考） | - |

## 系統需求

- 作業系統：Windows 10（版本 1803+）/ Windows 11
- 執行環境：.NET Framework 4.8
- 權限：一般使用者權限可執行（系統級環境變數設定需管理員權限）

## 快速開始

### 方式一：下載 Release

1. 前往 [Releases](../../releases) 頁面下載最新版本的 `SDK-Manager-GUI.zip`
2. 解壓縮到任意目錄
3. 執行 `SDK-Manager-GUI.exe`

### 方式二：從原始碼編譯

1. 確保已安裝 Visual Studio 2022（或更高版本）和 .NET Framework 4.8 開發工具包
2. 複製儲存庫：

   ```bash
   git clone <倉庫地址>
   ```

3. 使用 Visual Studio 開啟 `SDK-Manager-GUI.slnx` 解決方案檔案
4. 還原 NuGet 套件
5. 編譯專案（Debug 或 Release 設定）
6. 執行或從 `bin\Debug\` 或 `bin\Release\` 目錄啟動 `SDK-Manager-GUI.exe`

也可使用 MSBuild 命令列編譯：

```bash
msbuild SDK-Manager-GUI\SDK-Manager-GUI.csproj /p:Configuration=Release
```

## 專案結構

```
SDK-Manager-GUI/
├── SDK-Manager-GUI/              # 主專案
│   ├── Models/                   # 資料模型
│   │   ├── Entities/             #   實體類別 (AppConfig, Sdk, InstalledSdk...)
│   │   ├── Enums/                #   列舉 (SdkLanguage, DownloadStatus...)
│   │   ├── Messages/             #   Messenger 訊息定義
│   │   └── DTOs/                 #   ViewModel 展示模型
│   ├── Services/                 # 服務層
│   │   ├── Interfaces/           #   介面定義 (ISdkProvider, IDownloadEngine...)
│   │   ├── Providers/            #   SDK 提供者 (NodeJs/Java/Python)
│   │   ├── SdkManagerService.cs  #   核心業務編排
│   │   ├── DownloadEngine.cs     #   下載引擎
│   │   ├── EnvironmentManager.cs #   環境變數管理
│   │   ├── MirrorProvider.cs     #   鏡像源管理
│   │   ├── MavenService.cs       #   Maven 管理服務
│   │   ├── LanguageService.cs    #   多語言服務
│   │   ├── ConfigService.cs      #   設定管理
│   │   └── ...
│   ├── ViewModels/               # 視圖模型 (MVVM)
│   ├── Views/                    # 視圖 (XAML)
│   ├── Converters/               # 值轉換器
│   ├── Languages/                # 多語言資源檔案
│   │   ├── zh-CN.xaml            #   簡體中文
│   │   ├── zh-TW.xaml            #   繁體中文
│   │   └── en.xaml               #   English
│   ├── App.xaml                  # 應用入口 + DataTemplate 註冊
│   ├── App.xaml.cs               # DI 容器設定、服務註冊
│   └── MainWindow.xaml           # 主視窗（無邊框自訂視窗）
├── docs/                         # 專案文件
│   └── requirements.md           #   需求分析文件
└── SDK-Manager-GUI.slnx          # 解決方案檔案
```

## 設定說明

應用設定儲存在 `config/config.json` 檔案中，包含以下設定項：

| 設定項 | 說明 | 預設值 |
|--------|------|--------|
| DefaultInstallPath | SDK 預設安裝根路徑 | C:\SDK-Manager |
| MaxConcurrentDownloads | 最大並行下載數 | 3 |
| MaxRetryCount | 下載失敗最大重試次數 | 3 |
| AutoCleanLogs | 是否自動清理過期日誌 | true |
| LogKeepDays | 日誌保留天數 | 30 |
| Language | 介面語言 (zh-CN / zh-TW / en) | 首次啟動自動偵測系統語言 |

鏡像源設定檔案：

- `config/mirrors.json` — SDK 下載鏡像源列表
- `config/maven-download-mirrors.json` — Maven 下載鏡像源列表

## 多語言支援

| 語言代碼 | 語言 | 說明 |
|----------|------|------|
| zh-CN | 簡體中文 | 預設語言（簡體中文系統） |
| zh-TW | 繁體中文 | 繁體中文系統自動選擇 |
| en | English | 其他語言系統預設選擇 |

- 首次啟動時自動偵測系統顯示語言並持久化偏好
- 之後可在「設定」頁面隨時切換，切換即時生效，無需重新啟動
- 語言資源檔案位於 `Languages/` 目錄，使用 WPF ResourceDictionary + DynamicResource 實現執行時動態切換

## 開源授權

本專案基於 [MIT License](LICENSE) 開源。

Copyright © 2026

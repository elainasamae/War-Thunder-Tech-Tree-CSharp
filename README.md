# War Thunder 科技树查看器（C# 原生 Windows 版）

这是从 Electron/React 版本迁移出的 C#/.NET 8 WinForms 工程，主要面向 Windows x64。界面使用原生窗口和双缓冲自绘科技树，不启动浏览器，也不嵌入 Chromium。

本项目是非官方社区项目，与 Gaijin Entertainment 无隶属或认可关系。

## 性能设计

- 启动时只读取国家、中文名称和当前科技树。
- 弹药、挂载、车辆性能、辅助设备和改装件在右键查看时按需加载，并在内存中缓存。
- 科技树采用单个双缓冲自绘控件，只绘制当前可视区域，不为每辆载具创建一组 WinForms 子控件。
- 载具图片异步下载，不阻塞科技树操作；离线时仍可正常使用文字和全部本地数据。
- 发布版启用 x64、ReadyToRun、自包含和单文件主程序。

## 已迁移功能

- 十个国家、陆军/空军/直升机切换。
- 街机、历史、全真模式权重切换。
- 中文/英文载具名称切换。
- 科技树原生绘制、前置连线、载具组折叠/展开。
- 点击选择、研发点和银狮汇总。
- 最快研发路径及等级解锁数量规则。
- 右键以原生卡片查看车辆资料、辅助设备、性能、弹药穿深表、可筛选挂点和改装件网格。
- 官网链接、复制载具 ID、异步载具图片。

## 开发

要求 .NET 8 SDK：

```powershell
dotnet build -c Release
dotnet run --project src/WarThunderTechTree.Native
```

运行数据自检：

```powershell
dotnet run --project src/WarThunderTechTree.Native -c Release -- --self-test
```

发布 Windows x64 独立运行版：

```powershell
dotnet publish src/WarThunderTechTree.Native/WarThunderTechTree.Native.csproj -c Release -r win-x64 --self-contained true -o artifacts/release/win-x64
```

## 目录结构

```text
War-Thunder-Tech-Tree-CSharp-20260917/
├─ WarThunder科技树.exe             可直接双击运行的单文件程序
├─ Data/                            唯一的数据目录
├─ src/                             C# 源代码
├─ artifacts/
│  ├─ build/                        编译中间文件和输出
│  ├─ release/win-x64/              完整发布副本
│  ├─ verification/                 界面验证截图
│  └─ backups/                      结构调整前的必要备份
├─ WarThunderTechTree.Native.sln    Visual Studio 解决方案
├─ Directory.Build.props            统一编译输出位置
└─ README.md                        项目说明
```

本地交付目录中的 `WarThunder科技树.exe` 可以直接双击运行，只依赖同级 `Data` 目录。GitHub 源码仓库不提交 EXE 和构建产物；执行上面的发布命令后，可在 `artifacts/release/win-x64` 获得完整运行副本。所有编译、发布和验证产物均存放在 `artifacts` 下，不会散落在源码目录或工程根目录。

## 开源许可

项目原创源代码和工程文件采用 [MIT License](LICENSE)。`Data` 中的第三方游戏数据、载具名称、图片链接和商标不因本项目的 MIT 许可而被重新授权，详情见 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)。

欢迎通过 Issue 报告数据或功能问题，也欢迎提交 Pull Request。提交代码前请先运行：

```powershell
dotnet build WarThunderTechTree.Native.sln -c Release
dotnet run --project src/WarThunderTechTree.Native -c Release --no-build -- --self-test
```

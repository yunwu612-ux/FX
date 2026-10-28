# FX Simulator V1.1

一个完全离线、使用虚拟随机行情的 Windows 外汇交易模拟器。仅用于娱乐与学习，不连接券商，不使用真实资金。

## V1.1 功能\n- 买入/做多按钮使用高对比绿色，卖出/做空按钮使用高对比红色，白色粗体文字更易辨认。\n- 调整下单面板布局与按钮间距，减少窗口缩放时文字被挤压或按钮内容被裁切的情况。\n- 统一更新窗口标题、界面版本号与项目版本号。\n- 保留 V1.0 原有交易功能与深色粉红主题；不添加数据分析功能。\n\n## V1.0 原有功能
- 深色粉红主题的交易终端，采用原创视觉设计（非官方动漫作品素材）。
- USD/JPY、EUR/JPY、GBP/JPY 三种虚拟货币对。
- 随机行情、实时浮动盈亏、余额/净值/保证金/保证金率。
- 做多、做空、平仓；支持手数与杠杆设置。
- 模拟保证金强平机制。
- 自动保存账户、设置、持仓和最近交易记录。
- 本地数据文件位于 `%LOCALAPPDATA%\\FXSimulator\\account.json`。
- GitHub Actions 自动构建 Windows x64 自包含发布包。

## 运行方式
### 直接运行发布版
解压构建产物 `FXSimulator-win-x64.zip`，双击 `FXSimulator.exe`。

### 本地开发运行（可选）
安装 .NET 8 SDK 后，在项目目录执行：
```powershell
dotnet run --project FXSimulator/FXSimulator.csproj
```

## GitHub Actions 构建
1. 新建 GitHub 仓库，例如 `FXSimulator`。
2. 上传此 ZIP 解压后的全部文件（包括 `.github` 文件夹）。
3. 打开仓库的 **Actions** 页面，启用 Actions。
4. 运行 `Build Windows App` 工作流，或推送代码自动触发。
5. 在成功的工作流运行记录的 **Artifacts** 中下载 `FXSimulator-win-x64`。

## 重要说明
- 行情是随机生成的，不代表真实汇率，也不保证模拟结果反映真实市场。
- 交易、杠杆、保证金和强平仅为游戏机制，不提供投资建议。
- V1.1 重点修复下单按钮辨识度与布局问题。

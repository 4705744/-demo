# Generic Industrial Monitor Demo

一个基于 **C# / .NET 8 / WPF** 的通用上位机演示项目。项目默认使用内置模拟设备，因此不需要 PLC、数据库、Modbus Slave、Redis 或其他中间件即可启动和演示。

## 功能

- WPF 桌面监控界面
- 模拟设备实时采集
- TCP 设备接入示例（JSON Lines 协议）
- 连接 / 断开与状态显示
- 温度、压力、转速、振动实时数据
- 自定义实时趋势图控件
- 阈值报警、恢复记录、报警确认
- CSV 历史数据落盘与最近记录加载
- 文件日志与全局异常捕获
- MVVM 风格的数据绑定与命令
- GitHub Actions Windows 构建验证

## 环境要求

- Windows 10 / Windows 11
- .NET 8 SDK

本项目没有第三方 NuGet 包。安装 .NET 8 SDK 后，克隆仓库即可运行。

## 一键运行

双击仓库根目录：

```text
Start-Demo.bat
```

也可以在 PowerShell / CMD 中执行：

```powershell
dotnet run --project .\src\IndustrialMonitor.App\IndustrialMonitor.App.csproj
```

## 默认演示方式

1. 启动程序。
2. 协议保持“模拟设备”。
3. 点击“连接设备”。
4. 观察实时指标、趋势曲线和采集日志。
5. 当数据超过阈值时会生成报警，可点击“确认全部报警”。
6. 历史数据自动保存到当前用户的本地应用数据目录。

## TCP 接入示例

选择 `TCP JSON` 后填写 IP 与端口。服务端每行发送一个 JSON 对象，例如：

```json
{"temperature":62.5,"pressure":0.72,"speed":1460,"vibration":2.4,"productionCount":1024}
```

每个 JSON 报文以换行符结束。字段缺失时会保留默认值。

## 目录结构

```text
.
├─ .github/workflows/build.yml
├─ src/IndustrialMonitor.App
│  ├─ Controls/TrendChart.cs
│  ├─ Infrastructure/AppInfrastructure.cs
│  ├─ Models/Models.cs
│  ├─ Services/AlarmAndHistoryServices.cs
│  ├─ Services/DeviceServices.cs
│  ├─ ViewModels/MainViewModel.cs
│  ├─ App.xaml
│  ├─ App.xaml.cs
│  ├─ MainWindow.xaml
│  ├─ MainWindow.xaml.cs
│  └─ IndustrialMonitor.App.csproj
├─ IndustrialMonitorDemo.sln
└─ Start-Demo.bat
```

## 数据与日志位置

程序运行后会在以下目录创建文件：

```text
%LOCALAPPDATA%\GenericIndustrialMonitorDemo\
├─ data\history-YYYYMMDD.csv
└─ logs\app-YYYYMMDD.log
```

这些文件不写入 Git 仓库。

## 架构说明

项目将 UI、ViewModel、设备通信、报警规则、历史存储和基础设施进行分离。`IDeviceClient` 用于抽象设备通信，因此后续可继续增加串口、Modbus TCP、Modbus RTU 或 OPC UA 实现，而无需重写主界面。

Demo 为了保证克隆后可直接运行，默认历史存储使用 CSV。实际项目中可以新增 SQL Server 实现并通过接口替换，界面层无需感知数据库细节。

## 构建

```powershell
dotnet restore IndustrialMonitorDemo.sln
dotnet build IndustrialMonitorDemo.sln -c Release --no-restore
```

GitHub Actions 会在 `windows-latest` 上执行同样的构建检查。

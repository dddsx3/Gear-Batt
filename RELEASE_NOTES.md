# Gear-Batt v1.3.1

**中文** | [English](#english)

## 中文

Gear-Batt 是一个 Windows 单文件托盘工具，用于显示所选无线外设（2.4G 接收器 / 蓝牙）的电量。充电时显示闪电，使用电池时按比例填充并显示百分比。

### 本版重点

- **界面跟随系统语言**：主程序和数据采集器启动时读取 Windows 显示语言；中文系统显示中文，其他语言显示英文。
- **可强制指定语言**：`Gear-Batt.exe --lang zh|en`；采集器同样支持 `--lang zh|en`。
- 主程序与采集器均已更新；采集报告仍是英文技术报告，界面文案跟随系统语言。

### 已有功能

- 状态即时刷新：设备接入/移除、系统电源状态变化立即触发扫描；充电或电量 ≤10% 时每 5 秒快速检查，其余 60 秒。
- 低电量提醒：当前显示设备未充电且低于 5% 时静默弹出提示卡片；不播放声音、不抢焦点、**不会自动关闭**，需点击右上角 × 关闭；每轮只提醒一次。
- 单设备显示：多设备时在菜单中指定要显示哪一个，并记住选择。
- 支持 Windows 电池接口、罗技 HID++ 2.0、ATK/VXE 373B:1216、DARKRIM 3654:6901、Windows 蓝牙电量属性。

### 自愿提交设备协议数据

如果希望适配你的 2.4G 设备，可以运行与主程序同目录的 `Gear-Batt-Collector.exe`：

- **完全自愿**：需要主动勾选同意后才会开始采集。
- 报告只保存在本机，**不会自动上传**。
- 采集 HID 元数据、解析后的字段信息、只读电量协议响应和 Windows/蓝牙电量值。
- 不采集按键、鼠标移动、原始输入报告、用户名、计算机名、IP、USB 序列号或蓝牙地址。
- 请先检查报告，再手动附加到 [GitHub Issue](https://github.com/dddsx3/Gear-Batt/issues)。

### 下载

- `Gear-Batt.exe`：主托盘程序
- `Gear-Batt-Collector.exe`：自愿数据采集器（可选）

### 系统要求

Windows 10 1903+ / Windows 11；系统自带 .NET Framework 4.8；无需安装。

### 链接

- [双语 README](https://github.com/dddsx3/Gear-Batt#readme)（[English](https://github.com/dddsx3/Gear-Batt/blob/main/README.en.md)）
- [问题与适配请求](https://github.com/dddsx3/Gear-Batt/issues)

## English

Gear-Batt is a single-exe Windows tray utility that shows the battery level of the selected wireless peripheral (2.4 GHz receiver / Bluetooth). It shows a lightning bolt while charging, and a proportional battery fill with percentage on battery.

### Highlights

- **UI follows the system language**: the main app and data collector read the Windows display language at startup; Chinese Windows shows Chinese, other languages show English.
- **Language override**: `Gear-Batt.exe --lang zh|en`; the collector supports `--lang zh|en` as well.
- Both the main app and collector are updated. The collector report remains an English technical report; the UI follows the system language.

### Existing features

- Instant status refresh: device attach/remove and system power-status changes trigger a scan immediately; while charging or at ≤10% it checks every 5 seconds, otherwise every 60 seconds.
- Low-battery alert: when the selected device is discharging and below 5%, a silent card appears. It plays no sound, does not steal focus, and **does not auto-close**; click × to dismiss. It is shown once per low-battery episode.
- One displayed device: choose which device to show from the menu when several are detected; the selection is remembered.
- Supported sources: Windows battery interface, Logitech HID++ 2.0, ATK/VXE 373B:1216, DARKRIM 3654:6901, and the Windows Bluetooth battery property.

### Voluntary protocol data submission

To help add support for your 2.4 GHz device, run `Gear-Batt-Collector.exe` from the same folder as the main app:

- **Entirely voluntary**: collection starts only after you check the consent box.
- The report stays on your machine and is **not uploaded automatically**.
- It collects HID metadata, parsed field information, read-only battery protocol responses, and Windows/Bluetooth battery values.
- It does not collect keystrokes, mouse movement, raw input reports, username, computer name, IP addresses, USB serial numbers, or Bluetooth addresses.
- Review the report, then attach it manually to a [GitHub Issue](https://github.com/dddsx3/Gear-Batt/issues).

### Downloads

- `Gear-Batt.exe` — main tray app
- `Gear-Batt-Collector.exe` — optional voluntary data collector

### Requirements

Windows 10 1903+ / Windows 11 with the built-in .NET Framework 4.8; no installation required.

### Links

- [Bilingual README](https://github.com/dddsx3/Gear-Batt#readme) ([中文](https://github.com/dddsx3/Gear-Batt/blob/main/README.md))
- [Issues & adaptation requests](https://github.com/dddsx3/Gear-Batt/issues)

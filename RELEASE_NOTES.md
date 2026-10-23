# Gear-Batt v1.4.0

**中文** | [English](#english)

## 中文

Gear-Batt 是一个 Windows 单文件托盘工具，用于显示所选无线外设（2.4G 接收器 / 蓝牙）的电量。

### 本版重点

- **新增 CX 2.4G 接收器适配（如 260D:1163）**：在本机真实鼠标上抓包逆向得到的只读电量协议；回复包含电量百分比、有线/充电标志和电池电压（mV）。托盘提示与菜单现在会显示电压（例如 `4.32V`），可以确认读到的是实时数据。
- **修复“永远显示 100%”的假电量**：当鼠标切到 2.4G 模式后，Windows 会一直保留蓝牙模式下最后一次电量（残留数据）。现在电量超过 7 天未更新的蓝牙条目会被跳过，不再顶替真实设备。
- 休眠的 CX/ATK 鼠标不会回复电量查询：动一下鼠标即可获得首次读数；成功读数后缓存 12 小时，休眠期间每 10 秒自动重试。

### 已有功能

- 状态即时刷新：设备接入/移除、电源状态变化立即触发扫描；充电或电量 ≤10% 时每 5 秒快速检查。
- 低电量提醒：未充电且低于 5% 时静默弹出提示卡片，不自动关闭，点击 × 关闭。
- 单设备显示；支持 Windows 电池接口、罗技 HID++ 2.0、CX、ATK/VXE、DARKRIM、蓝牙电量属性。
- 界面跟随 Windows 显示语言（中文/英文），`--lang zh|en` 可强制指定。

### 自愿提交设备协议数据

希望适配你的 2.4G 设备？运行与主程序同目录的 `Gear-Batt-Collector.exe`：完全自愿，报告只保存在本机、不会自动上传；检查后手动附加到 [GitHub Issue](https://github.com/dddsx3/Gear-Batt/issues)。

### 下载

- `Gear-Batt.exe`：主托盘程序
- `Gear-Batt-Collector.exe`：自愿数据采集器（可选，已加入 CX 探测）

### 系统要求

Windows 10 1903+ / Windows 11；系统自带 .NET Framework 4.8；无需安装。

## English

Gear-Batt is a single-exe Windows tray utility that shows the battery level of the selected wireless peripheral (2.4 GHz receiver / Bluetooth).

### Highlights

- **New CX 2.4G receiver support (e.g. 260D:1163)**: a read-only battery protocol reverse-engineered from captured traffic on a real mouse. The reply contains the percentage, a wired/charging flag, and the battery voltage in millivolts. The tooltip and menu now show the voltage (for example `4.32V`) so you can confirm the data is live.
- **Fixed the phantom "always 100%" battery**: after a mouse switches to 2.4G mode, Windows keeps the last Bluetooth-mode battery value forever. Bluetooth entries whose battery has not been refreshed for 7+ days are now skipped instead of shadowing the real device.
- Sleeping CX/ATK mice do not answer battery queries: move the mouse once for the first reading; a successful reading is cached for 12 hours and retried every 10 seconds while the mouse sleeps.

### Existing features

- Instant status refresh on device/power events; 5-second fast polling while charging or at ≤10%.
- Low-battery alert card below 5% while discharging; silent, no auto-close.
- One displayed device; supported sources: Windows battery interface, Logitech HID++ 2.0, CX, ATK/VXE, DARKRIM, Bluetooth battery property.
- UI follows the Windows display language (Chinese/English); override with `--lang zh|en`.

### Voluntary protocol data submission

Want your 2.4G device supported? Run `Gear-Batt-Collector.exe` from the same folder: entirely voluntary, the report stays local and is never uploaded automatically; review it and attach it to a [GitHub Issue](https://github.com/dddsx3/Gear-Batt/issues).

### Downloads

- `Gear-Batt.exe` — main tray app
- `Gear-Batt-Collector.exe` — optional voluntary data collector (now with a CX probe)

### Requirements

Windows 10 1903+ / Windows 11 with the built-in .NET Framework 4.8; no installation required.

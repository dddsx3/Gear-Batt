# Gear-Batt v1.4.1

**中文** | [English](#english)

## 中文

Gear-Batt 是一个 Windows 单文件托盘工具，用于显示所选无线外设（2.4G 接收器 / 蓝牙）的电量。

### 本版重点：修正 CX 鼠标电量解码

在真实鼠标上分别抓取了【电池供电】与【充电中】两种状态的协议回复，确认了此前解码错误：

- **电池供电时固件永远回复 100%**（两次实测电压分别为 4.320V 和 4.288V，百分比字段却都固定为 100）——这就是“保持电池供电恒显示 100%”的根本原因，不是显示层问题。
- 现在：电池供电时改用**静置电压估算电量**，并带 `≈` 近似标记（曲线按本机实测 4.32V=满电、4.288V≈95% 标定）；随放电电压下降，数值会真实变化。
- **充电中**：第 7 字节翻转为 1，第 6 字节是充电计的**真实百分比**（实测 95% @ 4.38V），直接精确显示。
- 托盘与菜单始终显示电压（如 `4.29V` / `4.38V`），可作为数据来源证据。

### 使用提示

- 鼠标休眠时不回复查询：动一下鼠标即可在 10 秒内获得读数；成功读数缓存 12 小时。
- 充电时显示精确百分比 + 闪电；拔线后显示 `≈` 电压估算值。

### 其他

- 支持来源：Windows 电池接口、罗技 HID++ 2.0、CX 2.4G（260D:1163）、ATK/VXE、DARKRIM、蓝牙电量属性（超 7 天未更新的残留条目会被跳过）。
- 界面跟随 Windows 显示语言（中文/英文），`--lang zh|en` 可强制指定。
- 自愿数据采集器 `Gear-Batt-Collector.exe` 完全自愿、只存本机、不上传；报告可提交到 [GitHub Issue](https://github.com/dddsx3/Gear-Batt/issues)。

### 下载

- `Gear-Batt.exe`：主托盘程序
- `Gear-Batt-Collector.exe`：自愿数据采集器（可选）

### 系统要求

Windows 10 1903+ / Windows 11；系统自带 .NET Framework 4.8；无需安装。

## English

Gear-Batt is a single-exe Windows tray utility that shows the battery level of the selected wireless peripheral (2.4 GHz receiver / Bluetooth).

### Highlight: corrected CX battery decoding

Protocol replies were captured on a real mouse in both the **on-battery** and **charging** states, which revealed the earlier decoding mistake:

- **On battery the firmware always replies 100%** (two captures at 4.320V and 4.288V both reported a pinned 100) — that was the real cause of "always 100% on battery", not a display bug.
- Now: on battery the level is **estimated from the resting voltage** and shown with an `≈` marker (curve anchored on 4.32V = full and 4.288V ≈ 95% measured on this mouse); the value now actually drops as the battery discharges.
- **While charging**: byte 7 flips to 1 and byte 6 carries the **real charger-gauge percentage** (measured 95% at 4.38V), displayed exactly.
- The tooltip and menu always show the voltage (e.g. `4.29V` / `4.38V`) as evidence of the data source.

### Usage notes

- A sleeping mouse does not answer queries: move it once and a reading appears within 10 seconds; successful readings are cached for 12 hours.
- Charging shows the exact percentage plus a lightning bolt; unplugged shows the `≈` voltage estimate.

### Other

- Supported sources: Windows battery interface, Logitech HID++ 2.0, CX 2.4G (260D:1163), ATK/VXE, DARKRIM, Bluetooth battery property (entries stale for 7+ days are skipped).
- UI follows the Windows display language (Chinese/English); override with `--lang zh|en`.
- The voluntary collector `Gear-Batt-Collector.exe` is entirely voluntary, stays local, never uploads; attach reports to a [GitHub Issue](https://github.com/dddsx3/Gear-Batt/issues).

### Downloads

- `Gear-Batt.exe` — main tray app
- `Gear-Batt-Collector.exe` — optional voluntary data collector

### Requirements

Windows 10 1903+ / Windows 11 with the built-in .NET Framework 4.8; no installation required.

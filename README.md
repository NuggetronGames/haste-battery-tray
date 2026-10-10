# Haste Battery

A tiny Windows tray app that shows your HyperX Pulsefire Haste Wireless battery
percentage as colored digits in the notification area, like HWiNFO's tray readings.

## Build (Windows)

1. Install the .NET 8 SDK: https://dotnet.microsoft.com/download
2. In this folder:

       dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true

3. Run `bin\Release\net8.0-windows\win-x64\publish\HasteBattery.exe`.

The first time, drag the icon out of the tray overflow (^) onto the taskbar so it stays visible.

## Use

- Right-click the tray icon: Refresh now, Settings, Run diagnostics, Exit.
- Settings: pick the four colors (70-100, 30-70, 10-30, 10 or less), polling interval,
  and "Start with Windows". Startup launches straight into the tray, no window.
- Config is saved to `%AppData%\HasteBattery\settings.json`.

## IMPORTANT: the HID protocol needs a one-time check

HyperX does not publish the battery protocol for the original Pulsefire Haste Wireless
(USB ID 03F0:028E). The defaults here come from a USB capture of NGENUITY: the app sends
`51 00 00 ...` and the dongle replies `51 00 00 09 5D ...`, with byte 4 (0x5D = 93) taken as
the battery percentage. That interpretation still needs confirming against what NGENUITY
shows. If you saved settings before this version, delete `%AppData%\HasteBattery\settings.json`
so the new defaults apply.

If the tray shows `--`:

1. Right-click the icon > Run diagnostics. It takes about 12 seconds per interface, then
   writes and opens `diagnostics.txt` listing every HyperX HID interface and any raw packets.
2. Keep moving the mouse the whole time, and also try plugging/unplugging the charge cable
   during the run. Close HyperX NGENUITY first if it seems to hold the device.
3. Look for a reply containing your known battery value (compare with NGENUITY).
   Then edit `settings.json`: `ProbeHex`, `HeaderHex`, `BatteryOffset`, `ChargingOffset`.
   If you send me the diagnostics output I can work out the right values.

The mouse sleeps after about a minute idle, so a reading may be missing until you move it;
the app keeps showing the last known value and notes when it was taken.

# Wireless Peripheral Battery for Stream Deck

A plugin for **Elgato Stream Deck** that displays the battery level of supported wireless peripherals by communicating directly with their USB HID interfaces—without requiring Razer Synapse or HyperX NGENUITY to be running.

Currently supported devices:

- **Razer Viper Ultimate**
- **HyperX Cloud III S Wireless**

## Features

- Displays the battery percentage on a Stream Deck key.
- Automatically refreshes the battery reading every 30 seconds.
- Allows an immediate refresh by pressing the key.
- Uses colored battery icons:
  - **Green:** above 50%.
  - **Yellow:** 21–50%.
  - **Red:** 20% or below.
  - **Blue with a lightning bolt:** charging (Razer).
  - **Gray:** battery information unavailable.
- Handles disconnected or unresponsive devices.
- Reads battery information without requiring the manufacturer's companion software.

## Supported Devices

| Device                      | Battery percentage          | Charging status         |
| --------------------------- | --------------------------- | ----------------------- |
| Razer Viper Ultimate        | Yes                         | Yes                     |
| HyperX Cloud III S Wireless | Yes, through the USB dongle | Not currently supported |

The project currently checks these USB identifiers:

- **Razer:** VID `1532`, PID `007A` and `007B`.
- **HyperX:** VID `03F0`, PID `02CC` and `06BE`.

Compatibility depends on the device and receiver exposing the expected HID protocol.

## Screenshots

### Battery Level

![Battery level](docs/battery.png)

### Stream Deck Plugin

![Plugin](docs/plugin.png)

## Requirements

- Windows 10 or Windows 11.
- Elgato Stream Deck 6.4 or later.
- .NET Framework 4.8.
- A compatible .NET SDK for building the project.
- A supported peripheral connected, preferably through its wireless USB receiver.

## Installation

### Install from source

1. Download or clone this repository.
2. Make sure the .NET SDK and Stream Deck are installed.
3. Close Stream Deck.
4. Run `install.bat`.
5. The script builds the project in Release mode, copies the plugin to the Stream Deck plugins directory, and launches Stream Deck again.
6. In Stream Deck, find the **Wireless Peripheral Battery** category and add the relevant action to a key.

The installer copies the plugin to:

```text
%APPDATA%\Elgato\StreamDeck\Plugins\com.jose.razerbattery.sdPlugin
```

> The installer closes and restarts Stream Deck during installation.

### Manual installation

1. Build the project in `Release` configuration.
2. Copy the output files, including `RazerBattery.exe`, `manifest.json`, and the `Images` folder, into the plugin directory shown above.
3. Restart Stream Deck.

## Usage

1. Add the **Razer Viper Ultimate Battery** or **HyperX Cloud III S Wireless Battery** action to a Stream Deck key.
2. Wait for the first battery reading.
3. The key will display the battery percentage.
4. Press the key to request an immediate refresh.

Possible statuses include:

- `reading...` — the first query is still in progress.
- `no mouse` — no compatible device was detected.
- `sleeping` — the device or receiver did not respond.
- `--` — no battery percentage is available yet.

## How It Works

The plugin communicates with the peripherals through USB HID.

### Razer Viper Ultimate

Sends battery commands using Razer's HID Feature Reports, reads the battery level and charging status, and converts the returned value into a percentage.

### HyperX Cloud III S Wireless

Communicates with the USB receiver through the manufacturer's HID interface, sends a battery query, and parses the response.

The code identifies candidate HID interfaces and prioritizes those associated with the manufacturer's Usage Page. It also remembers the interface that successfully responded so it can try to reuse it for subsequent readings.

### Monitoring

Each device uses a shared background monitor that polls the battery every 30 seconds. Stream Deck keys are updated when the status or percentage changes to avoid unnecessary redraws.

## Project Structure

```text
RazerBattery/
├── BatteryAction.cs      # Stream Deck actions and key rendering
├── BatteryMonitor.cs    # Periodic polling and battery state
├── BatteryReading.cs    # Battery reading model and statuses
├── RazerHid.cs          # HID communication with Razer Viper Ultimate
├── HyperXHid.cs         # HID communication with HyperX Cloud III S Wireless
├── Program.cs            # Plugin entry point
├── manifest.json         # Stream Deck plugin metadata and actions
├── RazerBattery.csproj   # .NET project and dependencies
├── instalar.bat          # Build and installation script
└── Images/               # Plugin and battery-level icons
```

## Built With

- C#
- .NET Framework 4.8
- [StreamDeck-Tools](https://github.com/BarRaider/streamdeck-tools)
- [HidSharp](https://github.com/IntergatedCircuits/HidSharp)
- SkiaSharp
- Windows HID API

## Known Limitations

- Charging status for the HyperX Cloud III S Wireless is not implemented yet.
- HyperX battery readings are obtained through the USB receiver; Bluetooth is not queried.
- If a peripheral is powered off, asleep, or unresponsive, the plugin may retain the last known percentage while showing a `sleeping` status.
- Support is currently limited to the listed models and USB identifiers. Other peripherals may use different protocols.
- HID behavior may vary between hardware revisions or firmware versions.

## Building

Build the project manually with:

```powershell
dotnet build RazerBattery.csproj -c Release
```

The project produces a console application that Stream Deck launches as a plugin. It uses `StreamDeck-Tools` to manage actions and key updates.

## Credits

- [BarRaider / StreamDeck-Tools](https://github.com/BarRaider/streamdeck-tools)
- [HidSharp](https://github.com/IntergatedCircuits/HidSharp)

## License

This project is licensed under the MIT License. See [LICENSE](LICENSE) for details.

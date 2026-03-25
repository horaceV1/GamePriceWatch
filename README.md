<p align="center">
  <h1 align="center">🎮 Game Price Watch</h1>
  <p align="center">
    A sleek Windows desktop app that tracks the <strong>cheapest game prices</strong> across stores, powered by <a href="https://www.allkeyshop.com">AllKeyShop</a>.
  </p>
  <p align="center">
    <a href="https://github.com/horaceV1/GamePriceWatch/releases/latest"><img src="https://img.shields.io/github/v/release/horaceV1/GamePriceWatch?style=flat-square&color=2ECC71" alt="Latest Release"></a>
    <a href="https://github.com/horaceV1/GamePriceWatch/actions"><img src="https://img.shields.io/github/actions/workflow/status/horaceV1/GamePriceWatch/release.yml?style=flat-square" alt="Build Status"></a>
    <img src="https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square" alt=".NET 8">
    <img src="https://img.shields.io/badge/UI-WPF%20%2B%20Fluent-0078D4?style=flat-square" alt="WPF + Fluent UI">
    <a href="https://github.com/horaceV1/GamePriceWatch/blob/master/LICENSE"><img src="https://img.shields.io/github/license/horaceV1/GamePriceWatch?style=flat-square" alt="License"></a>
  </p>
</p>

---

## ✨ Features

- **TOP 50 Popular Games** — Instantly loads the most popular games from AllKeyShop's main page
- **Best Store Offers** — Click any game to see the 5 cheapest offers across verified stores
- **Price Comparison** — See original vs. discounted prices, store ratings, regions, and editions at a glance
- **Coupon Codes** — One-click copy for available discount coupons
- **Game Thumbnails** — Sprite-based thumbnails in the list, full cover art in the detail view
- **Auto-Refresh** — Prices update automatically every hour in the background
- **Modern Fluent UI** — Built with [WPF-UI](https://github.com/lepoco/wpfui) for a native Windows 11 look & feel
- **Self-Contained** — No .NET runtime installation required; everything is bundled

## 📸 Screenshots

> _Coming soon — launch the app and click **Refresh** to see it in action!_

## 📦 Installation

### Windows (recommended)

Download from the [**Latest Release**](https://github.com/horaceV1/GamePriceWatch/releases/latest):

| File | Description |
|------|-------------|
| `GamePriceWatch-Setup.exe` | **Installer** — guided setup, desktop shortcut, uninstaller |
| `GamePriceWatch-Windows-x64.zip` | **Portable** — extract anywhere and run, no install needed |

> **Requirements:** Windows 10 or later (x64). Self-contained — no .NET install needed.

### Ubuntu / Debian (experimental)

The `.deb` package wraps the Windows executable to run via [Wine](https://www.winehq.org/):

```bash
# Install (will pull Wine as a dependency)
sudo dpkg -i gamepricewatch_*_amd64.deb
sudo apt-get install -f   # resolve dependencies if needed

# Launch
gamepricewatch
```

> **Requirements:** Ubuntu 22.04+ or Debian 12+ with Wine (`sudo apt install wine64`).

## 🚀 Usage

1. Launch **Game Price Watch**
2. Click **Refresh** to load the TOP 50 most popular games
3. Select any game from the list to see the cheapest store offers
4. Click **AllKeyShop ↗** to open the full listing in your browser
5. Click any **coupon code** badge to copy it to your clipboard

The app auto-refreshes every hour, so you can leave it running to stay on top of price drops.

## 🛠️ Building from Source

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10+ (WPF requires Windows)

### Build & Run

```bash
# Clone the repo
git clone https://github.com/horaceV1/GamePriceWatch.git
cd GamePriceWatch

# Run in development
dotnet run --project GamePriceWatch/GamePriceWatch.csproj

# Publish a self-contained single-file executable
dotnet publish GamePriceWatch/GamePriceWatch.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o publish/win-x64
```

### Build the Installer

Requires [Inno Setup](https://jrsoftware.org/isinfo.php):

```powershell
# After publishing, build the installer
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" `
  /DPublishDir="publish\win-x64" `
  installer.iss
```

The installer will be output to `Output/GamePriceWatch-Setup.exe`.

## 🏗️ Architecture

```
GamePriceWatch/
├── Models/
│   └── GameInfo.cs          # GameInfo and StorePrice data models
├── Services/
│   └── ScraperService.cs    # AllKeyShop HTML scraper + sprite image cropper
├── ViewModels/
│   └── MainViewModel.cs     # MVVM ViewModel (CommunityToolkit.Mvvm)
├── Converters/
│   └── Converters.cs        # WPF value converters
├── MainWindow.xaml           # Fluent UI master-detail layout
├── MainWindow.xaml.cs        # Code-behind (coupon copy animation)
└── App.xaml                  # Application entry point & theme
```

### Key Technologies

| Technology | Purpose |
|------------|---------|
| **.NET 8 / WPF** | Desktop UI framework |
| [**WPF-UI (Fluent)**](https://github.com/lepoco/wpfui) | Modern Windows 11 styling |
| [**CommunityToolkit.Mvvm**](https://learn.microsoft.com/dotnet/communitytoolkit/mvvm/) | MVVM source generators |
| [**HtmlAgilityPack**](https://html-agility-pack.net/) | HTML parsing for web scraping |
| [**Inno Setup**](https://jrsoftware.org/isinfo.php) | Windows installer creation |
| **GitHub Actions** | CI/CD — automated builds & releases |

## 🔄 CI/CD

Every tagged release (`v*`) triggers a GitHub Actions workflow that:

1. **Builds** a self-contained Windows x64 executable
2. **Packages** it as an Inno Setup installer + portable ZIP
3. **Cross-compiles** on Linux and creates a `.deb` package (Wine-based)
4. **Publishes** all artifacts to a GitHub Release

See [`.github/workflows/release.yml`](.github/workflows/release.yml) for details.

## 📄 License

This project is open source. See the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- [AllKeyShop](https://www.allkeyshop.com) — game price data source
- [WPF-UI](https://github.com/lepoco/wpfui) — Fluent Design System for WPF
- [Inno Setup](https://jrsoftware.org/isinfo.php) — Windows installer compiler

---

<p align="center">
  Made with ❤️ by <a href="https://github.com/horaceV1">horaceV1</a>
</p>

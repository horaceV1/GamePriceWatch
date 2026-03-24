# 🎮 Game Price Watch

A sleek Windows desktop application that tracks game prices from [AllKeyShop](https://www.allkeyshop.com) — showing you the cheapest stores for the latest 20 game releases.

![.NET 8](https://img.shields.io/badge/.NET-8.0-purple) ![WPF](https://img.shields.io/badge/WPF-UI%20Fluent-blue) ![License](https://img.shields.io/badge/License-MIT-green)

## ✨ Features

- **Latest 20 Releases** — Automatically fetches the newest game releases from AllKeyShop
- **Top 5 Cheapest Stores** — For each game, displays the 5 cheapest store offers sorted by price
- **Auto-Refresh** — Prices update automatically every 1 hour
- **Modern Fluent UI** — Dark theme with Windows 11 Fluent Design (WPF-UI)
- **Fast & Responsive** — Async data loading with progress indicators
- **Direct Links** — Click through to AllKeyShop for any game

## 🛠️ Tech Stack

- **C# / .NET 8** — Modern, performant runtime
- **WPF** — Native Windows UI framework
- **WPF-UI (Fluent)** — Windows 11 Fluent Design System
- **HtmlAgilityPack** — HTML parsing for web scraping
- **CommunityToolkit.Mvvm** — Clean MVVM architecture
- **Microsoft.Extensions.Hosting** — Dependency injection & hosting

## 🚀 Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10/11

### Build & Run

```bash
cd GamePriceWatch
dotnet restore
dotnet run
```

### Or build a standalone executable

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

## 📋 How It Works

1. The app scrapes the AllKeyShop **New Releases** page to find the latest 20 game releases
2. For each game, it fetches the individual game page to get all store offers
3. Store prices are sorted ascending, and the **top 5 cheapest unique stores** are displayed
4. Data refreshes automatically every hour, or you can manually refresh anytime

## 📸 UI Overview

- **Header** — App title, last update time, and refresh button
- **Game Cards** — Each game shows name, platform, release date, and historical low price
- **Price Table** — Store name, region, edition, and price for each of the top 5 offers
- **Status Bar** — Current status and auto-refresh indicator

## 📄 License

MIT License — feel free to use and modify.

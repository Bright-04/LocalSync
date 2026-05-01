# LocalSync 🚀

LocalSync is a **LAN-first, production-grade file transfer and synchronization platform** built with **.NET 8** and **React**. It demonstrates advanced systems engineering through zero-configuration peer discovery, chunked streaming, and real-time operational visibility.

![Modern UI](https://img.shields.io/badge/UI-Modern-blueviolet)
![Realtime](https://img.shields.io/badge/Realtime-SignalR-blue)
![Discovery](https://img.shields.io/badge/Discovery-mDNS-emerald)

## ✨ Premium UI/UX Experience

LocalSync features a high-fidelity dashboard designed with modern web standards:
- **Rich Aesthetics**: A deep `#0B0F19` dark theme with ambient glowing gradients and glassmorphic panels.
- **Modern Typography**: Precision-tailored using `Outfit` for structural headings and `Inter` for data density.
- **Dynamic Interactions**: Micro-animations on device cards, smooth width transitions for progress bars, and real-time state signaling.

## 🛠️ Technical Architecture

LocalSync is built on **Clean Architecture** principles, ensuring a decoupled and maintainable codebase:

- **LocalSync.Core**: Domain models, interfaces, and business logic (Transfer/Sync management).
- **LocalSync.Infrastructure**: mDNS discovery engine (`Makaretu.Dns`) and high-performance `HttpClient` streaming client.
- **LocalSync.Api**: ASP.NET Core 8 Web API, SignalR Real-time Hub, and Controller endpoints.
- **LocalSync.Worker**: Background monitoring using `FileSystemWatcher` for automated folder synchronization.
- **LocalSync.Tests**: xUnit test suite for validating transfer logic and state transitions.

## 🚀 Key Features

### 1. Robust Device Discovery
Uses mDNS to discover peers on the local network. Includes a **deterministic ID fallback** mechanism to handle fragmented DNS packets on complex local network adapters (like Windows Loopback).

### 2. Chunked File Transfer
Large files are sliced into 1MB chunks natively in the browser and streamed to the target using `FileStream.Seek`. This allows for resumable, low-memory transfers of multi-gigabyte files.

### 3. Real-time Monitoring
Powered by **SignalR**, providing a "live" feel to the dashboard. Progress, speeds, and state changes are pushed from the server to the client instantly.

### 4. Folder Sync (One-Way)
Automated folder watching that triggers background transfers. Implements a **Last-Writer-Wins** strategy using MD5 hashing and UTC timestamps to resolve conflicts.

## 🏁 Getting Started

### Prerequisites
- .NET 8 SDK
- Node.js (v18+)

### Running the Project

1. **Start the Backend**:
   ```bash
   cd backend/LocalSync.Api
   dotnet run --urls "http://localhost:5000"
   ```

2. **Start the Frontend**:
   ```bash
   cd frontend/localsync-web
   npm install
   npm run dev
   ```

3. **Optional: Launch Instance B (Receiver)**:
   ```bash
   # In a new terminal
   cd backend/LocalSync.Api
   dotnet run --urls "http://localhost:5001"
   ```

Open your browser at `http://localhost:5173` to experience the LocalSync Network Radar.

## 🧪 CI/CD
Automated builds and tests are handled via **GitHub Actions** (`.github/workflows/ci.yml`), ensuring every pull request meets engineering standards.
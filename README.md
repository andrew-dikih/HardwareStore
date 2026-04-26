# HardwareStore

A web tool to compare hardware store product prices across multiple online retailers. Users describe what they need in natural language; the tool queries selected retailers and returns a normalized price comparison with a best-store recommendation.

## Project Structure

```
HardwareStore.slnx            — .NET solution
src/
  HardwareStore.Core/         — Domain models & interfaces
  HardwareStore.Infrastructure/ — CosmosDB repos, retailer clients, background services
  HardwareStore.Api/          — ASP.NET Core Web API (port 5000)
  HardwareStore.Web/          — React + Vite frontend (port 5173)
```

## Features

- **Public search**: Compare Home Depot vs Lowe's without logging in
- **Authenticated search**: Compare multiple retailers, multiple products, save history
- **Natural language input**: Describe your project in plain English
- **Async search jobs**: Background processing with status polling
- **Report history**: Browse past searches and results
- **Admin panel**: Approve/reject users, manage retailers, set per-user limits

## Getting Started

### Backend (ASP.NET Core)
```bash
cd src/HardwareStore.Api
dotnet run
# API available at http://localhost:5000
```

Configure `appsettings.Development.json` with your CosmosDB, email (SMTP), and OpenAI keys.

### Frontend (React + Vite)
```bash
cd src/HardwareStore.Web
npm install
npm run dev
# App available at http://localhost:5173
```

The Vite dev server proxies `/api` requests to `http://localhost:5000`.

## Tech Stack

- **Backend**: ASP.NET Core 8, CosmosDB, MailKit, BCrypt, JWT auth
- **Frontend**: React 19, TypeScript, Vite, Tailwind CSS, React Router, Axios
- **Hosting**: Azure Container Apps (Docker-ready)

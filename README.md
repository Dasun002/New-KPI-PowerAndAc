# KPI Monitoring System

A web-based system for tracking and monitoring Key Performance Indicators (KPIs) for Power and AC. It lets users record KPI data, view progress, and make better decisions from clear dashboards and reports.

## Features

- KPI data entry and management (add, edit, delete)
- Dashboard with summaries and charts
- Monitoring of KPI performance against targets
- Reports and data views
- Responsive, user-friendly interface
- REST API for communication between frontend and backend

## Tech Stack

| Layer     | Technology                |
| --------- | ------------------------- |
| Frontend  | Angular                   |
| Backend   | ASP.NET Core Web API (C#) |
| Database  | SQL Server (update if different) |
| Version Control | Git and GitHub      |

## Project Structure

```
New-KPI-PowerAndAc/
├── backend/        # ASP.NET Core Web API
├── frontend/       # Angular application
└── README.md
```

> Update the folder names above if your project uses different ones (for example `KPIMonitoring`).

## Getting Started

### Prerequisites

- [Node.js](https://nodejs.org/) (LTS version) and npm
- [Angular CLI](https://angular.dev/tools/cli): `npm install -g @angular/cli`
- [.NET SDK](https://dotnet.microsoft.com/download) (version used by the project)
- SQL Server (or the database your project uses)
- Git

### 1. Clone the repository

```bash
git clone https://github.com/<your-username>/New-KPI-PowerAndAc.git
cd New-KPI-PowerAndAc
```

### 2. Run the backend

```bash
cd backend
dotnet restore
dotnet run
```

Update the database connection string in `appsettings.json` before running. If the project uses Entity Framework migrations, run:

```bash
dotnet ef database update
```

The API will start on the URL shown in the terminal (for example `https://localhost:5001`).

### 3. Run the frontend

```bash
cd frontend
npm install
ng serve
```

Open `http://localhost:4200/` in your browser. The app reloads automatically when you change a file.

## Build for Production

```bash
# Frontend
cd frontend
ng build

# Backend
cd backend
dotnet publish -c Release
```

## Branches

- `main`: stable, working version
- `kpi-new-update-features-sanduni`: new update features (work in progress)

## Contributing

1. Create a new branch from `main`
2. Make your changes
3. Commit with a clear message
4. Push the branch and open a pull request



## License

This project was created for academic purposes.

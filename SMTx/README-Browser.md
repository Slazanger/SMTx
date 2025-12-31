# Browser Support Setup

This application now supports running in the browser! Here's how to set it up:

## Overview

The application uses a platform-agnostic data service interface:
- **Desktop**: Uses SQLite database (`render.db`)
- **Browser**: Uses JSON files served via HTTP

## Setup Steps

### 1. Export Database to JSON

First, you need to export the database to JSON files that the browser can load:

```bash
cd SMTx/DataExporter
dotnet run
```

This will:
- Read from `DataExport/3142455/render.db`
- Export to `SMTx/SMTx.Browser/wwwroot/data/solar-systems.json`
- Export to `SMTx/SMTx.Browser/wwwroot/data/stargate-links.json`

### 2. Build and Run Browser Project

```bash
cd SMTx/SMTx.Browser
dotnet run
```

The browser project will:
- Serve the JSON files from `wwwroot/data/`
- Load them via HTTP when the app starts
- Display the map just like the desktop version

## Architecture

### Data Service Interface

- `IDataService`: Interface for loading solar systems and stargate links
- `SqliteDataService`: Desktop implementation using SQLite
- `JsonDataService`: Browser implementation using HTTP/JSON

### Platform Detection

The `App.axaml.cs` automatically detects the platform:
- `IClassicDesktopStyleApplicationLifetime` → Desktop → SQLite
- `ISingleViewApplicationLifetime` → Browser/Mobile → JSON

### Updating Data

When you update the database, re-run the DataExporter to regenerate the JSON files:

```bash
cd SMTx/DataExporter
dotnet run
```

The browser will automatically pick up the new data on the next build/run.

## Notes

- The JSON files are served as static assets from `wwwroot/data/`
- In the browser, file system access is not available, so HTTP is used
- The desktop version continues to use SQLite for better performance
- Both implementations use the same `MainViewModel` and rendering code


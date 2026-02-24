# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build entire solution
dotnet build Kajsmentkeri.sln

# Run the web application
dotnet run --project Kajsmentkeri.Web

# Apply pending EF Core migrations (two separate DbContexts)
dotnet ef database update --project Kajsmentkeri.Infrastructure --startup-project Kajsmentkeri.Web
dotnet ef database update --project Kajsmentkeri.Web --startup-project Kajsmentkeri.Web

# Add a new migration (Infrastructure context)
dotnet ef migrations add <MigrationName> --project Kajsmentkeri.Infrastructure --startup-project Kajsmentkeri.Web

# Run the import tool
dotnet run --project Kajsmentkeri.ImportTool

# Docker build
docker build -t kajsmentkeri:latest .
```

There are no automated tests in this project.

## Architecture

Clean architecture with four layers — domain entities have no outward dependencies; application services depend only on domain and interfaces; infrastructure implements those interfaces; the web project wires everything together.

```
Kajsmentkeri.Domain          → Core entities, no dependencies
Kajsmentkeri.Application     → Services + interfaces, depends on Domain
Kajsmentkeri.Infrastructure  → EF Core + PostgreSQL, depends on Application
Kajsmentkeri.Web             → ASP.NET Core Razor Pages, depends on all above
Kajsmentkeri.ImportTool      → Console CLI, depends on all above
```

### Two DbContexts

- **`AppDbContext`** (Infrastructure): Domain entities — championships, matches, predictions, audit logs, percentage predictions.
- **`ApplicationDbContext`** (Web): ASP.NET Core Identity tables only.

Both target the same PostgreSQL database (Neon cloud). Migrations for each context live in their respective project's `Migrations/` folder. `AppDbContext` uses `IDbContextFactory<AppDbContext>` throughout the application layer to manage connection lifetimes.

### Domain Model

- **Championship** owns **Matches** (1:N) and has a **ChampionshipScoringRules** (1:1). All cascade deletes are set to `Restrict`.
- **Match** owns **Predictions** (1:N). A prediction is scoped to one user per match (unique constraint on `UserId + MatchId`).
- **AppUser** extends `IdentityUser<Guid>` and adds `IsAdmin`.
- **PredictionAuditLog** records admin-made prediction edits (old/new scores, actor info).
- **ChampionshipWinnerPrediction** — one per user per championship (unique constraint).
- **PercentagePrediction** — one per user, holds 10 integer fields (0–100) for global event questions.

### Scoring

`ChampionshipScoringRules` stores configurable point values (exact score, correct winner, only-correct-winner bonus, rarity bonus, and championship winner/runner-up/third). `PredictionScoringService` recalculates all predictions for a match whenever an admin updates a result.

### Time Handling

`TimeService` encapsulates UTC ↔ Europe/Bratislava conversions. All timestamps in the database are stored in UTC; display uses Bratislava local time.

### Percentage Predictions

Configured in `appsettings.json` under `PercentagePredictions` — an ordered list of question objects. The questions themselves are stored in config, not in the database. The `PercentagePrediction` entity stores one row per user with `Question1`–`Question10` integer columns.

### Leaderboard & Line Graph

`LeaderboardService` computes ranked entries with cumulative points. `LineGraphViewModel` / `LineSeriesDto` represent per-user cumulative-points-over-matches data for chart rendering.

### Visibility Rules

Championships have `EnforceLeaderboardVisibilityRules`. When enabled, leaderboard/predictions of other users are hidden until the match starts (controlled in the web layer).

### Admin Capabilities

Pages under `Pages/Admin/` and guarded by `IsAdmin` on `AppUser`. Admins can import championships from Excel (`ImportService` uses `ExcelDataReader`), edit match scores (which triggers rescoring), and manage users.

# .NET Server Development Rules (Project P)

## 🎯 Core Philosophy
The backend is a stateless, high-performance API server. It is the sole authority for data validation, currency manipulation (Gold, Energy), and cheat prevention.

## 🏗️ Architecture & Framework
- **Framework:** .NET 8 ASP.NET Core Web API.
- **Routing:** Use **Minimal APIs** (`app.MapGet`, `app.MapPost` in `Program.cs` or modular extension methods). Do NOT use legacy MVC `ControllerBase` classes.
- **Dependency Injection (DI):** Strictly inject services, database connections, and configurations via the DI container. Never use tight coupling or static singletons for services.

## 💾 Database & ORM
- **Micro-ORM:** Use `Dapper` for SQL queries. Write raw, optimized SQL. Do NOT use heavy ORMs like Entity Framework for real-time game state queries.
- **Caching:** Use `Redis` for frequent state checks (e.g., Energy recovery validation, Session management) to reduce RDBMS load.

## ⏱️ Time & Security
- **Time Zone:** ALL date and time calculations (especially for Idle rewards and Energy recovery) MUST use `DateTime.UtcNow`. Never trust the client's local time.
- **Async Everywhere:** Every I/O operation (DB, Network) MUST be asynchronous using `async Task` and `await`.
- **Validation:** Always validate client inputs. Do not trust client-reported scores or combat results without verifying the time elapsed or the logic seed.
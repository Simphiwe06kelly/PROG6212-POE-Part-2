# RaceDay API (Part 2)

A RESTful API for managing running, walking and cycling events. Organisers create events and race categories, Participants enrol in categories, and Organisers capture results that Participants can view afterwards.

Built with **ASP.NET Core Web API (.NET 10)**, **Entity Framework Core (Code-First)** and **SQL Server**, with **Swagger** for live documentation and **xUnit** for testing.

The code for this part lives in the [`Part2-API`](Part2-API) folder.

## Video demonstration

[Watch the demo on YouTube (unlisted)](PASTE-YOUR-YOUTUBE-LINK-HERE)

## CI/CD status

The GitHub Actions workflow builds the project and runs all unit tests on every push.

![Green build](docs/ci-green-build.png)

## Table of contents

1. [Tech stack](#tech-stack)
2. [Project structure](#project-structure)
3. [Setup and run instructions](#setup-and-run-instructions)
4. [The two roles](#the-two-roles)
5. [Authentication and sessions](#authentication-and-sessions)
6. [Endpoints](#endpoints)
7. [Swagger](#swagger)
8. [Unit tests](#unit-tests)
9. [Differences from the Part 1 plan](#differences-from-the-part-1-plan)

## Tech stack

| Area | Technology |
|---|---|
| Framework | ASP.NET Core Web API (.NET 10, controllers) |
| Data access | Entity Framework Core 10, Code-First with migrations |
| Database | SQL Server (LocalDB for development) |
| Authentication | Server-side session (ASP.NET Core `Session`) |
| Password hashing | BCrypt (`BCrypt.Net-Next`) |
| API documentation | Swagger / Swashbuckle with XML comments |
| Testing | xUnit, `WebApplicationFactory`, EF Core InMemory provider |
| CI/CD | GitHub Actions |

## Project structure

```
.
├── .github/workflows/part2-ci.yml     GitHub Actions: build and test on every push
├── docs/                              Part 1 documents and the CI screenshot
└── Part2-API/
    ├── RaceDay.API.slnx               Solution file
    ├── RaceDay.Api/                   The API project
    │   ├── Attributes/                RequireRole attribute (role enforcement)
    │   ├── Controllers/               Auth, Users, Events, Categories, Enrolments, Results
    │   ├── Data/                      RaceDayDbContext (constraints and seed data)
    │   ├── DTOs/                      Request and response shapes
    │   ├── Helpers/                   Session helpers
    │   ├── Migrations/                EF Core migrations
    │   ├── Models/                    Entities matching the Part 1 ERD
    │   └── Program.cs                 Service configuration and middleware
    └── RaceDay.Api.Tests/             xUnit tests
        ├── Helpers/                   In-memory test factory and test helpers
        ├── AuthTests.cs
        ├── EventTests.cs
        ├── RoleAccessTests.cs
        └── EnrolmentTests.cs
```

## Setup and run instructions

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server. SQL Server Express LocalDB (installed with Visual Studio's *Data storage and processing* workload) is the easiest option.
- Visual Studio 2022 or later, or any editor plus the command line.

### 1. Clone the repository

```bash
git clone https://github.com/Simphiwe06kelly/YOUR-REPO-NAME.git
cd YOUR-REPO-NAME/Part2-API/RaceDay.Api
```

### 2. Check the connection string

`appsettings.json` points at LocalDB by default:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=RaceDayDB;Trusted_Connection=True;TrustServerCertificate=True"
}
```

If you use SQL Server Express or full SQL Server, change the `Server=` value to match your instance.

### 3. Create the database

The database is created from the EF Core migrations (Code-First). It matches the Part 1 ERD and SQL script, including the seed data.

```bash
dotnet tool install --global dotnet-ef
dotnet ef database update
```

In Visual Studio you can run `Update-Database` in the Package Manager Console instead.

### 4. Run the API

```bash
dotnet run --launch-profile https
```

Or press the green **https** button in Visual Studio. Then open Swagger in a browser:

```
https://localhost:7228/swagger
```

The port is set in `Properties/launchSettings.json`. If yours differs, use the address printed in the console.

### 5. Try it out

1. In Swagger, call **POST /api/auth/register** to create an Organiser and a Participant.
2. Call **POST /api/auth/login**. Swagger keeps the session cookie, so later requests are authenticated.
3. Call **POST /api/auth/logout** before switching between roles.

> **Seeded users cannot log in.** The four users from the Part 1 SQL script have placeholder password values (`HASHED_PASSWORD_1` and so on), matching the original script. Register new users to test login. The seeded users still own the seeded events, which is useful for testing ownership checks.

> **Sessions are held in server memory.** Restarting the API signs everyone out, so log in again after a restart.

### 6. Run the unit tests

```bash
cd Part2-API
dotnet test RaceDay.Api.Tests/RaceDay.Api.Tests.csproj
```

The tests use an in-memory database, so no SQL Server is needed.

## The two roles

Every user chooses a role when registering. The role is stored on the user and in the session after login.

### Organiser

Runs events. An Organiser can:

- Create, update and delete **their own** events
- Add, update and delete categories on **their own** events
- View all enrolments (the roster) for **their own** events
- Capture and correct results for enrolments in **their own** events
- View their profile, update it, and list all registered users

Organisers cannot enrol in events or view participant-only history.

### Participant

Takes part in events. A Participant can:

- Browse events, categories and public results (no login needed)
- Enrol in a category and cancel their **own** enrolment
- View their own enrolment history and their own results
- View and update their own profile

Participants cannot create or manage events, categories, rosters or results.

### How roles are enforced

| Situation | Response |
|---|---|
| Not logged in on a protected endpoint | `401 Unauthorized` |
| Logged in with the wrong role | `403 Forbidden` |
| Correct role but the event, enrolment or result belongs to someone else | `403 Forbidden` |

Role checks are done by the `[RequireRole(...)]` attribute. Ownership checks are done inside the controllers.

## Authentication and sessions

- `POST /api/auth/register` validates the input, rejects duplicate emails (`409`), hashes the password with **BCrypt** and stores only the hash. Passwords are never stored or returned in plain text.
- `POST /api/auth/login` verifies the password and then stores the user's **ID and role in a server-side session**. The browser receives only an HTTP-only session cookie.
- Protected endpoints read the user ID and role from the session. They never trust anything sent by the client.
- `POST /api/auth/logout` clears the session.

## Endpoints

All endpoints are visible and testable in Swagger, each with a description, expected input and responses.

### Authentication

| Method | Route | Role | Purpose |
|---|---|---|---|
| POST | `/api/auth/register` | Public | Create an Organiser or Participant account |
| POST | `/api/auth/login` | Public | Log in and start a session |
| POST | `/api/auth/logout` | Public | Clear the session (extra, not in the Part 1 plan) |

### Users

| Method | Route | Role | Purpose |
|---|---|---|---|
| GET | `/api/users/me` | Any logged-in user | View own profile |
| PUT | `/api/users/me` | Any logged-in user | Update own profile |
| GET | `/api/users` | Organiser | List all users |

### Events

| Method | Route | Role | Purpose |
|---|---|---|---|
| GET | `/api/events` | Public | List upcoming events |
| GET | `/api/events/{id}` | Public | Event detail with venue and categories |
| POST | `/api/events` | Organiser | Create an event |
| PUT | `/api/events/{id}` | Organiser (owner) | Update an event |
| DELETE | `/api/events/{id}` | Organiser (owner) | Delete an event |

### Categories

| Method | Route | Role | Purpose |
|---|---|---|---|
| GET | `/api/events/{eventId}/categories` | Public | List an event's categories |
| POST | `/api/events/{eventId}/categories` | Organiser (owner) | Add a category |
| PUT | `/api/categories/{id}` | Organiser (owner) | Update a category |
| DELETE | `/api/categories/{id}` | Organiser (owner) | Delete a category |

### Enrolments

| Method | Route | Role | Purpose |
|---|---|---|---|
| POST | `/api/enrolments` | Participant | Enrol in a category |
| GET | `/api/enrolments/mine` | Participant | Own enrolment history |
| GET | `/api/events/{eventId}/enrolments` | Organiser (owner) | Event roster |
| DELETE | `/api/enrolments/{id}` | Participant (owner) | Cancel own enrolment |

### Results

| Method | Route | Role | Purpose |
|---|---|---|---|
| POST | `/api/results` | Organiser (owner) | Capture a result |
| PUT | `/api/results/{id}` | Organiser (owner) | Correct a result |
| GET | `/api/events/{eventId}/results` | Public | Event leaderboard |
| GET | `/api/results/mine` | Participant | Own results |

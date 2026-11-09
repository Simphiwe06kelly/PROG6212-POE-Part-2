# RaceDay (Part 3: MVC web application)

RaceDay is an event management system for South African running, walking and cycling events. **Organisers** create events and race categories and capture results. **Participants** browse events, enter a category and see their race history.

This repository holds all three layers of the system:

| Layer | Folder | What it is |
|---|---|---|
| Web app (Part 3) | `RaceDay.Web` | ASP.NET Core MVC front end. It has no database and no business rules. Every page gets its data from the API. |
| API (Part 2, extended in Part 3) | `RaceDay.API` | ASP.NET Core Web API with EF Core and SQL Server. Part 3 added venues, event filters, a "my events" endpoint, finisher totals and **Azure Blob Storage** image uploads. |
| Tests | `RaceDay.Api.Tests` | xUnit tests that run the real API in memory. |

Part 3 work is on the `part-3-mvc` branch.

## Video demonstration

- **Part 3 (MVC app, Azure Blob Storage, Docker):** [Watch on YouTube (unlisted)](ADD-YOUR-PART-3-YOUTUBE-LINK-HERE)
- Part 2 (API): https://youtu.be/YLs8fmIuQgY

## CI/CD status

The GitHub Actions workflow (`.github/workflows/ci.yml`) runs on every push. It checks the repository structure, builds the API and the MVC web app, runs all unit tests, then builds the Docker image and starts the container.

![Green GitHub Actions build](docs/ci-green.png)

## Table of contents

1. [How the system fits together](#how-the-system-fits-together)
2. [Tech stack](#tech-stack)
3. [Project structure](#project-structure)
4. [Setup and run instructions](#setup-and-run-instructions)
5. [Run the web app in Docker](#run-the-web-app-in-docker)
6. [The two roles and the pages they see](#the-two-roles-and-the-pages-they-see)
7. [Authentication and sessions](#authentication-and-sessions)
8. [Azure Blob Storage](#azure-blob-storage)
9. [API endpoints](#api-endpoints)
10. [Unit tests](#unit-tests)
11. [Changes made to the API in Part 3](#changes-made-to-the-api-in-part-3)
12. [Differences from the Part 1 plan](#differences-from-the-part-1-plan)
13. [Known limitations](#known-limitations)
14. [Use of AI tools](#use-of-ai-tools)

## How the system fits together

```
 Browser ──► RaceDay.Web (MVC) ──► RaceDay.API ──► SQL Server
                                        │
                                        └────────► Azure Blob Storage (images)
```

- The browser only ever talks to the **MVC app**.
- The MVC app only ever talks to the **API**. It never touches the database or Azure.
- Images are uploaded from the MVC app **to the API**, and the API stores them in Azure Blob Storage and saves the URL in the database. The browser then shows each image straight from its Blob URL.

## Tech stack

| Area | Technology |
|---|---|
| Web app | ASP.NET Core MVC (.NET 10), Razor views, Bootstrap 5, Bootstrap Icons |
| API | ASP.NET Core Web API (.NET 10, controllers) |
| Data access | Entity Framework Core 10, Code-First with migrations |
| Database | SQL Server (LocalDB for development) |
| Authentication | Server-side sessions in both the API and the web app |
| Password hashing | BCrypt (`BCrypt.Net-Next`) |
| Image storage | Azure Blob Storage (`Azure.Storage.Blobs`) |
| API documentation | Swagger / Swashbuckle with XML comments |
| Testing | xUnit, `WebApplicationFactory`, EF Core InMemory provider |
| Containers | Docker (multi-stage Dockerfile) |
| CI/CD | GitHub Actions |

## Project structure

```
.
├── .github/workflows/ci.yml        Build, test and Docker check on every push
├── docker-compose.yml              One-command start for the web container
├── RaceDay.API.slnx                Solution file (API, tests and web app)
├── RaceDay.API/                    The API
│   ├── Attributes/                 RequireRole attribute (role enforcement)
│   ├── Controllers/                Auth, Users, Venues, Events, Categories, Enrolments, Results
│   ├── Data/                       RaceDayDbContext (constraints and seed data)
│   ├── DTOs/                       Request and response shapes
│   ├── Helpers/                    Session helpers and the image file validator
│   ├── Migrations/                 EF Core migrations
│   ├── Models/                     Entities matching the Part 1 ERD
│   ├── Services/                   Image storage (IImageStorage, Azure Blob implementation)
│   └── Program.cs                  Service configuration and middleware
├── RaceDay.Api.Tests/              xUnit tests
│   └── Helpers/                    In-memory test factory and a fake image storage
└── RaceDay.Web/                    The MVC web app
    ├── Controllers/                Home, Account, Events, Participant, Organiser
    ├── Filters/                    SessionAuthorize (role-based page access)
    ├── Helpers/                    Display formatting (dates, times, "47th of 312")
    ├── Models/                     API data shapes and form view models
    ├── Services/                   API client, session handling, cookie forwarding
    ├── Views/                      Razor views and shared layout
    ├── wwwroot/                    RaceDay theme (CSS) and script
    └── Dockerfile                  Container image for the web app
```

## Setup and run instructions

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server. SQL Server Express LocalDB (installed with Visual Studio's *Data storage and processing* workload) is the easiest option.
- Visual Studio 2022 or later, or any editor plus the command line.
- For Docker: [Docker Desktop](https://www.docker.com/products/docker-desktop/).
- For image uploads: an Azure Storage account (see [Azure Blob Storage](#azure-blob-storage)), or the free Azurite emulator.

### 1. Clone the repository and switch to the Part 3 branch

```bash
git clone https://github.com/Simphiwe06kelly/PROG6212-POE-Part-2.git
cd PROG6212-POE-Part-2
git checkout part-3-mvc
```

### 2. Check the database connection string

`RaceDay.API/appsettings.json` points at LocalDB by default:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=RaceDayDB;Trusted_Connection=True;TrustServerCertificate=True"
}
```

If you use SQL Server Express or full SQL Server, change the `Server=` value to match your instance.

### 3. Create the database

```bash
dotnet tool install --global dotnet-ef
dotnet ef database update --project RaceDay.API --startup-project RaceDay.API
```

In Visual Studio you can run `Update-Database` in the Package Manager Console instead.

### 4. Give the API your Azure Storage connection string

Never put the key in `appsettings.json`. Store it with .NET user-secrets:

```bash
dotnet user-secrets set "AzureBlob:ConnectionString" "<your storage account connection string>" --project RaceDay.API
```

In Visual Studio: right-click **RaceDay.Api → Manage User Secrets** and paste:

```json
{ "AzureBlob": { "ConnectionString": "<your storage account connection string>" } }
```

Without this, everything works except image uploads, which show a friendly "Image storage is unavailable" message.

### 5. Run the API and the web app

In Visual Studio: right-click the solution, choose **Configure Startup Projects**, select **Multiple startup projects**, set `RaceDay.Api` and `RaceDay.Web` to **Start**, then press **F5**.

Or from two terminals:

```bash
dotnet run --project RaceDay.API --launch-profile http     # API on http://localhost:5286
dotnet run --project RaceDay.Web --launch-profile http     # web app on http://localhost:5130
```

| App | Address |
|---|---|
| Web app | http://localhost:5130 (or https://localhost:7130) |
| API Swagger | http://localhost:5286/swagger |

The web app finds the API through `ApiSettings:BaseUrl` in `RaceDay.Web/appsettings.json` (default `http://localhost:5286/`).

### 6. Try it out

1. Open the web app and choose **Join RaceDay**. Register an **Organiser**, then log in.
2. Open **Venues** (three sample venues exist), then **Create event**, and add categories such as 5km and 10km.
3. Open a private/incognito window, register a **Participant** and click **Enter event** on that event.
4. As the Organiser, open **Enrolments** and **Results** for the event and capture the participant's finish time and positions.
5. As the Participant, open **My results** to see the finish time and position, for example "1st of 1 finishers".

> **Seeded users cannot log in.** The four users from the Part 1 SQL script have placeholder password values (`HASHED_PASSWORD_1` and so on). Register new users to test. The seeded users still own the sample events.

### 7. Run the unit tests

```bash
dotnet test RaceDay.Api.Tests/RaceDay.Api.Tests.csproj
```

The tests use an in-memory database and a fake image storage, so no SQL Server or Azure is needed.

## Run the web app in Docker

Only the web app is containerised. The API keeps running on your computer (step 5 above), and the container reaches it at `host.docker.internal`.

**1. Start the API** so it listens on `http://localhost:5286` (Visual Studio with the API as the startup project, or `dotnet run --project RaceDay.API --launch-profile http`).

**2. Build and start the container with one command:**

```bash
docker compose up --build
```

Then open **http://localhost:8080**.

Without Compose, the same thing as plain Docker commands:

```bash
docker build -t raceday-web ./RaceDay.Web
docker run --rm -p 8080:8080 -e ApiSettings__BaseUrl=http://host.docker.internal:5286/ raceday-web
```

The Dockerfile (`RaceDay.Web/Dockerfile`) follows these practices: a multi-stage build (SDK image to build, small ASP.NET runtime image to run), the project file is copied and restored first so package restore is cached, a `.dockerignore` keeps the build context small, the app runs as the non-root `app` user, and the API address is set with an environment variable so the container behaves exactly like the local app.

**If the container cannot reach the API**, run the API on all network interfaces instead:

```bash
dotnet run --project RaceDay.API --launch-profile http --urls http://0.0.0.0:5286
```

and allow the connection if Windows Firewall asks. Do not run the local web app and the container at the same time, because both use the same browser cookie name on `localhost`.

## The two roles and the pages they see

Every user chooses a role when registering. The role is stored on the user and in the server-side session after login. Each role has its own menu, and each role is turned away from the other's pages.

### Organiser

| Page | What it does |
|---|---|
| Dashboard | Summary of all the Organiser's events: total events, upcoming events, total enrolments, next event, and enrolments per event |
| My events | Create, edit and delete events (name, description, date, venue, type) and upload a **banner image** |
| Categories | Add, edit and delete the age or distance categories of an event |
| Enrolments | See every participant who entered an event, with their category and status |
| Results | Capture and correct finish times and positions for confirmed enrolments |
| Venues | List and add venues (an event's location) |
| Profile | Update name and email, upload a profile picture |

### Participant

| Page | What it does |
|---|---|
| Find events (home) | All upcoming events with filters: search, type, province and date range |
| Event details | Full information, banner, categories, fees, public results, and **Enter event** |
| My enrolments | Every event entered, with colour-coded status (green Confirmed, red Cancelled) and a cancel button |
| My results | Race history: event, date, category, finish time and position such as "47th of 312 finishers" |
| Profile | Update name and email, upload a profile picture |

Anyone can browse events and results without logging in.

## Authentication and sessions

- **Registering and logging in** happen through the web app, which calls the API. Passwords are hashed with BCrypt in the API and never stored or returned in plain text.
- **The API keeps a server-side session** holding the user's ID and role. The browser never sees the API's session cookie.
- **The web app keeps its own server-side session** holding the user's ID, role and name, and the web app's session cookie is the only cookie the browser receives. The web app also saves the API's session cookie inside its own session (`ApiSessionCookieHandler`) and attaches it to every call it makes to the API for that user.
- **Menus and pages** are decided from the web session. `[SessionAuthorize]` redirects visitors who are not logged in to the login page (and back afterwards) and sends the wrong role to an "access denied" page. The API checks the role and ownership again on every call.
- **Logging out** ends the API session and the web session, then redirects to the login page.
- If the API has forgotten the user (for example after an API restart), the web app clears its session and redirects to the login page with a message.

## Azure Blob Storage

Event banners and profile pictures are stored in an Azure Blob Storage container called `raceday-images`.

1. The MVC app sends the chosen file to the API (`POST /api/events/{id}/banner` or `POST /api/users/me/picture`).
2. The API checks the file is really a JPG, PNG, GIF or WEBP image of at most 5 MB (it inspects the file's first bytes, not just its name).
3. The API uploads it with `BlobImageStorage` and saves the public URL on the event (`BannerUrl`) or user (`ProfilePictureUrl`).
4. The MVC app shows the image from that URL on the event cards, the event detail page, the menu and the profile page.

Azure setup needed once:

- Create a **Storage account** (Standard, LRS).
- Under **Settings → Configuration**, set **Allow Blob anonymous access** to **Enabled**. The API creates the container itself, with public read access for blobs only.
- Copy the **connection string** from **Security + networking → Access keys** into user-secrets (setup step 4).

For local development without an Azure account, the default connection string `UseDevelopmentStorage=true` targets the Azurite emulator.

## API endpoints

All endpoints are visible and testable in Swagger, each with a description, expected input and responses.

### Authentication

| Method | Route | Role | Purpose |
|---|---|---|---|
| POST | `/api/auth/register` | Public | Create an Organiser or Participant account |
| POST | `/api/auth/login` | Public | Log in and start a session |
| POST | `/api/auth/logout` | Public | Clear the session |

### Users

| Method | Route | Role | Purpose |
|---|---|---|---|
| GET | `/api/users/me` | Any logged-in user | View own profile |
| PUT | `/api/users/me` | Any logged-in user | Update own profile |
| POST | `/api/users/me/picture` | Any logged-in user | Upload a profile picture (**Part 3**) |
| GET | `/api/users` | Organiser | List all users |

### Venues (Part 3)

| Method | Route | Role | Purpose |
|---|---|---|---|
| GET | `/api/venues` | Public | List venues |
| POST | `/api/venues` | Organiser | Add a venue |

### Events

| Method | Route | Role | Purpose |
|---|---|---|---|
| GET | `/api/events` | Public | List upcoming events. Optional filters (Part 3): `type`, `province`, `search`, `from`, `to` |
| GET | `/api/events/mine` | Organiser | The Organiser's own events with enrolment counts (**Part 3**) |
| GET | `/api/events/{id}` | Public | Event detail with venue and categories |
| POST | `/api/events` | Organiser | Create an event |
| PUT | `/api/events/{id}` | Organiser (owner) | Update an event |
| POST | `/api/events/{id}/banner` | Organiser (owner) | Upload the event banner image (**Part 3**) |
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
| GET | `/api/results/mine` | Participant | Own results, with event date and `totalFinishers` |

## Unit tests

There are **100+ unit tests** (see Test Explorer for the exact count), all passing locally and in GitHub Actions. They start the real API in memory using `WebApplicationFactory` and the EF Core InMemory provider, with a fake image storage so Azure is never contacted. Every test registers its own users and uses its own cookie session, so tests do not depend on each other.

| Test file | What it covers |
|---|---|
| `AuthTests.cs` | Registration success and failure, password hashing, login, session lifetime and logout |
| `EventTests.cs` | Create, read, update and delete events, ownership checks, validation, adding categories |
| `RoleAccessTests.cs` | Organiser-only endpoints reject Participants and the other way round, protected endpoints reject anonymous callers, public endpoints work without login |
| `EnrolmentTests.cs` | Enrolling, duplicates, full categories, past events, cancelling and re-enrolling, rosters, results |
| `ImageUploadTests.cs` | Banner and profile picture uploads: success, wrong file type, oversized files, missing files, wrong role, wrong owner, not logged in |
| `VenueTests.cs` | Listing and creating venues, role checks, validation |
| `EventQueryTests.cs` | Event filters (type, province, search, dates), the Organiser's "my events" dashboard data |
| `ResultTotalsTests.cs` | Event date and finisher totals on results ("47th of 312") |

### CI workflow

`.github/workflows/ci.yml` runs on every push and pull request:

1. **Build and test job:** checks the expected solution, projects, folders and Dockerfile exist, sets up .NET 10, restores and builds the API and the MVC web app, then runs all unit tests.
2. **Docker job:** builds the web app's Docker image and starts the container, checking that it serves pages.

## Changes made to the API in Part 3

| Change | Why |
|---|---|
| `GET/POST /api/venues` | An event needs a venue (its location), so the web app needs to list and add venues |
| `GET /api/events/mine` | The Organiser dashboard needs the Organiser's own events, including past ones, with enrolment counts |
| Filters on `GET /api/events` | The Participant home page filters events |
| `POST /api/events/{id}/banner`, `POST /api/users/me/picture` | Image uploads stored in Azure Blob Storage |
| `BannerUrl` on events, `ProfilePictureUrl` on users (new migration) | Saves the image URL in the database |
| `eventDate` and `totalFinishers` on results | "My results" shows the event date and a position such as "47th of 312 finishers" |

## Differences from the Part 1 plan

| Part 1 plan | What was built | Why |
|---|---|---|
| Login returns a JWT: `{ token, role, userId }` | Login starts a server-side session and returns `{ userId, role, message }` | The brief asks for session management with the user's ID and role held server-side |
| No logout endpoint | Added `POST /api/auth/logout` | Needed to end a session and to test switching roles |
| Deleting an enrolment | The enrolment is kept and its status is set to `Cancelled`; enrolling again re-activates it | Keeps history, and works with the unique participant + category constraint from the ERD |
| Enrolling only needs a valid category | Enrolling also rejects full categories and events that have already taken place | Enforces `MaxParticipants` and prevents enrolling in past events |
| Deleting events and categories | Rejected with `409` if they already have enrolments | Protects enrolment and result data |
| `POST /api/results` listed without `403` | Returns `403` if the Organiser does not own the event | Organisers should only capture results for their own events |

## Known limitations

- **Sessions are held in server memory.** Restarting the API or the web app signs users out. The web app detects this and sends the user to the login page.
- **Enrolment statuses are Confirmed and Cancelled only**, as defined by the Part 1 database constraint, so the interface colours Confirmed green and Cancelled red.
- **Seeded users have placeholder passwords** and cannot log in. Register new users.
- The Docker container runs only the web app. The API and SQL Server run on the host machine.

## Use of AI tools

> **Edit this section so it is true for you, and check your institution's rules on AI assistance.** If AI tools helped with this work, state which tools, what they helped with, and confirm that you reviewed, tested and understand the code. Example: *"I used an AI assistant (Claude) to help plan and write parts of the Part 3 code. I reviewed, tested and can explain all of the code in this repository."*

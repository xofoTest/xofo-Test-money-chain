# Money Chain SAS — Bilingual Member-Management Prototype

A responsive, bilingual (English / മലയാളം) member-management web application with a
three-child placement tree, sponsor/placement separation, a disabled-by-default reward
simulation, and a role-gated admin panel.

> **Prototype notice.** This application uses **fictional sample data only**. The
> recruitment-based compensation model requires legal review in India before any
> financial feature can be enabled. **No real payments, payouts or rewards are active
> anywhere in this app — and no withdrawal feature exists.** Every financial figure is
> labelled `SIMULATED` / `DEMO DATA — NOT REAL MONEY`.

## Stack

| Layer     | Technology                                                            |
|-----------|-----------------------------------------------------------------------|
| Frontend  | React 18 + TypeScript, Vite, Tailwind CSS, React Router, React Hook Form + Zod, Lucide |
| Backend   | ASP.NET Core (minimal APIs), JWT bearer auth, EF Core                  |
| Database  | **SQLite** for the runnable local prototype; **PostgreSQL** schema + RLS ship in `database/migrations/` |
| Tests     | xUnit (backend), Vitest (frontend)                                     |

Currency is stored as **integer paise** (1 INR = 100 paise) to avoid floating-point money math.

## Directory layout

```
to-money/
├── frontend/                  React SPA (16 screens)
│   └── src/
│       ├── lib/               i18n (en/ml), api client, auth, money helpers
│       ├── components/        Shell, placement Tree, shared UI
│       └── pages/             Landing, auth, member and admin screens
├── backend/
│   ├── MoneyChain.Api/        ASP.NET Core API, services, seeder
│   └── MoneyChain.Tests/      xUnit unit + integration tests
├── database/migrations/       PostgreSQL schema with RLS + constraints
└── .env.example               placeholder configuration
```

## Run locally

### 1. Backend (API + seeded demo data)

```powershell
cd backend/MoneyChain.Api
dotnet run --urls http://localhost:5200
```

On startup the idempotent seeder creates the SQLite database (`moneychain.db`) and
populates it with fictional members, sponsorships, placements, registration records and
a reward ledger. Re-running never duplicates data.

### 2. Frontend (dev server, proxies `/api` to the backend)

```powershell
cd frontend
npm install
npm run dev          # http://localhost:5173
```

### Single-process run (production build)

```powershell
cd frontend && npm run build        # outputs frontend/dist
cd ../backend/MoneyChain.Api
dotnet run --urls http://localhost:5200   # serves the built SPA at http://localhost:5200
```

### Demo accounts (local demo mode only — never for production)

| Role   | Email                    | Password    |
|--------|--------------------------|-------------|
| Member | `arun.nair@example.com`  | `Demo1234`  |
| Admin  | `admin@moneychain.demo`  | `Admin@1234`|

All seeded members share the password `Demo1234`. Override via `Demo:MemberPassword`
and `Demo:AdminPassword` configuration.

## Tests

```powershell
# Backend (unit + integration, 15 tests)
cd backend
dotnet test MoneyChain.Tests/MoneyChain.Tests.csproj

# Frontend (Vitest)
cd frontend
npm test
```

Covered: login/authorization, member vs admin access, three-child placement limit,
sponsor vs placement separation, cycle/self-parent/duplicate prevention, concurrent
placement attempts, duplicate simulated-reward prevention, paise currency math, audit
logging, and that live payments/payouts stay disabled.

## Disabled financial features

- `reward_simulation_enabled` defaults to `false`; the admin may enable it locally to
  produce deterministic, fictional ledger entries (₹100 one-time per sponsor after the
  third qualifying direct member; ₹10 when a direct placement child completes three
  placements — trigger is an explicit configuration field, not an assumption).
- `live_payments` and `live_payouts` are **hard-disabled** and can never be enabled from
  the UI or API. `withdrawal_requests` exists as a schema-only table with no creation
  path.
- Reward-ledger rows are immutable; reversals are written as new negative entries with
  an audit trail.

## PostgreSQL (production target)

The canonical schema, constraints (unique indexes, three-child trigger, cycle/self-parent
guards) and row-level-security policies are in
[`database/migrations/001_schema.sql`](database/migrations/001_schema.sql). The local
prototype uses SQLite (which has no RLS); the application-level authorization still
applies everywhere, and RLS is enforced when the schema is deployed to PostgreSQL.

## Configuration

Copy `.env.example` to your environment. All values are placeholders — no real
credentials or API keys are included in this repository.

# E2E API Test Suite

This project contains automated API-level tests for Dynamics 365 / Dataverse integrations.
It’s designed for cross-platform local execution (Mac, Windows, Linux) and CI/CD pipelines.
The suite focuses on modularity, reusability, and clean layering between configuration, infrastructure, and test logic.

## Folder Structure

/tests
├── e2e_api
│ ├── Tests/ # Test classes (MSTest)
│ ├── Config/ # Strongly-typed configuration objects and defaults
│ ├── Fixtures/ # Reusable test data builders for setup/cleanup
│ ├── Services/ # Domain-level abstractions per entity
│ ├── Util/ # Core infrastructure and shared utilities
│ ├── appsettings.json # Non-secret configuration (timeouts, token buffer, etc.)
│ ├── .env # Local secrets (Dataverse URL, Tenant, Client ID/Secret)
│ ├── E2E.Api.Tests.csproj # Test project definition

## Layered Architecture

| Layer                     | Responsibility                                                | Example Classes                               |
| ------------------------- | ------------------------------------------------------------- | --------------------------------------------- |
| **Config**                | Centralized, typed configuration (auth, token, entity names). | `AuthConfig`, `TokenConfig`, `EntityNames`    |
| **Util (Infrastructure)** | Generic Dataverse HTTP client and shared helpers.             | `DataverseClient`, `TokenCache`, `TestConfig` |
| **Services (Domain)**     | Entity-specific logic built on top of DataverseClient.        | `CaseService`, `UsageFormService`             |
| **Fixtures (Data setup)** | Test data orchestration across entities.                      | `CaseFixture`                                 |
| **Tests**                 | Actual test cases and assertions.                             | `CaseTests`, `UsageFormTests`                 |

This mirrors production-grade layering (Infra → Domain → Test) and keeps each concern cleanly separated.

## Environment Configuration

| File                | Purpose                                                                                                                                               |
| ------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| `.env`              | **Local secrets** — not checked into source control. Contains:<br>`DYNAMICS_URL`, `DATAVERSE_TENANT`, `DATAVERSE_CLIENTID`, `DATAVERSE_CLIENTSECRET`. |
| `appsettings.json`  | **Non-sensitive config** shared across environments (e.g., token buffer, timeouts).                                                                   |
| `ConfigDefaults.cs` | Hardcoded defaults when no value is provided.                                                                                                         |

In CI/CD, `.env` is **not required** — environment variables override configuration automatically.

---

## Running Tests

You can build locally to check for errors with

```bash
dotnet build e2e_api/E2E.Api.Tests.csproj
```

You can run tests locally with

```bash
dotnet test e2e_api/E2E.Api.Tests.csproj
```

## Extending the Suite

### Add a new entity

Add a constant to EntityNames.cs.

Create a new service in /Services (e.g. PhysicianService.cs).

(Optional) Add a fixture if the entity is often used in test setup.

Write test cases in /Tests.

### Add new configuration

Create a new config class (e.g. TimeoutConfig.cs).

Add defaults in ConfigDefaults.cs.

Extend TestConfig to include it.

## Security Notes

.env must never be committed — it contains credentials.

All secrets are injected via environment variables in CI/CD.

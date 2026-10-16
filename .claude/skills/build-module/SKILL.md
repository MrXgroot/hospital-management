---
name: build-module
description: Build or extend one HMS module (patients, doctors, appointments, consultations, prescriptions, billing, dashboard) as a full vertical slice from DB to UI, following the project specs. Use when asked to implement a module or feature end to end.
---

# Build an HMS module as a vertical slice

Argument: the module name, optionally followed by the specific feature (e.g. `appointments reschedule`).

## 1. Load the spec
- Read the module's section in `docs/FEATURES.md` (requirements + rules).
- Read its tables in `docs/DATABASE.md` and its endpoints in `docs/API.md`.
- List any ambiguity or conflict between the docs and ask the user before coding.

## 2. Backend
1. **Domain** (`HMS.Domain/<Module>/`): entities, enums, behaviour methods (status transitions, invariants).
2. **Infrastructure**: `IEntityTypeConfiguration<T>` matching DATABASE.md exactly (lengths, indexes, FKs,
   generated columns). Add `DbSet` to `HmsDbContext` and `IAppDbContext`.
3. **Migration**: `dotnet ef migrations add <Descriptive>`. Then run `dotnet ef migrations script` and check
   the SQL against DATABASE.md (index names, `ON DELETE RESTRICT`, check constraints).
4. **Application**: request/response records, validators, service interface + implementation, mappings.
5. **API**: controller with role policies from API.md. Map errors to the documented codes.
6. **Tests**: unit tests for every rule in FEATURES.md. Integration tests for concurrency, unique-index,
   and auth paths.
7. Run `dotnet build` and `dotnet test`. Fix until both pass.

## 3. Frontend
1. `src/types/<module>.ts` and `src/api/<module>.ts` mirroring API.md.
2. `features/<module>/hooks`: query key factory + query/mutation hooks.
3. `features/<module>/schemas`: Zod schemas mirroring the backend validators.
4. Pages and components, then register the routes with role guards and add the nav items.
5. Tests for forms and the main flow (MSW).
6. Run `npm run typecheck`, `npm run lint`, and `npm run test`.

## 4. Finish
- Update FEATURES.md, DATABASE.md, and API.md if anything changed during implementation.
- Tick the module's items in `docs/ROADMAP.md`.
- Summarise for the user what was built, what was tested, and any open questions or deviations.

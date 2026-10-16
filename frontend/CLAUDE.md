# Frontend (React + TypeScript + Vite)

Read the root [CLAUDE.md](../CLAUDE.md) first. Specs: [FEATURES.md](../docs/FEATURES.md), [API.md](../docs/API.md),
[CODING_STANDARDS.md](../docs/CODING_STANDARDS.md#react--frontend).

## Commands (from `frontend/`)
```bash
npm run dev        # http://localhost:5173, /api proxied to http://localhost:5001 (start the API first)
npm run typecheck
npm run lint
npm run test
npm run build
```

## Structure
```
src/
  api/client.ts       # axios instance, bearer token, single-flight refresh on 401, logout event
  api/endpoints.ts    # typed API functions for every module (authApi, patientsApi, ...)
  api/queryKeys.ts    # TanStack Query key factories; invalidate by the broadest matching key
  api/errors.ts       # ProblemDetails parsing, code → friendly message, applyFieldErrors for RHF
  types/api.ts        # DTO types mirroring API.md (camelCase). Keep in sync with the backend records
  auth/               # AuthProvider (session in localStorage: tokens + own profile only), route guards
  components/         # AppLayout (role-filtered nav), common.tsx (PageHeader, Section, StatusChip, dialogs…),
                      # form.tsx (FormText/FormSelect bound to RHF), Notifications (snackbar)
  features/<module>/  # pages + module components (appointments/components.tsx holds SlotPicker, actions)
  routes.tsx          # lazy routes with role guards; /print/... routes render without the app shell
  utils/format.ts     # money (INR), dates/times, small converters
  test/               # Vitest setup and tests
```

## Screens
| Route | Screen | Roles |
|---|---|---|
| `/login` | Login | public |
| `/` | Dashboard (KPIs, waiting queue, available doctors, quick links) | all |
| `/queue` | Today's queue, auto-refresh 30 s, role-aware actions | all |
| `/appointments`, `/appointments/book` | Day list + actions; booking (patient → doctor/type → slot) | all / A R |
| `/patients`, `/patients/new`, `/patients/:id`, `/patients/:id/edit` | Search, register, detail tabs | all / A R |
| `/doctors`, `/doctors/new`, `/doctors/:id`, `/doctors/:id/edit` | List, profile, schedule editor, leave | all / A |
| `/consultations/:id` | Vitals, clinical notes, prescription editor, previous visits, change history | A D N |
| `/print/prescriptions/:id` | Printable prescription (A4) | all |
| `/billing`, `/billing/invoices/:id` | Invoices, demo "Mark as paid" | A R |
| `/admin/users`, `/admin/masters` | Users, departments, specializations, medicines | A |

## Rules of thumb
- Forms keep values as strings and convert on submit (`toXxxRequest`). Avoid `z.coerce` (input/output types diverge).
- Status colours come only from `StatusChip`.
- After `409 SLOT_UNAVAILABLE`, invalidate that doctor's slots and keep the rest of the form.
- `409 DUPLICATE_PATIENT` shows the matches, with "Create anyway" (resubmits with `confirmDuplicate=true`).
- Edits to finalised consultations/prescriptions go through `ReasonDialog` (amendment reason).
- `TextField select` must always get children: use `(query.data ?? []).map(...)`.
- Typography renders `<p>`; use `component="div"` when it wraps chips/stacks (invalid nesting otherwise).
- No MUI X: dates/times use native `type="date"`/`type="time"` inputs; lists use MUI `Table`.

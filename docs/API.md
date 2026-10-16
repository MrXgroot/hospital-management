# REST API Contract

Base path: `/api`. JSON uses camelCase; enums are strings (`"CheckedIn"`).
Validation errors use JSON paths as keys (`items[0].dosage`). `GET /health` is anonymous. Dates are `YYYY-MM-DD`, times `HH:mm`, and timestamps ISO-8601 UTC
(`2026-10-16T05:30:00Z`).
All endpoints require `Authorization: Bearer <token>` unless marked **public**.

## Conventions

**Pagination** (list endpoints): `?page=1&pageSize=20` (max 100). Response:
```json
{ "items": [ ... ], "page": 1, "pageSize": 20, "totalCount": 134 }
```

**Errors**: RFC 7807 ProblemDetails with a stable `code`:
```json
{
  "type": "https://hms/errors/slot-unavailable",
  "title": "The selected slot is no longer available.",
  "status": 409,
  "code": "SLOT_UNAVAILABLE",
  "traceId": "00-...",
  "errors": { "startTime": ["..."] }
}
```

| Status | When | Example codes |
|---|---|---|
| 400 | Malformed request / validation failure | `VALIDATION_FAILED` |
| 401 | Missing/expired token | `UNAUTHENTICATED` |
| 403 | Role or ownership check failed | `FORBIDDEN` |
| 404 | Entity not found (or soft-deleted) | `NOT_FOUND` |
| 401 | Login/refresh failed | `INVALID_CREDENTIALS`, `INVALID_REFRESH_TOKEN` |
| 409 | Conflict | `SLOT_UNAVAILABLE`, `DUPLICATE_PATIENT`, `CONCURRENCY_CONFLICT`, `PRESCRIPTION_EXISTS`, `USERNAME_TAKEN` |
| 422 | Business rule violated | `INVALID_STATUS_TRANSITION`, `DOCTOR_INACTIVE`, `DOCTOR_HAS_FUTURE_APPOINTMENTS` (+ `appointments[]`), `SLOT_NOT_IN_SCHEDULE`, `SLOT_IN_PAST`, `DATE_OUT_OF_RANGE`, `PATIENT_ALREADY_BOOKED`, `NO_SLOTS_AVAILABLE`, `WALK_IN_TODAY_ONLY`, `CHECK_IN_TODAY_ONLY`, `NO_SHOW_TOO_EARLY`, `CHIEF_COMPLAINT_REQUIRED`, `CONSULTATION_FINALISED`, `AMENDMENT_REASON_REQUIRED`, `PRESCRIPTION_EMPTY`, `INVOICE_NOT_UNPAID`, `INVOICE_ALREADY_PAID`, `PATIENT_HAS_CONSULTATIONS` |

**Concurrency**: entities with `rowVersion` return it in GET responses. PUT/PATCH must send it back.

## Endpoints

Roles: A = Admin, R = Receptionist, D = Doctor, N = Nurse.

### Auth
| Method | Path | Roles | Notes |
|---|---|---|---|
| POST | `/auth/login` | **public** | `{username, password}` → `{accessToken, refreshToken, expiresAt, user}` |
| POST | `/auth/refresh` | **public** | `{refreshToken}` → new pair (rotation) |
| POST | `/auth/logout` | any | revokes the refresh token |
| GET | `/auth/me` | any | current user + role + doctorId |
| GET/POST/PUT | `/users`, `/users/{id}` | A | user management |

### Dashboard
| Method | Path | Roles | Notes |
|---|---|---|---|
| GET | `/dashboard/summary` | A R D N | `{ totalPatients, todayAppointments: {total, byStatus}, availableDoctors: [...], waitingQueue: [...], todayRevenue, pendingPayments }`. Doctor → scoped to self. |

### Patients
| Method | Path | Roles | Notes |
|---|---|---|---|
| GET | `/patients?search=&page=&pageSize=` | A R D N | search by MRN / phone / name (see FEATURES) |
| POST | `/patients?confirmDuplicate=false` | A R | → 201 with `mrn`; 409 `DUPLICATE_PATIENT` with `matches[]` |
| GET | `/patients/{id}` | A R D N | demographics + allergies + conditions |
| PUT | `/patients/{id}` | A R | MRN not updatable |
| DELETE | `/patients/{id}` | A | soft delete; 422 if consultations exist |
| GET | `/patients/{id}/visits` | A R D N | consultations summary list (paginated) |
| GET | `/patients/{id}/appointments?upcoming=true` | A R D N | |
| POST/PUT/DELETE | `/patients/{id}/allergies[/{allergyId}]` | A R D N | |
| POST/PUT/DELETE | `/patients/{id}/conditions[/{conditionId}]` | A D | |

### Master data
| Method | Path | Roles | Notes |
|---|---|---|---|
| GET | `/departments`, `/specializations`, `/medicines?search=` | any | |
| POST/PUT | `/departments[/{id}]`, `/specializations[/{id}]`, `/medicines[/{id}]` | A | |

### Doctors
| Method | Path | Roles | Notes |
|---|---|---|---|
| GET | `/doctors?departmentId=&specializationId=&isActive=&search=` | any | |
| POST | `/doctors` | A | generates `doctorCode` |
| GET / PUT | `/doctors/{id}` | any / A | |
| PATCH | `/doctors/{id}/status` | A | `{isActive}`; 422 + list if future appointments exist |
| GET / PUT | `/doctors/{id}/schedule` | any / A | PUT replaces the whole weekly schedule |
| GET / POST / DELETE | `/doctors/{id}/leaves[/{leaveId}]` | any / A / A | |
| GET | `/doctors/{id}/slots?date=2026-10-20` | A R D N | `[{startTime, endTime, isAvailable}]` |
| GET | `/doctors/available-today` | any | |

### Appointments
| Method | Path | Roles | Notes |
|---|---|---|---|
| GET | `/appointments?date=&doctorId=&patientId=&status=` | A R D N | |
| POST | `/appointments` | A R | `{patientId, doctorId, appointmentDate, startTime, appointmentType, reasonForVisit, followUpOfConsultationId?}`. `WalkIn` with `startTime: null` takes the next free slot today and checks in |
| GET | `/appointments/{id}` | A R D N | includes status history |
| PUT | `/appointments/{id}/reschedule` | A R | `{appointmentDate, startTime, rowVersion}` |
| POST | `/appointments/{id}/confirm` | A R | |
| POST | `/appointments/{id}/check-in` | A R N | today only; assigns `queueToken`, creates invoice |
| POST | `/appointments/{id}/cancel` | A R | `{reason}`; also cancels an unpaid invoice |
| POST | `/appointments/{id}/no-show` | A R | |
| GET | `/appointments/queue?date=&doctorId=` | A R D N | ordered daily queue |

### Consultations
| Method | Path | Roles | Notes |
|---|---|---|---|
| POST | `/consultations` | D | `{appointmentId}` (or `{patientId}` for walk-in) → Draft; appointment → InConsultation |
| GET | `/consultations/{id}` | A D N | includes symptoms, vitals, prescription summary |
| PUT | `/consultations/{id}` | D (owner) A | Draft edit, or amendment (requires `amendmentReason` when Finalised) |
| POST | `/consultations/{id}/finalise` | D (owner) | validates chief complaint; appointment → Completed |
| GET | `/consultations/{id}/history` | A D | revisions with field diffs |
| POST | `/consultations/{id}/vitals` | D N | |
| PUT | `/consultations/{id}/vitals/{vitalsId}` | D N | writes a revision |
| GET | `/patients/{id}/vitals?from=&to=` | A D N | trend data |

### Prescriptions
| Method | Path | Roles | Notes |
|---|---|---|---|
| POST | `/consultations/{id}/prescription` | D (owner) | creates Draft with items |
| GET | `/prescriptions/{id}` | A D N R | |
| PUT | `/prescriptions/{id}` | D (owner) | replaces items; amendment reason if Finalised |
| POST | `/prescriptions/{id}/finalise` | D (owner) | ≥1 item required |
| GET | `/prescriptions/{id}/print` | A D N R | print-ready DTO (hospital header, doctor, patient, vitals, items) |
| GET | `/prescriptions/{id}/history` | A D | |
| GET | `/patients/{id}/prescriptions` | A D N R | |

### Billing
| Method | Path | Roles | Notes |
|---|---|---|---|
| GET | `/invoices?status=&date=&patientId=` | A R | paginated |
| GET | `/invoices/{id}` | A R | items + payments |
| POST | `/invoices/{id}/mark-paid` | A R | `{method: Cash/Card/UPI/Other, referenceNo?}`: demo payment of the full balance |
| POST | `/invoices/{id}/cancel` | A | `{reason}`; unpaid invoices only |

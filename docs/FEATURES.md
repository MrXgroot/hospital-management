# Features and Business Rules

This is the source of truth for **behaviour**. Each module lists its requirements (from the project brief),
then the business rules and acceptance criteria to implement and test against.

## Roles

| Role | Can do |
|---|---|
| `Admin` | Everything, including user management, doctor management, and viewing all reports |
| `Receptionist` | Register/update patients, book/reschedule/cancel appointments, check in, take payments |
| `Doctor` | View own queue, conduct consultations, record diagnosis and notes, write prescriptions |
| `Nurse` | Record vital signs, view the queue and patient details |

A `Doctor` user is linked 1:1 to a `doctors` row (`users.doctor_id`).

---

## 1. Dashboard

**Requirements**
- Total patients and today's appointments
- Available doctors
- Patients waiting for consultation
- Today's revenue and pending payments
- Quick links to register patients and book appointments

**Rules**
- "Total patients": count of active patients (`is_deleted = 0`).
- "Today's appointments": appointments with `appointment_date = today (hospital TZ)`, excluding `Cancelled`.
  Show a breakdown by status.
- "Available doctors": active doctors with a schedule covering today's weekday who are not on leave today.
- "Waiting for consultation": today's appointments with status `CheckedIn`, ordered by check-in time.
- "Today's revenue": sum of `payments.amount` received today (hospital TZ).
- "Pending payments": sum of `invoices.balance_due` where status is `Unpaid`.
- A `Doctor` sees the dashboard scoped to their own appointments and queue.
- One endpoint returns all dashboard figures (`GET /api/dashboard/summary`). It must stay fast (< 300 ms on
  the dev dataset), using aggregate queries and no N+1 queries.

---

## 2. Patient Management

**Requirements**
- Register new patients
- Generate a unique patient ID
- Store name, age/date of birth, gender, contact details, and address
- Search by patient ID, name, or phone number
- View and update patient details
- View previous visits and medical history

**Rules**
- **Patient ID (MRN)** format: `P{YYYY}{6-digit sequence}`, for example `P2026000123`. The sequence resets
  each year. It is generated server-side in the same transaction as the insert, using the `id_sequences`
  table with a row lock (`SELECT ... FOR UPDATE`). It is immutable once assigned.
- Required: first name, last name, gender, phone, and **either** date of birth **or** age in years.
  If only age is given, store `date_of_birth = NULL`, `age_years` = age, and `age_recorded_on = today`, and
  compute the current age on display.
- Phone: 10–15 digits, optional leading `+`. Normalise to digits (keeping the `+`) before saving.
- Duplicate warning (not a block): if the same phone and same first name already exist, return a
  `409` with the matching patients so the user can confirm. Re-submitting with `confirmDuplicate=true`
  creates the patient anyway.
- Search: one box. If the input matches the MRN pattern, search by exact MRN. If it is all digits, search by
  phone prefix. Otherwise search by name prefix (first or last). Results are paginated, max 50 per page.
- Updates are audited (`audit_logs`). The MRN and created date can't be changed.
- Patient detail page shows: demographics, upcoming appointments, a visit history list (consultations,
  newest first), and allergies and chronic conditions.
- Patients are soft-deleted (Admin only), and only if they have no consultations.

---

## 3. Doctor Management

**Requirements**
- Add and update doctor profiles
- Specialization and department
- Consultation fees
- Availability and working hours
- Active/inactive status

**Rules**
- Departments and specializations are master tables, managed by Admin.
- A doctor has one department and one or more specializations (one is marked primary).
- `consultation_fee` ≥ 0, `DECIMAL(10,2)`. A fee change applies to **new** invoices only. Existing
  invoices keep the fee they were created with.
- **Weekly schedule**: rows of (weekday, start_time, end_time, slot_duration_minutes). A doctor may have
  multiple blocks per day (e.g. 09:00–13:00 and 17:00–20:00). Blocks on the same day must not overlap.
- **Leave / unavailability**: date ranges when no slots are offered.
- Inactive doctors: hidden from booking, but their history stays visible. You can't deactivate a doctor who
  has future `Booked`/`Confirmed` appointments until those are rescheduled or cancelled (the API returns the
  list).
- Doctor code format: `D{4-digit sequence}`, e.g. `D0012`.

---

## 4. Appointment Management

**Requirements**
- Book appointments for patients
- Select doctor, date, and time slot
- Reschedule or cancel appointments
- Track booked, confirmed, completed, and cancelled appointments
- Prevent conflicting bookings for the same doctor
- Display the daily appointment queue

**Status lifecycle**

```
Booked ──► Confirmed ──► CheckedIn ──► InConsultation ──► Completed
  │            │             ▲  │
  │            │             │  └──► Cancelled
  └────────────┼─────────────┘       (Booked can be checked in directly: walk-ins, unconfirmed arrivals)
  Booked/Confirmed ──► Cancelled
  Booked/Confirmed ──► NoShow   (only after the slot's end time has passed)
```

- Allowed transitions are enforced in the Domain layer (`Appointment.TransitionTo`). Invalid transitions
  return `422` with code `INVALID_STATUS_TRANSITION`.
- `Completed` is set automatically when the linked consultation is finalised.

**Slot rules**
- Available slots for a doctor on a date = schedule blocks for that weekday, split by `slot_duration_minutes`,
  minus leave, minus slots already held by appointments not in (`Cancelled`, `NoShow`), minus past slots for today.
- You can't book in the past or more than 90 days ahead (configurable: `Hospital:MaxDaysAhead`).
- A patient can't hold two active appointments with the **same doctor on the same day**.
- **Conflict prevention (double booking):**
  1. The service checks slot availability inside a transaction.
  2. The DB enforces it with a unique index on `(doctor_id, appointment_date, start_time, active_slot)`,
     where `active_slot` is a generated column that is `NULL` for cancelled or no-show appointments
     (see DATABASE.md).
  3. A duplicate-key error on that index is translated to `409 SLOT_UNAVAILABLE`.
- **Reschedule** = same appointment row, new date/slot, `reschedule_count + 1`. The old values go to
  `appointment_status_history`. Only `Booked` or `Confirmed` can be rescheduled.
- **Cancel** requires a reason. The slot is freed immediately.
- Walk-ins: Receptionist books with type `WalkIn` and no start time; the first free slot today that hasn't ended
  yet is used and the patient is checked in in the same transaction.
- Check-in is only allowed on the appointment date.

**Daily queue**
- Per doctor, for a date: checked-in patients by token first, then the rest by `start_time`. Token numbers
  (`queue_token`) are 1..n per doctor per day, assigned at check-in, and unique (`UX_appointments_queue_token`).
- The queue screen auto-refreshes (polling every 30 s is fine for v1).

---

## 5. Consultation and Medical Records

**Requirements**
- Record symptoms and complaints
- Record diagnosis and clinical notes
- Capture vital signs
- View previous consultations
- Link each consultation to the patient and doctor
- Maintain a history of changes to clinical records

**Rules**
- A consultation is started from a `CheckedIn` appointment (status becomes `InConsultation`). Walk-in
  consultations without an appointment are allowed for Doctor only, with `appointment_id = NULL`.
- One consultation per appointment (unique `appointment_id`).
- Fields: chief complaint (required to finalise), symptoms (list with duration), history of present illness,
  examination findings, diagnosis (free text plus optional ICD-10 code), clinical notes, advice.
- **Vital signs** (Nurse or Doctor): temperature (°C), pulse (bpm), BP systolic/diastolic (mmHg), respiratory
  rate, SpO₂ (%), weight (kg), height (cm). BMI is computed. Validate physiological ranges and warn (but don't
  block) on abnormal values. Multiple vitals readings per consultation are allowed, each with a timestamp.
- **Status:** `Draft` → `Finalised`. While `Draft`, the doctor can edit freely and each save still writes a
  revision. Once `Finalised`, the appointment becomes `Completed` and further edits are **amendments**: they
  require an amendment reason, and the full before/after snapshot goes to `clinical_record_revisions`.
- Only the consulting doctor (or Admin) can edit or amend. Other doctors can view.
- **Change history:** the UI shows a "History" tab per consultation listing every revision (who, when,
  reason, field diff).
- Previous consultations for the patient are shown in a side panel during consultation, newest first.

---

## 6. Prescription Management

**Requirements**
- Create prescriptions during consultation
- Add medicines, dosage, frequency, and duration
- Include instructions and follow-up date
- View and print prescriptions
- Link prescriptions to the relevant consultation

**Rules**
- A prescription belongs to exactly one consultation. One consultation has at most one active prescription.
  Corrections create a revision, not a second prescription.
- Prescription number: `RX{YYYYMMDD}{4-digit daily sequence}`.
- Items: medicine (pick from the `medicines` master **or** free text), strength/dosage (e.g. "500 mg"), route
  (oral, IV, topical, ...), frequency (enum plus display text: `OD`, `BD`, `TDS`, `QID`, `HS`, `SOS`, `STAT`,
  custom), duration (number + unit: days/weeks/months), quantity (optional), and item-level instructions
  (e.g. "after food").
- At least one item is required to finalise.
- General instructions and a `follow_up_date` (must be in the future). Setting a follow-up date shows a
  "Book follow-up" button that pre-fills an appointment with the same doctor.
- **Print view:** A4/A5 printable page with the hospital header, doctor name/qualification/registration no.,
  patient name/MRN/age/gender, date, vitals summary, diagnosis, item table, instructions, follow-up date,
  and a signature line. Implement it as a print-specific React route using `@media print` CSS. A server-side
  PDF is a v2 item.
- Prescriptions follow the same Draft/Finalised plus revision rules as consultations.
- Allergy check: if a medicine name matches a recorded patient allergy, show a warning (non-blocking).

---

## 7. Billing (demo, supports the dashboard)

- An invoice is created automatically when a patient is checked in, with one line for the doctor's
  consultation fee as of that moment. Invoice number: `INV{YYYYMMDD}{4-digit daily sequence}`.
- **Payments are a demo:** the receptionist presses **Mark as paid**, picks a method (Cash / Card / UPI / Other)
  and an optional reference. The full balance is recorded as one payment. No gateway, no partial payments,
  no discounts or extra line items in v1.
- Invoice status: `Unpaid`, `Paid`, `Cancelled`. `balance_due` = total − paid (generated column).
- Cancelling a checked-in appointment cancels its unpaid invoice automatically. A paid invoice can't be
  cancelled (`INVOICE_ALREADY_PAID`), so neither can its appointment. Admin can cancel an unpaid invoice directly.

---

## Out of scope for v1

Pharmacy inventory, lab orders/results, in-patient/bed management, insurance claims, SMS/email notifications,
patient portal, multi-branch. Keep the schema extensible, but don't build these.

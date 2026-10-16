# Database Design (MySQL 8)

## Conventions

- Engine **InnoDB**, charset **utf8mb4**, collation **utf8mb4_0900_ai_ci** (case- and accent-insensitive
  search on names).
- Table and column names are `snake_case`, and tables are plural. C# uses PascalCase. Mapping is done with
  `EFCore.NamingConventions` (`UseSnakeCaseNamingConvention()`).
- PK: `id BIGINT NOT NULL AUTO_INCREMENT` (C# `long`). FKs: `<entity>_id`.
- Audit columns on every mutable table:
  `created_at DATETIME(6) NOT NULL`, `created_by BIGINT NULL`, `updated_at DATETIME(6) NULL`,
  `updated_by BIGINT NULL`. They are set by the EF interceptor, in **UTC**.
- Optimistic concurrency: `row_version INT NOT NULL DEFAULT 0` on `appointments`, `consultations`,
  `prescriptions`, and `invoices`.
- Money: `DECIMAL(10,2)`. Booleans: `TINYINT(1)` (`bool`). Enums: stored as `VARCHAR(30)` strings
  (`HasConversion<string>()`) so the data is readable and reordering an enum is safe.
- Soft delete: `is_deleted TINYINT(1) NOT NULL DEFAULT 0` with an EF global query filter.
- FKs use `ON DELETE RESTRICT` unless noted. Clinical data must never cascade-delete.
- Index naming: `IX_<table>_<cols>`, unique `UX_<table>_<cols>`, FK `FK_<table>_<ref>`.

## Migration rules

- The schema is owned by **EF Core migrations** in `HMS.Infrastructure/Persistence/Migrations`. The SQL below
  is the *reference design*. If you change the schema, update this file in the same change.
- One migration per logical change, with a descriptive name (`AddDoctorLeaves`, not `Update3`).
- Never edit a migration that has been applied to a shared DB. Add a new one instead.
- Raw SQL is allowed inside migrations for things EF can't express (generated columns, check constraints).
  Use `migrationBuilder.Sql(...)`.
- Reference data (departments, specializations, medicines) and the initial `admin` user are seeded at startup by
  `DbSeeder`. Demo data (doctors + schedules, staff users, patients, today's appointments) is seeded by the same
  class when `Seed:DemoData=true` (Development only).

## Entity relationship overview

```
users ─┬─(0..1)─ doctors ─┬─< doctor_specializations >── specializations
       │                  ├─< doctor_schedules
       │                  ├─< doctor_leaves
       │                  └── departments
       └─< refresh_tokens

patients ─┬─< patient_allergies
          ├─< patient_conditions
          ├─< appointments >── doctors
          │       └─< appointment_status_history
          ├─< consultations >── doctors   (consultation.appointment_id 0..1, unique)
          │       ├─< consultation_symptoms
          │       ├─< vital_signs
          │       └── prescriptions (0..1) ─< prescription_items >── medicines (optional)
          └─< invoices ─┬─< invoice_items
                        └─< payments

clinical_record_revisions  (polymorphic: entity_type + entity_id)
audit_logs                 (polymorphic: entity_type + entity_id)
id_sequences               (MRN / doctor code / Rx number generation)
```

---

## Reference schema

### Identity

```sql
CREATE TABLE users (
  id              BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  username        VARCHAR(50)  NOT NULL,
  email           VARCHAR(150) NULL,
  password_hash   VARCHAR(100) NOT NULL,           -- BCrypt
  full_name       VARCHAR(150) NOT NULL,
  role            VARCHAR(30)  NOT NULL,           -- Admin | Receptionist | Doctor | Nurse
  doctor_id       BIGINT NULL,                     -- set when role = Doctor
  is_active       TINYINT(1) NOT NULL DEFAULT 1,
  last_login_at   DATETIME(6) NULL,
  created_at DATETIME(6) NOT NULL, created_by BIGINT NULL, updated_at DATETIME(6) NULL, updated_by BIGINT NULL,
  UNIQUE KEY UX_users_username (username),
  UNIQUE KEY UX_users_doctor_id (doctor_id),
  CONSTRAINT FK_users_doctors FOREIGN KEY (doctor_id) REFERENCES doctors(id)
);

CREATE TABLE refresh_tokens (
  id            BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  user_id       BIGINT NOT NULL,
  token_hash    CHAR(64) NOT NULL,                 -- SHA-256 hex
  expires_at    DATETIME(6) NOT NULL,
  revoked_at    DATETIME(6) NULL,
  replaced_by_id BIGINT NULL,
  created_at    DATETIME(6) NOT NULL,
  UNIQUE KEY UX_refresh_tokens_token_hash (token_hash),
  CONSTRAINT FK_refresh_tokens_users FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);
```

### Sequences

```sql
CREATE TABLE id_sequences (
  seq_key       VARCHAR(40) NOT NULL PRIMARY KEY,  -- 'PATIENT:2026', 'DOCTOR', 'RX:20261016'
  current_value BIGINT NOT NULL
);
```
Usage (`SequenceGenerator`, always inside the caller's transaction; the upsert's row lock is held until commit):
```sql
INSERT INTO id_sequences (seq_key, current_value) VALUES (@key, 1)
  ON DUPLICATE KEY UPDATE current_value = current_value + 1;
SELECT current_value FROM id_sequences WHERE seq_key = @key;
```
Keys: `PATIENT:<year>`, `DOCTOR`, `RX:<yyyyMMdd>`, `INV:<yyyyMMdd>`.

### Doctors

```sql
CREATE TABLE departments (
  id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  name VARCHAR(100) NOT NULL,
  description VARCHAR(500) NULL,
  is_active TINYINT(1) NOT NULL DEFAULT 1,
  created_at DATETIME(6) NOT NULL, created_by BIGINT NULL, updated_at DATETIME(6) NULL, updated_by BIGINT NULL,
  UNIQUE KEY UX_departments_name (name)
);

CREATE TABLE specializations (
  id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  name VARCHAR(100) NOT NULL,
  is_active TINYINT(1) NOT NULL DEFAULT 1,
  created_at DATETIME(6) NOT NULL, created_by BIGINT NULL, updated_at DATETIME(6) NULL, updated_by BIGINT NULL,
  UNIQUE KEY UX_specializations_name (name)
);

CREATE TABLE doctors (
  id                  BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  doctor_code         VARCHAR(10)  NOT NULL,        -- D0012
  first_name          VARCHAR(80)  NOT NULL,
  last_name           VARCHAR(80)  NOT NULL,
  gender              VARCHAR(10)  NOT NULL,
  phone               VARCHAR(16)  NOT NULL,
  email               VARCHAR(150) NULL,
  qualification       VARCHAR(200) NOT NULL,        -- "MBBS, MD (Medicine)"
  registration_number VARCHAR(50)  NOT NULL,        -- medical council reg. no. (printed on Rx)
  department_id       BIGINT NOT NULL,
  consultation_fee    DECIMAL(10,2) NOT NULL,
  years_of_experience SMALLINT NULL,
  is_active           TINYINT(1) NOT NULL DEFAULT 1,
  is_deleted          TINYINT(1) NOT NULL DEFAULT 0,
  created_at DATETIME(6) NOT NULL, created_by BIGINT NULL, updated_at DATETIME(6) NULL, updated_by BIGINT NULL,
  UNIQUE KEY UX_doctors_doctor_code (doctor_code),
  UNIQUE KEY UX_doctors_registration_number (registration_number),
  KEY IX_doctors_department_id_is_active (department_id, is_active),
  CONSTRAINT FK_doctors_departments FOREIGN KEY (department_id) REFERENCES departments(id),
  CONSTRAINT CK_doctors_fee CHECK (consultation_fee >= 0)
);

CREATE TABLE doctor_specializations (
  doctor_id         BIGINT NOT NULL,
  specialization_id BIGINT NOT NULL,
  is_primary        TINYINT(1) NOT NULL DEFAULT 0,
  PRIMARY KEY (doctor_id, specialization_id),
  CONSTRAINT FK_doctor_specializations_doctors FOREIGN KEY (doctor_id) REFERENCES doctors(id) ON DELETE CASCADE,
  CONSTRAINT FK_doctor_specializations_specializations FOREIGN KEY (specialization_id) REFERENCES specializations(id)
);

CREATE TABLE doctor_schedules (
  id                    BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  doctor_id             BIGINT NOT NULL,
  day_of_week           TINYINT NOT NULL,           -- 0=Sunday .. 6=Saturday (matches System.DayOfWeek)
  start_time            TIME NOT NULL,
  end_time              TIME NOT NULL,
  slot_duration_minutes SMALLINT NOT NULL DEFAULT 15,
  is_active             TINYINT(1) NOT NULL DEFAULT 1,
  created_at DATETIME(6) NOT NULL, created_by BIGINT NULL, updated_at DATETIME(6) NULL, updated_by BIGINT NULL,
  KEY IX_doctor_schedules_doctor_id_day (doctor_id, day_of_week),
  CONSTRAINT FK_doctor_schedules_doctors FOREIGN KEY (doctor_id) REFERENCES doctors(id) ON DELETE CASCADE,
  CONSTRAINT CK_doctor_schedules_time CHECK (end_time > start_time),
  CONSTRAINT CK_doctor_schedules_slot CHECK (slot_duration_minutes BETWEEN 5 AND 120)
);
-- Overlap between blocks on the same day is validated in the service (MySQL can't express it as a constraint).

CREATE TABLE doctor_leaves (
  id         BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  doctor_id  BIGINT NOT NULL,
  from_date  DATE NOT NULL,
  to_date    DATE NOT NULL,
  reason     VARCHAR(250) NULL,
  created_at DATETIME(6) NOT NULL, created_by BIGINT NULL, updated_at DATETIME(6) NULL, updated_by BIGINT NULL,
  KEY IX_doctor_leaves_doctor_id_dates (doctor_id, from_date, to_date),
  CONSTRAINT FK_doctor_leaves_doctors FOREIGN KEY (doctor_id) REFERENCES doctors(id) ON DELETE CASCADE,
  CONSTRAINT CK_doctor_leaves_dates CHECK (to_date >= from_date)
);
```

### Patients

```sql
CREATE TABLE patients (
  id              BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  mrn             VARCHAR(12)  NOT NULL,            -- P2026000123 (immutable)
  first_name      VARCHAR(80)  NOT NULL,
  last_name       VARCHAR(80)  NOT NULL,
  gender          VARCHAR(10)  NOT NULL,            -- Male | Female | Other | Unknown
  date_of_birth   DATE NULL,
  age_years       SMALLINT NULL,                    -- used when DOB unknown
  age_recorded_on DATE NULL,
  blood_group     VARCHAR(5) NULL,                  -- A+, O-, ...
  phone           VARCHAR(16)  NOT NULL,            -- normalised
  alternate_phone VARCHAR(16)  NULL,
  email           VARCHAR(150) NULL,
  address_line1   VARCHAR(200) NULL,
  address_line2   VARCHAR(200) NULL,
  city            VARCHAR(80)  NULL,
  state           VARCHAR(80)  NULL,
  postal_code     VARCHAR(12)  NULL,
  country         VARCHAR(60)  NULL,
  emergency_contact_name  VARCHAR(150) NULL,
  emergency_contact_phone VARCHAR(16)  NULL,
  emergency_contact_relation VARCHAR(40) NULL,
  is_deleted      TINYINT(1) NOT NULL DEFAULT 0,
  created_at DATETIME(6) NOT NULL, created_by BIGINT NULL, updated_at DATETIME(6) NULL, updated_by BIGINT NULL,
  UNIQUE KEY UX_patients_mrn (mrn),
  KEY IX_patients_phone (phone),
  KEY IX_patients_last_name_first_name (last_name, first_name),
  KEY IX_patients_first_name (first_name),
  CONSTRAINT CK_patients_age CHECK (date_of_birth IS NOT NULL OR age_years IS NOT NULL)
);

CREATE TABLE patient_allergies (
  id         BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  patient_id BIGINT NOT NULL,
  allergen   VARCHAR(100) NOT NULL,
  reaction   VARCHAR(200) NULL,
  severity   VARCHAR(20)  NULL,                     -- Mild | Moderate | Severe
  created_at DATETIME(6) NOT NULL, created_by BIGINT NULL, updated_at DATETIME(6) NULL, updated_by BIGINT NULL,
  KEY IX_patient_allergies_patient_id (patient_id),
  CONSTRAINT FK_patient_allergies_patients FOREIGN KEY (patient_id) REFERENCES patients(id)
);

CREATE TABLE patient_conditions (                   -- chronic conditions / past medical history
  id          BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  patient_id  BIGINT NOT NULL,
  condition_name VARCHAR(150) NOT NULL,
  since_year  SMALLINT NULL,
  notes       VARCHAR(500) NULL,
  created_at DATETIME(6) NOT NULL, created_by BIGINT NULL, updated_at DATETIME(6) NULL, updated_by BIGINT NULL,
  KEY IX_patient_conditions_patient_id (patient_id),
  CONSTRAINT FK_patient_conditions_patients FOREIGN KEY (patient_id) REFERENCES patients(id)
);
```

### Appointments

```sql
CREATE TABLE appointments (
  id                  BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  patient_id          BIGINT NOT NULL,
  doctor_id           BIGINT NOT NULL,
  appointment_date    DATE NOT NULL,                -- hospital-local date
  start_time          TIME NOT NULL,                -- hospital-local time
  end_time            TIME NOT NULL,
  status              VARCHAR(20) NOT NULL,         -- Booked|Confirmed|CheckedIn|InConsultation|Completed|Cancelled|NoShow
  appointment_type    VARCHAR(20) NOT NULL DEFAULT 'Scheduled',  -- Scheduled | WalkIn | FollowUp
  reason_for_visit    VARCHAR(300) NULL,
  queue_token         SMALLINT NULL,                -- assigned at check-in
  checked_in_at       DATETIME(6) NULL,             -- UTC
  cancelled_at        DATETIME(6) NULL,
  cancellation_reason VARCHAR(300) NULL,
  reschedule_count    SMALLINT NOT NULL DEFAULT 0,
  follow_up_of_consultation_id BIGINT NULL,
  -- NULL when the appointment no longer holds the slot, so the unique index ignores it
  active_slot         TINYINT GENERATED ALWAYS AS
                        (CASE WHEN status IN ('Cancelled','NoShow') THEN NULL ELSE 1 END) STORED,
  row_version         INT NOT NULL DEFAULT 0,
  created_at DATETIME(6) NOT NULL, created_by BIGINT NULL, updated_at DATETIME(6) NULL, updated_by BIGINT NULL,
  UNIQUE KEY UX_appointments_active_slot (doctor_id, appointment_date, start_time, active_slot),
  UNIQUE KEY UX_appointments_queue_token (doctor_id, appointment_date, queue_token),
  KEY IX_appointments_patient_id_date (patient_id, appointment_date),
  KEY IX_appointments_date_status (appointment_date, status),
  CONSTRAINT FK_appointments_patients FOREIGN KEY (patient_id) REFERENCES patients(id),
  CONSTRAINT FK_appointments_doctors FOREIGN KEY (doctor_id) REFERENCES doctors(id),
  CONSTRAINT CK_appointments_time CHECK (end_time > start_time)
);
-- EF: ActiveSlot is .HasComputedColumnSql("...", stored: true); both unique indexes are plain HasIndex().IsUnique().

CREATE TABLE appointment_status_history (
  id             BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  appointment_id BIGINT NOT NULL,
  from_status    VARCHAR(20) NULL,
  to_status      VARCHAR(20) NOT NULL,
  old_date       DATE NULL,                         -- filled on reschedule
  old_start_time TIME NULL,
  note           VARCHAR(300) NULL,
  changed_at     DATETIME(6) NOT NULL,
  changed_by     BIGINT NULL,
  KEY IX_appointment_status_history_appointment_id (appointment_id),
  CONSTRAINT FK_appointment_status_history_appointments FOREIGN KEY (appointment_id) REFERENCES appointments(id)
);
```

### Consultations and vitals

```sql
CREATE TABLE consultations (
  id                  BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  patient_id          BIGINT NOT NULL,
  doctor_id           BIGINT NOT NULL,
  appointment_id      BIGINT NULL,
  status              VARCHAR(20) NOT NULL DEFAULT 'Draft',   -- Draft | Finalised
  started_at          DATETIME(6) NOT NULL,
  finalised_at        DATETIME(6) NULL,
  chief_complaint     VARCHAR(500) NULL,             -- required to finalise
  history_of_present_illness TEXT NULL,
  examination_findings TEXT NULL,
  diagnosis           VARCHAR(500) NULL,
  diagnosis_icd10     VARCHAR(10)  NULL,
  clinical_notes      TEXT NULL,
  advice              TEXT NULL,
  row_version         INT NOT NULL DEFAULT 0,
  created_at DATETIME(6) NOT NULL, created_by BIGINT NULL, updated_at DATETIME(6) NULL, updated_by BIGINT NULL,
  UNIQUE KEY UX_consultations_appointment_id (appointment_id),
  KEY IX_consultations_patient_id_started_at (patient_id, started_at),
  KEY IX_consultations_doctor_id_started_at (doctor_id, started_at),
  CONSTRAINT FK_consultations_patients FOREIGN KEY (patient_id) REFERENCES patients(id),
  CONSTRAINT FK_consultations_doctors FOREIGN KEY (doctor_id) REFERENCES doctors(id),
  CONSTRAINT FK_consultations_appointments FOREIGN KEY (appointment_id) REFERENCES appointments(id)
);

CREATE TABLE consultation_symptoms (
  id              BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  consultation_id BIGINT NOT NULL,
  symptom         VARCHAR(150) NOT NULL,
  duration_value  SMALLINT NULL,
  duration_unit   VARCHAR(10) NULL,                  -- Hours | Days | Weeks | Months
  severity        VARCHAR(20) NULL,
  sort_order      SMALLINT NOT NULL DEFAULT 0,
  KEY IX_consultation_symptoms_consultation_id (consultation_id),
  CONSTRAINT FK_consultation_symptoms_consultations FOREIGN KEY (consultation_id) REFERENCES consultations(id)
);

CREATE TABLE vital_signs (
  id               BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  consultation_id  BIGINT NOT NULL,
  patient_id       BIGINT NOT NULL,                  -- denormalised for trend queries
  recorded_at      DATETIME(6) NOT NULL,
  recorded_by      BIGINT NOT NULL,
  temperature_c    DECIMAL(4,1) NULL,
  pulse_bpm        SMALLINT NULL,
  bp_systolic      SMALLINT NULL,
  bp_diastolic     SMALLINT NULL,
  respiratory_rate SMALLINT NULL,
  spo2_percent     TINYINT NULL,
  weight_kg        DECIMAL(5,2) NULL,
  height_cm        DECIMAL(5,1) NULL,
  bmi              DECIMAL(4,1) NULL,                -- computed in service
  notes            VARCHAR(300) NULL,
  KEY IX_vital_signs_consultation_id (consultation_id),
  KEY IX_vital_signs_patient_id_recorded_at (patient_id, recorded_at),
  CONSTRAINT FK_vital_signs_consultations FOREIGN KEY (consultation_id) REFERENCES consultations(id),
  CONSTRAINT FK_vital_signs_patients FOREIGN KEY (patient_id) REFERENCES patients(id)
);
```

### Prescriptions

```sql
CREATE TABLE medicines (                            -- simple master list (no inventory in v1)
  id           BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  name         VARCHAR(150) NOT NULL,               -- brand or generic display name
  generic_name VARCHAR(150) NULL,
  form         VARCHAR(30)  NULL,                   -- Tablet | Capsule | Syrup | Injection | Ointment ...
  strength     VARCHAR(50)  NULL,                   -- "500 mg"
  is_active    TINYINT(1) NOT NULL DEFAULT 1,
  created_at DATETIME(6) NOT NULL, created_by BIGINT NULL, updated_at DATETIME(6) NULL, updated_by BIGINT NULL,
  KEY IX_medicines_name (name),
  KEY IX_medicines_generic_name (generic_name)
);

CREATE TABLE prescriptions (
  id                   BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  prescription_number  VARCHAR(16) NOT NULL,          -- RX202610160007
  consultation_id      BIGINT NOT NULL,
  patient_id           BIGINT NOT NULL,
  doctor_id            BIGINT NOT NULL,
  status               VARCHAR(20) NOT NULL DEFAULT 'Draft',  -- Draft | Finalised
  general_instructions TEXT NULL,
  follow_up_date       DATE NULL,
  finalised_at         DATETIME(6) NULL,
  row_version          INT NOT NULL DEFAULT 0,
  created_at DATETIME(6) NOT NULL, created_by BIGINT NULL, updated_at DATETIME(6) NULL, updated_by BIGINT NULL,
  UNIQUE KEY UX_prescriptions_prescription_number (prescription_number),
  UNIQUE KEY UX_prescriptions_consultation_id (consultation_id),
  KEY IX_prescriptions_patient_id (patient_id),
  CONSTRAINT FK_prescriptions_consultations FOREIGN KEY (consultation_id) REFERENCES consultations(id),
  CONSTRAINT FK_prescriptions_patients FOREIGN KEY (patient_id) REFERENCES patients(id),
  CONSTRAINT FK_prescriptions_doctors FOREIGN KEY (doctor_id) REFERENCES doctors(id)
);

CREATE TABLE prescription_items (
  id               BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  prescription_id  BIGINT NOT NULL,
  medicine_id      BIGINT NULL,                     -- NULL when free text
  medicine_name    VARCHAR(150) NOT NULL,           -- always stored (snapshot), even when medicine_id is set
  dosage           VARCHAR(50)  NOT NULL,           -- "500 mg", "1 tablet", "5 ml"
  route            VARCHAR(20)  NULL,               -- Oral | IV | IM | Topical | Inhalation ...
  frequency_code   VARCHAR(10)  NOT NULL,           -- OD | BD | TDS | QID | HS | SOS | STAT | CUSTOM
  frequency_text   VARCHAR(60)  NULL,               -- "1-0-1", "every 6 hours"
  duration_value   SMALLINT NULL,
  duration_unit    VARCHAR(10)  NULL,               -- Days | Weeks | Months
  quantity         VARCHAR(30)  NULL,
  instructions     VARCHAR(300) NULL,               -- "after food"
  sort_order       SMALLINT NOT NULL DEFAULT 0,
  KEY IX_prescription_items_prescription_id (prescription_id),
  CONSTRAINT FK_prescription_items_prescriptions FOREIGN KEY (prescription_id) REFERENCES prescriptions(id),
  CONSTRAINT FK_prescription_items_medicines FOREIGN KEY (medicine_id) REFERENCES medicines(id)
);
```

### Clinical change history and audit

```sql
CREATE TABLE clinical_record_revisions (
  id              BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  entity_type     VARCHAR(30) NOT NULL,             -- Consultation | Prescription | VitalSigns
  entity_id       BIGINT NOT NULL,
  consultation_id BIGINT NOT NULL,                  -- grouping key for the History tab
  revision_number INT NOT NULL,                     -- 1..n per (entity_type, entity_id)
  change_type     VARCHAR(20) NOT NULL,             -- Created | Updated | Finalised | Amended
  reason          VARCHAR(500) NULL,                -- required when change_type = Amended
  snapshot_json   JSON NOT NULL,                    -- full entity (incl. child rows) AFTER the change
  changed_fields  JSON NULL,                        -- ["diagnosis","clinicalNotes"]
  changed_at      DATETIME(6) NOT NULL,
  changed_by      BIGINT NOT NULL,
  UNIQUE KEY UX_clinical_record_revisions_entity_rev (entity_type, entity_id, revision_number),
  KEY IX_clinical_record_revisions_consultation_id (consultation_id)
);
-- Append-only: the app never UPDATEs or DELETEs this table. In production, grant the app user INSERT/SELECT only.

CREATE TABLE audit_logs (                           -- non-clinical changes (patients, doctors, users, invoices)
  id           BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  entity_type  VARCHAR(50) NOT NULL,
  entity_id    BIGINT NOT NULL,
  action       VARCHAR(20) NOT NULL,                -- Insert | Update | Delete
  changes_json JSON NULL,                           -- { "phone": { "old": "...", "new": "..." } }
  changed_at   DATETIME(6) NOT NULL,
  changed_by   BIGINT NULL,
  KEY IX_audit_logs_entity (entity_type, entity_id),
  KEY IX_audit_logs_changed_at (changed_at)
);
```

### Billing

```sql
CREATE TABLE invoices (
  id              BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  invoice_number  VARCHAR(20) NOT NULL,             -- INV202610160012
  patient_id      BIGINT NOT NULL,
  appointment_id  BIGINT NULL,
  invoice_date    DATE NOT NULL,                    -- hospital-local
  subtotal        DECIMAL(10,2) NOT NULL,
  discount_amount DECIMAL(10,2) NOT NULL DEFAULT 0,
  total_amount    DECIMAL(10,2) NOT NULL,
  paid_amount     DECIMAL(10,2) NOT NULL DEFAULT 0,
  balance_due     DECIMAL(10,2) GENERATED ALWAYS AS (total_amount - paid_amount) STORED,
  status          VARCHAR(20) NOT NULL,             -- Unpaid | Paid | Cancelled (demo billing: no partial payments)
  cancel_reason   VARCHAR(300) NULL,
  row_version     INT NOT NULL DEFAULT 0,
  created_at DATETIME(6) NOT NULL, created_by BIGINT NULL, updated_at DATETIME(6) NULL, updated_by BIGINT NULL,
  UNIQUE KEY UX_invoices_invoice_number (invoice_number),
  UNIQUE KEY UX_invoices_appointment_id (appointment_id),
  KEY IX_invoices_status (status),
  KEY IX_invoices_patient_id (patient_id),
  CONSTRAINT FK_invoices_patients FOREIGN KEY (patient_id) REFERENCES patients(id),
  CONSTRAINT FK_invoices_appointments FOREIGN KEY (appointment_id) REFERENCES appointments(id)
);

CREATE TABLE invoice_items (
  id          BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  invoice_id  BIGINT NOT NULL,
  description VARCHAR(200) NOT NULL,
  item_type   VARCHAR(20) NOT NULL,                 -- ConsultationFee | Procedure | Other
  quantity    SMALLINT NOT NULL DEFAULT 1,
  unit_price  DECIMAL(10,2) NOT NULL,
  line_total  DECIMAL(10,2) NOT NULL,
  KEY IX_invoice_items_invoice_id (invoice_id),
  CONSTRAINT FK_invoice_items_invoices FOREIGN KEY (invoice_id) REFERENCES invoices(id)
);

CREATE TABLE payments (
  id           BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  invoice_id   BIGINT NOT NULL,
  amount       DECIMAL(10,2) NOT NULL,
  method       VARCHAR(20) NOT NULL,                -- Cash | Card | UPI | Other
  reference_no VARCHAR(60) NULL,
  paid_at      DATETIME(6) NOT NULL,                -- UTC
  received_by  BIGINT NOT NULL,
  KEY IX_payments_invoice_id (invoice_id),
  KEY IX_payments_paid_at (paid_at),
  CONSTRAINT FK_payments_invoices FOREIGN KEY (invoice_id) REFERENCES invoices(id),
  CONSTRAINT CK_payments_amount CHECK (amount > 0)
);
```

---

## Key queries (keep these index-backed)

| Use case | Query shape | Index |
|---|---|---|
| Patient search by MRN | `WHERE mrn = ?` | `UX_patients_mrn` |
| Patient search by phone | `WHERE phone LIKE '98765%'` | `IX_patients_phone` |
| Patient search by name | `WHERE first_name LIKE 'ram%' OR last_name LIKE 'ram%'` | name indexes |
| Doctor's day queue | `WHERE doctor_id=? AND appointment_date=?` | `UX_appointments_active_slot` (prefix) |
| Dashboard today | `WHERE appointment_date=? GROUP BY status` | `IX_appointments_date_status` |
| Patient visit history | `WHERE patient_id=? ORDER BY started_at DESC` | `IX_consultations_patient_id_started_at` |
| Today's revenue | `WHERE paid_at BETWEEN ? AND ?` (UTC bounds of the local day) | `IX_payments_paid_at` |
| Pending payments | `WHERE status IN ('Unpaid','PartiallyPaid')` | `IX_invoices_status` |

Never use a leading-wildcard `LIKE '%x%'` on patients. If fuzzy search is needed later, add a FULLTEXT index.

## Gotchas

- MySQL treats `NULL`s as distinct in unique indexes, which is what makes `UX_appointments_active_slot` work.
  Don't change `active_slot` to `0/1`.
- `TIME` maps to `TimeOnly` and `DATE` to `DateOnly` with Pomelo. Use those types, not `DateTime`, for
  appointment date/time.
- Pass the server version explicitly (`new MySqlServerVersion(8.0.36)`) (`ServerVersion.AutoDetect` hits the DB at
  startup, which is slow and fails in tests without a DB).
- Run all dates in queries through `IClock` and hospital-TZ helpers. MySQL `CURDATE()` uses the server TZ,
  so don't use it.

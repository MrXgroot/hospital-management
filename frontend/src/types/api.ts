// DTO types mirroring docs/API.md and the backend records (camelCase JSON). Keep in sync.

export type Role = 'Admin' | 'Receptionist' | 'Doctor' | 'Nurse';
export type Gender = 'Male' | 'Female' | 'Other' | 'Unknown';
export type DurationUnit = 'Hours' | 'Days' | 'Weeks' | 'Months';
export type AppointmentStatus =
  | 'Booked' | 'Confirmed' | 'CheckedIn' | 'InConsultation' | 'Completed' | 'Cancelled' | 'NoShow';
export type AppointmentType = 'Scheduled' | 'WalkIn' | 'FollowUp';
export type ClinicalStatus = 'Draft' | 'Finalised';
export type FrequencyCode = 'OD' | 'BD' | 'TDS' | 'QID' | 'HS' | 'SOS' | 'STAT' | 'CUSTOM';
export type InvoiceStatus = 'Unpaid' | 'Paid' | 'Cancelled';
export type PaymentMethod = 'Cash' | 'Card' | 'UPI' | 'Other';
export type RevisionChangeType = 'Created' | 'Updated' | 'Finalised' | 'Amended';
export type ClinicalEntityType = 'Consultation' | 'Prescription' | 'VitalSigns';

/** YYYY-MM-DD */
export type IsoDate = string;
/** HH:mm */
export type TimeOfDay = string;
/** ISO-8601 UTC timestamp */
export type Timestamp = string;

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

// ---- Auth / users
export interface UserDto {
  id: number;
  username: string;
  fullName: string;
  email: string | null;
  role: Role;
  doctorId: number | null;
  isActive: boolean;
  lastLoginAt: Timestamp | null;
}
export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: Timestamp;
  user: UserDto;
}
export interface CreateUserRequest {
  username: string;
  password: string;
  fullName: string;
  email: string | null;
  role: Role;
  doctorId: number | null;
}
export interface UpdateUserRequest {
  fullName: string;
  email: string | null;
  role: Role;
  doctorId: number | null;
  isActive: boolean;
  newPassword: string | null;
}

// ---- Master data
export interface DepartmentDto { id: number; name: string; description: string | null; isActive: boolean }
export interface SpecializationDto { id: number; name: string; isActive: boolean }
export interface MedicineDto {
  id: number;
  name: string;
  genericName: string | null;
  form: string | null;
  strength: string | null;
  isActive: boolean;
}

// ---- Doctors
export interface DoctorListItemDto {
  id: number;
  doctorCode: string;
  fullName: string;
  departmentId: number;
  departmentName: string;
  primarySpecialization: string | null;
  consultationFee: number;
  phone: string;
  isActive: boolean;
}
export interface DoctorDto {
  id: number;
  doctorCode: string;
  firstName: string;
  lastName: string;
  fullName: string;
  gender: Gender;
  phone: string;
  email: string | null;
  qualification: string;
  registrationNumber: string;
  departmentId: number;
  departmentName: string;
  consultationFee: number;
  yearsOfExperience: number | null;
  isActive: boolean;
  specializations: { specializationId: number; name: string; isPrimary: boolean }[];
}
export interface SaveDoctorRequest {
  firstName: string;
  lastName: string;
  gender: Gender;
  phone: string;
  email: string | null;
  qualification: string;
  registrationNumber: string;
  departmentId: number;
  consultationFee: number;
  yearsOfExperience: number | null;
  specializationIds: number[];
  primarySpecializationId: number;
}
export interface ScheduleBlockDto {
  dayOfWeek: number;
  startTime: TimeOfDay;
  endTime: TimeOfDay;
  slotDurationMinutes: number;
}
export interface DoctorLeaveDto { id: number; fromDate: IsoDate; toDate: IsoDate; reason: string | null }
export interface SlotDto { startTime: TimeOfDay; endTime: TimeOfDay; isAvailable: boolean }
export interface AvailableDoctorDto {
  id: number;
  fullName: string;
  departmentName: string;
  primarySpecialization: string | null;
  workingHours: string;
  freeSlotsRemaining: number;
  waitingCount: number;
}

// ---- Patients
export interface PatientListItemDto {
  id: number;
  mrn: string;
  fullName: string;
  gender: Gender;
  age: number | null;
  phone: string;
  city: string | null;
  createdAt: Timestamp;
}
export interface AllergyDto { id: number; allergen: string; reaction: string | null; severity: string | null }
export interface ConditionDto { id: number; conditionName: string; sinceYear: number | null; notes: string | null }
export interface PatientDto {
  id: number;
  mrn: string;
  firstName: string;
  lastName: string;
  fullName: string;
  gender: Gender;
  dateOfBirth: IsoDate | null;
  ageYears: number | null;
  age: number | null;
  bloodGroup: string | null;
  phone: string;
  alternatePhone: string | null;
  email: string | null;
  addressLine1: string | null;
  addressLine2: string | null;
  city: string | null;
  state: string | null;
  postalCode: string | null;
  country: string | null;
  emergencyContactName: string | null;
  emergencyContactPhone: string | null;
  emergencyContactRelation: string | null;
  createdAt: Timestamp;
  allergies: AllergyDto[];
  conditions: ConditionDto[];
}
export type SavePatientRequest = Omit<PatientDto, 'id' | 'mrn' | 'fullName' | 'age' | 'createdAt' | 'allergies' | 'conditions'>;
export interface DuplicatePatientMatch { id: number; mrn: string; fullName: string; phone: string; age: number | null }
export interface VisitDto {
  consultationId: number;
  startedAt: Timestamp;
  doctorId: number;
  doctorName: string;
  chiefComplaint: string | null;
  diagnosis: string | null;
  status: ClinicalStatus;
  prescriptionId: number | null;
}

// ---- Appointments
export interface AppointmentStatusHistoryDto {
  fromStatus: AppointmentStatus | null;
  toStatus: AppointmentStatus;
  oldDate: IsoDate | null;
  oldStartTime: TimeOfDay | null;
  note: string | null;
  changedAt: Timestamp;
  changedByName: string | null;
}
export interface AppointmentDto {
  id: number;
  patientId: number;
  patientMrn: string;
  patientName: string;
  patientPhone: string;
  doctorId: number;
  doctorName: string;
  departmentName: string;
  appointmentDate: IsoDate;
  startTime: TimeOfDay;
  endTime: TimeOfDay;
  status: AppointmentStatus;
  appointmentType: AppointmentType;
  reasonForVisit: string | null;
  queueToken: number | null;
  checkedInAt: Timestamp | null;
  cancellationReason: string | null;
  rescheduleCount: number;
  consultationId: number | null;
  invoiceId: number | null;
  rowVersion: number;
  history: AppointmentStatusHistoryDto[] | null;
}
export interface CreateAppointmentRequest {
  patientId: number;
  doctorId: number;
  appointmentDate: IsoDate;
  startTime: TimeOfDay | null;
  appointmentType: AppointmentType;
  reasonForVisit: string | null;
  followUpOfConsultationId?: number | null;
}

// ---- Clinical
export interface SymptomDto {
  symptom: string;
  durationValue: number | null;
  durationUnit: DurationUnit | null;
  severity: string | null;
}
export interface VitalSignsDto {
  id: number;
  recordedAt: Timestamp;
  recordedByName: string | null;
  temperatureC: number | null;
  pulseBpm: number | null;
  bpSystolic: number | null;
  bpDiastolic: number | null;
  respiratoryRate: number | null;
  spo2Percent: number | null;
  weightKg: number | null;
  heightCm: number | null;
  bmi: number | null;
  notes: string | null;
  warnings: string[];
}
export interface SaveVitalsRequest {
  temperatureC: number | null;
  pulseBpm: number | null;
  bpSystolic: number | null;
  bpDiastolic: number | null;
  respiratoryRate: number | null;
  spo2Percent: number | null;
  weightKg: number | null;
  heightCm: number | null;
  notes: string | null;
  amendmentReason: string | null;
}
export interface ConsultationDto {
  id: number;
  patient: {
    id: number;
    mrn: string;
    fullName: string;
    gender: Gender;
    age: number | null;
    bloodGroup: string | null;
    allergies: string[];
    conditions: string[];
  };
  doctorId: number;
  doctorName: string;
  appointmentId: number | null;
  status: ClinicalStatus;
  startedAt: Timestamp;
  finalisedAt: Timestamp | null;
  chiefComplaint: string | null;
  historyOfPresentIllness: string | null;
  examinationFindings: string | null;
  diagnosis: string | null;
  diagnosisIcd10: string | null;
  clinicalNotes: string | null;
  advice: string | null;
  symptoms: SymptomDto[];
  vitals: VitalSignsDto[];
  prescriptionId: number | null;
  rowVersion: number;
  canEdit: boolean;
}
export interface UpdateConsultationRequest {
  chiefComplaint: string | null;
  historyOfPresentIllness: string | null;
  examinationFindings: string | null;
  diagnosis: string | null;
  diagnosisIcd10: string | null;
  clinicalNotes: string | null;
  advice: string | null;
  symptoms: SymptomDto[];
  rowVersion: number;
  amendmentReason: string | null;
}
export interface RevisionDto {
  id: number;
  entityType: ClinicalEntityType;
  entityId: number;
  revisionNumber: number;
  changeType: RevisionChangeType;
  reason: string | null;
  changedAt: Timestamp;
  changedByName: string | null;
  changes: { field: string; oldValue: string | null; newValue: string | null }[];
}

// ---- Prescriptions
export interface PrescriptionItemDto {
  medicineId: number | null;
  medicineName: string;
  dosage: string;
  route: string | null;
  frequencyCode: FrequencyCode;
  frequencyText: string | null;
  durationValue: number | null;
  durationUnit: DurationUnit | null;
  quantity: string | null;
  instructions: string | null;
}
export interface PrescriptionDto {
  id: number;
  prescriptionNumber: string;
  consultationId: number;
  patientId: number;
  patientName: string;
  patientMrn: string;
  doctorId: number;
  doctorName: string;
  status: ClinicalStatus;
  generalInstructions: string | null;
  followUpDate: IsoDate | null;
  finalisedAt: Timestamp | null;
  createdAt: Timestamp;
  items: PrescriptionItemDto[];
  rowVersion: number;
  canEdit: boolean;
  allergyWarnings: string[];
}
export interface PrescriptionListItemDto {
  id: number;
  prescriptionNumber: string;
  consultationId: number;
  doctorName: string;
  createdAt: Timestamp;
  status: ClinicalStatus;
  itemCount: number;
  followUpDate: IsoDate | null;
}
export interface SavePrescriptionRequest {
  generalInstructions: string | null;
  followUpDate: IsoDate | null;
  items: PrescriptionItemDto[];
  rowVersion: number | null;
  amendmentReason: string | null;
}
export interface PrescriptionPrintDto {
  hospital: { name: string; addressLine: string; phone: string };
  doctor: { name: string; qualification: string; registrationNumber: string; department: string };
  patient: { name: string; mrn: string; age: number | null; gender: Gender; phone: string };
  prescriptionNumber: string;
  date: Timestamp;
  status: ClinicalStatus;
  vitals: {
    temperatureC: number | null;
    pulseBpm: number | null;
    bpSystolic: number | null;
    bpDiastolic: number | null;
    spo2Percent: number | null;
    weightKg: number | null;
  } | null;
  chiefComplaint: string | null;
  diagnosis: string | null;
  items: PrescriptionItemDto[];
  generalInstructions: string | null;
  followUpDate: IsoDate | null;
}

// ---- Billing
export interface InvoiceListItemDto {
  id: number;
  invoiceNumber: string;
  patientId: number;
  patientName: string;
  patientMrn: string;
  doctorName: string | null;
  invoiceDate: IsoDate;
  totalAmount: number;
  balanceDue: number;
  status: InvoiceStatus;
}
export interface InvoiceDto {
  id: number;
  invoiceNumber: string;
  patientId: number;
  patientName: string;
  patientMrn: string;
  appointmentId: number | null;
  doctorName: string | null;
  invoiceDate: IsoDate;
  subtotal: number;
  discountAmount: number;
  totalAmount: number;
  paidAmount: number;
  balanceDue: number;
  status: InvoiceStatus;
  cancelReason: string | null;
  items: { id: number; description: string; itemType: string; quantity: number; unitPrice: number; lineTotal: number }[];
  payments: { id: number; amount: number; method: PaymentMethod; referenceNo: string | null; paidAt: Timestamp; receivedByName: string | null }[];
  rowVersion: number;
}

// ---- Dashboard
export interface WaitingPatientDto {
  appointmentId: number;
  queueToken: number | null;
  patientId: number;
  patientName: string;
  patientMrn: string;
  doctorId: number;
  doctorName: string;
  startTime: TimeOfDay;
  checkedInAt: Timestamp | null;
  consultationId: number | null;
}
export interface DashboardSummaryDto {
  totalPatients: number;
  todayAppointments: { total: number; byStatus: Record<string, number> };
  availableDoctors: AvailableDoctorDto[];
  waitingQueue: WaitingPatientDto[];
  todayRevenue: number;
  pendingPayments: number;
  pendingInvoiceCount: number;
  currency: string;
  scopedToDoctor: boolean;
}

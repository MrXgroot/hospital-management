import { api } from './client';
import type {
  AppointmentDto, AppointmentStatus, AuthResponse, AvailableDoctorDto, ConditionDto, AllergyDto, ConsultationDto,
  CreateAppointmentRequest, CreateUserRequest, DashboardSummaryDto, DepartmentDto, DoctorDto, DoctorLeaveDto,
  DoctorListItemDto, InvoiceDto, InvoiceListItemDto, InvoiceStatus, IsoDate, MedicineDto, PagedResult, PatientDto,
  PatientListItemDto, PaymentMethod, PrescriptionDto, PrescriptionListItemDto, PrescriptionPrintDto, RevisionDto,
  SaveDoctorRequest, SavePatientRequest, SavePrescriptionRequest, SaveVitalsRequest, ScheduleBlockDto, SlotDto,
  SpecializationDto, UpdateConsultationRequest, UpdateUserRequest, UserDto, VisitDto, VitalSignsDto, TimeOfDay,
} from '../types/api';

const get = async <T>(url: string, params?: object) => (await api.get<T>(url, { params })).data;
const post = async <T>(url: string, body?: unknown, params?: object) => (await api.post<T>(url, body, { params })).data;
const put = async <T>(url: string, body: unknown) => (await api.put<T>(url, body)).data;
const del = async (url: string) => {
  await api.delete(url);
};

export const authApi = {
  login: (username: string, password: string) => post<AuthResponse>('/auth/login', { username, password }),
  logout: (refreshToken: string) => post<void>('/auth/logout', { refreshToken }),
  me: () => get<UserDto>('/auth/me'),
};

export const usersApi = {
  list: () => get<UserDto[]>('/users'),
  create: (body: CreateUserRequest) => post<UserDto>('/users', body),
  update: (id: number, body: UpdateUserRequest) => put<UserDto>(`/users/${id}`, body),
};

export const dashboardApi = {
  summary: () => get<DashboardSummaryDto>('/dashboard/summary'),
};

export const masterApi = {
  departments: (activeOnly = false) => get<DepartmentDto[]>('/departments', { activeOnly }),
  saveDepartment: (id: number | null, body: Omit<DepartmentDto, 'id'>) =>
    id ? put<DepartmentDto>(`/departments/${id}`, body) : post<DepartmentDto>('/departments', body),
  specializations: (activeOnly = false) => get<SpecializationDto[]>('/specializations', { activeOnly }),
  saveSpecialization: (id: number | null, body: Omit<SpecializationDto, 'id'>) =>
    id ? put<SpecializationDto>(`/specializations/${id}`, body) : post<SpecializationDto>('/specializations', body),
  medicines: (search?: string, activeOnly = false) => get<MedicineDto[]>('/medicines', { search, activeOnly }),
  saveMedicine: (id: number | null, body: Omit<MedicineDto, 'id'>) =>
    id ? put<MedicineDto>(`/medicines/${id}`, body) : post<MedicineDto>('/medicines', body),
};

export interface DoctorFilters {
  search?: string;
  departmentId?: number;
  isActive?: boolean;
}

export const doctorsApi = {
  list: (filters: DoctorFilters = {}) => get<DoctorListItemDto[]>('/doctors', filters),
  availableToday: () => get<AvailableDoctorDto[]>('/doctors/available-today'),
  get: (id: number) => get<DoctorDto>(`/doctors/${id}`),
  create: (body: SaveDoctorRequest) => post<DoctorDto>('/doctors', body),
  update: (id: number, body: SaveDoctorRequest) => put<DoctorDto>(`/doctors/${id}`, body),
  setStatus: async (id: number, isActive: boolean) => {
    await api.patch(`/doctors/${id}/status`, { isActive });
  },
  schedule: (id: number) => get<ScheduleBlockDto[]>(`/doctors/${id}/schedule`),
  saveSchedule: (id: number, blocks: ScheduleBlockDto[]) => put<ScheduleBlockDto[]>(`/doctors/${id}/schedule`, { blocks }),
  leaves: (id: number) => get<DoctorLeaveDto[]>(`/doctors/${id}/leaves`),
  addLeave: (id: number, body: Omit<DoctorLeaveDto, 'id'>) => post<DoctorLeaveDto>(`/doctors/${id}/leaves`, body),
  deleteLeave: (id: number, leaveId: number) => del(`/doctors/${id}/leaves/${leaveId}`),
  slots: (id: number, date: IsoDate) => get<SlotDto[]>(`/doctors/${id}/slots`, { date }),
};

export const patientsApi = {
  search: (search: string, page: number, pageSize: number) =>
    get<PagedResult<PatientListItemDto>>('/patients', { search: search || undefined, page, pageSize }),
  get: (id: number) => get<PatientDto>(`/patients/${id}`),
  create: (body: SavePatientRequest, confirmDuplicate = false) => post<PatientDto>('/patients', body, { confirmDuplicate }),
  update: (id: number, body: SavePatientRequest) => put<PatientDto>(`/patients/${id}`, body),
  visits: (id: number) => get<PagedResult<VisitDto>>(`/patients/${id}/visits`, { pageSize: 100 }),
  appointments: (id: number, upcoming: boolean) =>
    get<PagedResult<AppointmentDto>>(`/patients/${id}/appointments`, { upcoming, pageSize: 100 }),
  prescriptions: (id: number) => get<PrescriptionListItemDto[]>(`/patients/${id}/prescriptions`),
  vitals: (id: number) => get<VitalSignsDto[]>(`/patients/${id}/vitals`),
  addAllergy: (id: number, body: Omit<AllergyDto, 'id'>) => post<AllergyDto>(`/patients/${id}/allergies`, body),
  deleteAllergy: (id: number, allergyId: number) => del(`/patients/${id}/allergies/${allergyId}`),
  addCondition: (id: number, body: Omit<ConditionDto, 'id'>) => post<ConditionDto>(`/patients/${id}/conditions`, body),
  deleteCondition: (id: number, conditionId: number) => del(`/patients/${id}/conditions/${conditionId}`),
};

export interface AppointmentFilters {
  date?: IsoDate;
  doctorId?: number;
  patientId?: number;
  status?: AppointmentStatus;
}

export const appointmentsApi = {
  list: (filters: AppointmentFilters) => get<PagedResult<AppointmentDto>>('/appointments', { ...filters, pageSize: 100 }),
  queue: (date: IsoDate, doctorId?: number) => get<AppointmentDto[]>('/appointments/queue', { date, doctorId }),
  get: (id: number) => get<AppointmentDto>(`/appointments/${id}`),
  create: (body: CreateAppointmentRequest) => post<AppointmentDto>('/appointments', body),
  reschedule: (id: number, appointmentDate: IsoDate, startTime: TimeOfDay, rowVersion: number) =>
    put<AppointmentDto>(`/appointments/${id}/reschedule`, { appointmentDate, startTime, rowVersion }),
  confirm: (id: number) => post<AppointmentDto>(`/appointments/${id}/confirm`),
  checkIn: (id: number) => post<AppointmentDto>(`/appointments/${id}/check-in`),
  cancel: (id: number, reason: string) => post<AppointmentDto>(`/appointments/${id}/cancel`, { reason }),
  noShow: (id: number) => post<AppointmentDto>(`/appointments/${id}/no-show`),
};

export const consultationsApi = {
  start: (body: { appointmentId?: number; patientId?: number }) => post<ConsultationDto>('/consultations', body),
  get: (id: number) => get<ConsultationDto>(`/consultations/${id}`),
  update: (id: number, body: UpdateConsultationRequest) => put<ConsultationDto>(`/consultations/${id}`, body),
  finalise: (id: number) => post<ConsultationDto>(`/consultations/${id}/finalise`),
  history: (id: number) => get<RevisionDto[]>(`/consultations/${id}/history`),
  addVitals: (id: number, body: SaveVitalsRequest) => post<VitalSignsDto>(`/consultations/${id}/vitals`, body),
  updateVitals: (id: number, vitalsId: number, body: SaveVitalsRequest) =>
    put<VitalSignsDto>(`/consultations/${id}/vitals/${vitalsId}`, body),
  createPrescription: (id: number, body: SavePrescriptionRequest) =>
    post<PrescriptionDto>(`/consultations/${id}/prescription`, body),
};

export const prescriptionsApi = {
  get: (id: number) => get<PrescriptionDto>(`/prescriptions/${id}`),
  update: (id: number, body: SavePrescriptionRequest) => put<PrescriptionDto>(`/prescriptions/${id}`, body),
  finalise: (id: number) => post<PrescriptionDto>(`/prescriptions/${id}/finalise`),
  print: (id: number) => get<PrescriptionPrintDto>(`/prescriptions/${id}/print`),
};

export const invoicesApi = {
  list: (filters: { status?: InvoiceStatus; date?: IsoDate; patientId?: number }) =>
    get<PagedResult<InvoiceListItemDto>>('/invoices', { ...filters, pageSize: 100 }),
  get: (id: number) => get<InvoiceDto>(`/invoices/${id}`),
  markPaid: (id: number, method: PaymentMethod, referenceNo: string | null) =>
    post<InvoiceDto>(`/invoices/${id}/mark-paid`, { method, referenceNo }),
  cancel: (id: number, reason: string) => post<InvoiceDto>(`/invoices/${id}/cancel`, { reason }),
};

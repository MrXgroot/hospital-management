import type { AppointmentFilters, DoctorFilters } from './endpoints';

/** Query key factories. Invalidate by the broadest key that covers the change (e.g. keys.appointments.all). */
export const keys = {
  dashboard: ['dashboard'] as const,
  users: ['users'] as const,
  departments: ['departments'] as const,
  specializations: ['specializations'] as const,
  medicines: (search: string) => ['medicines', search] as const,
  doctors: {
    all: ['doctors'] as const,
    list: (f: DoctorFilters) => ['doctors', 'list', f] as const,
    detail: (id: number) => ['doctors', id] as const,
    schedule: (id: number) => ['doctors', id, 'schedule'] as const,
    leaves: (id: number) => ['doctors', id, 'leaves'] as const,
    slots: (id: number, date: string) => ['doctors', id, 'slots', date] as const,
  },
  patients: {
    all: ['patients'] as const,
    search: (q: string, page: number, size: number) => ['patients', 'search', q, page, size] as const,
    detail: (id: number) => ['patients', id] as const,
    visits: (id: number) => ['patients', id, 'visits'] as const,
    appointments: (id: number) => ['patients', id, 'appointments'] as const,
    prescriptions: (id: number) => ['patients', id, 'prescriptions'] as const,
  },
  appointments: {
    all: ['appointments'] as const,
    list: (f: AppointmentFilters) => ['appointments', 'list', f] as const,
    queue: (date: string, doctorId?: number) => ['appointments', 'queue', date, doctorId ?? null] as const,
  },
  consultations: {
    detail: (id: number) => ['consultations', id] as const,
    history: (id: number) => ['consultations', id, 'history'] as const,
  },
  prescriptions: {
    detail: (id: number) => ['prescriptions', id] as const,
    print: (id: number) => ['prescriptions', id, 'print'] as const,
  },
  invoices: {
    all: ['invoices'] as const,
    list: (f: object) => ['invoices', 'list', f] as const,
    detail: (id: number) => ['invoices', id] as const,
  },
};

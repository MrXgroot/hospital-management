import { lazy, Suspense, type ReactNode } from 'react';
import { createBrowserRouter } from 'react-router';
import { AppLayout } from './components/AppLayout';
import { Loading } from './components/common';
import { RequireAuth, RequireRole } from './auth/RouteGuards';
import LoginPage from './features/auth/LoginPage';
import type { Role } from './types/api';

const DashboardPage = lazy(() => import('./features/dashboard/DashboardPage'));
const PatientListPage = lazy(() => import('./features/patients/PatientListPage'));
const PatientFormPage = lazy(() => import('./features/patients/PatientFormPage'));
const PatientDetailPage = lazy(() => import('./features/patients/PatientDetailPage'));
const DoctorListPage = lazy(() => import('./features/doctors/DoctorListPage'));
const DoctorFormPage = lazy(() => import('./features/doctors/DoctorFormPage'));
const DoctorDetailPage = lazy(() => import('./features/doctors/DoctorDetailPage'));
const AppointmentsPage = lazy(() => import('./features/appointments/AppointmentsPage'));
const BookAppointmentPage = lazy(() => import('./features/appointments/BookAppointmentPage'));
const QueuePage = lazy(() => import('./features/appointments/QueuePage'));
const ConsultationPage = lazy(() => import('./features/consultations/ConsultationPage'));
const PrescriptionPrintPage = lazy(() => import('./features/prescriptions/PrescriptionPrintPage'));
const InvoicesPage = lazy(() => import('./features/billing/InvoicesPage'));
const InvoiceDetailPage = lazy(() => import('./features/billing/InvoiceDetailPage'));
const UsersPage = lazy(() => import('./features/admin/UsersPage'));
const MastersPage = lazy(() => import('./features/admin/MastersPage'));

const page = (node: ReactNode, roles?: Role[]) => (
  <Suspense fallback={<Loading />}>{roles ? <RequireRole roles={roles}>{node}</RequireRole> : node}</Suspense>
);

const FRONT_DESK: Role[] = ['Admin', 'Receptionist'];

export const router = createBrowserRouter([
  { path: '/login', element: <LoginPage /> },
  {
    path: '/print/prescriptions/:id',
    element: <RequireAuth>{page(<PrescriptionPrintPage />)}</RequireAuth>,
  },
  {
    path: '/',
    element: (
      <RequireAuth>
        <AppLayout />
      </RequireAuth>
    ),
    children: [
      { index: true, element: page(<DashboardPage />) },
      { path: 'patients', element: page(<PatientListPage />) },
      { path: 'patients/new', element: page(<PatientFormPage />, FRONT_DESK) },
      { path: 'patients/:id', element: page(<PatientDetailPage />) },
      { path: 'patients/:id/edit', element: page(<PatientFormPage />, FRONT_DESK) },
      { path: 'doctors', element: page(<DoctorListPage />) },
      { path: 'doctors/new', element: page(<DoctorFormPage />, ['Admin']) },
      { path: 'doctors/:id', element: page(<DoctorDetailPage />) },
      { path: 'doctors/:id/edit', element: page(<DoctorFormPage />, ['Admin']) },
      { path: 'appointments', element: page(<AppointmentsPage />) },
      { path: 'appointments/book', element: page(<BookAppointmentPage />, FRONT_DESK) },
      { path: 'queue', element: page(<QueuePage />) },
      { path: 'consultations/:id', element: page(<ConsultationPage />, ['Admin', 'Doctor', 'Nurse']) },
      { path: 'billing', element: page(<InvoicesPage />, FRONT_DESK) },
      { path: 'billing/invoices/:id', element: page(<InvoiceDetailPage />, FRONT_DESK) },
      { path: 'admin/users', element: page(<UsersPage />, ['Admin']) },
      { path: 'admin/masters', element: page(<MastersPage />, ['Admin']) },
    ],
  },
]);

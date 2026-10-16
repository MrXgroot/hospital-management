import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { AxiosError, AxiosHeaders } from 'axios';
import { patientSchema, emptyPatientForm, toPatientRequest } from '../features/patients/patientSchema';
import { errorMessage } from '../api/errors';
import { formatMoney } from '../utils/format';
import { AppLayout } from '../components/AppLayout';
import type { Role } from '../types/api';

const mockUser = vi.hoisted(() => ({ role: 'Receptionist' as Role }));

vi.mock('../auth/AuthProvider', () => ({
  useAuth: () => ({
    user: { id: 1, username: 'u', fullName: 'Test User', role: mockUser.role, doctorId: null, email: null, isActive: true, lastLoginAt: null },
    hasRole: (...roles: Role[]) => roles.includes(mockUser.role),
    logout: vi.fn(),
    login: vi.fn(),
  }),
}));

describe('patient form schema', () => {
  it('requires either a date of birth or an age', () => {
    const result = patientSchema.safeParse({ ...emptyPatientForm, firstName: 'A', lastName: 'B', phone: '9876543210', ageMode: 'age', ageYears: '' });
    expect(result.success).toBe(false);
    expect(result.error?.issues.some((i) => i.path[0] === 'ageYears')).toBe(true);
  });

  it('rejects a short phone number', () => {
    const result = patientSchema.safeParse({ ...emptyPatientForm, firstName: 'A', lastName: 'B', phone: '12345', ageYears: '30' });
    expect(result.success).toBe(false);
  });

  it('maps an age-only form to a request without date of birth', () => {
    const values = { ...emptyPatientForm, firstName: ' Asha ', lastName: 'Rao', phone: '9876543210', ageYears: '34', email: '' };
    expect(patientSchema.safeParse(values).success).toBe(true);
    const request = toPatientRequest(values);
    expect(request).toMatchObject({ firstName: 'Asha', ageYears: 34, dateOfBirth: null, email: null });
  });
});

describe('error messages', () => {
  const problem = (data: object, status = 409) =>
    new AxiosError('fail', 'ERR', undefined, undefined, {
      data, status, statusText: '', headers: {}, config: { headers: new AxiosHeaders() },
    });

  it('uses a friendly message for a taken slot', () => {
    expect(errorMessage(problem({ code: 'SLOT_UNAVAILABLE', title: 'x' }))).toMatch(/just taken/);
  });

  it('falls back to the server title', () => {
    expect(errorMessage(problem({ code: 'PATIENT_ALREADY_BOOKED', title: 'Already booked' }, 422))).toBe('Already booked');
  });
});

describe('formatting', () => {
  it('formats rupees', () => {
    expect(formatMoney(1500)).toContain('1,500');
  });
});

describe('navigation by role', () => {
  const renderLayout = () =>
    render(
      <QueryClientProvider client={new QueryClient()}>
        <MemoryRouter><AppLayout /></MemoryRouter>
      </QueryClientProvider>,
    );

  it('shows billing but not admin items to receptionists', () => {
    mockUser.role = 'Receptionist';
    renderLayout();
    expect(screen.getAllByText('Billing').length).toBeGreaterThan(0);
    expect(screen.queryByText('Users')).not.toBeInTheDocument();
  });

  it('hides billing from doctors', () => {
    mockUser.role = 'Doctor';
    renderLayout();
    expect(screen.queryByText('Billing')).not.toBeInTheDocument();
    expect(screen.getAllByText("Today's queue").length).toBeGreaterThan(0);
  });
});

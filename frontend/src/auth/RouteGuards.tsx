import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router';
import { Alert } from '@mui/material';
import { useAuth } from './AuthProvider';
import type { Role } from '../types/api';

export function RequireAuth({ children }: { children: ReactNode }) {
  const { user } = useAuth();
  const location = useLocation();
  if (!user) return <Navigate to="/login" replace state={{ from: location.pathname }} />;
  return <>{children}</>;
}

/** UI-level guard only. The API enforces the same roles. */
export function RequireRole({ roles, children }: { roles: Role[]; children: ReactNode }) {
  const { hasRole } = useAuth();
  if (!hasRole(...roles)) return <Alert severity="warning">You don't have access to this page.</Alert>;
  return <>{children}</>;
}

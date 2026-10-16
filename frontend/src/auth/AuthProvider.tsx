import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { LOGOUT_EVENT, session } from '../api/client';
import { authApi } from '../api/endpoints';
import type { Role, UserDto } from '../types/api';

interface AuthContextValue {
  user: UserDto | null;
  login: (username: string, password: string) => Promise<UserDto>;
  logout: () => Promise<void>;
  hasRole: (...roles: Role[]) => boolean;
}

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<UserDto | null>(() => session.get()?.user ?? null);
  const queryClient = useQueryClient();

  useEffect(() => {
    const onLogout = () => {
      setUser(null);
      queryClient.clear();
    };
    window.addEventListener(LOGOUT_EVENT, onLogout);
    return () => window.removeEventListener(LOGOUT_EVENT, onLogout);
  }, [queryClient]);

  const login = useCallback(async (username: string, password: string) => {
    const response = await authApi.login(username, password);
    session.set(response);
    setUser(response.user);
    return response.user;
  }, []);

  const logout = useCallback(async () => {
    const current = session.get();
    if (current) await authApi.logout(current.refreshToken).catch(() => undefined);
    session.clear();
    setUser(null);
    queryClient.clear();
  }, [queryClient]);

  const hasRole = useCallback((...roles: Role[]) => !!user && roles.includes(user.role), [user]);

  const value = useMemo(() => ({ user, login, logout, hasRole }), [user, login, logout, hasRole]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider');
  return ctx;
}

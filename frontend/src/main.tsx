import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { RouterProvider } from 'react-router';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { CssBaseline, ThemeProvider } from '@mui/material';
import { isAxiosError } from 'axios';
import { theme } from './theme';
import { AuthProvider } from './auth/AuthProvider';
import { NotificationProvider } from './components/Notifications';
import { router } from './routes';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      refetchOnWindowFocus: false,
      // Don't retry client errors (404, 403, validation); retry transient failures once.
      retry: (count, error) => count < 1 && !(isAxiosError(error) && (error.response?.status ?? 500) < 500),
    },
  },
});

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <ThemeProvider theme={theme}>
        <CssBaseline />
        <NotificationProvider>
          <AuthProvider>
            <RouterProvider router={router} />
          </AuthProvider>
        </NotificationProvider>
      </ThemeProvider>
    </QueryClientProvider>
  </StrictMode>,
);

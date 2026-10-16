import { createContext, useCallback, useContext, useState, type ReactNode } from 'react';
import { Alert, Snackbar, type AlertColor } from '@mui/material';

type Notify = (message: string, severity?: AlertColor) => void;

const NotifyContext = createContext<Notify>(() => undefined);

export function NotificationProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<{ message: string; severity: AlertColor; key: number } | null>(null);

  const notify = useCallback<Notify>((message, severity = 'success') => {
    setState({ message, severity, key: Date.now() });
  }, []);

  return (
    <NotifyContext.Provider value={notify}>
      {children}
      <Snackbar
        key={state?.key}
        open={!!state}
        autoHideDuration={state?.severity === 'error' ? 6000 : 3000}
        onClose={() => setState(null)}
        anchorOrigin={{ vertical: 'bottom', horizontal: 'right' }}
      >
        {state ? (
          <Alert severity={state.severity} variant="filled" onClose={() => setState(null)} sx={{ minWidth: 280 }}>
            {state.message}
          </Alert>
        ) : undefined}
      </Snackbar>
    </NotifyContext.Provider>
  );
}

export const useNotify = () => useContext(NotifyContext);

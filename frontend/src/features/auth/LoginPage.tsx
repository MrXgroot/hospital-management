import { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router';
import { Alert, Box, Button, Paper, Stack, TextField, Typography } from '@mui/material';
import LocalHospitalIcon from '@mui/icons-material/LocalHospital';
import { useAuth } from '../../auth/AuthProvider';
import { errorMessage } from '../../api/errors';

export default function LoginPage() {
  const { user, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  if (user) return <Navigate to="/" replace />;

  const submit = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await login(username.trim(), password);
      const from = (location.state as { from?: string } | null)?.from;
      navigate(from && from !== '/login' ? from : '/', { replace: true });
    } catch (err) {
      setError(errorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Box
      sx={{
        minHeight: '100vh', display: 'grid', placeItems: 'center', px: 2,
        background: 'linear-gradient(160deg, #0c3b3a 0%, #0f766e 55%, #e6f2f1 55%)',
      }}
    >
      <Paper component="form" onSubmit={submit} sx={{ p: { xs: 3, sm: 4 }, width: '100%', maxWidth: 400, boxShadow: '0 20px 50px rgba(12,59,58,0.18)' }}>
        <Stack spacing={2.5}>
          <Stack direction="row" spacing={1.5} alignItems="center">
            <LocalHospitalIcon color="primary" sx={{ fontSize: 36 }} />
            <Box>
              <Typography variant="h6" lineHeight={1.2}>City Care Hospital</Typography>
              <Typography variant="body2" color="text.secondary">Sign in to continue</Typography>
            </Box>
          </Stack>
          {error && <Alert severity="error">{error}</Alert>}
          <TextField label="Username" value={username} onChange={(e) => setUsername(e.target.value)} autoComplete="username" autoFocus required />
          <TextField label="Password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} autoComplete="current-password" required />
          <Button type="submit" variant="contained" size="large" disabled={busy || !username || !password}>
            {busy ? 'Signing in…' : 'Sign in'}
          </Button>
        </Stack>
      </Paper>
    </Box>
  );
}

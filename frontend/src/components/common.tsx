import { useState, type ReactNode } from 'react';
import {
  Alert, Box, Button, Chip, CircularProgress, Dialog, DialogActions, DialogContent, DialogTitle, Paper, Stack,
  TextField, Typography, type ChipProps,
} from '@mui/material';
import { errorMessage } from '../api/errors';

export function PageHeader({ title, subtitle, actions }: { title: string; subtitle?: ReactNode; actions?: ReactNode }) {
  return (
    <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ mb: 3 }} justifyContent="space-between" alignItems={{ sm: 'center' }}>
      <Box>
        <Typography variant="h5" component="h1">{title}</Typography>
        {subtitle && <Typography variant="body2" component="div" color="text.secondary" sx={{ mt: 0.5 }}>{subtitle}</Typography>}
      </Box>
      {actions && <Stack direction="row" spacing={1} flexWrap="wrap" useFlexGap>{actions}</Stack>}
    </Stack>
  );
}

export function Section({ title, action, children, sx }: { title?: string; action?: ReactNode; children: ReactNode; sx?: object }) {
  return (
    <Paper sx={{ p: { xs: 2, md: 2.5 }, ...sx }}>
      {(title || action) && (
        <Stack direction="row" justifyContent="space-between" alignItems="center" sx={{ mb: 2 }}>
          {title && <Typography variant="subtitle1" fontWeight={650}>{title}</Typography>}
          {action}
        </Stack>
      )}
      {children}
    </Paper>
  );
}

export function Loading({ label = 'Loading…' }: { label?: string }) {
  return (
    <Stack alignItems="center" justifyContent="center" spacing={1.5} sx={{ py: 6 }} role="status">
      <CircularProgress size={28} />
      <Typography variant="body2" color="text.secondary">{label}</Typography>
    </Stack>
  );
}

export function ErrorAlert({ error, onRetry }: { error: unknown; onRetry?: () => void }) {
  return (
    <Alert severity="error" action={onRetry && <Button color="inherit" size="small" onClick={onRetry}>Retry</Button>}>
      {errorMessage(error)}
    </Alert>
  );
}

export function EmptyState({ message, action }: { message: string; action?: ReactNode }) {
  return (
    <Stack alignItems="center" spacing={1.5} sx={{ py: 5, color: 'text.secondary' }}>
      <Typography variant="body2">{message}</Typography>
      {action}
    </Stack>
  );
}

export function LabelValue({ label, value }: { label: string; value: ReactNode }) {
  return (
    <Box>
      <Typography variant="caption" color="text.secondary" sx={{ textTransform: 'uppercase', letterSpacing: '0.04em' }}>
        {label}
      </Typography>
      <Typography variant="body2" component="div" sx={{ mt: 0.25, wordBreak: 'break-word' }}>{value || '—'}</Typography>
    </Box>
  );
}

const STATUS_COLORS: Record<string, ChipProps['color']> = {
  Booked: 'info',
  Confirmed: 'primary',
  CheckedIn: 'warning',
  InConsultation: 'secondary',
  Completed: 'success',
  Cancelled: 'default',
  NoShow: 'error',
  Draft: 'warning',
  Finalised: 'success',
  Unpaid: 'warning',
  Paid: 'success',
  Active: 'success',
  Inactive: 'default',
};

const STATUS_LABELS: Record<string, string> = {
  CheckedIn: 'Checked in',
  InConsultation: 'In consultation',
  NoShow: 'No-show',
};

/** Single source of truth for status colours across the app. */
export function StatusChip({ status, size = 'small' }: { status: string; size?: ChipProps['size'] }) {
  return (
    <Chip
      label={STATUS_LABELS[status] ?? status}
      color={STATUS_COLORS[status] ?? 'default'}
      size={size}
      variant={status === 'Cancelled' ? 'outlined' : 'filled'}
    />
  );
}

export function ConfirmDialog(props: {
  open: boolean;
  title: string;
  message: ReactNode;
  confirmLabel?: string;
  danger?: boolean;
  loading?: boolean;
  onClose: () => void;
  onConfirm: () => void;
}) {
  return (
    <Dialog open={props.open} onClose={props.onClose} maxWidth="xs" fullWidth>
      <DialogTitle>{props.title}</DialogTitle>
      <DialogContent><Typography variant="body2">{props.message}</Typography></DialogContent>
      <DialogActions>
        <Button onClick={props.onClose}>Back</Button>
        <Button variant="contained" color={props.danger ? 'error' : 'primary'} onClick={props.onConfirm} disabled={props.loading}>
          {props.confirmLabel ?? 'Confirm'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

/** Asks for a required free-text reason (cancellations, amendments). */
export function ReasonDialog(props: {
  open: boolean;
  title: string;
  label: string;
  helper?: string;
  confirmLabel?: string;
  danger?: boolean;
  loading?: boolean;
  onClose: () => void;
  onConfirm: (reason: string) => void;
}) {
  const [reason, setReason] = useState('');
  const close = () => {
    setReason('');
    props.onClose();
  };
  return (
    <Dialog open={props.open} onClose={close} maxWidth="sm" fullWidth>
      <DialogTitle>{props.title}</DialogTitle>
      <DialogContent>
        {props.helper && <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>{props.helper}</Typography>}
        <TextField
          autoFocus
          label={props.label}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
          multiline
          minRows={2}
          slotProps={{ htmlInput: { maxLength: 300 } }}
          sx={{ mt: 1 }}
        />
      </DialogContent>
      <DialogActions>
        <Button onClick={close}>Back</Button>
        <Button
          variant="contained"
          color={props.danger ? 'error' : 'primary'}
          disabled={!reason.trim() || props.loading}
          onClick={() => {
            props.onConfirm(reason.trim());
            setReason('');
          }}
        >
          {props.confirmLabel ?? 'Confirm'}
        </Button>
      </DialogActions>
    </Dialog>
  );
}

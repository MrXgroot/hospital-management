import { useState } from 'react';
import { useNavigate } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert, Box, Button, Chip, Dialog, DialogActions, DialogContent, DialogTitle, IconButton, ListItemText, Menu, MenuItem,
  Stack, TextField, Typography,
} from '@mui/material';
import MoreIcon from '@mui/icons-material/MoreVert';
import { appointmentsApi, consultationsApi, doctorsApi } from '../../api/endpoints';
import { keys } from '../../api/queryKeys';
import { errorMessage, getErrorCode } from '../../api/errors';
import { useAuth } from '../../auth/AuthProvider';
import { Loading, ReasonDialog, StatusChip } from '../../components/common';
import { useNotify } from '../../components/Notifications';
import { addDaysIso, formatDate, formatDateTime, formatTimeOfDay, todayIso } from '../../utils/format';
import type { AppointmentDto, TimeOfDay } from '../../types/api';

/** Grid of slots for a doctor/date. Unavailable slots are shown disabled so the day's shape stays visible. */
export function SlotPicker({ doctorId, date, value, onChange }: {
  doctorId: number | null;
  date: string;
  value: TimeOfDay | null;
  onChange: (slot: TimeOfDay) => void;
}) {
  const { data, isLoading, error } = useQuery({
    queryKey: keys.doctors.slots(doctorId ?? 0, date),
    queryFn: () => doctorsApi.slots(doctorId!, date),
    enabled: !!doctorId && !!date,
  });
  if (!doctorId) return <Typography variant="body2" color="text.secondary">Select a doctor to see available slots.</Typography>;
  if (isLoading) return <Loading label="Loading slots…" />;
  if (error) return <Alert severity="error">{errorMessage(error)}</Alert>;
  if (!data?.length) return <Alert severity="info">The doctor is not available on this date.</Alert>;
  const free = data.filter((s) => s.isAvailable).length;
  return (
    <Box>
      <Typography variant="caption" color="text.secondary">{free} of {data.length} slots free</Typography>
      <Box sx={{ display: 'grid', gap: 1, mt: 1, gridTemplateColumns: 'repeat(auto-fill, minmax(92px, 1fr))' }} role="listbox" aria-label="Time slots">
        {data.map((s) => (
          <Button
            key={s.startTime}
            size="small"
            role="option"
            aria-selected={value === s.startTime}
            variant={value === s.startTime ? 'contained' : 'outlined'}
            disabled={!s.isAvailable}
            onClick={() => onChange(s.startTime)}
            sx={{ fontVariantNumeric: 'tabular-nums' }}
          >
            {formatTimeOfDay(s.startTime)}
          </Button>
        ))}
      </Box>
    </Box>
  );
}

export function RescheduleDialog({ appointment, onClose }: { appointment: AppointmentDto | null; onClose: () => void }) {
  const queryClient = useQueryClient();
  const notify = useNotify();
  const [date, setDate] = useState(appointment?.appointmentDate ?? todayIso());
  const [slot, setSlot] = useState<TimeOfDay | null>(null);
  const mutation = useMutation({
    mutationFn: () => appointmentsApi.reschedule(appointment!.id, date, slot!, appointment!.rowVersion),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: keys.appointments.all });
      queryClient.invalidateQueries({ queryKey: keys.dashboard });
      notify('Appointment rescheduled');
      onClose();
    },
    onError: (e) => {
      if (getErrorCode(e) === 'SLOT_UNAVAILABLE') {
        setSlot(null);
        queryClient.invalidateQueries({ queryKey: ['doctors', appointment!.doctorId, 'slots'] });
      }
      notify(errorMessage(e), 'error');
    },
  });
  if (!appointment) return null;
  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Reschedule · {appointment.patientName}</DialogTitle>
      <DialogContent>
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Currently {formatDate(appointment.appointmentDate)} at {formatTimeOfDay(appointment.startTime)} with {appointment.doctorName}
        </Typography>
        <TextField
          label="New date" type="date" value={date} sx={{ mb: 2, maxWidth: 220 }}
          onChange={(e) => { setDate(e.target.value); setSlot(null); }}
          slotProps={{ inputLabel: { shrink: true }, htmlInput: { min: todayIso(), max: addDaysIso(todayIso(), 90) } }}
        />
        <SlotPicker doctorId={appointment.doctorId} date={date} value={slot} onChange={setSlot} />
      </DialogContent>
      <DialogActions>
        <Button onClick={onClose}>Back</Button>
        <Button variant="contained" disabled={!slot || mutation.isPending} onClick={() => mutation.mutate()}>Reschedule</Button>
      </DialogActions>
    </Dialog>
  );
}

export function HistoryDialog({ appointmentId, onClose }: { appointmentId: number | null; onClose: () => void }) {
  const { data, isLoading } = useQuery({
    queryKey: ['appointments', appointmentId, 'detail'],
    queryFn: () => appointmentsApi.get(appointmentId!),
    enabled: !!appointmentId,
  });
  if (!appointmentId) return null;
  return (
    <Dialog open onClose={onClose} maxWidth="sm" fullWidth>
      <DialogTitle>Appointment history</DialogTitle>
      <DialogContent>
        {isLoading && <Loading />}
        <Stack spacing={1.5}>
          {data?.history?.map((h, i) => (
            <Box key={i} sx={{ borderLeft: 3, borderColor: 'primary.light', pl: 1.5 }}>
              <Stack direction="row" spacing={1} alignItems="center">
                {h.fromStatus && h.fromStatus !== h.toStatus && <><StatusChip status={h.fromStatus} /><span>→</span></>}
                <StatusChip status={h.toStatus} />
                {h.oldDate && <Typography variant="body2">moved from {formatDate(h.oldDate)} {formatTimeOfDay(h.oldStartTime)}</Typography>}
              </Stack>
              <Typography variant="caption" color="text.secondary">
                {formatDateTime(h.changedAt)} · {h.changedByName ?? 'system'}{h.note ? ` · ${h.note}` : ''}
              </Typography>
            </Box>
          ))}
        </Stack>
      </DialogContent>
      <DialogActions><Button onClick={onClose}>Close</Button></DialogActions>
    </Dialog>
  );
}

/** Role-aware action menu for one appointment; used by the appointment list and the queue. */
export function AppointmentActions({ appointment }: { appointment: AppointmentDto }) {
  const { hasRole } = useAuth();
  const navigate = useNavigate();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [anchor, setAnchor] = useState<HTMLElement | null>(null);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [reschedule, setReschedule] = useState(false);
  const [history, setHistory] = useState(false);

  const done = (message: string) => {
    queryClient.invalidateQueries({ queryKey: keys.appointments.all });
    queryClient.invalidateQueries({ queryKey: keys.dashboard });
    queryClient.invalidateQueries({ queryKey: keys.invoices.all });
    notify(message);
  };
  const run = useMutation({
    mutationFn: (action: () => Promise<unknown>) => action(),
    onError: (e) => notify(errorMessage(e), 'error'),
  });
  const act = (action: () => Promise<unknown>, message: string) => {
    setAnchor(null);
    run.mutate(action, { onSuccess: () => done(message) });
  };

  const startConsultation = useMutation({
    mutationFn: () => consultationsApi.start({ appointmentId: appointment.id }),
    onSuccess: (c) => {
      queryClient.invalidateQueries({ queryKey: keys.appointments.all });
      navigate(`/consultations/${c.id}`);
    },
    onError: (e) => notify(errorMessage(e), 'error'),
  });

  const s = appointment.status;
  const frontDesk = hasRole('Admin', 'Receptionist');
  const isToday = appointment.appointmentDate === todayIso();
  const items: { label: string; onClick: () => void; danger?: boolean }[] = [];
  if (frontDesk && s === 'Booked') items.push({ label: 'Confirm', onClick: () => act(() => appointmentsApi.confirm(appointment.id), 'Appointment confirmed') });
  if (hasRole('Admin', 'Receptionist', 'Nurse') && isToday && (s === 'Booked' || s === 'Confirmed'))
    items.push({ label: 'Check in', onClick: () => act(() => appointmentsApi.checkIn(appointment.id), 'Patient checked in · invoice created') });
  if (frontDesk && (s === 'Booked' || s === 'Confirmed')) items.push({ label: 'Reschedule', onClick: () => { setAnchor(null); setReschedule(true); } });
  if (frontDesk && (s === 'Booked' || s === 'Confirmed')) items.push({ label: 'Mark no-show', onClick: () => act(() => appointmentsApi.noShow(appointment.id), 'Marked as no-show') });
  if (frontDesk && (s === 'Booked' || s === 'Confirmed' || s === 'CheckedIn'))
    items.push({ label: 'Cancel appointment', danger: true, onClick: () => { setAnchor(null); setCancelOpen(true); } });
  if (frontDesk && appointment.invoiceId) items.push({ label: 'View invoice', onClick: () => navigate(`/billing/invoices/${appointment.invoiceId}`) });
  items.push({ label: 'Status history', onClick: () => { setAnchor(null); setHistory(true); } });

  const primary =
    hasRole('Doctor') && s === 'CheckedIn' ? (
      <Button size="small" variant="contained" disabled={startConsultation.isPending} onClick={() => startConsultation.mutate()}>Start consultation</Button>
    ) : appointment.consultationId && hasRole('Admin', 'Doctor', 'Nurse') ? (
      <Button size="small" variant="outlined" onClick={() => navigate(`/consultations/${appointment.consultationId}`)}>
        {hasRole('Nurse') ? 'Vitals' : 'Open'}
      </Button>
    ) : frontDesk && isToday && (s === 'Booked' || s === 'Confirmed') ? (
      <Button size="small" variant="outlined" disabled={run.isPending} onClick={() => act(() => appointmentsApi.checkIn(appointment.id), 'Patient checked in · invoice created')}>
        Check in
      </Button>
    ) : null;

  return (
    <Stack direction="row" spacing={0.5} alignItems="center" justifyContent="flex-end">
      {primary}
      <IconButton size="small" aria-label="More actions" onClick={(e) => setAnchor(e.currentTarget)}><MoreIcon fontSize="small" /></IconButton>
      <Menu anchorEl={anchor} open={!!anchor} onClose={() => setAnchor(null)}>
        {items.map((i) => (
          <MenuItem key={i.label} onClick={i.onClick} sx={i.danger ? { color: 'error.main' } : undefined}>
            <ListItemText>{i.label}</ListItemText>
          </MenuItem>
        ))}
      </Menu>
      <ReasonDialog
        open={cancelOpen}
        title={`Cancel appointment · ${appointment.patientName}`}
        label="Reason for cancellation"
        helper={s === 'CheckedIn' ? 'The unpaid invoice for this visit will also be cancelled.' : undefined}
        confirmLabel="Cancel appointment"
        danger
        loading={run.isPending}
        onClose={() => setCancelOpen(false)}
        onConfirm={(reason) => {
          setCancelOpen(false);
          run.mutate(() => appointmentsApi.cancel(appointment.id, reason), { onSuccess: () => done('Appointment cancelled') });
        }}
      />
      {reschedule && <RescheduleDialog appointment={appointment} onClose={() => setReschedule(false)} />}
      {history && <HistoryDialog appointmentId={appointment.id} onClose={() => setHistory(false)} />}
    </Stack>
  );
}

export function TypeChip({ type }: { type: AppointmentDto['appointmentType'] }) {
  if (type === 'Scheduled') return null;
  return <Chip size="small" variant="outlined" label={type === 'WalkIn' ? 'Walk-in' : 'Follow-up'} sx={{ ml: 1 }} />;
}

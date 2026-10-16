import { useState } from 'react';
import { Link as RouterLink, useParams } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert, Box, Button, Chip, IconButton, List, ListItem, ListItemText, MenuItem, Stack, Switch, TextField, Typography,
} from '@mui/material';
import EditIcon from '@mui/icons-material/EditOutlined';
import DeleteIcon from '@mui/icons-material/DeleteOutline';
import AddIcon from '@mui/icons-material/Add';
import { doctorsApi } from '../../api/endpoints';
import { keys } from '../../api/queryKeys';
import { errorMessage, getProblem } from '../../api/errors';
import { useAuth } from '../../auth/AuthProvider';
import { ConfirmDialog, ErrorAlert, LabelValue, Loading, PageHeader, Section, StatusChip } from '../../components/common';
import { useNotify } from '../../components/Notifications';
import { WEEKDAYS, formatDate, formatMoney, formatTimeOfDay, todayIso } from '../../utils/format';
import type { DoctorDto, ScheduleBlockDto } from '../../types/api';

export default function DoctorDetailPage() {
  const doctorId = Number(useParams().id);
  const { hasRole } = useAuth();
  const isAdmin = hasRole('Admin');
  const { data: doctor, error, isLoading } = useQuery({ queryKey: keys.doctors.detail(doctorId), queryFn: () => doctorsApi.get(doctorId) });

  if (isLoading) return <Loading />;
  if (error || !doctor) return <ErrorAlert error={error} />;

  return (
    <>
      <PageHeader
        title={doctor.fullName}
        subtitle={`${doctor.doctorCode} · ${doctor.departmentName} · ${doctor.qualification}`}
        actions={isAdmin && <Button variant="outlined" startIcon={<EditIcon />} component={RouterLink} to={`/doctors/${doctor.id}/edit`}>Edit profile</Button>}
      />
      <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', lg: '1fr 1fr' }, alignItems: 'start' }}>
        <Stack spacing={2}>
          <Section title="Profile" action={<StatusToggle doctor={doctor} canEdit={isAdmin} />}>
            <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)' } }}>
              <LabelValue label="Consultation fee" value={formatMoney(doctor.consultationFee)} />
              <LabelValue label="Registration no." value={doctor.registrationNumber} />
              <LabelValue label="Phone" value={doctor.phone} />
              <LabelValue label="Email" value={doctor.email} />
              <LabelValue label="Experience" value={doctor.yearsOfExperience != null ? `${doctor.yearsOfExperience} years` : null} />
              <LabelValue
                label="Specializations"
                value={
                  <Stack direction="row" spacing={0.5} flexWrap="wrap" useFlexGap component="span">
                    {doctor.specializations.map((s) => (
                      <Chip key={s.specializationId} label={s.name} size="small" color={s.isPrimary ? 'primary' : 'default'} variant={s.isPrimary ? 'filled' : 'outlined'} />
                    ))}
                  </Stack>
                }
              />
            </Box>
          </Section>
          <LeavesSection doctorId={doctor.id} canEdit={isAdmin} />
        </Stack>
        <ScheduleSection doctorId={doctor.id} canEdit={isAdmin} />
      </Box>
    </>
  );
}

function StatusToggle({ doctor, canEdit }: { doctor: DoctorDto; canEdit: boolean }) {
  const queryClient = useQueryClient();
  const notify = useNotify();
  const [blocking, setBlocking] = useState<{ id: number; appointmentDate: string; startTime: string; patientName: string }[] | null>(null);
  const [confirm, setConfirm] = useState(false);

  const mutation = useMutation({
    mutationFn: (isActive: boolean) => doctorsApi.setStatus(doctor.id, isActive),
    onSuccess: (_, isActive) => {
      setConfirm(false);
      queryClient.invalidateQueries({ queryKey: keys.doctors.all });
      notify(isActive ? 'Doctor activated' : 'Doctor deactivated');
    },
    onError: (e) => {
      setConfirm(false);
      const problem = getProblem(e);
      if (problem?.code === 'DOCTOR_HAS_FUTURE_APPOINTMENTS') setBlocking(problem.appointments as typeof blocking);
      else notify(errorMessage(e), 'error');
    },
  });

  if (!canEdit) return <StatusChip status={doctor.isActive ? 'Active' : 'Inactive'} />;
  return (
    <>
      <Stack direction="row" alignItems="center" spacing={0.5}>
        <Typography variant="body2" color="text.secondary">{doctor.isActive ? 'Active' : 'Inactive'}</Typography>
        <Switch
          checked={doctor.isActive}
          onChange={(_, checked) => (checked ? mutation.mutate(true) : setConfirm(true))}
          slotProps={{ input: { 'aria-label': 'Doctor active' } }}
        />
      </Stack>
      <ConfirmDialog
        open={confirm}
        title="Deactivate doctor?"
        message="The doctor will be hidden from booking. Their history stays visible."
        confirmLabel="Deactivate"
        danger
        loading={mutation.isPending}
        onClose={() => setConfirm(false)}
        onConfirm={() => mutation.mutate(false)}
      />
      {blocking && (
        <Alert severity="warning" onClose={() => setBlocking(null)} sx={{ position: 'fixed', bottom: 24, left: '50%', transform: 'translateX(-50%)', zIndex: 1400, maxWidth: 560 }}>
          Reschedule or cancel these appointments first:
          <List dense>
            {blocking.map((a) => (
              <ListItem key={a.id} disablePadding>
                <ListItemText primary={`${formatDate(a.appointmentDate)} ${formatTimeOfDay(a.startTime)} · ${a.patientName}`} />
              </ListItem>
            ))}
          </List>
        </Alert>
      )}
    </>
  );
}

function ScheduleSection({ doctorId, canEdit }: { doctorId: number; canEdit: boolean }) {
  const queryClient = useQueryClient();
  const notify = useNotify();
  const { data, isLoading, error } = useQuery({ queryKey: keys.doctors.schedule(doctorId), queryFn: () => doctorsApi.schedule(doctorId) });
  const [draft, setDraft] = useState<ScheduleBlockDto[] | null>(null);
  const [errors, setErrors] = useState<string[]>([]);

  const save = useMutation({
    mutationFn: (blocks: ScheduleBlockDto[]) => doctorsApi.saveSchedule(doctorId, blocks),
    onSuccess: () => {
      setDraft(null);
      setErrors([]);
      queryClient.invalidateQueries({ queryKey: keys.doctors.all });
      notify('Weekly schedule saved');
    },
    onError: (e) => {
      const problem = getProblem(e);
      setErrors(problem?.errors ? Object.values(problem.errors).flat() : [errorMessage(e)]);
    },
  });

  if (isLoading) return <Section title="Weekly schedule"><Loading /></Section>;
  if (error) return <ErrorAlert error={error} />;

  const editing = draft !== null;
  const blocks = draft ?? data ?? [];
  const update = (i: number, patch: Partial<ScheduleBlockDto>) => setDraft(blocks.map((b, j) => (i === j ? { ...b, ...patch } : b)));

  return (
    <Section
      title="Weekly schedule"
      action={canEdit && !editing && <Button size="small" startIcon={<EditIcon />} onClick={() => setDraft(data ?? [])}>Edit</Button>}
    >
      {errors.length > 0 && <Alert severity="error" sx={{ mb: 2 }}>{errors.map((e) => <div key={e}>{e}</div>)}</Alert>}
      {!editing ? (
        blocks.length === 0 ? (
          <Typography variant="body2" color="text.secondary">No working hours set. The doctor can't be booked.</Typography>
        ) : (
          <Stack spacing={1}>
            {WEEKDAYS.map((day, d) => {
              const dayBlocks = blocks.filter((b) => b.dayOfWeek === d);
              return (
                <Stack key={day} direction="row" spacing={2} alignItems="center">
                  <Typography variant="body2" sx={{ width: 96 }} fontWeight={600}>{day}</Typography>
                  <Typography variant="body2" color={dayBlocks.length ? 'text.primary' : 'text.secondary'}>
                    {dayBlocks.length
                      ? dayBlocks.map((b) => `${formatTimeOfDay(b.startTime)} – ${formatTimeOfDay(b.endTime)} (${b.slotDurationMinutes} min)`).join(', ')
                      : 'Off'}
                  </Typography>
                </Stack>
              );
            })}
          </Stack>
        )
      ) : (
        <Stack spacing={1.5}>
          {blocks.map((b, i) => (
            <Stack key={i} direction={{ xs: 'column', sm: 'row' }} spacing={1} alignItems={{ sm: 'center' }}>
              <TextField select label="Day" value={b.dayOfWeek} onChange={(e) => update(i, { dayOfWeek: Number(e.target.value) })} sx={{ minWidth: 140 }}>
                {WEEKDAYS.map((d, n) => <MenuItem key={d} value={n}>{d}</MenuItem>)}
              </TextField>
              <TextField label="Start" type="time" value={b.startTime} onChange={(e) => update(i, { startTime: e.target.value })} slotProps={{ inputLabel: { shrink: true } }} />
              <TextField label="End" type="time" value={b.endTime} onChange={(e) => update(i, { endTime: e.target.value })} slotProps={{ inputLabel: { shrink: true } }} />
              <TextField select label="Slot" value={b.slotDurationMinutes} onChange={(e) => update(i, { slotDurationMinutes: Number(e.target.value) })} sx={{ minWidth: 100 }}>
                {[10, 15, 20, 30, 45, 60].map((m) => <MenuItem key={m} value={m}>{m} min</MenuItem>)}
              </TextField>
              <IconButton aria-label="Remove block" onClick={() => setDraft(blocks.filter((_, j) => j !== i))}><DeleteIcon /></IconButton>
            </Stack>
          ))}
          <Box>
            <Button size="small" startIcon={<AddIcon />}
              onClick={() => setDraft([...blocks, { dayOfWeek: 1, startTime: '09:00', endTime: '13:00', slotDurationMinutes: 15 }])}>
              Add time block
            </Button>
          </Box>
          <Stack direction="row" spacing={1} justifyContent="flex-end">
            <Button onClick={() => { setDraft(null); setErrors([]); }}>Cancel</Button>
            <Button variant="contained" disabled={save.isPending} onClick={() => save.mutate(blocks)}>Save schedule</Button>
          </Stack>
        </Stack>
      )}
    </Section>
  );
}

function LeavesSection({ doctorId, canEdit }: { doctorId: number; canEdit: boolean }) {
  const queryClient = useQueryClient();
  const notify = useNotify();
  const [from, setFrom] = useState(todayIso());
  const [to, setTo] = useState(todayIso());
  const [reason, setReason] = useState('');
  const { data } = useQuery({ queryKey: keys.doctors.leaves(doctorId), queryFn: () => doctorsApi.leaves(doctorId) });
  const invalidate = () => queryClient.invalidateQueries({ queryKey: keys.doctors.all });

  const add = useMutation({
    mutationFn: () => doctorsApi.addLeave(doctorId, { fromDate: from, toDate: to, reason: reason.trim() || null }),
    onSuccess: () => {
      setReason('');
      invalidate();
      notify('Leave added');
    },
    onError: (e) => notify(errorMessage(e), 'error'),
  });
  const remove = useMutation({
    mutationFn: (leaveId: number) => doctorsApi.deleteLeave(doctorId, leaveId),
    onSuccess: invalidate,
    onError: (e) => notify(errorMessage(e), 'error'),
  });

  return (
    <Section title="Leave / unavailability">
      {data?.length === 0 && <Typography variant="body2" color="text.secondary">No leave recorded.</Typography>}
      <List dense disablePadding>
        {data?.map((l) => (
          <ListItem key={l.id} disableGutters
            secondaryAction={canEdit && <IconButton edge="end" aria-label="Remove leave" onClick={() => remove.mutate(l.id)}><DeleteIcon fontSize="small" /></IconButton>}>
            <ListItemText primary={`${formatDate(l.fromDate)} – ${formatDate(l.toDate)}`} secondary={l.reason} />
          </ListItem>
        ))}
      </List>
      {canEdit && (
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1} sx={{ mt: 2 }}>
          <TextField label="From" type="date" value={from} onChange={(e) => setFrom(e.target.value)} slotProps={{ inputLabel: { shrink: true } }} />
          <TextField label="To" type="date" value={to} onChange={(e) => setTo(e.target.value)} slotProps={{ inputLabel: { shrink: true } }} />
          <TextField label="Reason" value={reason} onChange={(e) => setReason(e.target.value)} />
          <Button variant="outlined" disabled={!from || !to || add.isPending} onClick={() => add.mutate()}>Add</Button>
        </Stack>
      )}
    </Section>
  );
}

import { useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import {
  Button, IconButton, MenuItem, Paper, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField,
  Tooltip, Typography,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import PrevIcon from '@mui/icons-material/ChevronLeft';
import NextIcon from '@mui/icons-material/ChevronRight';
import { appointmentsApi, doctorsApi } from '../../api/endpoints';
import { keys } from '../../api/queryKeys';
import { useAuth } from '../../auth/AuthProvider';
import { EmptyState, ErrorAlert, Loading, PageHeader, StatusChip } from '../../components/common';
import { addDaysIso, formatDate, formatTimeOfDay, todayIso } from '../../utils/format';
import type { AppointmentStatus } from '../../types/api';
import { AppointmentActions, TypeChip } from './components';

const STATUSES: AppointmentStatus[] = ['Booked', 'Confirmed', 'CheckedIn', 'InConsultation', 'Completed', 'Cancelled', 'NoShow'];

export default function AppointmentsPage() {
  const { user, hasRole } = useAuth();
  const [date, setDate] = useState(todayIso());
  const [doctorId, setDoctorId] = useState<number | ''>(user?.role === 'Doctor' && user.doctorId ? user.doctorId : '');
  const [status, setStatus] = useState<AppointmentStatus | ''>('');

  const doctors = useQuery({ queryKey: keys.doctors.list({}), queryFn: () => doctorsApi.list() });
  const filters = { date, doctorId: doctorId || undefined, status: status || undefined };
  const { data, error, isLoading } = useQuery({ queryKey: keys.appointments.list(filters), queryFn: () => appointmentsApi.list(filters) });

  return (
    <>
      <PageHeader
        title="Appointments"
        subtitle={formatDate(date)}
        actions={hasRole('Admin', 'Receptionist') && (
          <Button variant="contained" startIcon={<AddIcon />} component={RouterLink} to="/appointments/book">Book appointment</Button>
        )}
      />
      <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.5} sx={{ mb: 2 }} alignItems={{ md: 'center' }}>
        <Stack direction="row" alignItems="center">
          <Tooltip title="Previous day"><IconButton aria-label="Previous day" onClick={() => setDate(addDaysIso(date, -1))}><PrevIcon /></IconButton></Tooltip>
          <TextField type="date" value={date} onChange={(e) => e.target.value && setDate(e.target.value)} sx={{ width: 170 }} slotProps={{ htmlInput: { 'aria-label': 'Date' } }} />
          <Tooltip title="Next day"><IconButton aria-label="Next day" onClick={() => setDate(addDaysIso(date, 1))}><NextIcon /></IconButton></Tooltip>
          <Button size="small" onClick={() => setDate(todayIso())} disabled={date === todayIso()}>Today</Button>
        </Stack>
        <TextField select label="Doctor" value={doctorId} onChange={(e) => setDoctorId(e.target.value === '' ? '' : Number(e.target.value))} sx={{ maxWidth: { md: 260 } }}>
          <MenuItem value="">All doctors</MenuItem>
          {(doctors.data ?? []).map((d) => <MenuItem key={d.id} value={d.id}>{d.fullName}</MenuItem>)}
        </TextField>
        <TextField select label="Status" value={status} onChange={(e) => setStatus(e.target.value as AppointmentStatus | '')} sx={{ maxWidth: { md: 200 } }}>
          <MenuItem value="">All statuses</MenuItem>
          {STATUSES.map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
        </TextField>
      </Stack>

      {error && <ErrorAlert error={error} />}
      {isLoading ? <Loading /> : data && (
        <Paper>
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Time</TableCell><TableCell>Patient</TableCell>
                  <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>Doctor</TableCell>
                  <TableCell sx={{ display: { xs: 'none', lg: 'table-cell' } }}>Reason</TableCell>
                  <TableCell>Status</TableCell><TableCell align="right">Actions</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {data.items.map((a) => (
                  <TableRow key={a.id} hover>
                    <TableCell sx={{ whiteSpace: 'nowrap', fontVariantNumeric: 'tabular-nums' }}>{formatTimeOfDay(a.startTime)}</TableCell>
                    <TableCell>
                      <Typography variant="body2" fontWeight={600} component={RouterLink} to={`/patients/${a.patientId}`} sx={{ color: 'inherit', textDecoration: 'none' }}>
                        {a.patientName}
                      </Typography>
                      <TypeChip type={a.appointmentType} />
                      <Typography variant="caption" display="block" color="text.secondary">{a.patientMrn} · {a.patientPhone}</Typography>
                    </TableCell>
                    <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>{a.doctorName}</TableCell>
                    <TableCell sx={{ display: { xs: 'none', lg: 'table-cell' } }}>{a.reasonForVisit ?? '—'}</TableCell>
                    <TableCell><StatusChip status={a.status} /></TableCell>
                    <TableCell align="right"><AppointmentActions appointment={a} /></TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
          {data.items.length === 0 && <EmptyState message="No appointments for this day." />}
        </Paper>
      )}
    </>
  );
}

import { useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import {
  Box, Chip, MenuItem, Paper, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, Typography,
} from '@mui/material';
import { appointmentsApi, doctorsApi } from '../../api/endpoints';
import { keys } from '../../api/queryKeys';
import { useAuth } from '../../auth/AuthProvider';
import { EmptyState, ErrorAlert, Loading, PageHeader, StatusChip } from '../../components/common';
import { formatDate, formatTimeOfDay, formatTimestampTime, todayIso } from '../../utils/format';
import { AppointmentActions, TypeChip } from './components';

export default function QueuePage() {
  const { user } = useAuth();
  const [doctorId, setDoctorId] = useState<number | ''>(user?.role === 'Doctor' && user.doctorId ? user.doctorId : '');
  const date = todayIso();

  const doctors = useQuery({ queryKey: [...keys.doctors.all, 'available-today'], queryFn: doctorsApi.availableToday });
  const { data, error, isLoading, dataUpdatedAt } = useQuery({
    queryKey: keys.appointments.queue(date, doctorId || undefined),
    queryFn: () => appointmentsApi.queue(date, doctorId || undefined),
    refetchInterval: 30_000,
  });

  const waiting = data?.filter((a) => a.status === 'CheckedIn').length ?? 0;
  const inRoom = data?.filter((a) => a.status === 'InConsultation').length ?? 0;
  const done = data?.filter((a) => a.status === 'Completed').length ?? 0;

  return (
    <>
      <PageHeader
        title="Today's queue"
        subtitle={`${formatDate(date)} · refreshes every 30 seconds${dataUpdatedAt ? ` · updated ${formatTimestampTime(new Date(dataUpdatedAt).toISOString())}` : ''}`}
      />
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} sx={{ mb: 2 }} alignItems={{ sm: 'center' }}>
        <TextField select label="Doctor" value={doctorId} onChange={(e) => setDoctorId(e.target.value === '' ? '' : Number(e.target.value))} sx={{ maxWidth: { sm: 300 } }}>
          <MenuItem value="">All doctors</MenuItem>
          {(doctors.data ?? []).map((d) => <MenuItem key={d.id} value={d.id}>{d.fullName}</MenuItem>)}
        </TextField>
        <Stack direction="row" spacing={1}>
          <Chip label={`${waiting} waiting`} color="warning" />
          <Chip label={`${inRoom} in consultation`} color="secondary" />
          <Chip label={`${done} completed`} color="success" variant="outlined" />
        </Stack>
      </Stack>
      {error && <ErrorAlert error={error} />}
      {isLoading ? <Loading /> : data && (
        <Paper>
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Token</TableCell><TableCell>Slot</TableCell><TableCell>Patient</TableCell>
                  <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>Doctor</TableCell>
                  <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>Checked in</TableCell>
                  <TableCell>Status</TableCell><TableCell align="right">Actions</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {data.map((a) => (
                  <TableRow key={a.id} hover sx={a.status === 'CheckedIn' ? { bgcolor: '#fffbeb' } : undefined}>
                    <TableCell>
                      {a.queueToken ? (
                        <Box sx={{ width: 36, height: 36, borderRadius: '50%', display: 'grid', placeItems: 'center', bgcolor: 'primary.main', color: '#fff', fontWeight: 700 }}>
                          {a.queueToken}
                        </Box>
                      ) : '—'}
                    </TableCell>
                    <TableCell sx={{ whiteSpace: 'nowrap' }}>{formatTimeOfDay(a.startTime)}</TableCell>
                    <TableCell>
                      <Typography variant="body2" fontWeight={600} component={RouterLink} to={`/patients/${a.patientId}`} sx={{ color: 'inherit', textDecoration: 'none' }}>
                        {a.patientName}
                      </Typography>
                      <TypeChip type={a.appointmentType} />
                      <Typography variant="caption" display="block" color="text.secondary">{a.patientMrn}{a.reasonForVisit ? ` · ${a.reasonForVisit}` : ''}</Typography>
                    </TableCell>
                    <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>{a.doctorName}</TableCell>
                    <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>{formatTimestampTime(a.checkedInAt)}</TableCell>
                    <TableCell><StatusChip status={a.status} /></TableCell>
                    <TableCell align="right"><AppointmentActions appointment={a} /></TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
          {data.length === 0 && <EmptyState message="No appointments today." />}
        </Paper>
      )}
    </>
  );
}

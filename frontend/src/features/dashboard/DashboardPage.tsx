import { useQuery } from '@tanstack/react-query';
import { Link as RouterLink, useNavigate } from 'react-router';
import {
  Box, Button, Chip, List, ListItem, ListItemButton, ListItemText, Paper, Stack, Typography,
} from '@mui/material';
import PersonAddIcon from '@mui/icons-material/PersonAddAlt1';
import EventAvailableIcon from '@mui/icons-material/EventAvailable';
import { dashboardApi } from '../../api/endpoints';
import { keys } from '../../api/queryKeys';
import { useAuth } from '../../auth/AuthProvider';
import { EmptyState, ErrorAlert, Loading, PageHeader, Section } from '../../components/common';
import { formatMoney, formatTimestampTime } from '../../utils/format';
import dayjs from 'dayjs';

const STATUS_TEXT: Record<string, string> = { CheckedIn: 'checked in', InConsultation: 'in consultation', NoShow: 'no-show' };

function StatCard({ label, value, hint, accent }: { label: string; value: string | number; hint?: string; accent: string }) {
  return (
    <Paper sx={{ p: 2.5, position: 'relative', overflow: 'hidden' }}>
      <Box sx={{ position: 'absolute', inset: '0 auto 0 0', width: 4, bgcolor: accent }} />
      <Typography variant="body2" color="text.secondary">{label}</Typography>
      <Typography variant="h4" sx={{ mt: 0.5, fontVariantNumeric: 'tabular-nums' }}>{value}</Typography>
      {hint && <Typography variant="caption" color="text.secondary">{hint}</Typography>}
    </Paper>
  );
}

export default function DashboardPage() {
  const { user, hasRole } = useAuth();
  const navigate = useNavigate();
  const { data, error, isLoading, refetch } = useQuery({
    queryKey: keys.dashboard,
    queryFn: dashboardApi.summary,
    refetchInterval: 60_000,
  });

  const canFrontDesk = hasRole('Admin', 'Receptionist');
  const canBill = canFrontDesk;

  return (
    <>
      <PageHeader
        title={`Good ${dayjs().hour() < 12 ? 'morning' : dayjs().hour() < 17 ? 'afternoon' : 'evening'}, ${user?.fullName.split(' ').slice(0, 2).join(' ')}`}
        subtitle={`${dayjs().format('dddd, D MMMM YYYY')}${data?.scopedToDoctor ? ' · showing your patients only' : ''}`}
        actions={canFrontDesk && (
          <>
            <Button variant="outlined" startIcon={<PersonAddIcon />} component={RouterLink} to="/patients/new">Register patient</Button>
            <Button variant="contained" startIcon={<EventAvailableIcon />} component={RouterLink} to="/appointments/book">Book appointment</Button>
          </>
        )}
      />

      {isLoading && <Loading />}
      {error && <ErrorAlert error={error} onRetry={() => refetch()} />}
      {data && (
        <Stack spacing={3}>
          <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)', lg: `repeat(${canBill ? 5 : 3}, 1fr)` } }}>
            <StatCard label="Total patients" value={data.totalPatients.toLocaleString('en-IN')} accent="#0f766e" />
            <StatCard
              label="Today's appointments"
              value={data.todayAppointments.total}
              hint={Object.entries(data.todayAppointments.byStatus).map(([s, n]) => `${n} ${STATUS_TEXT[s] ?? s.toLowerCase()}`).join(' · ') || 'None yet'}
              accent="#4f46e5"
            />
            <StatCard label="Waiting for consultation" value={data.waitingQueue.filter((w) => !w.consultationId).length} accent="#d97706" />
            {canBill && <StatCard label="Today's revenue" value={formatMoney(data.todayRevenue)} accent="#16a34a" />}
            {canBill && (
              <StatCard label="Pending payments" value={formatMoney(data.pendingPayments)} hint={`${data.pendingInvoiceCount} unpaid invoice(s)`} accent="#dc2626" />
            )}
          </Box>

          <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', lg: '3fr 2fr' }, alignItems: 'start' }}>
            <Section title="Waiting queue" action={<Button size="small" component={RouterLink} to="/queue">Open queue</Button>}>
              {data.waitingQueue.length === 0 ? (
                <EmptyState message="No patients are waiting right now." />
              ) : (
                <List disablePadding>
                  {data.waitingQueue.map((w) => (
                    <ListItem key={w.appointmentId} disablePadding divider>
                      <ListItemButton
                        onClick={() => navigate(w.consultationId ? `/consultations/${w.consultationId}` : `/patients/${w.patientId}`)}
                      >
                        <Chip label={`#${w.queueToken ?? '-'}`} size="small" color="primary" variant="outlined" sx={{ mr: 2, minWidth: 48 }} />
                        <ListItemText
                          primary={`${w.patientName} · ${w.patientMrn}`}
                          secondary={`${w.doctorName} · checked in ${formatTimestampTime(w.checkedInAt)}`}
                        />
                        {w.consultationId && <Chip label="In consultation" size="small" color="secondary" />}
                      </ListItemButton>
                    </ListItem>
                  ))}
                </List>
              )}
            </Section>

            <Section title="Doctors available today">
              {data.availableDoctors.length === 0 ? (
                <EmptyState message="No doctors are scheduled today." />
              ) : (
                <Stack spacing={1.5}>
                  {data.availableDoctors.map((d) => (
                    <Box key={d.id} sx={{ p: 1.5, borderRadius: 2, bgcolor: '#f8fafb', border: 1, borderColor: 'divider' }}>
                      <Stack direction="row" justifyContent="space-between" alignItems="flex-start" spacing={1}>
                        <Box>
                          <Typography variant="body2" fontWeight={600}>{d.fullName}</Typography>
                          <Typography variant="caption" color="text.secondary">
                            {d.primarySpecialization ?? d.departmentName} · {d.workingHours}
                          </Typography>
                        </Box>
                        <Stack direction="row" spacing={0.5}>
                          <Chip size="small" label={`${d.freeSlotsRemaining} free`} color={d.freeSlotsRemaining ? 'success' : 'default'} variant="outlined" />
                          {d.waitingCount > 0 && <Chip size="small" label={`${d.waitingCount} waiting`} color="warning" />}
                        </Stack>
                      </Stack>
                    </Box>
                  ))}
                </Stack>
              )}
            </Section>
          </Box>
        </Stack>
      )}
    </>
  );
}

import { useState } from 'react';
import { Link as RouterLink, useNavigate, useParams } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Box, Button, Chip, IconButton, MenuItem, Paper, Stack, Tab, Table, TableBody, TableCell, TableContainer, TableHead,
  TableRow, Tabs, TextField, Tooltip, Typography,
} from '@mui/material';
import EditIcon from '@mui/icons-material/EditOutlined';
import EventIcon from '@mui/icons-material/EventAvailable';
import DeleteIcon from '@mui/icons-material/DeleteOutline';
import PrintIcon from '@mui/icons-material/PrintOutlined';
import { patientsApi } from '../../api/endpoints';
import { keys } from '../../api/queryKeys';
import { errorMessage } from '../../api/errors';
import { useAuth } from '../../auth/AuthProvider';
import { EmptyState, ErrorAlert, LabelValue, Loading, PageHeader, Section, StatusChip } from '../../components/common';
import { useNotify } from '../../components/Notifications';
import { formatDate, formatDateTime, formatTimeOfDay } from '../../utils/format';
import type { PatientDto } from '../../types/api';

export default function PatientDetailPage() {
  const patientId = Number(useParams().id);
  const { hasRole } = useAuth();
  const [tab, setTab] = useState(0);
  const { data: patient, error, isLoading } = useQuery({
    queryKey: keys.patients.detail(patientId),
    queryFn: () => patientsApi.get(patientId),
  });

  if (isLoading) return <Loading />;
  if (error || !patient) return <ErrorAlert error={error} />;

  const address = [patient.addressLine1, patient.addressLine2, patient.city, patient.state, patient.postalCode].filter(Boolean).join(', ');

  return (
    <>
      <PageHeader
        title={patient.fullName}
        subtitle={
          <Stack direction="row" spacing={1} alignItems="center" component="span" flexWrap="wrap" useFlexGap>
            <Chip size="small" label={patient.mrn} sx={{ fontFamily: 'monospace' }} />
            <span>{patient.age ?? '?'} years · {patient.gender}{patient.bloodGroup ? ` · ${patient.bloodGroup}` : ''} · {patient.phone}</span>
          </Stack>
        }
        actions={hasRole('Admin', 'Receptionist') && (
          <>
            <Button startIcon={<EditIcon />} variant="outlined" component={RouterLink} to={`/patients/${patient.id}/edit`}>Edit</Button>
            <Button startIcon={<EventIcon />} variant="contained" component={RouterLink} to={`/appointments/book?patientId=${patient.id}`}>
              Book appointment
            </Button>
          </>
        )}
      />

      {patient.allergies.length > 0 && (
        <Paper sx={{ p: 1.5, mb: 2, bgcolor: '#fef2f2', borderColor: '#fecaca' }}>
          <Typography variant="body2" color="error.dark" fontWeight={600}>
            Allergies: {patient.allergies.map((a) => a.allergen).join(', ')}
          </Typography>
        </Paper>
      )}

      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2, borderBottom: 1, borderColor: 'divider' }} variant="scrollable">
        <Tab label="Overview" />
        <Tab label="Visit history" />
        <Tab label="Appointments" />
        <Tab label="Prescriptions" />
      </Tabs>

      {tab === 0 && (
        <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', lg: '2fr 1fr' }, alignItems: 'start' }}>
          <Section title="Details">
            <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: 'repeat(3, 1fr)' } }}>
              <LabelValue label="Date of birth" value={patient.dateOfBirth ? formatDate(patient.dateOfBirth) : `Age recorded: ${patient.age}`} />
              <LabelValue label="Phone" value={patient.phone} />
              <LabelValue label="Alternate phone" value={patient.alternatePhone} />
              <LabelValue label="Email" value={patient.email} />
              <Box sx={{ gridColumn: { sm: 'span 2' } }}><LabelValue label="Address" value={address} /></Box>
              <LabelValue label="Emergency contact" value={patient.emergencyContactName && `${patient.emergencyContactName} (${patient.emergencyContactRelation ?? '—'})`} />
              <LabelValue label="Emergency phone" value={patient.emergencyContactPhone} />
              <LabelValue label="Registered" value={formatDate(patient.createdAt)} />
            </Box>
          </Section>
          <Stack spacing={2}>
            <AllergiesSection patient={patient} />
            <ConditionsSection patient={patient} canEdit={hasRole('Admin', 'Doctor')} />
          </Stack>
        </Box>
      )}
      {tab === 1 && <VisitsTab patientId={patient.id} />}
      {tab === 2 && <AppointmentsTab patientId={patient.id} />}
      {tab === 3 && <PrescriptionsTab patientId={patient.id} />}
    </>
  );
}

function AllergiesSection({ patient }: { patient: PatientDto }) {
  const queryClient = useQueryClient();
  const notify = useNotify();
  const [allergen, setAllergen] = useState('');
  const [reaction, setReaction] = useState('');
  const [severity, setSeverity] = useState('');
  const invalidate = () => queryClient.invalidateQueries({ queryKey: keys.patients.detail(patient.id) });

  const add = useMutation({
    mutationFn: () => patientsApi.addAllergy(patient.id, { allergen: allergen.trim(), reaction: reaction.trim() || null, severity: severity || null }),
    onSuccess: () => {
      setAllergen('');
      setReaction('');
      setSeverity('');
      invalidate();
    },
    onError: (e) => notify(errorMessage(e), 'error'),
  });
  const remove = useMutation({
    mutationFn: (allergyId: number) => patientsApi.deleteAllergy(patient.id, allergyId),
    onSuccess: invalidate,
    onError: (e) => notify(errorMessage(e), 'error'),
  });

  return (
    <Section title="Allergies">
      {patient.allergies.length === 0 && <Typography variant="body2" color="text.secondary">No known allergies.</Typography>}
      <Stack spacing={1}>
        {patient.allergies.map((a) => (
          <Stack key={a.id} direction="row" alignItems="center" justifyContent="space-between">
            <Box>
              <Typography variant="body2" fontWeight={600}>{a.allergen}</Typography>
              <Typography variant="caption" color="text.secondary">{[a.reaction, a.severity].filter(Boolean).join(' · ') || '—'}</Typography>
            </Box>
            <IconButton size="small" aria-label={`Remove allergy ${a.allergen}`} onClick={() => remove.mutate(a.id)}><DeleteIcon fontSize="small" /></IconButton>
          </Stack>
        ))}
      </Stack>
      <Stack spacing={1} sx={{ mt: 2 }}>
        <TextField label="Allergen" value={allergen} onChange={(e) => setAllergen(e.target.value)} />
        <Stack direction="row" spacing={1}>
          <TextField label="Reaction" value={reaction} onChange={(e) => setReaction(e.target.value)} />
          <TextField select label="Severity" value={severity} onChange={(e) => setSeverity(e.target.value)} sx={{ minWidth: 130 }}>
            <MenuItem value="">—</MenuItem>
            {['Mild', 'Moderate', 'Severe'].map((s) => <MenuItem key={s} value={s}>{s}</MenuItem>)}
          </TextField>
        </Stack>
        <Button variant="outlined" size="small" disabled={!allergen.trim() || add.isPending} onClick={() => add.mutate()}>Add allergy</Button>
      </Stack>
    </Section>
  );
}

function ConditionsSection({ patient, canEdit }: { patient: PatientDto; canEdit: boolean }) {
  const queryClient = useQueryClient();
  const notify = useNotify();
  const [name, setName] = useState('');
  const [since, setSince] = useState('');
  const invalidate = () => queryClient.invalidateQueries({ queryKey: keys.patients.detail(patient.id) });

  const add = useMutation({
    mutationFn: () => patientsApi.addCondition(patient.id, { conditionName: name.trim(), sinceYear: since ? Number(since) : null, notes: null }),
    onSuccess: () => {
      setName('');
      setSince('');
      invalidate();
    },
    onError: (e) => notify(errorMessage(e), 'error'),
  });
  const remove = useMutation({
    mutationFn: (id: number) => patientsApi.deleteCondition(patient.id, id),
    onSuccess: invalidate,
    onError: (e) => notify(errorMessage(e), 'error'),
  });

  return (
    <Section title="Medical history">
      {patient.conditions.length === 0 && <Typography variant="body2" color="text.secondary">No chronic conditions recorded.</Typography>}
      <Stack spacing={1}>
        {patient.conditions.map((c) => (
          <Stack key={c.id} direction="row" alignItems="center" justifyContent="space-between">
            <Typography variant="body2">
              <strong>{c.conditionName}</strong>{c.sinceYear ? ` · since ${c.sinceYear}` : ''}
            </Typography>
            {canEdit && (
              <IconButton size="small" aria-label={`Remove ${c.conditionName}`} onClick={() => remove.mutate(c.id)}><DeleteIcon fontSize="small" /></IconButton>
            )}
          </Stack>
        ))}
      </Stack>
      {canEdit && (
        <Stack direction="row" spacing={1} sx={{ mt: 2 }}>
          <TextField label="Condition" value={name} onChange={(e) => setName(e.target.value)} />
          <TextField label="Since (year)" value={since} onChange={(e) => setSince(e.target.value.replace(/\D/g, '').slice(0, 4))} sx={{ width: 130 }} />
          <Button variant="outlined" size="small" disabled={!name.trim() || add.isPending} onClick={() => add.mutate()}>Add</Button>
        </Stack>
      )}
    </Section>
  );
}

function VisitsTab({ patientId }: { patientId: number }) {
  const navigate = useNavigate();
  const { hasRole } = useAuth();
  const { data, error, isLoading } = useQuery({ queryKey: keys.patients.visits(patientId), queryFn: () => patientsApi.visits(patientId) });
  if (isLoading) return <Loading />;
  if (error) return <ErrorAlert error={error} />;
  const clinical = hasRole('Admin', 'Doctor', 'Nurse');
  return (
    <Paper>
      <TableContainer>
        <Table size="small">
          <TableHead>
            <TableRow>
              <TableCell>Date</TableCell><TableCell>Doctor</TableCell><TableCell>Complaint</TableCell>
              <TableCell>Diagnosis</TableCell><TableCell>Status</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {data!.items.map((v) => (
              <TableRow key={v.consultationId} hover={clinical} sx={{ cursor: clinical ? 'pointer' : 'default' }}
                onClick={() => clinical && navigate(`/consultations/${v.consultationId}`)}>
                <TableCell>{formatDateTime(v.startedAt)}</TableCell>
                <TableCell>{v.doctorName}</TableCell>
                <TableCell>{v.chiefComplaint ?? '—'}</TableCell>
                <TableCell>{v.diagnosis ?? '—'}</TableCell>
                <TableCell><StatusChip status={v.status} /></TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      {data!.items.length === 0 && <EmptyState message="No previous visits." />}
    </Paper>
  );
}

function AppointmentsTab({ patientId }: { patientId: number }) {
  const { data, error, isLoading } = useQuery({ queryKey: keys.patients.appointments(patientId), queryFn: () => patientsApi.appointments(patientId, false) });
  if (isLoading) return <Loading />;
  if (error) return <ErrorAlert error={error} />;
  const items = [...data!.items].reverse();
  return (
    <Paper>
      <TableContainer>
        <Table size="small">
          <TableHead>
            <TableRow><TableCell>Date</TableCell><TableCell>Time</TableCell><TableCell>Doctor</TableCell><TableCell>Reason</TableCell><TableCell>Status</TableCell></TableRow>
          </TableHead>
          <TableBody>
            {items.map((a) => (
              <TableRow key={a.id}>
                <TableCell>{formatDate(a.appointmentDate)}</TableCell>
                <TableCell>{formatTimeOfDay(a.startTime)}</TableCell>
                <TableCell>{a.doctorName}</TableCell>
                <TableCell>{a.reasonForVisit ?? '—'}</TableCell>
                <TableCell><StatusChip status={a.status} /></TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      {items.length === 0 && <EmptyState message="No appointments." />}
    </Paper>
  );
}

function PrescriptionsTab({ patientId }: { patientId: number }) {
  const { data, error, isLoading } = useQuery({ queryKey: keys.patients.prescriptions(patientId), queryFn: () => patientsApi.prescriptions(patientId) });
  if (isLoading) return <Loading />;
  if (error) return <ErrorAlert error={error} />;
  return (
    <Paper>
      <TableContainer>
        <Table size="small">
          <TableHead>
            <TableRow><TableCell>Rx number</TableCell><TableCell>Date</TableCell><TableCell>Doctor</TableCell><TableCell>Items</TableCell><TableCell>Follow-up</TableCell><TableCell>Status</TableCell><TableCell /></TableRow>
          </TableHead>
          <TableBody>
            {data!.map((p) => (
              <TableRow key={p.id}>
                <TableCell sx={{ fontFamily: 'monospace' }}>{p.prescriptionNumber}</TableCell>
                <TableCell>{formatDate(p.createdAt)}</TableCell>
                <TableCell>{p.doctorName}</TableCell>
                <TableCell>{p.itemCount}</TableCell>
                <TableCell>{formatDate(p.followUpDate)}</TableCell>
                <TableCell><StatusChip status={p.status} /></TableCell>
                <TableCell align="right">
                  <Tooltip title="Print">
                    <IconButton size="small" component={RouterLink} to={`/print/prescriptions/${p.id}`} target="_blank" aria-label="Print prescription">
                      <PrintIcon fontSize="small" />
                    </IconButton>
                  </Tooltip>
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </TableContainer>
      {data!.length === 0 && <EmptyState message="No prescriptions." />}
    </Paper>
  );
}

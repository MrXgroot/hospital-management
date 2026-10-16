import { useEffect, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert, Autocomplete, Box, Button, MenuItem, Stack, TextField, ToggleButton, ToggleButtonGroup, Typography,
} from '@mui/material';
import { appointmentsApi, doctorsApi, patientsApi } from '../../api/endpoints';
import { keys } from '../../api/queryKeys';
import { errorMessage, getErrorCode } from '../../api/errors';
import { PageHeader, Section } from '../../components/common';
import { useNotify } from '../../components/Notifications';
import { addDaysIso, formatDate, formatMoney, formatTimeOfDay, todayIso } from '../../utils/format';
import type { AppointmentType, PatientListItemDto, TimeOfDay } from '../../types/api';
import { SlotPicker } from './components';

export default function BookAppointmentPage() {
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const notify = useNotify();
  const queryClient = useQueryClient();

  const [patient, setPatient] = useState<PatientListItemDto | null>(null);
  const [patientQuery, setPatientQuery] = useState('');
  const [debouncedQuery, setDebouncedQuery] = useState('');
  const [doctorId, setDoctorId] = useState<number | null>(params.get('doctorId') ? Number(params.get('doctorId')) : null);
  const [type, setType] = useState<AppointmentType>(params.get('followUpOf') ? 'FollowUp' : 'Scheduled');
  const [date, setDate] = useState(params.get('date') ?? todayIso());
  const [slot, setSlot] = useState<TimeOfDay | null>(null);
  const [reason, setReason] = useState(params.get('followUpOf') ? 'Follow-up visit' : '');

  // Pre-select a patient passed in the URL (from the patient page or a prescription follow-up).
  const preselectId = params.get('patientId');
  const preselect = useQuery({
    queryKey: keys.patients.detail(Number(preselectId)),
    queryFn: () => patientsApi.get(Number(preselectId)),
    enabled: !!preselectId,
  });
  useEffect(() => {
    const p = preselect.data;
    if (p) setPatient({ id: p.id, mrn: p.mrn, fullName: p.fullName, gender: p.gender, age: p.age, phone: p.phone, city: p.city, createdAt: p.createdAt });
  }, [preselect.data]);

  useEffect(() => {
    const t = setTimeout(() => setDebouncedQuery(patientQuery.trim()), 300);
    return () => clearTimeout(t);
  }, [patientQuery]);

  const patientOptions = useQuery({
    queryKey: keys.patients.search(debouncedQuery, 0, 10),
    queryFn: () => patientsApi.search(debouncedQuery, 1, 10),
    enabled: debouncedQuery.length >= 2,
  });
  const doctors = useQuery({ queryKey: keys.doctors.list({ isActive: true }), queryFn: () => doctorsApi.list({ isActive: true }) });
  const doctor = doctors.data?.find((d) => d.id === doctorId);

  const isWalkIn = type === 'WalkIn';
  useEffect(() => {
    if (isWalkIn) setDate(todayIso());
  }, [isWalkIn]);

  const book = useMutation({
    mutationFn: () =>
      appointmentsApi.create({
        patientId: patient!.id,
        doctorId: doctorId!,
        appointmentDate: date,
        startTime: slot,
        appointmentType: type,
        reasonForVisit: reason.trim() || null,
        followUpOfConsultationId: params.get('followUpOf') ? Number(params.get('followUpOf')) : null,
      }),
    onSuccess: (a) => {
      queryClient.invalidateQueries({ queryKey: keys.appointments.all });
      queryClient.invalidateQueries({ queryKey: keys.doctors.all });
      queryClient.invalidateQueries({ queryKey: keys.dashboard });
      notify(isWalkIn
        ? `Walk-in booked at ${formatTimeOfDay(a.startTime)} and checked in · token #${a.queueToken}`
        : `Booked ${formatDate(a.appointmentDate)} at ${formatTimeOfDay(a.startTime)}`);
      navigate(isWalkIn ? '/queue' : '/appointments');
    },
    onError: (e) => {
      if (getErrorCode(e) === 'SLOT_UNAVAILABLE') {
        // Keep the rest of the form; just refresh slots and clear the taken one.
        setSlot(null);
        queryClient.invalidateQueries({ queryKey: keys.doctors.slots(doctorId!, date) });
      }
      notify(errorMessage(e), 'error');
    },
  });

  const canSubmit = !!patient && !!doctorId && !!date && (isWalkIn || !!slot) && !book.isPending;

  return (
    <>
      <PageHeader title="Book appointment" />
      <Stack spacing={2.5} sx={{ maxWidth: 960 }}>
        <Section title="1. Patient">
          <Autocomplete
            value={patient}
            onChange={(_, v) => setPatient(v)}
            inputValue={patientQuery}
            onInputChange={(_, v) => setPatientQuery(v)}
            options={patientOptions.data?.items ?? []}
            filterOptions={(x) => x}
            getOptionLabel={(p) => `${p.fullName} · ${p.mrn}`}
            isOptionEqualToValue={(a, b) => a.id === b.id}
            loading={patientOptions.isFetching}
            noOptionsText={debouncedQuery.length < 2 ? 'Type at least 2 characters' : 'No patients found'}
            renderOption={(props, p) => (
              <li {...props} key={p.id}>
                <Box>
                  <Typography variant="body2" fontWeight={600}>{p.fullName} · {p.mrn}</Typography>
                  <Typography variant="caption" color="text.secondary">{p.age ?? '?'} y / {p.gender} · {p.phone}</Typography>
                </Box>
              </li>
            )}
            renderInput={(p) => <TextField {...p} label="Search patient by ID, phone or name" />}
          />
          <Button size="small" sx={{ mt: 1 }} onClick={() => navigate('/patients/new')}>New patient? Register first</Button>
        </Section>

        <Section title="2. Doctor and visit type">
          <Stack direction={{ xs: 'column', md: 'row' }} spacing={2} alignItems={{ md: 'center' }}>
            <TextField select label="Doctor" value={doctorId ?? ''} onChange={(e) => { setDoctorId(Number(e.target.value)); setSlot(null); }} sx={{ maxWidth: { md: 380 } }}>
              {(doctors.data ?? []).map((d) => (
                <MenuItem key={d.id} value={d.id}>
                  {d.fullName} · {d.primarySpecialization ?? d.departmentName} · {formatMoney(d.consultationFee)}
                </MenuItem>
              ))}
            </TextField>
            <ToggleButtonGroup exclusive size="small" value={type} onChange={(_, v: AppointmentType | null) => v && setType(v)} aria-label="Visit type">
              <ToggleButton value="Scheduled">Scheduled</ToggleButton>
              <ToggleButton value="WalkIn">Walk-in (today)</ToggleButton>
              <ToggleButton value="FollowUp">Follow-up</ToggleButton>
            </ToggleButtonGroup>
          </Stack>
        </Section>

        <Section title="3. Date and time">
          <TextField
            label="Date" type="date" value={date} disabled={isWalkIn}
            onChange={(e) => { setDate(e.target.value); setSlot(null); }}
            sx={{ mb: 2, maxWidth: 220 }}
            slotProps={{ inputLabel: { shrink: true }, htmlInput: { min: todayIso(), max: addDaysIso(todayIso(), 90) } }}
          />
          {isWalkIn && (
            <Alert severity="info" sx={{ mb: 2 }}>
              Leave the time empty to take the next free slot. The patient is checked in straight away.
            </Alert>
          )}
          <SlotPicker doctorId={doctorId} date={date} value={slot} onChange={setSlot} />
        </Section>

        <Section title="4. Reason for visit">
          <TextField value={reason} onChange={(e) => setReason(e.target.value)} placeholder="e.g. Fever since 3 days" multiline minRows={2} slotProps={{ htmlInput: { maxLength: 300 } }} />
        </Section>

        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} justifyContent="space-between" alignItems={{ sm: 'center' }}>
          <Typography variant="body2" color="text.secondary">
            {patient && doctor
              ? `${patient.fullName} with ${doctor.fullName} on ${formatDate(date)}${slot ? ` at ${formatTimeOfDay(slot)}` : isWalkIn ? ' (next free slot)' : ''}`
              : 'Select a patient, doctor and slot.'}
          </Typography>
          <Stack direction="row" spacing={1}>
            <Button onClick={() => navigate(-1)}>Cancel</Button>
            <Button variant="contained" disabled={!canSubmit} onClick={() => book.mutate()}>
              {book.isPending ? 'Booking…' : isWalkIn ? 'Book & check in' : 'Book appointment'}
            </Button>
          </Stack>
        </Stack>
      </Stack>
    </>
  );
}

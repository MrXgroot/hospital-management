import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import {
  Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, List, ListItemButton,
  ListItemText, Radio, RadioGroup, Stack, Typography,
} from '@mui/material';
import { patientsApi } from '../../api/endpoints';
import { keys } from '../../api/queryKeys';
import { applyFieldErrors, errorMessage, getProblem } from '../../api/errors';
import { ErrorAlert, Loading, PageHeader, Section } from '../../components/common';
import { FormSelect, FormText, GENDER_OPTIONS } from '../../components/form';
import { useNotify } from '../../components/Notifications';
import type { DuplicatePatientMatch } from '../../types/api';
import { emptyPatientForm, patientSchema, toPatientForm, toPatientRequest, type PatientFormValues } from './patientSchema';

const BLOOD_GROUPS = ['', 'A+', 'A-', 'B+', 'B-', 'AB+', 'AB-', 'O+', 'O-'].map((b) => ({ value: b, label: b || 'Unknown' }));

const grid = { display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)', md: 'repeat(3, 1fr)' } };

export default function PatientFormPage() {
  const { id } = useParams();
  const patientId = id ? Number(id) : null;
  const navigate = useNavigate();
  const notify = useNotify();
  const queryClient = useQueryClient();
  const [duplicates, setDuplicates] = useState<DuplicatePatientMatch[] | null>(null);

  const existing = useQuery({
    queryKey: keys.patients.detail(patientId ?? 0),
    queryFn: () => patientsApi.get(patientId!),
    enabled: !!patientId,
  });

  const { control, handleSubmit, reset, setError, setValue, getValues } = useForm<PatientFormValues>({
    resolver: zodResolver(patientSchema),
    defaultValues: emptyPatientForm,
  });
  const ageMode = useWatch({ control, name: 'ageMode' });

  useEffect(() => {
    if (existing.data) reset(toPatientForm(existing.data));
  }, [existing.data, reset]);

  const save = useMutation({
    mutationFn: ({ values, confirmDuplicate }: { values: PatientFormValues; confirmDuplicate: boolean }) =>
      patientId
        ? patientsApi.update(patientId, toPatientRequest(values))
        : patientsApi.create(toPatientRequest(values), confirmDuplicate),
    onSuccess: (patient) => {
      queryClient.invalidateQueries({ queryKey: keys.patients.all });
      notify(patientId ? 'Patient details updated' : `Patient registered · ${patient.mrn}`);
      navigate(`/patients/${patient.id}`);
    },
    onError: (error) => {
      const problem = getProblem(error);
      if (problem?.code === 'DUPLICATE_PATIENT') {
        setDuplicates((problem.matches as DuplicatePatientMatch[]) ?? []);
        return;
      }
      if (!applyFieldErrors(error, setError)) notify(errorMessage(error), 'error');
    },
  });

  const onSubmit = (values: PatientFormValues) => save.mutate({ values, confirmDuplicate: false });

  if (patientId && existing.isLoading) return <Loading />;
  if (existing.error) return <ErrorAlert error={existing.error} />;

  return (
    <>
      <PageHeader
        title={patientId ? `Edit patient` : 'Register new patient'}
        subtitle={patientId ? `${existing.data?.fullName} · ${existing.data?.mrn}` : 'A unique patient ID is generated when you save.'}
      />
      <Box component="form" onSubmit={handleSubmit(onSubmit)} noValidate>
        <Stack spacing={2.5}>
          <Section title="Personal details">
            <Box sx={grid}>
              <FormText control={control} name="firstName" label="First name" required />
              <FormText control={control} name="lastName" label="Last name" required />
              <FormSelect control={control} name="gender" label="Gender" options={GENDER_OPTIONS} required />
            </Box>
            <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2} alignItems={{ sm: 'center' }} sx={{ mt: 2 }}>
              <RadioGroup
                row
                value={ageMode}
                onChange={(e) => setValue('ageMode', e.target.value as 'dob' | 'age')}
                aria-label="Age entry mode"
              >
                <FormControlLabel value="dob" control={<Radio size="small" />} label="Date of birth" />
                <FormControlLabel value="age" control={<Radio size="small" />} label="Age only" />
              </RadioGroup>
              <Box sx={{ width: { xs: '100%', sm: 220 } }}>
                {ageMode === 'dob' ? (
                  <FormText control={control} name="dateOfBirth" label="Date of birth" type="date" slotProps={{ inputLabel: { shrink: true } }} />
                ) : (
                  <FormText control={control} name="ageYears" label="Age (years)" type="number" />
                )}
              </Box>
              <Box sx={{ width: { xs: '100%', sm: 160 } }}>
                <FormSelect control={control} name="bloodGroup" label="Blood group" options={BLOOD_GROUPS} />
              </Box>
            </Stack>
          </Section>

          <Section title="Contact">
            <Box sx={grid}>
              <FormText control={control} name="phone" label="Phone" required placeholder="10-digit mobile" />
              <FormText control={control} name="alternatePhone" label="Alternate phone" />
              <FormText control={control} name="email" label="Email" type="email" />
              <Box sx={{ gridColumn: { md: 'span 2' } }}>
                <FormText control={control} name="addressLine1" label="Address line 1" />
              </Box>
              <FormText control={control} name="addressLine2" label="Address line 2" />
              <FormText control={control} name="city" label="City" />
              <FormText control={control} name="state" label="State" />
              <FormText control={control} name="postalCode" label="PIN code" />
            </Box>
          </Section>

          <Section title="Emergency contact">
            <Box sx={grid}>
              <FormText control={control} name="emergencyContactName" label="Name" />
              <FormText control={control} name="emergencyContactPhone" label="Phone" />
              <FormText control={control} name="emergencyContactRelation" label="Relation" />
            </Box>
          </Section>

          <Stack direction="row" spacing={1.5} justifyContent="flex-end">
            <Button onClick={() => navigate(-1)}>Cancel</Button>
            <Button type="submit" variant="contained" disabled={save.isPending}>
              {save.isPending ? 'Saving…' : patientId ? 'Save changes' : 'Register patient'}
            </Button>
          </Stack>
        </Stack>
      </Box>

      <Dialog open={!!duplicates} onClose={() => setDuplicates(null)} maxWidth="sm" fullWidth>
        <DialogTitle>Possible duplicate patient</DialogTitle>
        <DialogContent>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 1 }}>
            A patient with the same first name and phone number already exists. Open the existing record, or create a new one anyway.
          </Typography>
          <List>
            {duplicates?.map((m) => (
              <ListItemButton key={m.id} onClick={() => navigate(`/patients/${m.id}`)} sx={{ borderRadius: 2 }}>
                <ListItemText primary={`${m.fullName} · ${m.mrn}`} secondary={`${m.phone} · age ${m.age ?? '?'}`} />
              </ListItemButton>
            ))}
          </List>
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setDuplicates(null)}>Back</Button>
          <Button
            variant="contained"
            onClick={() => {
              setDuplicates(null);
              save.mutate({ values: getValues(), confirmDuplicate: true });
            }}
          >
            Create anyway
          </Button>
        </DialogActions>
      </Dialog>
    </>
  );
}

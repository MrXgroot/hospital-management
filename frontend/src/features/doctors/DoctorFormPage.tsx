import { useEffect } from 'react';
import { useNavigate, useParams } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Controller, useForm, useWatch } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Box, Button, Checkbox, ListItemText, MenuItem, Stack, TextField } from '@mui/material';
import { doctorsApi, masterApi } from '../../api/endpoints';
import { keys } from '../../api/queryKeys';
import { applyFieldErrors, errorMessage } from '../../api/errors';
import { ErrorAlert, Loading, PageHeader, Section } from '../../components/common';
import { FormSelect, FormText, GENDER_OPTIONS } from '../../components/form';
import { useNotify } from '../../components/Notifications';
import type { DoctorDto, SaveDoctorRequest } from '../../types/api';
import { emptyToNull } from '../../utils/format';

const schema = z
  .object({
    firstName: z.string().trim().min(1, 'Required').max(80),
    lastName: z.string().trim().min(1, 'Required').max(80),
    gender: z.enum(['Male', 'Female', 'Other', 'Unknown']),
    phone: z.string().trim().regex(/^\+?[0-9 -]{10,20}$/, 'Enter a valid phone number'),
    email: z.string().refine((v) => !v.trim() || z.email().safeParse(v.trim()).success, 'Enter a valid email'),
    qualification: z.string().trim().min(1, 'Required').max(200),
    registrationNumber: z.string().trim().min(1, 'Required').max(50),
    departmentId: z.string().min(1, 'Select a department'),
    consultationFee: z.string().refine((v) => v.trim() !== '' && Number(v) >= 0, 'Enter a fee of 0 or more'),
    yearsOfExperience: z.string().refine((v) => !v.trim() || (Number.isInteger(Number(v)) && Number(v) >= 0 && Number(v) <= 70), '0 to 70'),
    specializationIds: z.array(z.number()).min(1, 'Select at least one specialization'),
    primarySpecializationId: z.string().min(1, 'Select the primary specialization'),
  })
  .refine((v) => v.specializationIds.includes(Number(v.primarySpecializationId)), {
    path: ['primarySpecializationId'],
    message: 'Must be one of the selected specializations',
  });

type FormValues = z.infer<typeof schema>;

const empty: FormValues = {
  firstName: '', lastName: '', gender: 'Male', phone: '', email: '', qualification: '', registrationNumber: '',
  departmentId: '', consultationFee: '', yearsOfExperience: '', specializationIds: [], primarySpecializationId: '',
};

const fromDoctor = (d: DoctorDto): FormValues => ({
  firstName: d.firstName, lastName: d.lastName, gender: d.gender, phone: d.phone, email: d.email ?? '',
  qualification: d.qualification, registrationNumber: d.registrationNumber, departmentId: String(d.departmentId),
  consultationFee: String(d.consultationFee), yearsOfExperience: d.yearsOfExperience?.toString() ?? '',
  specializationIds: d.specializations.map((s) => s.specializationId),
  primarySpecializationId: String(d.specializations.find((s) => s.isPrimary)?.specializationId ?? ''),
});

const toRequest = (v: FormValues): SaveDoctorRequest => ({
  firstName: v.firstName.trim(), lastName: v.lastName.trim(), gender: v.gender, phone: v.phone.trim(),
  email: emptyToNull(v.email), qualification: v.qualification.trim(), registrationNumber: v.registrationNumber.trim(),
  departmentId: Number(v.departmentId), consultationFee: Number(v.consultationFee),
  yearsOfExperience: v.yearsOfExperience.trim() ? Number(v.yearsOfExperience) : null,
  specializationIds: v.specializationIds, primarySpecializationId: Number(v.primarySpecializationId),
});

const grid = { display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: 'repeat(2, 1fr)', md: 'repeat(3, 1fr)' } };

export default function DoctorFormPage() {
  const { id } = useParams();
  const doctorId = id ? Number(id) : null;
  const navigate = useNavigate();
  const notify = useNotify();
  const queryClient = useQueryClient();

  const departments = useQuery({ queryKey: keys.departments, queryFn: () => masterApi.departments() });
  const specializations = useQuery({ queryKey: keys.specializations, queryFn: () => masterApi.specializations() });
  const existing = useQuery({ queryKey: keys.doctors.detail(doctorId ?? 0), queryFn: () => doctorsApi.get(doctorId!), enabled: !!doctorId });

  const { control, handleSubmit, reset, setError } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: empty });
  const selectedSpecs = useWatch({ control, name: 'specializationIds' });

  useEffect(() => {
    if (existing.data) reset(fromDoctor(existing.data));
  }, [existing.data, reset]);

  const save = useMutation({
    mutationFn: (v: FormValues) => (doctorId ? doctorsApi.update(doctorId, toRequest(v)) : doctorsApi.create(toRequest(v))),
    onSuccess: (doctor) => {
      queryClient.invalidateQueries({ queryKey: keys.doctors.all });
      notify(doctorId ? 'Doctor profile updated' : `Doctor added · ${doctor.doctorCode}`);
      navigate(`/doctors/${doctor.id}`);
    },
    onError: (e) => {
      if (!applyFieldErrors(e, setError)) notify(errorMessage(e), 'error');
    },
  });

  if (doctorId && existing.isLoading) return <Loading />;
  if (existing.error) return <ErrorAlert error={existing.error} />;

  const specName = (sid: number) => specializations.data?.find((s) => s.id === sid)?.name ?? sid;

  return (
    <>
      <PageHeader title={doctorId ? 'Edit doctor profile' : 'Add doctor'} subtitle={existing.data && `${existing.data.fullName} · ${existing.data.doctorCode}`} />
      <Box component="form" onSubmit={handleSubmit((v) => save.mutate(v))} noValidate>
        <Stack spacing={2.5}>
          <Section title="Profile">
            <Box sx={grid}>
              <FormText control={control} name="firstName" label="First name" required />
              <FormText control={control} name="lastName" label="Last name" required />
              <FormSelect control={control} name="gender" label="Gender" options={GENDER_OPTIONS} />
              <FormText control={control} name="phone" label="Phone" required />
              <FormText control={control} name="email" label="Email" />
              <FormText control={control} name="yearsOfExperience" label="Years of experience" type="number" />
              <FormText control={control} name="qualification" label="Qualification" placeholder="MBBS, MD (Medicine)" required />
              <FormText control={control} name="registrationNumber" label="Medical registration no." required />
            </Box>
          </Section>
          <Section title="Department, specialization and fee">
            <Box sx={grid}>
              <FormSelect
                control={control} name="departmentId" label="Department" required
                options={(departments.data ?? []).filter((d) => d.isActive || String(d.id) === existing.data?.departmentId.toString()).map((d) => ({ value: String(d.id), label: d.name }))}
              />
              <Controller
                control={control}
                name="specializationIds"
                render={({ field, fieldState }) => (
                  <TextField
                    select
                    label="Specializations"
                    value={field.value}
                    onChange={(e) => field.onChange(e.target.value as unknown as number[])}
                    error={!!fieldState.error}
                    helperText={fieldState.error?.message}
                    slotProps={{ select: { multiple: true, renderValue: (v) => (v as number[]).map(specName).join(', ') } }}
                  >
                    {(specializations.data ?? []).map((s) => (
                      <MenuItem key={s.id} value={s.id}>
                        <Checkbox size="small" checked={field.value.includes(s.id)} />
                        <ListItemText primary={s.name} />
                      </MenuItem>
                    ))}
                  </TextField>
                )}
              />
              <FormSelect
                control={control} name="primarySpecializationId" label="Primary specialization"
                options={selectedSpecs.map((sid) => ({ value: String(sid), label: String(specName(sid)) }))}
              />
              <FormText control={control} name="consultationFee" label="Consultation fee (₹)" type="number" required
                helperText={doctorId ? 'Changes apply to new invoices only' : undefined} />
            </Box>
          </Section>
          <Stack direction="row" spacing={1.5} justifyContent="flex-end">
            <Button onClick={() => navigate(-1)}>Cancel</Button>
            <Button type="submit" variant="contained" disabled={save.isPending}>{save.isPending ? 'Saving…' : 'Save'}</Button>
          </Stack>
        </Stack>
      </Box>
    </>
  );
}

import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useFieldArray, useForm } from 'react-hook-form';
import {
  Alert, Box, Button, Chip, IconButton, List, ListItemButton, ListItemText, MenuItem, Paper, Stack, Tab, Tabs, Typography,
} from '@mui/material';
import DeleteIcon from '@mui/icons-material/DeleteOutline';
import AddIcon from '@mui/icons-material/Add';
import { consultationsApi, patientsApi } from '../../api/endpoints';
import { keys } from '../../api/queryKeys';
import { errorMessage, getProblem } from '../../api/errors';
import { ConfirmDialog, EmptyState, ErrorAlert, Loading, PageHeader, ReasonDialog, Section, StatusChip } from '../../components/common';
import { FormText } from '../../components/form';
import { useNotify } from '../../components/Notifications';
import { useAuth } from '../../auth/AuthProvider';
import { emptyToNull, formatDateTime } from '../../utils/format';
import type { ConsultationDto, DurationUnit, SymptomDto } from '../../types/api';
import { VitalsSection } from './VitalsSection';
import { PrescriptionEditor } from './PrescriptionEditor';

interface ClinicalForm {
  chiefComplaint: string;
  historyOfPresentIllness: string;
  examinationFindings: string;
  diagnosis: string;
  diagnosisIcd10: string;
  clinicalNotes: string;
  advice: string;
  symptoms: { symptom: string; durationValue: string; durationUnit: DurationUnit | ''; severity: string }[];
}

const toForm = (c: ConsultationDto): ClinicalForm => ({
  chiefComplaint: c.chiefComplaint ?? '', historyOfPresentIllness: c.historyOfPresentIllness ?? '',
  examinationFindings: c.examinationFindings ?? '', diagnosis: c.diagnosis ?? '', diagnosisIcd10: c.diagnosisIcd10 ?? '',
  clinicalNotes: c.clinicalNotes ?? '', advice: c.advice ?? '',
  symptoms: c.symptoms.map((s) => ({ symptom: s.symptom, durationValue: s.durationValue?.toString() ?? '', durationUnit: s.durationUnit ?? '', severity: s.severity ?? '' })),
});

const toSymptoms = (rows: ClinicalForm['symptoms']): SymptomDto[] =>
  rows.filter((s) => s.symptom.trim()).map((s) => ({
    symptom: s.symptom.trim(), durationValue: s.durationValue ? Number(s.durationValue) : null,
    durationUnit: s.durationUnit || null, severity: s.severity || null,
  }));

export default function ConsultationPage() {
  const consultationId = Number(useParams().id);
  const [tab, setTab] = useState(0);
  const { hasRole } = useAuth();
  const { data: c, error, isLoading } = useQuery({
    queryKey: keys.consultations.detail(consultationId),
    queryFn: () => consultationsApi.get(consultationId),
  });

  if (isLoading) return <Loading />;
  if (error || !c) return <ErrorAlert error={error} />;

  return (
    <>
      <PageHeader
        title={c.patient.fullName}
        subtitle={
          <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap component="span">
            <Chip size="small" label={c.patient.mrn} sx={{ fontFamily: 'monospace' }} />
            <span>{c.patient.age ?? '?'} y / {c.patient.gender}{c.patient.bloodGroup ? ` · ${c.patient.bloodGroup}` : ''}</span>
            <span>· {c.doctorName} · started {formatDateTime(c.startedAt)}</span>
            <StatusChip status={c.status} />
          </Stack>
        }
      />
      {(c.patient.allergies.length > 0 || c.patient.conditions.length > 0) && (
        <Stack direction={{ xs: 'column', md: 'row' }} spacing={1} sx={{ mb: 2 }}>
          {c.patient.allergies.length > 0 && <Alert severity="error" sx={{ flex: 1 }}><strong>Allergies:</strong> {c.patient.allergies.join(', ')}</Alert>}
          {c.patient.conditions.length > 0 && <Alert severity="info" sx={{ flex: 1 }}><strong>History:</strong> {c.patient.conditions.join(', ')}</Alert>}
        </Stack>
      )}
      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2, borderBottom: 1, borderColor: 'divider' }}>
        <Tab label="Consultation" />
        <Tab label="Change history" />
      </Tabs>
      {tab === 0 ? (
        <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', xl: '3fr 1fr' }, alignItems: 'start' }}>
          <Stack spacing={2} sx={{ minWidth: 0 }}>
            <VitalsSection consultation={c} canRecord={c.canEdit || hasRole('Nurse')} />
            <ClinicalNotes consultation={c} />
            <PrescriptionEditor consultation={c} />
          </Stack>
          <PreviousVisits patientId={c.patient.id} currentId={c.id} />
        </Box>
      ) : (
        <HistoryTab consultationId={c.id} />
      )}
    </>
  );
}

function ClinicalNotes({ consultation: c }: { consultation: ConsultationDto }) {
  const queryClient = useQueryClient();
  const notify = useNotify();
  const navigate = useNavigate();
  const [amendOpen, setAmendOpen] = useState(false);
  const [finaliseOpen, setFinaliseOpen] = useState(false);
  const [editing, setEditing] = useState(c.status === 'Draft');
  const { control, handleSubmit, reset, getValues, formState } = useForm<ClinicalForm>({ defaultValues: toForm(c) });
  const symptoms = useFieldArray({ control, name: 'symptoms' });
  const readOnly = !c.canEdit || !editing;

  useEffect(() => {
    reset(toForm(c));
  }, [c, reset]);

  const invalidate = (updated: ConsultationDto) => {
    queryClient.setQueryData(keys.consultations.detail(c.id), updated);
    queryClient.invalidateQueries({ queryKey: keys.consultations.history(c.id) });
    queryClient.invalidateQueries({ queryKey: keys.appointments.all });
    queryClient.invalidateQueries({ queryKey: keys.dashboard });
  };

  const save = useMutation({
    mutationFn: (amendmentReason: string | null) => {
      const v = getValues();
      return consultationsApi.update(c.id, {
        chiefComplaint: emptyToNull(v.chiefComplaint), historyOfPresentIllness: emptyToNull(v.historyOfPresentIllness),
        examinationFindings: emptyToNull(v.examinationFindings), diagnosis: emptyToNull(v.diagnosis),
        diagnosisIcd10: emptyToNull(v.diagnosisIcd10), clinicalNotes: emptyToNull(v.clinicalNotes), advice: emptyToNull(v.advice),
        symptoms: toSymptoms(v.symptoms), rowVersion: c.rowVersion, amendmentReason,
      });
    },
    onSuccess: (updated) => {
      invalidate(updated);
      if (updated.status === 'Finalised') setEditing(false);
      notify('Clinical notes saved');
    },
    onError: (e) => {
      const problem = getProblem(e);
      notify(problem?.errors ? Object.values(problem.errors).flat()[0] : errorMessage(e), 'error');
    },
  });

  const finalise = useMutation({
    mutationFn: async () => {
      if (formState.isDirty) await save.mutateAsync(null);
      return consultationsApi.finalise(c.id);
    },
    onSuccess: (updated) => {
      setFinaliseOpen(false);
      setEditing(false);
      invalidate(updated);
      notify('Consultation finalised · appointment completed');
    },
    onError: (e) => {
      setFinaliseOpen(false);
      notify(errorMessage(e), 'error');
    },
  });

  const onSave = handleSubmit(() => (c.status === 'Finalised' ? setAmendOpen(true) : save.mutate(null)));

  return (
    <Section
      title="Clinical notes"
      action={c.canEdit && c.status === 'Finalised' && !editing && <Button size="small" variant="outlined" onClick={() => setEditing(true)}>Amend</Button>}
    >
      <Stack spacing={2} component="form" onSubmit={onSave}>
        <FormText control={control} name="chiefComplaint" label="Chief complaint" disabled={readOnly} required helperText="Required to finalise" />

        <Box>
          <Typography variant="subtitle2" sx={{ mb: 1 }}>Symptoms</Typography>
          <Stack spacing={1}>
            {symptoms.fields.map((f, i) => (
              <Stack key={f.id} direction={{ xs: 'column', sm: 'row' }} spacing={1}>
                <FormText control={control} name={`symptoms.${i}.symptom`} label="Symptom" disabled={readOnly} sx={{ flex: 2 }} />
                <FormText control={control} name={`symptoms.${i}.durationValue`} label="For" type="number" disabled={readOnly} sx={{ flex: 0.6 }} />
                <FormText control={control} name={`symptoms.${i}.durationUnit`} label="Unit" select disabled={readOnly} sx={{ flex: 0.8 }}>
                  <MenuItem value="">—</MenuItem>
                  {['Hours', 'Days', 'Weeks', 'Months'].map((u) => <MenuItem key={u} value={u}>{u}</MenuItem>)}
                </FormText>
                <FormText control={control} name={`symptoms.${i}.severity`} label="Severity" select disabled={readOnly} sx={{ flex: 0.8 }}>
                  <MenuItem value="">—</MenuItem>
                  {['Mild', 'Moderate', 'Severe'].map((u) => <MenuItem key={u} value={u}>{u}</MenuItem>)}
                </FormText>
                {!readOnly && <IconButton aria-label="Remove symptom" onClick={() => symptoms.remove(i)}><DeleteIcon /></IconButton>}
              </Stack>
            ))}
            {symptoms.fields.length === 0 && readOnly && <Typography variant="body2" color="text.secondary">None recorded.</Typography>}
            {!readOnly && (
              <Box>
                <Button size="small" startIcon={<AddIcon />} onClick={() => symptoms.append({ symptom: '', durationValue: '', durationUnit: 'Days', severity: '' })}>
                  Add symptom
                </Button>
              </Box>
            )}
          </Stack>
        </Box>

        <FormText control={control} name="historyOfPresentIllness" label="History of present illness" multiline minRows={2} disabled={readOnly} />
        <FormText control={control} name="examinationFindings" label="Examination findings" multiline minRows={2} disabled={readOnly} />
        <Stack direction={{ xs: 'column', sm: 'row' }} spacing={2}>
          <FormText control={control} name="diagnosis" label="Diagnosis" disabled={readOnly} sx={{ flex: 3 }} />
          <FormText control={control} name="diagnosisIcd10" label="ICD-10 (optional)" placeholder="J06.9" disabled={readOnly} sx={{ flex: 1 }} />
        </Stack>
        <FormText control={control} name="clinicalNotes" label="Clinical notes" multiline minRows={3} disabled={readOnly} />
        <FormText control={control} name="advice" label="Advice to patient" multiline minRows={2} disabled={readOnly} />

        {c.canEdit && (
          <Stack direction="row" spacing={1} justifyContent="flex-end">
            {c.status === 'Finalised' && editing && <Button onClick={() => { reset(toForm(c)); setEditing(false); }}>Discard</Button>}
            {editing && <Button type="submit" variant="outlined" disabled={save.isPending}>{c.status === 'Finalised' ? 'Save amendment' : 'Save draft'}</Button>}
            {c.status === 'Draft' && (
              <Button variant="contained" color="success" disabled={finalise.isPending} onClick={() => setFinaliseOpen(true)}>Finalise consultation</Button>
            )}
            {c.status === 'Finalised' && !editing && <Button onClick={() => navigate('/queue')}>Back to queue</Button>}
          </Stack>
        )}
      </Stack>

      <ConfirmDialog
        open={finaliseOpen}
        title="Finalise consultation?"
        message="The appointment will be marked completed. Later changes will be recorded as amendments with a reason."
        confirmLabel="Finalise"
        loading={finalise.isPending}
        onClose={() => setFinaliseOpen(false)}
        onConfirm={() => finalise.mutate()}
      />
      <ReasonDialog
        open={amendOpen}
        title="Amend finalised consultation"
        label="Reason for amendment"
        helper="The previous version is kept in the change history."
        confirmLabel="Save amendment"
        onClose={() => setAmendOpen(false)}
        onConfirm={(reason) => {
          setAmendOpen(false);
          save.mutate(reason);
        }}
      />
    </Section>
  );
}

function PreviousVisits({ patientId, currentId }: { patientId: number; currentId: number }) {
  const navigate = useNavigate();
  const { data, isLoading } = useQuery({ queryKey: keys.patients.visits(patientId), queryFn: () => patientsApi.visits(patientId) });
  const visits = data?.items.filter((v) => v.consultationId !== currentId) ?? [];
  return (
    <Paper sx={{ p: 2, position: { xl: 'sticky' }, top: { xl: 88 } }}>
      <Typography variant="subtitle1" fontWeight={650} sx={{ mb: 1 }}>Previous visits</Typography>
      {isLoading ? <Loading /> : visits.length === 0 ? (
        <Typography variant="body2" color="text.secondary">This is the patient's first recorded visit.</Typography>
      ) : (
        <List dense disablePadding>
          {visits.map((v) => (
            <ListItemButton key={v.consultationId} onClick={() => navigate(`/consultations/${v.consultationId}`)} sx={{ borderRadius: 2, alignItems: 'flex-start' }}>
              <ListItemText
                primary={v.diagnosis ?? v.chiefComplaint ?? 'No diagnosis recorded'}
                secondary={`${formatDateTime(v.startedAt)} · ${v.doctorName}`}
                slotProps={{ primary: { fontWeight: 600, fontSize: 14 } }}
              />
            </ListItemButton>
          ))}
        </List>
      )}
    </Paper>
  );
}

const FIELD_LABELS: Record<string, string> = {
  chiefComplaint: 'Chief complaint', historyOfPresentIllness: 'History of present illness', examinationFindings: 'Examination',
  diagnosis: 'Diagnosis', diagnosisIcd10: 'ICD-10', clinicalNotes: 'Clinical notes', advice: 'Advice', symptoms: 'Symptoms',
  status: 'Status', generalInstructions: 'Instructions', followUpDate: 'Follow-up date', items: 'Medicines',
  temperatureC: 'Temperature', pulseBpm: 'Pulse', bpSystolic: 'BP systolic', bpDiastolic: 'BP diastolic',
  respiratoryRate: 'Resp. rate', spo2Percent: 'SpO₂', weightKg: 'Weight', heightCm: 'Height', bmi: 'BMI', notes: 'Notes',
};

function HistoryTab({ consultationId }: { consultationId: number }) {
  const { data, error, isLoading } = useQuery({
    queryKey: keys.consultations.history(consultationId),
    queryFn: () => consultationsApi.history(consultationId),
  });
  if (isLoading) return <Loading />;
  if (error) return <ErrorAlert error={error} />;
  if (!data?.length) return <EmptyState message="No history yet." />;
  return (
    <Stack spacing={1.5}>
      {data.map((r) => (
        <Paper key={r.id} sx={{ p: 2, borderLeft: 4, borderLeftColor: r.changeType === 'Amended' ? 'warning.main' : r.changeType === 'Finalised' ? 'success.main' : 'primary.light' }}>
          <Stack direction="row" spacing={1} alignItems="center" flexWrap="wrap" useFlexGap>
            <Chip size="small" label={r.entityType === 'VitalSigns' ? 'Vitals' : r.entityType} variant="outlined" />
            <Chip size="small" label={`${r.changeType} · v${r.revisionNumber}`} color={r.changeType === 'Amended' ? 'warning' : r.changeType === 'Finalised' ? 'success' : 'default'} />
            <Typography variant="caption" color="text.secondary">{formatDateTime(r.changedAt)} · {r.changedByName ?? '—'}</Typography>
          </Stack>
          {r.reason && <Typography variant="body2" sx={{ mt: 1 }}><strong>Reason:</strong> {r.reason}</Typography>}
          {r.changes.length > 0 && (
            <Box component="dl" sx={{ mt: 1, mb: 0, display: 'grid', gridTemplateColumns: { xs: '1fr', sm: '180px 1fr' }, columnGap: 2, rowGap: 0.75 }}>
              {r.changes.map((ch) => (
                <Box key={ch.field} sx={{ display: 'contents' }}>
                  <Typography component="dt" variant="body2" color="text.secondary">{FIELD_LABELS[ch.field] ?? ch.field}</Typography>
                  <Typography component="dd" variant="body2" sx={{ m: 0, wordBreak: 'break-word' }}>
                    <Box component="span" sx={{ textDecoration: 'line-through', color: 'error.main', mr: 1 }}>{summarise(ch.oldValue)}</Box>
                    <Box component="span" sx={{ color: 'success.dark' }}>{summarise(ch.newValue)}</Box>
                  </Typography>
                </Box>
              ))}
            </Box>
          )}
        </Paper>
      ))}
    </Stack>
  );
}

/** Lists/objects arrive as raw JSON; show something readable. */
function summarise(value: string | null) {
  if (value == null || value === '') return '∅';
  if (value.startsWith('[')) {
    try {
      const arr = JSON.parse(value) as Record<string, unknown>[];
      return arr.map((x) => String(x.symptom ?? x.medicineName ?? JSON.stringify(x))).join(', ') || '∅';
    } catch {
      return value;
    }
  }
  return value.length > 160 ? `${value.slice(0, 160)}…` : value;
}

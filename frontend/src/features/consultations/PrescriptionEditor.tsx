import { useEffect, useState } from 'react';
import { Link as RouterLink } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert, Autocomplete, Box, Button, IconButton, MenuItem, Stack, TextField, Typography,
} from '@mui/material';
import DeleteIcon from '@mui/icons-material/DeleteOutline';
import AddIcon from '@mui/icons-material/Add';
import PrintIcon from '@mui/icons-material/PrintOutlined';
import EventIcon from '@mui/icons-material/EventAvailable';
import { consultationsApi, masterApi, prescriptionsApi } from '../../api/endpoints';
import { keys } from '../../api/queryKeys';
import { errorMessage, getProblem } from '../../api/errors';
import { ReasonDialog, Section, StatusChip } from '../../components/common';
import { useNotify } from '../../components/Notifications';
import { addDaysIso, formatDate, todayIso } from '../../utils/format';
import type { ConsultationDto, DurationUnit, FrequencyCode, MedicineDto, PrescriptionDto, PrescriptionItemDto } from '../../types/api';

export const FREQUENCIES: { code: FrequencyCode; label: string }[] = [
  { code: 'OD', label: 'OD · once daily' },
  { code: 'BD', label: 'BD · twice daily' },
  { code: 'TDS', label: 'TDS · three times daily' },
  { code: 'QID', label: 'QID · four times daily' },
  { code: 'HS', label: 'HS · at bedtime' },
  { code: 'SOS', label: 'SOS · when required' },
  { code: 'STAT', label: 'STAT · immediately' },
  { code: 'CUSTOM', label: 'Custom…' },
];
const ROUTES = ['Oral', 'Topical', 'Inhalation', 'IV', 'IM', 'SC', 'Sublingual', 'Eye drops', 'Ear drops', 'Nasal'];
const UNITS: DurationUnit[] = ['Days', 'Weeks', 'Months'];

const emptyItem = (): PrescriptionItemDto => ({
  medicineId: null, medicineName: '', dosage: '', route: 'Oral', frequencyCode: 'BD', frequencyText: null,
  durationValue: 5, durationUnit: 'Days', quantity: null, instructions: 'After food',
});

export function PrescriptionEditor({ consultation }: { consultation: ConsultationDto }) {
  const queryClient = useQueryClient();
  const notify = useNotify();
  const prescriptionId = consultation.prescriptionId;
  const { data: prescription } = useQuery({
    queryKey: keys.prescriptions.detail(prescriptionId ?? 0),
    queryFn: () => prescriptionsApi.get(prescriptionId!),
    enabled: !!prescriptionId,
  });

  const [items, setItems] = useState<PrescriptionItemDto[]>([]);
  const [instructions, setInstructions] = useState('');
  const [followUp, setFollowUp] = useState('');
  const [editing, setEditing] = useState(false);
  const [amendOpen, setAmendOpen] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({});

  useEffect(() => {
    if (prescription && !editing) {
      setItems(prescription.items);
      setInstructions(prescription.generalInstructions ?? '');
      setFollowUp(prescription.followUpDate ?? '');
    }
  }, [prescription, editing]);

  const canEdit = consultation.canEdit;
  const finalised = prescription?.status === 'Finalised';
  const readOnly = !canEdit || (!!prescription && !editing);

  const refresh = (p: PrescriptionDto) => {
    queryClient.setQueryData(keys.prescriptions.detail(p.id), p);
    queryClient.invalidateQueries({ queryKey: keys.consultations.detail(consultation.id) });
    queryClient.invalidateQueries({ queryKey: keys.consultations.history(consultation.id) });
    queryClient.invalidateQueries({ queryKey: keys.patients.prescriptions(consultation.patient.id) });
  };

  const save = useMutation({
    mutationFn: (amendmentReason: string | null) => {
      const body = {
        generalInstructions: instructions.trim() || null,
        followUpDate: followUp || null,
        items,
        rowVersion: prescription?.rowVersion ?? null,
        amendmentReason,
      };
      return prescription ? prescriptionsApi.update(prescription.id, body) : consultationsApi.createPrescription(consultation.id, body);
    },
    onSuccess: (p) => {
      setEditing(false);
      setFieldErrors({});
      refresh(p);
      notify(p.allergyWarnings.length ? 'Prescription saved — check allergy warnings' : 'Prescription saved', p.allergyWarnings.length ? 'warning' : 'success');
    },
    onError: (e) => {
      const problem = getProblem(e);
      if (problem?.errors) setFieldErrors(problem.errors);
      notify(errorMessage(e), 'error');
    },
  });

  const finalise = useMutation({
    mutationFn: () => prescriptionsApi.finalise(prescription!.id),
    onSuccess: (p) => {
      refresh(p);
      notify('Prescription finalised');
    },
    onError: (e) => notify(errorMessage(e), 'error'),
  });

  const update = (i: number, patch: Partial<PrescriptionItemDto>) => setItems(items.map((it, j) => (i === j ? { ...it, ...patch } : it)));
  const started = !!prescription || editing;

  return (
    <Section
      title={prescription ? `Prescription · ${prescription.prescriptionNumber}` : 'Prescription'}
      action={
        <Stack direction="row" spacing={1} alignItems="center">
          {prescription && <StatusChip status={prescription.status} />}
          {prescription && (
            <Button size="small" startIcon={<PrintIcon />} component={RouterLink} to={`/print/prescriptions/${prescription.id}`} target="_blank">Print</Button>
          )}
        </Stack>
      }
    >
      {!started ? (
        canEdit ? (
          <Button variant="outlined" startIcon={<AddIcon />} onClick={() => { setItems([emptyItem()]); setEditing(true); }}>Write prescription</Button>
        ) : (
          <Typography variant="body2" color="text.secondary">No prescription for this visit.</Typography>
        )
      ) : (
        <Stack spacing={2}>
          {prescription?.allergyWarnings.map((w) => <Alert key={w} severity="warning">{w}</Alert>)}

          {items.map((item, i) => (
            <Box key={i} sx={{ p: 1.5, borderRadius: 2, border: 1, borderColor: 'divider', bgcolor: readOnly ? '#fafcfc' : 'background.paper' }}>
              {readOnly ? (
                <Typography variant="body2">
                  <strong>{i + 1}. {item.medicineName}</strong> — {item.dosage}{item.route ? ` · ${item.route}` : ''} ·{' '}
                  {item.frequencyCode === 'CUSTOM' ? item.frequencyText : item.frequencyCode}
                  {item.durationValue ? ` · ${item.durationValue} ${item.durationUnit}` : ''}
                  {item.instructions ? ` · ${item.instructions}` : ''}
                </Typography>
              ) : (
                <ItemEditor item={item} index={i} errors={fieldErrors} onChange={(p) => update(i, p)} onRemove={() => setItems(items.filter((_, j) => j !== i))} />
              )}
            </Box>
          ))}
          {!readOnly && (
            <Box><Button size="small" startIcon={<AddIcon />} onClick={() => setItems([...items, emptyItem()])}>Add medicine</Button></Box>
          )}

          <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: '3fr 1fr' } }}>
            <TextField label="General instructions" value={instructions} onChange={(e) => setInstructions(e.target.value)} multiline minRows={2} disabled={readOnly} />
            <TextField
              label="Follow-up date" type="date" value={followUp} onChange={(e) => setFollowUp(e.target.value)} disabled={readOnly}
              error={!!fieldErrors.followUpDate} helperText={fieldErrors.followUpDate?.[0]}
              slotProps={{ inputLabel: { shrink: true }, htmlInput: { min: addDaysIso(todayIso(), 1) } }}
            />
          </Box>

          <Stack direction="row" spacing={1} justifyContent="flex-end" flexWrap="wrap" useFlexGap>
            {prescription?.followUpDate && (
              <Button
                startIcon={<EventIcon />}
                component={RouterLink}
                to={`/appointments/book?patientId=${consultation.patient.id}&doctorId=${consultation.doctorId}&date=${prescription.followUpDate}&followUpOf=${consultation.id}`}
              >
                Book follow-up ({formatDate(prescription.followUpDate)})
              </Button>
            )}
            {canEdit && readOnly && (
              <Button variant="outlined" onClick={() => setEditing(true)}>{finalised ? 'Amend' : 'Edit'}</Button>
            )}
            {canEdit && !readOnly && (
              <>
                {prescription && <Button onClick={() => { setEditing(false); setFieldErrors({}); }}>Discard changes</Button>}
                {!prescription && <Button onClick={() => { setEditing(false); setItems([]); }}>Cancel</Button>}
                <Button
                  variant="contained"
                  disabled={save.isPending || items.some((i) => !i.medicineName.trim() || !i.dosage.trim())}
                  onClick={() => (finalised ? setAmendOpen(true) : save.mutate(null))}
                >
                  Save prescription
                </Button>
              </>
            )}
            {canEdit && prescription && !finalised && readOnly && (
              <Button variant="contained" color="success" disabled={finalise.isPending} onClick={() => finalise.mutate()}>Finalise</Button>
            )}
          </Stack>
        </Stack>
      )}

      <ReasonDialog
        open={amendOpen}
        title="Amend finalised prescription"
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

function ItemEditor({ item, index, errors, onChange, onRemove }: {
  item: PrescriptionItemDto;
  index: number;
  errors: Record<string, string[]>;
  onChange: (patch: Partial<PrescriptionItemDto>) => void;
  onRemove: () => void;
}) {
  const [search, setSearch] = useState(item.medicineName);
  const medicines = useQuery({
    queryKey: keys.medicines(search.trim()),
    queryFn: () => masterApi.medicines(search.trim(), true),
    enabled: search.trim().length >= 2,
    staleTime: 5 * 60_000,
  });
  const err = (field: string) => errors[`items[${index}].${field}`]?.[0];
  const label = (m: MedicineDto) => [m.name, m.strength].filter(Boolean).join(' ');

  return (
    <Stack spacing={1.5}>
      <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.5}>
        <Autocomplete<MedicineDto, false, false, true>
          freeSolo
          sx={{ flex: 2 }}
          options={medicines.data ?? []}
          filterOptions={(x) => x}
          getOptionLabel={(o) => (typeof o === 'string' ? o : label(o))}
          inputValue={search}
          onInputChange={(_, v, reason) => {
            setSearch(v);
            if (reason === 'input') onChange({ medicineName: v, medicineId: null });
          }}
          onChange={(_, v) => {
            if (v && typeof v !== 'string') onChange({ medicineId: v.id, medicineName: label(v), dosage: item.dosage || (v.form === 'Tablet' || v.form === 'Capsule' ? '1 ' + v.form.toLowerCase() : '') });
          }}
          renderOption={(props, m) => (
            <li {...props} key={m.id}>
              <Box>
                <Typography variant="body2">{label(m)}</Typography>
                <Typography variant="caption" color="text.secondary">{[m.genericName, m.form].filter(Boolean).join(' · ')}</Typography>
              </Box>
            </li>
          )}
          renderInput={(p) => <TextField {...p} label={`Medicine ${index + 1}`} required error={!!err('medicineName')} helperText={err('medicineName') ?? 'Pick from the list or type any name'} />}
        />
        <TextField label="Dosage" value={item.dosage} onChange={(e) => onChange({ dosage: e.target.value })} placeholder="1 tablet / 5 ml" required sx={{ flex: 1 }} error={!!err('dosage')} helperText={err('dosage')} />
        <TextField select label="Route" value={item.route ?? ''} onChange={(e) => onChange({ route: e.target.value || null })} sx={{ flex: 1 }}>
          <MenuItem value="">—</MenuItem>
          {ROUTES.map((r) => <MenuItem key={r} value={r}>{r}</MenuItem>)}
        </TextField>
      </Stack>
      <Stack direction={{ xs: 'column', md: 'row' }} spacing={1.5} alignItems={{ md: 'flex-start' }}>
        <TextField select label="Frequency" value={item.frequencyCode} onChange={(e) => onChange({ frequencyCode: e.target.value as FrequencyCode })} sx={{ flex: 1.4 }}>
          {FREQUENCIES.map((f) => <MenuItem key={f.code} value={f.code}>{f.label}</MenuItem>)}
        </TextField>
        <TextField
          label="Frequency detail" value={item.frequencyText ?? ''} onChange={(e) => onChange({ frequencyText: e.target.value || null })}
          placeholder="1-0-1" sx={{ flex: 1 }} required={item.frequencyCode === 'CUSTOM'} error={!!err('frequencyText')} helperText={err('frequencyText')}
        />
        <TextField label="Duration" type="number" value={item.durationValue ?? ''} onChange={(e) => onChange({ durationValue: e.target.value ? Number(e.target.value) : null })} sx={{ flex: 0.7 }} />
        <TextField select label="Unit" value={item.durationUnit ?? ''} onChange={(e) => onChange({ durationUnit: (e.target.value || null) as DurationUnit | null })} sx={{ flex: 0.8 }}>
          {UNITS.map((u) => <MenuItem key={u} value={u}>{u}</MenuItem>)}
        </TextField>
        <TextField label="Instructions" value={item.instructions ?? ''} onChange={(e) => onChange({ instructions: e.target.value || null })} sx={{ flex: 1.4 }} />
        <IconButton aria-label={`Remove medicine ${index + 1}`} onClick={onRemove}><DeleteIcon /></IconButton>
      </Stack>
    </Stack>
  );
}

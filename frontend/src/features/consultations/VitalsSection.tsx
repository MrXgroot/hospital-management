import { useState } from 'react';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, IconButton, Stack, TextField, Tooltip, Typography,
} from '@mui/material';
import EditIcon from '@mui/icons-material/EditOutlined';
import WarningIcon from '@mui/icons-material/WarningAmberRounded';
import { consultationsApi } from '../../api/endpoints';
import { keys } from '../../api/queryKeys';
import { errorMessage, getProblem } from '../../api/errors';
import { Section } from '../../components/common';
import { useNotify } from '../../components/Notifications';
import { formatDateTime, toNumberOrNull } from '../../utils/format';
import type { ConsultationDto, SaveVitalsRequest, VitalSignsDto } from '../../types/api';

const FIELDS: { key: keyof Omit<SaveVitalsRequest, 'notes' | 'amendmentReason'>; label: string; unit: string }[] = [
  { key: 'temperatureC', label: 'Temperature', unit: '°C' },
  { key: 'pulseBpm', label: 'Pulse', unit: 'bpm' },
  { key: 'bpSystolic', label: 'BP systolic', unit: 'mmHg' },
  { key: 'bpDiastolic', label: 'BP diastolic', unit: 'mmHg' },
  { key: 'respiratoryRate', label: 'Resp. rate', unit: '/min' },
  { key: 'spo2Percent', label: 'SpO₂', unit: '%' },
  { key: 'weightKg', label: 'Weight', unit: 'kg' },
  { key: 'heightCm', label: 'Height', unit: 'cm' },
];

type Draft = Record<string, string>;

const toDraft = (v?: VitalSignsDto): Draft =>
  Object.fromEntries([...FIELDS.map((f) => [f.key, v?.[f.key]?.toString() ?? '']), ['notes', v?.notes ?? ''], ['amendmentReason', '']]);

export function VitalsSection({ consultation, canRecord }: { consultation: ConsultationDto; canRecord: boolean }) {
  const queryClient = useQueryClient();
  const notify = useNotify();
  const [editing, setEditing] = useState<{ vitals?: VitalSignsDto } | null>(null);
  const [draft, setDraft] = useState<Draft>(toDraft());
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const finalised = consultation.status === 'Finalised';

  const open = (vitals?: VitalSignsDto) => {
    setDraft(toDraft(vitals));
    setErrors({});
    setEditing({ vitals });
  };

  const save = useMutation({
    mutationFn: () => {
      const body: SaveVitalsRequest = {
        temperatureC: toNumberOrNull(draft.temperatureC), pulseBpm: toNumberOrNull(draft.pulseBpm),
        bpSystolic: toNumberOrNull(draft.bpSystolic), bpDiastolic: toNumberOrNull(draft.bpDiastolic),
        respiratoryRate: toNumberOrNull(draft.respiratoryRate), spo2Percent: toNumberOrNull(draft.spo2Percent),
        weightKg: toNumberOrNull(draft.weightKg), heightCm: toNumberOrNull(draft.heightCm),
        notes: draft.notes.trim() || null, amendmentReason: draft.amendmentReason.trim() || null,
      };
      return editing?.vitals
        ? consultationsApi.updateVitals(consultation.id, editing.vitals.id, body)
        : consultationsApi.addVitals(consultation.id, body);
    },
    onSuccess: (v) => {
      setEditing(null);
      queryClient.invalidateQueries({ queryKey: keys.consultations.detail(consultation.id) });
      queryClient.invalidateQueries({ queryKey: keys.consultations.history(consultation.id) });
      notify(v.warnings.length ? `Vitals saved · ${v.warnings.length} abnormal value(s)` : 'Vitals saved', v.warnings.length ? 'warning' : 'success');
    },
    onError: (e) => {
      const problem = getProblem(e);
      if (problem?.errors) setErrors(problem.errors);
      else notify(errorMessage(e), 'error');
    },
  });

  const latest = consultation.vitals[0];

  return (
    <Section title="Vital signs" action={canRecord && <Button size="small" variant="outlined" onClick={() => open()}>Record vitals</Button>}>
      {!latest ? (
        <Typography variant="body2" color="text.secondary">No vitals recorded yet.</Typography>
      ) : (
        <Stack spacing={2}>
          {consultation.vitals.map((v, idx) => (
            <Box key={v.id} sx={{ opacity: idx === 0 ? 1 : 0.75 }}>
              <Stack direction="row" justifyContent="space-between" alignItems="center">
                <Typography variant="caption" color="text.secondary">
                  {formatDateTime(v.recordedAt)} · {v.recordedByName ?? '—'}{idx === 0 ? ' · latest' : ''}
                </Typography>
                {canRecord && (
                  <Tooltip title="Correct this reading">
                    <IconButton size="small" aria-label="Edit vitals" onClick={() => open(v)}><EditIcon fontSize="small" /></IconButton>
                  </Tooltip>
                )}
              </Stack>
              <Box sx={{ display: 'grid', gap: 1, mt: 0.5, gridTemplateColumns: { xs: 'repeat(2, 1fr)', sm: 'repeat(4, 1fr)', xl: 'repeat(5, 1fr)' } }}>
                {v.bpSystolic != null && <Metric label="BP" value={`${v.bpSystolic}/${v.bpDiastolic ?? '?'}`} unit="mmHg" />}
                {v.pulseBpm != null && <Metric label="Pulse" value={v.pulseBpm} unit="bpm" />}
                {v.temperatureC != null && <Metric label="Temp" value={v.temperatureC} unit="°C" />}
                {v.spo2Percent != null && <Metric label="SpO₂" value={v.spo2Percent} unit="%" />}
                {v.respiratoryRate != null && <Metric label="RR" value={v.respiratoryRate} unit="/min" />}
                {v.weightKg != null && <Metric label="Weight" value={v.weightKg} unit="kg" />}
                {v.heightCm != null && <Metric label="Height" value={v.heightCm} unit="cm" />}
                {v.bmi != null && <Metric label="BMI" value={v.bmi} unit="" />}
              </Box>
              {v.warnings.length > 0 && (
                <Stack direction="row" spacing={0.5} alignItems="center" sx={{ mt: 0.75, color: 'warning.dark' }}>
                  <WarningIcon fontSize="small" />
                  <Typography variant="caption">{v.warnings.join(' · ')}</Typography>
                </Stack>
              )}
              {v.notes && <Typography variant="caption" color="text.secondary">Note: {v.notes}</Typography>}
            </Box>
          ))}
        </Stack>
      )}

      <Dialog open={!!editing} onClose={() => setEditing(null)} maxWidth="sm" fullWidth>
        <DialogTitle>{editing?.vitals ? 'Correct vital signs' : 'Record vital signs'}</DialogTitle>
        <DialogContent>
          <Box sx={{ display: 'grid', gap: 2, mt: 1, gridTemplateColumns: { xs: '1fr 1fr', sm: 'repeat(4, 1fr)' } }}>
            {FIELDS.map((f) => (
              <TextField
                key={f.key}
                label={`${f.label} (${f.unit})`}
                value={draft[f.key]}
                onChange={(e) => setDraft({ ...draft, [f.key]: e.target.value })}
                error={!!errors[f.key]}
                helperText={errors[f.key]?.[0]}
                slotProps={{ htmlInput: { inputMode: 'decimal' } }}
              />
            ))}
          </Box>
          {errors.vitals && <Alert severity="error" sx={{ mt: 2 }}>{errors.vitals[0]}</Alert>}
          <TextField label="Notes" value={draft.notes} onChange={(e) => setDraft({ ...draft, notes: e.target.value })} sx={{ mt: 2 }} />
          {(finalised || editing?.vitals) && (
            <TextField
              label={finalised ? 'Amendment reason (required — record is finalised)' : 'Reason for correction (optional)'}
              value={draft.amendmentReason}
              onChange={(e) => setDraft({ ...draft, amendmentReason: e.target.value })}
              sx={{ mt: 2 }}
              required={finalised}
            />
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setEditing(null)}>Cancel</Button>
          <Button variant="contained" disabled={save.isPending || (finalised && !draft.amendmentReason.trim())} onClick={() => save.mutate()}>Save</Button>
        </DialogActions>
      </Dialog>
    </Section>
  );
}

function Metric({ label, value, unit }: { label: string; value: string | number; unit: string }) {
  return (
    <Box sx={{ p: 1, borderRadius: 1.5, bgcolor: '#f8fafb', border: 1, borderColor: 'divider' }}>
      <Typography variant="caption" color="text.secondary">{label}</Typography>
      <Typography variant="body2" fontWeight={650} sx={{ fontVariantNumeric: 'tabular-nums' }}>
        {value} <Typography component="span" variant="caption" color="text.secondary">{unit}</Typography>
      </Typography>
    </Box>
  );
}

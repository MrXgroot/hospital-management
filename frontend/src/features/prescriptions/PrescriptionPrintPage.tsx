import { useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { Box, Button, GlobalStyles, Stack, Table, TableBody, TableCell, TableHead, TableRow, Typography } from '@mui/material';
import PrintIcon from '@mui/icons-material/Print';
import { prescriptionsApi } from '../../api/endpoints';
import { keys } from '../../api/queryKeys';
import { ErrorAlert, Loading } from '../../components/common';
import { formatDate } from '../../utils/format';
import { FREQUENCIES } from '../consultations/PrescriptionEditor';

/** A4/A5-friendly prescription. Uses @media print to hide the toolbar and page chrome. */
export default function PrescriptionPrintPage() {
  const id = Number(useParams().id);
  const { data: rx, error, isLoading } = useQuery({ queryKey: keys.prescriptions.print(id), queryFn: () => prescriptionsApi.print(id) });

  if (isLoading) return <Loading />;
  if (error || !rx) return <Box sx={{ p: 3 }}><ErrorAlert error={error} /></Box>;

  const v = rx.vitals;
  const vitalsLine = v && [
    v.bpSystolic != null && `BP ${v.bpSystolic}/${v.bpDiastolic ?? '?'} mmHg`,
    v.pulseBpm != null && `Pulse ${v.pulseBpm}/min`,
    v.temperatureC != null && `Temp ${v.temperatureC} °C`,
    v.spo2Percent != null && `SpO₂ ${v.spo2Percent}%`,
    v.weightKg != null && `Wt ${v.weightKg} kg`,
  ].filter(Boolean).join(' · ');

  return (
    <Box sx={{ bgcolor: '#eef2f3', minHeight: '100vh', py: 3, '@media print': { bgcolor: '#fff', py: 0 } }}>
      <GlobalStyles styles={{ '@page': { size: 'A4', margin: '14mm' } }} />
      <Stack direction="row" spacing={1} justifyContent="center" sx={{ mb: 2, '@media print': { display: 'none' } }}>
        <Button variant="contained" startIcon={<PrintIcon />} onClick={() => window.print()}>Print</Button>
        <Button onClick={() => window.close()}>Close</Button>
      </Stack>
      {rx.status === 'Draft' && (
        <Typography align="center" color="warning.dark" sx={{ mb: 1, '@media print': { display: 'none' } }}>
          Draft — finalise the prescription before giving it to the patient.
        </Typography>
      )}

      <Box sx={{
        width: '100%', maxWidth: '210mm', mx: 'auto', bgcolor: '#fff', p: '14mm', color: '#111', fontSize: 14,
        boxShadow: '0 4px 24px rgba(0,0,0,0.08)', '@media print': { boxShadow: 'none', p: 0, maxWidth: 'none' },
      }}>
        <Stack direction="row" justifyContent="space-between" alignItems="flex-start" sx={{ borderBottom: '3px solid #0f766e', pb: 1.5 }}>
          <Box>
            <Typography variant="h5" sx={{ color: '#0f766e', fontWeight: 800 }}>{rx.hospital.name}</Typography>
            <Typography variant="body2">{rx.hospital.addressLine}</Typography>
            <Typography variant="body2">Phone: {rx.hospital.phone}</Typography>
          </Box>
          <Box sx={{ textAlign: 'right' }}>
            <Typography fontWeight={700}>{rx.doctor.name}</Typography>
            <Typography variant="body2">{rx.doctor.qualification}</Typography>
            <Typography variant="body2">{rx.doctor.department}</Typography>
            <Typography variant="body2">Reg. No: {rx.doctor.registrationNumber}</Typography>
          </Box>
        </Stack>

        <Box sx={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: 1, py: 1.5, borderBottom: '1px solid #ccc' }}>
          <Typography variant="body2"><strong>Patient:</strong> {rx.patient.name}</Typography>
          <Typography variant="body2"><strong>Age/Sex:</strong> {rx.patient.age ?? '?'} y / {rx.patient.gender}</Typography>
          <Typography variant="body2"><strong>Date:</strong> {formatDate(rx.date)}</Typography>
          <Typography variant="body2"><strong>Patient ID:</strong> {rx.patient.mrn}</Typography>
          <Typography variant="body2"><strong>Phone:</strong> {rx.patient.phone}</Typography>
          <Typography variant="body2"><strong>Rx No:</strong> {rx.prescriptionNumber}</Typography>
        </Box>

        <Box sx={{ py: 1.5 }}>
          {vitalsLine && <Typography variant="body2"><strong>Vitals:</strong> {vitalsLine}</Typography>}
          {rx.chiefComplaint && <Typography variant="body2"><strong>Complaint:</strong> {rx.chiefComplaint}</Typography>}
          {rx.diagnosis && <Typography variant="body2"><strong>Diagnosis:</strong> {rx.diagnosis}</Typography>}
        </Box>

        <Typography sx={{ fontSize: 28, fontWeight: 700, fontFamily: 'serif', color: '#0f766e' }}>℞</Typography>
        <Table size="small" sx={{ '& td, & th': { borderColor: '#ccc', px: 1 } }}>
          <TableHead>
            <TableRow>
              <TableCell>#</TableCell><TableCell>Medicine</TableCell><TableCell>Dosage</TableCell>
              <TableCell>Frequency</TableCell><TableCell>Duration</TableCell><TableCell>Instructions</TableCell>
            </TableRow>
          </TableHead>
          <TableBody>
            {rx.items.map((item, i) => (
              <TableRow key={i}>
                <TableCell>{i + 1}</TableCell>
                <TableCell><strong>{item.medicineName}</strong>{item.route ? <><br /><small>{item.route}</small></> : null}</TableCell>
                <TableCell>{item.dosage}</TableCell>
                <TableCell>
                  {item.frequencyCode === 'CUSTOM' ? item.frequencyText : FREQUENCIES.find((f) => f.code === item.frequencyCode)?.label.split(' · ')[1]}
                  {item.frequencyCode !== 'CUSTOM' && item.frequencyText ? ` (${item.frequencyText})` : ''}
                </TableCell>
                <TableCell>{item.durationValue ? `${item.durationValue} ${item.durationUnit?.toLowerCase()}` : '—'}</TableCell>
                <TableCell>{item.instructions ?? ''}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>

        {rx.generalInstructions && (
          <Box sx={{ mt: 2 }}>
            <Typography variant="body2" fontWeight={700}>Advice</Typography>
            <Typography variant="body2" sx={{ whiteSpace: 'pre-wrap' }}>{rx.generalInstructions}</Typography>
          </Box>
        )}
        {rx.followUpDate && <Typography variant="body2" sx={{ mt: 1.5 }}><strong>Follow-up on:</strong> {formatDate(rx.followUpDate)}</Typography>}

        <Stack direction="row" justifyContent="flex-end" sx={{ mt: 8 }}>
          <Box sx={{ textAlign: 'center', borderTop: '1px solid #333', pt: 0.5, minWidth: 220 }}>
            <Typography variant="body2">{rx.doctor.name}</Typography>
            <Typography variant="caption">Signature</Typography>
          </Box>
        </Stack>
      </Box>
    </Box>
  );
}

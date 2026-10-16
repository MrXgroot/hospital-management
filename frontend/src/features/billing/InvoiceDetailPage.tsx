import { useState } from 'react';
import { Link as RouterLink, useParams } from 'react-router';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Alert, Box, Button, Dialog, DialogActions, DialogContent, DialogTitle, Stack, Table, TableBody, TableCell, TableHead,
  TableRow, TextField, ToggleButton, ToggleButtonGroup, Typography,
} from '@mui/material';
import { invoicesApi } from '../../api/endpoints';
import { keys } from '../../api/queryKeys';
import { errorMessage } from '../../api/errors';
import { useAuth } from '../../auth/AuthProvider';
import { ErrorAlert, LabelValue, Loading, PageHeader, ReasonDialog, Section, StatusChip } from '../../components/common';
import { useNotify } from '../../components/Notifications';
import { formatDate, formatDateTime, formatMoney } from '../../utils/format';
import type { PaymentMethod } from '../../types/api';

export default function InvoiceDetailPage() {
  const id = Number(useParams().id);
  const { hasRole } = useAuth();
  const queryClient = useQueryClient();
  const notify = useNotify();
  const [payOpen, setPayOpen] = useState(false);
  const [cancelOpen, setCancelOpen] = useState(false);
  const [method, setMethod] = useState<PaymentMethod>('Cash');
  const [reference, setReference] = useState('');

  const { data: invoice, error, isLoading } = useQuery({ queryKey: keys.invoices.detail(id), queryFn: () => invoicesApi.get(id) });

  const onDone = (message: string) => {
    queryClient.invalidateQueries({ queryKey: keys.invoices.all });
    queryClient.invalidateQueries({ queryKey: keys.dashboard });
    notify(message);
  };
  const markPaid = useMutation({
    mutationFn: () => invoicesApi.markPaid(id, method, reference.trim() || null),
    onSuccess: () => {
      setPayOpen(false);
      setReference('');
      onDone('Payment recorded · invoice marked paid');
    },
    onError: (e) => notify(errorMessage(e), 'error'),
  });
  const cancel = useMutation({
    mutationFn: (reason: string) => invoicesApi.cancel(id, reason),
    onSuccess: () => onDone('Invoice cancelled'),
    onError: (e) => notify(errorMessage(e), 'error'),
  });

  if (isLoading) return <Loading />;
  if (error || !invoice) return <ErrorAlert error={error} />;

  return (
    <>
      <PageHeader
        title={`Invoice ${invoice.invoiceNumber}`}
        subtitle={<StatusChip status={invoice.status} />}
        actions={
          <>
            {invoice.status === 'Unpaid' && hasRole('Admin') && <Button color="error" onClick={() => setCancelOpen(true)}>Cancel invoice</Button>}
            {invoice.status === 'Unpaid' && <Button variant="contained" onClick={() => setPayOpen(true)}>Mark as paid</Button>}
          </>
        }
      />
      <Stack spacing={2} sx={{ maxWidth: 900 }}>
        {invoice.cancelReason && <Alert severity="info">Cancelled: {invoice.cancelReason}</Alert>}
        <Section>
          <Box sx={{ display: 'grid', gap: 2, gridTemplateColumns: { xs: '1fr', sm: 'repeat(3, 1fr)' } }}>
            <LabelValue label="Patient" value={<RouterLink to={`/patients/${invoice.patientId}`}>{invoice.patientName} · {invoice.patientMrn}</RouterLink>} />
            <LabelValue label="Doctor" value={invoice.doctorName} />
            <LabelValue label="Invoice date" value={formatDate(invoice.invoiceDate)} />
          </Box>
        </Section>
        <Section title="Items">
          <Table size="small">
            <TableHead>
              <TableRow><TableCell>Description</TableCell><TableCell align="right">Qty</TableCell><TableCell align="right">Rate</TableCell><TableCell align="right">Amount</TableCell></TableRow>
            </TableHead>
            <TableBody>
              {invoice.items.map((item) => (
                <TableRow key={item.id}>
                  <TableCell>{item.description}</TableCell>
                  <TableCell align="right">{item.quantity}</TableCell>
                  <TableCell align="right">{formatMoney(item.unitPrice)}</TableCell>
                  <TableCell align="right">{formatMoney(item.lineTotal)}</TableCell>
                </TableRow>
              ))}
              <TableRow><TableCell colSpan={3} align="right"><strong>Total</strong></TableCell><TableCell align="right"><strong>{formatMoney(invoice.totalAmount)}</strong></TableCell></TableRow>
              <TableRow><TableCell colSpan={3} align="right">Paid</TableCell><TableCell align="right">{formatMoney(invoice.paidAmount)}</TableCell></TableRow>
              <TableRow><TableCell colSpan={3} align="right">Balance due</TableCell><TableCell align="right">{formatMoney(invoice.balanceDue)}</TableCell></TableRow>
            </TableBody>
          </Table>
        </Section>
        {invoice.payments.length > 0 && (
          <Section title="Payments">
            {invoice.payments.map((p) => (
              <Typography key={p.id} variant="body2">
                {formatMoney(p.amount)} · {p.method}{p.referenceNo ? ` (${p.referenceNo})` : ''} · {formatDateTime(p.paidAt)} · received by {p.receivedByName ?? '—'}
              </Typography>
            ))}
          </Section>
        )}
      </Stack>

      <Dialog open={payOpen} onClose={() => setPayOpen(false)} maxWidth="xs" fullWidth>
        <DialogTitle>Mark as paid · {formatMoney(invoice.balanceDue)}</DialogTitle>
        <DialogContent>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Demo payment: record how the patient paid. No money is actually charged.
          </Typography>
          <ToggleButtonGroup exclusive fullWidth size="small" value={method} onChange={(_, v: PaymentMethod | null) => v && setMethod(v)} aria-label="Payment method">
            {(['Cash', 'Card', 'UPI', 'Other'] as PaymentMethod[]).map((m) => <ToggleButton key={m} value={m}>{m}</ToggleButton>)}
          </ToggleButtonGroup>
          {method !== 'Cash' && (
            <TextField label="Reference no. (optional)" value={reference} onChange={(e) => setReference(e.target.value)} sx={{ mt: 2 }} slotProps={{ htmlInput: { maxLength: 60 } }} />
          )}
        </DialogContent>
        <DialogActions>
          <Button onClick={() => setPayOpen(false)}>Back</Button>
          <Button variant="contained" disabled={markPaid.isPending} onClick={() => markPaid.mutate()}>Confirm payment</Button>
        </DialogActions>
      </Dialog>
      <ReasonDialog
        open={cancelOpen}
        title="Cancel invoice"
        label="Reason"
        danger
        confirmLabel="Cancel invoice"
        onClose={() => setCancelOpen(false)}
        onConfirm={(reason) => {
          setCancelOpen(false);
          cancel.mutate(reason);
        }}
      />
    </>
  );
}

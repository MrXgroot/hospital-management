import { useState } from 'react';
import { useNavigate } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import {
  MenuItem, Paper, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, Typography,
} from '@mui/material';
import { invoicesApi } from '../../api/endpoints';
import { keys } from '../../api/queryKeys';
import { EmptyState, ErrorAlert, Loading, PageHeader, StatusChip } from '../../components/common';
import { formatDate, formatMoney, todayIso } from '../../utils/format';
import type { InvoiceStatus } from '../../types/api';

export default function InvoicesPage() {
  const navigate = useNavigate();
  const [status, setStatus] = useState<InvoiceStatus | ''>('Unpaid');
  const [date, setDate] = useState(todayIso());
  const filters = { status: status || undefined, date: date || undefined };
  const { data, error, isLoading } = useQuery({ queryKey: keys.invoices.list(filters), queryFn: () => invoicesApi.list(filters) });

  const total = data?.items.reduce((sum, i) => sum + (i.status === 'Cancelled' ? 0 : i.totalAmount), 0) ?? 0;

  return (
    <>
      <PageHeader title="Billing" subtitle="Consultation invoices are created automatically when a patient checks in." />
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} sx={{ mb: 2 }} alignItems={{ sm: 'center' }}>
        <TextField select label="Status" value={status} onChange={(e) => setStatus(e.target.value as InvoiceStatus | '')} sx={{ maxWidth: { sm: 180 } }}>
          <MenuItem value="">All</MenuItem>
          <MenuItem value="Unpaid">Unpaid</MenuItem>
          <MenuItem value="Paid">Paid</MenuItem>
          <MenuItem value="Cancelled">Cancelled</MenuItem>
        </TextField>
        <TextField label="Invoice date" type="date" value={date} onChange={(e) => setDate(e.target.value)} sx={{ maxWidth: { sm: 200 } }}
          slotProps={{ inputLabel: { shrink: true } }} helperText="Clear to show all dates" />
        {data && <Typography variant="body2" color="text.secondary">{data.items.length} invoice(s) · {formatMoney(total)}</Typography>}
      </Stack>
      {error && <ErrorAlert error={error} />}
      {isLoading ? <Loading /> : data && (
        <Paper>
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Invoice</TableCell><TableCell>Date</TableCell><TableCell>Patient</TableCell>
                  <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>Doctor</TableCell>
                  <TableCell align="right">Total</TableCell><TableCell align="right">Balance</TableCell><TableCell>Status</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {data.items.map((i) => (
                  <TableRow key={i.id} hover sx={{ cursor: 'pointer' }} onClick={() => navigate(`/billing/invoices/${i.id}`)}>
                    <TableCell sx={{ fontFamily: 'monospace' }}>{i.invoiceNumber}</TableCell>
                    <TableCell>{formatDate(i.invoiceDate)}</TableCell>
                    <TableCell>
                      <Typography variant="body2" fontWeight={600}>{i.patientName}</Typography>
                      <Typography variant="caption" color="text.secondary">{i.patientMrn}</Typography>
                    </TableCell>
                    <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>{i.doctorName ?? '—'}</TableCell>
                    <TableCell align="right">{formatMoney(i.totalAmount)}</TableCell>
                    <TableCell align="right">{formatMoney(i.balanceDue)}</TableCell>
                    <TableCell><StatusChip status={i.status} /></TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
          {data.items.length === 0 && <EmptyState message="No invoices match these filters." />}
        </Paper>
      )}
    </>
  );
}

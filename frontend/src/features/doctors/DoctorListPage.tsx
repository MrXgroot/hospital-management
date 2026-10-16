import { useState } from 'react';
import { Link as RouterLink, useNavigate } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import {
  Button, MenuItem, Paper, Stack, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, Typography,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import { doctorsApi, masterApi } from '../../api/endpoints';
import { keys } from '../../api/queryKeys';
import { useAuth } from '../../auth/AuthProvider';
import { EmptyState, ErrorAlert, Loading, PageHeader, StatusChip } from '../../components/common';
import { formatMoney } from '../../utils/format';

export default function DoctorListPage() {
  const { hasRole } = useAuth();
  const navigate = useNavigate();
  const [search, setSearch] = useState('');
  const [departmentId, setDepartmentId] = useState<number | ''>('');
  const [status, setStatus] = useState<'all' | 'active' | 'inactive'>('active');

  const filters = {
    search: search.trim() || undefined,
    departmentId: departmentId || undefined,
    isActive: status === 'all' ? undefined : status === 'active',
  };
  const departments = useQuery({ queryKey: keys.departments, queryFn: () => masterApi.departments() });
  const { data, error, isLoading } = useQuery({ queryKey: keys.doctors.list(filters), queryFn: () => doctorsApi.list(filters) });

  return (
    <>
      <PageHeader
        title="Doctors"
        actions={hasRole('Admin') && <Button variant="contained" startIcon={<AddIcon />} component={RouterLink} to="/doctors/new">Add doctor</Button>}
      />
      <Stack direction={{ xs: 'column', sm: 'row' }} spacing={1.5} sx={{ mb: 2 }}>
        <TextField label="Search name or code" value={search} onChange={(e) => setSearch(e.target.value)} sx={{ maxWidth: { sm: 280 } }} />
        <TextField select label="Department" value={departmentId} onChange={(e) => setDepartmentId(e.target.value === '' ? '' : Number(e.target.value))} sx={{ maxWidth: { sm: 240 } }}>
          <MenuItem value="">All departments</MenuItem>
          {(departments.data ?? []).map((d) => <MenuItem key={d.id} value={d.id}>{d.name}</MenuItem>)}
        </TextField>
        <TextField select label="Status" value={status} onChange={(e) => setStatus(e.target.value as typeof status)} sx={{ maxWidth: { sm: 160 } }}>
          <MenuItem value="active">Active</MenuItem>
          <MenuItem value="inactive">Inactive</MenuItem>
          <MenuItem value="all">All</MenuItem>
        </TextField>
      </Stack>
      {error && <ErrorAlert error={error} />}
      {isLoading ? <Loading /> : data && (
        <Paper>
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Code</TableCell><TableCell>Name</TableCell><TableCell>Department</TableCell>
                  <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>Specialization</TableCell>
                  <TableCell align="right">Fee</TableCell><TableCell>Status</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {data.map((d) => (
                  <TableRow key={d.id} hover sx={{ cursor: 'pointer' }} onClick={() => navigate(`/doctors/${d.id}`)}>
                    <TableCell sx={{ fontFamily: 'monospace' }}>{d.doctorCode}</TableCell>
                    <TableCell><Typography variant="body2" fontWeight={600}>{d.fullName}</Typography></TableCell>
                    <TableCell>{d.departmentName}</TableCell>
                    <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>{d.primarySpecialization ?? '—'}</TableCell>
                    <TableCell align="right">{formatMoney(d.consultationFee)}</TableCell>
                    <TableCell><StatusChip status={d.isActive ? 'Active' : 'Inactive'} /></TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
          {data.length === 0 && <EmptyState message="No doctors match these filters." />}
        </Paper>
      )}
    </>
  );
}

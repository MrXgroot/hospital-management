import { useEffect, useState } from 'react';
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { Link as RouterLink, useNavigate, useSearchParams } from 'react-router';
import {
  Button, InputAdornment, Paper, Table, TableBody, TableCell, TableContainer, TableHead, TablePagination, TableRow,
  TextField, Typography,
} from '@mui/material';
import SearchIcon from '@mui/icons-material/Search';
import PersonAddIcon from '@mui/icons-material/PersonAddAlt1';
import { patientsApi } from '../../api/endpoints';
import { keys } from '../../api/queryKeys';
import { useAuth } from '../../auth/AuthProvider';
import { EmptyState, ErrorAlert, Loading, PageHeader } from '../../components/common';
import { formatDate } from '../../utils/format';

export default function PatientListPage() {
  const { hasRole } = useAuth();
  const navigate = useNavigate();
  const [params, setParams] = useSearchParams();
  const [input, setInput] = useState(params.get('q') ?? '');
  const [page, setPage] = useState(0);
  const [pageSize, setPageSize] = useState(20);
  const search = params.get('q') ?? '';

  // Debounce typing into the URL so search is shareable and survives back-navigation.
  useEffect(() => {
    const t = setTimeout(() => {
      if (input.trim() !== search) {
        setParams(input.trim() ? { q: input.trim() } : {}, { replace: true });
        setPage(0);
      }
    }, 300);
    return () => clearTimeout(t);
  }, [input, search, setParams]);

  const { data, error, isLoading, refetch } = useQuery({
    queryKey: keys.patients.search(search, page, pageSize),
    queryFn: () => patientsApi.search(search, page + 1, pageSize),
    placeholderData: keepPreviousData,
  });

  return (
    <>
      <PageHeader
        title="Patients"
        subtitle="Search by patient ID (MRN), phone number or name"
        actions={hasRole('Admin', 'Receptionist') && (
          <Button variant="contained" startIcon={<PersonAddIcon />} component={RouterLink} to="/patients/new">Register patient</Button>
        )}
      />
      <TextField
        placeholder="e.g. P2026000012, 98765, or Ramesh"
        value={input}
        onChange={(e) => setInput(e.target.value)}
        autoFocus
        sx={{ mb: 2, maxWidth: 520 }}
        slotProps={{ input: { startAdornment: <InputAdornment position="start"><SearchIcon /></InputAdornment> }, htmlInput: { 'aria-label': 'Search patients' } }}
      />
      {error && <ErrorAlert error={error} onRetry={() => refetch()} />}
      {isLoading ? <Loading /> : data && (
        <Paper>
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Patient ID</TableCell>
                  <TableCell>Name</TableCell>
                  <TableCell>Age / Gender</TableCell>
                  <TableCell>Phone</TableCell>
                  <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>City</TableCell>
                  <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>Registered</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {data.items.map((p) => (
                  <TableRow key={p.id} hover sx={{ cursor: 'pointer' }} onClick={() => navigate(`/patients/${p.id}`)}>
                    <TableCell><Typography variant="body2" fontFamily="monospace">{p.mrn}</Typography></TableCell>
                    <TableCell><Typography variant="body2" fontWeight={600}>{p.fullName}</Typography></TableCell>
                    <TableCell>{p.age ?? '?'} / {p.gender}</TableCell>
                    <TableCell>{p.phone}</TableCell>
                    <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>{p.city ?? '—'}</TableCell>
                    <TableCell sx={{ display: { xs: 'none', md: 'table-cell' } }}>{formatDate(p.createdAt)}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
          {data.items.length === 0 && <EmptyState message={search ? `No patients match "${search}".` : 'No patients registered yet.'} />}
          <TablePagination
            component="div"
            count={data.totalCount}
            page={page}
            onPageChange={(_, p) => setPage(p)}
            rowsPerPage={pageSize}
            rowsPerPageOptions={[20, 50]}
            onRowsPerPageChange={(e) => {
              setPageSize(Number(e.target.value));
              setPage(0);
            }}
          />
        </Paper>
      )}
    </>
  );
}

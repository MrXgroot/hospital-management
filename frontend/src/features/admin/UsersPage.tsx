import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, MenuItem, Paper, Stack, Switch, Table,
  TableBody, TableCell, TableContainer, TableHead, TableRow, TextField,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import { doctorsApi, usersApi } from '../../api/endpoints';
import { keys } from '../../api/queryKeys';
import { errorMessage, getProblem } from '../../api/errors';
import { ErrorAlert, Loading, PageHeader, StatusChip } from '../../components/common';
import { useNotify } from '../../components/Notifications';
import { formatDateTime } from '../../utils/format';
import type { Role, UserDto } from '../../types/api';

const ROLES: Role[] = ['Admin', 'Receptionist', 'Doctor', 'Nurse'];

interface Draft {
  id: number | null;
  username: string;
  fullName: string;
  email: string;
  role: Role;
  doctorId: number | '';
  isActive: boolean;
  password: string;
}

const newDraft = (): Draft => ({ id: null, username: '', fullName: '', email: '', role: 'Receptionist', doctorId: '', isActive: true, password: '' });

export default function UsersPage() {
  const queryClient = useQueryClient();
  const notify = useNotify();
  const [draft, setDraft] = useState<Draft | null>(null);
  const [errors, setErrors] = useState<Record<string, string[]>>({});
  const { data, error, isLoading } = useQuery({ queryKey: keys.users, queryFn: usersApi.list });
  const doctors = useQuery({ queryKey: keys.doctors.list({}), queryFn: () => doctorsApi.list() });

  const save = useMutation({
    mutationFn: (d: Draft) => {
      const doctorId = d.role === 'Doctor' && d.doctorId ? d.doctorId : null;
      return d.id
        ? usersApi.update(d.id, { fullName: d.fullName, email: d.email || null, role: d.role, doctorId, isActive: d.isActive, newPassword: d.password || null })
        : usersApi.create({ username: d.username, password: d.password, fullName: d.fullName, email: d.email || null, role: d.role, doctorId });
    },
    onSuccess: () => {
      setDraft(null);
      queryClient.invalidateQueries({ queryKey: keys.users });
      notify('User saved');
    },
    onError: (e) => {
      const p = getProblem(e);
      setErrors(p?.errors ?? {});
      if (!p?.errors) notify(errorMessage(e), 'error');
    },
  });

  const edit = (u: UserDto) => {
    setErrors({});
    setDraft({ id: u.id, username: u.username, fullName: u.fullName, email: u.email ?? '', role: u.role, doctorId: u.doctorId ?? '', isActive: u.isActive, password: '' });
  };
  const field = (name: keyof Draft) => ({ error: !!errors[name], helperText: errors[name]?.[0] });

  return (
    <>
      <PageHeader title="Users" actions={<Button variant="contained" startIcon={<AddIcon />} onClick={() => { setErrors({}); setDraft(newDraft()); }}>Add user</Button>} />
      {error && <ErrorAlert error={error} />}
      {isLoading ? <Loading /> : (
        <Paper>
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow><TableCell>Username</TableCell><TableCell>Name</TableCell><TableCell>Role</TableCell><TableCell>Last login</TableCell><TableCell>Status</TableCell><TableCell /></TableRow>
              </TableHead>
              <TableBody>
                {data?.map((u) => (
                  <TableRow key={u.id} hover>
                    <TableCell sx={{ fontFamily: 'monospace' }}>{u.username}</TableCell>
                    <TableCell>{u.fullName}</TableCell>
                    <TableCell>{u.role}</TableCell>
                    <TableCell>{formatDateTime(u.lastLoginAt)}</TableCell>
                    <TableCell><StatusChip status={u.isActive ? 'Active' : 'Inactive'} /></TableCell>
                    <TableCell align="right"><Button size="small" onClick={() => edit(u)}>Edit</Button></TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </Paper>
      )}

      <Dialog open={!!draft} onClose={() => setDraft(null)} maxWidth="sm" fullWidth>
        {draft && (
          <>
            <DialogTitle>{draft.id ? `Edit ${draft.username}` : 'Add user'}</DialogTitle>
            <DialogContent>
              <Stack spacing={2} sx={{ mt: 1 }}>
                {!draft.id && <TextField label="Username" value={draft.username} onChange={(e) => setDraft({ ...draft, username: e.target.value })} {...field('username')} />}
                <TextField label="Full name" value={draft.fullName} onChange={(e) => setDraft({ ...draft, fullName: e.target.value })} {...field('fullName')} />
                <TextField label="Email" value={draft.email} onChange={(e) => setDraft({ ...draft, email: e.target.value })} {...field('email')} />
                <TextField select label="Role" value={draft.role} onChange={(e) => setDraft({ ...draft, role: e.target.value as Role })}>
                  {ROLES.map((r) => <MenuItem key={r} value={r}>{r}</MenuItem>)}
                </TextField>
                {draft.role === 'Doctor' && (
                  <TextField select label="Linked doctor profile" value={draft.doctorId} onChange={(e) => setDraft({ ...draft, doctorId: Number(e.target.value) })} {...field('doctorId')}>
                    {(doctors.data ?? []).map((d) => <MenuItem key={d.id} value={d.id}>{d.fullName} ({d.doctorCode})</MenuItem>)}
                  </TextField>
                )}
                <TextField
                  label={draft.id ? 'New password (leave blank to keep)' : 'Password'}
                  type="password"
                  value={draft.password}
                  onChange={(e) => setDraft({ ...draft, password: e.target.value })}
                  {...field(draft.id ? ('newPassword' as keyof Draft) : 'password')}
                  autoComplete="new-password"
                />
                {draft.id && <FormControlLabel control={<Switch checked={draft.isActive} onChange={(_, c) => setDraft({ ...draft, isActive: c })} />} label="Active" />}
              </Stack>
            </DialogContent>
            <DialogActions>
              <Button onClick={() => setDraft(null)}>Cancel</Button>
              <Button variant="contained" disabled={save.isPending} onClick={() => save.mutate(draft)}>Save</Button>
            </DialogActions>
          </>
        )}
      </Dialog>
    </>
  );
}

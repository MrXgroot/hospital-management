import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  Button, Dialog, DialogActions, DialogContent, DialogTitle, FormControlLabel, Paper, Stack, Switch, Tab, Table,
  TableBody, TableCell, TableContainer, TableHead, TableRow, Tabs, TextField,
} from '@mui/material';
import AddIcon from '@mui/icons-material/Add';
import { masterApi } from '../../api/endpoints';
import { keys } from '../../api/queryKeys';
import { errorMessage } from '../../api/errors';
import { Loading, PageHeader, StatusChip } from '../../components/common';
import { useNotify } from '../../components/Notifications';

/** Generic editor for the three simple master lists. */
interface Column { key: string; label: string; required?: boolean }

const TABS: { label: string; columns: Column[] }[] = [
  { label: 'Departments', columns: [{ key: 'name', label: 'Name', required: true }, { key: 'description', label: 'Description' }] },
  { label: 'Specializations', columns: [{ key: 'name', label: 'Name', required: true }] },
  {
    label: 'Medicines',
    columns: [
      { key: 'name', label: 'Name', required: true }, { key: 'genericName', label: 'Generic name' },
      { key: 'form', label: 'Form' }, { key: 'strength', label: 'Strength' },
    ],
  },
];

type Row = { id: number; isActive: boolean } & Record<string, string | number | boolean | null>;

export default function MastersPage() {
  const [tab, setTab] = useState(0);
  const queryClient = useQueryClient();
  const notify = useNotify();
  const [draft, setDraft] = useState<Row | null>(null);

  const departments = useQuery({ queryKey: keys.departments, queryFn: () => masterApi.departments(), enabled: tab === 0 });
  const specializations = useQuery({ queryKey: keys.specializations, queryFn: () => masterApi.specializations(), enabled: tab === 1 });
  const medicines = useQuery({ queryKey: keys.medicines('*all'), queryFn: () => masterApi.medicines(), enabled: tab === 2 });
  const current = [departments, specializations, medicines][tab];
  const rows = (current.data ?? []) as unknown as Row[];
  const config = TABS[tab];

  const save = useMutation({
    mutationFn: async (row: Row) => {
      const id = row.id || null;
      const s = (k: string) => (row[k] ? String(row[k]).trim() : null);
      if (tab === 0) await masterApi.saveDepartment(id, { name: s('name') ?? '', description: s('description'), isActive: row.isActive });
      else if (tab === 1) await masterApi.saveSpecialization(id, { name: s('name') ?? '', isActive: row.isActive });
      else await masterApi.saveMedicine(id, { name: s('name') ?? '', genericName: s('genericName'), form: s('form'), strength: s('strength'), isActive: row.isActive });
    },
    onSuccess: () => {
      setDraft(null);
      queryClient.invalidateQueries({ queryKey: [['departments'], ['specializations'], ['medicines']][tab] });
      notify('Saved');
    },
    onError: (e) => notify(errorMessage(e), 'error'),
  });

  return (
    <>
      <PageHeader
        title="Master data"
        actions={<Button variant="contained" startIcon={<AddIcon />} onClick={() => setDraft({ id: 0, isActive: true })}>Add {config.label.toLowerCase().replace(/s$/, '')}</Button>}
      />
      <Tabs value={tab} onChange={(_, v) => setTab(v)} sx={{ mb: 2, borderBottom: 1, borderColor: 'divider' }}>
        {TABS.map((t) => <Tab key={t.label} label={t.label} />)}
      </Tabs>
      {current.isLoading ? <Loading /> : (
        <Paper>
          <TableContainer>
            <Table size="small">
              <TableHead>
                <TableRow>
                  {config.columns.map((c) => <TableCell key={c.key}>{c.label}</TableCell>)}
                  <TableCell>Status</TableCell><TableCell />
                </TableRow>
              </TableHead>
              <TableBody>
                {rows.map((r) => (
                  <TableRow key={r.id} hover>
                    {config.columns.map((c) => <TableCell key={c.key}>{(r[c.key] as string) ?? '—'}</TableCell>)}
                    <TableCell><StatusChip status={r.isActive ? 'Active' : 'Inactive'} /></TableCell>
                    <TableCell align="right"><Button size="small" onClick={() => setDraft({ ...r })}>Edit</Button></TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>
        </Paper>
      )}

      <Dialog open={!!draft} onClose={() => setDraft(null)} maxWidth="xs" fullWidth>
        {draft && (
          <>
            <DialogTitle>{draft.id ? 'Edit' : 'Add'} {config.label.toLowerCase().replace(/s$/, '')}</DialogTitle>
            <DialogContent>
              <Stack spacing={2} sx={{ mt: 1 }}>
                {config.columns.map((c) => (
                  <TextField key={c.key} label={c.label} required={c.required} value={(draft[c.key] as string) ?? ''}
                    onChange={(e) => setDraft({ ...draft, [c.key]: e.target.value })} />
                ))}
                <FormControlLabel control={<Switch checked={draft.isActive} onChange={(_, v) => setDraft({ ...draft, isActive: v })} />} label="Active" />
              </Stack>
            </DialogContent>
            <DialogActions>
              <Button onClick={() => setDraft(null)}>Cancel</Button>
              <Button variant="contained" disabled={!String(draft.name ?? '').trim() || save.isPending} onClick={() => save.mutate(draft)}>Save</Button>
            </DialogActions>
          </>
        )}
      </Dialog>
    </>
  );
}

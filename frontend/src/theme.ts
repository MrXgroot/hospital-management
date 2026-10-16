import { createTheme } from '@mui/material/styles';

export const theme = createTheme({
  palette: {
    mode: 'light',
    primary: { main: '#0f766e', light: '#14b8a6', dark: '#115e59', contrastText: '#ffffff' },
    secondary: { main: '#4f46e5' },
    background: { default: '#f4f7f8', paper: '#ffffff' },
    text: { primary: '#0f1f24', secondary: '#4b5d63' },
    divider: '#e2e8ea',
  },
  shape: { borderRadius: 10 },
  typography: {
    fontFamily: '"Inter", "Segoe UI", system-ui, -apple-system, Roboto, "Helvetica Neue", Arial, sans-serif',
    h4: { fontWeight: 700, letterSpacing: '-0.02em' },
    h5: { fontWeight: 700, letterSpacing: '-0.01em' },
    h6: { fontWeight: 650 },
    subtitle2: { fontWeight: 600 },
    button: { textTransform: 'none', fontWeight: 600 },
  },
  components: {
    MuiPaper: { defaultProps: { elevation: 0 }, styleOverrides: { root: { border: '1px solid #e2e8ea' } } },
    MuiAppBar: { styleOverrides: { root: { border: 'none' } } },
    MuiDrawer: { styleOverrides: { paper: { border: 'none' } } },
    MuiButton: { defaultProps: { disableElevation: true } },
    MuiTextField: { defaultProps: { size: 'small', fullWidth: true } },
    MuiSelect: { defaultProps: { size: 'small' } },
    MuiTableCell: { styleOverrides: { head: { fontWeight: 600, color: '#4b5d63', backgroundColor: '#f8fafb' } } },
    MuiChip: { styleOverrides: { root: { fontWeight: 600 } } },
  },
});

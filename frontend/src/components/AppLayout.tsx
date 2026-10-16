import { useState } from 'react';
import { NavLink, Outlet, useNavigate } from 'react-router';
import {
  AppBar, Avatar, Box, Divider, Drawer, IconButton, List, ListItemButton, ListItemIcon, ListItemText, Menu, MenuItem,
  Toolbar, Typography, useMediaQuery, useTheme,
} from '@mui/material';
import MenuIcon from '@mui/icons-material/Menu';
import DashboardIcon from '@mui/icons-material/SpaceDashboardOutlined';
import PeopleIcon from '@mui/icons-material/PeopleAltOutlined';
import DoctorIcon from '@mui/icons-material/MedicalServicesOutlined';
import EventIcon from '@mui/icons-material/EventNoteOutlined';
import QueueIcon from '@mui/icons-material/FormatListNumberedOutlined';
import BillingIcon from '@mui/icons-material/ReceiptLongOutlined';
import UsersIcon from '@mui/icons-material/ManageAccountsOutlined';
import MastersIcon from '@mui/icons-material/TuneOutlined';
import LocalHospitalIcon from '@mui/icons-material/LocalHospital';
import { useAuth } from '../auth/AuthProvider';
import type { Role } from '../types/api';

const DRAWER_WIDTH = 248;

interface NavItem {
  to: string;
  label: string;
  icon: React.ReactNode;
  roles: Role[];
}

const ALL: Role[] = ['Admin', 'Receptionist', 'Doctor', 'Nurse'];

export const NAV_ITEMS: NavItem[] = [
  { to: '/', label: 'Dashboard', icon: <DashboardIcon />, roles: ALL },
  { to: '/queue', label: "Today's queue", icon: <QueueIcon />, roles: ALL },
  { to: '/appointments', label: 'Appointments', icon: <EventIcon />, roles: ALL },
  { to: '/patients', label: 'Patients', icon: <PeopleIcon />, roles: ALL },
  { to: '/doctors', label: 'Doctors', icon: <DoctorIcon />, roles: ALL },
  { to: '/billing', label: 'Billing', icon: <BillingIcon />, roles: ['Admin', 'Receptionist'] },
  { to: '/admin/users', label: 'Users', icon: <UsersIcon />, roles: ['Admin'] },
  { to: '/admin/masters', label: 'Master data', icon: <MastersIcon />, roles: ['Admin'] },
];

export function AppLayout() {
  const { user, logout, hasRole } = useAuth();
  const navigate = useNavigate();
  const theme = useTheme();
  const isDesktop = useMediaQuery(theme.breakpoints.up('md'));
  const [mobileOpen, setMobileOpen] = useState(false);
  const [menuAnchor, setMenuAnchor] = useState<HTMLElement | null>(null);

  const items = NAV_ITEMS.filter((item) => hasRole(...item.roles));

  const drawer = (
    <Box sx={{ height: '100%', display: 'flex', flexDirection: 'column', bgcolor: '#0c3b3a', color: '#e6f2f1' }}>
      <Toolbar sx={{ gap: 1.25 }}>
        <LocalHospitalIcon sx={{ color: '#5eead4' }} />
        <Box>
          <Typography fontWeight={700} lineHeight={1.1}>City Care</Typography>
          <Typography variant="caption" sx={{ opacity: 0.7 }}>Hospital Management</Typography>
        </Box>
      </Toolbar>
      <Divider sx={{ borderColor: 'rgba(255,255,255,0.08)' }} />
      <List sx={{ px: 1.25, py: 1.5 }} component="nav" aria-label="Main navigation">
        {items.map((item) => (
          <ListItemButton
            key={item.to}
            component={NavLink}
            to={item.to}
            end={item.to === '/'}
            onClick={() => setMobileOpen(false)}
            sx={{
              borderRadius: 2,
              mb: 0.5,
              color: 'inherit',
              '&.active': { bgcolor: 'rgba(94, 234, 212, 0.14)', color: '#ffffff', '& .MuiListItemIcon-root': { color: '#5eead4' } },
              '&:hover': { bgcolor: 'rgba(255,255,255,0.06)' },
            }}
          >
            <ListItemIcon sx={{ minWidth: 38, color: 'rgba(230,242,241,0.7)' }}>{item.icon}</ListItemIcon>
            <ListItemText primary={item.label} slotProps={{ primary: { fontSize: 14, fontWeight: 550 } }} />
          </ListItemButton>
        ))}
      </List>
    </Box>
  );

  return (
    <Box sx={{ display: 'flex', minHeight: '100vh' }}>
      <AppBar
        position="fixed"
        color="inherit"
        sx={{ width: { md: `calc(100% - ${DRAWER_WIDTH}px)` }, ml: { md: `${DRAWER_WIDTH}px` }, borderBottom: 1, borderColor: 'divider' }}
      >
        <Toolbar>
          {!isDesktop && (
            <IconButton edge="start" aria-label="Open navigation" onClick={() => setMobileOpen(true)} sx={{ mr: 1 }}>
              <MenuIcon />
            </IconButton>
          )}
          <Box sx={{ flexGrow: 1 }} />
          <Box sx={{ textAlign: 'right', mr: 1.5, display: { xs: 'none', sm: 'block' } }}>
            <Typography variant="body2" fontWeight={600}>{user?.fullName}</Typography>
            <Typography variant="caption" color="text.secondary">{user?.role}</Typography>
          </Box>
          <IconButton aria-label="Account menu" onClick={(e) => setMenuAnchor(e.currentTarget)}>
            <Avatar sx={{ width: 34, height: 34, bgcolor: 'primary.main', fontSize: 15 }}>
              {user?.fullName.replace('Dr. ', '').charAt(0)}
            </Avatar>
          </IconButton>
          <Menu anchorEl={menuAnchor} open={!!menuAnchor} onClose={() => setMenuAnchor(null)}>
            <MenuItem disabled>{user?.username}</MenuItem>
            <MenuItem
              onClick={async () => {
                setMenuAnchor(null);
                await logout();
                navigate('/login');
              }}
            >
              Sign out
            </MenuItem>
          </Menu>
        </Toolbar>
      </AppBar>

      <Box component="nav" sx={{ width: { md: DRAWER_WIDTH }, flexShrink: { md: 0 } }}>
        <Drawer
          variant={isDesktop ? 'permanent' : 'temporary'}
          open={isDesktop || mobileOpen}
          onClose={() => setMobileOpen(false)}
          ModalProps={{ keepMounted: true }}
          sx={{ '& .MuiDrawer-paper': { width: DRAWER_WIDTH, boxSizing: 'border-box' } }}
        >
          {drawer}
        </Drawer>
      </Box>

      <Box component="main" sx={{ flexGrow: 1, minWidth: 0, px: { xs: 2, md: 4 }, py: 3, mt: 8 }}>
        <Outlet />
      </Box>
    </Box>
  );
}

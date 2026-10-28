import { useState } from 'react';
import { Link, Outlet, useLocation, useNavigate } from 'react-router-dom';
import {
  BarChart3, Coins, LayoutDashboard, Network, Receipt, Settings, User, UserPlus, Users
} from 'lucide-react';
import { useAuth } from '../lib/auth';
import { useI18n } from '../lib/i18n';
import { initials } from '../lib/util';
import { Brand, LangSwitch } from './ui';

const MEMBER_NAV = [
  { to: '/dashboard', icon: LayoutDashboard, key: 'nav.dashboard' },
  { to: '/my-team', icon: Network, key: 'nav.myTeam' },
  { to: '/members/new', icon: UserPlus, key: 'nav.addMember' },
  { to: '/earnings', icon: Coins, key: 'nav.earnings' },
  { to: '/transactions', icon: Receipt, key: 'nav.transactions' },
  { to: '/members', icon: Users, key: 'nav.members' },
  { to: '/profile', icon: User, key: 'nav.profile' }
];

const ADMIN_NAV = [
  { to: '/admin', icon: BarChart3, key: 'nav.adminOverview' },
  { to: '/admin/members', icon: Users, key: 'nav.memberManagement' },
  { to: '/admin/placements', icon: Network, key: 'nav.placementManagement' },
  { to: '/admin/transactions', icon: Receipt, key: 'nav.transactionRecords' },
  { to: '/admin/settings', icon: Settings, key: 'nav.settings' }
];

const TITLES: Record<string, string> = {
  '/dashboard': 'dash.title',
  '/my-team': 'team.title',
  '/members/new': 'add.title',
  '/earnings': 'earn.title',
  '/transactions': 'txn.title',
  '/members': 'members.title',
  '/profile': 'prof.title',
  '/admin': 'admin.title',
  '/admin/members': 'am.title',
  '/admin/placements': 'ap.title',
  '/admin/transactions': 'at.title',
  '/admin/settings': 'as.title'
};

export default function Shell({ kind }: { kind: 'member' | 'admin' }) {
  const { profile, logout } = useAuth();
  const { t } = useI18n();
  const location = useLocation();
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);

  const isAdmin = kind === 'admin';
  const nav = isAdmin ? ADMIN_NAV : MEMBER_NAV;
  const titleKey = TITLES[location.pathname] || 'nav.dashboard';
  const userName = profile?.fullName ?? (isAdmin ? 'Demo Admin' : 'Demo Member');
  const roleLabel = isAdmin ? t('common.roleAdmin') : t('common.roleMember');

  const signOut = () => {
    logout();
    navigate('/login');
  };

  return (
    <div className="app">
      <aside className={`sidebar${open ? ' open' : ''}`} id="sidebar">
        <div className="sidebar-brand"><Brand dark /></div>
        <nav className="sidebar-nav">
          <div className="nav-group-label">{isAdmin ? t('nav.adminPanel') : t('nav.members')}</div>
          {nav.map((item) => {
            const Icon = item.icon;
            const active = location.pathname === item.to;
            return (
              <Link key={item.to} className={`nav-item${active ? ' active' : ''}`} to={item.to} onClick={() => setOpen(false)}>
                <Icon className="nav-ico" size={18} />
                <span>{t(item.key)}</span>
              </Link>
            );
          })}
          {isAdmin ? (
            <>
              <div className="nav-divider" />
              <Link className={`nav-item${location.pathname === '/dashboard' ? ' active' : ''}`} to="/dashboard" onClick={() => setOpen(false)}>
                <LayoutDashboard className="nav-ico" size={18} />
                <span>{t('nav.dashboard')}</span>
              </Link>
            </>
          ) : null}
        </nav>
        <div className="sidebar-foot">
          <div className="demo-banner">⚠ {t('common.demoBanner')}</div>
        </div>
      </aside>

      <div className="drawer-overlay" id="drawer-overlay" onClick={() => setOpen(false)} hidden={!open} />
      <div className="app-main">
        <header className="topbar">
          <button className="menu-btn" aria-label="Menu" onClick={() => setOpen((v) => !v)}>☰</button>
          <div className="topbar-title">Money Chain SAS · <b>{t(titleKey)}</b></div>
          <div className="topbar-right">
            <LangSwitch />
            <div className="user-chip">
              <span className="user-avatar">{initials(userName)}</span>
              <div className="user-meta">
                <div className="user-name">{userName}</div>
                <div className="user-role">{roleLabel}</div>
              </div>
            </div>
            <button className="btn btn-ghost btn-sm" onClick={signOut}>{t('common.signOut')}</button>
          </div>
        </header>
        <main className="content">
          <Outlet />
        </main>
      </div>
    </div>
  );
}

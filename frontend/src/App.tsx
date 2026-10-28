import type { ReactNode } from 'react';
import { Navigate, Route, Routes, useLocation } from 'react-router-dom';
import Shell from './components/Shell';
import { useAuth } from './lib/auth';
import Landing from './pages/Landing';
import Login from './pages/Login';
import Register from './pages/Register';
import Dashboard from './pages/Dashboard';
import Members from './pages/Members';
import AddMember from './pages/AddMember';
import MyTeam from './pages/MyTeam';
import Earnings from './pages/Earnings';
import Transactions from './pages/Transactions';
import Profile from './pages/Profile';
import AdminOverview from './pages/AdminOverview';
import AdminMembers from './pages/AdminMembers';
import AdminPlacements from './pages/AdminPlacements';
import AdminTransactions from './pages/AdminTransactions';
import AdminSettings from './pages/AdminSettings';
import NotFound from './pages/NotFound';

function RequireAuth({ admin = false, children }: { admin?: boolean; children: ReactNode }) {
  const { token, profile, loading } = useAuth();
  const location = useLocation();

  if (loading) {
    return <div className="empty-state"><div className="empty-ico">…</div>Loading…</div>;
  }
  if (!token) return <Navigate to="/login" state={{ from: location }} replace />;
  if (admin && profile?.role !== 'admin') return <Navigate to="/dashboard" replace />;
  return <>{children}</>;
}

export default function App() {
  return (
    <Routes>
      <Route path="/" element={<Landing />} />
      <Route path="/login" element={<Login />} />
      <Route path="/register" element={<Register />} />

      <Route element={<RequireAuth><Shell kind="member" /></RequireAuth>}>
        <Route path="/dashboard" element={<Dashboard />} />
        <Route path="/members" element={<Members />} />
        <Route path="/members/new" element={<AddMember />} />
        <Route path="/my-team" element={<MyTeam />} />
        <Route path="/earnings" element={<Earnings />} />
        <Route path="/transactions" element={<Transactions />} />
        <Route path="/profile" element={<Profile />} />
      </Route>

      <Route element={<RequireAuth admin><Shell kind="admin" /></RequireAuth>}>
        <Route path="/admin" element={<AdminOverview />} />
        <Route path="/admin/members" element={<AdminMembers />} />
        <Route path="/admin/placements" element={<AdminPlacements />} />
        <Route path="/admin/transactions" element={<AdminTransactions />} />
        <Route path="/admin/settings" element={<AdminSettings />} />
      </Route>

      <Route path="*" element={<NotFound />} />
    </Routes>
  );
}

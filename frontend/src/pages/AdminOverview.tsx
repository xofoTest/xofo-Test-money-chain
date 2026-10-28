import { Link } from 'react-router-dom';
import { ErrorBox, Loading, PageHead, StatCard } from '../components/ui';
import { useFetch } from '../lib/useFetch';
import type { AdminOverviewDto, AuditRowDto } from '../lib/api';
import { useI18n } from '../lib/i18n';
import { money } from '../lib/util';

function actionLabel(t: (k: string) => string, action: string): string {
  if (action === 'account_suspended') return t('am.suspend');
  if (action === 'account_active') return t('am.reactivate');
  if (action === 'reward_reversal') return t('earn.typeReversal');
  if (action === 'placement_created' || action === 'placement_corrected') return t('ap.correct');
  if (action === 'registration_approved') return t('common.approved');
  if (action === 'settings_updated') return t('nav.settings');
  if (action === 'simulation_run') return t('as.simEnable');
  return action.replace(/_/g, ' ');
}

export default function AdminOverview() {
  const { t } = useI18n();
  const { data, error, loading } = useFetch<AdminOverviewDto>('/api/admin/overview');

  if (loading) return <Loading />;
  if (error || !data) return <ErrorBox message={error || 'No data'} />;

  return (
    <>
      <PageHead
        title={t('admin.title')}
        sub={t('admin.sub')}
        actions={
          <>
            <Link className="btn btn-secondary" to="/admin/members">{t('am.title')}</Link>
            <Link className="btn btn-secondary" to="/admin/settings">{t('nav.settings')}</Link>
          </>
        }
      />

      <div className="stat-grid">
        <StatCard label={t('admin.totalMembers')} value={data.totalMembers} meta={t('common.simulated')} />
        <StatCard label={t('admin.activeMembers')} value={data.activeMembers} valueClassName="amount pos" meta={t('common.active')} />
        <StatCard label={t('admin.suspendedMembers')} value={data.suspendedMembers} valueClassName="amount neg" meta={t('common.suspended')} />
        <StatCard label={t('admin.registrationRecords')} value={data.registrationRecords} meta="₹50 ×" />
      </div>

      <div className="grid-2">
        <div className="card">
          <div className="card-head"><h3>{t('admin.placementStats')}</h3></div>
          <div className="card-pad">
            <div className="stat-grid cols-3">
              <StatCard label={t('admin.treeNodes')} value={data.treeNodes} />
              <StatCard label={t('admin.fullNodes')} value={data.fullNodes} />
              <StatCard label={t('admin.openSlots')} value={data.openSlots} />
            </div>
            <Link className="btn btn-secondary btn-sm mt-16" to="/admin/placements">{t('nav.placementManagement')}</Link>
          </div>
        </div>

        <div className="card">
          <div className="card-head"><h3>{t('admin.rewardStats')}</h3></div>
          <div className="card-pad">
            <div className="stat-grid cols-3">
              <StatCard label={t('admin.rewardsTotal')} value={money(data.rewardsTotalPaise)} valueClassName="amount pos" />
              <StatCard label={t('admin.rewardsPending')} value={money(data.rewardsPendingPaise)} valueClassName="amount" />
              <StatCard label={t('admin.rewardsSettled')} value={money(data.rewardsSettledPaise)} valueClassName="amount pos" />
            </div>
            <p className="hint mt-16">{data.simulationEnabled ? t('as.simEnable') : t('as.simDisabled')}</p>
          </div>
        </div>
      </div>

      <div className="card mt-16">
        <div className="card-head">
          <h3>{t('admin.recentActivity')}</h3>
          <Link className="btn btn-ghost btn-sm" to="/admin/placements">{t('admin.viewAll')}</Link>
        </div>
        <div className="table-wrap table-flush">
          <table className="table">
            <thead>
              <tr>
                <th>{t('common.date')}</th>
                <th>{t('common.admin')}</th>
                <th>{t('common.action')}</th>
                <th>Target</th>
                <th>{t('common.reason')}</th>
              </tr>
            </thead>
            <tbody>
              {data.recentActivity.map((e: AuditRowDto, i: number) => (
                <tr key={i}>
                  <td className="cell-muted">{e.date}</td>
                  <td className="cell-strong">{e.admin}</td>
                  <td><span className="badge badge-gray no-dot">{actionLabel(t, e.action)}</span></td>
                  <td className="mono">{e.target}</td>
                  <td className="cell-muted">{e.reason ?? '—'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    </>
  );
}

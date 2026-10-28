import { Link } from 'react-router-dom';
import { Alert, ErrorBox, Loading, PageHead, StatusBadge, StatCard } from '../components/ui';
import { useFetch } from '../lib/useFetch';
import type { DashboardDto } from '../lib/api';
import { useI18n } from '../lib/i18n';
import { money } from '../lib/util';

const ACTIVITY_ICONS: Record<string, string> = { join: '➕', placement: '🌳', reward: '💰', status: '⚙️' };

export default function Dashboard() {
  const { t } = useI18n();
  const { data, error, loading } = useFetch<DashboardDto>('/api/dashboard');

  if (loading) return <Loading />;
  if (error || !data) return <ErrorBox message={error || 'No data'} />;

  const s = data.stats as Record<string, number>;
  const regStatus = data.registrationStatus === 'approved' || data.registrationStatus === 'simulated' ? 'approved' : data.registrationStatus;

  return (
    <>
      <PageHead title={t('dash.title')} sub={`${t('dash.sub')}`} />
      <Alert tone="info">{t('dash.demoDisclaimer')}</Alert>

      <div className="card card-pad">
        <div className="flex-between"><h3>{t('dash.quickActions')}</h3></div>
        <div className="row mt-16">
          <Link className="btn btn-primary" to="/my-team">{t('dash.viewTeam')}</Link>
          <Link className="btn btn-secondary" to="/members/new">{t('dash.addMember')}</Link>
          <Link className="btn btn-secondary" to="/transactions">{t('dash.viewTransactions')}</Link>
          <Link className="btn btn-secondary" to="/profile">{t('dash.viewProfile')}</Link>
        </div>
      </div>

      <div className="stat-grid mt-16">
        <StatCard label={t('dash.memberId')} value={<span className="mono">{data.member.memberCode}</span>} meta={data.member.fullName} />
        <StatCard label={t('dash.accountStatus')} value={<StatusBadge status={data.member.accountStatus} />} meta={t('dash.registrationStatus')} />
        <StatCard label={t('dash.directSponsored')} value={s.directSponsored} meta="—" />
        <StatCard label={t('dash.directPlacement')} value={<>{s.directChildren} <span className="unit">/ 3</span></>} meta={t('team.maxNote')} />
        <StatCard label={t('dash.totalTeam')} value={s.totalTeam} meta={t('common.simulated')} />
        <StatCard label={t('dash.earningsBalance')} value={money(s.balancePaise)} valueClassName="amount pos" meta={t('common.simulated')} />
        <StatCard label={t('dash.pendingRewards')} value={money(s.pendingPaise)} valueClassName="amount" meta={t('common.pending')} />
        <StatCard label={t('dash.settledEntries')} value={money(s.settledPaise)} valueClassName="amount pos" meta={t('common.settled')} />
      </div>

      <div className="grid-2 mt-16">
        <div className="card">
          <div className="card-head"><h3>{t('dash.regAndPlacement')}</h3></div>
          <div className="card-pad">
            <div className="detail-list">
              <div className="detail-item">
                <div className="detail-label">{t('dash.registrationStatus')}</div>
                <div className="detail-value"><StatusBadge status={regStatus} /></div>
              </div>
              <div className="detail-item">
                <div className="detail-label">{t('dash.sponsor')}</div>
                <div className="detail-value">{data.sponsorName ? `${data.sponsorName} (${data.sponsorCode})` : '—'}</div>
              </div>
              <div className="detail-item">
                <div className="detail-label">{t('dash.placementParent')}</div>
                <div className="detail-value">{data.placementParentName ? `${data.placementParentName} (${data.placementParentCode})` : '—'}</div>
              </div>
              <div className="detail-item">
                <div className="detail-label">{t('dash.joined')}</div>
                <div className="detail-value">{data.member.createdAt}</div>
              </div>
              <div className="detail-item">
                <div className="detail-label">{t('dash.registrationStatus')} · Fee</div>
                <div className="detail-value">₹50 <span className="badge badge-amber no-dot">{t('common.simulated')}</span></div>
              </div>
            </div>
          </div>
        </div>

        <div className="card">
          <div className="card-head"><h3>{t('dash.recentActivity')}</h3></div>
          <div className="card-pad">
            <div className="table-wrap table-plain">
              <table className="table">
                <tbody>
                  {data.activity.map((a, i) => (
                    <tr key={i}>
                      <td className="cell-strong">{ACTIVITY_ICONS[a.kind] || '•'} {a.text}</td>
                      <td className="cell-muted text-right">{a.meta}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>
    </>
  );
}

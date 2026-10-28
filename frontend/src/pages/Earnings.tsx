import { useMemo, useState } from 'react';
import { Alert, Badge, ErrorBox, Loading, PageHead, StatCard } from '../components/ui';
import { useFetch } from '../lib/useFetch';
import type { EarningsDto } from '../lib/api';
import { useI18n } from '../lib/i18n';
import { amountClass, money } from '../lib/util';

export default function Earnings() {
  const { t } = useI18n();
  const [type, setType] = useState('all');
  const [status, setStatus] = useState('all');
  const { data, error, loading } = useFetch<EarningsDto>('/api/earnings');

  const typeLabel = (ty: string) => {
    if (ty === 'direct_member') return t('earn.typeDirect');
    if (ty === 'child_milestone') return t('earn.typeChild');
    if (ty === 'reversal') return t('earn.typeReversal');
    return ty;
  };

  const entries = useMemo(() => {
    if (!data) return [];
    return data.entries.filter((e) =>
      (type === 'all' || e.type === type) && (status === 'all' || e.status === status));
  }, [data, type, status]);

  if (loading) return <Loading />;
  if (error || !data) return <ErrorBox message={error || 'No data'} />;

  return (
    <>
      <PageHead title={t('earn.title')} sub={t('earn.sub')} />
      <Alert tone="warn">{t('earn.disclaimer')}</Alert>

      <div className="stat-grid cols-3">
        <StatCard label={t('earn.totalRewards')} value={money(data.totalPaise)} valueClassName="amount pos" meta={t('common.simulated')} />
        <StatCard label={t('earn.pendingRewards')} value={money(data.pendingPaise)} valueClassName="amount" meta={t('common.pending')} />
        <StatCard label={t('earn.settledEntries')} value={money(data.settledPaise)} valueClassName="amount pos" meta={t('common.settled')} />
      </div>

      <div className="card">
        <div className="card-head"><h3>{t('earn.history')}</h3></div>
        <div className="card-pad">
          <div className="filter-bar">
            <select className="select" value={type} onChange={(e) => setType(e.target.value)}>
              <option value="all">{t('common.all')}</option>
              <option value="direct_member">{t('earn.typeDirect')}</option>
              <option value="child_milestone">{t('earn.typeChild')}</option>
              <option value="reversal">{t('earn.typeReversal')}</option>
            </select>
            <select className="select" value={status} onChange={(e) => setStatus(e.target.value)}>
              <option value="all">{t('common.all')}</option>
              <option value="pending">{t('common.pending')}</option>
              <option value="settled">{t('common.settled')}</option>
            </select>
          </div>

          <div className="table-wrap table-flush">
            <table className="table">
              <thead>
                <tr>
                  <th>{t('earn.type')}</th>
                  <th>{t('common.amount')}</th>
                  <th>{t('earn.status')}</th>
                  <th>{t('earn.reason')}</th>
                  <th>{t('earn.date')}</th>
                </tr>
              </thead>
              <tbody>
                {entries.map((e, i) => (
                  <tr key={i}>
                    <td className="cell-strong">{typeLabel(e.type)}</td>
                    <td className={amountClass(e.amountPaise)}>{money(e.amountPaise)}</td>
                    <td><Badge tone={e.status === 'settled' ? 'green' : 'amber'} noDot>{t(`common.${e.status}`)}</Badge></td>
                    <td className="cell-muted">{e.reason}</td>
                    <td className="cell-muted">{e.date}</td>
                  </tr>
                ))}
                {entries.length === 0 ? <tr><td colSpan={5} className="empty-state">{t('members.empty')}</td></tr> : null}
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </>
  );
}

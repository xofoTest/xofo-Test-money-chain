import { useState } from 'react';
import { Alert, Badge, ErrorBox, Loading, PageHead } from '../components/ui';
import { useFetch } from '../lib/useFetch';
import type { LedgerRowDto } from '../lib/api';
import { useI18n } from '../lib/i18n';
import { amountClass, money } from '../lib/util';

export default function Transactions() {
  const { t } = useI18n();
  const [type, setType] = useState('all');
  const [status, setStatus] = useState('all');
  const path = `/api/transactions?type=${type}&status=${status}`;
  const { data, error, loading } = useFetch<LedgerRowDto[]>(path);

  return (
    <>
      <PageHead title={t('txn.title')} sub={t('txn.sub')} />
      <Alert tone="info">{t('txn.disclaimer')}</Alert>

      <div className="filter-bar">
        <select className="select" value={type} onChange={(e) => setType(e.target.value)}>
          <option value="all">{t('common.all')}</option>
          <option value="registration">{t('earn.typeRegistration')}</option>
          <option value="reward">{t('earn.typeReward')}</option>
        </select>
        <select className="select" value={status} onChange={(e) => setStatus(e.target.value)}>
          <option value="all">{t('common.all')}</option>
          <option value="pending">{t('common.pending')}</option>
          <option value="settled">{t('common.settled')}</option>
          <option value="approved">{t('common.approved')}</option>
        </select>
      </div>

      {loading ? <Loading /> : null}
      {error ? <ErrorBox message={error} /> : null}
      {data ? (
        <div className="table-wrap">
          <table className="table">
            <thead>
              <tr>
                <th>{t('txn.type')}</th>
                <th>{t('common.amount')}</th>
                <th>{t('txn.status')}</th>
                <th>{t('txn.reference')}</th>
                <th>{t('common.date')}</th>
              </tr>
            </thead>
            <tbody>
              {data.map((e) => (
                <tr key={e.reference + e.type}>
                  <td className="cell-strong">{e.type === 'registration' ? t('earn.typeRegistration') : t('earn.typeReward')}</td>
                  <td className={amountClass(e.amountPaise)}>{money(e.amountPaise)}</td>
                  <td><Badge tone={e.status === 'settled' || e.status === 'approved' ? 'green' : 'amber'} noDot>{t(`common.${e.status}`)}</Badge></td>
                  <td className="mono cell-muted">{e.reference}</td>
                  <td className="cell-muted">{e.date}</td>
                </tr>
              ))}
              {data.length === 0 ? <tr><td colSpan={5} className="empty-state">{t('members.empty')}</td></tr> : null}
            </tbody>
          </table>
        </div>
      ) : null}
    </>
  );
}

import { useState } from 'react';
import { Link } from 'react-router-dom';
import { ErrorBox, Loading, PageHead, StatusBadge } from '../components/ui';
import { useFetch } from '../lib/useFetch';
import type { MemberRowDto } from '../lib/api';
import { useI18n } from '../lib/i18n';

export default function Members() {
  const { t } = useI18n();
  const [q, setQ] = useState('');
  const [status, setStatus] = useState('all');
  const path = `/api/members?q=${encodeURIComponent(q)}&status=${status}`;
  const { data, error, loading } = useFetch<MemberRowDto[]>(path);

  return (
    <>
      <PageHead
        title={t('members.title')}
        sub={t('members.sub')}
        actions={<Link className="btn btn-primary" to="/members/new">{t('members.addMember')}</Link>}
      />

      <div className="filter-bar">
        <input className="input grow" placeholder={t('members.searchPlaceholder')} value={q} onChange={(e) => setQ(e.target.value)} />
        <select className="select" value={status} onChange={(e) => setStatus(e.target.value)}>
          <option value="all">{t('common.all')}</option>
          <option value="active">{t('common.active')}</option>
          <option value="pending">{t('common.pending')}</option>
          <option value="suspended">{t('common.suspended')}</option>
        </select>
      </div>

      {loading ? <Loading /> : null}
      {error ? <ErrorBox message={error} /> : null}
      {data ? (
        <div className="table-wrap">
          <table className="table">
            <thead>
              <tr>
                <th>{t('members.tableId')}</th>
                <th>{t('members.tableName')}</th>
                <th>{t('members.tablePhone')}</th>
                <th>{t('members.tableSponsor')}</th>
                <th>{t('members.tablePlacement')}</th>
                <th>{t('members.tableStatus')}</th>
                <th>{t('members.tableJoined')}</th>
              </tr>
            </thead>
            <tbody>
              {data.length === 0 ? (
                <tr><td colSpan={7} className="empty-state">{t('members.empty')}</td></tr>
              ) : data.map((m) => (
                <tr key={m.memberCode}>
                  <td className="mono cell-strong">{m.memberCode}</td>
                  <td className="cell-strong">{m.name}</td>
                  <td className="cell-muted">{m.phone || '—'}</td>
                  <td className="cell-muted">{m.sponsorName} <span className="mono muted">{m.sponsorCode !== '—' ? m.sponsorCode : ''}</span></td>
                  <td className="cell-muted">{m.placementParentName || '—'}</td>
                  <td><StatusBadge status={m.status} /></td>
                  <td className="cell-muted">{m.joined}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}
    </>
  );
}

import { useState } from 'react';
import { ErrorBox, Loading, PageHead, StatusBadge, useToast } from '../components/ui';
import { useFetch } from '../lib/useFetch';
import { api } from '../lib/api';
import type { MemberRowDto } from '../lib/api';
import { useI18n } from '../lib/i18n';

export default function AdminMembers() {
  const { t } = useI18n();
  const [q, setQ] = useState('');
  const [status, setStatus] = useState('all');
  const path = `/api/admin/members?q=${encodeURIComponent(q)}&status=${status}`;
  const { data, error, loading, reload } = useFetch<MemberRowDto[]>(path);
  const { node, show } = useToast();

  const [pending, setPending] = useState<{ code: string; name: string; action: 'suspend' | 'reactivate' } | null>(null);
  const [reason, setReason] = useState('');
  const [warn, setWarn] = useState(false);

  const openModal = (m: MemberRowDto) => {
    const action = m.status === 'suspended' ? 'reactivate' : 'suspend';
    setPending({ code: m.memberCode, name: m.name, action });
    setReason('');
    setWarn(false);
  };

  const confirm = async () => {
    if (!pending) return;
    if (!reason.trim()) { setWarn(true); return; }
    try {
      await api.post(`/api/admin/members/${pending.code}/status`, {
        status: pending.action === 'suspend' ? 'suspended' : 'active',
        reason
      });
      setPending(null);
      show(t('am.done'), 'success');
      reload();
    } catch (e) {
      show(e instanceof Error ? e.message : 'Error', 'error');
    }
  };

  const exportCsv = () => {
    if (!data) return;
    const head = 'MemberID,Name,Email,Phone,Status,Sponsor,PlacementParent,Joined';
    const lines = data.map((m) =>
      [m.memberCode, `"${m.name}"`, m.email, m.phone ?? '', m.status, m.sponsorCode, m.placementParentCode ?? '', m.joined].join(','));
    const blob = new Blob([head + '\n' + lines.join('\n')], { type: 'text/csv' });
    const a = document.createElement('a');
    a.href = URL.createObjectURL(blob);
    a.download = 'moneychain-members.csv';
    document.body.appendChild(a);
    a.click();
    a.remove();
  };

  return (
    <>
      <PageHead
        title={t('am.title')}
        sub={t('am.sub')}
        actions={<button className="btn btn-secondary" onClick={exportCsv}>{t('am.exportCsv')}</button>}
      />

      <div className="filter-bar">
        <input className="input grow" placeholder={t('am.search')} value={q} onChange={(e) => setQ(e.target.value)} />
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
                <th>{t('am.tableId')}</th>
                <th>{t('am.tableName')}</th>
                <th>{t('am.tableContact')}</th>
                <th>{t('am.tableStatus')}</th>
                <th>{t('am.tableJoined')}</th>
                <th>{t('common.actions')}</th>
              </tr>
            </thead>
            <tbody>
              {data.map((m) => (
                <tr key={m.memberCode}>
                  <td className="mono cell-strong">{m.memberCode}</td>
                  <td className="cell-strong">{m.name}</td>
                  <td className="cell-muted">{m.email}<br /><span className="mono">{m.phone ?? '—'}</span></td>
                  <td><StatusBadge status={m.status} /></td>
                  <td className="cell-muted">{m.joined}</td>
                  <td>
                    <button className="btn btn-outline-danger btn-sm" onClick={() => openModal(m)}>
                      {m.status === 'suspended' ? t('am.reactivate') : t('am.suspend')}
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : null}

      <div className={`modal-overlay${pending ? ' show' : ''}`} onClick={() => setPending(null)}>
        <div className="modal" onClick={(e) => e.stopPropagation()}>
          <h3>{pending?.action === 'suspend' ? t('am.confirmSuspend') : t('am.confirmReactivate')}</h3>
          <p className="muted">{pending?.name} ({pending?.code})</p>
          <div className="field">
            <label className="label">{t('common.reason')}</label>
            <textarea className="textarea" placeholder={t('am.reasonPlaceholder')} value={reason} onChange={(e) => setReason(e.target.value)} />
          </div>
          {warn ? <p className="hint warn">{t('am.reasonRequired')}</p> : null}
          <div className="row mt-16">
            <button className="btn btn-primary" onClick={confirm}>{t('common.save')}</button>
            <button className="btn btn-ghost" onClick={() => setPending(null)}>{t('common.cancel')}</button>
          </div>
        </div>
      </div>

      {node}
    </>
  );
}

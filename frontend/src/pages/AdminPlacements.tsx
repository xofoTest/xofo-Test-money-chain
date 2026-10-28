import { useState } from 'react';
import { Alert, ErrorBox, Loading, PageHead, StatCard, useToast } from '../components/ui';
import { useFetch } from '../lib/useFetch';
import { api } from '../lib/api';
import type { PlacementHistoryDto, SlotRowDto } from '../lib/api';
import { useI18n } from '../lib/i18n';

export default function AdminPlacements() {
  const { t } = useI18n();
  const { data: slots, error: slotsErr, loading: slotsLoading, reload: reloadSlots } = useFetch<SlotRowDto[]>('/api/admin/placements/slots');
  const { data: history, reload: reloadHistory } = useFetch<PlacementHistoryDto[]>('/api/admin/placements/history');
  const { node, show } = useToast();

  const [issues, setIssues] = useState<string[] | null>(null);
  const [modalOpen, setModalOpen] = useState(false);
  const [corrMember, setCorrMember] = useState('');
  const [corrParent, setCorrParent] = useState('');
  const [corrReason, setCorrReason] = useState('');
  const [busy, setBusy] = useState(false);

  const detect = async () => {
    try {
      const res = await api.post<{ issues: string[] }>('/api/admin/placements/validate');
      setIssues(res.issues);
    } catch (e) {
      show(e instanceof Error ? e.message : 'Error', 'error');
    }
  };

  const correct = async () => {
    if (!corrReason.trim() || !corrMember) { show(t('am.reasonRequired'), 'error'); return; }
    setBusy(true);
    try {
      await api.post('/api/admin/placements/correct', { memberCode: corrMember, newParentCode: corrParent || null, reason: corrReason });
      setModalOpen(false);
      setCorrReason('');
      show(t('am.done'), 'success');
      reloadSlots();
      reloadHistory();
    } catch (e) {
      show(e instanceof Error ? e.message : 'Error', 'error');
    } finally {
      setBusy(false);
    }
  };

  const treeNodes = slots?.length ?? 0;
  const fullNodes = slots?.filter((s) => s.childCount >= 3).length ?? 0;
  const openSlots = slots?.reduce((sum, s) => sum + s.openSlots, 0) ?? 0;

  return (
    <>
      <PageHead
        title={t('ap.title')}
        sub={t('ap.sub')}
        actions={
          <>
            <button className="btn btn-secondary" onClick={detect}>{t('ap.detectInvalid')}</button>
            <button className="btn btn-primary" onClick={() => setModalOpen(true)}>{t('ap.correct')}</button>
          </>
        }
      />

      {issues !== null ? (
        issues.length === 0
          ? <Alert tone="success">{t('ap.noInvalid')}</Alert>
          : <Alert tone="error">{issues.length} invalid relationship(s): {issues.join('; ')}</Alert>
      ) : null}

      <div className="stat-grid cols-3">
        <StatCard label={t('admin.treeNodes')} value={treeNodes} />
        <StatCard label={t('admin.fullNodes')} value={fullNodes} />
        <StatCard label={t('admin.openSlots')} value={openSlots} />
      </div>

      <div className="grid-2 mt-16">
        <div className="card">
          <div className="card-head"><h3>{t('ap.inspectSlots')}</h3></div>
          {slotsLoading ? <Loading /> : null}
          {slotsErr ? <ErrorBox message={slotsErr} /> : null}
          {slots ? (
            <div className="table-wrap table-flush">
              <table className="table">
                <thead>
                  <tr>
                    <th>{t('members.tableName')}</th>
                    <th>{t('dash.directPlacement')}</th>
                    <th>{t('ap.slotsAvailable')}</th>
                    <th>{t('common.status')}</th>
                  </tr>
                </thead>
                <tbody>
                  {slots.map((s) => (
                    <tr key={s.memberCode}>
                      <td className="cell-strong">{s.name} <span className="mono muted">{s.memberCode}</span></td>
                      <td className="mono">{s.childCount} / 3</td>
                      <td><span className={`badge ${s.openSlots > 0 ? 'badge-green' : 'badge-gray'} no-dot`}>{s.openSlots}</span></td>
                      <td><span className={`badge ${s.status === 'active' ? 'badge-green' : s.status === 'suspended' ? 'badge-red' : 'badge-amber'}`}>{t(`common.${s.status}`)}</span></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
        </div>

        <div className="card">
          <div className="card-head"><h3>{t('ap.history')}</h3></div>
          {history ? (
            <div className="table-wrap table-flush">
              <table className="table">
                <thead>
                  <tr>
                    <th>{t('common.date')}</th>
                    <th>{t('at.member')}</th>
                    <th>{t('common.change')}</th>
                    <th>{t('common.reason')}</th>
                  </tr>
                </thead>
                <tbody>
                  {history.map((h, i) => (
                    <tr key={i}>
                      <td className="cell-muted">{h.date}</td>
                      <td className="cell-strong">{h.member}</td>
                      <td className="cell-muted">{h.change}</td>
                      <td className="cell-muted">{h.reason ?? '—'}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : null}
          <div className="card-pad"><p className="hint">{t('ap.correctNote')}</p></div>
        </div>
      </div>

      <div className={`modal-overlay${modalOpen ? ' show' : ''}`} onClick={() => setModalOpen(false)}>
        <div className="modal" onClick={(e) => e.stopPropagation()}>
          <h3>{t('ap.correct')}</h3>
          <p className="muted">{t('ap.correctNote')}</p>
          <div className="field">
            <label className="label">{t('at.member')}</label>
            <select className="select" value={corrMember} onChange={(e) => setCorrMember(e.target.value)}>
              <option value="">—</option>
              {slots?.map((s) => <option key={s.memberCode} value={s.memberCode}>{s.name} ({s.memberCode})</option>)}
            </select>
          </div>
          <div className="field mt-16">
            <label className="label">{t('add.placementParent')}</label>
            <select className="select" value={corrParent} onChange={(e) => setCorrParent(e.target.value)}>
              <option value="">—</option>
              {slots?.filter((s) => s.openSlots > 0).map((s) => <option key={s.memberCode} value={s.memberCode}>{s.name} ({s.memberCode})</option>)}
            </select>
          </div>
          <div className="field mt-16">
            <label className="label">{t('common.reason')}</label>
            <textarea className="textarea" placeholder={t('am.reasonPlaceholder')} value={corrReason} onChange={(e) => setCorrReason(e.target.value)} />
          </div>
          <div className="row mt-16">
            <button className="btn btn-primary" disabled={busy} onClick={correct}>{t('common.save')}</button>
            <button className="btn btn-ghost" onClick={() => setModalOpen(false)}>{t('common.cancel')}</button>
          </div>
        </div>
      </div>

      {node}
    </>
  );
}

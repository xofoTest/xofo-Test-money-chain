import { useEffect, useState } from 'react';
import { Alert, ErrorBox, Loading, PageHead, useToast } from '../components/ui';
import { useFetch } from '../lib/useFetch';
import { api } from '../lib/api';
import type { SettingsDto } from '../lib/api';
import { useI18n } from '../lib/i18n';

export default function AdminSettings() {
  const { t } = useI18n();
  const { data, error, loading } = useFetch<SettingsDto>('/api/admin/settings');
  const { node, show } = useToast();

  const [defaultLanguage, setDefaultLanguage] = useState('en');
  const [fee, setFee] = useState(50);
  const [threshold, setThreshold] = useState(3);
  const [directReward, setDirectReward] = useState(100);
  const [childReward, setChildReward] = useState(10);
  const [trigger, setTrigger] = useState('child_completes_three_placements');
  const [simulation, setSimulation] = useState(false);
  const [maintenance, setMaintenance] = useState(false);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (data) {
      setDefaultLanguage(data.defaultLanguage);
      setFee(data.registrationFeeInr);
      setThreshold(data.directMemberCountThreshold);
      setDirectReward(data.directMemberRewardInr);
      setChildReward(data.childRewardInr);
      setTrigger(data.childRewardTrigger);
      setSimulation(data.rewardSimulationEnabled);
      setMaintenance(data.maintenanceMode);
    }
  }, [data]);

  if (loading) return <Loading />;
  if (error || !data) return <ErrorBox message={error || 'No data'} />;

  const save = async () => {
    setBusy(true);
    try {
      await api.put('/api/admin/settings', {
        defaultLanguage,
        registrationFeeInr: fee,
        directMemberCountThreshold: threshold,
        directMemberRewardInr: directReward,
        childRewardInr: childReward,
        childRewardTrigger: trigger,
        rewardSimulationEnabled: simulation,
        maintenanceMode: maintenance
      });
      show(t('as.saved'), 'success');
    } catch (e) {
      show(e instanceof Error ? e.message : 'Error', 'error');
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <PageHead title={t('as.title')} sub={t('as.sub')} />
      <Alert tone="warn">{t('as.disabledNote')}</Alert>

      <div className="grid-2">
        <div className="card">
          <div className="card-head"><h3>{t('as.languageDefault')}</h3></div>
          <div className="card-pad">
            <div className="stack">
              <div className="field">
                <label className="label">{t('as.languageDefault')}</label>
                <select className="select" value={defaultLanguage} onChange={(e) => setDefaultLanguage(e.target.value)}>
                  <option value="en">English</option>
                  <option value="ml">മലയാളം</option>
                </select>
              </div>
              <div className="field">
                <label className="label">{t('as.feeDisplay')}</label>
                <div className="input-group">
                  <input className="input" type="number" value={fee} onChange={(e) => setFee(Number(e.target.value))} />
                  <span className="badge badge-amber no-dot">{t('common.simulated')}</span>
                </div>
                <p className="hint">{t('add.feeInfo')}</p>
              </div>
              <div className="toggle">
                <input type="checkbox" id="maintenance" checked={maintenance} onChange={(e) => setMaintenance(e.target.checked)} />
                <label htmlFor="maintenance">{t('as.maintenance')}</label>
              </div>
              <button className="btn btn-primary" disabled={busy} onClick={save}>{t('as.save')}</button>
            </div>
          </div>
        </div>

        <div className="card">
          <div className="card-head"><h3>{t('as.rewardPolicy')}</h3></div>
          <div className="card-pad">
            <div className="form-grid">
              <div className="field">
                <label className="label">{t('as.directThreshold')}</label>
                <input className="input" type="number" value={threshold} onChange={(e) => setThreshold(Number(e.target.value))} />
              </div>
              <div className="field">
                <label className="label">{t('as.directReward')}</label>
                <input className="input" type="number" value={directReward} onChange={(e) => setDirectReward(Number(e.target.value))} />
              </div>
              <div className="field">
                <label className="label">{t('as.childReward')}</label>
                <input className="input" type="number" value={childReward} onChange={(e) => setChildReward(Number(e.target.value))} />
              </div>
              <div className="field">
                <label className="label">{t('as.childTrigger')}</label>
                <select className="select" value={trigger} onChange={(e) => setTrigger(e.target.value)}>
                  <option value="child_completes_three_placements">{t('as.childTriggerDesc')}</option>
                </select>
              </div>
            </div>

            <div className="toggle mt-16">
              <input type="checkbox" id="sim" checked={simulation} onChange={(e) => setSimulation(e.target.checked)} />
              <label htmlFor="sim">{t('as.simEnable')}</label>
            </div>
            <p className="hint">{t('as.simDisabled')}</p>

            <div className="detail-list mt-16">
              <div className="detail-item">
                <div className="detail-label">{t('as.livePayments')}</div>
                <div className="detail-value"><span className="badge badge-gray no-dot">OFF · {t('common.disabled')}</span></div>
              </div>
              <div className="detail-item">
                <div className="detail-label">{t('as.livePayouts')}</div>
                <div className="detail-value"><span className="badge badge-gray no-dot">OFF · {t('common.disabled')}</span></div>
              </div>
            </div>
          </div>
        </div>
      </div>

      {node}
    </>
  );
}

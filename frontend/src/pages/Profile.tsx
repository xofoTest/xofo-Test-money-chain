import { useEffect, useState } from 'react';
import { ErrorBox, Loading, PageHead, useToast } from '../components/ui';
import { useFetch } from '../lib/useFetch';
import { api } from '../lib/api';
import type { DashboardDto } from '../lib/api';
import { useAuth } from '../lib/auth';
import { useI18n } from '../lib/i18n';

export default function Profile() {
  const { t, lang, setLang } = useI18n();
  const { profile } = useAuth();
  const { data, error, loading } = useFetch<DashboardDto>('/api/dashboard');
  const { node, show } = useToast();

  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [language, setLanguage] = useState('en');
  const [currentPw, setCurrentPw] = useState('');
  const [newPw, setNewPw] = useState('');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (profile) {
      setName(profile.fullName);
      setPhone(profile.phone ?? '');
      setLanguage(profile.preferredLanguage);
    }
  }, [profile]);

  if (loading) return <Loading />;
  if (error || !data) return <ErrorBox message={error || 'No data'} />;

  const saveProfile = async () => {
    setBusy(true);
    try {
      await api.put('/api/profile', { fullName: name, phone: phone || null, preferredLanguage: language });
      setLang(language === 'ml' ? 'ml' : 'en');
      show(t('prof.saved'), 'success');
    } catch (e) {
      show(e instanceof Error ? e.message : 'Error', 'error');
    } finally {
      setBusy(false);
    }
  };

  const changePassword = async () => {
    setBusy(true);
    try {
      await api.post('/api/auth/change-password', { currentPassword: currentPw, newPassword: newPw });
      setCurrentPw(''); setNewPw('');
      show(t('prof.saved'), 'success');
    } catch (e) {
      show(e instanceof Error ? e.message : 'Error', 'error');
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <PageHead title={t('prof.title')} sub={t('prof.sub')} />

      <div className="grid-2">
        <div className="card">
          <div className="card-head"><h3>{t('prof.personalInfo')}</h3></div>
          <div className="card-pad">
            <div className="detail-list">
              <div className="detail-item">
                <div className="detail-label">{t('prof.memberId')}</div>
                <div className="detail-value mono">{profile?.memberCode}</div>
              </div>
              <div className="detail-item">
                <div className="detail-label">{t('prof.sponsor')}</div>
                <div className="detail-value">{data.sponsorName ?? '—'}</div>
              </div>
              <div className="detail-item">
                <div className="detail-label">{t('prof.placement')}</div>
                <div className="detail-value">{data.placementParentName ?? '—'}</div>
              </div>
              <div className="detail-item">
                <div className="detail-label">{t('dash.joined')}</div>
                <div className="detail-value">{profile?.createdAt}</div>
              </div>
            </div>
          </div>
        </div>

        <div className="card">
          <div className="card-head"><h3>{t('prof.contactInfo')}</h3></div>
          <div className="card-pad">
            <div className="stack">
              <div className="field">
                <label className="label">{t('auth.fullName')}</label>
                <input className="input" value={name} onChange={(e) => setName(e.target.value)} />
              </div>
              <div className="field">
                <label className="label">{t('auth.phone')}</label>
                <input className="input" value={phone} onChange={(e) => setPhone(e.target.value)} placeholder={t('auth.phonePlaceholder')} />
              </div>
              <div className="field">
                <label className="label">{t('auth.email')}</label>
                <input className="input readonly" value={profile?.email ?? ''} disabled />
              </div>
              <div className="field">
                <label className="label">{t('prof.language')}</label>
                <select className="select" value={language} onChange={(e) => setLanguage(e.target.value)}>
                  <option value="en">English</option>
                  <option value="ml">മലയാളം</option>
                </select>
              </div>
              <button className="btn btn-primary" disabled={busy} onClick={saveProfile}>{t('prof.save')}</button>
            </div>
          </div>
        </div>
      </div>

      <div className="card mt-16">
        <div className="card-head"><h3>{t('prof.passwordTitle')}</h3></div>
        <div className="card-pad">
          <div className="grid-2">
            <div className="field">
              <label className="label">{t('prof.currentPassword')}</label>
              <input className="input" type="password" value={currentPw} onChange={(e) => setCurrentPw(e.target.value)} />
            </div>
            <div className="field">
              <label className="label">{t('prof.newPassword')}</label>
              <input className="input" type="password" value={newPw} onChange={(e) => setNewPw(e.target.value)} />
            </div>
          </div>
          <button className="btn btn-secondary mt-16" disabled={busy} onClick={changePassword}>{t('prof.save')}</button>
        </div>
      </div>

      {node}
    </>
  );
}

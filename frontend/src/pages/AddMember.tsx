import { useState } from 'react';
import { ErrorBox, Loading, PageHead, useToast } from '../components/ui';
import { useFetch } from '../lib/useFetch';
import { api } from '../lib/api';
import type { EligibleParentDto, ProfileDto } from '../lib/api';
import { useAuth } from '../lib/auth';
import { useI18n } from '../lib/i18n';

export default function AddMember() {
  const { t } = useI18n();
  const { profile } = useAuth();
  const { data: parents, error, loading } = useFetch<EligibleParentDto[]>('/api/eligible-parents');
  const { node, show } = useToast();

  const [name, setName] = useState('');
  const [phone, setPhone] = useState('');
  const [email, setEmail] = useState('');
  const [lang, setLang] = useState('en');
  const [parentCode, setParentCode] = useState('');
  const [terms, setTerms] = useState(false);
  const [busy, setBusy] = useState(false);
  const [serverError, setServerError] = useState('');

  const selected = parents?.find((p) => p.memberCode === parentCode) ?? parents?.[0];
  const openSlots = selected?.openSlots ?? 0;

  const submit = async () => {
    setServerError('');
    if (!name.trim()) { show(t('add.error'), 'error'); return; }
    if (!terms) { show(t('add.terms'), 'error'); return; }
    if (!selected) { show(t('add.error'), 'error'); return; }
    setBusy(true);
    try {
      const res = await api.post<ProfileDto>('/api/members', {
        fullName: name, email, phone: phone || null, preferredLanguage: lang,
        placementParentCode: selected.memberCode, termsAccepted: terms
      });
      show(`${t('add.success')} (${res.memberCode})`, 'success');
      setName(''); setPhone(''); setEmail(''); setTerms(false);
    } catch (e) {
      const msg = e instanceof Error ? e.message : t('add.error');
      setServerError(msg);
      show(msg, 'error');
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <PageHead title={t('add.title')} sub={t('add.sub')} />

      {loading ? <Loading /> : null}
      {error ? <ErrorBox message={error} /> : null}

      {parents ? (
        <div className="grid-2">
          <div className="stack">
            <div className="card">
              <div className="card-head"><h3>{t('add.sponsorInfo')}</h3></div>
              <div className="card-pad">
                <div className="detail-list">
                  <div className="detail-item">
                    <div className="detail-label">{t('add.sponsor')}</div>
                    <div className="detail-value">{profile?.fullName ?? '—'}</div>
                  </div>
                  <div className="detail-item">
                    <div className="detail-label">{t('add.memberCode')}</div>
                    <div className="detail-value mono">{profile?.memberCode ?? '—'}</div>
                  </div>
                  <div className="detail-item">
                    <div className="detail-label">{t('common.roleMember')}</div>
                    <div className="detail-value"><span className="badge badge-blue no-dot">{t('common.roleMember')}</span></div>
                  </div>
                  <div className="detail-item">
                    <div className="detail-label">{t('add.placementParent')}</div>
                    <div className="detail-value muted">{t('dash.sponsor')}</div>
                  </div>
                </div>
              </div>
            </div>

            <div className="card">
              <div className="card-head"><h3>{t('add.placementParent')}</h3></div>
              <div className="card-pad">
                <div className="field">
                  <label className="label">{t('add.placementParent')}</label>
                  <select className="select" value={selected?.memberCode ?? ''} onChange={(e) => setParentCode(e.target.value)}>
                    {parents.map((p) => (
                      <option key={p.memberCode} value={p.memberCode}>{p.memberCode} — {p.name} ({p.childCount}/3)</option>
                    ))}
                  </select>
                </div>
                <div className="field mt-16">
                  <label className="label">{t('add.availableSlots')}</label>
                  <div className="slot-dots">
                    {Array.from({ length: 3 }).map((_, i) => (
                      <span key={i} className={`slot-dot${i < (selected?.childCount ?? 0) ? '' : ' empty'}`} />
                    ))}
                  </div>
                  <p className="hint">{openSlots} / 3 {t('team.slotOpen')}</p>
                </div>
                {openSlots === 0 ? <div className="alert alert-error"><span>⚠</span><span>{t('add.error')}</span></div> : null}
              </div>
            </div>
          </div>

          <div className="card">
            <div className="card-head"><h3>{t('add.newMemberDetails')}</h3></div>
            <div className="card-pad">
              <div className="stack">
                <div className="field">
                  <label className="label">{t('auth.fullName')} <span className="req">*</span></label>
                  <input className="input" placeholder={t('auth.fullNamePlaceholder')} value={name} onChange={(e) => setName(e.target.value)} />
                </div>
                <div className="field">
                  <label className="label">{t('auth.phone')}</label>
                  <input className="input" placeholder={t('auth.phonePlaceholder')} value={phone} onChange={(e) => setPhone(e.target.value)} />
                </div>
                <div className="field">
                  <label className="label">{t('auth.email')} <span className="req">*</span></label>
                  <input className="input" type="email" placeholder={t('auth.emailPlaceholder')} value={email} onChange={(e) => setEmail(e.target.value)} />
                </div>
                <div className="field">
                  <label className="label">{t('auth.language')}</label>
                  <select className="select" value={lang} onChange={(e) => setLang(e.target.value)}>
                    <option value="en">English</option>
                    <option value="ml">മലയാളം</option>
                  </select>
                </div>
                <label className="checkbox">
                  <input type="checkbox" checked={terms} onChange={(e) => setTerms(e.target.checked)} />
                  <span>{t('add.terms')}</span>
                </label>
                {serverError ? <div className="alert alert-error"><span>⚠</span><span>{serverError}</span></div> : null}
                <div className="alert alert-warn"><span>₹</span><span>{t('add.feeInfo')}</span></div>
                <button className="btn btn-primary btn-block btn-lg" disabled={busy || openSlots === 0} onClick={submit}>{t('add.submit')}</button>
              </div>
            </div>
          </div>
        </div>
      ) : null}

      {node}
    </>
  );
}

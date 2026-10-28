import { useState } from 'react';
import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';
import { useI18n } from '../lib/i18n';
import { statusClass } from '../lib/util';

export function BrandMark({ className = 'brand-mark' }: { className?: string }) {
  return (
    <svg className={className} viewBox="0 0 32 32" fill="none" aria-hidden="true">
      <circle cx="16" cy="6.5" r="3" fill="currentColor" />
      <circle cx="5.5" cy="22.5" r="3" fill="currentColor" />
      <circle cx="16" cy="26" r="3" fill="currentColor" />
      <circle cx="26.5" cy="22.5" r="3" fill="currentColor" />
      <path d="M16 9.5 L8 20 M16 9.5 L16 23 M16 9.5 L24 20" stroke="currentColor" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}

export function Brand({ dark = false, to = '/' }: { dark?: boolean; to?: string }) {
  return (
    <Link className={`brand${dark ? ' on-dark' : ''}`} to={to}>
      <BrandMark />
      <span className="brand-name">Money Chain <em>SAS</em></span>
    </Link>
  );
}

export function LangSwitch() {
  const { lang, setLang } = useI18n();
  return (
    <div className="lang-switch" role="group" aria-label="Language">
      <button type="button" className={lang === 'en' ? 'active' : ''} onClick={() => setLang('en')}>EN</button>
      <button type="button" className={lang === 'ml' ? 'active' : ''} onClick={() => setLang('ml')}>മലയാളം</button>
    </div>
  );
}

export function StatusBadge({ status }: { status: string }) {
  const { t } = useI18n();
  const label = t(`common.${status}`) === `common.${status}` ? status : t(`common.${status}`);
  return <span className={`badge ${statusClass(status)}`}>● <span>{label}</span></span>;
}

export function Badge({ tone = 'gray', noDot = false, children }: { tone?: 'green' | 'amber' | 'red' | 'blue' | 'gray'; noDot?: boolean; children: ReactNode }) {
  return <span className={`badge badge-${tone}${noDot ? ' no-dot' : ''}`}>{children}</span>;
}

export function StatCard({ label, value, meta, valueClassName = '' }: { label: string; value: ReactNode; meta?: ReactNode; valueClassName?: string }) {
  return (
    <div className="card stat-card">
      <div className="stat-label">{label}</div>
      <div className={`stat-value ${valueClassName}`}>{value}</div>
      {meta ? <div className="stat-meta">{meta}</div> : null}
    </div>
  );
}

export function Alert({ tone, children }: { tone: 'success' | 'error' | 'info' | 'warn'; children: ReactNode }) {
  const icon = tone === 'success' ? '✓' : tone === 'error' ? '⚠' : tone === 'info' ? 'ℹ' : '⚠';
  return (
    <div className={`alert alert-${tone}`}>
      <span>{icon}</span>
      <span>{children}</span>
    </div>
  );
}

export function PageHead({ title, sub, actions }: { title: string; sub?: string; actions?: ReactNode }) {
  return (
    <div className="page-head">
      <div>
        <h1 className="page-title">{title}</h1>
        {sub ? <p className="page-sub">{sub}</p> : null}
      </div>
      {actions ? <div className="page-head-actions">{actions}</div> : null}
    </div>
  );
}

export function Field({ label, required, hint, children }: { label: ReactNode; required?: boolean; hint?: ReactNode; children: ReactNode }) {
  return (
    <div className="field">
      <label className="label">{label}{required ? <span className="req"> *</span> : null}</label>
      {children}
      {hint ? <p className="hint">{hint}</p> : null}
    </div>
  );
}

export function Loading() {
  return <div className="empty-state"><div className="empty-ico">…</div>Loading…</div>;
}

export function ErrorBox({ message }: { message: string }) {
  return <Alert tone="error">{message}</Alert>;
}

export function useToast(): { node: ReactNode; show: (message: string, type?: 'success' | 'error') => void } {
  const [state, setState] = useState<{ message: string; type: string; visible: boolean }>({ message: '', type: '', visible: false });

  const show = (message: string, type: 'success' | 'error' = 'success') => {
    setState({ message, type, visible: true });
    window.setTimeout(() => setState((s) => ({ ...s, visible: false })), 3200);
  };

  const node = <div className={`toast ${state.type}${state.visible ? ' show' : ''}`}>{state.message}</div>;
  return { node, show };
}

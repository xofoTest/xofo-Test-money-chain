import { useState } from 'react';
import type { FormEvent } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { Brand, LangSwitch } from '../components/ui';
import { useAuth } from '../lib/auth';
import { useI18n } from '../lib/i18n';

export default function Login() {
  const { t } = useI18n();
  const { login } = useAuth();
  const navigate = useNavigate();
  const [email, setEmail] = useState('arun.nair@example.com');
  const [password, setPassword] = useState('Demo1234');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const doLogin = async (em: string, pw: string) => {
    setBusy(true);
    setError('');
    try {
      const profile = await login(em, pw);
      navigate(profile.role === 'admin' ? '/admin' : '/dashboard');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Login failed');
    } finally {
      setBusy(false);
    }
  };

  const submit = (e: FormEvent) => {
    e.preventDefault();
    doLogin(email, password);
  };

  return (
    <div className="auth-wrap">
      <div className="auth-nav">
        <Brand />
        <div className="auth-links">
          <LangSwitch />
          <Link className="btn btn-ghost" to="/register">{t('nav.register')}</Link>
        </div>
      </div>

      <div className="auth-main">
        <div className="auth-card card">
          <h1>{t('auth.loginTitle')}</h1>
          <p className="auth-sub">{t('auth.loginSub')}</p>
          {error ? <div className="alert alert-error"><span>⚠</span><span>{error}</span></div> : null}
          <form className="stack" onSubmit={submit}>
            <div className="field">
              <label className="label" htmlFor="email">{t('auth.email')}</label>
              <input className="input" id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} placeholder={t('auth.emailPlaceholder')} required />
            </div>
            <div className="field">
              <label className="label" htmlFor="password">{t('auth.password')}</label>
              <input className="input" id="password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} placeholder={t('auth.passwordPlaceholder')} required />
            </div>
            <div className="flex-between">
              <label className="checkbox"><input type="checkbox" defaultChecked /> <span>{t('auth.rememberMe')}</span></label>
              <span className="hint">{t('auth.forgot')}</span>
            </div>
            <button className="btn btn-primary btn-block btn-lg" type="submit" disabled={busy}>{t('auth.signIn')}</button>
          </form>
          <p className="hint text-center mt-16">
            {t('auth.noAccount')} <Link to="/register">{t('nav.register')}</Link>
          </p>

          <div className="demo-login">
            <p className="hint">{t('auth.loginAs')}</p>
            <button className="btn btn-secondary btn-block" disabled={busy} onClick={() => doLogin('arun.nair@example.com', 'Demo1234')}>
              {t('auth.loginAsMember')}
            </button>
            <button className="btn btn-secondary btn-block" disabled={busy} onClick={() => doLogin('admin@moneychain.demo', 'Admin@1234')}>
              {t('auth.loginAsAdmin')}
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}

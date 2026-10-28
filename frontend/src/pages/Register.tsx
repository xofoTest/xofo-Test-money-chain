import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { Brand, LangSwitch } from '../components/ui';
import { api } from '../lib/api';
import type { AuthResponse } from '../lib/api';
import { useAuth } from '../lib/auth';
import { useI18n } from '../lib/i18n';

const schema = z.object({
  fullName: z.string().min(1),
  email: z.string().email(),
  phone: z.string().optional(),
  sponsorCode: z.string().min(1),
  password: z.string().min(8),
  confirm: z.string().min(8),
  preferredLanguage: z.enum(['en', 'ml']),
  termsAccepted: z.boolean().refine((v) => v === true)
}).refine((d) => d.password === d.confirm, { path: ['confirm'], message: 'Passwords do not match' });

type RegisterForm = z.infer<typeof schema>;

export default function Register() {
  const { t } = useI18n();
  const { login } = useAuth();
  const navigate = useNavigate();
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);

  const { register, handleSubmit, formState: { errors } } = useForm<RegisterForm>({
    resolver: zodResolver(schema),
    defaultValues: { fullName: '', email: '', phone: '', sponsorCode: 'MC0001', password: '', confirm: '', preferredLanguage: 'en', termsAccepted: false }
  });

  const onSubmit = async (data: RegisterForm) => {
    setBusy(true);
    setError('');
    try {
      const res = await api.post<AuthResponse>('/api/auth/register', {
        fullName: data.fullName,
        email: data.email,
        phone: data.phone || null,
        password: data.password,
        sponsorCode: data.sponsorCode,
        preferredLanguage: data.preferredLanguage,
        termsAccepted: data.termsAccepted
      });
      const profile = await login(data.email, data.password);
      void res;
      navigate(profile.role === 'admin' ? '/admin' : '/dashboard');
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Registration failed');
    } finally {
      setBusy(false);
    }
  };

  const err = (key: keyof RegisterForm) => (errors[key]?.message as string) || '';

  return (
    <div className="auth-wrap">
      <div className="auth-nav">
        <Brand />
        <div className="auth-links">
          <LangSwitch />
          <Link className="btn btn-ghost" to="/login">{t('nav.login')}</Link>
        </div>
      </div>

      <div className="auth-main">
        <div className="auth-card card wide">
          <h1>{t('auth.registerTitle')}</h1>
          <p className="auth-sub">{t('auth.registerSub')}</p>

          <div className="alert alert-info"><span>ℹ</span><span>{t('auth.feeInfo')}</span></div>
          {error ? <div className="alert alert-error"><span>⚠</span><span>{error}</span></div> : null}

          <form className="stack" onSubmit={handleSubmit(onSubmit)}>
            <div className="form-grid">
              <div className="field">
                <label className="label">{t('auth.fullName')} <span className="req">*</span></label>
                <input className="input" placeholder={t('auth.fullNamePlaceholder')} {...register('fullName')} />
                {err('fullName') ? <p className="hint warn">{err('fullName')}</p> : null}
              </div>
              <div className="field">
                <label className="label">{t('auth.phone')}</label>
                <input className="input" placeholder={t('auth.phonePlaceholder')} {...register('phone')} />
              </div>
              <div className="field">
                <label className="label">{t('auth.email')} <span className="req">*</span></label>
                <input className="input" type="email" placeholder={t('auth.emailPlaceholder')} {...register('email')} />
                {err('email') ? <p className="hint warn">{err('email')}</p> : null}
              </div>
              <div className="field">
                <label className="label">{t('auth.sponsorCode')} <span className="req">*</span></label>
                <input className="input" placeholder={t('auth.sponsorCodePlaceholder')} {...register('sponsorCode')} />
                {err('sponsorCode') ? <p className="hint warn">{err('sponsorCode')}</p> : null}
              </div>
              <div className="field">
                <label className="label">{t('auth.password')} <span className="req">*</span></label>
                <input className="input" type="password" placeholder={t('auth.passwordPlaceholder')} {...register('password')} />
                {err('password') ? <p className="hint warn">{err('password')}</p> : null}
              </div>
              <div className="field">
                <label className="label">{t('auth.confirmPassword')} <span className="req">*</span></label>
                <input className="input" type="password" placeholder={t('auth.confirmPlaceholder')} {...register('confirm')} />
                {err('confirm') ? <p className="hint warn">{err('confirm')}</p> : null}
              </div>
              <div className="field full">
                <label className="label">{t('auth.language')}</label>
                <select className="select" {...register('preferredLanguage')}>
                  <option value="en">English</option>
                  <option value="ml">മലയാളം</option>
                </select>
              </div>
            </div>
            <label className="checkbox">
              <input type="checkbox" {...register('termsAccepted')} />
              <span>{t('auth.terms')}</span>
            </label>
            {err('termsAccepted') ? <p className="hint warn">{t('auth.terms')}</p> : null}
            <button className="btn btn-primary btn-block btn-lg" type="submit" disabled={busy}>{t('auth.createAccount')}</button>
            <p className="hint text-center">
              {t('auth.haveAccount')} <Link to="/login">{t('nav.login')}</Link>
            </p>
          </form>
        </div>
      </div>
    </div>
  );
}

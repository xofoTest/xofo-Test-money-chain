import { Link } from 'react-router-dom';
import { useI18n } from '../lib/i18n';

export default function NotFound() {
  const { t } = useI18n();
  return (
    <div className="auth-wrap">
      <div className="auth-main">
        <div className="auth-card card text-center">
          <div className="empty-ico" style={{ fontSize: 44 }}>🔍</div>
          <h1>{t('nf.title')}</h1>
          <p className="auth-sub">{t('nf.body')}</p>
          <Link className="btn btn-primary" to="/">{t('nf.home')}</Link>
        </div>
      </div>
    </div>
  );
}

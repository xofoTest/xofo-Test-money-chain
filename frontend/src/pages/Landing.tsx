import { Link } from 'react-router-dom';
import { Brand, BrandMark, LangSwitch } from '../components/ui';
import { useI18n } from '../lib/i18n';

const FEATURES = [
  ['🌳', 'landing.f1Title', 'landing.f1Desc'],
  ['🗣️', 'landing.f2Title', 'landing.f2Desc'],
  ['📊', 'landing.f3Title', 'landing.f3Desc'],
  ['🔗', 'landing.f4Title', 'landing.f4Desc'],
  ['🛡️', 'landing.f5Title', 'landing.f5Desc'],
  ['🔐', 'landing.f6Title', 'landing.f6Desc']
] as const;

export default function Landing() {
  const { t } = useI18n();
  const heroTitle = t('landing.heroTitle').split('\n');

  return (
    <div className="landing">
      <header className="site-nav">
        <div className="site-nav-inner">
          <Brand />
          <nav className="site-links">
            <a href="#features">{t('nav.features')}</a>
            <a href="#how">{t('nav.how')}</a>
          </nav>
          <div className="site-nav-right">
            <LangSwitch />
            <Link className="btn btn-ghost" to="/login">{t('nav.login')}</Link>
            <Link className="btn btn-primary" to="/register">{t('nav.register')}</Link>
          </div>
        </div>
      </header>

      <section className="hero">
        <div className="hero-inner">
          <div className="hero-copy">
            <span className="eyebrow">{t('landing.eyebrow')}</span>
            <h1>
              {heroTitle.map((line, i) => (
                <span key={i}>{line}{i < heroTitle.length - 1 ? <br /> : null}</span>
              ))}
            </h1>
            <p className="hero-sub">{t('landing.heroSub')}</p>
            <div className="hero-cta">
              <Link className="btn btn-primary btn-lg" to="/login">{t('landing.ctaDashboard')}</Link>
              <Link className="btn btn-secondary btn-lg" to="/register">{t('landing.ctaRegister')}</Link>
            </div>
            <p className="demo-note"><span className="dot" /> <span>{t('landing.demoNote')}</span></p>
          </div>
          <div className="hero-visual">
            <div className="tree-card card">
              <div className="tree-card-head">
                <span className="tree-card-label">{t('landing.treeLabel')}</span>
                <span className="badge badge-blue">{t('landing.treeLive')}</span>
              </div>
              <div className="mini-tree">
                <div className="mini-node root">
                  <span className="mini-avatar">AN</span>
                  <span className="mini-name">Arun Nair</span>
                  <span className="mini-code">MC0001</span>
                </div>
                <div className="mini-branch">
                  <div className="mini-node"><span className="mini-avatar">DM</span><span className="mini-name">Deepa Menon</span><span className="mini-code">MC0002</span></div>
                  <div className="mini-node"><span className="mini-avatar">SK</span><span className="mini-name">Suresh Kumar</span><span className="mini-code">MC0003</span></div>
                  <div className="mini-node"><span className="mini-avatar">AB</span><span className="mini-name">Aisha Beevi</span><span className="mini-code">MC0004</span></div>
                </div>
                <div className="mini-branch">
                  <div className="mini-node dim"><span className="mini-avatar">+3</span><span className="mini-name">{t('landing.moreMembers')}</span></div>
                  <div className="mini-node dim"><span className="mini-avatar">+3</span><span className="mini-name">{t('landing.moreMembers')}</span></div>
                  <div className="mini-node dim"><span className="mini-avatar">+2</span><span className="mini-name">{t('landing.moreMembers')}</span></div>
                </div>
              </div>
              <p className="tree-card-foot">{t('landing.treeFoot')}</p>
            </div>
          </div>
        </div>
      </section>

      <section className="section" id="features">
        <div className="section-head">
          <h2>{t('landing.featuresTitle')}</h2>
          <p>{t('landing.featuresSub')}</p>
        </div>
        <div className="feature-grid">
          {FEATURES.map(([icon, title, desc]) => (
            <div className="feature card" key={title}>
              <div className="feature-icon">{icon}</div>
              <h3>{t(title)}</h3>
              <p>{t(desc)}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="section section-alt" id="how">
        <div className="section-head">
          <h2>{t('landing.howTitle')}</h2>
          <p>{t('landing.howSub')}</p>
        </div>
        <div className="steps">
          {[1, 2, 3].map((n) => (
            <div className="step card" key={n}>
              <span className="step-num">{n}</span>
              <h3>{t(`landing.step${n}Title`)}</h3>
              <p>{t(`landing.step${n}Desc`)}</p>
            </div>
          ))}
        </div>
      </section>

      <section className="disclaimer-band">
        <div className="disclaimer-inner">
          <strong>{t('landing.disclaimerTitle')}</strong>
          <p>{t('landing.disclaimerBody')}</p>
        </div>
      </section>

      <footer className="site-footer">
        <div className="site-footer-inner">
          <div className="footer-brand">
            <Brand />
            <p>{t('landing.footerNote')}</p>
          </div>
          <div className="footer-links">
            <Link to="/login">{t('nav.login')}</Link>
            <Link to="/register">{t('nav.register')}</Link>
            <Link to="/dashboard">{t('nav.dashboard')}</Link>
            <Link to="/admin">{t('nav.adminDemo')}</Link>
          </div>
        </div>
      </footer>
    </div>
  );
}

export { BrandMark };

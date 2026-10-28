import { useState } from 'react';
import { ErrorBox, Loading, PageHead, StatusBadge } from '../components/ui';
import { useFetch } from '../lib/useFetch';
import type { TreeDto } from '../lib/api';
import { useAuth } from '../lib/auth';
import { useI18n } from '../lib/i18n';
import Tree from '../components/Tree';

export default function MyTeam() {
  const { t } = useI18n();
  const { profile } = useAuth();
  const meCode = profile?.memberCode ?? '';
  const [root, setRoot] = useState<string>(meCode);
  const [query, setQuery] = useState('');

  const path = `/api/tree?root=${encodeURIComponent(root)}`;
  const { data, error, loading, reload } = useFetch<TreeDto[]>(path);

  const rootNode = data?.find((n) => n.memberCode === root);
  const effectiveRoot = root || meCode;

  return (
    <>
      <PageHead title={t('team.title')} sub={t('team.sub')} />

      <div className="filter-bar">
        <input className="input grow" placeholder={t('team.search')} value={query} onChange={(e) => setQuery(e.target.value)} />
      </div>

      {root !== meCode ? (
        <div className="row" style={{ marginBottom: 12 }}>
          <button className="btn btn-ghost btn-sm" onClick={() => setRoot(meCode)}>← {t('common.back')}</button>
          <span className="muted">{rootNode?.name} ({rootNode?.memberCode})</span>
        </div>
      ) : null}

      {loading ? <Loading /> : null}
      {error ? <ErrorBox message={error} /> : null}
      {data ? (
        <Tree
          nodes={data}
          rootCode={effectiveRoot}
          meCode={meCode}
          query={query}
          onSubtree={(code) => { setRoot(code); setQuery(''); reload(); }}
        />
      ) : null}
    </>
  );
}

export { StatusBadge };

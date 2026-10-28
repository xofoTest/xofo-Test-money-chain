import { useState } from 'react';
import type { TreeDto } from '../lib/api';
import { useI18n } from '../lib/i18n';
import { initials } from '../lib/util';
import { StatusBadge } from './ui';

interface TreeProps {
  nodes: TreeDto[];
  rootCode: string;
  meCode: string;
  query: string;
  onSubtree: (code: string) => void;
}

export default function Tree({ nodes, rootCode, meCode, query, onSubtree }: TreeProps) {
  const { t } = useI18n();
  const [collapsed, setCollapsed] = useState<Set<string>>(new Set());

  const childrenOf = (code: string) => nodes.filter((n) => n.parentCode === code).slice(0, 3);
  const hasChildren = (code: string) => nodes.some((n) => n.parentCode === code);

  const matches = (code: string, name: string) => {
    if (!query) return false;
    const q = query.toLowerCase();
    return name.toLowerCase().includes(q) || code.toLowerCase().includes(q);
  };

  const NodeCard = ({ node, depth }: { node: TreeDto; depth: number }) => {
    const kids = childrenOf(node.memberCode);
    const isCollapsed = collapsed.has(node.memberCode);
    const isMe = node.memberCode === meCode;
    const hit = matches(node.memberCode, node.name);
    const dim = query !== '' && !hit;

    return (
      <div className="tn-group">
        <div className={`tree-node${isMe ? ' me' : ''}${dim ? ' dim' : ''}${hit ? ' hit' : ''}`}>
          <div className="tn-name">
            <span className="tn-avatar">{initials(node.name)}</span>
            {node.name}
            {isMe ? <span className="badge badge-blue no-dot">{t('team.me')}</span> : null}
          </div>
          <div className="tn-code">{node.memberCode} · {node.childCount}/3</div>
          <div className="tn-status"><StatusBadge status={node.status} /></div>
          <div className="tn-actions">
            {kids.length > 0 || hasChildren(node.memberCode) ? (
              <button
                className="btn btn-ghost btn-sm"
                onClick={() => {
                  setCollapsed((prev) => {
                    const next = new Set(prev);
                    if (next.has(node.memberCode)) next.delete(node.memberCode);
                    else next.add(node.memberCode);
                    return next;
                  });
                }}
              >
                {isCollapsed ? '+' : '−'}
              </button>
            ) : null}
            {node.memberCode !== rootCode ? (
              <button className="btn btn-secondary btn-sm" onClick={() => onSubtree(node.memberCode)}>
                {t('team.viewSubtree')}
              </button>
            ) : null}
          </div>
        </div>

        {kids.length > 0 && !isCollapsed ? (
          <div className="tn-kids">
            {kids.map((kid) => (
              <div className="tn-kid" key={kid.memberCode}>
                <NodeCard node={kid} depth={depth + 1} />
              </div>
            ))}
            {Array.from({ length: 3 - kids.length }).map((_, i) => (
              <div className="tn-kid" key={`empty-${i}`}>
                <div className="tree-node slot-empty">
                  <div className="slot-label">+ {t('team.slotOpen')}</div>
                </div>
              </div>
            ))}
          </div>
        ) : null}
      </div>
    );
  };

  const expandAll = () => setCollapsed(new Set());
  const collapseAll = () => setCollapsed(new Set(nodes.filter((n) => n.hasChildren).map((n) => n.memberCode)));

  const root = nodes.find((n) => n.memberCode === rootCode);

  if (!root) return null;

  return (
    <div className="card">
      <div className="tree-root">
        <div className="tree">
          <NodeCard node={root} depth={0} />
        </div>
      </div>
      <div className="card-pad">
        <div className="flex-between">
          <p className="hint" style={{ margin: 0 }}>{t('team.maxNote')}</p>
          <div className="row" style={{ gap: 8 }}>
            <button className="btn btn-ghost btn-sm" onClick={expandAll}>{t('team.expandAll')}</button>
            <button className="btn btn-ghost btn-sm" onClick={collapseAll}>{t('team.collapseAll')}</button>
          </div>
        </div>
      </div>
    </div>
  );
}

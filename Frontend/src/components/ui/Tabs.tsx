export interface TabItem<K extends string> {
  key: K;
  label: string;
  hidden?: boolean;
}

export function Tabs<K extends string>({ items, active, onChange }: { items: TabItem<K>[]; active: K; onChange: (key: K) => void }) {
  return (
    <div className="nxt-tabs" role="tablist">
      {items.filter((t) => !t.hidden).map((t) => (
        <button
          key={t.key}
          type="button"
          role="tab"
          aria-selected={t.key === active}
          className={`nxt-tab ${t.key === active ? 'is-active' : ''}`}
          onClick={() => onChange(t.key)}
        >
          {t.label}
        </button>
      ))}
    </div>
  );
}

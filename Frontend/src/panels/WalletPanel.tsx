import { useState, type FormEvent } from 'react';
import { FiArrowDownLeft, FiArrowUpRight, FiCreditCard } from 'react-icons/fi';
import { newIdempotencyKey, walletsApi } from '../api/platform';
import { getErrorMessage } from '../api/errors';
import { useAuth } from '../auth/useAuth';
import { Badge } from '../components/ui/Badge';
import { Card, CardHeader } from '../components/ui/Card';
import { DataTable } from '../components/ui/DataTable';
import { Field } from '../components/ui/Field';
import { Modal } from '../components/ui/Modal';
import { Pagination } from '../components/ui/Pagination';
import { StatCard } from '../components/ui/StatCard';
import { useApi } from '../hooks/useApi';
import { useToast } from '../toast/useToast';
import { Permissions } from '../types/permissions';
import { formatDate, formatMoney } from '../utils/format';

/** Site wallet: balance, append-only ledger and (platform) credit/debit. */
export function WalletPanel({ siteId, siteName }: { siteId: string; siteName?: string }) {
  const { hasPermission } = useAuth();
  const toast = useToast();
  const [page, setPage] = useState(1);
  const wallet = useApi((s) => walletsApi.get(siteId, s), [siteId]);
  const tx = useApi((s) => walletsApi.transactions(siteId, page, 20, s), [siteId, page]);
  const [mode, setMode] = useState<'credit' | 'debit' | null>(null);
  const [amount, setAmount] = useState('');
  const [reference, setReference] = useState('');
  const [description, setDescription] = useState('');
  const [key, setKey] = useState('');
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  const open = (m: 'credit' | 'debit') => { setMode(m); setAmount(''); setReference(''); setDescription(''); setErrors({}); setKey(newIdempotencyKey()); };

  const submit = async (e: FormEvent) => {
    e.preventDefault();
    const value = Number(amount);
    const errs: Record<string, string> = {};
    if (!Number.isFinite(value) || value < 0.01 || value > 10_000_000) errs.amount = 'Enter an amount between 0.01 and 10,000,000.';
    else if (Math.round(value * 100) !== value * 100) errs.amount = 'Use at most two decimal places.';
    if (mode === 'debit' && !reference.trim()) errs.reference = 'A reason is required for manual deductions.';
    setErrors(errs);
    if (Object.keys(errs).length || !mode) return;
    setSaving(true);
    try {
      if (mode === 'credit') await walletsApi.credit(siteId, { amount: value, reference: reference.trim() || null, description: description.trim() || null }, key);
      else await walletsApi.debit(siteId, { amount: value, reference: reference.trim(), description: description.trim() || null }, key);
      toast.success(mode === 'credit' ? 'Balance added.' : 'Amount deducted.');
      setMode(null);
      wallet.reload();
      setPage(1);
      tx.reload();
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setSaving(false);
    }
  };

  const currency = wallet.data?.currency ?? 'INR';
  return (
    <div className="d-grid gap-3">
      <div className="row g-3">
        <div className="col-12 col-md-6 col-xl-4">
          <StatCard label={siteName ? `Wallet balance · ${siteName}` : 'Wallet balance'} value={formatMoney(wallet.data?.balance, currency)} icon={<FiCreditCard />} loading={wallet.loading && !wallet.data}
            hint={wallet.data && !wallet.data.isActive ? 'Wallet is inactive' : 'Prepaid balance used for SMS and usage charges'} />
        </div>
        {(hasPermission(Permissions.WalletCredit) || hasPermission(Permissions.WalletDebit)) && (
          <div className="col-12 col-md-6 col-xl-8 d-flex flex-wrap align-items-center gap-2">
            {hasPermission(Permissions.WalletCredit) && <button type="button" className="btn btn-primary btn-icon" onClick={() => open('credit')}><FiArrowDownLeft /> Add balance</button>}
            {hasPermission(Permissions.WalletDebit) && <button type="button" className="btn btn-light btn-icon" onClick={() => open('debit')}><FiArrowUpRight /> Manual deduction</button>}
          </div>
        )}
      </div>
      <Card>
        <CardHeader title="Transactions" subtitle="Append-only ledger. Entries are never edited or deleted." />
        <DataTable
          rows={tx.data?.items}
          loading={tx.loading}
          error={tx.error}
          onRetry={tx.reload}
          rowKey={(t) => t.id}
          empty={{ title: 'No transactions yet', icon: <FiCreditCard /> }}
          mobileTitle={(t) => <span className={`fw-semibold ${t.type === 'Credit' ? 'text-success' : 'text-danger'}`}>{t.type === 'Credit' ? '+' : '−'}{formatMoney(t.amount, currency)}</span>}
          mobileBadge={(t) => <Badge tone={t.type === 'Credit' ? 'success' : 'danger'}>{t.type}</Badge>}
          columns={[
            { key: 'date', header: 'Date', render: (t) => formatDate(t.createdAt) },
            { key: 'type', header: 'Type', hideOnMobile: true, render: (t) => <Badge tone={t.type === 'Credit' ? 'success' : 'danger'}>{t.type}</Badge> },
            { key: 'amount', header: 'Amount', align: 'end', hideOnMobile: true, render: (t) => <span className={`font-tabular fw-semibold ${t.type === 'Credit' ? 'text-success' : 'text-danger'}`}>{t.type === 'Credit' ? '+' : '−'}{formatMoney(t.amount, currency)}</span> },
            { key: 'after', header: 'Balance after', align: 'end', render: (t) => <span className="font-tabular">{formatMoney(t.balanceAfter, currency)}</span> },
            { key: 'ref', header: 'Reference', render: (t) => <div><div>{t.reference ?? '—'}</div>{t.description && <div className="nxt-cell-secondary">{t.description}</div>}</div> },
          ]}
        />
        {tx.data && <Pagination page={page} pageSize={20} totalCount={tx.data.totalCount} onPageChange={setPage} />}
      </Card>
      <Modal
        open={mode !== null}
        size="sm"
        title={mode === 'credit' ? 'Add wallet balance' : 'Manual deduction'}
        subtitle={siteName}
        onClose={() => setMode(null)}
        busy={saving}
        footer={<><button type="button" className="btn btn-light" onClick={() => setMode(null)} disabled={saving}>Cancel</button><button type="submit" form="wallet-form" className={`btn ${mode === 'debit' ? 'btn-danger' : 'btn-primary'}`} disabled={saving}>{saving ? 'Processing…' : mode === 'credit' ? 'Add balance' : 'Deduct'}</button></>}
      >
        <form id="wallet-form" onSubmit={submit} noValidate className="d-grid gap-3">
          <Field label={`Amount (${currency})`} required error={errors.amount}>
            {(id, inv) => <input id={id} inputMode="decimal" className={`form-control ${inv ? 'is-invalid' : ''}`} value={amount} onChange={(e) => setAmount(e.target.value)} placeholder="0.00" />}
          </Field>
          <Field label={mode === 'debit' ? 'Reason' : 'Reference'} required={mode === 'debit'} error={errors.reference} hint={mode === 'credit' ? 'e.g. payment receipt or invoice number' : undefined}>
            {(id, inv) => <input id={id} className={`form-control ${inv ? 'is-invalid' : ''}`} value={reference} maxLength={100} onChange={(e) => setReference(e.target.value)} />}
          </Field>
          <Field label="Note">
            {(id) => <input id={id} className="form-control" value={description} maxLength={300} onChange={(e) => setDescription(e.target.value)} />}
          </Field>
        </form>
      </Modal>
    </div>
  );
}

import { useState } from 'react';
import { FiDownload, FiUpload } from 'react-icons/fi';
import { subscribersApi } from '../api/hotspot';
import { getErrorMessage } from '../api/errors';
import { Switch } from '../components/ui/Field';
import { Modal } from '../components/ui/Modal';
import { Notice } from '../components/ui/States';
import { useToast } from '../toast/useToast';
import { typeInfo, type ImportResult, type SubscriberType } from '../types/hotspot';

export function ImportModal({ organizationId, type, open, onClose, onImported }: { organizationId: string; type: SubscriberType; open: boolean; onClose: () => void; onImported: () => void }) {
  const toast = useToast();
  const [file, setFile] = useState<File | null>(null);
  const [update, setUpdate] = useState(false);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<ImportResult | null>(null);
  const info = typeInfo(type);

  const close = () => { setFile(null); setResult(null); setUpdate(false); onClose(); };

  const run = async () => {
    if (!file) { toast.warning('Choose an .xlsx file first.'); return; }
    if (file.size > 10 * 1024 * 1024) { toast.error('The file is larger than 10 MB.'); return; }
    setBusy(true);
    try {
      const r = await subscribersApi.importXlsx(organizationId, type, file, update);
      setResult(r);
      if (r.created + r.updated > 0) { toast.success(`${r.created} created, ${r.updated} updated.`); onImported(); }
    } catch (err) {
      toast.error(getErrorMessage(err));
    } finally {
      setBusy(false);
    }
  };

  const template = async () => {
    try { await subscribersApi.downloadTemplate(organizationId, type); }
    catch (err) { toast.error(getErrorMessage(err)); }
  };

  return (
    <Modal open={open} title={`Import ${info.plural.toLowerCase()} from Excel`} onClose={close} busy={busy}
      footer={<><button type="button" className="btn btn-light" onClick={close} disabled={busy}>{result ? 'Close' : 'Cancel'}</button>{!result && <button type="button" className="btn btn-primary btn-icon" onClick={run} disabled={busy || !file}><FiUpload /> {busy ? 'Importing…' : 'Import'}</button>}</>}>
      {!result ? (
        <div className="d-grid gap-3">
          <Notice>Download the template, fill one row per {info.label.toLowerCase()}, and upload it here. Up to 5,000 rows per file. Groups in the “Group” column are created automatically.</Notice>
          <div><button type="button" className="btn btn-light btn-icon" onClick={template}><FiDownload /> Download template</button></div>
          <div>
            <label className="form-label" htmlFor="import-file">Excel file (.xlsx)</label>
            <input id="import-file" type="file" className="form-control" accept=".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" onChange={(e) => setFile(e.target.files?.[0] ?? null)} />
          </div>
          <Switch label="Update existing records" description="Rows whose identifier already exists update that subscriber instead of being skipped." checked={update} onChange={setUpdate} />
        </div>
      ) : (
        <div className="d-grid gap-3">
          <div className="d-flex flex-wrap gap-2">
            <span className="nxt-badge success">{result.created} created</span>
            <span className="nxt-badge info">{result.updated} updated</span>
            <span className="nxt-badge neutral">{result.skipped} skipped</span>
            <span className={`nxt-badge ${result.errors.length ? 'danger' : 'neutral'}`}>{result.errors.length} with issues</span>
          </div>
          {result.errors.length > 0 && (
            <div className="border rounded-3" style={{ maxHeight: 260, overflow: 'auto' }}>
              <table className="nxt-table">
                <thead><tr><th>Row</th><th>Issue</th></tr></thead>
                <tbody>{result.errors.map((e, i) => <tr key={i}><td>{e.row}</td><td>{e.message}</td></tr>)}</tbody>
              </table>
            </div>
          )}
        </div>
      )}
    </Modal>
  );
}

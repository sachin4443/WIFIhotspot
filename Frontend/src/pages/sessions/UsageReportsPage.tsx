import { useState } from 'react';
import { FiActivity, FiClock, FiDownloadCloud, FiUploadCloud, FiUsers } from 'react-icons/fi';
import { reportsApi } from '../../api/sessions';
import { BarChart, LineChart } from '../../components/charts/Charts';
import { PageHeader } from '../../components/common/PageHeader';
import { Card, CardBody, CardHeader } from '../../components/ui/Card';
import { DataTable } from '../../components/ui/DataTable';
import { StatCard } from '../../components/ui/StatCard';
import { EmptyState, ErrorState } from '../../components/ui/States';
import { useApi } from '../../hooks/useApi';
import type { UsageRow } from '../../types/sessions';
import { formatBytes, formatNumber, formatSeconds } from '../../utils/format';
import { ScopeFilters, daysAgo, endOfDay, isoDate, startOfDay } from './ScopeFilters';

const PRESETS = [{ label: '7 days', days: 6 }, { label: '30 days', days: 29 }, { label: '90 days', days: 89 }];
const GB = 1024 ** 3;

function UsageTable({ title, rows, labelHeader }: { title: string; rows: UsageRow[] | undefined; labelHeader: string }) {
  return (
    <Card className="h-100">
      <CardHeader title={title} />
      <DataTable
        rows={rows}
        rowKey={(r) => r.key}
        empty={{ title: 'No usage in this period' }}
        columns={[
          { key: 'label', header: labelHeader, render: (r) => <span className="nxt-cell-primary">{r.label}</span> },
          { key: 'sessions', header: 'Sessions', render: (r) => formatNumber(r.sessions) },
          { key: 'data', header: 'Data', render: (r) => formatBytes(r.bytes) },
          { key: 'time', header: 'Online time', render: (r) => formatSeconds(r.sessionSeconds) },
        ]}
      />
    </Card>
  );
}

/** Usage reports from RADIUS accounting (usage is attributed to the day a session started). */
export function UsageReportsPage() {
  const [orgId, setOrgId] = useState('');
  const [siteId, setSiteId] = useState('');
  const [from, setFrom] = useState(daysAgo(29));
  const [to, setTo] = useState(isoDate(new Date()));
  const report = useApi((s) => reportsApi.usage({ organizationId: orgId, siteId, from: startOfDay(from)!, to: endOfDay(to)! }, s), [orgId, siteId, from, to]);
  const r = report.data;
  const labels = r?.daily.map((d) => new Date(`${d.date}T00:00:00`).toLocaleDateString(undefined, { day: 'numeric', month: 'short' })) ?? [];
  const hasData = !!r && r.sessions > 0;

  return (
    <>
      <PageHeader title="Usage reports" subtitle="Sessions, users, data and online time from RADIUS accounting." />
      <Card className="mb-3">
        <div className="nxt-toolbar border-0">
          <div className="d-flex flex-wrap gap-1">
            {PRESETS.map((p) => (
              <button key={p.label} type="button" className={`btn btn-sm ${from === daysAgo(p.days) && to === isoDate(new Date()) ? 'btn-primary' : 'btn-light'}`}
                onClick={() => { setFrom(daysAgo(p.days)); setTo(isoDate(new Date())); }}>{p.label}</button>
            ))}
          </div>
          <input type="date" className="form-control" style={{ width: 'auto', minWidth: 140 }} value={from} max={to} onChange={(e) => setFrom(e.target.value)} aria-label="From" />
          <input type="date" className="form-control" style={{ width: 'auto', minWidth: 140 }} value={to} min={from} onChange={(e) => setTo(e.target.value)} aria-label="To" />
          <ScopeFilters orgId={orgId} onOrgChange={setOrgId} siteId={siteId} onSiteChange={setSiteId} />
        </div>
      </Card>
      {report.error ? <Card><ErrorState message={report.error} onRetry={report.reload} /></Card> : (
        <>
          <div className="row g-3 mb-3">
            <div className="col-6 col-xl-3"><StatCard label="Sessions" icon={<FiActivity />} tone="indigo" loading={!r} value={formatNumber(r?.sessions)} /></div>
            <div className="col-6 col-xl-3"><StatCard label="Unique users" icon={<FiUsers />} tone="green" loading={!r} value={formatNumber(r?.uniqueUsers)} /></div>
            <div className="col-6 col-xl-3"><StatCard label="Downloaded" icon={<FiDownloadCloud />} tone="blue" loading={!r} value={formatBytes(r?.outputBytes)} hint={r ? `Uploaded ${formatBytes(r.inputBytes)}` : undefined} /></div>
            <div className="col-6 col-xl-3"><StatCard label="Online time" icon={<FiClock />} tone="amber" loading={!r} value={formatSeconds(r?.sessionSeconds)} /></div>
          </div>
          {r && !hasData ? (
            <Card><EmptyState icon={<FiUploadCloud />} title="No accounting data in this period" text="Reports fill in once routers send RADIUS accounting to the NXT RADIUS server." /></Card>
          ) : (
            <>
              <div className="row g-3 mb-3">
                <div className="col-12 col-xl-7">
                  <Card className="h-100">
                    <CardHeader title="Data transfer per day" subtitle="Download and upload, GB" />
                    <CardBody>{r ? <LineChart ariaLabel="Data per day" labels={labels} valueFormatter={(v) => `${v.toFixed(2)} GB`}
                      series={[{ label: 'Download', data: r.daily.map((d) => d.outputBytes / GB) }, { label: 'Upload', data: r.daily.map((d) => d.inputBytes / GB) }]} /> : <div className="nxt-chart nxt-skeleton" />}</CardBody>
                  </Card>
                </div>
                <div className="col-12 col-xl-5">
                  <Card className="h-100">
                    <CardHeader title="Unique users per day" />
                    <CardBody>{r ? <BarChart ariaLabel="Unique users per day" labels={labels} series={[{ label: 'Users', data: r.daily.map((d) => d.uniqueUsers) }]} /> : <div className="nxt-chart nxt-skeleton" />}</CardBody>
                  </Card>
                </div>
              </div>
              <div className="row g-3">
                <div className="col-12 col-xl-6"><UsageTable title="Top subscribers by data" rows={r?.topSubscribers} labelHeader="Subscriber" /></div>
                <div className="col-12 col-xl-6"><UsageTable title="By subscriber type" rows={r?.bySubscriberType} labelHeader="Type" /></div>
                <div className="col-12 col-xl-6"><UsageTable title="By site" rows={r?.bySite} labelHeader="Site" /></div>
                <div className="col-12 col-xl-6"><UsageTable title="By NAS device" rows={r?.byNas} labelHeader="NAS" /></div>
              </div>
            </>
          )}
        </>
      )}
    </>
  );
}

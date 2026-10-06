import { useState } from 'react'
import { Link, useOutletContext } from 'react-router-dom'
import { useOperations } from '../../context/OperationsContext'
import { getSyncOverview } from '../../data/mock/syncState'
import { sensorReadings } from '../../data/mock/sensorReadings'
import { sensorChannels } from '../../data/mock/sensorChannels'
import { sensorTypes } from '../../data/mock/sensorTypes'
import { displayTime } from '../../data/mock/alertSelectors'
import { PageHeader, StatCard, SectionCard, DataTable, StatusBadge, ProgressBar, EmptyState } from '../../components/app/ui'
import ManagementDialog from '../../components/app/ManagementDialog'
import '../../styles/sync.css'

const flow = [
  ['Sensor Node', 'Measures farm conditions'], ['Mobile Gateway', 'Collects sensor readings'], ['Local Storage', 'Retains data without Internet'], ['Pending Sync', 'Waits for a connection'],
  ['Server', 'Receives the upload'], ['Duplicate Check', 'Checks stable source keys'], ['Validation', 'Rejects invalid payloads'], ['Central Database', 'Stores valid, unique readings'],
]

export default function SyncPage() {
  const { operations, retrySync } = useOperations()
  const { currentFarm, farmWorkspace } = useOutletContext()
  const [selectedId, setSelectedId] = useState(null)
  const [error, setError] = useState('')
  const data = getSyncOverview(operations, currentFarm.id, farmWorkspace)
  const selected = data.batches.find(({ id }) => id === selectedId)
  const retry = (id) => { try { retrySync(id); setError('') } catch (cause) { setError(cause.message) } }
  const retryButton = (batch) => batch.status === 'FAILED' ? <button className="app-ui-button app-ui-button--primary" onClick={() => retry(batch.id)}>Retry Sync</button> : batch.activeRetry ? <span role="status">Retry in progress…</span> : null
  const kpis = [['Pending Batches', data.counts.PENDING, 'Stored locally'], ['Syncing', data.counts.SYNCING, 'Upload in progress'], ['Successful', data.counts.SUCCESS, 'Accepted or duplicate'], ['Partial', data.counts.PARTIAL, 'Contains rejected records'], ['Failed', data.counts.FAILED, 'Needs attention'], ['Rejected Records', data.rejected, 'Not stored from these payloads']]
  return <div className="app-ui sync-page">
    <PageHeader eyebrow="OFFLINE DATA COLLECTION" title="Data Sync" description={`${currentFarm.name} · From gateway storage to validated farm data.`} />
    <section className="sync-kpis" aria-label="Synchronization indicators">{kpis.map(([label, value, description]) => <StatCard key={label} label={label} value={value} description={description} />)}</section>
    <div className="sync-offline-panel"><span className="sync-storage-icon" aria-hidden="true">▤</span><div><h2>Collected offline. Kept on the gateway.</h2><p>{data.localQueueCount} unique readings remain in local upload queues; {data.unacknowledgedCount} await server acknowledgement. A lost Internet connection does not discard collected data.</p><small>Queued copies can remain after another upload succeeds. Duplicate checks prevent them from being stored twice.</small></div><StatusBadge status={data.localQueueCount ? 'PENDING' : 'SUCCESS'} label={data.localQueueCount ? 'Local queue retained' : 'No queued readings'} /></div>
    <SectionCard title="How Offline Synchronization Works" description="The gateway communicates through the server. Valid, unique records reach the central database.">
      <ol className="sync-flow" tabIndex={0} aria-label="Offline synchronization process">{flow.map(([title, description], index) => <li key={title} data-local={index === 2 || index === 3}><span className="sync-flow-step">{index + 1}</span><strong>{title}</strong><p>{description}</p>{index < flow.length - 1 && <span className="sync-flow-arrow" aria-hidden="true">→</span>}</li>)}</ol>
      <p className="sync-flow-note">Invalid payloads stay rejected. Duplicate records acknowledge an existing reading and do not create a second database entry.</p>
    </SectionCard>
    {error && !selected && <p className="sync-error" role="alert">{error}</p>}
    <SectionCard title="Sync Batches" description="Times use Vietnam time (UTC+7). Retry actions and results are local demo state and reset on refresh.">
      <DataTable caption="Sync batches" rows={data.batches} onRowClick={(batch) => { setSelectedId(batch.id); setError('') }} columns={[
        { key: 'code', header: 'Batch Code', render: (code, batch) => <button className="sync-link" onClick={() => { setSelectedId(batch.id); setError('') }}>{code}</button> },
        { key: 'gateway', header: 'Gateway', render: (gateway) => gateway?.code ?? 'Unknown' },
        { key: 'mission', header: 'Mission', render: (mission) => mission ? <Link className="sync-link" to={`/app/missions/${mission.id}`}>{mission.code}</Link> : 'Unassigned' },
        { key: 'recordCount', header: 'Total Records' }, { key: 'acceptedCount', header: 'Accepted' }, { key: 'duplicateCount', header: 'Duplicates' }, { key: 'rejectedCount', header: 'Rejected' },
        { key: 'retryCount', header: 'Retry Count' }, { key: 'status', header: 'Status', render: (status) => <StatusBadge status={status} /> },
        { key: 'startedAt', header: 'Started At', render: displayTime }, { key: 'completedAt', header: 'Completed At', render: displayTime },
        { key: 'actions', header: 'Actions', render: (_, batch) => retryButton(batch) },
      ]} emptyState={<EmptyState title="No sync batches for this farm" description="Gateway upload batches will appear here after sensor collection." />} />
    </SectionCard>
    {selected && <ManagementDialog title={`${selected.code} · Batch Details`} drawer onClose={() => { setSelectedId(null); setError('') }}><div className="management-drawer-body sync-detail">
      <div className="sync-detail-heading"><StatusBadge status={selected.status} />{retryButton(selected)}</div>
      <p role="status">{selected.activeRetry ? 'Mock upload in progress. Checking duplicates and validating records…' : selected.status === 'PENDING' ? 'Waiting in gateway storage. Records have not been acknowledged by the server.' : selected.status === 'SUCCESS' ? 'All records acknowledged. Accepted records were stored; duplicates reuse existing readings.' : selected.status === 'SYNCING' ? 'Recorded upload snapshot: remaining records are processing or waiting.' : 'Review the last error and per-record outcomes before retrying.'}</p>
      {error && <p className="sync-error" role="alert">{error}</p>}
      {selected.description && <p className="sync-muted">{selected.description}</p>}
      <dl className="management-detail-grid">{[['Gateway', `${selected.gateway?.code} · ${selected.gateway?.name}`], ['Mission', selected.mission?.name ?? 'Unassigned'], ['Total records', selected.recordCount], ['Accepted count', selected.acceptedCount], ['Duplicate count', selected.duplicateCount], ['Rejected count', selected.rejectedCount], ['Awaiting outcome', selected.pendingCount], ['Retry count', selected.retryCount], ['Started', displayTime(selected.startedAt)], ['Completed', displayTime(selected.completedAt)]].map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}</dl>
      <ProgressBar label="Records with an outcome" value={selected.recordCount - selected.pendingCount} max={selected.recordCount} />
      <section><h3>Last Error</h3><p className={selected.lastError ? 'sync-error' : 'sync-muted'}>{selected.lastError ?? 'None recorded.'}</p>{selected.rejectedCount > 0 && <p className="sync-muted">Retrying does not fix invalid signatures or out-of-range measurements. These rejected records will remain rejected.</p>}</section>
      <section><h3>Records</h3><div className="sync-record-list">{selected.records.map((record) => {
        const reading = sensorReadings.find(({ id }) => id === record.readingId)
        const channel = sensorChannels.find(({ id }) => id === record.sensorChannelId)
        const node = operations.sensorNodes.find(({ id }) => id === channel?.sensorNodeId)
        const type = sensorTypes.find(({ id }) => id === channel?.sensorTypeId)
        const receipt = operations.syncReceipts.find(({ sourceRecordKey }) => sourceRecordKey === record.sourceRecordKey)
        return <article key={record.id} className="sync-record"><header><strong>{node?.deviceCode ?? 'Unknown sensor'} · {type?.name ?? 'Unknown channel'}</strong><StatusBadge status={record.status} /></header><p>{reading ? `${reading.value} ${type?.unit ?? ''} · Collected ${displayTime(reading.collectedAt)}` : 'Invalid payload; no canonical sensor reading.'}</p><code>{record.sourceRecordKey}</code>{record.errorMessage && <p className="sync-record-error">{record.errorCode}: {record.errorMessage}</p>}{receipt && ['PENDING', 'PROCESSING'].includes(record.status) && <small>Already acknowledged by another batch; this queued copy will be detected as a duplicate.</small>}</article>
      })}</div></section>
      <section><h3>Retry History</h3>{selected.history.length ? <ol className="sync-history">{selected.history.map((attempt) => <li key={attempt.id}><header><strong>{attempt.id === 0 ? 'Initial upload' : `Retry ${attempt.id}`}</strong><StatusBadge status={attempt.status} /></header><p>{attempt.actorName} · Started {displayTime(attempt.startedAt)}</p><p>{attempt.completedAt ? `Completed ${displayTime(attempt.completedAt)}` : 'Awaiting completion'}</p>{attempt.acceptedCount !== undefined && <small>{attempt.acceptedCount} accepted · {attempt.duplicateCount} duplicates · {attempt.rejectedCount} rejected</small>}{attempt.error && <p className="sync-record-error">{attempt.error}</p>}</li>)}</ol> : <p className="sync-muted">No upload attempt yet. Data remains in local storage.</p>}</section>
    </div></ManagementDialog>}
  </div>
}

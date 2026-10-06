import { useState } from 'react'
import { Link, useParams, useOutletContext } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { useOperations } from '../../context/OperationsContext'
import { getOperationalSensorDetails } from '../../data/mock/operationsSelectors'
import { displayTime } from '../../data/mock/alertSelectors'
import { PERMISSIONS } from '../../config/permissions'
import { PageHeader, SectionCard, StatusBadge, BatteryIndicator, DataTable, EmptyState } from '../../components/app/ui'
import EquipmentDialog from '../../components/app/EquipmentDialog'
import '../../styles/operations.css'

export default function SensorDetailPage() {
  const { id } = useParams()
  const { operations, changeOperations } = useOperations()
  const { farmWorkspace, currentFarm } = useOutletContext()
  const { can } = useAuth()
  const [mode, setMode] = useState(null)
  const [notice, setNotice] = useState('')
  const node = /^\d+$/.test(id) ? getOperationalSensorDetails(Number(id), operations, farmWorkspace) : null
  const manage = can(PERMISSIONS.SENSORS_MANAGE)
  if (!node) return <EmptyState title="Sensor not found" description="This sensor is not registered in the current demo." actions={<Link className="app-ui-button" to="/app/sensors">Back to sensors</Link>} />
  return <div className="app-ui operations-page">
    <PageHeader eyebrow={node.deviceCode} title={node.name} description={`${node.farm?.name} · ${node.zone?.name}`} breadcrumbs={<Link to="/app/sensors">Sensor Nodes</Link>} actions={manage && <><button className="app-ui-button" onClick={() => setMode('edit')}>Edit Sensor</button><button className="app-ui-button" onClick={() => setMode('assign')}>Assign to Zone</button><button className="app-ui-button" onClick={() => setMode('toggle')}>{node.isActive ? 'Disable' : 'Enable'}</button></>} />
    {notice && <p className="management-success" role="status">{notice}</p>}
    <SectionCard title="Sensor identity" actions={<StatusBadge status={node.displayStatus} label={!node.isActive ? 'Disabled' : undefined} />}>
      <dl className="operations-details">{[['Device Code', node.deviceCode], ['Zone', node.zone?.name], ['Protocol', node.protocol], ['MAC Address', node.macAddress || 'Not recorded'], ['Serial Number', node.serialNumber || 'Not recorded'], ['Model', node.model || 'Not recorded'], ['Last Seen', displayTime(node.lastSeenAt)], ['Last Collected', displayTime(node.lastCollectedAt)]].map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}<div><dt>Battery</dt><dd><BatteryIndicator value={node.batteryPercent} /></dd></div></dl>
    </SectionCard>
    <SectionCard title="Sensor channels & latest readings" description="Most recent collected value per channel, including data waiting for synchronization.">
      <DataTable caption="Sensor channels and latest readings" rows={node.channels} columns={[
        { key: 'name', header: 'Channel' }, { key: 'type', header: 'Unit', render: (type) => type.unit },
        { key: 'latestReading', header: 'Latest Reading', render: (reading) => reading?.value ?? 'No readings' },
        { key: 'collected', header: 'Collected At', render: (_, row) => displayTime(row.latestReading?.collectedAt) },
        { key: 'receipt', header: 'Data Location', render: (_, row) => row.latestReading ? <StatusBadge status={row.latestReading.receivedAt ? 'SUCCESS' : 'PENDING'} label={row.latestReading.receivedAt ? 'Synchronized' : 'Buffered locally'} /> : 'No readings' },
      ]} emptyState={<EmptyState title="No channels registered" description="Channel configuration and readings will appear after device provisioning." />} />
    </SectionCard>
    <SectionCard title="Collection history" description="Mission attempts, including timeouts and successful retries. Historical gateway exports are not mission attempts.">
      <DataTable caption="Sensor collection history" rows={node.history} columns={[
        { key: 'mission', header: 'Mission', render: (mission) => can(PERMISSIONS.MISSIONS_VIEW) ? <Link className="operations-link" to={`/app/missions/${mission.id}`}>{mission.code}</Link> : mission.code },
        { key: 'gateway', header: 'Gateway', render: (gateway) => gateway?.code }, { key: 'attemptNo', header: 'Attempt' },
        { key: 'finishedAt', header: 'Time', render: displayTime }, { key: 'status', header: 'Result', render: (status) => <StatusBadge status={status} /> },
        { key: 'recordsReceived', header: 'Readings' }, { key: 'errorMessage', header: 'Details', render: (value) => value ?? 'Collection completed' },
      ]} />
    </SectionCard>
    {manage && mode && <EquipmentDialog key={`${node.id}-${mode}`} kind="sensor" record={node} mode={mode} workspace={farmWorkspace} operations={operations} currentFarm={currentFarm} onClose={() => setMode(null)} onSave={(values) => { changeOperations({ kind: 'sensor', id: node.id, values }, farmWorkspace); setNotice('Sensor updated for this demo session.'); setMode(null) }} />}
  </div>
}

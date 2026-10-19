import ChannelHistory from '../../components/app/ChannelHistory'
import SensorConfiguration from '../../components/app/SensorConfiguration'
import ApiState from '../../components/app/ApiState'
import { useState } from 'react'
import { Link, useParams, useOutletContext } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { useOperations } from '../../context/OperationsContext'
import { getOperationalSensorDetails } from '../../services/api/selectors'
import { displayTime } from '../../utils/display'
import { PERMISSIONS } from '../../config/permissions'
import { PageHeader, SectionCard, StatusBadge, BatteryIndicator, DataTable, EmptyState } from '../../components/app/ui'
import EquipmentDialog from '../../components/app/EquipmentDialog'
import '../../styles/operations.css'

export default function SensorDetailPage() {
  const { id } = useParams()
  const { operations, changeOperations, loading, error, reload } = useOperations()
  const { farmWorkspace, currentFarm } = useOutletContext()
  const { can } = useAuth()
  const [mode, setMode] = useState(null)
  const [notice, setNotice] = useState('')
  const node = /^\d+$/.test(id) ? getOperationalSensorDetails(Number(id), operations, farmWorkspace) : null
  const manage = can(PERMISSIONS.SENSORS_MANAGE)
  if (loading || error) return <ApiState loading={loading} error={error} reload={reload} />
  if (!node) return <EmptyState title="Sensor not found" description="This sensor is not available in your account." actions={<Link className="app-ui-button" to="/app/sensors">Back to sensors</Link>} />
  return <div className="app-ui operations-page">
    <ApiState loading={loading} error={error} reload={reload} />
    <PageHeader eyebrow={node.deviceCode} title={node.name} description={`${node.farm?.name} · ${node.zone?.name}`} breadcrumbs={<Link to="/app/sensors">Sensor Nodes</Link>} actions={manage && <><button className="app-ui-button" onClick={() => setMode('edit')}>Edit Sensor</button><button className="app-ui-button" onClick={() => setMode('assign')}>Assign to Zone</button><button className="app-ui-button" onClick={() => setMode('toggle')}>{node.isActive ? 'Disable' : 'Enable'}</button></>} />
    {notice && <p className="management-success" role="status">{notice}</p>}
    <SectionCard title="Sensor identity" actions={<StatusBadge status={node.displayStatus} label={!node.isActive ? 'Disabled' : undefined} />}>
      <dl className="operations-details">{[['Device Code', node.deviceCode], ['Zone', node.zone?.name], ['Protocol', node.protocol ?? 'Not supplied'], ['MAC Address', node.macAddress || 'Not recorded'], ['Serial Number', node.serialNumber || 'Not recorded'], ['Model', node.model || 'Not recorded'], ['Last Seen', displayTime(node.lastSeenAt)], ['Last Collected', displayTime(node.lastCollectedAt)]].map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value}</dd></div>)}<div><dt>Battery</dt><dd><BatteryIndicator value={node.batteryPercent} /></dd></div></dl>
    </SectionCard>
    <SectionCard title="Sensor channels & latest readings" description="Most recent collected value per channel, from synchronized backend measurements.">
      <DataTable caption="Sensor channels and latest readings" rows={node.channels} columns={[
        { key: 'channelCode', header: 'Channel' }, { key: 'type', header: 'Unit', render: (type) => type.unit },
        { key: 'latestReading', header: 'Latest Reading', render: (reading) => reading?.value ?? 'No readings' },
        { key: 'collected', header: 'Collected At', render: (_, row) => displayTime(row.latestReading?.collectedAt) },
        { key: 'receipt', header: 'Data Location', render: (_, row) => row.latestReading ? <StatusBadge status={row.latestReading.receivedAt ? 'SUCCESS' : 'PENDING'} label={row.latestReading.receivedAt ? 'Synchronized' : 'Buffered locally'} /> : 'No readings' },
      ]} emptyState={<EmptyState title="No channels registered" description="Channel configuration and readings will appear after device provisioning." />} />
    </SectionCard>
    <ChannelHistory channels={node.channels} />
    <SensorConfiguration node={node} types={operations.sensorTypes} reload={reload} />
    <SectionCard title="Collection history" description="Collection attempts are recorded per mission.">
      <p>Open a mission to inspect sensor attempts, retries, and errors. <Link to="/app/missions">View missions</Link></p>
    </SectionCard>
    {manage && mode && <EquipmentDialog key={`${node.id}-${mode}`} kind="sensor" record={node} mode={mode} workspace={farmWorkspace} operations={operations} currentFarm={currentFarm} onClose={() => setMode(null)} onSave={async (values) => { await changeOperations({ kind: 'sensor', id: node.id, action: mode === 'toggle' ? 'toggle' : 'save', values }, farmWorkspace); setNotice('Sensor updated successfully.'); setMode(null) }} />}
  </div>
}


import { useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { useOperations } from '../../context/OperationsContext'
import { PERMISSIONS } from '../../config/permissions'
import { displayTime } from '../../data/mock/alertSelectors'
import { getGatewayLastSync } from '../../data/mock/operationsSelectors'
import { PageHeader, DataTable, StatusBadge, BatteryIndicator, FilterBar } from '../../components/app/ui'
import EquipmentDialog from '../../components/app/EquipmentDialog'
import '../../styles/operations.css'

export default function DevicesPage() {
  const { operations, changeOperations } = useOperations()
  const { farmWorkspace, currentFarm } = useOutletContext()
  const { can } = useAuth()
  const manage = can(PERMISSIONS.DEVICES_MANAGE)
  const [tab, setTab] = useState('uav')
  const [farmId, setFarmId] = useState('')
  const [dialog, setDialog] = useState(null)
  const [notice, setNotice] = useState('')
  const open = (kind, record, mode = 'edit') => setDialog({ kind, record, mode })
  const actions = { key: 'actions', header: 'Actions', render: (_, record) => manage ? <div className="management-row-actions"><button onClick={() => open(tab, record)}>Edit</button>{tab === 'gateway' && <button onClick={() => open('gateway', record, 'assign')}>{record.uavId ? 'Assign / Unassign' : 'Assign Gateway'}</button>}</div> : 'View only' }
  const uavColumns = [
    { key: 'code', header: 'Code' }, { key: 'name', header: 'Name' }, { key: 'platform', header: 'Platform', render: (platform) => platform.replaceAll('_', ' ') }, { key: 'model', header: 'Model' },
    { key: 'status', header: 'Status', render: (status) => <StatusBadge status={status} /> }, { key: 'batteryPercent', header: 'Battery', render: (value) => <BatteryIndicator value={value} /> },
    { key: 'gpsSupport', header: 'GPS Support' }, { key: 'telemetrySupport', header: 'Telemetry Support' }, { key: 'lastSeenAt', header: 'Last Seen', render: displayTime }, actions,
  ]
  const gatewayColumns = [
    { key: 'code', header: 'Code' }, { key: 'name', header: 'Name' }, { key: 'uavId', header: 'Assigned UAV', render: (id) => operations.uavs.find((uav) => uav.id === id)?.code ?? 'Unassigned' },
    { key: 'gatewayType', header: 'Device Type', render: (type) => type.replaceAll('_', ' ') }, { key: 'softwareVersion', header: 'Software Version' },
    { key: 'status', header: 'Status', render: (status) => <StatusBadge status={status} /> }, { key: 'lastSeenAt', header: 'Last Seen', render: displayTime },
    { key: 'lastSync', header: 'Last Sync', render: (_, row) => displayTime(getGatewayLastSync(row.id, operations.syncBatches)) }, actions,
  ]
  const rows = (tab === 'uav' ? operations.uavs : operations.gateways).filter((record) => !farmId || record.farmId === Number(farmId))
  return <div className="app-ui operations-page">
    <PageHeader eyebrow="EQUIPMENT INVENTORY" title="UAVs & Gateways" description="Maintain equipment identity, capabilities, and gateway assignments." actions={manage ? <button className="app-ui-button app-ui-button--primary" onClick={() => open(tab)}>+ Register {tab === 'uav' ? 'UAV' : 'Gateway'}</button> : <StatusBadge status="INFO" label="Read-only access" />} />
    <p className="operations-note">Local demo inventory · Status, battery, and contact times are observed values, not flight commands. Last Sync is the latest completed batch with accepted readings.</p>
    {notice && <p className="management-success" role="status">{notice}</p>}
    <div role="tablist" aria-label="Device categories" className="operations-tabs" onKeyDown={(event) => {
      if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return
      event.preventDefault()
      const next = event.key === 'Home' ? 'uav' : event.key === 'End' ? 'gateway' : tab === 'uav' ? 'gateway' : 'uav'
      setTab(next); event.currentTarget.querySelector(`#devices-${next}-tab`).focus()
    }}>{[['uav', 'UAVs'], ['gateway', 'Mobile Gateways']].map(([key, label]) => <button key={key} id={`devices-${key}-tab`} role="tab" aria-selected={tab === key} aria-controls={`devices-${key}-panel`} tabIndex={tab === key ? 0 : -1} onClick={() => setTab(key)}>{label}</button>)}</div>
    <FilterBar label="Device filters"><label>Farm<select aria-label="Farm" value={farmId} onChange={(event) => setFarmId(event.target.value)}><option value="">All farms</option>{farmWorkspace.farms.map((farm) => <option key={farm.id} value={farm.id}>{farm.name}</option>)}</select></label></FilterBar>
    <div role="tabpanel" id={`devices-${tab}-panel`} aria-labelledby={`devices-${tab}-tab`}>
      <DataTable caption={tab === 'uav' ? 'UAV inventory' : 'Mobile gateway inventory'} className="operations-inventory-table" rows={rows} columns={tab === 'uav' ? uavColumns : gatewayColumns} footer={`${rows.length} ${tab === 'uav' ? 'UAVs' : 'gateways'} · Times in Vietnam (UTC+7)`} />
    </div>
    {manage && dialog && <EquipmentDialog {...dialog} workspace={farmWorkspace} operations={operations} currentFarm={currentFarm} onClose={() => setDialog(null)} onSave={(values) => { changeOperations({ kind: dialog.kind, id: dialog.record?.id, action: dialog.mode === 'assign' ? 'assign' : 'save', values }, farmWorkspace); setNotice('Equipment saved for this demo session.'); setDialog(null) }} />}
  </div>
}

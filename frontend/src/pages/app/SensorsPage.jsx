import { useState } from 'react'
import { Link, useNavigate, useOutletContext } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { useOperations } from '../../context/OperationsContext'
import { PERMISSIONS } from '../../config/permissions'
import { SENSOR_PROTOCOLS } from '../../data/mock/operationsState'
import { getOperationalSensor } from '../../data/mock/operationsSelectors'
import { displayTime } from '../../data/mock/alertSelectors'
import { PageHeader, DataTable, FilterBar, StatusBadge, BatteryIndicator } from '../../components/app/ui'
import EquipmentDialog from '../../components/app/EquipmentDialog'
import '../../styles/operations.css'

export default function SensorsPage() {
  const { operations, changeOperations } = useOperations()
  const { farmWorkspace, currentFarm } = useOutletContext()
  const { can } = useAuth()
  const navigate = useNavigate()
  const manage = can(PERMISSIONS.SENSORS_MANAGE)
  const [filters, setFilters] = useState({ farm: '', zone: '', status: '', protocol: '', search: '' })
  const [dialog, setDialog] = useState(null)
  const [notice, setNotice] = useState('')
  const rows = operations.sensorNodes.map((node) => getOperationalSensor(node, farmWorkspace)).filter((node) =>
    (!filters.farm || node.farm?.id === Number(filters.farm)) && (!filters.zone || node.zoneId === Number(filters.zone))
    && (!filters.status || node.displayStatus === filters.status) && (!filters.protocol || node.protocol === filters.protocol)
    && `${node.deviceCode} ${node.name} ${node.serialNumber} ${node.macAddress}`.toLowerCase().includes(filters.search.trim().toLowerCase()))
  const select = (key, label, options) => <label>{label}<select aria-label={label} value={filters[key]} onChange={(event) => setFilters({ ...filters, [key]: event.target.value, ...(key === 'farm' ? { zone: '' } : {}) })}><option value="">All {label.toLowerCase()}</option>{options.map(([id, name]) => <option key={id} value={id}>{name}</option>)}</select></label>
  const open = (record, mode = 'edit') => setDialog({ record, mode })
  const save = (values) => { changeOperations({ kind: 'sensor', id: dialog.record?.id, values }, farmWorkspace); setNotice('Sensor saved for this demo session.'); setDialog(null) }
  return <div className="app-ui operations-page">
    <PageHeader eyebrow="SENSOR NETWORK" title="Sensor Nodes" description="Registration, availability, and collection status across your farms." actions={manage ? <button className="app-ui-button app-ui-button--primary" onClick={() => open()}>+ Register Sensor</button> : <StatusBadge status="INFO" label="Read-only access" />} />
    <p className="operations-note">Enabled sensors are eligible for collection. Disabled sensors retain their readings. Times use Vietnam time (UTC+7).</p>
    {notice && <p role="status" className="management-success">{notice}</p>}
    <FilterBar label="Sensor filters" actions={<button type="button" className="app-ui-button" onClick={() => setFilters({ farm: '', zone: '', status: '', protocol: '', search: '' })}>Reset filters</button>}>
      {select('farm', 'Farm', farmWorkspace.farms.map(({ id, name }) => [id, name]))}{select('zone', 'Zone', farmWorkspace.zones.filter(({ farmId }) => !filters.farm || farmId === Number(filters.farm)).map(({ id, name }) => [id, name]))}
      {select('status', 'Status', [['ONLINE', 'Online'], ['OFFLINE', 'Offline'], ['INACTIVE', 'Disabled']])}{select('protocol', 'Protocol', SENSOR_PROTOCOLS.map((value) => [value, value]))}
      <label>Search<input type="search" value={filters.search} placeholder="Code, name, MAC or serial" onChange={(event) => setFilters({ ...filters, search: event.target.value })} /></label>
    </FilterBar>
    <DataTable caption="Sensor nodes" rows={rows} onRowClick={(node) => navigate(`/app/sensors/${node.id}`)} columns={[
      { key: 'deviceCode', header: 'Device Code', render: (code, row) => <Link className="operations-link" to={`/app/sensors/${row.id}`}>{code}</Link> },
      { key: 'name', header: 'Name' }, { key: 'zone', header: 'Zone', render: (zone) => zone?.name ?? 'Unassigned' }, { key: 'protocol', header: 'Protocol' },
      { key: 'batteryPercent', header: 'Battery', render: (value) => <BatteryIndicator value={value} /> }, { key: 'displayStatus', header: 'Status', render: (status) => <StatusBadge status={status} label={status === 'INACTIVE' ? 'Disabled' : undefined} /> },
      { key: 'lastSeenAt', header: 'Last Seen', render: displayTime }, { key: 'lastCollectedAt', header: 'Last Collected', render: displayTime },
      { key: 'actions', header: 'Actions', render: (_, row) => manage ? <div className="management-row-actions"><button onClick={() => open(row)}>Edit Sensor</button><button onClick={() => open(row, 'assign')}>Assign to Zone</button><button onClick={() => open(row, 'toggle')}>{row.isActive ? 'Disable' : 'Enable'}</button></div> : <Link className="operations-link" to={`/app/sensors/${row.id}`}>View</Link> },
    ]} footer={`${rows.length} of ${operations.sensorNodes.length} sensors`} />
    {manage && dialog && <EquipmentDialog kind="sensor" {...dialog} workspace={farmWorkspace} operations={operations} currentFarm={currentFarm} onSave={save} onClose={() => setDialog(null)} />}
  </div>
}

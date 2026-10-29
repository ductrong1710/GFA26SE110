import ApiState from '../../components/app/ApiState'
import { useState } from 'react'
import { Link, useNavigate, useOutletContext } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { useOperations } from '../../context/OperationsContext'
import { PERMISSIONS } from '../../config/permissions'
import { SENSOR_PROTOCOLS } from '../../services/api/selectors'
import { getOperationalSensor } from '../../services/api/selectors'
import { displayTime } from '../../utils/display'
import { useUiText } from '../../utils/useUiText'
import { PageHeader, DataTable, FilterBar, StatusBadge, BatteryIndicator } from '../../components/app/ui'
import EquipmentDialog from '../../components/app/EquipmentDialog'
import '../../styles/operations.css'

export default function SensorsPage() {
  const t = useUiText({
    'SENSOR NETWORK': 'MẠNG LƯỚI CẢM BIẾN',
    'Sensor Nodes': 'Thiết bị cảm biến',
    'Registration, availability, and collection status across your farms.': 'Thông tin đăng ký, khả dụng và trạng thái thu thập trên các trang trại của bạn.',
    '+ Register Sensor': '+ Đăng ký cảm biến',
    'Read-only access': 'Quyền chỉ xem',
    'Enabled sensors are eligible for collection. Disabled sensors retain their readings. Times use Vietnam time (UTC+7).': 'Cảm biến được bật có thể thu thập dữ liệu. Cảm biến bị tắt vẫn lưu giữ số liệu. Thời gian theo giờ Việt Nam (UTC+7).',
    'Sensor saved successfully.': 'Đã lưu cảm biến thành công.',
    'Sensor filters': 'Bộ lọc cảm biến',
    'Reset filters': 'Đặt lại bộ lọc',
    Farm: 'Trang trại',
    Zone: 'Khu vực',
    Status: 'Trạng thái',
    Protocol: 'Giao thức',
    'All farm': 'Tất cả trang trại',
    'All zone': 'Tất cả khu vực',
    'All status': 'Tất cả trạng thái',
    'All protocol': 'Tất cả giao thức',
    Online: 'Trực tuyến',
    Offline: 'Ngoại tuyến',
    Disabled: 'Đã tắt',
    Search: 'Tìm kiếm',
    'Code, name, MAC or serial': 'Mã, tên, MAC hoặc số sê-ri',
    'Sensor nodes': 'Thiết bị cảm biến',
    'Device Code': 'Mã thiết bị',
    Name: 'Tên',
    'Unassigned': 'Chưa phân công',
    Battery: 'Pin',
    'Last Seen': 'Lần cuối được ghi nhận',
    'Last Collected': 'Lần thu thập gần nhất',
    Actions: 'Thao tác',
    'Edit Sensor': 'Chỉnh sửa cảm biến',
    'Assign to Zone': 'Phân công vào khu vực',
    Disable: 'Tắt',
    Enable: 'Bật',
    View: 'Xem',
    'X of Y sensors': 'X trên tổng số Y cảm biến',
  })
  const { operations, changeOperations, loading, error, reload } = useOperations()
  const { farmWorkspace, currentFarm } = useOutletContext()
  const { can } = useAuth()
  const navigate = useNavigate()
  const manage = can(PERMISSIONS.SENSORS_MANAGE)
  const create = manage || can(PERMISSIONS.SENSORS_CREATE)
  const [filters, setFilters] = useState({ farm: '', zone: '', status: '', protocol: '', search: '' })
  const [dialog, setDialog] = useState(null)
  const [notice, setNotice] = useState('')
  const rows = operations.sensorNodes.map((node) => getOperationalSensor(node, farmWorkspace)).filter((node) =>
    (!filters.farm || node.farm?.id === Number(filters.farm)) && (!filters.zone || node.zoneId === Number(filters.zone))
    && (!filters.status || node.displayStatus === filters.status) && (!filters.protocol || node.protocol === filters.protocol)
    && `${node.deviceCode} ${node.name} ${node.serialNumber} ${node.macAddress}`.toLowerCase().includes(filters.search.trim().toLowerCase()))
  const select = (key, label, options) => <label>{t(label)}<select aria-label={t(label)} value={filters[key]} onChange={(event) => setFilters({ ...filters, [key]: event.target.value, ...(key === 'farm' ? { zone: '' } : {}) })}><option value="">{t(`All ${label.toLowerCase()}`)}</option>{options.map(([id, name]) => <option key={id} value={id}>{name}</option>)}</select></label>
  const open = (record, mode = 'edit') => setDialog({ record, mode })
  const save = async (values) => { await changeOperations({ kind: 'sensor', id: dialog.record?.id, action: dialog.mode === 'toggle' ? 'toggle' : 'save', values }, farmWorkspace); setNotice(t('Sensor saved successfully.')); setDialog(null) }
  return <div className="app-ui operations-page">
    <ApiState loading={loading} error={error} reload={reload} />
    <PageHeader eyebrow={t('SENSOR NETWORK')} title={t('Sensor Nodes')} description={t('Registration, availability, and collection status across your farms.')} actions={create ? <button className="app-ui-button app-ui-button--primary" onClick={() => open()}>{t('+ Register Sensor')}</button> : <StatusBadge status="INFO" label={t('Read-only access')} />} />
    <p className="operations-note">{t('Enabled sensors are eligible for collection. Disabled sensors retain their readings. Times use Vietnam time (UTC+7).')}</p>
    {notice && <p role="status" className="management-success">{notice}</p>}
    <FilterBar label={t('Sensor filters')} actions={<button type="button" className="app-ui-button" onClick={() => setFilters({ farm: '', zone: '', status: '', protocol: '', search: '' })}>{t('Reset filters')}</button>}>
      {select('farm', 'Farm', farmWorkspace.farms.map(({ id, name }) => [id, name]))}{select('zone', 'Zone', farmWorkspace.zones.filter(({ farmId }) => !filters.farm || farmId === Number(filters.farm)).map(({ id, name }) => [id, name]))}
      {select('status', 'Status', [['ONLINE', t('Online')], ['OFFLINE', t('Offline')], ['INACTIVE', t('Disabled')]])}{select('protocol', 'Protocol', SENSOR_PROTOCOLS.map((value) => [value, value]))}
      <label>{t('Search')}<input type="search" value={filters.search} placeholder={t('Code, name, MAC or serial')} onChange={(event) => setFilters({ ...filters, search: event.target.value })} /></label>
    </FilterBar>
    <DataTable caption={t('Sensor nodes')} rows={rows} onRowClick={(node) => navigate(`/app/sensors/${node.id}`)} columns={[
      { key: 'deviceCode', header: t('Device Code'), render: (code, row) => <Link className="operations-link" to={`/app/sensors/${row.id}`}>{code}</Link> },
      { key: 'name', header: t('Name') }, { key: 'zone', header: t('Zone'), render: (zone) => zone?.name ?? t('Unassigned') }, { key: 'protocol', header: t('Protocol') },
      { key: 'batteryPercent', header: t('Battery'), render: (value) => <BatteryIndicator value={value} /> }, { key: 'displayStatus', header: t('Status'), render: (status) => <StatusBadge status={status} label={status === 'INACTIVE' ? t('Disabled') : undefined} /> },
      { key: 'lastSeenAt', header: t('Last Seen'), render: displayTime }, { key: 'lastCollectedAt', header: t('Last Collected'), render: displayTime },
      { key: 'actions', header: t('Actions'), render: (_, row) => manage ? <div className="management-row-actions"><button onClick={() => open(row)}>{t('Edit Sensor')}</button><button onClick={() => open(row, 'assign')}>{t('Assign to Zone')}</button><button onClick={() => open(row, 'toggle')}>{row.isActive ? t('Disable') : t('Enable')}</button></div> : <Link className="operations-link" to={`/app/sensors/${row.id}`}>{t('View')}</Link> },
    ]} footer={t('X of Y sensors').replace('X', rows.length).replace('Y', operations.sensorNodes.length)} />
    {dialog && (manage || (create && !dialog.record)) && <EquipmentDialog kind="sensor" {...dialog} workspace={farmWorkspace} operations={operations} currentFarm={currentFarm} onSave={save} onClose={() => setDialog(null)} />}
  </div>
}

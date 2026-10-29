import ChannelHistory from '../../components/app/ChannelHistory'
import UnresolvedReadings from '../../components/app/UnresolvedReadings'
import SensorConfiguration from '../../components/app/SensorConfiguration'
import ApiState from '../../components/app/ApiState'
import { useState } from 'react'
import { Link, useParams, useOutletContext } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { useOperations } from '../../context/OperationsContext'
import { getOperationalSensorDetails } from '../../services/api/selectors'
import { displayTime } from '../../utils/display'
import { useUiText } from '../../utils/useUiText'
import { PERMISSIONS } from '../../config/permissions'
import { PageHeader, SectionCard, StatusBadge, BatteryIndicator, DataTable, EmptyState } from '../../components/app/ui'
import EquipmentDialog from '../../components/app/EquipmentDialog'
import '../../styles/operations.css'

export default function SensorDetailPage() {
  const t = useUiText({
    'Sensor not found': 'Không tìm thấy cảm biến',
    'This sensor is not available in your account.': 'Cảm biến này không khả dụng trong tài khoản của bạn.',
    'Back to sensors': 'Quay lại danh sách cảm biến',
    'Sensor Nodes': 'Thiết bị cảm biến',
    'Edit Sensor': 'Chỉnh sửa cảm biến',
    'Assign to Zone': 'Phân công vào khu vực',
    Disable: 'Tắt',
    Enable: 'Bật',
    'Sensor identity': 'Thông tin cảm biến',
    Disabled: 'Đã tắt',
    'Device Code': 'Mã thiết bị',
    Zone: 'Khu vực',
    Protocol: 'Giao thức',
    'Not supplied': 'Chưa cung cấp',
    'MAC Address': 'Địa chỉ MAC',
    'Not recorded': 'Chưa ghi nhận',
    'Serial Number': 'Số sê-ri',
    Model: 'Mẫu thiết bị',
    'Last Seen': 'Lần cuối được ghi nhận',
    'Last Collected': 'Lần thu thập gần nhất',
    Battery: 'Pin',
    'Sensor channels & latest readings': 'Kênh cảm biến & số liệu mới nhất',
    'Most recent collected value per channel, from synchronized backend measurements.': 'Giá trị được thu thập gần nhất của mỗi kênh, từ các số liệu đã đồng bộ.',
    'Sensor channels and latest readings': 'Kênh cảm biến và số liệu mới nhất',
    Channel: 'Kênh',
    Unit: 'Đơn vị',
    'Latest Reading': 'Số liệu mới nhất',
    'No readings': 'Chưa có số liệu',
    'Collected At': 'Thời điểm thu thập',
    'Data Location': 'Vị trí dữ liệu',
    Synchronized: 'Đã đồng bộ',
    'Buffered locally': 'Đã lưu tạm cục bộ',
    'No channels registered': 'Chưa đăng ký kênh nào',
    'Channel configuration and readings will appear after device provisioning.': 'Cấu hình kênh và số liệu sẽ hiển thị sau khi thiết bị được khởi tạo.',
    'Collection history': 'Lịch sử thu thập',
    'Collection attempts are recorded per mission.': 'Các lần thử thu thập được ghi lại theo từng nhiệm vụ.',
    'Open a mission to inspect sensor attempts, retries, and errors.': 'Mở nhiệm vụ để xem các lần thử, thử lại và lỗi của cảm biến.',
    'View missions': 'Xem nhiệm vụ',
    'Sensor updated successfully.': 'Đã cập nhật cảm biến thành công.',
  })
  const { id } = useParams()
  const { operations, changeOperations, loading, error, reload } = useOperations()
  const { farmWorkspace, currentFarm } = useOutletContext()
  const { can } = useAuth()
  const [mode, setMode] = useState(null)
  const [notice, setNotice] = useState('')
  const node = /^\d+$/.test(id) ? getOperationalSensorDetails(Number(id), operations, farmWorkspace) : null
  const manage = can(PERMISSIONS.SENSORS_MANAGE)
  if (loading || error) return <ApiState loading={loading} error={error} reload={reload} />
  if (!node) return <EmptyState title={t('Sensor not found')} description={t('This sensor is not available in your account.')} actions={<Link className="app-ui-button" to="/app/sensors">{t('Back to sensors')}</Link>} />
  return <div className="app-ui operations-page">
    <ApiState loading={loading} error={error} reload={reload} />
    <PageHeader eyebrow={node.deviceCode} title={node.name} description={`${node.farm?.name} · ${node.zone?.name}`} breadcrumbs={<Link to="/app/sensors">{t('Sensor Nodes')}</Link>} actions={manage && <><button className="app-ui-button" onClick={() => setMode('edit')}>{t('Edit Sensor')}</button><button className="app-ui-button" onClick={() => setMode('assign')}>{t('Assign to Zone')}</button><button className="app-ui-button" onClick={() => setMode('toggle')}>{node.isActive ? t('Disable') : t('Enable')}</button></>} />
    {notice && <p className="management-success" role="status">{notice}</p>}
    <SectionCard title={t('Sensor identity')} actions={<StatusBadge status={node.displayStatus} label={!node.isActive ? t('Disabled') : undefined} />}>
      <dl className="operations-details">{[['Device Code', node.deviceCode], ['Zone', node.zone?.name], ['Protocol', node.protocol ?? t('Not supplied')], ['MAC Address', node.macAddress || t('Not recorded')], ['Serial Number', node.serialNumber || t('Not recorded')], ['Model', node.model || t('Not recorded')], ['Last Seen', displayTime(node.lastSeenAt)], ['Last Collected', displayTime(node.lastCollectedAt)]].map(([label, value]) => <div key={label}><dt>{t(label)}</dt><dd>{value}</dd></div>)}<div><dt>{t('Battery')}</dt><dd><BatteryIndicator value={node.batteryPercent} /></dd></div></dl>
    </SectionCard>
    <SectionCard title={t('Sensor channels & latest readings')} description={t('Most recent collected value per channel, from synchronized backend measurements.')}>
      <DataTable caption={t('Sensor channels and latest readings')} rows={node.channels} columns={[
        { key: 'channelCode', header: t('Channel') }, { key: 'type', header: t('Unit'), render: (type) => type.unit },
        { key: 'latestReading', header: t('Latest Reading'), render: (reading) => reading?.value ?? t('No readings') },
        { key: 'collected', header: t('Collected At'), render: (_, row) => displayTime(row.latestReading?.collectedAt) },
        { key: 'receipt', header: t('Data Location'), render: (_, row) => row.latestReading ? <StatusBadge status={row.latestReading.receivedAt ? 'SUCCESS' : 'PENDING'} label={row.latestReading.receivedAt ? t('Synchronized') : t('Buffered locally')} /> : t('No readings') },
      ]} emptyState={<EmptyState title={t('No channels registered')} description={t('Channel configuration and readings will appear after device provisioning.')} />} />
    </SectionCard>
    <ChannelHistory channels={node.channels} />
    <UnresolvedReadings key={node.id} nodeId={node.id} />
    <SensorConfiguration node={node} types={operations.sensorTypes} reload={reload} />
    <SectionCard title={t('Collection history')} description={t('Collection attempts are recorded per mission.')}>
      <p>{t('Open a mission to inspect sensor attempts, retries, and errors.')} <Link to="/app/missions">{t('View missions')}</Link></p>
    </SectionCard>
    {manage && mode && <EquipmentDialog key={`${node.id}-${mode}`} kind="sensor" record={node} mode={mode} workspace={farmWorkspace} operations={operations} currentFarm={currentFarm} onClose={() => setMode(null)} onSave={async (values) => { await changeOperations({ kind: 'sensor', id: node.id, action: mode === 'toggle' ? 'toggle' : 'save', values }, farmWorkspace); setNotice(t('Sensor updated successfully.')); setMode(null) }} />}
  </div>
}

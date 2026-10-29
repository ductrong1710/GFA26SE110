import FarmAccess from '../../components/app/FarmAccess'
import { useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { PERMISSIONS } from '../../config/permissions'
import { useOperations } from '../../context/OperationsContext'
import { useUiText } from '../../utils/useUiText'
import { PageHeader, SectionCard, EmptyState, StatusBadge } from '../../components/app/ui'
import FarmRecordDialog from '../../components/app/FarmRecordDialog'
import FarmZoneMap from '../../components/app/FarmZoneMap'
import '../../styles/admin-workspace.css'

export default function FarmsPage() {
  const t = useUiText({
    'FARM DIRECTORY': 'DANH SÁCH TRANG TRẠI',
    'Farms & Zones': 'Trang trại & Khu vực',
    'Explore your farms, growing areas, and sensor coverage.': 'Khám phá trang trại, khu vực canh tác và phạm vi cảm biến của bạn.',
    '+ Add Farm': '+ Thêm trang trại',
    '+ Add Zone': '+ Thêm khu vực',
    'Read-only access': 'Quyền chỉ xem',
    'Farms and zones from your authorized workspace.': 'Trang trại và khu vực trong không gian làm việc được cấp quyền.',
    'Farm saved successfully.': 'Đã lưu trang trại thành công.',
    'Zone saved successfully.': 'Đã lưu khu vực thành công.',
    'Farm hierarchy': 'Cấu trúc trang trại',
    farms: 'trang trại',
    zones: 'khu vực',
    'Farm and zone hierarchy': 'Cấu trúc trang trại và khu vực',
    'No farms yet': 'Chưa có trang trại',
    'Edit Farm': 'Chỉnh sửa trang trại',
    'Selected zone details': 'Chi tiết khu vực đã chọn',
    'Coverage and boundary information': 'Thông tin phạm vi và ranh giới',
    'Choose or add a growing area': 'Chọn hoặc thêm khu vực canh tác',
    'Edit Zone': 'Chỉnh sửa khu vực',
    Name: 'Tên',
    Description: 'Mô tả',
    'No description provided': 'Chưa có mô tả',
    'Sensor Count': 'Số lượng cảm biến',
    'Online / Offline Count': 'Số cảm biến trực tuyến / ngoại tuyến',
    online: 'trực tuyến',
    offline: 'ngoại tuyến',
    Coordinates: 'Tọa độ',
    'Not recorded': 'Chưa ghi nhận',
    'Boundary summary': 'Tóm tắt ranh giới',
    'Boundary not recorded': 'Chưa ghi nhận ranh giới',
    'No zones yet': 'Chưa có khu vực',
    'Zones and sensor coverage will appear here when configured.': 'Khu vực và phạm vi cảm biến sẽ hiển thị tại đây sau khi được thiết lập.',
    'No farm selected': 'Chưa chọn trang trại',
    'Add a farm to start organizing zones.': 'Thêm trang trại để bắt đầu sắp xếp các khu vực.',
    'This role has read-only access.': 'Vai trò này chỉ có quyền xem.',
  })
  const { currentFarm, selectFarm, farmWorkspace, changeFarmRecord } = useOutletContext()
  const { can } = useAuth()
  const { operations } = useOperations()
  const { sensorNodes } = operations
  const canManage = can(PERMISSIONS.FARMS_MANAGE)
  const [selectedZoneId, setSelectedZoneId] = useState(null)
  const [dialog, setDialog] = useState(null)
  const [notice, setNotice] = useState('')
  const { farms, zones } = farmWorkspace
  const farm = farms.find(({ id }) => id === currentFarm.id)
  const farmZones = zones.filter(({ farmId }) => farmId === farm?.id)
  const zone = farmZones.find(({ id }) => id === selectedZoneId) ?? farmZones[0]
  const zoneNodes = sensorNodes.filter(({ zoneId }) => zoneId === zone?.id)
  const openDialog = (kind, record) => { if (canManage) setDialog({ kind, record }) }
  const save = async (values) => {
    if (!canManage) throw new Error(t('This role has read-only access.'))
    const next = await changeFarmRecord({ kind: dialog.kind, id: dialog.record?.id, values })
    if (!dialog.record) {
      if (dialog.kind === 'farm') { selectFarm(next.farms.at(-1).id); setSelectedZoneId(null) }
      else { const added = next.zones.at(-1); selectFarm(added.farmId); setSelectedZoneId(added.id) }
    }
    setNotice(t(dialog.kind === 'farm' ? 'Farm saved successfully.' : 'Zone saved successfully.'))
    setDialog(null)
  }
  return <div className="app-ui farms-page">
    <PageHeader eyebrow={t('FARM DIRECTORY')} title={t('Farms & Zones')} description={t('Explore your farms, growing areas, and sensor coverage.')}
      actions={canManage ? <><button className="app-ui-button" onClick={() => openDialog('farm')}>{t('+ Add Farm')}</button><button className="app-ui-button app-ui-button--primary" disabled={!farm} onClick={() => openDialog('zone')}>{t('+ Add Zone')}</button></> : <StatusBadge status="INFO" label={t('Read-only access')} />} />
    <p className="admin-scope-note">{t('Farms and zones from your authorized workspace.')}</p>
    {notice && <p className="farm-success" role="status">{notice}</p>}
    <div className="farms-layout">
      <SectionCard title={t('Farm hierarchy')} description={`${farms.length} ${t('farms')} · ${zones.length} ${t('zones')}`} className="farm-hierarchy">
        <nav aria-label={t('Farm and zone hierarchy')}><ul>{farms.map((item) => <li key={item.id}>
          <button className="farm-tree-farm" aria-current={farm?.id === item.id ? 'true' : undefined} onClick={() => { selectFarm(item.id); setSelectedZoneId(null) }}>{item.name}<small>{zones.filter(({ farmId }) => farmId === item.id).length} {t('zones')}</small></button>
          <ul>{zones.filter(({ farmId }) => farmId === item.id).map((child) => <li key={child.id}><button className="farm-tree-zone" aria-current={zone?.id === child.id ? 'true' : undefined} onClick={() => { selectFarm(item.id); setSelectedZoneId(child.id) }}>{child.name}</button></li>)}</ul>
        </li>)}</ul></nav>
        {!farms.length && <EmptyState title={t('No farms yet')} />}
      </SectionCard>
      <div className="farm-detail-column">
        {farm ? <>
          <SectionCard title={farm.name} description={farm.description} actions={canManage && <button className="app-ui-button" onClick={() => openDialog('farm', farm)}>{t('Edit Farm')}</button>}>
            <FarmZoneMap farm={farm} zones={farmZones} selectedZoneId={zone?.id} onSelect={setSelectedZoneId} nodes={sensorNodes} />
          </SectionCard>
          <FarmAccess farm={farm} onChanged={() => changeFarmRecord({ kind: 'reload' })} />
          <SectionCard title={t('Selected zone details')} description={zone ? t('Coverage and boundary information') : t('Choose or add a growing area')} actions={canManage && zone && <button className="app-ui-button" onClick={() => openDialog('zone', zone)}>{t('Edit Zone')}</button>}>
            {zone ? <dl className="farm-detail-grid">
              <div><dt>{t('Name')}</dt><dd>{zone.name}</dd></div><div><dt>{t('Description')}</dt><dd>{zone.description || t('No description provided')}</dd></div>
              <div><dt>{t('Sensor Count')}</dt><dd>{zoneNodes.length}</dd></div><div><dt>{t('Online / Offline Count')}</dt><dd>{zoneNodes.filter(({ status }) => status === 'ONLINE').length} {t('online')} / {zoneNodes.filter(({ status }) => status === 'OFFLINE').length} {t('offline')}</dd></div>
              <div><dt>{t('Coordinates')}</dt><dd>{zone.latitude?.toFixed(5) ?? t('Not recorded')}, {zone.longitude?.toFixed(5) ?? t('Not recorded')}</dd></div><div><dt>{t('Boundary summary')}</dt><dd>{zone.boundarySummary || t('Boundary not recorded')}</dd></div>
            </dl> : <EmptyState title={t('No zones yet')} description={t('Zones and sensor coverage will appear here when configured.')} />}
          </SectionCard>
        </> : <EmptyState title={t('No farm selected')} description={t('Add a farm to start organizing zones.')} />}
      </div>
    </div>
    {canManage && dialog && <FarmRecordDialog key={`${dialog.kind}-${dialog.record?.id ?? 'new'}`} {...dialog} farm={farm} farms={farms} onSave={save} onClose={() => setDialog(null)} />}
  </div>
}

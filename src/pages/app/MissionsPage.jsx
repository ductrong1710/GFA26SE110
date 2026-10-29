import { useState } from 'react'
import { Link, useNavigate, useOutletContext } from 'react-router-dom'
import { useOperations } from '../../context/OperationsContext'
import { useAuth } from '../../context/AuthContext'
import { PERMISSIONS } from '../../config/permissions'
import { MISSION_STATUSES, missionPlanProgress } from '../../services/api/planning'
import { displayTime } from '../../utils/display'
import { useUiText } from '../../utils/useUiText'
import { PageHeader, FilterBar, DataTable, StatusBadge, ProgressBar, EmptyState } from '../../components/app/ui'
import '../../styles/missions.css'

const translations = {
  'COLLECTION OPERATIONS': 'HOẠT ĐỘNG THU THẬP',
  Missions: 'Nhiệm vụ',
  'Plan sensor collection and monitor mission outcomes.': 'Lập kế hoạch thu thập dữ liệu cảm biến và theo dõi kết quả nhiệm vụ.',
  '+ Create Mission': '+ Tạo nhiệm vụ',
  'Mission filters': 'Bộ lọc nhiệm vụ',
  'Reset filters': 'Đặt lại bộ lọc',
  All: 'Tất cả',
  Status: 'Trạng thái',
  Farm: 'Trang trại',
  UAV: 'UAV',
  'Date (Vietnam)': 'Ngày (Việt Nam)',
  Search: 'Tìm kiếm',
  'Mission code or name': 'Mã hoặc tên nhiệm vụ',
  'Mission Code': 'Mã nhiệm vụ',
  'Mission Name': 'Tên nhiệm vụ',
  Gateway: 'Trạm kết nối',
  Targets: 'Mục tiêu',
  'View mission details': 'Xem chi tiết nhiệm vụ',
  Progress: 'Tiến độ',
  Collection: 'Thu thập',
  'Unavailable in summary': 'Không có trong phần tóm tắt',
  Actions: 'Thao tác',
  'Resume Draft': 'Tiếp tục bản nháp',
  'View Details': 'Xem chi tiết',
  'Scheduled Time': 'Thời gian dự kiến',
  missions: 'nhiệm vụ',
  'Times in Vietnam (UTC+7)': 'Thời gian theo giờ Việt Nam (UTC+7)',
  'Authorized backend missions': 'Nhiệm vụ được máy chủ cấp quyền',
  'Loading…': 'Đang tải…',
  'Try again': 'Thử lại',
  'No missions found': 'Không tìm thấy nhiệm vụ',
  'No missions match the current filters.': 'Không có nhiệm vụ nào phù hợp với bộ lọc hiện tại.',
  'Not selected': 'Chưa chọn',
  Unassigned: 'Chưa phân công',
}

export default function MissionsPage() {
  const t = useUiText(translations)
  const { operations, startMissionPlan, loading, error, reload } = useOperations()
  const { farmWorkspace } = useOutletContext()
  const { can } = useAuth()
  const navigate = useNavigate()
  const create = can(PERMISSIONS.MISSIONS_CREATE)
  const blank = { status: '', farm: '', uav: '', date: '', search: '' }
  const [filters, setFilters] = useState(blank)
  const [actionError, setActionError] = useState('')
  const [pending, setPending] = useState(false)
  const edit = async (mission) => { setPending(true); setActionError(''); try { await startMissionPlan(mission); navigate('/app/missions/create') } catch (e) { setActionError(e.message) } finally { setPending(false) } }
  const rows = operations.missions.filter((mission) => (!filters.status || mission.status === filters.status)
    && (!filters.farm || mission.farmId === Number(filters.farm)) && (!filters.uav || mission.uavId === Number(filters.uav))
    && (!filters.date || (mission.scheduledStartAt && new Date(Date.parse(mission.scheduledStartAt) + 7 * 3600000).toISOString().slice(0, 10) === filters.date))
    && `${mission.code} ${mission.name}`.toLowerCase().includes(filters.search.trim().toLowerCase()))
  const select = (key, label, items) => <label>{t(label)}<select aria-label={t(label)} value={filters[key]} onChange={(event) => setFilters({ ...filters, [key]: event.target.value })}><option value="">{t('All')}</option>{items.map(([id, name]) => <option key={id} value={id}>{name}</option>)}</select></label>
  return <div className="app-ui missions-page">{loading ? <p role="status" className="operations-note">{t('Loading…')}</p> : error ? <div role="alert" className="management-error"><p>{error.message}</p>{reload && <button className="app-ui-button" onClick={reload}>{t('Try again')}</button>}</div> : null}{actionError && <p role="alert">{actionError}</p>}<PageHeader eyebrow={t('COLLECTION OPERATIONS')} title={t('Missions')} description={t('Plan sensor collection and monitor mission outcomes.')} actions={create && <button className="app-ui-button app-ui-button--primary" disabled={pending} onClick={() => edit()}>{t('+ Create Mission')}</button>} />
    <FilterBar label={t('Mission filters')} actions={<button className="app-ui-button" onClick={() => setFilters(blank)}>{t('Reset filters')}</button>}>
      {select('status', 'Status', MISSION_STATUSES.map((status) => [status, status.replaceAll('_', ' ')]))}
      {select('farm', 'Farm', farmWorkspace.farms.map(({ id, name }) => [id, name]))}
      {select('uav', 'UAV', operations.uavs.map(({ id, code }) => [id, code]))}
      <label>{t('Date (Vietnam)')}<input type="date" value={filters.date} onChange={(event) => setFilters({ ...filters, date: event.target.value })} /></label>
      <label>{t('Search')}<input type="search" placeholder={t('Mission code or name')} value={filters.search} onChange={(event) => setFilters({ ...filters, search: event.target.value })} /></label>
    </FilterBar>
    <DataTable caption={t('Missions')} rows={rows} onRowClick={(row) => navigate(`/app/missions/${row.id}`)} emptyState={<EmptyState title={t('No missions found')} description={t('No missions match the current filters.')} />} columns={[
      { key: 'code', header: t('Mission Code'), render: (value, row) => <Link to={`/app/missions/${row.id}`}>{value}</Link> },
      { key: 'name', header: t('Mission Name') }, { key: 'farmId', header: t('Farm'), render: (id) => farmWorkspace.farms.find((farm) => farm.id === id)?.name ?? t('Not selected') },
      { key: 'uavId', header: t('UAV'), render: (id) => operations.uavs.find((uav) => uav.id === id)?.code ?? t('Unassigned') },
      { key: 'gatewayId', header: t('Gateway'), render: (id) => operations.gateways.find((gateway) => gateway.id === id)?.code ?? t('Unassigned') },
      { key: 'targets', header: t('Targets'), render: (_, row) => { const p = missionPlanProgress(row, operations); return row.progress ? `${p.collectedTargets}/${p.totalTargets}` : t('View mission details') } },
      { key: 'progress', header: t('Progress'), render: (_, row) => row.progress ? <ProgressBar value={missionPlanProgress(row, operations).percent} label={t('Collection')} /> : t('Unavailable in summary') },
      { key: 'status', header: 'Status', render: (status) => <StatusBadge status={status} /> },
      { key: 'scheduledStartAt', header: t('Scheduled Time'), render: displayTime },
      { key: 'actions', header: t('Actions'), render: (_, row) => create && row.status === 'PENDING' ? <button className="app-ui-button" onClick={() => edit(row)}>{t('Resume Draft')}</button> : <Link to={`/app/missions/${row.id}`}>{t('View Details')}</Link> },
    ]} footer={`${rows.length} ${t('missions')} · ${t('Times in Vietnam (UTC+7)')} · ${t('Authorized backend missions')}`} />
  </div>
}

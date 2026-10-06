import { useState } from 'react'
import { Link, useNavigate, useOutletContext } from 'react-router-dom'
import { useOperations } from '../../context/OperationsContext'
import { useAuth } from '../../context/AuthContext'
import { PERMISSIONS } from '../../config/permissions'
import { MISSION_STATUSES, missionPlanProgress } from '../../data/mock/missionPlanning'
import { displayTime } from '../../data/mock/alertSelectors'
import { PageHeader, FilterBar, DataTable, StatusBadge, ProgressBar } from '../../components/app/ui'
import '../../styles/missions.css'

export default function MissionsPage() {
  const { operations, startMissionPlan } = useOperations()
  const { farmWorkspace } = useOutletContext()
  const { can } = useAuth()
  const navigate = useNavigate()
  const create = can(PERMISSIONS.MISSIONS_CREATE)
  const blank = { status: '', farm: '', uav: '', date: '', search: '' }
  const [filters, setFilters] = useState(blank)
  const edit = (mission) => { startMissionPlan(mission); navigate('/app/missions/create') }
  const rows = operations.missions.filter((mission) => (!filters.status || mission.status === filters.status)
    && (!filters.farm || mission.farmId === Number(filters.farm)) && (!filters.uav || mission.uavId === Number(filters.uav))
    && (!filters.date || (mission.scheduledStartAt && new Date(Date.parse(mission.scheduledStartAt) + 7 * 3600000).toISOString().slice(0, 10) === filters.date))
    && `${mission.code} ${mission.name}`.toLowerCase().includes(filters.search.trim().toLowerCase()))
  const select = (key, label, items) => <label>{label}<select aria-label={label} value={filters[key]} onChange={(event) => setFilters({ ...filters, [key]: event.target.value })}><option value="">All</option>{items.map(([id, name]) => <option key={id} value={id}>{name}</option>)}</select></label>
  return <div className="app-ui missions-page"><PageHeader eyebrow="COLLECTION OPERATIONS" title="Missions" description="Plan sensor collection and monitor mission outcomes." actions={create && <button className="app-ui-button app-ui-button--primary" onClick={() => edit()}>+ Create Mission</button>} />
    <FilterBar label="Mission filters" actions={<button className="app-ui-button" onClick={() => setFilters(blank)}>Reset filters</button>}>
      {select('status', 'Status', MISSION_STATUSES.map((status) => [status, status.replaceAll('_', ' ')]))}
      {select('farm', 'Farm', farmWorkspace.farms.map(({ id, name }) => [id, name]))}
      {select('uav', 'UAV', operations.uavs.map(({ id, code }) => [id, code]))}
      <label>Date (Vietnam)<input type="date" value={filters.date} onChange={(event) => setFilters({ ...filters, date: event.target.value })} /></label>
      <label>Search<input type="search" placeholder="Mission code or name" value={filters.search} onChange={(event) => setFilters({ ...filters, search: event.target.value })} /></label>
    </FilterBar>
    <DataTable caption="Missions" rows={rows} onRowClick={(row) => navigate(`/app/missions/${row.id}`)} columns={[
      { key: 'code', header: 'Mission Code', render: (value, row) => <Link to={`/app/missions/${row.id}`}>{value}</Link> },
      { key: 'name', header: 'Mission Name' }, { key: 'farmId', header: 'Farm', render: (id) => farmWorkspace.farms.find((farm) => farm.id === id)?.name ?? 'Not selected' },
      { key: 'uavId', header: 'UAV', render: (id) => operations.uavs.find((uav) => uav.id === id)?.code ?? 'Unassigned' },
      { key: 'gatewayId', header: 'Gateway', render: (id) => operations.gateways.find((gateway) => gateway.id === id)?.code ?? 'Unassigned' },
      { key: 'targets', header: 'Targets', render: (_, row) => { const p = missionPlanProgress(row, operations); return `${p.collectedTargets}/${p.totalTargets}` } },
      { key: 'progress', header: 'Progress', render: (_, row) => <ProgressBar value={missionPlanProgress(row, operations).percent} label="Collection" /> },
      { key: 'status', header: 'Status', render: (status) => <StatusBadge status={status} /> },
      { key: 'scheduledStartAt', header: 'Scheduled Time', render: displayTime },
      { key: 'actions', header: 'Actions', render: (_, row) => create && row.status === 'DRAFT' ? <button className="app-ui-button" onClick={() => edit(row)}>Resume Draft</button> : <Link to={`/app/missions/${row.id}`}>View Details</Link> },
    ]} footer={`${rows.length} missions · Times in Vietnam (UTC+7) · Changes last for this demo session`} />
  </div>
}

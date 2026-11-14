import { useState } from 'react'
import { useAuth } from '../../context/AuthContext'
import { PERMISSIONS as P } from '../../config/permissions'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import ApiState from '../../components/app/ApiState'
import CultivationLog, { CultivationActivityForm } from '../../components/app/CultivationLog'
import CropSeasonComparison from '../../components/app/CropSeasonComparison'
import CropSeasonCompletion from '../../components/app/CropSeasonCompletion'
import CropSeasonForm from '../../components/app/CropSeasonForm'
import CropReportForm from '../../components/app/CropReportForm'
import CropSupportDetail from '../../components/app/CropSupportDetail'
import { DataTable, PageHeader, SectionCard, StatusBadge, TablePagination } from '../../components/app/ui'

export default function CropSeasonsPage() {
  const { can, user } = useAuth()
  const [selected, setSelected] = useState([])
  const [page, setPage] = useState(1)
  const [id, setId] = useState(null)
  const [form, setForm] = useState(null)
  const [notice, setNotice] = useState('')
  const [support, setSupport] = useState(null)
  const [supportBusy, setSupportBusy] = useState(false)
  const list = useApiQuery(`seasons:${page}`, signal => api.request('/api/crop-seasons', { query: { page }, signal }))
  const detail = useApiQuery(`season:${id}`, signal => api.request(`/api/crop-seasons/${id}`, { signal }), id !== null)
  const season = detail.data
  function saved(row) {
    setForm(null)
    setSelected([])
    setId(row.id)
    if (row.id === id) detail.replaceData(row)
    list.reload()
    setNotice('Season saved.')
  }
  return <>
    <PageHeader title="Crop seasons" description="Track crops and results for each growing season." />
    {can(P.CROP_SEASONS_MANAGE) && !form && <button onClick={() => setForm('create')}>Create season</button>}
    {(form === 'create' || form === 'edit') && <CropSeasonForm key={form} season={form === 'edit' ? season : null} onSaved={saved} onCancel={() => setForm(null)} />}
    {notice && <p role="status">{notice}</p>}
    <ApiState loading={list.loading} error={list.error} reload={list.reload} />
    <DataTable caption="Crop seasons" rows={list.data?.items ?? []} loading={list.loading} columns={[
      { key: 'name', header: 'Season' }, { key: 'crop', header: 'Crop' }, { key: 'farmId', header: 'Farm' },
      { key: 'status', header: 'Status', render: value => <StatusBadge status={value} /> },
      ...(can(P.CROP_SEASONS_MANAGE) ? [{ key: 'compare', header: 'Compare', render: (_, row) => row.ownerId === user.id &&
        <input type="checkbox" aria-label={`Compare season #${row.id}`} checked={selected.includes(row.id)}
          disabled={selected.length >= 50 && !selected.includes(row.id)}
          onChange={event => setSelected(event.target.checked ? [...selected, row.id] : selected.filter(id => id !== row.id))} /> }] : []),
      { key: 'open', header: 'Details', render: (_, row) => <button disabled={Boolean(form) || supportBusy} onClick={() => { setId(row.id); setNotice(''); setSupport(null) }}>Open season #{row.id}</button> },
    ]} footer={list.data?.pagination.totalItems > 0 && <TablePagination page={page} totalPages={list.data.pagination.totalPages} pageSize={list.data.pagination.pageSize}
      totalItems={list.data.pagination.totalItems} onPageChange={setPage} disabled={list.loading} label="Crop seasons table pagination" />} />
    {selected.length > 0 && <p>{selected.length} selected · Choose 2–50 seasons to compare. <button onClick={() => setSelected([])}>Clear selection</button></p>}
    {can(P.CROP_SEASONS_MANAGE) && selected.length >= 2 && <CropSeasonComparison ids={selected} />}
    <ApiState loading={detail.loading} error={detail.error} reload={detail.reload} />
    {season && <SectionCard title={season.name}>
      <dl>{Object.entries({ Crop: season.crop, Farm: season.farmId, Zone: season.zoneId ?? 'Whole farm', Status: season.status,
        'Area (ha)': season.areaHectares, 'Crop age (days)': season.cropAgeDays, 'Start date': season.startDate,
        'Expected end date': season.expectedEndDate, Notes: season.notes, 'End date': season.endDate, 'Yield (kg)': season.yieldKg,
        'Total cost (VND)': season.totalCostVnd, 'Revenue (VND)': season.revenueVnd, 'Completion notes': season.completionNotes })
        .map(([label, value]) => <div key={label}><dt>{label}</dt><dd>{value ?? '—'}</dd></div>)}</dl>
      {!form && can(P.CROP_SEASONS_MANAGE) && season.ownerId === user.id && season.status === 'ACTIVE' && <><button onClick={() => setForm('edit')}>Edit season</button>
        <button onClick={() => setForm('activity')}>Record activity</button>
        <button onClick={() => setForm('complete')}>Complete season</button></>}
      {form === 'activity' && <CultivationActivityForm season={season} onSaved={saved} onCancel={() => setForm(null)} />}
      {!form && can(P.CROP_SUPPORT_CREATE) && season.ownerId === user.id && <button disabled={supportBusy} onClick={() => setForm('report')}>Report abnormal crop condition</button>}
      {form === 'report' && <><CropReportForm key={season.id} initialSeason={season} busy={supportBusy} onBusyChange={setSupportBusy}
        onSaved={row => { setSupport(row); setForm(null); setNotice('Crop report submitted. The season remains unchanged.') }} />
        <button disabled={supportBusy} onClick={() => setForm(null)}>Cancel report</button></>}
      {support && <CropSupportDetail key={`support:${support.id}`} report={support} onSaved={setSupport} busy={supportBusy} onBusyChange={setSupportBusy}
        onReload={async () => { try { setSupport(await api.request(`/api/crop-condition-reports/${support.id}`)) } catch (error) { setNotice(error.message) } }} />}
      <CultivationLog key={`journal:${season.id}`} season={season} />
      {form === 'complete' && <CropSeasonCompletion season={season} onSaved={saved} onCancel={() => setForm(null)} />}
    </SectionCard>}
  </>
}

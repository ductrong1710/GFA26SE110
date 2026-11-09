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
import { DataTable, PageHeader, SectionCard, StatusBadge } from '../../components/app/ui'

export default function CropSeasonsPage() {
  const { can, user } = useAuth()
  const [selected, setSelected] = useState([])
  const [page, setPage] = useState(1)
  const [id, setId] = useState(null)
  const [form, setForm] = useState(null)
  const [notice, setNotice] = useState('')
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
      { key: 'open', header: 'Details', render: (_, row) => <button disabled={Boolean(form)} onClick={() => { setId(row.id); setNotice('') }}>Open season #{row.id}</button> },
    ]} footer={<div><button disabled={page === 1} onClick={() => setPage(page - 1)}>Previous seasons</button> Page {page} <button disabled={page >= (list.data?.pagination?.totalPages ?? 0)} onClick={() => setPage(page + 1)}>Next seasons</button></div>} />
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
      <CultivationLog key={season.id} season={season} />
      {form === 'complete' && <CropSeasonCompletion season={season} onSaved={saved} onCancel={() => setForm(null)} />}
    </SectionCard>}
  </>
}

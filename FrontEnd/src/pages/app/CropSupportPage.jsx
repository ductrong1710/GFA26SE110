import { useState } from 'react'
import { useAuth } from '../../context/AuthContext'
import { PERMISSIONS as P } from '../../config/permissions'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import CropReportForm from '../../components/app/CropReportForm'
import CropSupportDetail from '../../components/app/CropSupportDetail'
import ApiState from '../../components/app/ApiState'
import { DataTable, PageHeader, StatusBadge } from '../../components/app/ui'
import { displayTime } from '../../utils/display'

export default function CropSupportPage() {
  const { can } = useAuth()
  const [page, setPage] = useState(1)
  const [id, setId] = useState(null)
  const [busy, setBusy] = useState(false)
  const [notice, setNotice] = useState('')
  const query = useApiQuery(`crop-reports:${page}`, signal => api.request('/api/crop-condition-reports', { query: { page }, signal }))
  const detail = useApiQuery(`crop-report:${id}`, signal => api.request(`/api/crop-condition-reports/${id}`, { signal }), id !== null)
  function saved(row) { setId(row.id); if (row.id === id) detail.replaceData(row); query.reload(); setNotice('Crop report saved.') }
  return <>
    <PageHeader title="Crop condition support" description="Report symptoms and consult an agricultural engineer." />
    {can(P.CROP_SUPPORT_CREATE) && <CropReportForm onSaved={saved} busy={busy} onBusyChange={setBusy} />}
    {notice && <p role="status">{notice}</p>}
    <ApiState loading={detail.loading} error={detail.error} reload={detail.reload} />
    {detail.data && <CropSupportDetail key={`${detail.data.id}:${detail.data.revision}`} report={detail.data} onSaved={saved} onReload={detail.reload} onBusyChange={setBusy} busy={busy} />}
    <ApiState loading={query.loading} error={query.error} reload={query.reload} />
    <DataTable caption="Crop reports" rows={query.data?.items ?? []} loading={query.loading} columns={[
      { key: 'id', header: 'Report' }, { key: 'farmId', header: 'Farm' }, { key: 'crop', header: 'Crop' },
      { key: 'description', header: 'Symptoms' }, { key: 'reportedAt', header: 'Reported', render: displayTime },
      { key: 'status', header: 'Status', render: value => <StatusBadge status={value} /> },
      { key: 'action', header: 'Action', render: (_, row) => <button disabled={busy} onClick={() => { setId(row.id); setNotice('') }}>Open crop report #{row.id}</button> },
    ]} footer={<div className="management-row-actions"><button disabled={query.loading || page === 1} onClick={() => setPage(page - 1)}>Previous reports</button>
      <span>Page {page}</span><button disabled={query.loading || page * 20 >= (query.data?.pagination.totalItems ?? 0)} onClick={() => setPage(page + 1)}>Next reports</button></div>} />
  </>
}

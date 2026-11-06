import ServiceJobAssignmentForm from '../../components/app/ServiceJobAssignmentForm'
import { useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { useAuth } from '../../context/AuthContext'
import { PERMISSIONS as P } from '../../config/permissions'
import ApiState from '../../components/app/ApiState'
import EvidenceImages from '../../components/app/EvidenceImages'
import { DataTable, PageHeader, SectionCard, StatusBadge } from '../../components/app/ui'
import { displayTime } from '../../utils/display'

function IncidentForm({ onSaved }) {
  const [params] = useSearchParams()
  const [page, setPage] = useState(1)
  const [contractId, setContractId] = useState('')
  const [assignmentId, setAssignmentId] = useState('')
  const [description, setDescription] = useState('')
  const [images, setImages] = useState([])
  const [reading, setReading] = useState(false)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const missionId = Number(params.get('missionId'))
  const validMissionId = Number.isSafeInteger(missionId) && missionId > 0
  const mission = useApiQuery(`incident-mission:${missionId}`, signal => api.request(`/api/missions/${missionId}`, { signal }), params.has('missionId') && validMissionId)
  const contracts = useApiQuery(`incident-rentals:${page}`, signal => api.request('/api/rental-contracts', { query: { page }, signal }))
  const contract = useApiQuery(`incident-rental:${contractId}`, signal => api.request(`/api/rental-contracts/${contractId}`, { signal }), Boolean(contractId))
  const devices = contract.data?.status !== 'ENDED' ? (contract.data?.devices ?? []).filter(device => !device.returnedAt) : []
  const selectedDevice = assignmentId || devices.find(device => device.uavId && device.uavId === mission.data?.uav?.id)?.id || ''
  async function submit(event) {
    event.preventDefault()
    if (pending || reading) return
    setError('')
    if (!devices.some(device => device.id === Number(selectedDevice)) || !description.trim()) { setError('Choose an assigned device and describe the incident.'); return }
    const body = { rentalDeviceAssignmentId: Number(selectedDevice), description: description.trim(), images }
    if (params.has('missionId')) {
      if (!validMissionId || !mission.data) { setError('The linked mission is unavailable. Open this report from the mission again.'); return }
      const device = devices.find(item => item.id === Number(selectedDevice))
      if (contract.data.farmId !== mission.data.farm.id || !((device.uavId && device.uavId === mission.data.uav?.id) || (device.gatewayId && device.gatewayId === mission.data.gateway?.id) || (device.sensorNodeId && mission.data.targets?.some(target => target.sensorNodeId === device.sensorNodeId)))) {
        setError('Choose a device included in the linked mission.'); return
      }
      body.missionId = missionId
    }
    setPending(true)
    try {
      onSaved(await api.request('/api/device-incidents', { method: 'POST', toastMessage: 'Device issue reported successfully.', body }))
      setDescription(''); setImages([])
    } catch (cause) { setError(cause.message) } finally { setPending(false) }
  }
  return <SectionCard title="Report a device incident">
    <ApiState loading={contracts.loading} error={contracts.error} reload={contracts.reload} />
    <form className="management-form" onSubmit={submit} aria-label="Report device incident">
      {error && <p role="alert">{error}</p>}
      <fieldset disabled={pending || reading} className="management-form"><legend>Incident details</legend>
        {params.has('missionId') && <><ApiState loading={mission.loading} error={mission.error} reload={mission.reload} />
          {!validMissionId && <p role="alert">The linked mission is invalid.</p>}
          {mission.data && <p>Linked mission #{mission.data.id}: {mission.data.name}. Select the affected device from its rental.</p>}</>}
        <label>Rental contract<select required value={contractId} onChange={event => { setContractId(event.target.value); setAssignmentId('') }}>
          <option value="">Choose a rental contract</option>
          {(contracts.data?.items ?? []).filter(row => row.status !== 'ENDED').map(row => <option key={row.id} value={row.id}>Contract #{row.id} · Farm #{row.farmId} · {row.status}</option>)}
        </select></label>
        <div className="management-row-actions"><button type="button" disabled={page === 1 || contracts.loading} onClick={() => { setPage(page - 1); setContractId(''); setAssignmentId('') }}>Previous contracts</button>
          <span>Contracts page {page}</span><button type="button" disabled={contracts.loading || page * 20 >= (contracts.data?.pagination.totalItems ?? 0)} onClick={() => { setPage(page + 1); setContractId(''); setAssignmentId('') }}>Next contracts</button></div>
        {contracts.data && !contracts.data.items.some(row => row.status !== 'ENDED') && <p>No reportable rental contracts on this page.</p>}
        <ApiState loading={contract.loading} error={contract.error} reload={contract.reload} />
        <label>Assigned device<select required value={selectedDevice} disabled={!contract.data || contract.loading} onChange={event => setAssignmentId(event.target.value)}>
          <option value="">Choose an assigned device</option>
          {devices.map(device => <option key={device.id} value={device.id}>{device.code} · {device.name} · {device.installationLocation}</option>)}
        </select></label>
        {contract.data && !devices.length && <p>No unreturned devices are available in this contract.</p>}
        <label>Incident description<textarea required maxLength={4000} rows={4} value={description} onChange={event => setDescription(event.target.value)} /></label>
        <EvidenceImages images={images} onChange={setImages} onBusyChange={setReading} />
        <button type="submit" disabled={pending || reading || !selectedDevice || contract.loading || (params.has('missionId') && !mission.data)}>Report incident</button>
      </fieldset>
    </form>
  </SectionCard>
}

export default function DeviceIncidentsPage() {
  const { can } = useAuth()
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState(null)
  const [pending, setPending] = useState(false)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const query = useApiQuery(`device-incidents:${page}`, signal => api.request('/api/device-incidents', { query: { page }, signal }))
  async function open(id) {
    setPending(true); setError(''); setSelected(null)
    try { setSelected(await api.request(`/api/device-incidents/${id}`)) }
    catch (cause) { setError(cause.message) } finally { setPending(false) }
  }
  return <><PageHeader title="Device incidents" description="Report assigned equipment problems and review recorded evidence." />
    {can(P.INCIDENTS_CREATE) && <IncidentForm onSaved={incident => { setSelected(incident); setNotice('Incident reported.'); query.reload() }} />}
    {notice && <p role="status">{notice}</p>}{error && <p role="alert">{error}</p>}{pending && <p role="status">Loading incident…</p>}
    {selected && <SectionCard title={`Device incident #${selected.id}`} actions={<StatusBadge status={selected.status} />}>
      <dl><dt>Device</dt><dd>{selected.deviceCode}</dd><dt>Farm</dt><dd>#{selected.farmId}</dd>
        <dt>Reported by</dt><dd>User #{selected.reporterId}</dd><dt>Reported at</dt><dd>{displayTime(selected.reportedAt)}</dd></dl>
      <p style={{ whiteSpace: 'pre-wrap' }}>{selected.description}</p>
      {selected.missionId != null && <p><Link to={`/app/missions/${selected.missionId}`}>Linked mission #{selected.missionId}</Link></p>}
      <EvidenceImages images={selected.images} />
      {selected.serviceJobId && <Link to={`/app/service-jobs?jobId=${selected.serviceJobId}`}>View service job #{selected.serviceJobId}</Link>}
      {can(P.SERVICE_JOBS_MANAGE) && selected.status === 'OPEN' && !selected.serviceJobId && <ServiceJobAssignmentForm key={selected.id} incidentId={selected.id}
        onSaved={job => {
          setSelected({ ...selected, serviceJobId: job.id })
          setNotice(`Service job #${job.id} created.`)
        }} />}
    </SectionCard>}
    <ApiState loading={query.loading} error={query.error} reload={query.reload} />
    <DataTable caption="Device incidents" loading={query.loading} rows={query.data?.items ?? []} columns={[
      { key: 'id', header: 'Incident' }, { key: 'farmId', header: 'Farm' }, { key: 'deviceCode', header: 'Device' },
      { key: 'description', header: 'Description' }, { key: 'reportedAt', header: 'Reported at', render: displayTime },
      { key: 'status', header: 'Status', render: value => <StatusBadge status={value} /> },
      { key: 'action', header: 'Action', render: (_, row) => <button disabled={pending} onClick={() => open(row.id)}>Open incident #{row.id}</button> },
    ]} footer={<div className="management-row-actions"><button disabled={page === 1 || query.loading} onClick={() => setPage(page - 1)}>Previous</button>
      <span>Page {page}</span><button disabled={query.loading || page * 20 >= (query.data?.pagination.totalItems ?? 0)} onClick={() => setPage(page + 1)}>Next</button></div>} />
  </>
}

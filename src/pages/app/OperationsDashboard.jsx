import { useState } from 'react'
import { Link, useOutletContext } from 'react-router-dom'
import { api } from '../../services/api'
import { useApiQuery } from '../../services/api/useApiQuery'
import { allPages } from '../../services/api/workflow'
import { useWorkflowFormat } from '../../utils/useWorkflowText'
import { Workspace, Panel, State, JobLink } from '../../components/workflow/WorkflowUi'
import GeographicFarmMap from '../../components/app/GeographicFarmMap'
import PeriodFilter from '../../components/workflow/PeriodFilter'
import OperationsCharts from '../../components/workflow/OperationsCharts'

export default function OperationsDashboard() {
  const { time } = useWorkflowFormat()
  const { currentFarm, farmWorkspace } = useOutletContext() ?? {}
  const [range, setRange] = useState({ from: '', to: '' })
  const hasRange = Boolean(range.from && range.to)
  const query = useApiQuery('operations-dashboard:' + JSON.stringify(range), async signal => {
    const [operations, overview, dossiers] = await Promise.all([
      hasRange ? api.get('/api/operations/dashboard', range, signal) : Promise.resolve(null),
      api.get('/api/dashboard/overview', {}, signal), allPages('/api/installation-dossiers', {}, signal),
    ])
    return { operations, overview, dossiers }
  })
  const mapNodes = useApiQuery('dashboard-map:' + currentFarm?.id, signal => allPages('/api/sensor-nodes', { farmId: currentFarm.id }, signal), Boolean(currentFarm?.id))
  const data = query.data
  const operations = data?.operations
  const zones = (farmWorkspace?.zones ?? []).filter(zone => zone.farmId === currentFarm?.id)
  const cards = data && [['Farms', data.overview.farms], ['Monitored devices', data.overview.sensorNodes.total + data.overview.gateways.total],
    ['Open alerts', data.overview.alerts.open], ['Open work orders', data.overview.operations.openInstallations + data.overview.operations.openMaintenance + data.overview.operations.openRepairs],
    ['Expiring contracts', data.overview.operations.expiringRentals], ['Consultation requests in period', operations?.consultationRequests]]
  return <Workspace className="operations-dashboard" title="Farm operations" description="Track progress, technician schedules and dossiers awaiting action.">
    <div className="wf-dashboard-toolbar">
      <PeriodFilter value={range} onChange={setRange} />
      <Link className="app-ui-button app-ui-button--primary" to="/app/dispatch">Open dispatch calendar</Link>
    </div>
    {!hasRange && <p className="wf-muted">Select From date and To date to load statistics for a period.</p>}
    <State query={query}>{data && <>
      <section className="wf-metrics" aria-label="Operations overview">{cards.map(([name, value]) => <div key={name}><span>{name}</span><strong>{value ?? '—'}</strong></div>)}</section>
      <Panel title="Farm overview map" description={currentFarm?.name ?? 'Select a farm in the top bar to view its map.'}>
        {currentFarm?.id ? <State query={mapNodes}><GeographicFarmMap farm={currentFarm} zones={zones} nodes={mapNodes.data ?? []} /></State> : <p className="wf-muted">No farm selected.</p>}
      </Panel>
      {operations && <><OperationsCharts data={operations} /><p className="wf-muted">Charts group records by creation date and show their current status.</p></>}
      <Panel title={'Dossiers awaiting review: ' + data.dossiers.length} description="Engineer submissions include farm details and deposit receipts. This list is independent of the chart date range." actions={<Link to="/app/work-orders" className="wf-link">View all →</Link>}>
        <ul className="wf-list">{data.dossiers.slice(0, 5).map(row => <li key={row.id}><span>{row.customerName} · {row.farmName}</span><Link to={'/app/consultations/' + row.consultationId} className="wf-link">Review dossier and receipt →</Link></li>)}</ul>{!data.dossiers.length && <p className="wf-empty">No dossiers awaiting review.</p>}
      </Panel>
      {operations && <>
        <div className="wf-two-col"><Panel title="New consultation requests" actions={<Link to="/app/consultations" className="wf-link">View all →</Link>}>
          {operations.recentConsultationRequests.length ? <ul className="wf-list">{operations.recentConsultationRequests.map(row => <li key={row.id}><div><strong>{row.customerName}</strong><p>{row.serviceNeed}</p><small>{time(row.createdAt)}</small></div><a className="wf-link" href={'tel:' + row.phone}>{row.phone}</a></li>)}</ul> : <p className="wf-empty">No consultation requests in this period.</p>}
        </Panel><Panel title="Installations awaiting assignment" actions={<Link to="/app/work-orders" className="wf-link">View work orders →</Link>}>
          {operations.installationPipeline.length ? <ul className="wf-list">{operations.installationPipeline.map(row => <li key={row.id}><span>Request #{row.id} · Farm #{row.farmId}</span><Link className="wf-link" to={'/app/installation-requests/' + row.id}>Assign →</Link></li>)}</ul> : <p className="wf-empty">No requests awaiting assignment.</p>}
        </Panel></div>
        <div className="wf-two-col"><Panel title="Contracts ending in this period" actions={<Link className="wf-link" to="/app/rentals">Rental contracts →</Link>}>
          <ul className="wf-list">{operations.rentalsEndingInPeriod.map(row => <li key={row.id}><Link to={'/app/rentals?id=' + row.id} className="wf-link">Contract #{row.id}</Link><span>{time(row.endAt)}</span></li>)}</ul>{!operations.rentalsEndingInPeriod.length && <p className="wf-empty">No contracts ending in this period.</p>}
        </Panel><Panel title="Maintenance in this period" actions={<Link className="wf-link" to="/app/maintenance">Configure schedules →</Link>}>
          <ul className="wf-list">{operations.maintenance.slice(0, 5).map(row => <li key={row.id}><JobLink kind="service" id={row.id} /><span>{row.overdue ? 'Overdue · ' : ''}{time(row.scheduledAt)}</span></li>)}</ul>{!operations.maintenance.length && <p className="wf-empty">No maintenance jobs in this period.</p>}
        </Panel></div>
      </>}
    </>}</State>
  </Workspace>
}

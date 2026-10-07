import { useState } from 'react'
import { Link, useOutletContext } from 'react-router-dom'
import { useOperations } from '../../context/OperationsContext'
import { useAuth } from '../../context/AuthContext'
import { ROLES } from '../../config/roles'
import { PERMISSIONS } from '../../config/permissions'
import { selectAnalytics, ANALYTICS_DEFAULT_RANGE, analyticsTypes, formatMetric } from '../../data/mock/sensorAnalytics'
import { displayTime } from '../../data/mock/alertSelectors'
import { PageHeader, StatCard, SectionCard, ChartCard, DataTable, FilterBar, StatusBadge, EmptyState } from '../../components/app/ui'
import EnvironmentalChart from '../../components/app/EnvironmentalChart'
import ZoneComparison from '../../components/app/ZoneComparison'
import '../../styles/analytics.css'

function SensorDataView({ currentFarm, selectFarm, farmWorkspace, compareOnly }) {
  const { operations } = useOperations()
  const { activeRole, can } = useAuth()
  const simplified = activeRole === ROLES.FARM_OWNER
  const technical = activeRole === ROLES.UAV_DEVICE_OPERATOR
  const [filters, setFilters] = useState({ ...ANALYTICS_DEFAULT_RANGE, zoneId: '', sensorId: '', typeId: '1', includeBuffered: false })
  const [page, setPage] = useState(0)
  const effective = { ...filters, farmId: currentFarm.id, includeBuffered: technical && filters.includeBuffered }
  const data = selectAnalytics(operations, farmWorkspace, effective)
  const update = (values) => { setFilters({ ...filters, ...values }); setPage(0) }
  const pages = Math.max(1, Math.ceil(data.rows.length / 20)), currentPage = Math.min(page, pages - 1)
  return <div className="app-ui analytics-page"><PageHeader eyebrow={technical ? 'SENSOR TECHNICAL DATA' : 'ENVIRONMENTAL DATA'} title={compareOnly ? 'Compare Zones' : 'Sensor Data'} description={`${currentFarm.name} · ${simplified ? 'A clear view of farm conditions.' : 'Explore recorded measurements and configured threshold bands.'}`} actions={<StatusBadge status="INFO" label="Read-only data" />} />
    <FilterBar label="Sensor data filters" actions={<button className="app-ui-button" onClick={() => update({ ...ANALYTICS_DEFAULT_RANGE, zoneId: '', sensorId: '', typeId: '1', includeBuffered: false })}>Reset filters</button>}>
      <label>Farm<select aria-label="Farm" value={currentFarm.id} onChange={(event) => selectFarm(Number(event.target.value))}>{farmWorkspace.farms.map((farm) => <option key={farm.id} value={farm.id}>{farm.name}</option>)}</select></label>
      {!compareOnly && <><label>Zone<select aria-label="Zone" value={filters.zoneId} onChange={(event) => update({ zoneId: event.target.value, sensorId: '' })}><option value="">All zones</option>{data.zones.map((zone) => <option key={zone.id} value={zone.id}>{zone.name}</option>)}</select></label><label>Sensor<select aria-label="Sensor" value={filters.sensorId} onChange={(event) => update({ sensorId: event.target.value })}><option value="">All sensors</option>{data.nodes.map((node) => <option key={node.id} value={node.id}>{node.deviceCode} · {node.name}</option>)}</select></label></>}
      <label>Sensor Type<select aria-label="Sensor Type" value={filters.typeId} onChange={(event) => update({ typeId: event.target.value })}>{analyticsTypes.map((type) => <option key={type.id} value={type.id}>{type.name}</option>)}</select></label>
      <label>From<input type="datetime-local" aria-label="From" value={filters.from} onChange={(event) => update({ from: event.target.value })} /></label><label>To<input type="datetime-local" aria-label="To" value={filters.to} onChange={(event) => update({ to: event.target.value })} /></label>
      {technical && <label className="analytics-check"><input type="checkbox" checked={filters.includeBuffered} onChange={(event) => update({ includeBuffered: event.target.checked })} />Include buffered gateway readings</label>}
    </FilterBar>
    <p className="analytics-note">Date range uses measurement time in Vietnam (UTC+7), up to 31 days. Demo snapshot: 6 October 2026, 08:18. {effective.includeBuffered ? 'Includes local, unsynchronized measurements for technical inspection.' : 'Only synchronized, valid measurements are shown.'} Historical data can include currently offline sensors.</p>
    {data.error ? <p role="alert" className="analytics-error">{data.error}</p> : <>
      {!compareOnly && <><SectionCard title="Latest Readings" description={`Latest reading per channel within the selected date range · ${data.type.name} (${data.type.unit})`}>
        <div className="analytics-latest">{data.latest.length ? data.latest.map((row) => <article key={row.id}><strong>{row.node.deviceCode}</strong><span className="analytics-reading-value">{formatMetric(row.value, row.type)} <small>{row.type.unit}</small></span><small>{row.zone.name}</small><time dateTime={row.measuredAt}>{displayTime(row.measuredAt)}</time><StatusBadge status={row.node.isActive && row.node.status === 'ONLINE' ? 'ONLINE' : 'WARNING'} label={!row.node.isActive ? 'Sensor disabled' : row.node.status === 'ONLINE' ? 'Sensor online' : 'Sensor offline'} />{row.stale && <StatusBadge status="WARNING" label="Stale measurement" />}{!row.receivedAt && <StatusBadge status="PENDING" label="Buffered locally" />}</article>) : <EmptyState title="No readings match these filters" description="Choose another range, zone, sensor or sensor type." />}</div>
      </SectionCard>
      <ChartCard title={`Historical Trend · ${data.type.name} (${data.type.unit})`} caption="Hourly means give each contributing channel equal weight. Missing hours remain gaps; no values are interpolated."><EnvironmentalChart series={data.series} type={data.type} threshold={data.threshold} /></ChartCard>
      <section className="analytics-stats" aria-label="Reading statistics">{[['Minimum', data.stats.min], ['Maximum', data.stats.max], ['Average', data.stats.average]].map(([label, value]) => <StatCard key={label} label={label} value={formatMetric(value, data.type)} unit={value === null ? '' : data.type.unit} description={`${data.stats.count} individual measurements in range`} />)}</section>
      <SectionCard title="Threshold Bands" description="Configured channel warning limits, shown as the shaded chart band. These are demo monitoring thresholds."><p>{data.threshold ? `${data.threshold.min}–${data.threshold.max} ${data.type.unit}. Values outside this band may require review.` : data.channels.length ? 'Channels have different limits. No shared band is drawn.' : 'No channel readings available to show a threshold band.'}</p>{!simplified && <p className="analytics-note">Channel thresholds are read-only. System defaults do not retroactively change existing channel limits or captured alerts.</p>}</SectionCard></>}
      {can(PERMISSIONS.ZONES_COMPARE) && <><ZoneComparison operations={operations} workspace={farmWorkspace} filters={effective} />{!compareOnly && <Link className="app-ui-button analytics-compare-link" to="/app/sensor-data/compare-zones">Open Compare Zones</Link>}</>}
      {!compareOnly && <SectionCard title="Sensor Reading Table" description="Measured values and timestamps from the shared records."><DataTable caption="Sensor readings" rows={data.rows.slice(currentPage * 20, (currentPage + 1) * 20)} columns={[
        { key: 'node', header: 'Sensor', render: (node) => node.deviceCode }, { key: 'zone', header: 'Zone', render: (zone) => zone.name }, { key: 'type', header: 'Sensor Type', render: (type) => type.name },
        { key: 'value', header: 'Value', render: (value, row) => `${formatMetric(value, row.type)} ${row.type.unit}` }, { key: 'measuredAt', header: 'Measured At', render: displayTime },
        { key: 'band', header: 'Threshold', render: (_, row) => <StatusBadge status={row.value < row.channel.warningMin || row.value > row.channel.warningMax ? 'WARNING' : 'SUCCESS'} label={row.value < row.channel.warningMin ? 'Below band' : row.value > row.channel.warningMax ? 'Above band' : 'Within band'} /> },
        ...(!simplified ? [{ key: 'receivedAt', header: 'Data Availability', render: (value) => <StatusBadge status={value ? 'SUCCESS' : 'PENDING'} label={value ? 'Synchronized' : 'Buffered locally'} /> }] : []),
        ...(technical ? [{ key: 'sourceRecordKey', header: 'Source Record Key' }, { key: 'gatewayId', header: 'Gateway', render: (id) => operations.gateways.find((gateway) => gateway.id === id)?.code ?? 'Imported' }] : []),
      ]} footer={<div className="analytics-pagination"><span>{data.rows.length} readings · Page {currentPage + 1} of {pages}</span><button className="app-ui-button" disabled={currentPage === 0} onClick={() => setPage(currentPage - 1)}>Previous</button><button className="app-ui-button" disabled={currentPage >= pages - 1} onClick={() => setPage(currentPage + 1)}>Next</button></div>} /></SectionCard>}
    </>}
  </div>
}

export default function SensorDataPage({ compareOnly = false }) {
  const context = useOutletContext()
  const { activeRole } = useAuth()
  return <SensorDataView key={`${context.currentFarm.id}-${activeRole}-${compareOnly}`} {...context} compareOnly={compareOnly} />
}

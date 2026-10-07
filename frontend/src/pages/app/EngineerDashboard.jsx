import { useState } from 'react'
import { Link, useOutletContext } from 'react-router-dom'
import { useOperations } from '../../context/OperationsContext'
import { useManagement } from '../../context/ManagementContext'
import { engineerOverview, ANALYTICS_DEFAULT_RANGE, analyticsTypes, formatMetric } from '../../data/mock/sensorAnalytics'
import { displayTime } from '../../data/mock/alertSelectors'
import { PageHeader, StatCard, ChartCard, SectionCard, FilterBar, DataTable, EmptyState, AlertSeverityBadge, StatusBadge } from '../../components/app/ui'
import EnvironmentalChart from '../../components/app/EnvironmentalChart'
import ZoneComparison from '../../components/app/ZoneComparison'
import '../../styles/analytics.css'

function EngineerFarmDashboard({ currentFarm, selectFarm, farmWorkspace }) {
  const { operations } = useOperations()
  const { management } = useManagement()
  const [zoneId, setZoneId] = useState('')
  const [compareType, setCompareType] = useState('1')
  const data = engineerOverview(operations, farmWorkspace, management.alerts, currentFarm.id, zoneId)
  return <div className="app-ui analytics-page"><PageHeader eyebrow="AGRICULTURAL ANALYSIS" title="Environmental overview" description={`${currentFarm.name} · Environmental trends, zone conditions, and data availability.`} actions={<Link className="app-ui-button" to="/app/sensor-data">Explore Sensor Data</Link>} />
    <FilterBar label="Farm and zone selection"><label>Farm<select aria-label="Farm" value={currentFarm.id} onChange={(event) => selectFarm(Number(event.target.value))}>{farmWorkspace.farms.map((farm) => <option key={farm.id} value={farm.id}>{farm.name}</option>)}</select></label><label>Zone<select aria-label="Zone" value={zoneId} onChange={(event) => setZoneId(event.target.value)}><option value="">All zones</option>{farmWorkspace.zones.filter(({ farmId }) => farmId === currentFarm.id).map((zone) => <option key={zone.id} value={zone.id}>{zone.name}</option>)}</select></label></FilterBar>
    <p className="analytics-note">Read-only analysis · Demo snapshot: 6 October 2026, 08:18 Vietnam time. KPI averages use the latest synchronized reading from each online, enabled, non-stale channel.</p>
    <section className="analytics-kpis" aria-label="Environmental indicators">{data.metrics.slice(0, 3).map((metric) => <StatCard key={metric.id} label={`Average ${metric.id === 2 ? 'Humidity' : metric.name}`} value={formatMetric(metric.currentAverage, metric)} unit={metric.currentAverage === null ? '' : metric.unit} description={`${metric.contributing} contributing channels`} />)}<StatCard label="Active Sensor Nodes" value={data.activeNodes} description="Enabled and online" /><StatCard label="Environmental Alerts" value={data.environmentalAlerts.length} description="Open or acknowledged threshold alerts" /></section>
    <div className="analytics-trends">{data.metrics.map((metric) => <ChartCard key={metric.id} title={`${metric.name} (${metric.unit})`} caption={metric.threshold ? `Hourly channel means · Shaded band: ${metric.threshold.min}–${metric.threshold.max} ${metric.unit} · Demo configured thresholds` : 'No common configured threshold band for this selection.'}><EnvironmentalChart series={metric.series} type={metric} threshold={metric.threshold} /></ChartCard>)}</div>
    <label className="analytics-type-picker">Comparison metric<select aria-label="Comparison metric" value={compareType} onChange={(event) => setCompareType(event.target.value)}>{analyticsTypes.map((type) => <option key={type.id} value={type.id}>{type.name}</option>)}</select></label>
    <ZoneComparison operations={operations} workspace={farmWorkspace} filters={{ ...ANALYTICS_DEFAULT_RANGE, farmId: currentFarm.id, typeId: compareType }} />
    <SectionCard title="Sensor Health" description="Unavailable readings may reduce confidence in comparisons; missing values are not filled in."><DataTable caption="Sensor health affecting analysis" rows={data.health} columns={[{ key: 'deviceCode', header: 'Sensor' }, { key: 'zoneId', header: 'Zone', render: (id) => farmWorkspace.zones.find((zone) => zone.id === id)?.name }, { key: 'reason', header: 'Analysis limitation' }, { key: 'lastReadingAt', header: 'Latest synchronized measurement', render: displayTime }]} emptyState={<EmptyState title="All selected sensors have recent data" />} /></SectionCard>
    <SectionCard title="Environmental Alerts" description="Read-only threshold alerts for the selected farm and zone.">{data.environmentalAlerts.length ? <ul className="analytics-alerts">{data.environmentalAlerts.map((alert) => <li key={alert.id}><AlertSeverityBadge severity={alert.severity} /><div><strong>{alert.title}</strong><p>{alert.message}</p><small>{displayTime(alert.openedAt)} · Trigger {alert.triggeredValue} · Threshold {alert.thresholdValue}</small></div><StatusBadge status={alert.status} /></li>)}</ul> : <EmptyState title="No environmental alerts" description="No open threshold alerts match this selection." />}</SectionCard>
  </div>
}

export default function EngineerDashboard() {
  const context = useOutletContext()
  return <EngineerFarmDashboard key={context.currentFarm.id} {...context} />
}

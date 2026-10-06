import { useMemo } from 'react'
import { Link, useOutletContext } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { useManagement } from '../../context/ManagementContext'
import { useOperations } from '../../context/OperationsContext'
import { getFarmOwnerDashboard, formatSnapshotAge } from '../../data/mock/farmOwnerDashboard'
import { MOCK_NOW } from '../../data/mock/scenario'
import AppIcon from '../../components/app/AppIcon'
import FarmOverviewMap from '../../components/app/FarmOverviewMap'
import { PageHeader, StatCard, MetricCard, SectionCard, StatusBadge, ProgressBar, BatteryIndicator, AlertSeverityBadge, EmptyState } from '../../components/app/ui'
import '../../styles/farm-owner-dashboard.css'

function EnvironmentTrend({ metric }) {
  const values = metric.series.filter(({ value }) => value !== null)
  if (!values.length) return null
  const min = Math.min(...values.map(({ value }) => value))
  const max = Math.max(...values.map(({ value }) => value))
  const span = Math.max(max - min, 1)
  const points = values.map(({ value }, index) => `${8 + index / Math.max(values.length - 1, 1) * 184},${45 - (value - min) / span * 32}`).join(' ')
  return <figure className="owner-trend">
    <svg viewBox="0 0 200 56" role="img" aria-label={`${metric.name} over the last 12 hours: ${min} to ${max} ${metric.unit}. Latest average ${metric.value} ${metric.unit}.`}>
      <path d="M8 46H192" stroke="#e5ebe7" strokeDasharray="3 4" />
      <polygon points={`8,52 ${points} 192,52`} fill="currentColor" opacity=".07" />
      <polyline points={points} fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" strokeLinecap="round" />
    </svg>
    <figcaption><span>12 hours ago</span><span>Now</span></figcaption>
  </figure>
}

export default function FarmOwnerDashboard() {
  const { currentFarm, farmWorkspace } = useOutletContext()
  const { user } = useAuth()
  const { management } = useManagement()
  const { operations } = useOperations()
  const overview = useMemo(() => getFarmOwnerDashboard(currentFarm.id, farmWorkspace, management.alerts, operations.sensorNodes, operations), [currentFarm.id, farmWorkspace, management.alerts, operations])
  if (!overview) return <EmptyState title="Farm unavailable" description="Choose another farm from the selector above." />
  const { farm, zones, nodes, currentMission: mission, environment, importantAlerts, activity } = overview
  const firstName = user.fullName.trim().split(/\s+/)[0]
  const snapshotDate = new Intl.DateTimeFormat('en-GB', { day: 'numeric', month: 'short', year: 'numeric', timeZone: farm.timezone }).format(new Date(MOCK_NOW))
  const snapshotTime = new Intl.DateTimeFormat('en-GB', { hour: '2-digit', minute: '2-digit', timeZone: farm.timezone }).format(new Date(MOCK_NOW))

  return <div className="app-ui owner-dashboard">
    <PageHeader eyebrow="YOUR FARM AT A GLANCE" title={`Good morning, ${firstName}`} description="Farm overview and current operating status."
      actions={<div className="owner-snapshot"><span>Demo snapshot</span><time dateTime={MOCK_NOW}>{snapshotDate} · {snapshotTime}</time></div>} />

    <div className="owner-farm-context"><span><AppIcon name="map" size={17} /><strong>{farm.name}</strong></span><span>{farm.areaHectares} hectares <b>·</b> {zones.length} zones <b>·</b> {farm.location}</span></div>

    <section className="owner-kpis" aria-label="Farm key indicators">
      <StatCard label="Farm Health" value={overview.health} icon={<AppIcon name="sun" />} className="owner-health" data-health={overview.health} description={overview.openAlertCount ? 'Review the important alerts below' : 'No unresolved alerts'} />
      <StatCard label="Online Sensors" value={`${overview.onlineSensors}/${nodes.length}`} icon={<AppIcon name="sensor" />} description={`${nodes.filter(({ status, isActive }) => status === 'OFFLINE' && isActive).length} offline · ${nodes.some(({ isActive }) => !isActive) ? `${nodes.filter(({ isActive }) => !isActive).length} disabled` : `${zones.length} zones`}`} />
      <StatCard label="Active Missions" value={overview.activeMissionCount} icon={<AppIcon name="drone" />} description={mission ? 'Collecting your farm’s data' : 'No collection in progress'} />
      <StatCard label="Open Alerts" value={overview.openAlertCount} icon={<AppIcon name="alert" />} description="Including acknowledged alerts" />
      <StatCard label="Latest Sync" value={formatSnapshotAge(overview.latestSyncAt)} icon={<AppIcon name="cloud" />} className="owner-sync" description="Latest sensor data received" />
    </section>

    <div className="owner-main-grid">
      <SectionCard title="Farm Overview Map" description="A connected view of your land" className="owner-map-card"
        actions={<span className="owner-subtle-label">{zones.length} monitored zones</span>}>
        <FarmOverviewMap zones={zones} nodes={nodes} mission={mission} />
      </SectionCard>

      <SectionCard title="Current Mission" description="Your latest collection in progress" className="owner-mission-card">
        {mission ? <>
          <div className="owner-mission-heading"><span className="owner-mission-icon"><AppIcon name="drone" size={28} /></span><StatusBadge status={mission.status} label="IN_PROGRESS" /></div>
          <p className="owner-mission-code">{mission.code}</p>
          <h3>{mission.name}</h3>
          <p className="owner-mission-summary">Gathering fresh readings across your farm, so you can see how your crops are doing.</p>
          <div className="owner-mission-progress"><ProgressBar value={mission.progress.collectedTargets} max={mission.progress.totalTargets} label={`${mission.progress.collectedTargets}/${mission.progress.totalTargets} targets collected`} />
            <ProgressBar value={mission.progress.completedWaypoints} max={mission.progress.totalWaypoints} label={`${mission.progress.completedWaypoints}/${mission.progress.totalWaypoints} waypoints visited`} tone="info" /></div>
          <div className="owner-mission-battery"><span>UAV battery</span><BatteryIndicator value={mission.progress.batteryPercent} /></div>
          <Link className="app-ui-button owner-detail-link" to={`/app/missions/${mission.id}`}>View Details <AppIcon name="chevron" size={15} /></Link>
        </> : <EmptyState title="No active mission" description="Your next collection will appear here once it starts." />}
      </SectionCard>

      <section className="owner-environment" aria-labelledby="owner-environment-title">
        <div className="owner-section-heading"><div><h2 id="owner-environment-title">Environment Summary</h2><p>Latest received averages from online sensors</p></div><Link to="/app/sensor-data" className="owner-text-link">View sensor data <AppIcon name="chevron" size={15} /></Link></div>
        <div className="owner-environment-grid">{environment.map((metric) => <MetricCard key={metric.id} label={metric.name} value={metric.value} unit={metric.value !== null ? metric.unit : undefined}
          className={`owner-environment-metric owner-metric-${metric.id}`} description={metric.sensorCount ? `${metric.sensorCount} reporting sensors` : 'No readings available'}>
          <EnvironmentTrend metric={metric} />
          {metric.attentionCount > 0 && <span className="owner-metric-attention">{metric.attentionCount} sensor needs attention</span>}
        </MetricCard>)}</div>
      </section>

      <SectionCard title="Important Alerts" description="What needs your attention" className="owner-alerts-card"
        actions={<Link to="/app/alerts" className="owner-text-link">View all alerts <AppIcon name="chevron" size={15} /></Link>}>
        {importantAlerts.length ? <ul className="owner-alert-list">{importantAlerts.slice(0, 4).map((alert) => <li key={alert.id} data-severity={alert.severity}>
          <span className="owner-alert-icon"><AppIcon name="alert" size={18} /></span>
          <div><div className="owner-alert-title"><h3>{alert.title}</h3><AlertSeverityBadge severity={alert.severity} /></div><p>{alert.message}</p><span className="owner-subtle-label">{alert.status === 'ACKNOWLEDGED' ? 'Acknowledged · ' : ''}{formatSnapshotAge(alert.openedAt)}</span></div>
        </li>)}</ul> : <EmptyState title="All clear" description="No critical or warning alerts for this farm." />}
      </SectionCard>

      <SectionCard title="Recent Activity" description="The latest updates from your farm" className="owner-activity-card">
        {activity.length ? <ol className="owner-activity-list">{activity.map((event) => <li key={event.id}>
          <span className="owner-activity-icon"><AppIcon name={event.icon === 'check' ? 'alert' : event.icon} size={16} /></span>
          <div><h3>{event.title}</h3><p>{event.description}</p><time dateTime={event.at}>{formatSnapshotAge(event.at)}</time></div>
        </li>)}</ol> : <EmptyState title="No recent activity" description="Collection and alert updates will appear here." />}
      </SectionCard>
    </div>
    <p className="owner-footnote">Demo data · Times are relative to the snapshot shown above. Farm health reflects unresolved alerts.</p>
  </div>
}

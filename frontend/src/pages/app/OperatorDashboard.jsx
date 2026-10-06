import { Link, useOutletContext } from 'react-router-dom'
import { useOperations } from '../../context/OperationsContext'
import { getOperatorDashboard } from '../../data/mock/operationsSelectors'
import { displayTime } from '../../data/mock/alertSelectors'
import { PageHeader, StatCard, SectionCard, DataTable, StatusBadge, BatteryIndicator, ProgressBar, EmptyState } from '../../components/app/ui'
import AppIcon from '../../components/app/AppIcon'
import '../../styles/operations.css'

export default function OperatorDashboard() {
  const { currentFarm, farmWorkspace } = useOutletContext()
  const { operations } = useOperations()
  const data = getOperatorDashboard(currentFarm.id, operations, farmWorkspace)
  const mission = data.mission
  const kpis = [['Available UAVs', data.availableUavs, 'READY inventory'], ['Online Gateways', data.onlineGateways, 'Online or synchronizing'], ['Active Sensor Nodes', data.activeNodes, 'Enabled for collection'], ['Ready Missions', data.readyMissions, 'Planned and ready'], ['Mission In Progress', data.activeMissions, 'Currently collecting'], ['Pending Sync Batches', data.pending.length, 'Queued or synchronizing']]
  return <div className="app-ui operations-page">
    <PageHeader eyebrow="OPERATIONS" title="Operational readiness" description={`${currentFarm.name} · Equipment availability, mission progress, and data collection.`} actions={<Link className="app-ui-button" to="/app/missions">View missions</Link>} />
    <section className="operations-kpis" aria-label="Operational indicators">{kpis.map(([label, value, description]) => <StatCard key={label} label={label} value={value} description={description} />)}</section>
    <div className="operations-dashboard-grid">
      <SectionCard title="Active Mission" description="Current collection progress" actions={mission && <StatusBadge status={mission.status} />}>
        {mission ? <><div className="operations-mission-title"><AppIcon name="drone" size={30} /><div><span>{mission.code}</span><h3>{mission.name}</h3></div></div>
          <dl className="operations-details"><div><dt>UAV</dt><dd>{mission.uav?.code} · {mission.uav?.name}</dd></div><div><dt>Gateway</dt><dd>{mission.gateway?.code}</dd></div><div><dt>UAV battery</dt><dd><BatteryIndicator value={mission.uav?.batteryPercent} /></dd></div></dl>
          <div className="operations-progress"><ProgressBar label={`Waypoints · ${mission.progress.completedWaypoints}/${mission.progress.totalWaypoints}`} value={mission.progress.completedWaypoints} max={mission.progress.totalWaypoints} /><ProgressBar label={`Targets collected · ${mission.progress.collectedTargets}/${mission.progress.totalTargets}`} value={mission.progress.collectedTargets} max={mission.progress.totalTargets} tone="info" /></div>
          <Link className="app-ui-button" to={`/app/missions/${mission.id}`}>View mission details</Link></> : <EmptyState title="No active mission" description="A running collection mission will appear here." />}
      </SectionCard>
      <SectionCard title="Device Readiness" description="Equipment status and sensor availability" actions={<Link className="operations-link" to="/app/devices">View devices →</Link>}>
        <ul className="operations-readiness">{[...data.uavs, ...data.gateways].map((device) => <li key={device.code}><div><strong>{device.code}</strong><small>{device.name}{'uavId' in device && !device.uavId ? ' · Unassigned' : ''}</small></div><StatusBadge status={device.status} /></li>)}</ul>
        <ProgressBar label={`Sensors online and enabled · ${data.nodes.filter(({ status, isActive }) => status === 'ONLINE' && isActive).length}/${data.nodes.length}`} value={data.nodes.filter(({ status, isActive }) => status === 'ONLINE' && isActive).length} max={data.nodes.length || 1} />
        <p className="operations-note">{data.nodes.filter(({ status, isActive }) => status === 'OFFLINE' && isActive).length} offline · {data.nodes.filter(({ isActive }) => !isActive).length} disabled</p>
      </SectionCard>
      <SectionCard title="Collection Failures" description="Failed targets from recent missions; recovered retries are excluded" className="operations-wide">
        <DataTable caption="Collection failures" rows={data.failures} columns={[
          { key: 'sensor', header: 'Sensor', render: (sensor) => <Link className="operations-link" to={`/app/sensors/${sensor.id}`}>{sensor.deviceCode}</Link> },
          { key: 'mission', header: 'Mission', render: (mission) => <Link className="operations-link" to={`/app/missions/${mission.id}`}>{mission.code}</Link> },
          { key: 'lastAttempt', header: 'Reason', render: (attempt) => attempt?.errorMessage ?? 'Collection failed' },
          { key: 'attempts', header: 'Attempts', render: (_, row) => row.lastAttempt?.attemptNo },
          { key: 'time', header: 'Last Attempt', render: (_, row) => displayTime(row.lastAttempt?.finishedAt) },
        ]} emptyState={<EmptyState title="No failed targets" description="No unresolved collection failures in this farm's mission records." />} />
      </SectionCard>
      <SectionCard title="Pending Data Sync" description="Collected readings awaiting upload completion" actions={<Link className="operations-link" to="/app/sync">View data sync →</Link>}>
        {data.pending.length ? <ul className="operations-readiness">{data.pending.map((batch) => <li key={batch.id}><div><strong>Batch {String(batch.id).padStart(3, '0')} · {data.gateways.find(({ id }) => id === batch.gatewayId)?.code}</strong><small>{batch.pendingCount} readings pending of {batch.recordCount}</small></div><StatusBadge status={batch.status} /></li>)}</ul> : <EmptyState title="No pending batches" />}
      </SectionCard>
      <SectionCard title="Upcoming / Scheduled Missions" description="Planned collection windows in Vietnam time">
        {data.upcoming.length ? <ul className="operations-readiness">{data.upcoming.map((item) => <li key={item.id}><div><Link className="operations-link" to={`/app/missions/${item.id}`}>{item.name}</Link><small>{item.code} · {displayTime(item.scheduledStartAt)}</small>{operations.gateways.find(({ id }) => id === item.gatewayId)?.uavId !== item.uavId && <small className="operations-warning">Gateway assignment needs review</small>}</div><StatusBadge status={item.status} /></li>)}</ul> : <EmptyState title="No upcoming missions" />}
      </SectionCard>
    </div>
    <p className="operations-note">Demo snapshot · Inventory changes reset on refresh. Mission planning and monitoring only; UAV flight is handled outside this application.</p>
  </div>
}

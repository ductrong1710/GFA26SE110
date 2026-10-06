import { Link, useOutletContext } from 'react-router-dom'
import { getAdminDashboard } from '../../data/mock/adminDashboard'
import { formatSnapshotAge } from '../../data/mock/farmOwnerDashboard'
import { PageHeader, StatCard, SectionCard, StatusBadge, AlertSeverityBadge, DataTable, EmptyState, ProgressBar } from '../../components/app/ui'
import AppIcon from '../../components/app/AppIcon'
import '../../styles/admin-workspace.css'

export default function AdminDashboard() {
  const { farmWorkspace } = useOutletContext()
  const data = getAdminDashboard(farmWorkspace)
  const kpis = [['Total Users', data.totalUsers, 'users'], ['Total Sensors', data.totalSensors, 'sensor'], ['Offline Devices', data.offlineDevices, 'drone'], ['Open Alerts', data.openAlerts, 'alert'], ['Active Missions', data.activeMissions, 'mission'], ['Pending Sync', data.pendingSync, 'cloud']]
  return <div className="app-ui admin-dashboard">
    <PageHeader eyebrow="ADMINISTRATION" title="System overview" description="Account access, farm coverage, and operational health across all farms."
      actions={<Link className="app-ui-button" to="/app/users">View users & roles</Link>} />
    <p className="admin-scope-note">All farms · Demo snapshot · Open alerts include acknowledged alerts; pending sync includes batches in progress.</p>
    <section className="admin-kpis" aria-label="Administration indicators">{kpis.map(([label, value, icon]) => <StatCard key={label} label={label} value={value} icon={<AppIcon name={icon} />} />)}</section>
    <div className="admin-grid">
      <SectionCard title="System Status" description="Monitoring coverage and outstanding work">
        <dl className="admin-status-list">
          <div><dt>Application environment</dt><dd><StatusBadge status="INFO" label="Local demo" /></dd></div>
          <div><dt>Backend connection</dt><dd>Not connected</dd></div>
          <div><dt>Device connectivity</dt><dd><StatusBadge status={data.offlineDevices ? 'WARNING' : 'ONLINE'} label={data.offlineDevices ? `${data.offlineDevices} device offline` : 'All devices online'} /></dd></div>
          <div><dt>Synchronization queue</dt><dd>{data.pendingSync} batches awaiting completion</dd></div>
        </dl>
      </SectionCard>
      <SectionCard title="Farm/Zone Overview" description={`${farmWorkspace.farms.length} farms · ${farmWorkspace.zones.length} zones`} actions={<Link className="admin-link" to="/app/farms">View farms →</Link>}>
        <ul className="admin-farm-list">{data.farms.map((farm) => <li key={farm.id}><div><strong>{farm.name}</strong><p>{farm.zoneCount} zones · {farm.sensorCount} sensors</p></div><StatusBadge status={farm.onlineCount === farm.sensorCount ? 'ONLINE' : 'WARNING'} label={farm.sensorCount ? `${farm.onlineCount}/${farm.sensorCount} online` : 'No sensors'} /></li>)}</ul>
      </SectionCard>
      <SectionCard title="Recent Critical Alerts" description="Unresolved incidents requiring attention" actions={<Link className="admin-link" to="/app/alerts">View alerts →</Link>}>
        {data.criticalAlerts.length ? <ul className="admin-alert-list">{data.criticalAlerts.map((alert) => <li key={alert.id}><AlertSeverityBadge severity={alert.severity} /><h3>{alert.title}</h3><p>{alert.message}</p><small>{formatSnapshotAge(alert.openedAt)} · {alert.status === 'ACKNOWLEDGED' ? 'Acknowledged' : 'Open'}</small></li>)}</ul> : <EmptyState title="No critical alerts" />}
      </SectionCard>
      <SectionCard title="Device Health Summary" description="Connectivity and battery exceptions" actions={<Link className="admin-link" to="/app/devices">View device status →</Link>}>
        <div className="admin-device-groups">{data.deviceGroups.map((group) => <div key={group.name}><ProgressBar value={group.total - group.offline} max={group.total} label={`${group.name} · ${group.total - group.offline}/${group.total} connected`} tone={group.offline ? 'warning' : 'success'} /><p>{group.offline} offline · {group.lowBattery} low battery</p></div>)}</div>
      </SectionCard>
      <SectionCard title="Mission Overview" description="Status visibility across scheduled and recent collections" className="admin-wide" actions={<Link className="admin-link" to="/app/missions">View missions →</Link>}>
        <DataTable caption="Mission overview" rows={data.missions} columns={[
          { key: 'code', header: 'Mission', render: (code, row) => <Link className="admin-link" to={`/app/missions/${row.id}`}>{code}</Link> },
          { key: 'name', header: 'Name' },
          { key: 'farmId', header: 'Farm', render: (id) => farmWorkspace.farms.find((farm) => farm.id === id)?.name ?? 'Unknown farm' },
          { key: 'status', header: 'Status', render: (status) => <StatusBadge status={status} /> },
        ]} />
      </SectionCard>
      <SectionCard title="Recent User Activity" description="Recorded administrative and alert actions" className="admin-wide">
        <ul className="admin-user-activity">{data.activity.map((event) => <li key={event.id}><span className="admin-activity-avatar" aria-hidden="true">{event.userName.split(' ').map((word) => word[0]).slice(0, 2).join('')}</span><div><strong>{event.userName}</strong><p>{event.title} · {event.description}</p></div><time dateTime={event.at ?? undefined}>{event.at ? formatSnapshotAge(event.at) : 'Time not recorded'}</time></li>)}</ul>
      </SectionCard>
    </div>
  </div>
}

import { useDashboard } from '../../services/api/useDashboard'
import { useMemo } from 'react'
import { Link, useOutletContext } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { useManagement } from '../../context/ManagementContext'
import { useOperations } from '../../context/OperationsContext'
import { getFarmOwnerDashboard, formatSnapshotAge } from '../../services/api/dashboard'
import { getBatteryState, getProgress, getTone } from '../../components/app/ui/uiState'
import { useUiText } from '../../utils/useUiText'
const VIEW_OPENED_AT = new Date().toISOString()
import AppIcon from '../../components/app/AppIcon'
import FarmOverviewMap from '../../components/app/FarmOverviewMap'
import { PageHeader, StatCard, MetricCard, SectionCard, StatusBadge, BatteryIndicator, AlertSeverityBadge, EmptyState } from '../../components/app/ui'
import '../../styles/farm-owner-dashboard.css'

const translations = {
  'Loading…': 'Đang tải…',
  'Try again': 'Thử lại',
  'Farm unavailable': 'Không thể tải trang trại',
  'Choose another farm from the selector above.': 'Hãy chọn một trang trại khác từ danh sách phía trên.',
  'YOUR FARM AT A GLANCE': 'TỔNG QUAN TRANG TRẠI CỦA BẠN',
  'Good morning': 'Chào buổi sáng',
  'Farm overview and current operating status.': 'Tổng quan trang trại và tình trạng hoạt động hiện tại.',
  'Current view': 'Thời điểm xem',
  'No description recorded': 'Chưa có mô tả',
  'zones': 'khu vực',
  'Farm key indicators': 'Chỉ số chính của trang trại',
  'Farm Health': 'Sức khỏe trang trại',
  'Review the important alerts below': 'Xem các cảnh báo quan trọng bên dưới',
  'No unresolved alerts': 'Không có cảnh báo chưa xử lý',
  'Online Sensors': 'Cảm biến trực tuyến',
  'offline': 'ngoại tuyến',
  'disabled': 'đã tắt',
  'Active Missions': 'Nhiệm vụ đang hoạt động',
  'Collecting your farm’s data': 'Đang thu thập dữ liệu trang trại',
  'No collection in progress': 'Không có đợt thu thập dữ liệu nào đang diễn ra',
  'Open Alerts': 'Cảnh báo chưa xử lý',
  'Including acknowledged alerts': 'Bao gồm các cảnh báo đã xác nhận',
  'Latest Sync': 'Đồng bộ gần nhất',
  'Latest sensor data received': 'Thời điểm nhận dữ liệu cảm biến gần nhất',
  'Farm Overview Map': 'Bản đồ tổng quan trang trại',
  'A connected view of your land': 'Bản đồ kết nối các khu đất của bạn',
  'monitored zones': 'khu vực được giám sát',
  'Current Mission': 'Nhiệm vụ hiện tại',
  'Your latest collection in progress': 'Đợt thu thập dữ liệu mới nhất đang diễn ra',
  'Gathering fresh readings across your farm, so you can see how your crops are doing.': 'Đang thu thập dữ liệu mới trên toàn trang trại để bạn theo dõi tình trạng cây trồng.',
  'targets collected': 'mục tiêu đã thu thập',
  'Collection progress unavailable in mission summary.': 'Không có thông tin tiến độ trong phần tóm tắt nhiệm vụ.',
  'Waypoint completion unavailable.': 'Không có thông tin về điểm đánh dấu đã hoàn thành.',
  'UAV battery': 'Pin UAV',
  'View Details': 'Xem chi tiết',
  'No active mission': 'Không có nhiệm vụ đang hoạt động',
  'Your next collection will appear here once it starts.': 'Đợt thu thập tiếp theo sẽ hiển thị tại đây khi bắt đầu.',
  'Environment Summary': 'Tổng quan môi trường',
  'Latest received averages from online sensors': 'Giá trị trung bình mới nhất nhận được từ các cảm biến trực tuyến',
  'View sensor data': 'Xem dữ liệu cảm biến',
  'reporting sensors': 'cảm biến đang gửi dữ liệu',
  'No readings available': 'Chưa có dữ liệu đo',
  'sensor needs attention': 'cảm biến cần được kiểm tra',
  'Important Alerts': 'Cảnh báo quan trọng',
  'What needs your attention': 'Những nội dung cần bạn chú ý',
  'View all alerts': 'Xem tất cả cảnh báo',
  'Acknowledged · ': 'Đã xác nhận · ',
  'All clear': 'Mọi thứ đều ổn',
  'No critical or warning alerts for this farm.': 'Trang trại hiện không có cảnh báo nghiêm trọng hoặc cảnh báo cần chú ý.',
  'Recent Activity': 'Hoạt động gần đây',
  'The latest updates from your farm': 'Các cập nhật mới nhất từ trang trại',
  'No recent activity': 'Chưa có hoạt động gần đây',
  'Collection and alert updates will appear here.': 'Cập nhật về thu thập dữ liệu và cảnh báo sẽ hiển thị tại đây.',
  'Backend records · Times are relative to the current view. Farm health reflects unresolved alerts.': 'Dữ liệu từ hệ thống · Thời gian được tính tương đối với thời điểm xem hiện tại. Sức khỏe trang trại dựa trên các cảnh báo chưa xử lý.',
  '12 hours ago': '12 giờ trước',
  'Now': 'Bây giờ',
  'over the last 12 hours': 'trong 12 giờ qua',
  'to': 'đến',
  'Latest average': 'Giá trị trung bình mới nhất',
  'Not available': 'Không có dữ liệu',
  'Just now': 'Vừa xong',
  'minute': 'phút',
  'minutes': 'phút',
  'hour': 'giờ',
  'hours': 'giờ',
  'ago': 'trước',
  'Unknown': 'Không xác định',
  'Unknown battery level': 'Mức pin không xác định',
  'Unavailable': 'Không khả dụng',
}

function OwnerApiState({ loading, error, reload, t }) {
  if (loading) return <p role="status" className="operations-note">{t('Loading…')}</p>
  if (error) return <div role="alert" className="management-error"><p>{error.message}</p>{reload && <button className="app-ui-button" onClick={reload}>{t('Try again')}</button>}</div>
  return null
}

function OwnerProgressBar({ value, max, label, t }) {
  const progress = getProgress(value, max)
  const text = progress.percent === null ? t('Unavailable') : `${Math.round(progress.percent)}%`
  return <div className="app-ui app-ui-progress" data-tone={getTone('primary')}>
    <div className="app-ui-progress-label"><span>{label}</span><span>{text}</span></div>
    <div className="app-ui-progress-track" role="progressbar" aria-label={label} aria-valuemin={0}
      aria-valuemax={progress.max} aria-valuenow={progress.value ?? undefined} aria-valuetext={text}>
      <span className="app-ui-progress-fill" style={{ width: `${progress.percent ?? 0}%` }} />
    </div>
  </div>
}

function EnvironmentTrend({ metric, t }) {
  const values = metric.series.filter(({ value }) => value !== null)
  if (!values.length) return null
  const min = Math.min(...values.map(({ value }) => value))
  const max = Math.max(...values.map(({ value }) => value))
  const span = Math.max(max - min, 1)
  const points = values.map(({ value }, index) => `${8 + index / Math.max(values.length - 1, 1) * 184},${45 - (value - min) / span * 32}`).join(' ')
  return <figure className="owner-trend">
    <svg viewBox="0 0 200 56" role="img" aria-label={`${metric.name} ${t('over the last 12 hours')}: ${min} ${t('to')} ${max} ${metric.unit}. ${t('Latest average')} ${metric.value} ${metric.unit}.`}>
      <path d="M8 46H192" stroke="#e5ebe7" strokeDasharray="3 4" />
      <polygon points={`8,52 ${points} 192,52`} fill="currentColor" opacity=".07" />
      <polyline points={points} fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinejoin="round" strokeLinecap="round" />
    </svg>
    <figcaption><span>{t('12 hours ago')}</span><span>{t('Now')}</span></figcaption>
  </figure>
}

export default function FarmOwnerDashboard() {
  const t = useUiText(translations)
  const { currentFarm, farmWorkspace } = useOutletContext()
  const { user } = useAuth()
  const { management: demoManagement } = useManagement()
  const { operations: demoOperations } = useOperations()
  const query = useDashboard(currentFarm.id)
  const management = query.data?.management ?? demoManagement
  const operations = query.data?.operations ?? demoOperations
  const overview = useMemo(() => getFarmOwnerDashboard(currentFarm.id, farmWorkspace, management.alerts, operations.sensorNodes, operations), [currentFarm.id, farmWorkspace, management.alerts, operations])
  if (query.loading || query.error) return <OwnerApiState {...query} t={t} />
  if (!overview) return <EmptyState title={t('Farm unavailable')} description={t('Choose another farm from the selector above.')} />
  const { farm, zones, nodes, currentMission: mission, environment, importantAlerts, activity } = overview
  const firstName = user.fullName.trim().split(/\s+/)[0]
  const locale = t('Current view') === 'Current view' ? 'en-GB' : 'vi-VN'
  const snapshotDate = new Intl.DateTimeFormat(locale, { day: 'numeric', month: 'short', year: 'numeric', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(VIEW_OPENED_AT))
  const snapshotTime = new Intl.DateTimeFormat(locale, { hour: '2-digit', minute: '2-digit', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(VIEW_OPENED_AT))
  const snapshotAge = timestamp => {
    const age = formatSnapshotAge(timestamp)
    if (t('ago') === 'ago') return age
    if (age === 'Not available' || age === 'Just now') return t(age)
    const [, count, unit] = age.match(/^(\d+) (minute|minutes|hour|hours) ago$/) ?? []
    return count ? `${count} ${t(unit)} ${t('ago')}` : age
  }
  const battery = getBatteryState(mission?.progress.batteryPercent)

  return <div className="app-ui owner-dashboard">
    <PageHeader eyebrow={t('YOUR FARM AT A GLANCE')} title={`${t('Good morning')}, ${firstName}`} description={t('Farm overview and current operating status.')}
      actions={<div className="owner-snapshot"><span>{t('Current view')}</span><time dateTime={VIEW_OPENED_AT}>{snapshotDate} · {snapshotTime}</time></div>} />

    <div className="owner-farm-context"><span><AppIcon name="map" size={17} /><strong>{farm.name}</strong></span><span>{zones.length} {t('zones')} <b>·</b> {farm.description || t('No description recorded')}</span></div>

    <section className="owner-kpis" aria-label={t('Farm key indicators')}>
      <StatCard label={t('Farm Health')} value={overview.health} icon={<AppIcon name="sun" />} className="owner-health" data-health={overview.health} description={t(overview.openAlertCount ? 'Review the important alerts below' : 'No unresolved alerts')} />
      <StatCard label={t('Online Sensors')} value={`${overview.onlineSensors}/${nodes.length}`} icon={<AppIcon name="sensor" />} description={`${nodes.filter(({ status, isActive }) => status === 'OFFLINE' && isActive).length} ${t('offline')} · ${nodes.some(({ isActive }) => !isActive) ? `${nodes.filter(({ isActive }) => !isActive).length} ${t('disabled')}` : `${zones.length} ${t('zones')}`}`} />
      <StatCard label={t('Active Missions')} value={overview.activeMissionCount} icon={<AppIcon name="drone" />} description={t(mission ? 'Collecting your farm’s data' : 'No collection in progress')} />
      <StatCard label={t('Open Alerts')} value={overview.openAlertCount} icon={<AppIcon name="alert" />} description={t('Including acknowledged alerts')} />
      <StatCard label={t('Latest Sync')} value={snapshotAge(overview.latestSyncAt)} icon={<AppIcon name="cloud" />} className="owner-sync" description={t('Latest sensor data received')} />
    </section>

    <div className="owner-main-grid">
      <SectionCard title={t('Farm Overview Map')} description={t('A connected view of your land')} className="owner-map-card"
        actions={<span className="owner-subtle-label">{zones.length} {t('monitored zones')}</span>}>
        <FarmOverviewMap farm={farm} zones={zones} nodes={nodes} mission={mission} />
      </SectionCard>

      <SectionCard title={t('Current Mission')} description={t('Your latest collection in progress')} className="owner-mission-card">
        {mission ? <>
          <div className="owner-mission-heading"><span className="owner-mission-icon"><AppIcon name="drone" size={28} /></span><StatusBadge status={mission.status} label="IN_PROGRESS" /></div>
          <p className="owner-mission-code">{mission.code}</p>
          <h3>{mission.name}</h3>
          <p className="owner-mission-summary">{t('Gathering fresh readings across your farm, so you can see how your crops are doing.')}</p>
          <div className="owner-mission-progress">{mission.progress.totalTargets ? <OwnerProgressBar value={mission.progress.collectedTargets} max={mission.progress.totalTargets} label={`${mission.progress.collectedTargets}/${mission.progress.totalTargets} ${t('targets collected')}`} t={t} /> : <p>{t('Collection progress unavailable in mission summary.')}</p>}
            <p>{t('Waypoint completion unavailable.')}</p></div>
          <div className="owner-mission-battery"><span>{t('UAV battery')}</span>{battery.level === null
            ? <span className="app-ui app-ui-battery" role="img" aria-label={`${t('UAV battery')}: ${t('Unknown battery level')}`}><span className="app-ui-battery-case" aria-hidden="true"><span /></span><span aria-hidden="true">{t('Unknown')}</span></span>
            : <BatteryIndicator value={mission.progress.batteryPercent} label={t('UAV battery')} />}</div>
          <Link className="app-ui-button owner-detail-link" to={`/app/missions/${mission.id}`}>{t('View Details')} <AppIcon name="chevron" size={15} /></Link>
        </> : <EmptyState title={t('No active mission')} description={t('Your next collection will appear here once it starts.')} />}
      </SectionCard>

      <section className="owner-environment" aria-labelledby="owner-environment-title">
        <div className="owner-section-heading"><div><h2 id="owner-environment-title">{t('Environment Summary')}</h2><p>{t('Latest received averages from online sensors')}</p></div><Link to="/app/sensor-data" className="owner-text-link">{t('View sensor data')} <AppIcon name="chevron" size={15} /></Link></div>
        <div className="owner-environment-grid">{environment.map((metric) => <MetricCard key={metric.id} label={metric.name} value={metric.value} unit={metric.value !== null ? metric.unit : undefined}
          className={`owner-environment-metric owner-metric-${metric.id}`} description={metric.sensorCount ? `${metric.sensorCount} ${t('reporting sensors')}` : t('No readings available')}>
          <EnvironmentTrend metric={metric} t={t} />
          {metric.attentionCount > 0 && <span className="owner-metric-attention">{metric.attentionCount} {t('sensor needs attention')}</span>}
        </MetricCard>)}</div>
      </section>

      <SectionCard title={t('Important Alerts')} description={t('What needs your attention')} className="owner-alerts-card"
        actions={<Link to="/app/alerts" className="owner-text-link">{t('View all alerts')} <AppIcon name="chevron" size={15} /></Link>}>
        {importantAlerts.length ? <ul className="owner-alert-list">{importantAlerts.slice(0, 4).map((alert) => <li key={alert.id} data-severity={alert.severity}>
          <span className="owner-alert-icon"><AppIcon name="alert" size={18} /></span>
          <div><div className="owner-alert-title"><h3>{alert.title}</h3><AlertSeverityBadge severity={alert.severity} /></div><p>{alert.message}</p><span className="owner-subtle-label">{alert.status === 'ACKNOWLEDGED' ? t('Acknowledged · ') : ''}{snapshotAge(alert.openedAt)}</span></div>
        </li>)}</ul> : <EmptyState title={t('All clear')} description={t('No critical or warning alerts for this farm.')} />}
      </SectionCard>

      <SectionCard title={t('Recent Activity')} description={t('The latest updates from your farm')} className="owner-activity-card">
        {activity.length ? <ol className="owner-activity-list">{activity.map((event) => <li key={event.id}>
          <span className="owner-activity-icon"><AppIcon name={event.icon === 'check' ? 'alert' : event.icon} size={16} /></span>
          <div><h3>{event.title}</h3><p>{event.description}</p><time dateTime={event.at}>{snapshotAge(event.at)}</time></div>
        </li>)}</ol> : <EmptyState title={t('No recent activity')} description={t('Collection and alert updates will appear here.')} />}
      </SectionCard>
    </div>
    <p className="owner-footnote">{t('Backend records · Times are relative to the current view. Farm health reflects unresolved alerts.')}</p>
  </div>
}

import { useAnalytics } from '../../services/api/useAnalytics'
import ApiState from '../../components/app/ApiState'
import { useState } from 'react'
import { Link, useOutletContext } from 'react-router-dom'
import { useOperations } from '../../context/OperationsContext'
import { useAuth } from '../../context/AuthContext'
import { ROLES } from '../../config/roles'
import { PERMISSIONS } from '../../config/permissions'
import { selectAnalytics, ANALYTICS_DEFAULT_RANGE, formatMetric, readingThreshold } from '../../services/api/analytics'
import { displayTime } from '../../utils/display'
import { useUiText } from '../../utils/useUiText'
import { PageHeader, StatCard, SectionCard, ChartCard, DataTable, FilterBar, StatusBadge, EmptyState } from '../../components/app/ui'
import EnvironmentalChart from '../../components/app/EnvironmentalChart'
import ZoneComparison from '../../components/app/ZoneComparison'
import '../../styles/analytics.css'

function SensorDataView({ currentFarm, selectFarm, farmWorkspace, compareOnly }) {
  const t = useUiText({
    'SENSOR TECHNICAL DATA': 'DỮ LIỆU KỸ THUẬT CẢM BIẾN',
    'ENVIRONMENTAL DATA': 'DỮ LIỆU MÔI TRƯỜNG',
    'Compare Zones': 'So sánh khu vực',
    'Sensor Data': 'Dữ liệu cảm biến',
    'A clear view of farm conditions.': 'Tổng quan rõ ràng về điều kiện trang trại.',
    'Explore recorded measurements and configured threshold bands.': 'Khám phá số liệu đã ghi nhận và các ngưỡng được thiết lập.',
    'Read-only data': 'Dữ liệu chỉ xem',
    'Sensor data filters': 'Bộ lọc dữ liệu cảm biến',
    'Reset filters': 'Đặt lại bộ lọc',
    Farm: 'Trang trại',
    Zone: 'Khu vực',
    'All zones': 'Tất cả khu vực',
    Sensor: 'Cảm biến',
    'All sensors': 'Tất cả cảm biến',
    'Sensor Type': 'Loại cảm biến',
    From: 'Từ',
    To: 'Đến',
    'Include buffered gateway readings': 'Bao gồm số liệu đang lưu tạm trên cổng kết nối',
    'Date range uses measurement time in Vietnam (UTC+7), up to 31 days.': 'Khoảng thời gian dựa trên thời điểm đo tại Việt Nam (UTC+7), tối đa 31 ngày.',
    'Includes local, unsynchronized measurements for technical inspection.': 'Bao gồm số liệu cục bộ chưa đồng bộ để kiểm tra kỹ thuật.',
    'Only synchronized, valid measurements are shown.': 'Chỉ hiển thị số liệu hợp lệ đã đồng bộ.',
    'Historical data can include currently offline sensors.': 'Dữ liệu lịch sử có thể bao gồm cảm biến hiện đang ngoại tuyến.',
    'Choose valid start and end times (Vietnam time).': 'Chọn thời gian bắt đầu và kết thúc hợp lệ (giờ Việt Nam).',
    'Start time must be before or equal to end time.': 'Thời gian bắt đầu phải trước hoặc bằng thời gian kết thúc.',
    'Choose a date range of 31 days or less.': 'Chọn khoảng thời gian không quá 31 ngày.',
    Unavailable: 'Không khả dụng',
    'Not configured': 'Chưa cấu hình',
    'Below band': 'Dưới ngưỡng',
    'Above band': 'Trên ngưỡng',
    'Within configured limits': 'Trong giới hạn đã cấu hình',
    'Latest Readings': 'Số liệu mới nhất',
    'Latest reading per channel within the selected date range': 'Số liệu mới nhất của mỗi kênh trong khoảng thời gian đã chọn',
    'Sensor disabled': 'Cảm biến đã tắt',
    'Sensor online': 'Cảm biến trực tuyến',
    'Sensor offline': 'Cảm biến ngoại tuyến',
    'Stale measurement': 'Số liệu đã cũ',
    'Buffered locally': 'Đã lưu tạm cục bộ',
    'No readings match these filters': 'Không có số liệu phù hợp với các bộ lọc này',
    'Choose another range, zone, sensor or sensor type.': 'Chọn khoảng thời gian, khu vực, cảm biến hoặc loại cảm biến khác.',
    'Historical Trend': 'Xu hướng lịch sử',
    'Hourly means give each contributing channel equal weight. Missing hours remain gaps; no values are interpolated.': 'Giá trị trung bình theo giờ được tính với trọng số bằng nhau cho mỗi kênh. Các giờ thiếu dữ liệu được giữ nguyên; không nội suy giá trị.',
    'Reading statistics': 'Thống kê số liệu',
    Minimum: 'Tối thiểu',
    Maximum: 'Tối đa',
    Average: 'Trung bình',
    'individual measurements in range': 'lần đo trong khoảng thời gian',
    'Threshold Bands': 'Ngưỡng cảnh báo',
    'Configured channel warning limits, shown as the shaded chart band. Limits are configured per sensor channel.': 'Giới hạn cảnh báo đã cấu hình cho từng kênh, được biểu thị bằng dải màu trên biểu đồ. Giới hạn được thiết lập theo từng kênh cảm biến.',
    'Values outside this band may require review.': 'Các giá trị nằm ngoài ngưỡng này có thể cần được kiểm tra.',
    'Channels have different limits. No shared band is drawn.': 'Các kênh có giới hạn khác nhau. Không hiển thị dải ngưỡng chung.',
    'No channel readings available to show a threshold band.': 'Không có số liệu kênh để hiển thị dải ngưỡng.',
    'Channel thresholds are read-only. System defaults do not retroactively change existing channel limits or captured alerts.': 'Ngưỡng của kênh chỉ có thể xem. Giá trị mặc định của hệ thống không thay đổi ngược các giới hạn hiện có hoặc cảnh báo đã ghi nhận.',
    'Open Compare Zones': 'Mở trang so sánh khu vực',
    'Sensor Reading Table': 'Bảng số liệu cảm biến',
    'Measured values and timestamps from the shared records.': 'Giá trị đo và dấu thời gian từ các bản ghi dùng chung.',
    'Sensor readings': 'Số liệu cảm biến',
    Value: 'Giá trị',
    'Measured At': 'Thời điểm đo',
    Threshold: 'Ngưỡng',
    'Data Availability': 'Tình trạng dữ liệu',
    Synchronized: 'Đã đồng bộ',
    'Source Record Key': 'Mã bản ghi nguồn',
    Gateway: 'Cổng kết nối',
    Imported: 'Đã nhập',
    'X readings · Page Y of Z': 'X số liệu · Trang Y / Z',
    Previous: 'Trước',
    Next: 'Tiếp',
  })
  const { operations: demoOperations } = useOperations()
  const { activeRole, can } = useAuth()
  const simplified = activeRole === ROLES.FARM_OWNER
  const technical = activeRole === ROLES.UAV_DEVICE_OPERATOR
  const [filters, setFilters] = useState({ ...ANALYTICS_DEFAULT_RANGE, zoneId: '', sensorId: '', typeId: '1', includeBuffered: false })
  const [page, setPage] = useState(0)
  const query = useAnalytics(currentFarm.id, filters)
  const operations = query.data ?? demoOperations
  const analyticsTypes = operations.sensorTypes ?? []
  const selectedType = analyticsTypes.some(t => t.id === Number(filters.typeId)) ? filters.typeId : analyticsTypes[0]?.id ?? ''
  const effective = { ...filters, typeId: selectedType, farmId: currentFarm.id, includeBuffered: technical && filters.includeBuffered }
  const data = selectAnalytics(operations, farmWorkspace, effective)
  const metric = (value, type) => value === null || value === undefined ? t('Unavailable') : formatMetric(value, type)
  const localizedThreshold = (value, channel) => {
    const threshold = readingThreshold(value, channel)
    return { ...threshold, label: t(threshold.label) }
  }
  const update = (values) => { setFilters({ ...filters, ...values }); setPage(0) }
  const pages = Math.max(1, Math.ceil(data.rows.length / 20)), currentPage = Math.min(page, pages - 1)
  return <div className="app-ui analytics-page"><ApiState {...query} /><PageHeader eyebrow={t(technical ? 'SENSOR TECHNICAL DATA' : 'ENVIRONMENTAL DATA')} title={t(compareOnly ? 'Compare Zones' : 'Sensor Data')} description={`${currentFarm.name} · ${t(simplified ? 'A clear view of farm conditions.' : 'Explore recorded measurements and configured threshold bands.')}`} actions={<StatusBadge status="INFO" label={t('Read-only data')} />} />
    <FilterBar label={t('Sensor data filters')} actions={<button className="app-ui-button" onClick={() => update({ ...ANALYTICS_DEFAULT_RANGE, zoneId: '', sensorId: '', typeId: '1', includeBuffered: false })}>{t('Reset filters')}</button>}>
      <label>{t('Farm')}<select aria-label={t('Farm')} value={currentFarm.id} onChange={(event) => selectFarm(Number(event.target.value))}>{farmWorkspace.farms.map((farm) => <option key={farm.id} value={farm.id}>{farm.name}</option>)}</select></label>
      {!compareOnly && <><label>{t('Zone')}<select aria-label={t('Zone')} value={filters.zoneId} onChange={(event) => update({ zoneId: event.target.value, sensorId: '' })}><option value="">{t('All zones')}</option>{data.zones.map((zone) => <option key={zone.id} value={zone.id}>{zone.name}</option>)}</select></label><label>{t('Sensor')}<select aria-label={t('Sensor')} value={filters.sensorId} onChange={(event) => update({ sensorId: event.target.value })}><option value="">{t('All sensors')}</option>{data.nodes.map((node) => <option key={node.id} value={node.id}>{node.deviceCode} · {node.name}</option>)}</select></label></>}
      <label>{t('Sensor Type')}<select aria-label={t('Sensor Type')} value={selectedType} onChange={(event) => update({ typeId: event.target.value })}>{analyticsTypes.map((type) => <option key={type.id} value={type.id}>{type.name}</option>)}</select></label>
      <label>{t('From')}<input type="datetime-local" aria-label={t('From')} value={filters.from} onChange={(event) => update({ from: event.target.value })} /></label><label>{t('To')}<input type="datetime-local" aria-label={t('To')} value={filters.to} onChange={(event) => update({ to: event.target.value })} /></label>
      {technical && <label className="analytics-check"><input type="checkbox" checked={filters.includeBuffered} onChange={(event) => update({ includeBuffered: event.target.checked })} />{t('Include buffered gateway readings')}</label>}
    </FilterBar>
    <p className="analytics-note">{t('Date range uses measurement time in Vietnam (UTC+7), up to 31 days.')}  {t(effective.includeBuffered ? 'Includes local, unsynchronized measurements for technical inspection.' : 'Only synchronized, valid measurements are shown.')} {t('Historical data can include currently offline sensors.')}</p>
    {data.error ? <p role="alert" className="analytics-error">{t(data.error)}</p> : <>
      {!compareOnly && <><SectionCard title={t('Latest Readings')} description={`${t('Latest reading per channel within the selected date range')} · ${data.type.name} (${data.type.unit})`}>
        <div className="analytics-latest">{data.latest.length ? data.latest.map((row) => <article key={row.id}><strong>{row.node.deviceCode}</strong><span className="analytics-reading-value">{metric(row.value, row.type)} <small>{row.type.unit}</small></span><small>{row.zone.name}</small><time dateTime={row.measuredAt}>{displayTime(row.measuredAt)}</time><StatusBadge status={row.node.isActive && row.node.status === 'ONLINE' ? 'ONLINE' : 'WARNING'} label={!row.node.isActive ? t('Sensor disabled') : row.node.status === 'ONLINE' ? t('Sensor online') : t('Sensor offline')} />{row.stale && <StatusBadge status="WARNING" label={t('Stale measurement')} />}{!row.receivedAt && <StatusBadge status="PENDING" label={t('Buffered locally')} />}</article>) : <EmptyState title={t('No readings match these filters')} description={t('Choose another range, zone, sensor or sensor type.')} />}</div>
      </SectionCard>
      <ChartCard title={`${t('Historical Trend')} · ${data.type.name} (${data.type.unit})`} caption={t('Hourly means give each contributing channel equal weight. Missing hours remain gaps; no values are interpolated.')}><EnvironmentalChart series={data.series} type={data.type} threshold={data.threshold} /></ChartCard>
      <section className="analytics-stats" aria-label={t('Reading statistics')}>{[['Minimum', data.stats.min], ['Maximum', data.stats.max], ['Average', data.stats.average]].map(([label, value]) => <StatCard key={label} label={t(label)} value={metric(value, data.type)} unit={value === null ? '' : data.type.unit} description={`${data.stats.count} ${t('individual measurements in range')}`} />)}</section>
      <SectionCard title={t('Threshold Bands')} description={t('Configured channel warning limits, shown as the shaded chart band. Limits are configured per sensor channel.')}><p>{data.threshold ? `${data.threshold.min}–${data.threshold.max} ${data.type.unit}. ${t('Values outside this band may require review.')}` : data.channels.length ? t('Channels have different limits. No shared band is drawn.') : t('No channel readings available to show a threshold band.')}</p>{!simplified && <p className="analytics-note">{t('Channel thresholds are read-only. System defaults do not retroactively change existing channel limits or captured alerts.')}</p>}</SectionCard></>}
      {can(PERMISSIONS.ZONES_COMPARE) && <><ZoneComparison operations={operations} workspace={farmWorkspace} filters={effective} />{!compareOnly && <Link className="app-ui-button analytics-compare-link" to="/app/sensor-data/compare-zones">{t('Open Compare Zones')}</Link>}</>}
      {!compareOnly && <SectionCard title={t('Sensor Reading Table')} description={t('Measured values and timestamps from the shared records.')}><DataTable caption={t('Sensor readings')} rows={data.rows.slice(currentPage * 20, (currentPage + 1) * 20)} columns={[
        { key: 'node', header: t('Sensor'), render: (node) => node.deviceCode }, { key: 'zone', header: t('Zone'), render: (zone) => zone.name }, { key: 'type', header: t('Sensor Type'), render: (type) => type.name },
        { key: 'value', header: t('Value'), render: (value, row) => `${metric(value, row.type)} ${row.type.unit}` }, { key: 'measuredAt', header: t('Measured At'), render: displayTime },
        { key: 'band', header: t('Threshold'), render: (_, row) => <StatusBadge {...localizedThreshold(row.value, row.channel)} /> },
        ...(!simplified ? [{ key: 'receivedAt', header: t('Data Availability'), render: (value) => <StatusBadge status={value ? 'SUCCESS' : 'PENDING'} label={value ? t('Synchronized') : t('Buffered locally')} /> }] : []),
        ...(technical ? [{ key: 'sourceRecordKey', header: t('Source Record Key') }, { key: 'gatewayId', header: t('Gateway'), render: (id) => operations.gateways.find((gateway) => gateway.id === id)?.code ?? t('Imported') }] : []),
      ]} footer={<div className="analytics-pagination"><span>{t('X readings · Page Y of Z').replace('X', data.rows.length).replace('Y', currentPage + 1).replace('Z', pages)}</span><button className="app-ui-button" disabled={currentPage === 0} onClick={() => setPage(currentPage - 1)}>{t('Previous')}</button><button className="app-ui-button" disabled={currentPage >= pages - 1} onClick={() => setPage(currentPage + 1)}>{t('Next')}</button></div>} /></SectionCard>}
    </>}
  </div>
}

export default function SensorDataPage({ compareOnly = false }) {
  const context = useOutletContext()
  const { activeRole } = useAuth()
  return <SensorDataView key={`${context.currentFarm.id}-${activeRole}-${compareOnly}`} {...context} compareOnly={compareOnly} />
}

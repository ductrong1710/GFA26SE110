import { useState } from 'react'
import { useOutletContext } from 'react-router-dom'
import { useAuth } from '../../context/AuthContext'
import { REPORT_TYPES } from '../../config/reports'
import { PERMISSIONS } from '../../config/permissions'
import { loadReport, REPORT_DEFAULT_RANGE } from '../../services/api/reports'
import { reportCsv } from '../../services/api/reportCsv'
import { useUiText } from '../../utils/useUiText'

import { PageHeader, SectionCard, ChartCard, FilterBar, StatCard, DataTable, EmptyState, StatusBadge } from '../../components/app/ui'
import EnvironmentalChart from '../../components/app/EnvironmentalChart'
import '../../styles/analytics.css'
import '../../styles/reports.css'

const translations = {
  'No matching records': 'Không có bản ghi phù hợp',
  'Try another farm, zone or date range.': 'Hãy thử trang trại, khu vực hoặc khoảng ngày khác.',
  Unavailable: 'Không khả dụng',
  Yes: 'Có',
  No: 'Không',
  Previous: 'Trước',
  Next: 'Tiếp',
  rows: 'bản ghi',
  Page: 'Trang',
  'Reports unavailable': 'Báo cáo không khả dụng',
  'Your active role does not have report access.': 'Vai trò hiện tại của bạn không có quyền truy cập báo cáo.',
  'Report preview updated from backend records.': 'Đã cập nhật bản xem trước báo cáo từ các bản ghi máy chủ.',
  'CSV export created from the generated preview, including all matching rows.': 'Đã tạo tệp CSV từ bản xem trước, bao gồm tất cả các hàng phù hợp.',
  'FARM INSIGHTS': 'THÔNG TIN TRANG TRẠI',
  Reports: 'Báo cáo',
  'Build a report preview from backend farm records.': 'Tạo bản xem trước báo cáo từ dữ liệu trang trại trên máy chủ.',
  'Report workspace': 'Không gian báo cáo',
  'Read-only report preview': 'Bản xem trước báo cáo chỉ đọc',
  'Report filters': 'Bộ lọc báo cáo',
  'Generate Report': 'Tạo báo cáo',
  'Update Preview': 'Cập nhật bản xem trước',
  'Report Type': 'Loại báo cáo',
  Farm: 'Trang trại',
  Zone: 'Khu vực',
  Vietnam: 'Việt Nam',
  From: 'Từ',
  To: 'Đến',
  'All zones': 'Tất cả khu vực',
  'Dates use Vietnam time (UTC+7), up to 31 days. Mission reports support farm and date filters; device reports show current state. Filters are applied only when you update the preview.': 'Ngày giờ theo giờ Việt Nam (UTC+7), tối đa 31 ngày. Báo cáo nhiệm vụ hỗ trợ lọc theo trang trại và ngày; báo cáo thiết bị hiển thị trạng thái hiện tại. Bộ lọc chỉ được áp dụng khi bạn cập nhật bản xem trước.',
  'Filters have changed. Update the preview before exporting.': 'Bộ lọc đã thay đổi. Hãy cập nhật bản xem trước trước khi xuất.',
  'Your report preview will appear here': 'Bản xem trước báo cáo sẽ xuất hiện tại đây',
  'Choose a report type and filters, then generate or update the preview.': 'Chọn loại báo cáo và bộ lọc, sau đó tạo hoặc cập nhật bản xem trước.',
  'Export CSV': 'Xuất CSV',
  'Export PDF (Prototype)': 'Xuất PDF (bản thử nghiệm)',
  'PDF export is a prototype. No PDF file was generated. Use Export CSV for a downloadable report.': 'Chức năng xuất PDF đang ở dạng thử nghiệm. Không có tệp PDF nào được tạo. Hãy dùng Xuất CSV để tải báo cáo.',
  'High-level summary; individual operational records are not included.': 'Tóm tắt tổng quan; không bao gồm từng bản ghi vận hành.',
  'Read-only preview of matching shared records.': 'Bản xem trước chỉ đọc của các bản ghi dùng chung phù hợp.',
  'This is a snapshot: regenerate to include subsequent changes.': 'Đây là ảnh chụp dữ liệu: hãy tạo lại để bao gồm các thay đổi sau đó.',
  'Report summary': 'Tóm tắt báo cáo',
  'Historical chart': 'Biểu đồ lịch sử',
  'Hourly means from synchronized readings; missing hours remain gaps.': 'Giá trị trung bình theo giờ từ số liệu đã đồng bộ; các giờ thiếu vẫn để trống.',
  'Zone comparison': 'So sánh khu vực',
  'Uses the same farm, selected zone and date range as this report. Choose All zones to compare the whole farm.': 'Sử dụng cùng trang trại, khu vực đã chọn và khoảng ngày của báo cáo này. Chọn Tất cả khu vực để so sánh toàn trang trại.',
  'Report definitions': 'Định nghĩa báo cáo',
  Loading: 'Đang tải',
  'Try again': 'Thử lại',
}

function ReportTable({ item, pageSize = 20 }) {
  const t = useUiText(translations)
  const [page, setPage] = useState(0)
  const pages = Math.max(1, Math.ceil(item.rows.length / pageSize))
  return <SectionCard title={item.title}><DataTable caption={item.title} rowKey={row => `${row.deviceType ?? ''}:${row.id}`} rows={item.rows.slice(page * pageSize, (page + 1) * pageSize)} columns={item.columns.map((column) => ({ ...column,
    render: (value) => ['status', 'severity'].includes(column.key) ? <StatusBadge status={value} /> : value === null || value === undefined ? t('Unavailable') : typeof value === 'boolean' ? t(value ? 'Yes' : 'No') : value,
  }))} emptyState={<EmptyState title={t('No matching records')} description={t('Try another farm, zone or date range.')} />} footer={pages > 1 ? <div className="analytics-pagination"><span>{item.rows.length} {t('rows')} · {t('Page')} {page + 1} / {pages}</span><button className="app-ui-button" disabled={page === 0} onClick={() => setPage(page - 1)}>{t('Previous')}</button><button className="app-ui-button" disabled={page === pages - 1} onClick={() => setPage(page + 1)}>{t('Next')}</button></div> : `${item.rows.length} ${t('rows')}`} /></SectionCard>
}

function ReportsWorkspace() {
  const t = useUiText(translations)
  const { activeRole, can } = useAuth()
  const { currentFarm, farmWorkspace } = useOutletContext()
  const types = can(PERMISSIONS.REPORTS_VIEW) ? REPORT_TYPES : []
  const [filters, setFilters] = useState({ ...REPORT_DEFAULT_RANGE, farmId: currentFarm.id, zoneId: '', type: types[0]?.id ?? '', metricId: '1' })
  const [preview, setPreview] = useState(null)
  const [revision, setRevision] = useState(0)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [notice, setNotice] = useState('')
  const change = (values) => { setFilters({ ...filters, ...values }); setError(''); setNotice('') }
  const dirty = preview && JSON.stringify(filters) !== JSON.stringify(preview.filters)
  const generate = async (event) => {
    event.preventDefault()
    setBusy(true)
    try { setPreview(await loadReport(filters, farmWorkspace, types.find(t => t.id === filters.type)?.name, activeRole)); setRevision(revision + 1); setError(''); setNotice(t('Report preview updated from backend records.')) } catch (cause) { setError(cause.message) } finally { setBusy(false) }
  }
  const exportCsv = () => {
    try {
      const csv = reportCsv(preview, activeRole)
      const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8;' }))
      const link = document.createElement('a')
      link.href = url; link.download = `${preview.type}-report-farm-${preview.filters.farmId}.csv`
      document.body.appendChild(link); link.click(); link.remove()
      setTimeout(() => URL.revokeObjectURL(url), 1000)
      setNotice(t('CSV export created from the generated preview, including all matching rows.'))
    } catch (cause) { setError(cause.message) }
  }
  if (!types.length) return <EmptyState title={t('Reports unavailable')} description={t('Your active role does not have report access.')} />
  return <div className="app-ui analytics-page reports-page">
    <PageHeader eyebrow={t('FARM INSIGHTS')} title={t('Reports')} description={t('Build a report preview from backend farm records.')} actions={<StatusBadge status="INFO" label={t(can(PERMISSIONS.REPORTS_GENERATE) ? 'Report workspace' : 'Read-only report preview')} />} />
    <FilterBar label={t('Report filters')} onSubmit={generate} actions={<button type="submit" disabled={busy} className="app-ui-button app-ui-button--primary">{t(can(PERMISSIONS.REPORTS_GENERATE) ? 'Generate Report' : 'Update Preview')}</button>}>
      <label>{t('Report Type')}<select aria-label={t('Report Type')} value={filters.type} onChange={(event) => change({ type: event.target.value })}>{types.map((type) => <option key={type.id} value={type.id}>{type.name}</option>)}</select></label>
      <label>{t('Farm')}<select aria-label={t('Farm')} value={filters.farmId} onChange={(event) => change({ farmId: Number(event.target.value), zoneId: '' })}>{farmWorkspace.farms.map((farm) => <option key={farm.id} value={farm.id}>{farm.name}</option>)}</select></label>
      <label>{t('Zone')}<select disabled={['mission', 'device'].includes(filters.type)} aria-label={t('Zone')} value={filters.zoneId} onChange={(event) => change({ zoneId: event.target.value })}><option value="">{t('All zones')}</option>{farmWorkspace.zones.filter(({ farmId }) => farmId === Number(filters.farmId)).map((zone) => <option key={zone.id} value={zone.id}>{zone.name}</option>)}</select></label>
      <label>{t('From')}<input aria-label={t('From')} type="datetime-local" disabled={filters.type === 'device'} required value={filters.from} onChange={(event) => change({ from: event.target.value })} /></label><label>{t('To')}<input aria-label={t('To')} type="datetime-local" required value={filters.to} onChange={(event) => change({ to: event.target.value })} /></label>
      
    </FilterBar>
    <p className="analytics-note">{t('Dates use Vietnam time (UTC+7), up to 31 days. Mission reports support farm and date filters; device reports show current state. Filters are applied only when you update the preview.')}</p>
    {error && <p role="alert" className="analytics-error">{error}</p>}{notice && <p role="status" className="reports-notice">{notice}</p>}
    {dirty && <p role="status" className="reports-pending">{t('Filters have changed. Update the preview before exporting.')}</p>}
    {!preview ? <EmptyState title={t('Your report preview will appear here')} description={t('Choose a report type and filters, then generate or update the preview.')} /> : <div className="reports-preview" key={revision}>
      <SectionCard title={preview.title} description={`${preview.farm} · ${preview.zone} · ${preview.filters.from.replace('T', ' ')} - ${preview.filters.to.replace('T', ' ')} (${t('Vietnam')})`} actions={can(PERMISSIONS.REPORTS_GENERATE) && <><button className="app-ui-button" disabled={Boolean(dirty)} onClick={exportCsv}>{t('Export CSV')}</button><button className="app-ui-button" disabled={Boolean(dirty)} onClick={() => setNotice(t('PDF export is a prototype. No PDF file was generated. Use Export CSV for a downloadable report.'))}>{t('Export PDF (Prototype)')}</button></>}>
        <p className="analytics-note">{t(preview.highLevel ? 'High-level summary; individual operational records are not included.' : 'Read-only preview of matching shared records.')} {t('This is a snapshot: regenerate to include subsequent changes.')}</p>
        <div className="reports-kpis" aria-label={t('Report summary')}>{preview.kpis.map(([label, value]) => <StatCard key={label} label={label} value={value} />)}</div>
      </SectionCard>
      {preview.chart && <><ChartCard title={`${t('Historical chart')} · ${preview.chart.type.name} (${preview.chart.type.unit})`} caption={t('Hourly means from synchronized readings; missing hours remain gaps.')}><EnvironmentalChart series={preview.chart.series} type={preview.chart.type} threshold={preview.chart.threshold} /></ChartCard><ChartCard title={t('Zone comparison')} caption={t('Uses the same farm, selected zone and date range as this report. Choose All zones to compare the whole farm.')}><EnvironmentalChart series={preview.chart.comparison.series} type={preview.chart.type} zones={preview.chart.comparison.zones} /></ChartCard></>}
      <div className={preview.type === 'alert' || preview.type === 'device' ? 'reports-summary-grid' : 'reports-tables'}>{preview.tables.map((item) => <ReportTable key={item.title} item={item} />)}</div>
      {preview.detail && <ReportTable item={preview.detail} />}
      <SectionCard title={t('Report definitions')}><ul className="reports-definitions">{preview.notes.map((note) => <li key={note}>{note}</li>)}</ul></SectionCard>
    </div>}
  </div>
}

export default function ReportsPage() {
  const { activeRole } = useAuth()
  const { currentFarm } = useOutletContext()
  return <ReportsWorkspace key={`${activeRole}:${currentFarm.id}`} />
}

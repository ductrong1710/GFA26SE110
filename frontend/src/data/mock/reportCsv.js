import { canExportReport } from '../../config/reports.js'

// Quote all fields and neutralize spreadsheet formula prefixes in user-entered text.
export function csvCell(value) {
  if (value === null || value === undefined) return '""'
  const text = String(value)
  const safe = typeof value === 'string' && /^[\s\uFEFF]*[=+@-]/.test(text) ? `'${text}` : text
  return `"${safe.replaceAll('"', '""')}"`
}
export function reportCsv(report, role) {
  if (report.role !== role || !canExportReport(role, report.type)) throw new Error('Your active role cannot export this report.')
  const rows = [['Report', report.title], ['Farm', report.farm], ['Zone', report.zone], ['From (Vietnam UTC+7)', report.filters.from], ['To (Vietnam UTC+7)', report.filters.to], ['Data snapshot (UTC)', report.snapshotAt], [], ['Summary', 'Value'], ...report.kpis]
  for (const item of [...report.tables, ...(report.detail ? [report.detail] : [])]) {
    rows.push([], [item.title], item.columns.map(({ header }) => header), ...item.rows.map((row) => item.columns.map(({ key }) => row[key])))
  }
  rows.push([], ['Report definitions'], ...report.notes.map((note) => [note]))
  return '\uFEFF' + rows.map((row) => row.map(csvCell).join(',')).join('\r\n')
}

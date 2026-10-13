export function displayTime(at) {
  if (!at || !Number.isFinite(Date.parse(at))) return 'Not available'
  return new Intl.DateTimeFormat('en-GB', { timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }).format(new Date(at))
}
export const localDate = at => at && Number.isFinite(Date.parse(at)) ? new Date(Date.parse(at) + 7 * 3600000).toISOString().slice(0, 10) : ''
export const formatMetric = value => value === null || value === undefined || !Number.isFinite(Number(value)) ? '—' : Number(value).toLocaleString('en-GB', { maximumFractionDigits: 2 })

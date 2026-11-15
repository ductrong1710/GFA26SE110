export function displayTime(at) {
  if (!at || !Number.isFinite(Date.parse(at))) return 'Not available'
  return new Intl.DateTimeFormat('en-GB', { timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit' }).format(new Date(at))
}
const vietnameseTime = new Intl.DateTimeFormat('vi-VN', { timeZone: 'Asia/Ho_Chi_Minh', day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })

export function displayVietnameseTime(at) {
  return at && Number.isFinite(Date.parse(at)) ? vietnameseTime.format(new Date(at)) : 'Không có dữ liệu'
}

export const vietnamDateTimeInput = at => at && Number.isFinite(Date.parse(at)) ? new Date(Date.parse(at) + 7 * 3600000).toISOString().slice(0, 16) : ''
export const localDate = at => vietnamDateTimeInput(at).slice(0, 10)
export const parseVietnamDateTime = value => new Date(value + '+07:00')
export const formatMetric = value => value === null || value === undefined || !Number.isFinite(Number(value)) ? '—' : Number(value).toLocaleString('en-GB', { maximumFractionDigits: 2 })
export const formatVnd = value => value === null || value === undefined || !Number.isFinite(Number(value)) ? '—' : `${Number(value).toLocaleString('vi-VN', { maximumFractionDigits: 2 })}VNĐ`

import { api } from './index.js'

export const workflowLabels = {
  WAITING_ADMIN: 'Chờ Admin duyệt hồ sơ',
  ASSIGNED: 'Đã phân công', IN_PROGRESS: 'Đang thực hiện', REPORT_SUBMITTED: 'Chờ duyệt báo cáo',
  OWNER_CONFIRMED: 'Khách đã xác nhận', APPROVED: 'Đã duyệt', REWORK: 'Cần bổ sung', CLOSED: 'Hoàn tất',
  SUBMITTED: 'Chờ Admin kiểm tra', RETURNED: 'Đã trả bổ sung', DRAFT: 'Chờ ngày thuê', ACTIVE: 'Đang thuê',
  EXPIRED: 'Hết hạn chờ xử lý', ENDED: 'Đã kết thúc', PROPOSED: 'Chờ duyệt phương án',
  EXECUTED: 'Đã thực hiện, chờ hậu kiểm', APPLIED: 'Đã cập nhật chính thức', REJECTED: 'Đã từ chối',
  NEW: 'Mới', IN_REVIEW: 'Đang tư vấn', AWAITING_INFORMATION: 'Chờ thông tin', RESOLVED: 'Đã giải quyết',
  INSTALLATION: 'Lắp đặt', MAINTENANCE: 'Bảo trì', REPAIR: 'Sửa chữa', CROP: 'Cây trồng', DEVICE: 'Thiết bị',
}
export const label = value => workflowLabels[value] ?? value ?? '—'
export const money = value => value == null ? 'Chưa có giá' : `${new Intl.NumberFormat('vi-VN').format(value)} đ`
export const time = value => value ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(value)) : '—'
export const parseJson = (value, fallback = []) => { try { return typeof value === 'string' ? JSON.parse(value) : value ?? fallback } catch { return fallback } }
export const localInput = value => { const d = value ? new Date(value) : new Date(); return new Date(d.getTime() - d.getTimezoneOffset() * 60000).toISOString().slice(0, 16) }
export const iso = value => new Date(value).toISOString()
export function period(days = 30) {
  const to = new Date(); to.setHours(23, 59, 59, 999)
  const from = new Date(to); from.setDate(from.getDate() - days + 1); from.setHours(0, 0, 0, 0)
  return { from: from.toISOString(), to: to.toISOString() }
}
export async function allPages(path, query = {}, signal, auth = true) {
  const items = []
  for (let page = 1; ; page++) {
    const result = await api.request(path, { query: { ...query, page, pageSize: 100 }, signal, auth })
    const rows = result?.items ?? (Array.isArray(result) ? result : [])
    items.push(...rows)
    if (!result?.pagination || page >= result.pagination.totalPages) break
    if (!rows.length) break
  }
  return items
}
export const jobPath = (kind, id) => `/api/${kind === 'installation' ? 'installation' : 'service'}-jobs/${id}`
export function chartRows(rows = [], series = 'status') {
  const grouped = new Map()
  for (const row of rows) {
    const entry = grouped.get(row.date) ?? { date: row.date }
    entry[row[series]] = (entry[row[series]] ?? 0) + row.count
    grouped.set(row.date, entry)
  }
  return [...grouped.values()].sort((a, b) => a.date.localeCompare(b.date))
}
export function safeContact(value) {
  const text = value?.trim() ?? ''
  if (/^tel:\+?[\d ()-]+$/.test(text)) return text
  try { const url = new URL(text); return url.protocol === 'https:' ? url.href : null } catch { return null }
}

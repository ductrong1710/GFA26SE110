const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date())
const part = type => parts.find(p => p.type === type).value
const base = Date.parse(`${part('year')}-${part('month')}-${part('day')}`)
export function dateAt(offset, hour = 8) {
  return `${new Date(base + offset * 86400000).toISOString().slice(0, 10)}T${String(hour).padStart(2, '0')}:00`
}
export function dayOffset(value) { return Math.round((Date.parse(value.slice(0, 10)) - base) / 86400000) }
export function dateLabel(value) { return new Intl.DateTimeFormat('en-GB', { day: '2-digit', month: 'short', year: 'numeric', timeZone: 'UTC' }).format(new Date(value.slice(0, 10))) }
export function deviceCount(v) { return v.devices.reduce((sum, d) => sum + d.count, 0) }
export const reservationDays = 7
export function reservationEnd(departure, days = reservationDays) {
  if (!departure || !Number.isInteger(Number(days)) || Number(days) < 1 || Number(days) > 60) return ''
  const date = new Date(departure + ':00+07:00')
  if (Number.isNaN(date.getTime())) return ''
  return new Date(date.getTime() + (Number(days) * 24 + 7) * 3600000).toISOString().slice(0, 16)
}
export const technicians = ['Nguyen Minh Tuan', 'Tran Quoc Bao', 'Le Thanh Phong']
export const externalBookings = [{ technician: technicians[0], start: dateAt(0), end: dateAt(4, 18), farm: 'Thanh Binh Farm', type: 'Installation' }]
export function conflictFor(name, start, end, visits, excludeId) {
  if (!start || !end) return null
  const bookings = [...externalBookings, ...visits.filter(v => v.id !== excludeId && v.assignment && !v.completed).map(v => ({ technician: v.assignment.technician, start: v.assignment.departure, end: v.assignment.reservedUntil, farm: v.farm, type: 'Maintenance' }))]
  return bookings.find(b => b.technician === name && start < b.end && end > b.start)
}
export function createVisits() {
  const rows = [
    ['Da Lat Greenhouse', 'Xuan Tho, Da Lat', 2, 18, 2, 1],
    ['An Phu Farm', 'Cu Chi, Ho Chi Minh City', 4, 12, 1, 1],
    ['Mekong Riverside', 'Cai Be, Tien Giang', 6, 24, 2, 0],
    ['Bao Loc Tea Farm', 'Loc Thanh, Lam Dong', 7, 16, 1, 0],
    ['Tan Phu Orchard', 'Dinh Quan, Dong Nai', -2, 10, 1, 1],
    ['Hoa Binh Farm', 'Duc Trong, Lam Dong', -8, 14, 1, 0],
  ]
  return rows.map(([farm, address, offset, sensors, gateways, uavs], i) => ({
    id: i + 1, farm, address, due: dateAt(offset), ends: dateAt(90 + i * 15), contract: `RC-2026-${String(i + 41).padStart(3, '0')}`,
    contact: ['Nguyen Thi Lan', 'Tran Van Hung', 'Le Thi Mai'][i % 3], phone: '090 123 4567', completed: i === 5,
    devices: [{ name: 'Sensor nodes', count: sensors }, { name: 'Gateways', count: gateways }, { name: 'UAVs', count: uavs }],
    assignment: i === 1 || i === 5 ? { technician: technicians[1], departure: dateAt(offset - 1), workStart: dateAt(offset), reservationDays: 7, defaultDays: 7, reservedUntil: reservationEnd(dateAt(offset - 1)), actualCompletedAt: i === 5 ? dateAt(offset, 17) : null, note: 'Inspect connectivity, clean sensors and verify readings.' } : null,
  }))
}

import { alerts } from './alerts.js'
import { offsetMinutes } from './scenario.js'

const recipients = { 1: [1, 3, 4], 2: [1, 3, 4], 3: [1, 2, 3], 4: [2, 3], 5: [1, 5], 6: [1, 2], 7: [1, 5] }

export const notifications = alerts.flatMap((alert) => recipients[alert.id].map((userId) => ({
  id: alert.id * 100 + userId, userId, alertId: alert.id, channel: 'WEB',
  title: alert.title, message: alert.message, createdAt: offsetMinutes(alert.openedAt, 0.5),
  readAt: alert.status === 'CLOSED' ? alert.closedAt : null,
})))

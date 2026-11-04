import { navigation } from './navigation'
import { ROLES } from './roles'

export function getTopbarContext(pathname, role) {
  const owner = role === ROLES.FARM_OWNER
  const path = pathname.replace(/\/+$/, '')
  const item = [...navigation].sort((a, b) => b.path.length - a.path.length)
    .find(item => path === item.path || path.startsWith(item.path + '/'))
  let title = owner ? item?.ownerLabel : item?.label
  if (path === '/app/dashboard') title = owner ? 'Tổng quan nông trại' : role === ROLES.ADMINISTRATOR ? 'Farm operations' : role === ROLES.TECHNICIAN ? 'My work schedule' : 'Engineer workspace'
  if (/^\/app\/installation-requests\//.test(path)) title = owner ? 'Yêu cầu lắp đặt' : 'Installation request'
  else if (/^\/app\/work-orders\//.test(path)) title = owner ? 'Chi tiết công việc' : 'Work order details'
  else if (/^\/app\/consultations\//.test(path)) title = owner ? 'Hồ sơ tư vấn' : 'Consultation dossier'
  const farmPages = ['/app/dashboard', '/app/farms', '/app/sensors', '/app/devices', '/app/sensor-data', '/app/reports']
  const usesFarm = [ROLES.ADMINISTRATOR, ROLES.FARM_OWNER].includes(role)
    && farmPages.some(page => path === page || path.startsWith(page + '/'))
  return { title: title ?? (owner ? 'Không gian làm việc' : 'Workspace'), icon: item?.icon ?? 'grid', usesFarm }
}

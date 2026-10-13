import { ROLES } from '../../config/roles.js'

// Public domain identities. Passwords belong only to the separate demo login fixtures.
export const users = [
  { id: 1, fullName: 'Alex Nguyen', email: 'admin@smartfarm.test', roles: [ROLES.ADMINISTRATOR], activeRole: ROLES.ADMINISTRATOR, farmIds: [1, 2], status: 'ACTIVE' },
  { id: 2, fullName: 'Minh Tran', email: 'operator@smartfarm.test', roles: [ROLES.UAV_DEVICE_OPERATOR], activeRole: ROLES.UAV_DEVICE_OPERATOR, farmIds: [1], status: 'ACTIVE' },
  { id: 3, fullName: 'Linh Pham', email: 'user@smartfarm.test', roles: [ROLES.FARM_OWNER], activeRole: ROLES.FARM_OWNER, farmIds: [1, 2], status: 'ACTIVE' },
  { id: 4, fullName: 'Bao Le', email: 'engineer@smartfarm.test', roles: [ROLES.AGRICULTURAL_ENGINEER], activeRole: ROLES.AGRICULTURAL_ENGINEER, farmIds: [1, 2], status: 'ACTIVE' },
  { id: 5, fullName: 'An Vu', email: 'an.vu@smartfarm.test', roles: [ROLES.UAV_DEVICE_OPERATOR, ROLES.AGRICULTURAL_ENGINEER], activeRole: ROLES.UAV_DEVICE_OPERATOR, farmIds: [1], status: 'ACTIVE' },
]

import { ROLES } from '../../config/roles.js'

// Public demo fixtures only. These are not backend accounts or real credentials.
export const mockAccounts = [
  {
    id: 1,
    fullName: 'Quản trị thử nghiệm',
    email: 'admin@smartfarm.test',
    password: 'Demo@123',
    roles: [ROLES.ADMINISTRATOR],
    activeRole: ROLES.ADMINISTRATOR,
  },
  {
    id: 2,
    fullName: 'Vận hành UAV thử nghiệm',
    email: 'operator@smartfarm.test',
    password: 'Demo@123',
    roles: [ROLES.UAV_DEVICE_OPERATOR],
    activeRole: ROLES.UAV_DEVICE_OPERATOR,
  },
  {
    id: 3,
    fullName: 'Chủ nông trại thử nghiệm',
    email: 'user@smartfarm.test',
    password: 'Demo@123',
    roles: [ROLES.FARM_OWNER],
    activeRole: ROLES.FARM_OWNER,
  },
  {
    id: 4,
    fullName: 'Kỹ sư thử nghiệm',
    email: 'engineer@smartfarm.test',
    password: 'Demo@123',
    roles: [ROLES.AGRICULTURAL_ENGINEER],
    activeRole: ROLES.AGRICULTURAL_ENGINEER,
  },
]

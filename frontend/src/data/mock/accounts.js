import { users } from './users.js'

// Public demo fixtures only. These are not backend accounts or real credentials.
export const mockAccounts = users.filter(({ id }) => id <= 4).map((user) => ({
  ...user, roles: [...user.roles], password: 'Demo@123',
}))

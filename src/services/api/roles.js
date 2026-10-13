const backendRoles = Object.freeze({ FarmAdministrator: 'ADMINISTRATOR', FarmOwner: 'FARM_OWNER', FarmEngineer: 'AGRICULTURAL_ENGINEER' })
export function normalizeUser(user, preferredRole) {
  const roles = [...new Set((user.roles ?? []).map(role => backendRoles[role]).filter(Boolean))]
  return { ...user, backendRoles: user.roles ?? [], roles, activeRole: roles.includes(preferredRole) ? preferredRole : roles[0] ?? null, isMock: false }
}
export const toBackendRoles = roles => roles.map(role => Object.keys(backendRoles).find(key => backendRoles[key] === role)).filter(Boolean)

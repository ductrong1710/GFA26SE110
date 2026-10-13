// Human UI roles only. The Sensor Collection Engine is a device/service actor.
export const ROLES = Object.freeze({
  FARM_OWNER: 'FARM_OWNER',
  ADMINISTRATOR: 'ADMINISTRATOR',
  UAV_DEVICE_OPERATOR: 'UAV_DEVICE_OPERATOR',
  AGRICULTURAL_ENGINEER: 'AGRICULTURAL_ENGINEER',
})

export const HUMAN_ROLES = Object.freeze(Object.values(ROLES))

export const ROLE_LABELS = Object.freeze({
  [ROLES.FARM_OWNER]: 'Farm Owner',
  [ROLES.ADMINISTRATOR]: 'Administrator',
  [ROLES.UAV_DEVICE_OPERATOR]: 'UAV / Device Operator',
  [ROLES.AGRICULTURAL_ENGINEER]: 'Agricultural Engineer',
})

export const isHumanRole = (role) => HUMAN_ROLES.includes(role)

export function getActiveRole(user, { allowDemoRole = false } = {}) {
  if (!user || !isHumanRole(user.activeRole) || !Array.isArray(user.roles)) return null
  const assigned = user.roles.includes(user.activeRole)
  // DEMO ONLY: preview another human role without modifying assigned roles.
  return assigned || (allowDemoRole && user.isMock === true) ? user.activeRole : null
}

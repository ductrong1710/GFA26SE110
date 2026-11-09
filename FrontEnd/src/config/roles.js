// Human UI roles only. The Sensor Collection Engine is a device/service actor.
export const ROLES = Object.freeze({
  FARM_OWNER: 'FARM_OWNER',
  ADMINISTRATOR: 'ADMINISTRATOR',
  TECHNICIAN: 'TECHNICIAN',
  AGRICULTURAL_ENGINEER: 'AGRICULTURAL_ENGINEER',
})

export const HUMAN_ROLES = Object.freeze(Object.values(ROLES))
export const USER_ASSIGNABLE_ROLES = Object.freeze(
  HUMAN_ROLES.filter(role => role !== ROLES.ADMINISTRATOR),
)

export const ROLE_LABELS = Object.freeze({
  [ROLES.FARM_OWNER]: 'Farm Owner',
  [ROLES.ADMINISTRATOR]: 'Administrator',
  [ROLES.TECHNICIAN]: 'Technician',
  [ROLES.AGRICULTURAL_ENGINEER]: 'Agricultural Engineer',
})

export const isHumanRole = (role) => HUMAN_ROLES.includes(role)

export function getActiveRole(user) {
  if (!user || !isHumanRole(user.activeRole) || !Array.isArray(user.roles)) return null
  const assigned = user.roles.includes(user.activeRole)
  return assigned ? user.activeRole : null
}

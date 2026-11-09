import { api } from './index.js'

const credentialPath = gatewayId => `/api/gateways/${gatewayId}/credential`

// One-time responses belong only to the mounted dialog, never shared query state.
export const getGatewayCredentialStatus = (gatewayId, signal) => api.request(`${credentialPath(gatewayId)}/status`, { signal })
export const createGatewayCredential = (gatewayId, signal) => api.request(credentialPath(gatewayId), { method: 'POST', signal })
export const rotateGatewayCredential = (gatewayId, signal) => api.request(`${credentialPath(gatewayId)}/rotate`, { method: 'POST', signal })
export const revokeGatewayCredential = (gatewayId, signal) => api.request(credentialPath(gatewayId), { method: 'DELETE', signal })

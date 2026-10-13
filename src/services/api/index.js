import { createApiClient } from './apiClient.js'
import { session } from './session.js'
export const api = createApiClient({ baseUrl: import.meta.env?.VITE_API_BASE_URL || 'https://localhost:7043', session })

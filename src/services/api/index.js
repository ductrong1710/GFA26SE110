import { createApiClient } from './apiClient.js'
import { session } from './session.js'
import { API_BASE_URL, API_TIMEOUT } from '../../config/api.js'

export const api = createApiClient({ baseUrl: API_BASE_URL, timeout: API_TIMEOUT, session })

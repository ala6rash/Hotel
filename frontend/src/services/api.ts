import axios from 'axios'

const api = axios.create({ baseURL: '/api' })

// Attach JWT to every request
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('accessToken')
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

// Auto-refresh on 401
api.interceptors.response.use(
  (res) => res,
  async (error) => {
    const original = error.config
    if (error.response?.status === 401 && !original._retry) {
      original._retry = true
      const refreshToken = localStorage.getItem('refreshToken')
      if (refreshToken) {
        try {
          const { data } = await axios.post('/api/auth/refresh', refreshToken, {
            headers: { 'Content-Type': 'application/json' }
          })
          localStorage.setItem('accessToken', data.data.accessToken)
          localStorage.setItem('refreshToken', data.data.refreshToken)
          original.headers.Authorization = `Bearer ${data.data.accessToken}`
          return api(original)
        } catch {
          localStorage.clear()
          window.location.href = '/login'
        }
      }
    }
    return Promise.reject(error)
  }
)

export default api

// ─── Typed API functions ──────────────────────────────────────────────────────

export const authApi = {
  login: (email: string, password: string) =>
    api.post('/auth/login', { email, password }),
  logout: () => api.post('/auth/logout'),
}

export const hotelsApi = {
  getAll: (params?: object) => api.get('/hotels', { params }),
  getById: (id: number) => api.get(`/hotels/${id}`),
  create: (data: object) => api.post('/hotels', data),
  update: (id: number, data: object) => api.put(`/hotels/${id}`, data),
  delete: (id: number) => api.delete(`/hotels/${id}`),
  getStats: (id: number) => api.get(`/hotels/${id}/stats`),
}

export const bookingsApi = {
  getAll: (params?: object) => api.get('/bookings', { params }),
  getById: (id: number) => api.get(`/bookings/${id}`),
  triggerSync: (hotelId: number) => api.post(`/bookings/sync/${hotelId}`),
  updateStatus: (id: number, status: number) =>
    api.patch(`/bookings/${id}/status`, status),
}

export const credentialsApi = {
  getOTA: (hotelId: number) => api.get(`/credentials/ota/${hotelId}`),
  saveOTA: (data: object) => api.post('/credentials/ota', data),
  testOTA: (credId: number) => api.post(`/credentials/ota/${credId}/test`),
  deleteOTA: (credId: number) => api.delete(`/credentials/ota/${credId}`),
  getPMS: (hotelId: number) => api.get(`/credentials/pms/${hotelId}`),
  savePMS: (data: object) => api.post('/credentials/pms', data),
  testPMS: (credId: number) => api.post(`/credentials/pms/${credId}/test`),
}

export const onboardingApi = {
  start: (data: object) => api.post('/onboarding/start', data),
  configurePms: (hotelId: number, data: object) =>
    api.post(`/onboarding/${hotelId}/pms`, data),
  connectChannex: (hotelId: number, data: object) =>
    api.post(`/onboarding/${hotelId}/channex`, data),
  activateOTA: (hotelId: number, otaType: string, data: object) =>
    api.post(`/onboarding/${hotelId}/ota/${otaType}`, data),
  runSync: (hotelId: number) => api.post(`/onboarding/${hotelId}/sync`),
  getStatus: (hotelId: number) => api.get(`/onboarding/${hotelId}/status`),
}

import axios, { AxiosError } from 'axios'

/** Empty means same origin: the Vite proxy in dev, or the API's own wwwroot in production. */
export const API_BASE = import.meta.env.VITE_API_BASE ?? ''

const TOKEN_KEY = 'auction.token'

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY)
}

export function setToken(token: string | null): void {
  if (token) localStorage.setItem(TOKEN_KEY, token)
  else localStorage.removeItem(TOKEN_KEY)
}

export const api = axios.create({ baseURL: `${API_BASE}/api` })

api.interceptors.request.use((config) => {
  const token = getToken()
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

/** Raised by the session layer when the token is gone or rejected. */
export const SESSION_EXPIRED = 'auction:session-expired'

api.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    if (error.response?.status === 401) {
      setToken(null)
      window.dispatchEvent(new CustomEvent(SESSION_EXPIRED))
    }
    return Promise.reject(error)
  },
)

/**
 * Pulls the human-readable message out of a failure.
 * Auction rule violations come back as ProblemDetails with the auctioneer's wording
 * in `detail`, and that is what should reach the screen.
 */
export function errorMessage(error: unknown, fallback = 'Something went wrong.'): string {
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as
      | { detail?: string; title?: string; message?: string; errors?: Record<string, string[]> }
      | undefined

    if (data?.detail) return data.detail
    if (data?.message) return data.message

    if (data?.errors) {
      const first = Object.values(data.errors).flat()[0]
      if (first) return first
    }

    if (data?.title) return data.title
    if (!error.response) return 'Cannot reach the auction server.'
  }

  return fallback
}

/** True when the request never got a response — the case an offline retry should cover. */
export function isNetworkError(error: unknown): boolean {
  return axios.isAxiosError(error) && !error.response
}

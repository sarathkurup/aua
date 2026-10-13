import { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { authApi } from '../api/endpoints'
import { SESSION_EXPIRED, getToken, setToken } from '../api/client'
import type { OwnedTeam, UpdateProfileRequest, User } from '../types'

interface AuthContextValue {
  user: User | null
  isAuthenticated: boolean
  isAdmin: boolean
  isOwner: boolean
  /** True while the stored token is being validated on first load. */
  initialising: boolean
  login: (username: string, password: string) => Promise<User>
  logout: () => void
  /** Re-reads the profile so team purses follow a completed sale. */
  refreshUser: () => Promise<void>
  /** Saves the caller's own profile and keeps the header in step. */
  updateProfile: (body: UpdateProfileRequest) => Promise<User>
  /** The signed-in owner's team in a given tournament, if they have one. */
  teamIn: (tournamentId: number) => OwnedTeam | null
}

const AuthContext = createContext<AuthContextValue | null>(null)

/**
 * Signing in is optional here: spectators use the app without an account, so a missing
 * token is a normal state rather than an error.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [initialising, setInitialising] = useState(true)

  // A token in storage is only a claim; ask the API whether it is still good.
  useEffect(() => {
    let cancelled = false

    async function restore() {
      if (!getToken()) {
        setInitialising(false)
        return
      }

      try {
        const me = await authApi.me()
        if (!cancelled) setUser(me)
      } catch {
        setToken(null)
        if (!cancelled) setUser(null)
      } finally {
        if (!cancelled) setInitialising(false)
      }
    }

    void restore()
    return () => {
      cancelled = true
    }
  }, [])

  // The axios interceptor fires this when any call comes back 401.
  useEffect(() => {
    const onExpired = () => setUser(null)
    window.addEventListener(SESSION_EXPIRED, onExpired)
    return () => window.removeEventListener(SESSION_EXPIRED, onExpired)
  }, [])

  const login = useCallback(async (username: string, password: string) => {
    const result = await authApi.login(username, password)
    setToken(result.token)
    setUser(result.user)
    return result.user
  }, [])

  const logout = useCallback(() => {
    setToken(null)
    setUser(null)
  }, [])

  const updateProfile = useCallback(async (body: UpdateProfileRequest) => {
    const updated = await authApi.updateProfile(body)
    setUser(updated)
    return updated
  }, [])

  const refreshUser = useCallback(async () => {
    if (!getToken()) return
    try {
      setUser(await authApi.me())
    } catch {
      /* a failed refresh is not worth interrupting the auction for */
    }
  }, [])

  const teamIn = useCallback(
    (tournamentId: number) => user?.teams.find((team) => team.tournamentId === tournamentId) ?? null,
    [user],
  )

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: user !== null,
      isAdmin: user?.role === 'Admin',
      isOwner: user?.role === 'TeamOwner',
      initialising,
      login,
      logout,
      refreshUser,
      updateProfile,
      teamIn,
    }),
    [user, initialising, login, logout, refreshUser, updateProfile, teamIn],
  )

  return <AuthContext value={value}>{children}</AuthContext>
}

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth must be used inside <AuthProvider>')
  return context
}

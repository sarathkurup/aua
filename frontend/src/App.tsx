import type { ReactNode } from 'react'
import { Navigate, Route, Routes } from 'react-router'
import { AuthProvider, useAuth } from './auth/AuthContext'
import { ToastProvider } from './components/Toasts'
import { AdminPage } from './pages/AdminPage'
import { LobbyPage } from './pages/LobbyPage'
import { LoginPage } from './pages/LoginPage'
import { RegisterPage } from './pages/RegisterPage'
import { SettingsPage } from './pages/SettingsPage'
import { TeamBiddingPage, TeamHomePage } from './pages/TeamPage'
import { ViewerPage } from './pages/ViewerPage'
import { ThemeProvider } from './theme/ThemeContext'
import { PreferencesProvider } from './settings/PreferencesContext'
import { ResultsPage } from './pages/ResultsPage'

function Splash() {
  return (
    <div className="grid min-h-dvh place-items-center px-4">
      <p className="glass px-6 py-4 text-sm text-slate-300">Restoring your session…</p>
    </div>
  )
}

/** Where a signed-in user belongs when they land with no route in mind. */
function homeFor(isAdmin: boolean): string {
  return isAdmin ? '/admin' : '/team'
}

function RequireAuth({ children }: { children: ReactNode }) {
  const { isAuthenticated, initialising } = useAuth()

  // Redirecting before the stored token is validated would sign everyone out on refresh.
  if (initialising) return <Splash />
  if (!isAuthenticated) return <Navigate to="/login" replace />

  return children
}

function LoginRoute() {
  const { isAuthenticated, isAdmin, initialising } = useAuth()

  if (initialising) return <Splash />
  if (isAuthenticated) return <Navigate to={homeFor(isAdmin)} replace />

  return <LoginPage />
}

function RegisterRoute() {
  const { isAuthenticated, isAdmin, initialising } = useAuth()

  if (initialising) return <Splash />
  if (isAuthenticated) return <Navigate to={homeFor(isAdmin)} replace />

  return <RegisterPage />
}

export default function App() {
  return (
    <ThemeProvider>
      <PreferencesProvider>
        <AuthProvider>
          <ToastProvider>
          <Routes>
            {/* Public: no account, no redirect. Settings included — the theme is a
                device preference, not something worth making people sign in for. */}
            <Route path="/" element={<LobbyPage />} />
            {/* The share link. Works for public and private rooms alike — holding it is
                the permission, which is why nothing here checks for an account. */}
            <Route path="/watch/:publicId" element={<ViewerPage />} />
            {/* Same share link as the room: whoever may watch may read the results. */}
            <Route path="/results/:publicId" element={<ResultsPage />} />
            <Route path="/settings" element={<SettingsPage />} />
            <Route path="/login" element={<LoginRoute />} />
            <Route path="/register" element={<RegisterRoute />} />

            <Route
              path="/team"
              element={
                <RequireAuth>
                  <TeamHomePage />
                </RequireAuth>
              }
            />
            <Route
              path="/team/:auctionId"
              element={
                <RequireAuth>
                  <TeamBiddingPage />
                </RequireAuth>
              }
            />
            {/* Any signed-in account may run auctions. What they can actually change is
                decided per tournament by ownership, not by the route. */}
            <Route
              path="/admin"
              element={
                <RequireAuth>
                  <AdminPage />
                </RequireAuth>
              }
            />

            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
          </ToastProvider>
        </AuthProvider>
      </PreferencesProvider>
    </ThemeProvider>
  )
}

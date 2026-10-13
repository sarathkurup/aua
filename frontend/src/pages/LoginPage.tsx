import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useNavigate } from 'react-router'
import { useAuth } from '../auth/AuthContext'
import { errorMessage } from '../api/client'

const DEMO_ACCOUNTS = [
  { label: 'Auction control', username: 'admin', password: 'Admin@123', note: 'admin' },
  { label: 'Nadia Alvarez', username: 'coastal', password: 'Team@123', note: 'owner' },
  { label: 'Ibrahim Osei', username: 'metro', password: 'Team@123', note: 'owner' },
]

export function LoginPage() {
  const { login } = useAuth()
  const navigate = useNavigate()

  const [username, setUsername] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)

    try {
      const user = await login(username.trim(), password)
      void navigate(user.role === 'Admin' ? '/admin' : '/team', { replace: true })
    } catch (err) {
      setError(errorMessage(err, 'Could not sign in.'))
    } finally {
      setBusy(false)
    }
  }

  return (
    <main className="grid min-h-dvh place-items-center px-4 py-10">
      <div className="w-full max-w-sm">
        <div className="mb-6 text-center">
          <span className="pill pill-brand mb-3">Live auction</span>
          <h1 className="text-3xl font-black tracking-tight text-white">Auction Room</h1>
          <p className="mt-1 text-sm text-slate-400">
            Sign in to bid for your team or to run the sale.
          </p>
          <Link to="/" className="mt-2 inline-block text-sm text-brand-300 hover:text-brand-200">
            Just watching? Live auctions are open to everyone →
          </Link>
        </div>

        <form onSubmit={handleSubmit} className="glass-strong space-y-4 p-6">
          <div>
            <label className="field-label" htmlFor="username">
              Username
            </label>
            <input
              id="username"
              className="field"
              value={username}
              onChange={(event) => setUsername(event.target.value)}
              autoComplete="username"
              autoFocus
              required
            />
          </div>

          <div>
            <label className="field-label" htmlFor="password">
              Password
            </label>
            <input
              id="password"
              className="field"
              type="password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
              autoComplete="current-password"
              required
            />
          </div>

          {error && (
            <p
              className="rounded-xl border border-danger-500/40 bg-danger-500/12 px-3 py-2 text-sm text-danger-400"
              role="alert"
            >
              {error}
            </p>
          )}

          <button type="submit" className="btn btn-primary w-full" disabled={busy}>
            {busy ? 'Signing in…' : 'Sign in'}
          </button>

          <p className="text-center text-sm text-slate-400">
            Want to run your own auction?{' '}
            <Link to="/register" className="font-semibold text-brand-300 hover:text-brand-200">
              Create an account
            </Link>
          </p>
        </form>

        <div className="glass mt-4 p-4">
          <p className="section-title mb-2">Seeded demo accounts</p>
          <ul className="space-y-1.5">
            {DEMO_ACCOUNTS.map((account) => (
              <li key={account.username}>
                <button
                  type="button"
                  className="glass-inset flex w-full items-center gap-2 px-3 py-2 text-left text-sm transition hover:bg-white/8"
                  onClick={() => {
                    setUsername(account.username)
                    setPassword(account.password)
                  }}
                >
                  <span className="flex-1 truncate text-slate-100">{account.label}</span>
                  <span className="nums text-xs text-slate-400">{account.username}</span>
                  <span className={`pill ${account.note === 'admin' ? 'pill-brand' : 'pill-muted'}`}>
                    {account.note}
                  </span>
                </button>
              </li>
            ))}
          </ul>
          <p className="mt-2 text-xs text-slate-500">
            Tap to fill the form. Change these passwords before any real use.
          </p>
        </div>
      </div>
    </main>
  )
}

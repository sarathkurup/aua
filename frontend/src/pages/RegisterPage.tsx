import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router'
import { errorMessage } from '../api/client'
import { accountRequestsApi } from '../api/endpoints'

/**
 * Asking for an auction of your own.
 *
 * There is no sign-up here and no email in the loop: this is an office tool, so the request
 * goes to the auction admin, who approves it and hands over the password in person or over
 * Teams. The username is chosen here so the requester already knows half of their own
 * credentials by the time the reply arrives.
 */
export function RegisterPage() {
  const [username, setUsername] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [auctionName, setAuctionName] = useState('')
  const [contact, setContact] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [sent, setSent] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)

    try {
      await accountRequestsApi.submit({
        username: username.trim(),
        displayName: displayName.trim(),
        auctionName: auctionName.trim() || null,
        contact: contact.trim() || null,
      })
      setSent(true)
    } catch (err) {
      setError(errorMessage(err, 'Could not send that request.'))
    } finally {
      setBusy(false)
    }
  }

  if (sent) {
    return (
      <main className="grid min-h-dvh place-items-center px-4 py-10">
        <div className="glass-strong w-full max-w-md space-y-3 p-6 text-center">
          <span className="pill pill-live">Request sent</span>
          <h1 className="text-2xl font-black tracking-tight text-slate-100">
            The auction admin has it
          </h1>
          <p className="text-sm text-slate-400">
            When it is approved you will be sent a password to go with the username{' '}
            <span className="nums font-semibold text-slate-200">{username.trim().toLowerCase()}</span>.
            Nothing is emailed — it comes to you directly.
          </p>
          <div className="flex justify-center gap-2 pt-2">
            <Link to="/" className="btn btn-sm">
              Watch the live auctions
            </Link>
            <Link to="/login" className="btn btn-sm btn-primary">
              Sign in
            </Link>
          </div>
        </div>
      </main>
    )
  }

  return (
    <main className="grid min-h-dvh place-items-center px-4 py-10">
      <div className="w-full max-w-md">
        <div className="mb-6 text-center">
          <span className="pill pill-brand mb-3">Request an auction</span>
          <h1 className="text-3xl font-black tracking-tight text-slate-100">Run your own auction</h1>
          <p className="mt-1 text-sm text-slate-400">
            Tell the auction admin who you are. They approve the request and send you a password.
          </p>
        </div>

        <form onSubmit={handleSubmit} className="glass-strong space-y-4 p-6">
          <div>
            <label className="field-label" htmlFor="req-username">
              Username you want
            </label>
            <input
              id="req-username"
              className="field"
              value={username}
              onChange={(event) => setUsername(event.target.value)}
              pattern="[a-zA-Z0-9._\-]+"
              minLength={3}
              maxLength={64}
              autoComplete="username"
              autoFocus
              required
            />
            <p className="mt-1 text-xs text-slate-500">
              Letters, numbers, dots, dashes and underscores. This is what you will sign in with.
            </p>
          </div>

          <div>
            <label className="field-label" htmlFor="req-name">
              Your name
            </label>
            <input
              id="req-name"
              className="field"
              value={displayName}
              onChange={(event) => setDisplayName(event.target.value)}
              maxLength={128}
              required
            />
          </div>

          <div>
            <label className="field-label" htmlFor="req-auction">
              What you want to run
            </label>
            <input
              id="req-auction"
              className="field"
              value={auctionName}
              onChange={(event) => setAuctionName(event.target.value)}
              placeholder="Floor 3 badminton league"
              maxLength={128}
            />
          </div>

          <div>
            <label className="field-label" htmlFor="req-contact">
              How to reach you
            </label>
            <input
              id="req-contact"
              className="field"
              value={contact}
              onChange={(event) => setContact(event.target.value)}
              placeholder="Teams handle, desk, or extension"
              maxLength={256}
            />
            <p className="mt-1 text-xs text-slate-500">
              This is how your password will reach you, so make it something they can use.
            </p>
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
            {busy ? 'Sending…' : 'Send request'}
          </button>

          <p className="text-center text-sm text-slate-400">
            Already have credentials?{' '}
            <Link to="/login" className="text-brand-300 hover:text-brand-200">
              Sign in
            </Link>
          </p>
        </form>
      </div>
    </main>
  )
}

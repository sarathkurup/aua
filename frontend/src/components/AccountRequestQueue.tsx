import { useCallback, useEffect, useState } from 'react'
import { errorMessage } from '../api/client'
import { accountRequestsApi } from '../api/endpoints'
import { useToast } from './Toasts'
import { EmptyState, Panel } from './primitives'
import type { AccountRequest, ApprovalResult } from '../types'

/**
 * The auction admin's queue of people asking to run an auction.
 *
 * Approving mints a password and shows it once. It is never stored in readable form, so the
 * card stays on screen until it is dismissed — closing it is the point of no return, and the
 * copy button is right there so nobody has to retype it into Teams.
 */
export function AccountRequestQueue() {
  const { push } = useToast()

  const [requests, setRequests] = useState<AccountRequest[]>([])
  const [issued, setIssued] = useState<ApprovalResult | null>(null)
  const [busyId, setBusyId] = useState<number | null>(null)
  const [showDecided, setShowDecided] = useState(false)
  const [copied, setCopied] = useState(false)

  const load = useCallback(async () => {
    try {
      setRequests(await accountRequestsApi.list())
    } catch {
      /* the console is still usable without the queue */
    }
  }, [])

  useEffect(() => {
    void load()
  }, [load])

  async function approve(request: AccountRequest) {
    setBusyId(request.id)
    try {
      const result = await accountRequestsApi.approve(request.id)
      setIssued(result)
      setCopied(false)
      await load()
    } catch (err) {
      push(errorMessage(err, 'Could not approve that request.'), 'error')
    } finally {
      setBusyId(null)
    }
  }

  async function decline(request: AccountRequest) {
    const reason = window.prompt(`Decline ${request.username}? Add a reason if you like:`, '')
    if (reason === null) return

    setBusyId(request.id)
    try {
      await accountRequestsApi.decline(request.id, reason.trim() || null)
      push(`${request.username} declined.`, 'success')
      await load()
    } catch (err) {
      push(errorMessage(err, 'Could not decline that request.'), 'error')
    } finally {
      setBusyId(null)
    }
  }

  const pending = requests.filter((r) => r.status === 'Pending')
  const decided = requests.filter((r) => r.status !== 'Pending')
  const visible = showDecided ? requests : pending

  return (
    <Panel
      title={`Auction requests${pending.length > 0 ? ` (${pending.length} waiting)` : ''}`}
      action={
        decided.length > 0 && (
          <button type="button" className="btn btn-sm" onClick={() => setShowDecided((v) => !v)}>
            {showDecided ? 'Waiting only' : `Show decided (${decided.length})`}
          </button>
        )
      }
    >
      {issued && (
        <div
          className="glass-inset animate-rise-in mb-3 p-4"
          style={{ boxShadow: 'inset 0 0 0 1px rgb(var(--accent) / 0.55)' }}
        >
          <p className="section-title">Send these credentials by hand</p>
          <p className="mt-1 text-xs text-slate-400">
            This password is not stored anywhere readable. Once you close this card it cannot be
            shown again — only reset.
          </p>

          <dl className="mt-3 grid gap-2 sm:grid-cols-2">
            <div>
              <dt className="field-label">Username</dt>
              <dd className="nums text-lg font-bold text-slate-100">{issued.username}</dd>
            </div>
            <div>
              <dt className="field-label">Password</dt>
              <dd className="nums text-lg font-black tracking-wider text-slate-100">
                {issued.generatedPassword}
              </dd>
            </div>
          </dl>

          <div className="mt-3 flex flex-wrap gap-2">
            <button
              type="button"
              className="btn btn-sm btn-primary"
              onClick={async () => {
                try {
                  await navigator.clipboard.writeText(
                    `Auction Room\nUsername: ${issued.username}\nPassword: ${issued.generatedPassword}`,
                  )
                  setCopied(true)
                } catch {
                  push('Could not reach the clipboard — copy it by hand.', 'warn')
                }
              }}
            >
              {copied ? 'Copied' : 'Copy for Teams'}
            </button>
            <button type="button" className="btn btn-sm" onClick={() => setIssued(null)}>
              I have sent it
            </button>
          </div>
        </div>
      )}

      {visible.length === 0 ? (
        <EmptyState>
          Nobody is waiting. Requests arrive when someone fills in the form at{' '}
          <span className="text-slate-300">/register</span>.
        </EmptyState>
      ) : (
        <ul className="space-y-2">
          {visible.map((request) => (
            <li key={request.id} className="glass-inset flex flex-wrap items-center gap-3 px-3 py-2.5">
              <div className="min-w-[10rem] flex-1">
                <p className="flex flex-wrap items-center gap-2 text-sm font-semibold text-slate-100">
                  {request.displayName}
                  <span className="nums text-xs font-normal text-slate-400">{request.username}</span>
                  {request.status === 'Approved' && <span className="pill pill-live">approved</span>}
                  {request.status === 'Declined' && <span className="pill pill-danger">declined</span>}
                </p>
                <p className="text-xs text-slate-400">
                  {request.auctionName ?? 'No auction named'}
                  {request.contact && <span className="text-slate-500"> · {request.contact}</span>}
                </p>
                {request.decisionNote && (
                  <p className="mt-0.5 text-xs text-slate-500">“{request.decisionNote}”</p>
                )}
              </div>

              {request.status === 'Pending' && (
                <div className="flex gap-2">
                  <button
                    type="button"
                    className="btn btn-sm btn-live"
                    disabled={busyId !== null}
                    onClick={() => void approve(request)}
                  >
                    {busyId === request.id ? 'Approving…' : 'Approve'}
                  </button>
                  <button
                    type="button"
                    className="btn btn-sm"
                    disabled={busyId !== null}
                    onClick={() => void decline(request)}
                  >
                    Decline
                  </button>
                </div>
              )}
            </li>
          ))}
        </ul>
      )}
    </Panel>
  )
}

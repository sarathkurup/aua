import { useCallback, useEffect, useState } from 'react'
import { errorMessage } from '../api/client'
import { auctionsApi } from '../api/endpoints'
import { AppShell } from '../components/AppShell'
import { AuctionCard } from '../components/AuctionCard'
import { EmptyState, Panel } from '../components/primitives'
import { useLobbyHub } from '../realtime/useAuctionHub'
import type { LiveAuctionCard, RetentionPolicy } from '../types'

/**
 * The public dashboard: every room running right now, and every one scheduled to open.
 *
 * No account is needed to reach it. Live cards are pushed over the hub, so an auction
 * opening or a bid landing in any room updates them without a refresh; the scheduled list
 * changes rarely and is re-read whenever the live feed moves.
 */
export function LobbyPage() {
  const [live, setLive] = useState<LiveAuctionCard[]>([])
  const [upcoming, setUpcoming] = useState<LiveAuctionCard[]>([])
  const [retention, setRetention] = useState<RetentionPolicy | null>(null)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  const loadUpcoming = useCallback(async () => {
    try {
      setUpcoming(await auctionsApi.upcoming())
    } catch {
      /* the schedule is secondary; leave the last known list in place */
    }
  }, [])

  // A room opening or closing moves it between the two lists, so refresh the schedule
  // whenever the live feed changes rather than polling for it.
  const onLobby = useCallback(
    (running: LiveAuctionCard[]) => {
      setLive(running)
      setLoading(false)
      void loadUpcoming()
    },
    [loadUpcoming],
  )

  const hubStatus = useLobbyHub(onLobby)

  // First paint comes from REST so the page has content even if the socket is slow.
  useEffect(() => {
    let cancelled = false

    auctionsApi
      .live()
      .then((running) => {
        if (cancelled) return
        setLive(running)
        setError(null)
      })
      .catch((err) => {
        if (!cancelled) setError(errorMessage(err, 'Could not reach the auction server.'))
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    void loadUpcoming()
    auctionsApi
      .retention()
      .then((policy) => !cancelled && setRetention(policy))
      .catch(() => undefined)

    return () => {
      cancelled = true
    }
  }, [loadUpcoming])

  const nothingAtAll = !loading && !error && live.length === 0 && upcoming.length === 0

  return (
    <AppShell hubStatus={hubStatus}>
      <div className="space-y-6">
        <div className="text-center sm:text-left">
          <h1 className="text-2xl font-black tracking-tight text-white sm:text-3xl 2xl:text-4xl">
            Live auctions
          </h1>
          <p className="mt-1 text-sm text-slate-400">
            Watch any room as it happens — no account needed. Team owners sign in to bid.
          </p>
        </div>

        {loading && <Panel>Looking for auctions…</Panel>}

        {!loading && error && (
          <Panel>
            <p className="text-sm text-danger-400">{error}</p>
          </Panel>
        )}

        {nothingAtAll && (
          <Panel strong>
            <EmptyState>
              Nothing is running or scheduled. This page updates on its own the instant an
              auctioneer opens a room.
            </EmptyState>
          </Panel>
        )}

        {live.length > 0 && (
          <section className="space-y-3">
            <h2 className="section-title flex items-center gap-2">
              <span aria-hidden className="inline-block size-1.5 rounded-full bg-live-400 animate-pulse-ring" />
              Happening now ({live.length})
            </h2>
            {/* Widens on a TV rather than stretching three cards across two metres. */}
            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4">
              {live.map((auction) => (
                <AuctionCard key={auction.publicId} auction={auction} />
              ))}
            </div>
          </section>
        )}

        {upcoming.length > 0 && (
          <section className="space-y-3">
            <h2 className="section-title">Scheduled ({upcoming.length})</h2>
            <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4">
              {upcoming.map((auction) => (
                <AuctionCard key={auction.publicId} auction={auction} />
              ))}
            </div>
          </section>
        )}

        {retention?.enabled && (
          <p className="text-xs text-slate-500">
            Auction data is kept for {retention.auctionDays} days. Once an auction has been
            finished for longer than that, it and its bid history are permanently deleted.
          </p>
        )}
      </div>
    </AppShell>
  )
}

import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useParams } from 'react-router'
import { errorMessage, isNetworkError } from '../api/client'
import { auctionsApi, bidsApi } from '../api/endpoints'
import { useAuth } from '../auth/AuthContext'
import { AppShell } from '../components/AppShell'
import { AuctionRoom } from '../components/AuctionRoom'
import type { SoldEvent } from '../components/AuctionRoom'
import { AuctionStatusPill, EmptyState, Panel, Stat } from '../components/primitives'
import { useToast } from '../components/Toasts'
import { money, moneyFull } from '../lib/format'
import { useAuctionRoom } from '../realtime/useAuctionRoom'
import type { Auction, AuctionState, TeamSummary } from '../types'

/** Ladder offered above the minimum, in multiples of the auction's bid increment. */
const QUICK_STEPS = [0, 1, 2, 5]

/** The owner's own teams, and which of their auctions are open right now. */
export function TeamHomePage() {
  const { user } = useAuth()
  const [auctions, setAuctions] = useState<Auction[]>([])

  useEffect(() => {
    let cancelled = false
    auctionsApi
      .list()
      .then((list) => !cancelled && setAuctions(list))
      .catch(() => {
        /* the cards still render without live status */
      })
    return () => {
      cancelled = true
    }
  }, [])

  const teams = user?.teams ?? []

  return (
    <AppShell>
      <div className="space-y-5">
        <div>
          <h1 className="text-2xl font-black tracking-tight text-white">My teams</h1>
          <p className="mt-1 text-sm text-slate-400">
            Each team holds its own purse, so bidding in two tournaments at once never crosses budgets.
          </p>
        </div>

        {teams.length === 0 ? (
          <Panel strong>
            <EmptyState>
              No teams are assigned to this account yet. The auctioneer assigns teams from the admin
              console.
            </EmptyState>
          </Panel>
        ) : (
          <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
            {teams.map((team) => {
              const auction = auctions.find(
                (a) =>
                  a.tournamentId === team.tournamentId &&
                  (a.status === 'Live' || a.status === 'Paused'),
              )

              return (
                <div key={team.teamId} className="glass p-4">
                  <div className="flex items-start gap-3">
                    <div className="min-w-0 flex-1">
                      <p className="truncate font-bold text-white">{team.teamName}</p>
                      <p className="truncate text-xs text-slate-400">
                        {team.tournamentName} · {team.sportName}
                      </p>
                    </div>
                    {auction && <AuctionStatusPill status={auction.status} />}
                  </div>

                  <div className="mt-3">
                    <Stat label="Purse" value={money(team.purse)} hint={moneyFull(team.purse)} />
                  </div>

                  {auction ? (
                    <Link to={`/team/${auction.id}`} className="btn btn-primary mt-3 w-full">
                      Bid in {auction.name}
                    </Link>
                  ) : (
                    <p className="mt-3 text-center text-xs text-slate-500">
                      No auction running for this tournament.
                    </p>
                  )}
                </div>
              )
            })}
          </div>
        )}
      </div>
    </AppShell>
  )
}

/** The room, with a bid panel wired to whichever team this owner holds in it. */
export function TeamBiddingPage() {
  const { auctionId } = useParams()
  const id = Number(auctionId)
  const { push } = useToast()
  const { teamIn, refreshUser } = useAuth()

  // Stamped with the arrival time so two sales in a row still each trigger the animation.
  const [soldEvent, setSoldEvent] = useState<SoldEvent | null>(null)
  const [lastReaction, setLastReaction] = useState<{ emoji: string; at: number } | null>(null)

  const room = useAuctionRoom({ auctionId: Number.isFinite(id) ? id : null }, {
    onReaction: (reaction) => setLastReaction({ emoji: reaction.emoji, at: Date.now() }),
    onSold: (player) => {
      setSoldEvent({ player, at: Date.now() })
      push(
        `${player.name} sold to ${player.soldToTeamName ?? 'a team'} for ${moneyFull(player.soldPrice)}.`,
        'success',
      )
      void refreshUser()
    },
    onUnsold: (player) => push(`${player.name} went unsold.`, 'warn'),
  })

  const { state, players, loading, error, hubStatus } = room
  const ownedTeam = state ? teamIn(state.auction.tournamentId) : null
  const standing = state?.teams.find((team) => team.teamId === ownedTeam?.teamId) ?? null

  return (
    <AppShell hubStatus={hubStatus}>
      {loading && <Panel>Loading the auction room…</Panel>}

      {!loading && error && (
        <Panel>
          <p className="text-sm text-danger-400">{error}</p>
          <Link to="/team" className="btn btn-sm mt-3">
            Back to my teams
          </Link>
        </Panel>
      )}

      {state && !ownedTeam && (
        <Panel className="mb-4">
          <p className="text-sm text-warn-400">
            You do not own a team in this tournament, so you can only watch this room.
          </p>
        </Panel>
      )}

      {state && (
        <AuctionRoom
          state={state}
          players={players}
          highlightTeamId={ownedTeam?.teamId}
          soldEvent={soldEvent}
          viewerCount={room.viewerCount}
          onReact={room.sendReaction}
          lastReaction={lastReaction}
          controls={
            ownedTeam && standing ? (
              <BidPanel state={state} team={standing} onPlaced={(message) => push(message, 'success')} />
            ) : undefined
          }
        />
      )}
    </AppShell>
  )
}

function BidPanel({
  state,
  team,
  onPlaced,
}: {
  state: AuctionState
  team: TeamSummary
  onPlaced: (message: string) => void
}) {
  const { auction, currentPlayer, nextMinimumBid: minimum } = state
  const iLead = state.topBid?.teamId === team.teamId

  const [amount, setAmount] = useState('')
  const [placing, setPlacing] = useState(false)
  const [bidError, setBidError] = useState<string | null>(null)

  // A fresh lot, or a bid that moved the floor, resets the box to the new minimum.
  useEffect(() => {
    setBidError(null)
    setAmount(minimum > 0 ? String(minimum) : '')
  }, [minimum, currentPlayer?.id])

  /**
   * Kept across a retry so the server can recognise the second attempt as the same bid.
   * Cleared once a bid is accepted or abandoned.
   */
  const requestIdRef = useRef<string | null>(null)

  const submitBid = useCallback(
    async (value: number) => {
      if (!currentPlayer) return

      setPlacing(true)
      setBidError(null)
      const requestId = (requestIdRef.current ??= crypto.randomUUID())

      try {
        await bidsApi.place(auction.id, currentPlayer.id, team.teamId, value, requestId)
        requestIdRef.current = null
        onPlaced(`Bid of ${moneyFull(value)} placed.`)
      } catch (err) {
        if (isNetworkError(err)) {
          // The bid may well have landed — the same request id makes one retry safe.
          try {
            await bidsApi.place(auction.id, currentPlayer.id, team.teamId, value, requestId)
            requestIdRef.current = null
            onPlaced(`Bid of ${moneyFull(value)} placed.`)
            return
          } catch (retryError) {
            setBidError(errorMessage(retryError, 'Could not reach the auction server.'))
            return
          }
        }

        requestIdRef.current = null
        setBidError(errorMessage(err, 'Bid rejected.'))
      } finally {
        setPlacing(false)
      }
    },
    [auction.id, currentPlayer, team.teamId, onPlaced],
  )

  const quickBids = useMemo(() => {
    if (minimum <= 0) return []
    const seen = new Set<number>()
    return QUICK_STEPS.map((step) => minimum + step * auction.minBidIncrement).filter((value) => {
      if (seen.has(value)) return false
      seen.add(value)
      return true
    })
  }, [auction.minBidIncrement, minimum])

  const canBid =
    auction.status === 'Live' && currentPlayer !== null && !iLead && !placing && team.canBid

  function handleSubmit(event: FormEvent) {
    event.preventDefault()
    const value = Number(amount)
    if (!Number.isFinite(value) || value <= 0) {
      setBidError('Enter a bid amount.')
      return
    }
    void submitBid(value)
  }

  return (
    <Panel title={`Bidding as ${team.name}`} strong>
      {auction.status !== 'Live' ? (
        <EmptyState>
          {auction.status === 'Paused' ? 'The auctioneer has paused bidding.' : 'Bidding is closed.'}
        </EmptyState>
      ) : !currentPlayer ? (
        <EmptyState>Wait for the next lot to be called.</EmptyState>
      ) : (
        <div className="space-y-3">
          {iLead && (
            <p className="rounded-xl border border-live-500/40 bg-live-500/12 px-3 py-2 text-sm text-live-400">
              You hold the top bid at {moneyFull(state.topBid?.amount)}. Wait for another team to raise.
            </p>
          )}

          {team.blockedReason && (
            <p className="rounded-xl border border-danger-500/40 bg-danger-500/12 px-3 py-2 text-sm text-danger-400">
              {team.blockedReason} — you cannot bid on this lot.
            </p>
          )}

          {!team.blockedReason && team.womenRequired > 0 && (
            <p className="rounded-xl border border-brand-400/40 bg-brand-500/12 px-3 py-2 text-sm text-brand-300">
              This league needs {team.womenRequired} more{' '}
              {team.womenRequired === 1 ? 'woman' : 'women'} in your squad.{' '}
              {team.reservedPurse > 0 && `${moneyFull(team.reservedPurse)} of your purse is held back for them.`}
            </p>
          )}

          <div className="flex flex-wrap gap-2">
            {quickBids.map((value) => (
              <button
                key={value}
                type="button"
                className="btn btn-sm btn-live"
                disabled={!canBid || value > team.spendablePurse}
                onClick={() => void submitBid(value)}
                title={
                  value > team.spendablePurse
                    ? `Above your spendable purse of ${moneyFull(team.spendablePurse)}`
                    : moneyFull(value)
                }
              >
                {money(value)}
              </button>
            ))}
          </div>

          <form onSubmit={handleSubmit} className="flex flex-wrap items-end gap-2">
            <div className="min-w-[8rem] flex-1">
              <label className="field-label" htmlFor="bid-amount">
                Custom amount
              </label>
              <input
                id="bid-amount"
                className="field nums"
                type="number"
                inputMode="numeric"
                min={minimum}
                step={1}
                value={amount}
                onChange={(event) => setAmount(event.target.value)}
              />
            </div>
            <button type="submit" className="btn btn-primary" disabled={!canBid}>
              {placing ? 'Placing…' : 'Bid'}
            </button>
          </form>

          {bidError && (
            <p className="text-sm text-danger-400" role="alert">
              {bidError}
            </p>
          )}

          <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
            <Stat label="Minimum" value={money(minimum)} hint={moneyFull(minimum)} />
            <Stat
              label="You can bid"
              value={money(team.spendablePurse)}
              hint={moneyFull(team.spendablePurse)}
              tone={team.spendablePurse < minimum ? 'danger' : 'live'}
            />
            <Stat label="Squad" value={team.playersWon} />
            <Stat
              label="Women"
              value={team.womenWon}
              tone={team.womenRequired > 0 ? 'warn' : 'default'}
            />
          </div>
        </div>
      )}
    </Panel>
  )
}

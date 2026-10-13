import { useState } from 'react'
import { Link, useParams } from 'react-router'
import { AppShell } from '../components/AppShell'
import { AuctionRoom } from '../components/AuctionRoom'
import type { SoldEvent } from '../components/AuctionRoom'
import { EmptyState, Panel } from '../components/primitives'
import { useToast } from '../components/Toasts'
import { moneyFull } from '../lib/format'
import { useAuctionRoom } from '../realtime/useAuctionRoom'
import { useAuth } from '../auth/AuthContext'

/**
 * The spectator room. Anyone with the link can watch: the hub connection is anonymous and
 * the endpoint behind it is open. An owner who happens to be signed in gets a shortcut
 * through to the bidding screen for the same auction.
 *
 * Public and private rooms render identically here. Visibility only decides whether the
 * room is also advertised on the lobby.
 */
export function ViewerPage() {
  // The route carries the share id, never the sequential one — a private room is only
  // reachable by someone who was given its link.
  const { publicId } = useParams()
  const { push } = useToast()
  const { teamIn, isOwner } = useAuth()

  // Stamped with the arrival time so two sales in a row still each trigger the animation.
  const [soldEvent, setSoldEvent] = useState<SoldEvent | null>(null)
  const [lastReaction, setLastReaction] = useState<{ emoji: string; at: number } | null>(null)

  const room = useAuctionRoom({ publicId }, {
    onReaction: (reaction) => setLastReaction({ emoji: reaction.emoji, at: Date.now() }),
    onSold: (player) => {
      setSoldEvent({ player, at: Date.now() })
      push(
        `${player.name} sold to ${player.soldToTeamName ?? 'a team'} for ${moneyFull(player.soldPrice)}.`,
        'success',
      )
    },
    onUnsold: (player) => push(`${player.name} went unsold.`, 'warn'),
  })

  const { state, players, loading, error, hubStatus } = room
  const myTeam = state ? teamIn(state.auction.tournamentId) : null

  return (
    <AppShell hubStatus={hubStatus}>
      {loading && <Panel>Loading the auction room…</Panel>}

      {!loading && error && (
        <Panel>
          <p className="text-sm text-danger-400">{error}</p>
          <div className="mt-3 flex gap-2">
            <button type="button" className="btn btn-sm" onClick={() => void room.refresh()}>
              Try again
            </button>
            <Link to="/" className="btn btn-sm">
              Back to live auctions
            </Link>
          </div>
        </Panel>
      )}

      {!loading && !error && !state && (
        <Panel strong>
          <EmptyState>
            That link does not match an auction. It may have been mistyped, or the room may
            have been deleted.
          </EmptyState>
        </Panel>
      )}

      {state && (
        <>
          {isOwner && myTeam && (
            <Panel className="mb-4 !p-3">
              <div className="flex flex-wrap items-center gap-3">
                <p className="min-w-0 flex-1 text-sm text-slate-300">
                  You own <span className="font-semibold text-white">{myTeam.teamName}</span> in this
                  tournament.
                </p>
                <Link to={`/team/${state.auction.id}`} className="btn btn-sm btn-primary">
                  Open bidding
                </Link>
              </div>
            </Panel>
          )}

          <AuctionRoom
            state={state}
            players={players}
            highlightTeamId={myTeam?.teamId}
            soldEvent={soldEvent}
            viewerCount={room.viewerCount}
            onReact={room.sendReaction}
            lastReaction={lastReaction}
          />
        </>
      )}
    </AppShell>
  )
}

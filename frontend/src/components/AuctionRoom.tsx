import { useCallback, useEffect, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import type { AuctionState, Player } from '../types'
import { money, moneyFull } from '../lib/format'
import { usePreferences } from '../settings/PreferencesContext'
import { AuctionStatusPill, Panel, Stat, TournamentTypePill } from './primitives'
import { AuctionProgress } from './AuctionProgress'
import { IneligibleTeamsBanner } from './IneligibleTeamsBanner'
import { LiveBidFeed } from './LiveBidFeed'
import { LotCountdown } from './LotCountdown'
import { OnBlockCard } from './OnBlockCard'
import { PlayerPoolTable } from './PlayerPoolTable'
import { SoldStrip } from './SoldStrip'
import { TeamColumns } from './TeamColumns'
import { SoldFlight } from './SoldFlight'
import type { FlightRects } from './SoldFlight'
import { SoldOverlay } from './SoldOverlay'
import { RoomToolbar } from './RoomToolbar'
import { ReactionBar } from './ReactionBar'
import { usePhotoPreload } from './PlayerPhoto'

/** A completed sale to animate. `at` changes on every sale so repeats still fire. */
export interface SoldEvent {
  player: Player
  at: number
}

/**
 * The room, shared by every screen that watches one auction: the public viewer, the team
 * owner's bidding page and the auctioneer's console. Only the panel slipped in under the
 * lot card differs between them.
 *
 * Reading order follows the room itself — how far through the night we are, who is still in
 * for this lot, the lot under the hammer, what has just gone, and the squads it is going
 * into. The full pool comes last, because it is reference rather than theatre.
 */
export function AuctionRoom({
  state,
  players,
  highlightTeamId,
  controls,
  poolActions,
  soldEvent,
  viewerCount,
  onReact,
  lastReaction,
}: {
  state: AuctionState
  players: Player[]
  /** Ring the signed-in owner's team. */
  highlightTeamId?: number
  /** Bid form or auctioneer controls, shown directly below the lot. */
  controls?: ReactNode
  /** Admin pool actions. Omitted entirely on the public screens. */
  poolActions?: Parameters<typeof PlayerPoolTable>[0]
  /** Set by the parent when a sale lands, to celebrate it. */
  soldEvent?: SoldEvent | null
  /** People watching this room right now, from the hub. */
  viewerCount?: number
  /** Relays a cheer. Omit to hide the reaction bar entirely. */
  onReact?: (emoji: string) => void
  /** The last reaction to arrive, so it can float. */
  lastReaction?: { emoji: string; at: number } | null
}) {
  const { auction, rules } = state
  const { saleAnimation } = usePreferences()

  /** Collapses everything but the lot — for screen-sharing or a projector. */
  const [focusMode, setFocusMode] = useState(false)

  // Warm the next few lots so a called player's face is already in cache.
  usePhotoPreload(players.filter((player) => player.status === 'Available').slice(0, 6))

  const sold = players.filter((player) => player.status === 'Sold')
  const totalSpent = sold.reduce((sum, player) => sum + (player.soldPrice ?? 0), 0)

  const { lotRef, registerColumn, flight, flashTeamId, endFlight } = useSoldAnimation(
    soldEvent,
    saleAnimation,
  )

  const [overlayFor, setOverlayFor] = useState<Player | null>(null)
  const overlayHandled = useRef<number | null>(null)

  useEffect(() => {
    if (saleAnimation !== 'overlay' || !soldEvent) return
    if (overlayHandled.current === soldEvent.at) return

    overlayHandled.current = soldEvent.at
    setOverlayFor(soldEvent.player)
  }, [soldEvent, saleAnimation])

  return (
    <div className="space-y-4">
      <Panel className="!p-3">
        <div className="flex flex-wrap items-center gap-3">
          <span aria-hidden className="text-2xl">
            {auction.sportIcon ?? '🏆'}
          </span>
          {/* Full basis forces the name onto its own row on a phone: alongside the pills
              it was squeezed to "TechCor…", which tells a spectator nothing about which
              room they have opened. */}
          <div className="min-w-0 flex-1 basis-full sm:basis-auto">
            <p className="text-sm font-bold text-white">{auction.name}</p>
            <p className="truncate text-xs text-slate-400">
              {auction.tournamentName} · {auction.sportName}
            </p>
          </div>
          <TournamentTypePill type={auction.tournamentType} />
          <AuctionStatusPill status={auction.status} />
        </div>

        {(rules.minWomenPerSquad > 0 || rules.maxSquadSize > 0 || rules.maxSpendPerPlayer !== null) && (
          <p className="mt-2 flex flex-wrap gap-x-3 gap-y-1 text-xs text-slate-400">
            <span className="font-semibold uppercase tracking-[0.1em] text-slate-500">Squad rules</span>
            {rules.minSquadSize > 0 && <span>min {rules.minSquadSize} players</span>}
            {rules.maxSquadSize > 0 && <span>max {rules.maxSquadSize} players</span>}
            {rules.minWomenPerSquad > 0 && (
              <span className="text-brand-300">at least {rules.minWomenPerSquad} women</span>
            )}
            {rules.maxSpendPerPlayer !== null && (
              <span title={moneyFull(rules.maxSpendPerPlayer)}>
                cap {money(rules.maxSpendPerPlayer)} per player
              </span>
            )}
          </p>
        )}

        <RoomToolbar
          auction={auction}
          viewerCount={viewerCount}
          focusMode={focusMode}
          onToggleFocus={() => setFocusMode((on) => !on)}
        />
      </Panel>

      {!focusMode && <AuctionProgress players={players} />}

      <IneligibleTeamsBanner state={state} />

      {/* The lot, across the full width. On a wide screen the bid feed sits beside it so
          the two things that change every few seconds share one glance. */}
      <div className="grid gap-4 xl:grid-cols-[minmax(0,3fr)_minmax(0,1fr)]">
        <div className="space-y-4">
          <div ref={lotRef}>
            <OnBlockCard state={state} />
          </div>

          <LotCountdown state={state} />

          {controls}

          {onReact && state.currentPlayer && (
            <ReactionBar onSend={onReact} incoming={lastReaction ?? null} />
          )}

          {!focusMode && (
            <div className="grid grid-cols-2 gap-2 sm:grid-cols-4">
              <Stat label="Sold" value={`${sold.length}/${players.length}`} />
              <Stat label="Total spent" value={money(totalSpent)} hint={moneyFull(totalSpent)} />
              <Stat label="Teams" value={state.teams.length} />
              <Stat
                label="Increment"
                value={money(auction.minBidIncrement)}
                hint={moneyFull(auction.minBidIncrement)}
              />
            </div>
          )}
        </div>

        <LiveBidFeed bids={state.recentBids} />
      </div>

      {!focusMode && <SoldStrip players={players} />}

      <TeamColumns
        teams={state.teams}
        players={players}
        rules={rules}
        auction={auction}
        highlightTeamId={highlightTeamId}
        flashTeamId={flashTeamId}
        registerColumn={registerColumn}
      />

      {!focusMode &&
        (poolActions ? (
          <PlayerPoolTable {...poolActions} />
        ) : (
          <PlayerPoolTable players={players} title={`${auction.sportName} pool`} />
        ))}

      {flight && (
        <SoldFlight player={flight.player} from={flight.from} to={flight.to} onDone={endFlight} />
      )}

      {overlayFor && <SoldOverlay player={overlayFor} onDone={() => setOverlayFor(null)} />}
    </div>
  )
}

/**
 * Drives the sold-player flight: measures the lot card and the winning team's column, flies
 * a copy between them, and leaves the column lit for a moment afterwards.
 *
 * Measurement happens when the sale arrives rather than being tracked continuously, so
 * scrolling and resizing cost nothing. The flash is kept independent of the flight, so a
 * viewer whose column is off-screen — or who has asked for reduced motion, or picked the
 * full-screen overlay instead — still gets told where the player went.
 */
function useSoldAnimation(soldEvent: SoldEvent | null | undefined, mode: string) {
  const lotRef = useRef<HTMLDivElement>(null)
  const columns = useRef(new Map<number, HTMLElement>())

  const [flight, setFlight] = useState<(FlightRects & { player: Player }) | null>(null)
  const [flashTeamId, setFlashTeamId] = useState<number | null>(null)

  const registerColumn = useCallback((teamId: number, element: HTMLElement | null) => {
    if (element) columns.current.set(teamId, element)
    else columns.current.delete(teamId)
  }, [])

  const endFlight = useCallback(() => setFlight(null), [])

  useEffect(() => {
    if (!soldEvent) return

    const teamId = soldEvent.player.soldToTeamId
    setFlashTeamId(teamId ?? null)

    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches
    const source = lotRef.current
    const target = teamId === null || teamId === undefined ? null : columns.current.get(teamId)

    if (mode === 'flight' && !reduceMotion && source && target) {
      setFlight({
        player: soldEvent.player,
        from: source.getBoundingClientRect(),
        to: target.getBoundingClientRect(),
      })
    }

    // Backstop: the flight normally clears itself on transitionend, but a card that never
    // gets to animate (tab hidden mid-flight) would otherwise stay pinned to the page.
    const clearFlight = window.setTimeout(() => setFlight(null), 1400)
    const clearFlash = window.setTimeout(() => setFlashTeamId(null), 1800)

    return () => {
      window.clearTimeout(clearFlight)
      window.clearTimeout(clearFlash)
    }
  }, [soldEvent, mode])

  return { lotRef, registerColumn, flight, flashTeamId, endFlight }
}

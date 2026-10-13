import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { errorMessage } from '../api/client'
import { auctionsApi, playersApi } from '../api/endpoints'
import { playCue } from '../lib/sound'
import { useAuctionHub } from './useAuctionHub'
import type { HubStatus } from './useAuctionHub'
import type { AuctionState, Player } from '../types'

export interface AuctionRoomHandlers {
  onSold?: (player: Player) => void
  onUnsold?: (player: Player) => void
  onReaction?: (reaction: { emoji: string; from: string }) => void
}

/**
 * How the room was addressed.
 *
 * `publicId` is the spectator path — the share link, which needs no account and is the
 * only way into a private room. `auctionId` is the signed-in path used by the admin
 * console and the bidding screens, where a sequential id is already in hand.
 */
export interface AuctionRoomKey {
  auctionId?: number | null
  publicId?: string | null
}

export interface AuctionRoom {
  state: AuctionState | null
  /** Every player in the tournament's pool, for the pool table. */
  players: Player[]
  loading: boolean
  error: string | null
  hubStatus: HubStatus
  /** People watching this room right now. */
  viewerCount: number
  sendReaction: (emoji: string) => void
  /** Applies a state returned directly by a REST call, without waiting for the broadcast. */
  applyState: (state: AuctionState) => void
  refresh: () => Promise<void>
  reloadPlayers: () => Promise<void>
}

/**
 * One auction's live state plus its tournament's player pool.
 *
 * Rooms are independent: the hook is keyed on a single auction, and the server locks per
 * auction, so two rooms open in two tabs never interfere.
 */
export function useAuctionRoom(
  key: AuctionRoomKey,
  handlers: AuctionRoomHandlers = {},
): AuctionRoom {
  // Pulled out to primitives so a fresh key object each render does not re-run effects.
  const auctionId = key.auctionId ?? null
  const publicId = key.publicId ?? null

  const [state, setState] = useState<AuctionState | null>(null)
  const [players, setPlayers] = useState<Player[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [viewerCount, setViewerCount] = useState(0)

  // Held in refs so the hub subscription never rebuilds just because a callback changed.
  const handlersRef = useRef(handlers)
  handlersRef.current = handlers

  const stateRef = useRef<AuctionState | null>(null)

  const fetchState = useCallback(async (): Promise<AuctionState | null> => {
    if (publicId) return auctionsApi.stateByLink(publicId)
    if (auctionId !== null) return auctionsApi.state(auctionId)
    return null
  }, [auctionId, publicId])

  const loadPlayers = useCallback(async (tournamentId: number) => {
    try {
      setPlayers(await playersApi.list(tournamentId))
    } catch {
      /* keep the last known pool; the next state broadcast will retry */
    }
  }, [])

  const applyState = useCallback(
    (next: AuctionState) => {
      const previous = stateRef.current
      stateRef.current = next
      setState(next)

      // Bids do not change the pool, but calling, selling, passing and lifecycle
      // changes all do — only re-fetch the roster for those.
      const poolMayHaveChanged =
        previous === null ||
        previous.auction.id !== next.auction.id ||
        previous.auction.currentPlayerId !== next.auction.currentPlayerId ||
        previous.auction.status !== next.auction.status

      if (poolMayHaveChanged) void loadPlayers(next.auction.tournamentId)
    },
    [loadPlayers],
  )

  const reloadPlayers = useCallback(async () => {
    const tournamentId = stateRef.current?.auction.tournamentId
    if (tournamentId !== undefined) await loadPlayers(tournamentId)
  }, [loadPlayers])

  const refresh = useCallback(async () => {
    try {
      const next = await fetchState()
      if (next) applyState(next)
      setError(null)
    } catch (err) {
      setError(errorMessage(err, 'Could not load the auction.'))
    }
  }, [fetchState, applyState])

  useEffect(() => {
    if (auctionId === null && !publicId) {
      stateRef.current = null
      setState(null)
      setPlayers([])
      setLoading(false)
      return
    }

    let cancelled = false

    async function load() {
      setLoading(true)
      try {
        const next = await fetchState()
        if (cancelled || !next) return
        setError(null)
        applyState(next)
      } catch (err) {
        if (!cancelled) setError(errorMessage(err, 'Could not load the auction.'))
      } finally {
        if (!cancelled) setLoading(false)
      }
    }

    void load()

    return () => {
      cancelled = true
    }
  }, [auctionId, publicId, fetchState, applyState])

  // Spectators already hold the share id, so the socket opens on first paint. Signed-in
  // screens arrive with a sequential id and pick the share id up from the loaded state.
  const hubKey = useMemo(() => publicId ?? state?.auction.publicId ?? null, [publicId, state])

  const hub = useAuctionHub(hubKey, {
    onState: applyState,
    onViewers: setViewerCount,
    onReaction: (reaction) => handlersRef.current.onReaction?.(reaction),

    // Sound lives here rather than in each page, so every screen watching a room gets the
    // same cues. It is a no-op until the viewer has switched sound on.
    onBid: (bid) => {
      const base = stateRef.current?.currentPlayer?.basePrice ?? 0
      // Pitch tracks how far above the opening price the lot has climbed, so a room can
      // hear a bidding war without reading the number.
      const intensity = base > 0 ? (bid.amount - base) / (base * 4) : 0
      playCue('bid', intensity)
    },

    onSold: (player) => {
      playCue('sold')
      handlersRef.current.onSold?.(player)
    },

    onUnsold: (player) => {
      playCue('unsold')
      handlersRef.current.onUnsold?.(player)
    },
  })

  return {
    state,
    players,
    loading,
    error,
    hubStatus: hub.status,
    viewerCount,
    sendReaction: hub.sendReaction,
    applyState,
    refresh,
    reloadPlayers,
  }
}

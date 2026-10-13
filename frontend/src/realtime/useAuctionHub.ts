import { useCallback, useEffect, useRef, useState } from 'react'
import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import type { HubConnection } from '@microsoft/signalr'
import { API_BASE, getToken } from '../api/client'
import type { Auction, AuctionState, Bid, LiveAuctionCard, Player } from '../types'

export type HubStatus = 'idle' | 'connecting' | 'connected' | 'reconnecting' | 'disconnected'

export interface AuctionHubHandlers {
  onState?: (state: AuctionState) => void
  onBid?: (bid: Bid) => void
  onSold?: (player: Player) => void
  onUnsold?: (player: Player) => void
  onStatusChanged?: (auction: Auction) => void
  /** People currently in this room, pushed on every join and leave. */
  onViewers?: (count: number) => void
  onReaction?: (reaction: { emoji: string; from: string }) => void
}

export interface AuctionHubApi {
  status: HubStatus
  /** Relays a cheer to the room. Silently ignored when not connected or not signed in. */
  sendReaction: (emoji: string) => void
}

/**
 * Builds a hub connection. The token is optional — spectators connect anonymously, which
 * is why the viewer screens need no account.
 */
function build(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${API_BASE}/hubs/auction`, {
      accessTokenFactory: () => getToken() ?? '',
    })
    .withAutomaticReconnect([0, 2_000, 5_000, 10_000, 20_000])
    .configureLogging(LogLevel.Warning)
    .build()
}

/**
 * Keeps a live subscription to one auction.
 *
 * The server sends the complete room state after every change rather than a delta, so
 * a client that missed events while its connection was down converges as soon as it
 * rejoins — no event replay needed. Group membership does not survive a reconnect,
 * which is why JoinAuction is re-invoked on every (re)connect.
 *
 * Keyed on the auction's share id, not its sequential id: the hub is anonymous, so the
 * unguessable key is what stops a private room being joined by walking the id range.
 */
export function useAuctionHub(publicId: string | null, handlers: AuctionHubHandlers): AuctionHubApi {
  const [status, setStatus] = useState<HubStatus>('idle')

  // Held in a ref so changing callbacks never tear down and rebuild the connection.
  const handlersRef = useRef(handlers)
  handlersRef.current = handlers

  const connectionRef = useRef<HubConnection | null>(null)

  const sendReaction = useCallback(
    (emoji: string) => {
      const connection = connectionRef.current
      if (!publicId || connection?.state !== HubConnectionState.Connected) return

      // Fire and forget: a cheer that does not arrive is not worth an error.
      void connection.invoke('SendReaction', publicId, emoji).catch(() => undefined)
    },
    [publicId],
  )

  useEffect(() => {
    if (!publicId) {
      setStatus('idle')
      return
    }

    let cancelled = false
    const connection = build()

    connection.on('auctionState', (state: AuctionState) => handlersRef.current.onState?.(state))
    connection.on('bidPlaced', (bid: Bid) => handlersRef.current.onBid?.(bid))
    connection.on('playerSold', (player: Player) => handlersRef.current.onSold?.(player))
    connection.on('playerUnsold', (player: Player) => handlersRef.current.onUnsold?.(player))
    connection.on('auctionStatusChanged', (auction: Auction) =>
      handlersRef.current.onStatusChanged?.(auction),
    )
    connection.on('viewers', (count: number) => handlersRef.current.onViewers?.(count))
    connection.on('reaction', (reaction: { emoji: string; from: string }) =>
      handlersRef.current.onReaction?.(reaction),
    )

    connectionRef.current = connection

    connection.onreconnecting(() => !cancelled && setStatus('reconnecting'))

    connection.onreconnected(async () => {
      if (cancelled) return
      setStatus('connected')
      try {
        await connection.invoke('JoinAuction', publicId)
      } catch {
        /* the next reconnect attempt will retry the join */
      }
    })

    connection.onclose(() => !cancelled && setStatus('disconnected'))

    async function start() {
      setStatus('connecting')
      try {
        await connection.start()
        if (cancelled) return
        setStatus('connected')
        await connection.invoke('JoinAuction', publicId)
      } catch {
        if (!cancelled) setStatus('disconnected')
      }
    }

    const started = start()

    return () => {
      cancelled = true
      connectionRef.current = null
      // Wait for the handshake to settle before tearing down. Calling stop() during
      // negotiation makes SignalR log a connection failure for what is really just
      // a navigation away from the room.
      void started.finally(() => {
        if (connection.state !== HubConnectionState.Disconnected) {
          void connection.stop()
        }
      })
    }
  }, [publicId])

  return { status, sendReaction }
}

/**
 * Watches the set of running auctions rather than one room — the public landing page,
 * which has to notice an auction opening or closing without polling for it.
 */
export function useLobbyHub(onLobby: (running: LiveAuctionCard[]) => void): HubStatus {
  const [status, setStatus] = useState<HubStatus>('idle')

  const handlerRef = useRef(onLobby)
  handlerRef.current = onLobby

  useEffect(() => {
    let cancelled = false
    const connection = build()

    connection.on('lobby', (running: LiveAuctionCard[]) => handlerRef.current(running))

    connection.onreconnecting(() => !cancelled && setStatus('reconnecting'))

    connection.onreconnected(async () => {
      if (cancelled) return
      setStatus('connected')
      try {
        await connection.invoke('JoinLobby')
      } catch {
        /* the next reconnect attempt will retry the join */
      }
    })

    connection.onclose(() => !cancelled && setStatus('disconnected'))

    async function start() {
      setStatus('connecting')
      try {
        await connection.start()
        if (cancelled) return
        setStatus('connected')
        await connection.invoke('JoinLobby')
      } catch {
        if (!cancelled) setStatus('disconnected')
      }
    }

    const started = start()

    return () => {
      cancelled = true
      void started.finally(() => {
        if (connection.state !== HubConnectionState.Disconnected) {
          void connection.stop()
        }
      })
    }
  }, [])

  return status
}

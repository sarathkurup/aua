import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import { Link } from 'react-router'
import { errorMessage } from '../api/client'
import {
  auctionsApi,
  bidsApi,
  playersApi,
  sportsApi,
  devApi,
  tournamentsApi,
  usersApi,
} from '../api/endpoints'
import { AccountRequestQueue } from '../components/AccountRequestQueue'
import { AppShell } from '../components/AppShell'
import { AuctionRoom } from '../components/AuctionRoom'
import { useAuth } from '../auth/AuthContext'
import type { SoldEvent } from '../components/AuctionRoom'
import { BidPad } from '../components/BidPad'
import { BiddingRulesFields, defaultBiddingRules } from '../components/BiddingRulesFields'
import { PlayerFormDialog } from '../components/PlayerFormDialog'
import { TeamManager } from '../components/TeamManager'
import { TournamentForm } from '../components/TournamentForm'
import {
  AuctionStatusPill,
  EmptyState,
  Panel,
  TournamentTypePill,
} from '../components/primitives'
import { useToast } from '../components/Toasts'
import { money, moneyFull, scheduleStamp, timeUntil } from '../lib/format'
import { useAuctionRoom } from '../realtime/useAuctionRoom'
import { MAX_SCHEDULE_DAYS_AHEAD } from '../types'
import type {
  Auction,
  AuctionState,
  AuctionVisibility,
  OwnerOption,
  Player,
  SavePlayerRequest,
  SaveTournamentRequest,
  Sport,
  TeamSummary,
  Tournament,
} from '../types'

/**
 * The auctioneer's console.
 *
 * Work is scoped to one tournament at a time — its rules, teams, pool and auctions — but
 * the running-auction strip at the top stays global, because auctions in other tournaments
 * carry on regardless of what is selected here.
 */
export function AdminPage() {
  const { push } = useToast()
  const { isAdmin } = useAuth()

  const [sports, setSports] = useState<Sport[]>([])
  const [tournaments, setTournaments] = useState<Tournament[]>([])
  const [owners, setOwners] = useState<OwnerOption[]>([])
  const [auctions, setAuctions] = useState<Auction[]>([])
  const [teams, setTeams] = useState<TeamSummary[]>([])

  const [tournamentId, setTournamentId] = useState<number | null>(null)
  const [auctionId, setAuctionId] = useState<number | null>(null)

  const [editingRules, setEditingRules] = useState(false)
  const [creatingTournament, setCreatingTournament] = useState(false)
  const [busyAction, setBusyAction] = useState<string | null>(null)
  const [callingId, setCallingId] = useState<number | null>(null)
  const [editingPlayer, setEditingPlayer] = useState<Player | null>(null)
  const [dialogOpen, setDialogOpen] = useState(false)

  const [soldEvent, setSoldEvent] = useState<SoldEvent | null>(null)
  /** Team whose bid is in flight, so its button shows it rather than firing twice. */
  const [biddingTeamId, setBiddingTeamId] = useState<number | null>(null)
  const [undoing, setUndoing] = useState(false)
  /** Bring the next lot up automatically once a sale settles. */
  const [autoAdvance, setAutoAdvance] = useState(true)
  const advancedFor = useRef<number | null>(null)

  const room = useAuctionRoom({ auctionId }, {
    onSold: (player) => {
      setSoldEvent({ player, at: Date.now() })
      push(
        `${player.name} sold to ${player.soldToTeamName ?? 'a team'} for ${moneyFull(player.soldPrice)}.`,
        'success',
      )
    },
    onUnsold: (player) => {
      setSoldEvent(null)
      push(`${player.name} passed — no sale.`, 'warn')
    },
  })

  const { state, players, hubStatus, applyState, reloadPlayers } = room

  // Only what this account is the auctioneer for. An admin sees everything; everyone
  // else sees the tournaments they created.
  const manageable = useMemo(() => tournaments.filter((t) => t.canManage), [tournaments])
  const manageableIds = useMemo(() => new Set(manageable.map((t) => t.id)), [manageable])

  const tournament = manageable.find((t) => t.id === tournamentId) ?? null
  const sport = sports.find((s) => s.id === tournament?.sportId) ?? null
  const tournamentAuctions = auctions.filter((a) => a.tournamentId === tournamentId)
  const auction = state?.auction ?? null
  const isRunning = auction?.status === 'Live' || auction?.status === 'Paused'
  const running = auctions.filter(
    (a) => (a.status === 'Live' || a.status === 'Paused') && manageableIds.has(a.tournamentId),
  )

  const loadAuctions = useCallback(async () => {
    const list = await auctionsApi.list()
    setAuctions(list)
    return list
  }, [])

  const loadTeams = useCallback(async (id: number) => {
    setTeams(await tournamentsApi.teams(id))
  }, [])

  const loadTournaments = useCallback(async () => {
    const list = await tournamentsApi.list()
    setTournaments(list)
    return list
  }, [])

  // First load: select whichever tournament has something running, else the newest.
  useEffect(() => {
    let cancelled = false

    async function bootstrap() {
      try {
        const [sportList, tournamentList, auctionList, ownerList] = await Promise.all([
          sportsApi.list(),
          tournamentsApi.list(),
          auctionsApi.list(),
          usersApi.owners(),
        ])
        if (cancelled) return

        setSports(sportList)
        setTournaments(tournamentList)
        setAuctions(auctionList)
        setOwners(ownerList)

        const mine = tournamentList.filter((t) => t.canManage)
        const mineIds = new Set(mine.map((t) => t.id))
        const live = auctionList.find(
          (a) => (a.status === 'Live' || a.status === 'Paused') && mineIds.has(a.tournamentId),
        )
        const initial = live?.tournamentId ?? mine[0]?.id ?? null
        setTournamentId(initial)
        setAuctionId(live?.id ?? auctionList.find((a) => a.tournamentId === initial)?.id ?? null)
      } catch (err) {
        if (!cancelled) push(errorMessage(err, 'Could not load the console.'), 'error')
      }
    }

    void bootstrap()
    return () => {
      cancelled = true
    }
  }, [push])

  // Switching tournament re-points the console at that tournament's auction.
  useEffect(() => {
    if (tournamentId === null) return
    void loadTeams(tournamentId)
    setAuctionId((current) => {
      const belongs = auctions.find((a) => a.id === current)?.tournamentId === tournamentId
      if (belongs) return current
      const forTournament = auctions.filter((a) => a.tournamentId === tournamentId)
      const live = forTournament.find((a) => a.status === 'Live' || a.status === 'Paused')
      return live?.id ?? forTournament[0]?.id ?? null
    })
  }, [tournamentId, auctions, loadTeams])

  // Standings move with the room, so keep the team manager in step after each sale.
  useEffect(() => {
    if (state && tournamentId !== null) setTeams(state.teams)
  }, [state, tournamentId])

  /** Runs an admin command, surfacing the auctioneer's wording on a rule violation. */
  const run = useCallback(
    async (key: string, action: () => Promise<AuctionState | void>, successMessage?: string) => {
      setBusyAction(key)
      try {
        const next = await action()
        if (next) applyState(next)
        if (successMessage) push(successMessage, 'success')
        return true
      } catch (err) {
        push(errorMessage(err), 'error')
        return false
      } finally {
        setBusyAction(null)
      }
    },
    [applyState, push],
  )

  const lifecycle = useCallback(
    (key: string, action: () => Promise<AuctionState>, message: string) => async () => {
      await run(key, action, message)
      await loadAuctions()
    },
    [run, loadAuctions],
  )

  async function handleCall(player: Player) {
    if (!auction) return
    setCallingId(player.id)
    await run('call', () => auctionsApi.call(auction.id, player.id), `${player.name} is on the block.`)
    setCallingId(null)
  }

  /**
   * Books a bid against a team. Every bid in this app is recorded by the auctioneer — there
   * is no online bidding — so this is the single path a price moves through.
   */
  async function handleBid(teamId: number, amount: number) {
    if (!auction || !state?.currentPlayer) return

    setBiddingTeamId(teamId)
    try {
      await bidsApi.placeManual(auction.id, state.currentPlayer.id, teamId, amount)
    } catch (err) {
      push(errorMessage(err, 'Bid rejected.'), 'error')
    } finally {
      setBiddingTeamId(null)
    }
  }

  /** Takes the standing bid back off the board after a mis-tap. */
  async function handleUndoBid() {
    if (!auction) return

    setUndoing(true)
    try {
      const next = await bidsApi.undo(auction.id)
      applyState(next)
      push(
        next.topBid
          ? `Bid taken back. ${next.topBid.teamName} leads again at ${moneyFull(next.topBid.amount)}.`
          : 'Bid taken back. The lot is open at its base price again.',
        'success',
      )
    } catch (err) {
      push(errorMessage(err, 'Could not take that bid back.'), 'error')
    } finally {
      setUndoing(false)
    }
  }

  /** Un-sells the last lot and refunds it — the way back from a wrong hammer. */
  async function handleReopen() {
    if (!auction || !state?.lastSale) return

    const { playerName, teamName, amount } = state.lastSale
    if (
      !window.confirm(
        `Reopen ${playerName}?\n\n${teamName} will be refunded ${moneyFull(amount)} and the lot ` +
          'goes back on the block with its bids intact.',
      )
    ) {
      return
    }

    await run('reopen', () => auctionsApi.reopenLastLot(auction.id), `${playerName} is back on the block.`)
    await reloadPlayers()
  }

  async function handleSavePlayer(body: SavePlayerRequest): Promise<Player> {
    try {
      const saved = editingPlayer
        ? await playersApi.update(editingPlayer.id, body)
        : await playersApi.create(body)

      await reloadPlayers()
      await loadTournaments()
      push(editingPlayer ? `${body.name} updated.` : `${body.name} added to the pool.`, 'success')

      // Keep editing the same row so a photo can be attached straight after creating it.
      setEditingPlayer(saved)
      return saved
    } catch (err) {
      throw new Error(errorMessage(err, 'Could not save the player.'))
    }
  }

  async function handleDeletePlayer(player: Player) {
    if (!window.confirm(`Remove ${player.name} from the pool?`)) return

    try {
      await playersApi.remove(player.id)
      await reloadPlayers()
      push(`${player.name} removed.`, 'success')
    } catch (err) {
      push(errorMessage(err, 'Could not remove the player.'), 'error')
    }
  }

  const nextLotOrder = useMemo(
    () => players.reduce((highest, player) => Math.max(highest, player.lotOrder), 0) + 1,
    [players],
  )

  const poolPlayers = players.length > 0 || auctionId !== null ? players : []

  const nextLot = useMemo(
    () =>
      poolPlayers
        .filter((player) => player.status === 'Available')
        .sort((a, b) => a.lotOrder - b.lotOrder)[0] ?? null,
    [poolPlayers],
  )

  /**
   * After a sale settles, bring the next player up on their own.
   *
   * The delay lets the sold card finish flying into its team column — calling instantly
   * would swap the lot out from under the animation. Guarded by the sale's timestamp so a
   * re-render cannot call the same next player twice, and switched off the moment the
   * auctioneer turns auto-advance off.
   */
  useEffect(() => {
    if (!autoAdvance || !soldEvent || !auction || !nextLot) return
    if (advancedFor.current === soldEvent.at) return
    if (auction.status !== 'Live' || state?.currentPlayer) return

    advancedFor.current = soldEvent.at
    const timer = window.setTimeout(() => void handleCall(nextLot), 1600)
    return () => window.clearTimeout(timer)
    // handleCall is recreated each render; the timestamp guard is what keeps this once-only.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [soldEvent, autoAdvance, auction, nextLot, state?.currentPlayer])

  return (
    <AppShell hubStatus={hubStatus}>
      <div className="space-y-4">
        {/* Only the site admin decides who gets an account. */}
        {isAdmin && <AccountRequestQueue />}

        {/* Everything currently running, whichever tournament it belongs to. */}
        {running.length > 0 && (
          <Panel className="!p-3">
            <div className="flex flex-wrap items-center gap-2">
              <span className="section-title">Running now</span>
              {running.map((item) => (
                <button
                  key={item.id}
                  type="button"
                  className={`btn btn-sm ${item.id === auctionId ? 'btn-primary' : ''}`}
                  onClick={() => {
                    setTournamentId(item.tournamentId)
                    setAuctionId(item.id)
                  }}
                >
                  {item.sportIcon} {item.name}
                </button>
              ))}
              <Link to="/" className="btn btn-sm ml-auto">
                Public view
              </Link>
            </div>
          </Panel>
        )}

        <Panel
          title="Tournament"
          strong
          action={
            tournament && (
              <div className="flex flex-wrap gap-2">
                <TournamentTypePill type={tournament.type} />
                <button
                  type="button"
                  className="btn btn-sm"
                  onClick={() => setEditingRules((open) => !open)}
                  disabled={isRunning}
                  title={isRunning ? 'Rules cannot change mid-auction' : 'Edit rules'}
                >
                  {editingRules ? 'Close rules' : 'Rules'}
                </button>
              </div>
            )
          }
        >
          <div className="space-y-3">
            <div className="flex flex-wrap items-end gap-2">
              <div className="min-w-[14rem] flex-1">
                <label className="field-label" htmlFor="tournament-select">
                  Working on
                </label>
                <select
                  id="tournament-select"
                  className="field"
                  value={tournamentId ?? ''}
                  onChange={(event) => setTournamentId(Number(event.target.value) || null)}
                >
                  {manageable.length === 0 && <option value="">No tournaments yet</option>}
                  {manageable.map((item) => (
                    <option key={item.id} value={item.id}>
                      {item.sportIcon} {item.name} · {item.playerCount} players
                    </option>
                  ))}
                </select>
              </div>

              <button
                type="button"
                className="btn btn-sm"
                onClick={() => {
                  setCreatingTournament((open) => !open)
                  setEditingRules(false)
                }}
              >
                {creatingTournament ? 'Cancel' : 'New tournament'}
              </button>

              {/* Development only: the endpoint refuses anywhere else. */}
              {import.meta.env.DEV && (
                <button
                  type="button"
                  className="btn btn-sm"
                  disabled={busyAction !== null}
                  title="Creates a tournament whose photos cover portrait, landscape and square"
                  onClick={async () => {
                    setBusyAction('mock')
                    try {
                      const result = await devApi.seedMockData()
                      const list = await loadTournaments()
                      setTournamentId(result.tournamentId)
                      await loadAuctions()
                      push(`${result.name}: ${result.players} players with photos.`, 'success')
                      if (list.length === 0) push('Nothing was created.', 'warn')
                    } catch (err) {
                      push(errorMessage(err, 'Could not create the test data.'), 'error')
                    } finally {
                      setBusyAction(null)
                    }
                  }}
                >
                  {busyAction === 'mock' ? 'Building…' : 'Load test data'}
                </button>
              )}

              {tournament && (
                <button
                  type="button"
                  className="btn btn-sm btn-danger"
                  disabled={busyAction !== null || isRunning}
                  onClick={async () => {
                    if (!window.confirm(`Delete ${tournament.name} and everything in it?`)) return
                    try {
                      await tournamentsApi.remove(tournament.id)
                      const list = await loadTournaments()
                      await loadAuctions()
                      setTournamentId(list[0]?.id ?? null)
                      push('Tournament deleted.', 'success')
                    } catch (err) {
                      push(errorMessage(err), 'error')
                    }
                  }}
                >
                  Delete
                </button>
              )}
            </div>

            {tournament?.description && (
              <p className="text-sm text-slate-400">{tournament.description}</p>
            )}

            {(creatingTournament || editingRules) && (
              <TournamentForm
                tournament={creatingTournament ? null : tournament}
                sports={sports}
                onCancel={() => {
                  setCreatingTournament(false)
                  setEditingRules(false)
                }}
                onSave={async (body: SaveTournamentRequest) => {
                  try {
                    if (creatingTournament) {
                      const created = await tournamentsApi.create(body)
                      await loadTournaments()
                      setTournamentId(created.id)
                      setCreatingTournament(false)
                      push(`${created.name} created.`, 'success')
                    } else if (tournament) {
                      await tournamentsApi.update(tournament.id, body)
                      await loadTournaments()
                      await loadTeams(tournament.id)
                      setEditingRules(false)
                      push('Rules saved.', 'success')
                    }
                  } catch (err) {
                    throw new Error(errorMessage(err, 'Could not save the tournament.'))
                  }
                }}
              />
            )}
          </div>
        </Panel>

        {tournament && (
          <TeamManager
            teams={teams}
            owners={owners}
            disabled={isRunning}
            onAdd={async (name, shortName, ownerUserId) => {
              try {
                await tournamentsApi.addTeam(tournament.id, { name, shortName, ownerUserId })
                await loadTeams(tournament.id)
                await loadTournaments()
                push(`${name} added.`, 'success')
              } catch (err) {
                push(errorMessage(err), 'error')
              }
            }}
            onUpdate={async (teamId, ownerUserId) => {
              const team = teams.find((t) => t.teamId === teamId)
              if (!team) return
              try {
                await tournamentsApi.updateTeam(teamId, {
                  name: team.name,
                  shortName: team.shortName,
                  ownerUserId,
                })
                await loadTeams(tournament.id)
                push('Owner updated.', 'success')
              } catch (err) {
                push(errorMessage(err), 'error')
              }
            }}
            onRemove={async (team) => {
              if (!window.confirm(`Remove ${team.name}?`)) return
              try {
                await tournamentsApi.removeTeam(team.teamId)
                await loadTeams(tournament.id)
                await loadTournaments()
                push(`${team.name} removed.`, 'success')
              } catch (err) {
                push(errorMessage(err), 'error')
              }
            }}
            onAddMember={async (teamId, body) => {
              try {
                await tournamentsApi.addMember(teamId, body)
                await loadTeams(tournament.id)
                await reloadPlayers()
                push(`${body.name} added to the squad.`, 'success')
              } catch (err) {
                push(errorMessage(err), 'error')
              }
            }}
            onRemoveMember={async (playerId, name) => {
              if (!window.confirm(`Remove ${name} from the squad?`)) return
              try {
                await tournamentsApi.removeMember(playerId)
                await loadTeams(tournament.id)
                await reloadPlayers()
                push(`${name} removed.`, 'success')
              } catch (err) {
                push(errorMessage(err), 'error')
              }
            }}
          />
        )}

        {tournament && (
          <Panel
            title="Auctions"
            action={auction && <AuctionStatusPill status={auction.status} />}
          >
            <div className="space-y-3">
              <div className="flex flex-wrap items-end gap-2">
                <div className="min-w-[12rem] flex-1">
                  <label className="field-label" htmlFor="auction-select">
                    Auction
                  </label>
                  <select
                    id="auction-select"
                    className="field"
                    value={auctionId ?? ''}
                    onChange={(event) => setAuctionId(Number(event.target.value) || null)}
                  >
                    {tournamentAuctions.length === 0 && <option value="">None yet</option>}
                    {tournamentAuctions.map((item) => (
                      <option key={item.id} value={item.id}>
                        {item.name} · {item.status}
                      </option>
                    ))}
                  </select>
                </div>

                {auction && (
                  <div className="flex flex-wrap gap-2">
                    {auction.status === 'Draft' && (
                      <button
                        type="button"
                        className="btn btn-live"
                        disabled={busyAction !== null}
                        onClick={lifecycle(
                          'start',
                          () => auctionsApi.start(auction.id),
                          `${auction.name} is live.`,
                        )}
                      >
                        Start
                      </button>
                    )}

                    {auction.status === 'Live' && (
                      <button
                        type="button"
                        className="btn btn-warn"
                        disabled={busyAction !== null}
                        onClick={lifecycle('pause', () => auctionsApi.pause(auction.id), 'Bidding paused.')}
                      >
                        Pause
                      </button>
                    )}

                    {auction.status === 'Paused' && (
                      <button
                        type="button"
                        className="btn btn-live"
                        disabled={busyAction !== null}
                        onClick={lifecycle('resume', () => auctionsApi.resume(auction.id), 'Bidding resumed.')}
                      >
                        Resume
                      </button>
                    )}

                    {isRunning && (
                      <button
                        type="button"
                        className="btn btn-danger"
                        disabled={busyAction !== null}
                        onClick={() => {
                          if (window.confirm('Close this auction? Any lot on the block goes unsold.')) {
                            void lifecycle('end', () => auctionsApi.end(auction.id), 'Auction closed.')()
                          }
                        }}
                      >
                        End
                      </button>
                    )}

                    {!isRunning && (
                      <button
                        type="button"
                        className="btn"
                        disabled={busyAction !== null}
                        onClick={async () => {
                          if (!window.confirm(`Delete ${auction.name}?`)) return
                          const ok = await run('delete', () => auctionsApi.remove(auction.id), 'Auction deleted.')
                          if (!ok) return
                          const list = await loadAuctions()
                          setAuctionId(list.find((a) => a.tournamentId === tournament.id)?.id ?? null)
                        }}
                      >
                        Delete
                      </button>
                    )}
                  </div>
                )}
              </div>

              {auction && (
                <>
                  {auction.scheduledStartAt && auction.status === 'Draft' && (
                    <p className="glass-inset px-3 py-2 text-xs text-slate-300">
                      <span className="pill pill-info mr-2">Scheduled</span>
                      Opens {scheduleStamp(auction.scheduledStartAt)} ·{' '}
                      {timeUntil(auction.scheduledStartAt)}. It cannot be started before then —
                      clear the date below to open it now.
                    </p>
                  )}

                  <ShareLink auction={auction} />

                  <EditAuctionForm
                    auction={auction}
                    onSaved={async () => {
                      await loadAuctions()
                      await room.refresh()
                      push('Auction updated.', 'success')
                    }}
                  />
                </>
              )}

              <NewAuctionForm
                tournamentId={tournament.id}
                onCreated={async (created) => {
                  await loadAuctions()
                  setAuctionId(created.id)
                  push(
                    created.visibility === 'Private'
                      ? `${created.name} created as a private draft. Share its link to let people watch.`
                      : `${created.name} created as a draft.`,
                    'success',
                  )
                }}
              />
            </div>
          </Panel>
        )}

        {!tournament && (
          <Panel>
            <EmptyState>Create a tournament to get started.</EmptyState>
          </Panel>
        )}

        {state && auction && (
          <AuctionRoom
            state={state}
            players={poolPlayers}
            soldEvent={soldEvent}
            viewerCount={room.viewerCount}
            controls={
              <Panel
                title="The hammer"
                strong
                action={
                  auction.status === 'Live' ? (
                    <label className="flex cursor-pointer items-center gap-2 text-xs text-slate-300">
                      <input
                        type="checkbox"
                        className="accent-brand-500"
                        checked={autoAdvance}
                        onChange={(event) => setAutoAdvance(event.target.checked)}
                      />
                      Auto-call next lot
                    </label>
                  ) : undefined
                }
              >
                {!state.currentPlayer ? (
                  <div className="space-y-3">
                    <EmptyState>
                      {auction.status === 'Live'
                        ? 'Call a player from the pool below to open the next lot.'
                        : 'Start the auction to call players.'}
                    </EmptyState>

                    {auction.status === 'Live' && nextLot && (
                      <button
                        type="button"
                        className="btn btn-primary w-full"
                        disabled={callingId !== null}
                        onClick={() => void handleCall(nextLot)}
                      >
                        Call lot {nextLot.lotOrder}: {nextLot.name} · opens at{' '}
                        {money(nextLot.basePrice)}
                      </button>
                    )}

                    {/* Offered only between lots, which is also the only time it is legal:
                        reopening while a lot is up would put two players on the block. */}
                    {state.lastSale && (
                      <button
                        type="button"
                        className="btn w-full"
                        disabled={busyAction !== null}
                        onClick={() => void handleReopen()}
                        title={`Un-sell ${state.lastSale.playerName} and refund ${state.lastSale.teamName}`}
                      >
                        ↩ Reopen {state.lastSale.playerName} — sold to {state.lastSale.teamName} for{' '}
                        {money(state.lastSale.amount)}
                      </button>
                    )}
                  </div>
                ) : (
                  <div className="space-y-3">
                    <div className="flex flex-wrap gap-2">
                      <button
                        type="button"
                        className="btn btn-live"
                        disabled={busyAction !== null || !state.topBid}
                        onClick={() =>
                          void run(
                            'sell',
                            () => auctionsApi.sell(auction.id),
                            `Sold to ${state.topBid?.teamName}.`,
                          )
                        }
                        title={state.topBid ? 'Hammer down to the leading bid' : 'No bids to sell against'}
                      >
                        Sell
                      </button>
                      <button
                        type="button"
                        className="btn btn-warn"
                        disabled={busyAction !== null}
                        onClick={() =>
                          void run('unsold', () => auctionsApi.markUnsold(auction.id), 'Marked unsold.')
                        }
                      >
                        Pass / unsold
                      </button>
                    </div>

                    <BidPad
                      state={state}
                      disabled={auction.status !== 'Live' || biddingTeamId !== null}
                      busyTeamId={biddingTeamId}
                      undoing={undoing}
                      onBid={(teamId, amount) => void handleBid(teamId, amount)}
                      onUndo={() => void handleUndoBid()}
                    />
                  </div>
                )}
              </Panel>
            }
            poolActions={{
              players: poolPlayers,
              title: `${auction.sportName} pool`,
              canCall: auction.status === 'Live' && !state.currentPlayer,
              busyId: callingId,
              onCall: (player) => void handleCall(player),
              onEdit: (player) => {
                setEditingPlayer(player)
                setDialogOpen(true)
              },
              onDelete: (player) => void handleDeletePlayer(player),
            }}
          />
        )}

        {tournament && (
          <div className="flex justify-end">
            <button
              type="button"
              className="btn btn-primary"
              onClick={() => {
                setEditingPlayer(null)
                setDialogOpen(true)
              }}
            >
              Add player
            </button>
          </div>
        )}
      </div>

      {dialogOpen && tournament && (
        <PlayerFormDialog
          player={editingPlayer}
          tournamentId={tournament.id}
          skillFields={sport?.skillFields ?? []}
          genderRequired={tournament.rules.minWomenPerSquad > 0}
          nextLotOrder={nextLotOrder}
          onSave={handleSavePlayer}
          onClose={() => {
            setDialogOpen(false)
            setEditingPlayer(null)
            void reloadPlayers()
          }}
        />
      )}
    </AppShell>
  )
}

function NewAuctionForm({
  tournamentId,
  onCreated,
}: {
  tournamentId: number
  onCreated: (auction: Auction) => Promise<void>
}) {
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [visibility, setVisibility] = useState<AuctionVisibility>('Public')
  const [scheduledAt, setScheduledAt] = useState('')
  const [rules, setRules] = useState(() => defaultBiddingRules(100_000))
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)

    try {
      const created = await auctionsApi.create({
        ...rules,
        name: name.trim(),
        tournamentId,
        visibility,
        scheduledStartAt: toInstant(scheduledAt),
      })
      await onCreated(created)
      setName('')
      setOpen(false)
    } catch (err) {
      setError(errorMessage(err, 'Could not create the auction.'))
    } finally {
      setBusy(false)
    }
  }

  if (!open) {
    return (
      <button type="button" className="btn btn-sm" onClick={() => setOpen(true)}>
        New auction
      </button>
    )
  }

  return (
    <form onSubmit={handleSubmit} className="glass-inset space-y-3 p-3">
      <div className="grid gap-3 sm:grid-cols-2">
        <div>
          <label className="field-label" htmlFor="auction-name">
            Name
          </label>
          <input
            id="auction-name"
            className="field"
            value={name}
            onChange={(event) => setName(event.target.value)}
            required
            autoFocus
          />
        </div>
      </div>

      <BiddingRulesFields value={rules} onChange={setRules} idPrefix="new-auction" />

      <div>
        <label className="field-label" htmlFor="auction-schedule">
          Opens on (optional)
        </label>
        <input
          id="auction-schedule"
          className="field"
          type="datetime-local"
          value={scheduledAt}
          min={localInputValue(new Date())}
          max={localInputValue(addDays(new Date(), MAX_SCHEDULE_DAYS_AHEAD))}
          onChange={(event) => setScheduledAt(event.target.value)}
        />
        <p className="mt-1 text-xs text-slate-500">
          Up to {MAX_SCHEDULE_DAYS_AHEAD} days ahead. It appears on the dashboard as scheduled
          and can be started from that time. Leave blank to start it by hand.
        </p>
      </div>

      {/* The consequence of this choice is what people get wrong, so it is spelled out
          on the option itself rather than hidden behind a help icon. */}
      <fieldset>
        <legend className="field-label">Who can find this auction</legend>
        <div className="grid gap-2 sm:grid-cols-2">
          {VISIBILITY_CHOICES.map((choice) => (
            <label
              key={choice.value}
              className={`glass-inset cursor-pointer p-3 transition ${
                visibility === choice.value ? 'ring-1 ring-brand-400/70' : 'hover:bg-white/6'
              }`}
            >
              <span className="flex items-center gap-2">
                <input
                  type="radio"
                  name="auction-visibility"
                  className="accent-brand-500"
                  value={choice.value}
                  checked={visibility === choice.value}
                  onChange={() => setVisibility(choice.value)}
                />
                <span className="text-sm font-semibold text-slate-100">{choice.label}</span>
              </span>
              <span className="mt-1 block text-xs leading-relaxed text-slate-400">
                {choice.description}
              </span>
            </label>
          ))}
        </div>
      </fieldset>

      {error && (
        <p className="text-sm text-danger-400" role="alert">
          {error}
        </p>
      )}

      <div className="flex justify-end gap-2">
        <button type="button" className="btn btn-sm" onClick={() => setOpen(false)} disabled={busy}>
          Cancel
        </button>
        <button type="submit" className="btn btn-sm btn-primary" disabled={busy}>
          {busy ? 'Creating…' : 'Create draft'}
        </button>
      </div>
    </form>
  )
}

function addDays(from: Date, days: number): Date {
  const next = new Date(from)
  next.setDate(next.getDate() + days)
  return next
}

/** `datetime-local` wants local wall-clock text, not an ISO instant. */
function localInputValue(when: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0')
  return (
    `${when.getFullYear()}-${pad(when.getMonth() + 1)}-${pad(when.getDate())}` +
    `T${pad(when.getHours())}:${pad(when.getMinutes())}`
  )
}

/** Pulls the bidding rules off a saved auction so the edit form starts where it left off. */
function rulesFrom(auction: Auction) {
  return {
    minBidIncrement: auction.minBidIncrement,
    minimumBid: auction.minimumBid,
    increments: auction.increments.length > 0
      ? auction.increments
      : [{ from: 0, increment: auction.minBidIncrement }],
    autoTimeoutSeconds: auction.autoTimeoutSeconds,
  }
}

/** Local wall-clock text back to an ISO instant, or null when the field is blank. */
function toInstant(localValue: string): string | null {
  if (!localValue) return null
  const parsed = new Date(localValue)
  return Number.isNaN(parsed.getTime()) ? null : parsed.toISOString()
}

/**
 * Edits an auction. Available whatever state it is in — the auctioneer can rename a room,
 * change its increment, unlist it or move its opening time while it is running.
 */
function EditAuctionForm({
  auction,
  onSaved,
}: {
  auction: Auction
  onSaved: () => Promise<void>
}) {
  const [open, setOpen] = useState(false)
  const [name, setName] = useState(auction.name)
  const [visibility, setVisibility] = useState<AuctionVisibility>(auction.visibility)
  const [scheduledAt, setScheduledAt] = useState(
    auction.scheduledStartAt ? localInputValue(new Date(auction.scheduledStartAt)) : '',
  )
  const [rules, setRules] = useState(() => rulesFrom(auction))
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState<string | null>(null)

  function reopen() {
    setName(auction.name)
    setVisibility(auction.visibility)
    setScheduledAt(
      auction.scheduledStartAt ? localInputValue(new Date(auction.scheduledStartAt)) : '',
    )
    setRules(rulesFrom(auction))
    setError(null)
    setOpen(true)
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    setError(null)

    try {
      await auctionsApi.update(auction.id, {
        ...rules,
        name: name.trim(),
        visibility,
        scheduledStartAt: toInstant(scheduledAt),
      })
      await onSaved()
      setOpen(false)
    } catch (err) {
      setError(errorMessage(err, 'Could not save the auction.'))
    } finally {
      setBusy(false)
    }
  }

  if (!open) {
    return (
      <button type="button" className="btn btn-sm" onClick={reopen}>
        Edit auction
      </button>
    )
  }

  return (
    <form onSubmit={handleSubmit} className="glass-inset space-y-3 p-3">
      <div className="grid gap-3 sm:grid-cols-2">
        <div>
          <label className="field-label" htmlFor="edit-auction-name">
            Name
          </label>
          <input
            id="edit-auction-name"
            className="field"
            value={name}
            onChange={(event) => setName(event.target.value)}
            required
            autoFocus
          />
        </div>
      </div>

      <BiddingRulesFields value={rules} onChange={setRules} idPrefix="edit-auction" />

      <div className="grid gap-3 sm:grid-cols-2">
        <div>
          <label className="field-label" htmlFor="edit-auction-schedule">
            Opens on
          </label>
          <input
            id="edit-auction-schedule"
            className="field"
            type="datetime-local"
            value={scheduledAt}
            max={localInputValue(addDays(new Date(), MAX_SCHEDULE_DAYS_AHEAD))}
            onChange={(event) => setScheduledAt(event.target.value)}
          />
          <p className="mt-1 text-xs text-slate-500">
            Clear this to start the auction immediately.
          </p>
        </div>
        <div>
          <label className="field-label" htmlFor="edit-auction-visibility">
            Visibility
          </label>
          <select
            id="edit-auction-visibility"
            className="field"
            value={visibility}
            onChange={(event) => setVisibility(event.target.value as AuctionVisibility)}
          >
            <option value="Public">Public — listed on the home page</option>
            <option value="Private">Private — unlisted, link only</option>
          </select>
        </div>
      </div>

      {error && (
        <p className="text-sm text-danger-400" role="alert">
          {error}
        </p>
      )}

      <div className="flex justify-end gap-2">
        <button type="button" className="btn btn-sm" onClick={() => setOpen(false)} disabled={busy}>
          Cancel
        </button>
        <button type="submit" className="btn btn-sm btn-primary" disabled={busy}>
          {busy ? 'Saving…' : 'Save changes'}
        </button>
      </div>
    </form>
  )
}

const VISIBILITY_CHOICES: Array<{
  value: AuctionVisibility
  label: string
  description: string
}> = [
  {
    value: 'Public',
    label: 'Public',
    description:
      'Listed on the home page for anyone to open. Player photos and profiles are visible to the public.',
  },
  {
    value: 'Private',
    label: 'Private',
    description:
      'Unlisted. Only people you send the share link to can watch, and player photos stay hidden from everyone else.',
  },
]

/**
 * The share link, and the only address a spectator is ever given. Shown for public rooms
 * too — a direct link is the easiest way to send someone to a specific auction.
 */
function ShareLink({ auction }: { auction: Auction }) {
  const [copied, setCopied] = useState(false)
  const url = `${window.location.origin}/watch/${auction.publicId}`

  async function copy() {
    try {
      await navigator.clipboard.writeText(url)
      setCopied(true)
      window.setTimeout(() => setCopied(false), 2000)
    } catch {
      // Clipboard access is refused on insecure origins; the input below is
      // selectable so the link can still be copied by hand.
    }
  }

  return (
    <div className="glass-inset p-3">
      <div className="flex flex-wrap items-center gap-2">
        <p className="section-title">Share link</p>
        <span className={`pill ${auction.visibility === 'Private' ? 'pill-warn' : 'pill-live'}`}>
          {auction.visibility === 'Private' ? 'unlisted' : 'listed publicly'}
        </span>
      </div>

      <div className="mt-2 flex flex-wrap items-center gap-2">
        <input
          className="field nums min-w-0 flex-1 py-1.5 text-xs"
          value={url}
          readOnly
          onFocus={(event) => event.target.select()}
          aria-label="Public link to this auction"
        />
        <button type="button" className="btn btn-sm" onClick={() => void copy()}>
          {copied ? 'Copied' : 'Copy'}
        </button>
      </div>

      <p className="mt-2 text-xs text-slate-400">
        {auction.visibility === 'Private'
          ? 'This room is not listed anywhere. Anyone holding this link can watch it live — share it with as many people as you like.'
          : 'This room is listed on the home page. Anyone can find and watch it.'}
      </p>
    </div>
  )
}

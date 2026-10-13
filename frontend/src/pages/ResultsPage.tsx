import { useMemo, useState } from 'react'
import { Link, useParams } from 'react-router'
import { AppShell } from '../components/AppShell'
import { EmptyState, Panel, PlayerStatusPill } from '../components/primitives'
import { PlayerPhoto } from '../components/PlayerPhoto'
import { useToast } from '../components/Toasts'
import { useAuth } from '../auth/AuthContext'
import { auctionsApi } from '../api/endpoints'
import { useAuctionRoom } from '../realtime/useAuctionRoom'
import { hueFor, money, moneyFull, squadNumber } from '../lib/format'
import { resultsUrl, share, teamUrl } from '../lib/share'
import type { AuctionState, Player, TeamSummary } from '../types'

type Tab = 'teams' | 'unsold' | 'pending'

/**
 * The read-out: who bought whom, what is still to come, and what went unsold.
 *
 * Reached by the same share link as the room, because whoever may watch may read the
 * results. It rides the same hub, so the counts move while an auction is still running —
 * this is a live summary, not a report generated at the end.
 */
export function ResultsPage() {
  const { publicId } = useParams()
  const { push } = useToast()
  const { teamIn } = useAuth()

  const room = useAuctionRoom({ publicId })
  const { state, players, loading, error } = room

  const [tab, setTab] = useState<Tab>('teams')
  const [query, setQuery] = useState('')

  const unsold = useMemo(() => players.filter((p) => p.status === 'Unsold'), [players])
  const pending = useMemo(
    () => players.filter((p) => p.status === 'Available' || p.status === 'OnBlock'),
    [players],
  )

  const myTeam = state ? teamIn(state.auction.tournamentId) : null

  async function shareResults() {
    if (!state) return

    const outcome = await share({
      title: `${state.auction.name} — results`,
      text: `Results for ${state.auction.name}`,
      url: resultsUrl(state.auction.publicId),
    })

    if (outcome === 'copied') push('Results link copied.', 'success')
    if (outcome === 'failed') push('Could not share that link.', 'warn')
  }

  return (
    <AppShell hubStatus={room.hubStatus}>
      {loading && <Panel>Loading results…</Panel>}

      {!loading && error && (
        <Panel>
          <p className="text-sm text-danger-400">{error}</p>
          <Link to="/" className="btn btn-sm mt-3">
            Back to live auctions
          </Link>
        </Panel>
      )}

      {!loading && !error && !state && (
        <Panel strong>
          <EmptyState>That link does not match an auction.</EmptyState>
        </Panel>
      )}

      {state && (
        <div className="space-y-4">
          <Panel className="!p-3">
            <div className="flex flex-wrap items-center gap-3">
              <span aria-hidden className="text-2xl">
                {state.auction.sportIcon ?? '🏆'}
              </span>
              <div className="min-w-0 flex-1 basis-full sm:basis-auto">
                <p className="text-sm font-bold text-white">{state.auction.name}</p>
                <p className="truncate text-xs text-slate-400">Auction results</p>
              </div>
              <Link to={`/watch/${state.auction.publicId}`} className="btn btn-sm btn-primary">
                Live view
              </Link>
              <button type="button" className="btn btn-sm" onClick={() => void shareResults()}>
                Share
              </button>

              {/* Plain links, not fetch-and-blob: the browser's own download handling gets
                  the filename from the response and works the same on a phone. */}
              <a
                className="btn btn-sm"
                href={auctionsApi.exportUrl(state.auction.publicId, 'squads')}
                download
              >
                Squads CSV
              </a>
              <a
                className="btn btn-sm"
                href={auctionsApi.exportUrl(state.auction.publicId, 'bids')}
                download
              >
                Bids CSV
              </a>
            </div>
          </Panel>

          {/* The room, condensed to one line and pinned. Someone reading the results while
              the sale is still running should not have to leave to see where it is up to. */}
          {state.currentPlayer && <LiveTicker state={state} />}

          <div className="flex flex-wrap gap-1" role="tablist" aria-label="Results view">
            <TabButton active={tab === 'teams'} onClick={() => setTab('teams')}>
              Teams ({state.teams.length})
            </TabButton>
            <TabButton active={tab === 'unsold'} onClick={() => setTab('unsold')}>
              Unsold ({unsold.length})
            </TabButton>
            <TabButton active={tab === 'pending'} onClick={() => setTab('pending')}>
              Yet to auction ({pending.length})
            </TabButton>
          </div>

          <input
            type="search"
            className="field"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Search by name or #number…"
            aria-label="Search players"
          />

          {tab === 'teams' && (
            <div className="space-y-3">
              {state.teams.map((team) => (
                <TeamResultCard
                  key={team.teamId}
                  team={team}
                  players={players}
                  query={query}
                  auctionName={state.auction.name}
                  publicId={state.auction.publicId}
                  isYours={team.teamId === myTeam?.teamId}
                />
              ))}
            </div>
          )}

          {tab === 'unsold' && <PlayerList players={filterPlayers(unsold, query)} emptyLabel="Nothing has gone unsold." />}

          {tab === 'pending' && (
            <PlayerList players={filterPlayers(pending, query)} emptyLabel="Every player has been called." />
          )}
        </div>
      )}
    </AppShell>
  )
}

function TabButton({
  active,
  onClick,
  children,
}: {
  active: boolean
  onClick: () => void
  children: React.ReactNode
}) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={active}
      className={`btn btn-sm ${active ? 'btn-primary' : ''}`}
      onClick={onClick}
    >
      {children}
    </button>
  )
}

function LiveTicker({ state }: { state: AuctionState }) {
  const player = state.currentPlayer!

  return (
    <div className="glass sticky top-16 z-10 flex flex-wrap items-center gap-3 p-3">
      <span className="pill pill-live">
        <span aria-hidden className="inline-block size-1.5 rounded-full bg-current animate-pulse-ring" />
        Live
      </span>

      <PlayerPhoto player={player} size="sm" />

      <div className="min-w-0 flex-1">
        <p className="truncate text-sm font-semibold text-slate-100">
          <span className="nums text-slate-500">{squadNumber(player)}</span> {player.name}
        </p>
        <p className="truncate text-xs text-slate-400">{player.role ?? player.sportName}</p>
      </div>

      <div className="text-right">
        <p className="nums text-lg font-black text-white">
          {money(state.topBid?.amount ?? player.basePrice)}
        </p>
        <p className="text-[0.65rem] text-slate-400">
          {state.topBid ? state.topBid.teamName : 'No bids yet'}
        </p>
      </div>
    </div>
  )
}

/**
 * One squad, with the numbers that decide whether it can still compete: what it owes, what
 * it can commit to a single player, and what is left in the bank.
 */
function TeamResultCard({
  team,
  players,
  query,
  auctionName,
  publicId,
  isYours,
}: {
  team: TeamSummary
  players: Player[]
  query: string
  auctionName: string
  publicId: string
  isYours: boolean
}) {
  const { push } = useToast()
  const [open, setOpen] = useState(true)

  const squad = useMemo(
    () =>
      players
        .filter((p) => p.soldToTeamId === team.teamId && p.status === 'Sold')
        .sort((a, b) => (b.soldPrice ?? 0) - (a.soldPrice ?? 0)),
    [players, team.teamId],
  )

  const visible = filterPlayers(squad, query)

  // A search that matches nothing in this squad hides it rather than showing an empty card.
  if (query.trim() && visible.length === 0) return null

  async function shareTeam() {
    const outcome = await share({
      title: team.name,
      text: `${team.name} — ${squad.length} signed in ${auctionName}`,
      url: teamUrl(publicId, team.teamId),
    })

    if (outcome === 'copied') push(`${team.name} link copied.`, 'success')
    if (outcome === 'failed') push('Could not share that link.', 'warn')
  }

  return (
    <section
      id={`team-${team.teamId}`}
      className={`glass p-4 ${isYours ? 'ring-1 ring-brand-400/60' : ''}`}
    >
      <header className="flex flex-wrap items-center gap-3">
        <span
          aria-hidden
          className="grid size-10 shrink-0 place-items-center rounded-full text-sm font-black text-white"
          style={{
            background: `linear-gradient(140deg, hsl(${hueFor(team.name)} 72% 52%), hsl(${
              (hueFor(team.name) + 48) % 360
            } 68% 40%))`,
          }}
        >
          {(team.shortName ?? team.name).slice(0, 2).toUpperCase()}
        </span>

        <div className="min-w-0 flex-1">
          <p className="truncate font-bold text-white">
            {team.name}
            {isYours && <span className="ml-2 text-xs font-normal text-brand-300">you</span>}
          </p>
          <p className="nums text-xs text-slate-400">
            {squad.length} players · spent{' '}
            <span title={moneyFull(team.totalSpent)}>{money(team.totalSpent)}</span>
          </p>
        </div>

        <button type="button" className="btn btn-sm" onClick={() => void shareTeam()}>
          Share
        </button>
        <button
          type="button"
          className="btn btn-sm"
          onClick={() => setOpen((o) => !o)}
          aria-expanded={open}
        >
          {open ? 'Hide' : 'Show'}
        </button>
      </header>

      <dl className="mt-3 grid grid-cols-3 gap-2">
        <div className="glass-inset px-3 py-2">
          <dt className="text-[0.62rem] uppercase tracking-[0.1em] text-slate-400">Need</dt>
          <dd className="nums text-base font-bold text-warn-400">
            {team.slotsRemaining > 0 ? `${team.slotsRemaining} more` : 'Complete'}
          </dd>
        </div>
        <div className="glass-inset px-3 py-2">
          <dt className="text-[0.62rem] uppercase tracking-[0.1em] text-slate-400">Max bid</dt>
          <dd className="nums text-base font-bold text-slate-100" title={moneyFull(team.spendablePurse)}>
            {money(team.spendablePurse)}
          </dd>
        </div>
        <div className="glass-inset px-3 py-2">
          <dt className="text-[0.62rem] uppercase tracking-[0.1em] text-slate-400">Balance</dt>
          <dd className="nums text-base font-bold text-live-400" title={moneyFull(team.purse)}>
            {money(team.purse)}
          </dd>
        </div>
      </dl>

      {open && (
        <ul className="mt-3 space-y-1.5">
          {visible.length === 0 ? (
            <li>
              <EmptyState>No signings yet.</EmptyState>
            </li>
          ) : (
            visible.map((player) => (
              <li key={player.id} className="glass-inset flex items-center gap-3 px-3 py-2">
                <span className="nums w-10 shrink-0 text-xs text-slate-500">
                  {squadNumber(player)}
                </span>
                <PlayerPhoto player={player} size="sm" />
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-semibold text-slate-100">{player.name}</p>
                  <p className="truncate text-xs text-slate-400">{player.role ?? player.sportName}</p>
                </div>
                <p
                  className="nums shrink-0 text-sm font-bold text-live-400"
                  title={moneyFull(player.soldPrice)}
                >
                  {money(player.soldPrice)}
                </p>
              </li>
            ))
          )}
        </ul>
      )}
    </section>
  )
}

function PlayerList({ players, emptyLabel }: { players: Player[]; emptyLabel: string }) {
  if (players.length === 0) {
    return (
      <Panel>
        <EmptyState>{emptyLabel}</EmptyState>
      </Panel>
    )
  }

  return (
    <Panel>
      <ul className="space-y-1.5">
        {players.map((player) => (
          <li key={player.id} className="glass-inset flex items-center gap-3 px-3 py-2">
            <span className="nums w-10 shrink-0 text-xs text-slate-500">{squadNumber(player)}</span>
            <PlayerPhoto player={player} size="sm" />
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-semibold text-slate-100">{player.name}</p>
              <p className="truncate text-xs text-slate-400">
                {[player.role, player.country].filter(Boolean).join(' · ') || player.sportName}
              </p>
            </div>
            <p className="nums shrink-0 text-sm text-slate-300" title={moneyFull(player.basePrice)}>
              {money(player.basePrice)}
            </p>
            <PlayerStatusPill status={player.status} />
          </li>
        ))}
      </ul>
    </Panel>
  )
}

/** Matches on name, role, country or the printed squad number, with or without the `#`. */
function filterPlayers(players: Player[], query: string): Player[] {
  const needle = query.trim().toLowerCase().replace(/^#/, '')
  if (!needle) return players

  return players.filter(
    (player) =>
      player.name.toLowerCase().includes(needle) ||
      (player.role ?? '').toLowerCase().includes(needle) ||
      (player.country ?? '').toLowerCase().includes(needle) ||
      squadNumber(player).slice(1) === needle,
  )
}

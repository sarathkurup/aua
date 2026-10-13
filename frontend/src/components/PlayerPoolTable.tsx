import { useMemo, useState } from 'react'
import type { Player, PlayerStatus } from '../types'
import { money, moneyFull } from '../lib/format'
import { EmptyState, GenderTag, Panel, PlayerStatusPill } from './primitives'
import { PlayerPhoto } from './PlayerPhoto'

interface PlayerPoolTableProps {
  players: Player[]
  title?: string
  /** Admin only: bring this player up for bidding. */
  onCall?: (player: Player) => void
  onEdit?: (player: Player) => void
  onDelete?: (player: Player) => void
  /** False while another lot is unresolved or the auction is not live. */
  canCall?: boolean
  busyId?: number | null
}

const FILTERS: Array<{ key: PlayerStatus | 'All' | 'Women'; label: string }> = [
  { key: 'All', label: 'All' },
  { key: 'Available', label: 'In pool' },
  { key: 'Women', label: 'Women' },
  { key: 'Sold', label: 'Sold' },
  { key: 'Unsold', label: 'Unsold' },
]

export function PlayerPoolTable({
  players,
  title = 'Player pool',
  onCall,
  onEdit,
  onDelete,
  canCall = false,
  busyId,
}: PlayerPoolTableProps) {
  const [filter, setFilter] = useState<PlayerStatus | 'All' | 'Women'>('All')
  const [query, setQuery] = useState('')

  const visible = useMemo(() => {
    const needle = query.trim().toLowerCase()
    return players.filter((player) => {
      if (filter === 'Women') {
        if (player.gender !== 'Female') return false
      } else if (filter !== 'All' && player.status !== filter) {
        return false
      }

      if (!needle) return true
      return (
        player.name.toLowerCase().includes(needle) ||
        (player.role ?? '').toLowerCase().includes(needle) ||
        (player.country ?? '').toLowerCase().includes(needle) ||
        Object.values(player.skills).some((value) => value.toLowerCase().includes(needle))
      )
    })
  }, [players, filter, query])

  const hasActions = Boolean(onCall || onEdit || onDelete)

  return (
    <Panel
      title={`${title} (${visible.length})`}
      action={
        <div className="flex flex-wrap items-center gap-2">
          <input
            type="search"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            placeholder="Search players"
            aria-label="Search players"
            className="field max-w-[10rem] py-1.5 text-xs"
          />
          <div className="flex flex-wrap gap-1" role="group" aria-label="Filter by status">
            {FILTERS.map((option) => (
              <button
                key={option.key}
                type="button"
                onClick={() => setFilter(option.key)}
                className={`btn btn-sm ${filter === option.key ? 'btn-primary' : ''}`}
                aria-pressed={filter === option.key}
              >
                {option.label}
              </button>
            ))}
          </div>
        </div>
      }
    >
      {visible.length === 0 ? (
        <EmptyState>No players match this view.</EmptyState>
      ) : (
        <>
          {/* Desktop: full table. */}
          <div className="hidden overflow-x-auto md:block">
            <table className="w-full min-w-[48rem] border-collapse text-sm">
              <thead>
                <tr className="border-b border-white/10 text-left">
                  {['Lot', 'Player', 'Attributes', 'Base', 'Status', 'Result'].map((heading) => (
                    <th
                      key={heading}
                      scope="col"
                      className="px-2 py-2 text-[0.65rem] uppercase tracking-[0.1em] text-slate-400"
                    >
                      {heading}
                    </th>
                  ))}
                  {hasActions && (
                    <th
                      scope="col"
                      className="px-2 py-2 text-right text-[0.65rem] uppercase tracking-[0.1em] text-slate-400"
                    >
                      Actions
                    </th>
                  )}
                </tr>
              </thead>
              <tbody>
                {visible.map((player) => (
                  <tr key={player.id} className="border-b border-white/6 last:border-0 hover:bg-white/4">
                    <td className="nums px-2 py-2.5 text-slate-400">{player.lotOrder}</td>
                    <td className="px-2 py-2.5">
                      <div className="flex items-center gap-2.5">
                        <PlayerPhoto player={player} size="sm" zoom />
                        <div className="min-w-0">
                          <p className="flex items-center gap-1.5 truncate font-semibold text-slate-100">
                            {player.name}
                            <GenderTag gender={player.gender} />
                          </p>
                          <p className="truncate text-xs text-slate-400">
                            {[player.role, player.country].filter(Boolean).join(' · ') || '—'}
                          </p>
                        </div>
                      </div>
                    </td>
                    <td className="max-w-[14rem] px-2 py-2.5 text-xs text-slate-400">
                      <span className="line-clamp-2">
                        {Object.entries(player.skills)
                          .filter(([, value]) => value && value !== '—')
                          .map(([, value]) => value)
                          .join(' · ') || '—'}
                      </span>
                    </td>
                    <td className="nums px-2 py-2.5 text-slate-200" title={moneyFull(player.basePrice)}>
                      {money(player.basePrice)}
                    </td>
                    <td className="px-2 py-2.5">
                      <PlayerStatusPill status={player.status} />
                    </td>
                    <td className="px-2 py-2.5">
                      {player.status === 'Sold' ? (
                        <div>
                          <p className="nums font-semibold text-live-400" title={moneyFull(player.soldPrice)}>
                            {money(player.soldPrice)}
                          </p>
                          <p className="truncate text-xs text-slate-400">{player.soldToTeamName}</p>
                        </div>
                      ) : (
                        <span className="text-slate-500">—</span>
                      )}
                    </td>
                    {hasActions && (
                      <td className="px-2 py-2.5">
                        <div className="flex justify-end gap-1.5">
                          <PlayerActions
                            player={player}
                            onCall={onCall}
                            onEdit={onEdit}
                            onDelete={onDelete}
                            canCall={canCall}
                            busy={busyId === player.id}
                          />
                        </div>
                      </td>
                    )}
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {/* Mobile: one card per player — a 7-column table does not fit a phone. */}
          <ul className="space-y-2 md:hidden">
            {visible.map((player) => (
              <li key={player.id} className="glass-inset p-3">
                <div className="flex items-start gap-3">
                  <PlayerPhoto player={player} size="md" zoom />
                  <div className="min-w-0 flex-1">
                    <div className="flex items-start justify-between gap-2">
                      <p className="flex items-center gap-1.5 truncate font-semibold text-slate-100">
                        {player.name}
                        <GenderTag gender={player.gender} />
                      </p>
                      <PlayerStatusPill status={player.status} />
                    </div>
                    <p className="truncate text-xs text-slate-400">
                      Lot {player.lotOrder} ·{' '}
                      {[player.role, player.country].filter(Boolean).join(' · ') || player.sportName}
                    </p>
                    <p className="nums mt-1 text-sm text-slate-200">
                      Base {money(player.basePrice)}
                      {player.status === 'Sold' && (
                        <span className="text-live-400">
                          {' '}
                          → {money(player.soldPrice)} ({player.soldToTeamName})
                        </span>
                      )}
                    </p>
                    {hasActions && (
                      <div className="mt-2 flex flex-wrap gap-1.5">
                        <PlayerActions
                          player={player}
                          onCall={onCall}
                          onEdit={onEdit}
                          onDelete={onDelete}
                          canCall={canCall}
                          busy={busyId === player.id}
                        />
                      </div>
                    )}
                  </div>
                </div>
              </li>
            ))}
          </ul>
        </>
      )}
    </Panel>
  )
}

function PlayerActions({
  player,
  onCall,
  onEdit,
  onDelete,
  canCall,
  busy,
}: {
  player: Player
  onCall?: (player: Player) => void
  onEdit?: (player: Player) => void
  onDelete?: (player: Player) => void
  canCall: boolean
  busy: boolean
}) {
  const callable = player.status === 'Available' || player.status === 'Unsold'
  const editable = player.status === 'Available' || player.status === 'Unsold'

  return (
    <>
      {onCall && (
        <button
          type="button"
          className="btn btn-sm btn-live"
          disabled={!canCall || !callable || busy}
          onClick={() => onCall(player)}
          title={canCall ? `Call ${player.name}` : 'Resolve the current lot first'}
        >
          {busy ? 'Calling…' : 'Call'}
        </button>
      )}
      {onEdit && (
        <button
          type="button"
          className="btn btn-sm"
          disabled={!editable}
          onClick={() => onEdit(player)}
          title={editable ? `Edit ${player.name}` : 'Sold and on-block players cannot be edited'}
        >
          Edit
        </button>
      )}
      {onDelete && (
        <button
          type="button"
          className="btn btn-sm btn-danger"
          disabled={!editable}
          onClick={() => onDelete(player)}
          title={editable ? `Delete ${player.name}` : 'Sold and on-block players cannot be deleted'}
        >
          Delete
        </button>
      )}
    </>
  )
}

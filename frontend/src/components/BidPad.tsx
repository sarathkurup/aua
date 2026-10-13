import { useEffect, useMemo, useState } from 'react'
import type { AuctionState, TeamSummary } from '../types'
import { hueFor, money, moneyFull } from '../lib/format'
import { Panel } from './primitives'

/**
 * How the auctioneer records a bid.
 *
 * The room is run from the floor: someone raises a hand, and the auctioneer taps their team.
 * So the primary control is one large button per team, each showing what that team can still
 * commit, and tapping it books the next rung of the ladder against them. No dropdown to open,
 * no amount to type — at ten seconds a lot, both are too slow.
 *
 * Teams that cannot meet the ask are shown greyed rather than hidden, so the auctioneer can
 * see *why* a team has gone quiet instead of watching it vanish from the board.
 */
export function BidPad({
  state,
  onBid,
  onUndo,
  disabled,
  busyTeamId,
  undoing,
}: {
  state: AuctionState
  /** Places `amount` against `teamId`. */
  onBid: (teamId: number, amount: number) => void
  /** Takes the standing bid back off the board. */
  onUndo?: () => void
  disabled?: boolean
  busyTeamId?: number | null
  undoing?: boolean
}) {
  const ask = state.nextMinimumBid
  const leaderId = state.topBid?.teamId ?? null

  const [custom, setCustom] = useState('')
  const [customTeam, setCustomTeam] = useState<number | null>(null)

  // Re-arm the custom amount to the current ask whenever the price moves, so it is always a
  // legal starting point rather than a stale number.
  useEffect(() => setCustom(String(ask)), [ask])

  const ranked = useMemo(
    () => [...state.teams].sort((a, b) => b.spendablePurse - a.spendablePurse),
    [state.teams],
  )

  if (!state.currentPlayer) return null

  const canBid = (team: TeamSummary) =>
    !disabled && team.canBid && team.spendablePurse >= ask && team.teamId !== leaderId

  return (
    <Panel
      title="Record a bid"
      strong
      action={
        <div className="flex flex-wrap items-center gap-2">
          {/* Sits beside the ask because that is what it moves: undoing drops the price
              back a rung. Labelled with the bid it will remove — an unlabelled undo in a
              live room is worse than none, because it invites a second mistake. */}
          {onUndo && state.topBid && (
            <button
              type="button"
              className="btn btn-sm"
              onClick={onUndo}
              disabled={disabled || undoing}
              title={`Take back ${moneyFull(state.topBid.amount)} from ${state.topBid.teamName}`}
            >
              ↩ {undoing ? 'Undoing…' : `Undo ${state.topBid.teamShortName ?? state.topBid.teamName}`}
            </button>
          )}
          <p className="nums text-xs text-slate-400">
            Next: <span className="font-bold text-live-400">{money(ask)}</span>
          </p>
        </div>
      }
    >
      <div className="grid gap-2" style={{ gridTemplateColumns: 'repeat(auto-fit, minmax(11rem, 1fr))' }}>
        {ranked.map((team) => {
          const allowed = canBid(team)
          const isLeader = team.teamId === leaderId
          const hue = hueFor(team.name)

          return (
            <button
              key={team.teamId}
              type="button"
              disabled={!allowed}
              onClick={() => onBid(team.teamId, ask)}
              className={`glass-inset p-3 text-left transition-transform duration-150 ${
                allowed ? 'hover:-translate-y-0.5 hover:bg-white/10' : 'cursor-not-allowed opacity-45'
              }`}
              style={isLeader ? { boxShadow: 'inset 0 0 0 1px rgb(var(--accent) / 0.6)' } : undefined}
              title={
                isLeader
                  ? `${team.name} already holds the top bid`
                  : allowed
                    ? `Bid ${moneyFull(ask)} for ${team.name}`
                    : (team.blockedReason ?? `${team.name} can only commit ${moneyFull(team.spendablePurse)}`)
              }
            >
              <span className="flex items-center gap-2">
                <span
                  aria-hidden
                  className="size-2.5 shrink-0 rounded-full"
                  style={{ background: `hsl(${hue} 72% 58%)` }}
                />
                <span className="min-w-0 flex-1 truncate text-sm font-bold text-slate-100">
                  {team.name}
                </span>
                {isLeader && <span className="pill pill-brand">leading</span>}
              </span>

              <span className="nums mt-1.5 block text-lg font-black text-white">
                {busyTeamId === team.teamId ? '…' : money(ask)}
              </span>

              <span className="nums mt-0.5 block text-[0.68rem] text-slate-400">
                max {money(team.spendablePurse)} · {team.slotsRemaining} to fill
              </span>
            </button>
          )
        })}
      </div>

      {/* The escape hatch: a jump bid, or a correction. Rarely the fast path, so it sits
          under the buttons rather than competing with them. */}
      <div className="mt-3 flex flex-wrap items-end gap-2 border-t border-white/8 pt-3">
        <div className="min-w-[8rem] flex-1">
          <label className="field-label" htmlFor="bidpad-team">
            Custom bid — team
          </label>
          <select
            id="bidpad-team"
            className="field"
            value={customTeam ?? ''}
            onChange={(event) => setCustomTeam(Number(event.target.value) || null)}
          >
            <option value="">Choose a team</option>
            {state.teams.map((team) => (
              <option key={team.teamId} value={team.teamId}>
                {team.name}
              </option>
            ))}
          </select>
        </div>

        <div className="min-w-[8rem] flex-1">
          <label className="field-label" htmlFor="bidpad-amount">
            Amount
          </label>
          <input
            id="bidpad-amount"
            className="field nums"
            type="number"
            min={ask}
            step={1}
            value={custom}
            onChange={(event) => setCustom(event.target.value)}
          />
        </div>

        <button
          type="button"
          className="btn btn-primary"
          disabled={disabled || customTeam === null || Number(custom) < ask}
          onClick={() => {
            if (customTeam !== null) onBid(customTeam, Number(custom))
          }}
        >
          Record
        </button>
      </div>
    </Panel>
  )
}

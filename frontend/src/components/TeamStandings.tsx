import type { SquadRules, TeamMember, TeamSummary } from '../types'
import { hueFor, money, moneyFull } from '../lib/format'
import { EmptyState, Panel } from './primitives'
import { PlayerPhoto } from './PlayerPhoto'

/**
 * Purse and squad progress per team.
 *
 * In a company tournament the interesting number is not the purse — it is how close a
 * team is to breaking a rule. Slots still owed, women still owed and the purse held back
 * for them are shown inline, because that is what decides whether the next bid is legal.
 */
export function TeamStandings({
  teams,
  rules,
  highlightTeamId,
}: {
  teams: TeamSummary[]
  rules: SquadRules
  highlightTeamId?: number
}) {
  const hasSquadRules = rules.minSquadSize > 0 || rules.minWomenPerSquad > 0

  return (
    <Panel title="Teams">
      {teams.length === 0 ? (
        <EmptyState>No teams have been added to this tournament.</EmptyState>
      ) : (
        <ul className="space-y-2">
          {teams.map((team) => {
            const remaining = team.initialPurse > 0 ? team.purse / team.initialPurse : 0
            const isYou = team.teamId === highlightTeamId
            const hue = hueFor(team.name)

            return (
              <li
                key={team.teamId}
                className={`glass-inset px-3 py-2.5 ${isYou ? 'ring-1 ring-brand-400/60' : ''}`}
              >
                <div className="flex items-center gap-2">
                  <span
                    aria-hidden
                    className="size-2 shrink-0 rounded-full"
                    style={{ background: `hsl(${hue} 72% 58%)` }}
                  />
                  <p className="min-w-0 flex-1 truncate text-sm font-semibold text-slate-100">
                    {team.name}
                    {isYou && <span className="ml-1.5 text-xs font-normal text-brand-300">you</span>}
                  </p>
                  <p className="nums text-sm font-bold text-white" title={moneyFull(team.purse)}>
                    {money(team.purse)}
                  </p>
                </div>

                {/* Purse remaining, as a share of the starting budget. The paler band on
                    the right is the part reserved for slots the team still has to fill. */}
                <div
                  className="mt-2 flex h-1.5 overflow-hidden rounded-full bg-white/8"
                  role="img"
                  aria-label={`${Math.round(remaining * 100)}% of purse remaining, ${moneyFull(
                    team.reservedPurse,
                  )} reserved`}
                >
                  <div
                    className="h-full transition-[width] duration-500"
                    style={{
                      width: `${clamp(team.spendablePurse / (team.initialPurse || 1)) * 100}%`,
                      background: `linear-gradient(90deg, hsl(${hue} 72% 58%), hsl(${(hue + 40) % 360} 70% 46%))`,
                    }}
                  />
                  <div
                    className="h-full bg-white/25 transition-[width] duration-500"
                    style={{ width: `${clamp(team.reservedPurse / (team.initialPurse || 1)) * 100}%` }}
                  />
                </div>

                <RetainedStrip members={team.retained} />

                <p className="nums mt-1.5 flex flex-wrap items-center gap-x-2 gap-y-1 text-xs text-slate-400">
                  <span title={`${team.squadSize} in the squad including retained members`}>
                    {team.playersWon} {team.playersWon === 1 ? 'player' : 'players'}
                    {team.retained.length > 0 && (
                      <span className="text-slate-500"> +{team.retained.length}</span>
                    )}
                  </span>
                  <span className="text-slate-600">·</span>
                  <span title={moneyFull(team.totalSpent)}>spent {money(team.totalSpent)}</span>

                  {hasSquadRules && team.slotsRemaining > 0 && (
                    <>
                      <span className="text-slate-600">·</span>
                      <span className="text-warn-400">{team.slotsRemaining} slot{team.slotsRemaining === 1 ? '' : 's'} to fill</span>
                    </>
                  )}

                  {team.womenRequired > 0 && (
                    <span className="pill pill-pink" title="Required by this tournament's rules">
                      needs {team.womenRequired} W
                    </span>
                  )}

                  {team.womenRequired === 0 && rules.minWomenPerSquad > 0 && (
                    <span className="pill pill-live" title="Participation requirement met">
                      W ✓
                    </span>
                  )}
                </p>

                {team.blockedReason ? (
                  <p className="mt-1 text-xs text-danger-400">{team.blockedReason}</p>
                ) : (
                  team.reservedPurse > 0 && (
                    <p className="nums mt-1 text-xs text-slate-500">
                      can bid up to{' '}
                      <span className="text-slate-300" title={moneyFull(team.spendablePurse)}>
                        {money(team.spendablePurse)}
                      </span>{' '}
                      · {money(team.reservedPurse)} reserved
                    </p>
                  )
                )}
              </li>
            )
          })}
        </ul>
      )}
    </Panel>
  )
}

/**
 * The permanent members every squad starts with — the owner and the marquee players.
 *
 * Shown as faces rather than a list of names: in a room, the point of this strip is that
 * someone glancing at the screen can tell which three people were never up for sale. Hovering
 * any of them lifts the full picture out.
 */
function RetainedStrip({ members }: { members: TeamMember[] }) {
  if (members.length === 0) return null

  return (
    <ul className="mt-2 flex flex-wrap items-center gap-1.5">
      {members.map((member) => (
        <li key={member.playerId} className="flex items-center gap-1.5">
          <PlayerPhoto player={member} size="sm" zoom />
          <span className="text-[0.68rem] leading-tight text-slate-400">
            <span className="block max-w-[7rem] truncate text-slate-300">{member.name}</span>
            <span className="uppercase tracking-[0.08em]">
              {member.teamRole === 'Owner' ? 'owner' : 'marquee'}
            </span>
          </span>
        </li>
      ))}
    </ul>
  )
}

function clamp(value: number): number {
  return Math.max(0, Math.min(1, value))
}

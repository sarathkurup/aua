import { useRef } from 'react'
import type { Auction, Player, SquadRules, TeamSummary } from '../types'
import { hueFor, money, moneyFull, squadNumber } from '../lib/format'
import { share, teamUrl } from '../lib/share'
import { EmptyState, Panel } from './primitives'
import { PlayerPhoto } from './PlayerPhoto'
import { useToast } from './Toasts'

/**
 * One column per team, each holding the squad it has bought.
 *
 * This is the board the room actually watches: below the lot, so a sold player travels
 * downward into the column that won them. Columns are equal-width and scroll independently,
 * and the track widens on a TV rather than stretching four columns across two metres.
 */
export function TeamColumns({
  teams,
  players,
  rules,
  auction,
  highlightTeamId,
  flashTeamId,
  registerColumn,
}: {
  teams: TeamSummary[]
  players: Player[]
  rules: SquadRules
  /** Needed to build a shareable deep link to a squad. */
  auction?: Auction
  /** Ring the signed-in owner's own team. */
  highlightTeamId?: number
  /** Briefly lit after a sale lands, so the eye is told where the player went. */
  flashTeamId?: number | null
  /** Gives the parent each column's element, which the sold animation flies towards. */
  registerColumn?: (teamId: number, element: HTMLElement | null) => void
}) {
  const { push } = useToast()
  const elements = useRef(new Map<number, HTMLElement>())

  if (teams.length === 0) {
    return (
      <Panel title="Teams">
        <EmptyState>No teams have been added to this tournament yet.</EmptyState>
      </Panel>
    )
  }

  /** Blows one squad up to the whole screen — the view to leave on a hall display. */
  async function goFullscreen(teamId: number) {
    const element = elements.current.get(teamId)
    if (!element) return

    try {
      if (document.fullscreenElement) await document.exitFullscreen()
      else await element.requestFullscreen()
    } catch {
      push('This browser would not go fullscreen.', 'warn')
    }
  }

  async function shareTeam(team: TeamSummary) {
    if (!auction) return

    const outcome = await share({
      title: team.name,
      text: `${team.name} — ${team.playersWon} signed in ${auction.name}`,
      url: teamUrl(auction.publicId, team.teamId),
    })

    if (outcome === 'copied') push(`${team.name} link copied.`, 'success')
    if (outcome === 'failed') push('Could not share that link.', 'warn')
  }

  return (
    <section className="space-y-3">
      <h2 className="section-title">Squads</h2>

      <div
        className="grid gap-3"
        style={{ gridTemplateColumns: 'repeat(auto-fit, minmax(15rem, 1fr))' }}
      >
        {teams.map((team) => {
          const squad = players
            .filter((player) => player.soldToTeamId === team.teamId && player.status === 'Sold')
            .sort((a, b) => (b.soldPrice ?? 0) - (a.soldPrice ?? 0))

          const hue = hueFor(team.name)
          const isYours = team.teamId === highlightTeamId
          const isFlashing = team.teamId === flashTeamId

          return (
            <div
              key={team.teamId}
              ref={(element) => {
                if (element) elements.current.set(team.teamId, element)
                else elements.current.delete(team.teamId)
                registerColumn?.(team.teamId, element)
              }}
              id={`team-${team.teamId}`}
              className={`glass flex flex-col p-3 transition-all duration-300 ${
                isFlashing ? 'animate-team-flash' : ''
              } ${isYours ? 'ring-1 ring-brand-400/60' : ''}`}
            >
              <header className="flex items-start gap-2">
                <span
                  aria-hidden
                  className="mt-1 size-2.5 shrink-0 rounded-full"
                  style={{ background: `hsl(${hue} 72% 58%)` }}
                />
                <div className="min-w-0 flex-1">
                  <p className="truncate text-sm font-bold text-slate-100">
                    {team.name}
                    {isYours && <span className="ml-1.5 text-xs font-normal text-brand-300">you</span>}
                  </p>
                  <p className="nums text-xs text-slate-400" title={moneyFull(team.purse)}>
                    {money(team.purse)} left · max {money(team.spendablePurse)}
                  </p>
                </div>
                <p className="nums shrink-0 text-lg font-black text-white">{squad.length}</p>
              </header>

              {/* What this team still owes the rule book, which is what the auctioneer
                  is tracking as the pool runs down. */}
              {(team.slotsRemaining > 0 || team.womenRequired > 0) && (
                <p className="mt-2 flex flex-wrap gap-1.5">
                  {team.slotsRemaining > 0 && (
                    <span className="pill pill-muted">{team.slotsRemaining} to fill</span>
                  )}
                  {team.womenRequired > 0 && (
                    <span className="pill pill-brand">needs {team.womenRequired} W</span>
                  )}
                </p>
              )}

              {!team.canBid && team.blockedReason && (
                <p className="mt-2 text-xs text-warn-400">{team.blockedReason}</p>
              )}

              <ul className="mt-3 flex-1 space-y-1.5 overflow-y-auto" style={{ maxHeight: '18rem' }}>
                {squad.length === 0 ? (
                  <li className="rounded-lg border border-dashed border-white/12 px-3 py-4 text-center text-xs text-slate-500">
                    No signings yet
                  </li>
                ) : (
                  squad.map((player) => (
                    <li
                      key={player.id}
                      className="glass-inset animate-rise-in flex items-center gap-2 px-2 py-1.5"
                    >
                      <PlayerPhoto player={player} size="sm" />
                      <div className="min-w-0 flex-1">
                        <p className="truncate text-xs font-semibold text-slate-100">
                          <span className="nums text-slate-500">{squadNumber(player)}</span>{' '}
                          {player.name}
                        </p>
                        <p className="truncate text-[0.65rem] text-slate-400">
                          {player.role ?? player.sportName}
                        </p>
                      </div>
                      <p
                        className="nums shrink-0 text-xs font-bold text-live-400"
                        title={moneyFull(player.soldPrice)}
                      >
                        {money(player.soldPrice)}
                      </p>
                    </li>
                  ))
                )}
              </ul>

              {rules.minSquadSize > 0 && (
                <div
                  className="mt-2 h-1 overflow-hidden rounded-full bg-white/8"
                  role="img"
                  aria-label={`${squad.length} of ${rules.minSquadSize} minimum signings`}
                >
                  <div
                    className="h-full rounded-full transition-[width] duration-500"
                    style={{
                      width: `${Math.min(100, (squad.length / rules.minSquadSize) * 100)}%`,
                      background: `hsl(${hue} 72% 58%)`,
                    }}
                  />
                </div>
              )}

              {auction && (
                <div className="mt-2 flex justify-end gap-1">
                  <button
                    type="button"
                    className="btn btn-sm"
                    onClick={() => void goFullscreen(team.teamId)}
                    title={`Show ${team.name} fullscreen`}
                    aria-label={`Show ${team.name} fullscreen`}
                  >
                    ⛶
                  </button>
                  <button
                    type="button"
                    className="btn btn-sm"
                    onClick={() => void shareTeam(team)}
                    title={`Share ${team.name}`}
                    aria-label={`Share ${team.name}`}
                  >
                    ↗
                  </button>
                </div>
              )}
            </div>
          )
        })}
      </div>
    </section>
  )
}

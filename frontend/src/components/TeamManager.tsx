import { useState } from 'react'
import type { FormEvent } from 'react'
import type { Gender, OwnerOption, SaveTeamMemberRequest, TeamSummary } from '../types'
import { money, moneyFull } from '../lib/format'
import { EmptyState, Panel } from './primitives'
import { PlayerPhoto } from './PlayerPhoto'

/** Teams of one tournament, with the account that bids for each. */
export function TeamManager({
  teams,
  owners,
  onAdd,
  onUpdate,
  onRemove,
  onAddMember,
  onRemoveMember,
  disabled,
}: {
  teams: TeamSummary[]
  owners: OwnerOption[]
  onAdd: (name: string, shortName: string | null, ownerUserId: number | null) => Promise<void>
  onUpdate: (teamId: number, ownerUserId: number | null) => Promise<void>
  onRemove: (team: TeamSummary) => void
  /** Adds the owner or a marquee player — permanent, never auctioned. */
  onAddMember: (teamId: number, body: SaveTeamMemberRequest) => Promise<void>
  onRemoveMember: (playerId: number, name: string) => Promise<void>
  /** True while an auction is running — teams must not change mid-sale. */
  disabled: boolean
}) {
  const [name, setName] = useState('')
  const [shortName, setShortName] = useState('')
  const [ownerId, setOwnerId] = useState<number | ''>('')
  const [busy, setBusy] = useState(false)

  async function handleAdd(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    try {
      await onAdd(name.trim(), shortName.trim() || null, ownerId === '' ? null : ownerId)
      setName('')
      setShortName('')
      setOwnerId('')
    } finally {
      setBusy(false)
    }
  }

  return (
    <Panel title={`Teams (${teams.length})`}>
      {teams.length === 0 ? (
        <EmptyState>No teams yet. Add at least two so there is something to bid against.</EmptyState>
      ) : (
        <ul className="space-y-2">
          {teams.map((team) => (
            <li key={team.teamId} className="glass-inset px-3 py-2.5">
              <div className="flex flex-wrap items-center gap-2">
              <div className="min-w-[8rem] flex-1">
                <p className="truncate text-sm font-semibold text-slate-100">
                  {team.name}
                  {team.shortName && <span className="ml-1.5 text-xs text-slate-500">{team.shortName}</span>}
                </p>
                <p className="nums text-xs text-slate-400" title={moneyFull(team.purse)}>
                  {money(team.purse)} · squad {team.squadSize}
                  {team.retained.length > 0 && (
                    <span className="text-slate-500">
                      {' '}
                      ({team.playersWon} bought + {team.retained.length} retained)
                    </span>
                  )}
                  {team.womenWon > 0 && ` · ${team.womenWon} ${team.womenWon === 1 ? 'woman' : 'women'}`}
                </p>
              </div>

              <select
                className="field max-w-[11rem] py-1.5 text-xs"
                aria-label={`Owner of ${team.name}`}
                value={team.ownerUserId ?? ''}
                onChange={(event) =>
                  void onUpdate(team.teamId, event.target.value === '' ? null : Number(event.target.value))
                }
              >
                <option value="">Offline only</option>
                {owners.map((owner) => (
                  <option key={owner.id} value={owner.id}>
                    {owner.displayName}
                  </option>
                ))}
              </select>

              <button
                type="button"
                className="btn btn-sm btn-danger"
                disabled={disabled || team.playersWon > 0}
                onClick={() => onRemove(team)}
                title={
                  team.playersWon > 0
                    ? 'This team has bought players'
                    : disabled
                      ? 'The auction is running'
                      : `Remove ${team.name}`
                }
              >
                Remove
              </button>
              </div>

              <RetainedMembers
                team={team}
                onAdd={onAddMember}
                onRemove={onRemoveMember}
              />
            </li>
          ))}
        </ul>
      )}

      <form onSubmit={handleAdd} className="mt-3 flex flex-wrap items-end gap-2">
        <div className="min-w-[8rem] flex-1">
          <label className="field-label" htmlFor="team-name">
            New team
          </label>
          <input
            id="team-name"
            className="field"
            value={name}
            onChange={(event) => setName(event.target.value)}
            placeholder="Platform Engineering"
            required
          />
        </div>
        <div className="w-24">
          <label className="field-label" htmlFor="team-short">
            Tag
          </label>
          <input
            id="team-short"
            className="field"
            maxLength={8}
            value={shortName}
            onChange={(event) => setShortName(event.target.value)}
            placeholder="PLT"
          />
        </div>
        <div className="min-w-[9rem]">
          <label className="field-label" htmlFor="team-owner">
            Owner
          </label>
          <select
            id="team-owner"
            className="field"
            value={ownerId}
            onChange={(event) => setOwnerId(event.target.value === '' ? '' : Number(event.target.value))}
          >
            <option value="">Offline only</option>
            {owners.map((owner) => (
              <option key={owner.id} value={owner.id}>
                {owner.displayName}
              </option>
            ))}
          </select>
        </div>
        <button type="submit" className="btn btn-primary" disabled={busy || disabled}>
          {busy ? 'Adding…' : 'Add team'}
        </button>
      </form>

      {disabled && (
        <p className="mt-2 text-xs text-slate-500">
          The auction is running — teams cannot be added or removed until it closes.
        </p>
      )}
    </Panel>
  )
}

const GENDERS: Array<{ value: Gender; label: string }> = [
  { value: 'Male', label: 'Man' },
  { value: 'Female', label: 'Woman' },
  { value: 'Unspecified', label: 'Not recorded' },
]

/**
 * The squad members a team starts with: its owner, and its marquee players.
 *
 * They are added here rather than in the player pool because they never go up for sale —
 * putting them in the pool form would mean asking for a base price and a lot number that
 * mean nothing. They still count towards the squad rules, which is why the count beside
 * the team name includes them.
 */
function RetainedMembers({
  team,
  onAdd,
  onRemove,
}: {
  team: TeamSummary
  onAdd: (teamId: number, body: SaveTeamMemberRequest) => Promise<void>
  onRemove: (playerId: number, name: string) => Promise<void>
}) {
  const [open, setOpen] = useState(false)
  const [name, setName] = useState('')
  const [teamRole, setTeamRole] = useState<'Owner' | 'Marquee'>('Marquee')
  const [gender, setGender] = useState<Gender>('Male')
  const [role, setRole] = useState('')
  const [busy, setBusy] = useState(false)

  const hasOwner = team.retained.some((member) => member.teamRole === 'Owner')

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)
    try {
      await onAdd(team.teamId, {
        name: name.trim(),
        teamRole,
        role: role.trim() || null,
        gender,
        country: null,
        skills: {},
      })
      setName('')
      setRole('')
      setOpen(false)
    } finally {
      setBusy(false)
    }
  }

  return (
    <div className="mt-2 border-t border-white/8 pt-2">
      <div className="flex flex-wrap items-center gap-2">
        {team.retained.length === 0 ? (
          <p className="text-xs text-slate-500">No owner or marquee players yet.</p>
        ) : (
          <ul className="flex flex-wrap items-center gap-2">
            {team.retained.map((member) => (
              <li key={member.playerId} className="flex items-center gap-1.5">
                <PlayerPhoto player={member} size="sm" zoom />
                <span className="text-[0.68rem] leading-tight">
                  <span className="block max-w-[8rem] truncate text-slate-200">{member.name}</span>
                  <span className="uppercase tracking-[0.08em] text-slate-500">
                    {member.teamRole === 'Owner' ? 'owner' : 'marquee'}
                  </span>
                </span>
                <button
                  type="button"
                  className="text-xs text-slate-500 transition hover:text-danger-400"
                  aria-label={`Remove ${member.name}`}
                  title={`Remove ${member.name}`}
                  onClick={() => void onRemove(member.playerId, member.name)}
                >
                  ×
                </button>
              </li>
            ))}
          </ul>
        )}

        <button
          type="button"
          className="btn btn-sm ml-auto"
          onClick={() => {
            setTeamRole(hasOwner ? 'Marquee' : 'Owner')
            setOpen((v) => !v)
          }}
        >
          {open ? 'Cancel' : '+ Owner / marquee'}
        </button>
      </div>

      {open && (
        <form onSubmit={handleSubmit} className="mt-2 flex flex-wrap items-end gap-2">
          <div className="min-w-[9rem] flex-1">
            <label className="field-label" htmlFor={`member-name-${team.teamId}`}>
              Name
            </label>
            <input
              id={`member-name-${team.teamId}`}
              className="field"
              value={name}
              onChange={(event) => setName(event.target.value)}
              required
              autoFocus
            />
          </div>

          <div className="w-32">
            <label className="field-label" htmlFor={`member-role-${team.teamId}`}>
              Type
            </label>
            <select
              id={`member-role-${team.teamId}`}
              className="field"
              value={teamRole}
              onChange={(event) => setTeamRole(event.target.value as 'Owner' | 'Marquee')}
            >
              <option value="Owner" disabled={hasOwner}>
                Owner
              </option>
              <option value="Marquee">Marquee</option>
            </select>
          </div>

          <div className="w-32">
            <label className="field-label" htmlFor={`member-gender-${team.teamId}`}>
              Gender
            </label>
            <select
              id={`member-gender-${team.teamId}`}
              className="field"
              value={gender}
              onChange={(event) => setGender(event.target.value as Gender)}
            >
              {GENDERS.map((option) => (
                <option key={option.value} value={option.value}>
                  {option.label}
                </option>
              ))}
            </select>
          </div>

          <div className="w-36">
            <label className="field-label" htmlFor={`member-position-${team.teamId}`}>
              Position
            </label>
            <input
              id={`member-position-${team.teamId}`}
              className="field"
              value={role}
              onChange={(event) => setRole(event.target.value)}
              placeholder="Singles, doubles…"
            />
          </div>

          <button type="submit" className="btn btn-sm btn-primary" disabled={busy}>
            {busy ? 'Adding…' : 'Add'}
          </button>
        </form>
      )}
    </div>
  )
}

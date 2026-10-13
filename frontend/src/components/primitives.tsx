import type { ReactNode } from 'react'
import type { AuctionStatus, Gender, PlayerStatus, TournamentType } from '../types'
import type { HubStatus } from '../realtime/useAuctionHub'

export function Panel({
  title,
  action,
  children,
  className = '',
  strong = false,
}: {
  title?: string
  action?: ReactNode
  children: ReactNode
  className?: string
  strong?: boolean
}) {
  return (
    <section className={`${strong ? 'glass-strong' : 'glass'} p-4 sm:p-5 ${className}`}>
      {(title || action) && (
        <header className="mb-3 flex flex-wrap items-center justify-between gap-3">
          {title && <h2 className="section-title">{title}</h2>}
          {action}
        </header>
      )}
      {children}
    </section>
  )
}

const auctionTone: Record<AuctionStatus, string> = {
  Draft: 'pill-muted',
  Live: 'pill-live',
  Paused: 'pill-warn',
  Completed: 'pill-brand',
}

export function AuctionStatusPill({ status }: { status: AuctionStatus }) {
  return (
    <span className={`pill ${auctionTone[status]}`}>
      {status === 'Live' && (
        <span className="inline-block size-1.5 rounded-full bg-current animate-pulse-ring" aria-hidden />
      )}
      {status}
    </span>
  )
}

const playerTone: Record<PlayerStatus, string> = {
  Available: 'pill-muted',
  OnBlock: 'pill-brand',
  Sold: 'pill-live',
  Unsold: 'pill-danger',
  Retained: 'pill-info',
}

const playerLabel: Record<PlayerStatus, string> = {
  Available: 'In pool',
  OnBlock: 'On block',
  Sold: 'Sold',
  Unsold: 'Unsold',
  // Never auctioned: the owner and marquee players a squad starts with.
  Retained: 'Retained',
}

export function PlayerStatusPill({ status }: { status: PlayerStatus }) {
  return <span className={`pill ${playerTone[status]}`}>{playerLabel[status]}</span>
}

/** Corporate tournaments carry squad rules; the badge is the cue to expect them. */
export function TournamentTypePill({ type }: { type: TournamentType }) {
  return (
    <span className={`pill ${type === 'Corporate' ? 'pill-info' : 'pill-muted'}`}>
      {type === 'Corporate' ? 'Company league' : 'Professional'}
    </span>
  )
}

/** Shown only where a squad rule depends on it, so it never becomes decoration. */
export function GenderTag({ gender }: { gender: Gender }) {
  if (gender === 'Unspecified') return null
  return (
    <span className={`pill ${gender === 'Female' ? 'pill-pink' : 'pill-muted'}`}>
      {gender === 'Female' ? 'W' : 'M'}
    </span>
  )
}

const hubCopy: Record<HubStatus, { label: string; className: string }> = {
  idle: { label: 'Idle', className: 'pill-muted' },
  connecting: { label: 'Connecting', className: 'pill-warn' },
  connected: { label: 'Live link', className: 'pill-live' },
  reconnecting: { label: 'Reconnecting', className: 'pill-warn' },
  disconnected: { label: 'Offline', className: 'pill-danger' },
}

export function ConnectionBadge({ status }: { status: HubStatus }) {
  const { label, className } = hubCopy[status]
  return (
    <span className={`pill ${className}`} title={`Real-time connection: ${status}`}>
      <span className="inline-block size-1.5 rounded-full bg-current" aria-hidden />
      {label}
    </span>
  )
}

export function EmptyState({ children }: { children: ReactNode }) {
  return (
    <p className="rounded-xl border border-dashed border-white/12 px-4 py-6 text-center text-sm text-slate-400">
      {children}
    </p>
  )
}

export function Stat({
  label,
  value,
  hint,
  tone = 'default',
}: {
  label: string
  value: ReactNode
  hint?: string
  tone?: 'default' | 'live' | 'warn' | 'danger'
}) {
  const valueTone = {
    default: 'text-white',
    live: 'text-live-400',
    warn: 'text-warn-400',
    danger: 'text-danger-400',
  }[tone]

  return (
    <div className="glass-inset px-3 py-2.5">
      <p className="text-[0.65rem] font-semibold uppercase tracking-[0.1em] text-slate-400">{label}</p>
      <p className={`nums mt-0.5 text-lg font-bold ${valueTone}`} title={hint}>
        {value}
      </p>
    </div>
  )
}

/** Sport-specific attributes, rendered from whatever keys the player carries. */
export function SkillList({ skills }: { skills: Record<string, string> }) {
  const entries = Object.entries(skills).filter(([, value]) => value && value !== '—')
  if (entries.length === 0) return null

  return (
    <dl className="flex flex-wrap gap-x-4 gap-y-1.5">
      {entries.map(([label, value]) => (
        <div key={label}>
          <dt className="text-[0.6rem] font-semibold uppercase tracking-[0.1em] text-slate-500">
            {label}
          </dt>
          <dd className="text-sm text-slate-200">{value}</dd>
        </div>
      ))}
    </dl>
  )
}

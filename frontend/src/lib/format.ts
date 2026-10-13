/**
 * Money display. Change CURRENCY here to re-label every amount in the app.
 * Amounts are plain numbers in the database; there is no minor-unit scaling.
 */
const CURRENCY = '₹'

const compact = new Intl.NumberFormat(undefined, {
  notation: 'compact',
  maximumFractionDigits: 2,
})

const full = new Intl.NumberFormat(undefined, { maximumFractionDigits: 0 })

/** Short form for cards and tables, e.g. "₹22.5M". */
export function money(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—'
  return `${CURRENCY}${compact.format(value)}`
}

/** Exact grouped form, for tooltips and confirmation copy. */
export function moneyFull(value: number | null | undefined): string {
  if (value === null || value === undefined) return '—'
  return `${CURRENCY}${full.format(value)}`
}

export function timeAgo(iso: string): string {
  const seconds = Math.max(0, Math.floor((Date.now() - new Date(iso).getTime()) / 1000))
  if (seconds < 10) return 'just now'
  if (seconds < 60) return `${seconds}s ago`
  const minutes = Math.floor(seconds / 60)
  if (minutes < 60) return `${minutes}m ago`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours}h ago`
  return new Date(iso).toLocaleDateString()
}

/** How long until a future instant, e.g. "in 3 days". Past instants read as "due now". */
export function timeUntil(iso: string): string {
  const seconds = Math.floor((new Date(iso).getTime() - Date.now()) / 1000)
  if (seconds <= 0) return 'due now'
  if (seconds < 3600) return `in ${Math.max(1, Math.round(seconds / 60))} min`
  if (seconds < 86_400) return `in ${Math.round(seconds / 3600)} h`

  const days = Math.round(seconds / 86_400)
  return `in ${days} ${days === 1 ? 'day' : 'days'}`
}

/** Absolute date and time in the viewer's own zone, for scheduled auctions. */
export function scheduleStamp(iso: string): string {
  return new Date(iso).toLocaleString([], {
    weekday: 'short',
    day: 'numeric',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  })
}

export function clockTime(iso: string): string {
  return new Date(iso).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', second: '2-digit' })
}

/**
 * The number to print next to a player's name. Prefers the shirt number; falls back to lot
 * order so a league that does not use squad numbers still gets a stable handle to call out.
 */
export function squadNumber(player: { jerseyNumber: number | null; lotOrder: number }): string {
  return `#${player.jerseyNumber ?? player.lotOrder}`
}

/** Two-letter monogram used in place of player photos. */
export function initials(name: string): string {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0]?.toUpperCase() ?? '')
    .join('')
}

/** Deterministic hue per name, so a team keeps the same colour everywhere. */
export function hueFor(seed: string): number {
  let hash = 0
  for (let i = 0; i < seed.length; i++) {
    hash = (hash * 31 + seed.charCodeAt(i)) % 360
  }
  return hash
}

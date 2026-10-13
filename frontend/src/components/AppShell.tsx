import type { ReactNode } from 'react'
import { Link, NavLink, useNavigate } from 'react-router'
import { useAuth } from '../auth/AuthContext'
import { ConnectionBadge } from './primitives'
import { GavelIcon, LiveIcon, SettingsIcon, SignInIcon, SignOutIcon, TeamsIcon } from './icons'
import type { HubStatus } from '../realtime/useAuctionHub'

interface Destination {
  to: string
  label: string
  icon: typeof LiveIcon
  end?: boolean
}

/**
 * Chrome shared by every screen.
 *
 * Navigation is a glass rail pinned to the left on desktop and to the bottom on
 * phones, where a vertical rail would eat the width the auction tables need.
 * Every destination uses the same button — one shape, one colour, the accent
 * reserved for whichever one is current — so the rail stays a single material.
 */
export function AppShell({
  hubStatus,
  children,
}: {
  hubStatus?: HubStatus
  children: ReactNode
}) {
  const { user, isAdmin, isOwner, logout } = useAuth()
  const navigate = useNavigate()

  const destinations: Destination[] = [
    { to: '/', label: 'Live auctions', icon: LiveIcon, end: true },
    ...(isOwner ? [{ to: '/team', label: 'My teams', icon: TeamsIcon }] : []),
    // Open to every signed-in account: anyone can run their own tournament. Which ones
    // they can change is settled inside the console, per tournament.
    ...(user ? [{ to: '/admin', label: 'Auctioneer console', icon: GavelIcon }] : []),
    { to: '/settings', label: 'Settings', icon: SettingsIcon },
  ]

  return (
    <div className="min-h-dvh sm:pl-20">
      {/* Desktop: vertical rail. */}
      <nav
        aria-label="Main"
        className="glass fixed left-3 top-3 bottom-3 z-30 hidden w-16 flex-col items-center gap-1 py-4 sm:flex"
      >
        <Link
          to="/"
          aria-label="Auction Room home"
          className="grid size-9 shrink-0 place-items-center rounded-xl text-sm font-black text-[color:var(--accent-text)] transition-transform duration-200 hover:scale-105"
          style={{ background: 'rgb(var(--accent))' }}
        >
          A
        </Link>

        <div className="my-2 h-px w-6" style={{ background: 'var(--inset-border)' }} />

        {destinations.map(({ to, label, icon: Icon, end }) => (
          <NavLink key={to} to={to} end={end} className="rail-btn" title={label} aria-label={label}>
            <Icon />
          </NavLink>
        ))}

        <div className="mt-auto flex flex-col items-center gap-2">
          {user ? (
            <button
              type="button"
              className="rail-btn"
              title={`Sign out — ${user.displayName}`}
              aria-label="Sign out"
              onClick={() => {
                logout()
                void navigate('/', { replace: true })
              }}
            >
              <SignOutIcon />
            </button>
          ) : (
            <Link to="/login" className="rail-btn" title="Sign in" aria-label="Sign in">
              <SignInIcon />
            </Link>
          )}
        </div>
      </nav>

      {/* Slim top bar: identity and connection state, not navigation. */}
      <header className="header-veil sticky top-0 z-20 px-3 pb-2 pt-3 sm:px-4">
        <div className="glass flex flex-wrap items-center gap-x-3 gap-y-2 px-3 py-2.5">
          <Link to="/" className="flex items-center gap-2 sm:hidden">
            <span
              aria-hidden
              className="grid size-7 place-items-center rounded-lg text-xs font-black text-[color:var(--accent-text)]"
              style={{ background: 'rgb(var(--accent))' }}
            >
              A
            </span>
          </Link>

          <p className="text-sm font-bold tracking-tight text-slate-100">Auction Room</p>

          <div className="ml-auto flex flex-wrap items-center gap-2">
            {hubStatus && <ConnectionBadge status={hubStatus} />}

            {user ? (
              <div className="glass-inset px-2.5 py-1.5 text-right leading-tight">
                <p className="max-w-[10rem] truncate text-xs font-semibold text-slate-100">
                  {user.displayName}
                </p>
                <p className="text-[0.7rem] text-brand-300">
                  {isAdmin
                    ? 'auctioneer'
                    : `${user.teams.length} team${user.teams.length === 1 ? '' : 's'}`}
                </p>
              </div>
            ) : (
              <Link to="/login" className="btn btn-sm btn-primary">
                Sign in
              </Link>
            )}
          </div>
        </div>
      </header>

      {/* Widens on a television so squad columns get real estate instead of whitespace. */}
      <main className="mx-auto max-w-7xl px-3 pb-24 pt-4 sm:px-4 sm:pb-8 2xl:max-w-[110rem]">
        {children}
      </main>

      {/* Phones: the same rail, laid on its side. */}
      <nav
        aria-label="Main"
        className="glass rail-row fixed inset-x-3 bottom-3 z-30 flex items-center justify-around gap-1 px-2 py-2 sm:hidden"
      >
        {destinations.map(({ to, label, icon: Icon, end }) => (
          <NavLink key={to} to={to} end={end} className="rail-btn" title={label} aria-label={label}>
            <Icon />
          </NavLink>
        ))}
        {user ? (
          <button
            type="button"
            className="rail-btn"
            aria-label="Sign out"
            onClick={() => {
              logout()
              void navigate('/', { replace: true })
            }}
          >
            <SignOutIcon />
          </button>
        ) : (
          <Link to="/login" className="rail-btn" aria-label="Sign in">
            <SignInIcon />
          </Link>
        )}
      </nav>
    </div>
  )
}

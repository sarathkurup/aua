import type { SVGProps } from 'react'

/**
 * Inline stroke icons for navigation and controls.
 *
 * Drawn on a 24px grid with a 1.6 stroke so they sit on the same optical weight
 * as the interface text. Every icon inherits `currentColor`, which is what lets
 * the rail keep one colour per state instead of one colour per destination.
 */
type IconProps = SVGProps<SVGSVGElement> & { size?: number }

function Icon({ size = 20, children, ...props }: IconProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.6}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      focusable="false"
      {...props}
    >
      {children}
    </svg>
  )
}

/** Live auctions — a broadcast signal. */
export function LiveIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <circle cx="12" cy="12" r="2.5" />
      <path d="M7.8 7.8a6 6 0 0 0 0 8.4M16.2 16.2a6 6 0 0 0 0-8.4" />
      <path d="M4.9 4.9a10 10 0 0 0 0 14.2M19.1 19.1a10 10 0 0 0 0-14.2" />
    </Icon>
  )
}

/** My teams — a group. */
export function TeamsIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M16 19v-1.5a3.5 3.5 0 0 0-3.5-3.5h-5A3.5 3.5 0 0 0 4 17.5V19" />
      <circle cx="10" cy="8" r="3.2" />
      <path d="M20 19v-1.5a3.5 3.5 0 0 0-2.6-3.4M15.4 5.2a3.2 3.2 0 0 1 0 5.6" />
    </Icon>
  )
}

/** Admin console — a gavel. */
export function GavelIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="m5 19 6-6" />
      <path d="M4 20.5h6" />
      <rect x="11.2" y="6.2" width="7.2" height="4.4" rx="1.2" transform="rotate(45 14.8 8.4)" />
      <path d="m16.6 4.6 2.8 2.8" />
    </Icon>
  )
}

/** Settings — a gear. */
export function SettingsIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <circle cx="12" cy="12" r="3" />
      <path d="M19.4 14.5a1.6 1.6 0 0 0 .3 1.8l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.6 1.6 0 0 0-2.7 1.1v.3a2 2 0 1 1-4 0v-.2a1.6 1.6 0 0 0-2.8-1.1l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.6 1.6 0 0 0-1.1-2.7H3a2 2 0 1 1 0-4h.2a1.6 1.6 0 0 0 1.1-2.8l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.6 1.6 0 0 0 2.7-1.1V3a2 2 0 1 1 4 0v.2a1.6 1.6 0 0 0 2.8 1.1l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.6 1.6 0 0 0 1.1 2.7h.3a2 2 0 1 1 0 4h-.2a1.6 1.6 0 0 0-1.4 1z" />
    </Icon>
  )
}

/** Sign in / out — a door with an arrow. */
export function SignInIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M15 3h3a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-3" />
      <path d="M10 17 5 12l5-5" />
      <path d="M5 12h11" />
    </Icon>
  )
}

export function SignOutIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M9 3H6a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h3" />
      <path d="m15 17 5-5-5-5" />
      <path d="M20 12H9" />
    </Icon>
  )
}

export function MoonIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <path d="M20 13.5A8.2 8.2 0 0 1 10.5 4a8.5 8.5 0 1 0 9.5 9.5z" />
    </Icon>
  )
}

export function SunIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <circle cx="12" cy="12" r="4" />
      <path d="M12 2.5v2M12 19.5v2M4.2 4.2l1.4 1.4M18.4 18.4l1.4 1.4M2.5 12h2M19.5 12h2M4.2 19.8l1.4-1.4M18.4 5.6l1.4-1.4" />
    </Icon>
  )
}

/** System theme — a display. */
export function DisplayIcon(props: IconProps) {
  return (
    <Icon {...props}>
      <rect x="3" y="4" width="18" height="12" rx="2" />
      <path d="M8 20h8M12 16v4" />
    </Icon>
  )
}

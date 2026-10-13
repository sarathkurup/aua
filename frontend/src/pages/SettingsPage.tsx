import { useState } from 'react'
import type { FormEvent } from 'react'
import { AppShell } from '../components/AppShell'
import { Panel } from '../components/primitives'
import { DisplayIcon, MoonIcon, SunIcon } from '../components/icons'
import { useAuth } from '../auth/AuthContext'
import { useToast } from '../components/Toasts'
import { errorMessage } from '../api/client'
import { useTheme } from '../theme/ThemeContext'
import type { ThemeMode } from '../theme/ThemeContext'
import { usePreferences } from '../settings/PreferencesContext'
import type { SaleAnimation } from '../settings/PreferencesContext'

const THEMES: Array<{
  mode: ThemeMode
  label: string
  description: string
  icon: typeof MoonIcon
}> = [
  {
    mode: 'dark',
    label: 'Dark',
    description: 'Deep glass over an aurora wash. Built for a dim auction room and long sessions.',
    icon: MoonIcon,
  },
  {
    mode: 'light',
    label: 'Bright',
    description: 'Frosted white glass on a pale wash. Readable on a projector or in daylight.',
    icon: SunIcon,
  },
  {
    mode: 'system',
    label: 'System',
    description: 'Follows the device setting and switches with it.',
    icon: DisplayIcon,
  },
]

/**
 * The caller's own profile. There is no route that takes an account id — the token
 * decides whose profile this is, so no one can reach anyone else's by editing a URL.
 */
function ProfileForm() {
  const { user, updateProfile } = useAuth()
  const { push } = useToast()

  const [displayName, setDisplayName] = useState(user?.displayName ?? '')
  const [email, setEmail] = useState(user?.email ?? '')
  const [phone, setPhone] = useState(user?.phoneNumber ?? '')
  const [busy, setBusy] = useState(false)

  async function handleSubmit(event: FormEvent) {
    event.preventDefault()
    setBusy(true)

    try {
      await updateProfile({
        displayName: displayName.trim(),
        email: email.trim() || null,
        phoneNumber: phone.trim() || null,
        avatarUrl: user?.avatarUrl ?? null,
      })
      push('Profile saved.', 'success')
    } catch (err) {
      push(errorMessage(err, 'Could not save your profile.'), 'error')
    } finally {
      setBusy(false)
    }
  }

  return (
    <form onSubmit={handleSubmit} className="space-y-3">
      <p className="section-title">Your profile</p>

      <div className="grid gap-3 sm:grid-cols-3">
        <div>
          <label className="field-label" htmlFor="profile-name">
            Display name
          </label>
          <input
            id="profile-name"
            className="field"
            value={displayName}
            onChange={(event) => setDisplayName(event.target.value)}
            maxLength={128}
            required
          />
        </div>
        <div>
          <label className="field-label" htmlFor="profile-email">
            Email
          </label>
          <input
            id="profile-email"
            className="field"
            type="email"
            value={email}
            onChange={(event) => setEmail(event.target.value)}
            placeholder="optional"
            maxLength={256}
          />
        </div>
        <div>
          <label className="field-label" htmlFor="profile-phone">
            Mobile
          </label>
          <input
            id="profile-phone"
            className="field nums"
            type="tel"
            value={phone}
            onChange={(event) => setPhone(event.target.value)}
            placeholder="+91…"
            maxLength={32}
          />
        </div>
      </div>

      <div className="flex flex-wrap items-center gap-3">
        <button type="submit" className="btn btn-sm btn-primary" disabled={busy}>
          {busy ? 'Saving…' : 'Save profile'}
        </button>
        <p className="text-xs text-slate-500">
          Your mobile number is stored for the one-time-code sign-in that will replace passwords.
          It is never shown to other people.
        </p>
      </div>
    </form>
  )
}

const SALE_ANIMATIONS: Array<{ value: SaleAnimation; label: string; description: string }> = [
  {
    value: 'flight',
    label: 'Card flies',
    description:
      "The player's card travels into the team that bought them. Best on a laptop, where the lot and the squads are both on screen.",
  },
  {
    value: 'overlay',
    label: 'Full screen',
    description:
      'A large SOLD card over the whole room. Best on a projector, where a travelling card is lost.',
  },
  {
    value: 'none',
    label: 'Neither',
    description: 'The squad column simply updates. Quietest option.',
  },
]

export function SettingsPage() {
  const { mode, resolved, setMode } = useTheme()
  const { user, isAdmin } = useAuth()
  const { sound, setSound, saleAnimation, setSaleAnimation } = usePreferences()

  return (
    <AppShell>
      <div className="space-y-4">
        <div>
          <h1 className="text-2xl font-black tracking-tight text-slate-100">Settings</h1>
          <p className="mt-1 text-sm text-slate-400">
            Saved on this device. Spectators get them too — no account needed.
          </p>
        </div>

        <Panel title="Appearance" strong>
          <div className="grid gap-3 sm:grid-cols-3">
            {THEMES.map(({ mode: option, label, description, icon: Icon }) => {
              const active = mode === option

              return (
                <button
                  key={option}
                  type="button"
                  onClick={() => setMode(option)}
                  aria-pressed={active}
                  className={`glass-inset animate-rise-in p-4 text-left transition-transform duration-200 hover:-translate-y-0.5 ${
                    active ? 'ring-1' : ''
                  }`}
                  style={active ? { boxShadow: 'inset 0 0 0 1px rgb(var(--accent) / 0.55)' } : undefined}
                >
                  <span
                    className="grid size-9 place-items-center rounded-xl"
                    style={{
                      background: active ? 'rgb(var(--accent))' : 'var(--inset-bg)',
                      color: active ? 'var(--accent-text)' : 'inherit',
                    }}
                  >
                    <Icon />
                  </span>

                  <p className="mt-3 flex items-center gap-2 text-sm font-semibold text-slate-100">
                    {label}
                    {active && <span className="pill pill-brand">on</span>}
                  </p>
                  <p className="mt-1 text-xs leading-relaxed text-slate-400">{description}</p>
                </button>
              )
            })}
          </div>

          <p className="mt-3 text-xs text-slate-500">
            Currently showing the <span className="text-slate-300">{resolved}</span> theme
            {mode === 'system' && ' — following your device'}.
          </p>
        </Panel>

        <Panel title="The auction room">
          <div className="space-y-4">
            <div>
              <p className="field-label">Sound</p>
              <div className="flex flex-wrap items-center gap-3">
                <button
                  type="button"
                  className={`btn btn-sm ${sound ? 'btn-live' : ''}`}
                  onClick={() => void setSound(!sound)}
                  aria-pressed={sound}
                >
                  <span aria-hidden>{sound ? '🔊' : '🔇'}</span>
                  {sound ? 'On' : 'Off'}
                </button>
                <p className="min-w-0 flex-1 text-xs leading-relaxed text-slate-400">
                  A tone on every bid, rising as the price climbs, and a flourish when a lot is
                  sold. Browsers only allow audio after a click, so this cannot be on by
                  default and has to be switched on again in each new tab.
                </p>
              </div>
            </div>

            <div>
              <p className="field-label">When a player is sold</p>
              <div className="grid gap-2 sm:grid-cols-3">
                {SALE_ANIMATIONS.map((option) => {
                  const active = saleAnimation === option.value

                  return (
                    <button
                      key={option.value}
                      type="button"
                      onClick={() => setSaleAnimation(option.value)}
                      aria-pressed={active}
                      className="glass-inset p-3 text-left transition-transform duration-200 hover:-translate-y-0.5"
                      style={
                        active ? { boxShadow: 'inset 0 0 0 1px rgb(var(--accent) / 0.55)' } : undefined
                      }
                    >
                      <p className="flex items-center gap-2 text-sm font-semibold text-slate-100">
                        {option.label}
                        {active && <span className="pill pill-brand">on</span>}
                      </p>
                      <p className="mt-1 text-xs leading-relaxed text-slate-400">
                        {option.description}
                      </p>
                    </button>
                  )
                })}
              </div>
            </div>
          </div>
        </Panel>

        {/* A live sample so the choice can be judged without leaving the page. */}
        <Panel title="Preview">
          <div className="flex flex-wrap items-center gap-2">
            <button type="button" className="btn btn-primary">
              Primary
            </button>
            <button type="button" className="btn">
              Secondary
            </button>
            <button type="button" className="btn btn-live">
              Sell
            </button>
            <button type="button" className="btn btn-warn">
              Pass
            </button>
            <button type="button" className="btn btn-danger">
              End
            </button>
          </div>

          <div className="mt-3 flex flex-wrap gap-2">
            <span className="pill pill-live">live</span>
            <span className="pill pill-warn">paused</span>
            <span className="pill pill-danger">unsold</span>
            <span className="pill pill-brand">on block</span>
            <span className="pill pill-info">company league</span>
            <span className="pill pill-muted">in pool</span>
          </div>

          <div className="glass-inset mt-3 px-4 py-3">
            <p className="text-[0.65rem] font-semibold uppercase tracking-[0.12em] text-slate-400">
              Current bid
            </p>
            <p className="nums mt-1 text-4xl font-black text-white">₹22.5M</p>
            <p className="mt-1 text-sm text-slate-300">
              <span className="font-semibold text-brand-300">Coastal Titans</span>{' '}
              <span className="text-slate-500">leads</span>
            </p>
          </div>
        </Panel>

        <Panel title="Account">
          {user ? (
            <>
              <dl className="grid gap-3 sm:grid-cols-3">
                <div>
                  <dt className="field-label">Username</dt>
                  <dd className="text-sm text-slate-100">{user.username}</dd>
                </div>
                <div>
                  <dt className="field-label">Role</dt>
                  <dd className="text-sm text-slate-100">
                    {isAdmin ? 'Site admin' : 'Team owner'}
                  </dd>
                </div>
                <div>
                  <dt className="field-label">Teams</dt>
                  <dd className="text-sm text-slate-100">
                    {user.teams.length === 0
                      ? 'None assigned'
                      : user.teams.map((team) => team.teamName).join(', ')}
                  </dd>
                </div>
              </dl>

              <div className="mt-4 border-t border-white/8 pt-4">
                <ProfileForm />
              </div>
            </>
          ) : (
            <p className="text-sm text-slate-400">
              Not signed in. You can watch every auction without an account — sign in only to bid or
              to run a sale.
            </p>
          )}
        </Panel>
      </div>
    </AppShell>
  )
}

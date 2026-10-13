# Auction Room — web client

React 19 + Vite + Tailwind v4. See the [root README](../README.md) for how to run the whole app.

```bash
npm run dev      # dev server on :5173, proxying /api, /hubs and /media to the API on :5180
npm run build    # type-check and bundle to dist/
npm run lint     # oxlint
```

## Layout

| Path | What lives there |
| --- | --- |
| `src/api` | axios instance, token storage, typed endpoint wrappers |
| `src/auth` | `AuthProvider` — optional sign-in; a missing token is a normal state, not an error |
| `src/realtime` | `useAuctionHub` / `useLobbyHub` (SignalR) and `useAuctionRoom` (room state + pool) |
| `src/components` | `AuctionRoom` and the panels it composes; forms for tournaments, teams and players |
| `src/pages` | `LobbyPage` (public), `ViewerPage` (public room), `TeamPage`, `AdminPage`, `SettingsPage`, `LoginPage` |
| `src/theme` | `ThemeProvider` — dark / bright / system, stamped as `data-theme` on `<html>` |

## Four things worth knowing

**The server sends whole state, not deltas.** Every change broadcasts the complete room
payload, so a client that missed events while offline converges the moment it rejoins — there
is no event replay to get wrong.

**Bids carry a client request id.** A bid that fails with no response is retried once with the
same id; the server returns the original bid instead of recording a second one.

**One room component, three screens.** `AuctionRoom` renders the lot, feed, standings and pool;
the viewer passes no controls, the owner passes a bid panel, the admin passes the hammer and
pool actions.

**Photos are pre-sized.** `PlayerPhoto` always renders a fixed box and picks the 96px or 480px
rendition by size, and `usePhotoPreload` warms the next lots — so a called player appears
without a reflow or a spinner.

## Theming

Two themes, switched from `/settings` and stored per device. `ThemeProvider` stamps
`data-theme="dark|light"` on `<html>`; a small script in `index.html` does the same before
first paint so a bright-theme user never sees a dark flash.

Rather than rewriting every component for a light mode, `index.css` **redefines what the
colour ramp means** under `:root[data-theme='light']` — `--color-slate-100` becomes near-black,
`--color-white` becomes near-black, and the status hues darken until they clear 4.5:1 on a pale
surface. So `text-slate-100` keeps meaning "strongest text" in both themes and no markup had to
change. Glass itself is a set of tokens (`--glass-bg`, `--glass-rim`, `--glass-blur`, …) that
each theme supplies its own values for.

A panel is four layers: a tinted fill, a blurred and saturated backdrop, a hairline border, and
a gradient rim drawn in `::after` with a mask — the rim is what stops glass reading as a flat
translucent rectangle.

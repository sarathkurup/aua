import { Link } from 'react-router'
import type { LiveAuctionCard } from '../types'
import { money, moneyFull, scheduleStamp, timeUntil } from '../lib/format'
import { AuctionStatusPill, TournamentTypePill } from './primitives'

/**
 * One room on the dashboard.
 *
 * The same card serves a live room and a scheduled one — a spectator is choosing between
 * them in the same glance, so they read as one family rather than two designs. What
 * changes is the middle band: the lot on the block, or when the room opens.
 */
export function AuctionCard({ auction }: { auction: LiveAuctionCard }) {
  const scheduled = auction.scheduledStartAt !== null && auction.status === 'Draft'

  return (
    <Link
      to={`/watch/${auction.publicId}`}
      className="glass animate-rise-in group block p-4 transition duration-200 hover:-translate-y-0.5 hover:bg-white/10"
    >
      <div className="flex items-start gap-3">
        <span aria-hidden className="text-3xl">
          {auction.sportIcon ?? '🏆'}
        </span>
        <div className="min-w-0 flex-1">
          {/* Wraps to two lines rather than truncating: an auction name cut to
              "Premier T20 …" tells a spectator nothing about which room it is. */}
          <p className="line-clamp-2 font-bold leading-snug text-white">{auction.auctionName}</p>
          <p className="truncate text-xs text-slate-400">{auction.tournamentName}</p>
        </div>
        {scheduled ? (
          <span className="pill pill-info">Scheduled</span>
        ) : (
          <AuctionStatusPill status={auction.status} />
        )}
      </div>

      <div className="glass-inset mt-3 px-3 py-2.5">
        {scheduled ? (
          <div className="flex items-center gap-3">
            <span aria-hidden className="text-lg">
              🗓️
            </span>
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-semibold text-slate-100">
                {scheduleStamp(auction.scheduledStartAt!)}
              </p>
              <p className="truncate text-xs text-slate-400">
                {auction.playersTotal} players · {auction.teamCount} teams
              </p>
            </div>
            <p className="nums shrink-0 text-sm font-bold text-brand-300">
              {timeUntil(auction.scheduledStartAt!)}
            </p>
          </div>
        ) : auction.currentPlayerName ? (
          <div className="flex items-center gap-3">
            {auction.currentPlayerPhotoThumbUrl ? (
              <img
                src={auction.currentPlayerPhotoThumbUrl}
                alt=""
                width={40}
                height={40}
                className="size-10 shrink-0 rounded-full border border-white/20 object-cover"
              />
            ) : (
              <span aria-hidden className="size-2 shrink-0 rounded-full bg-live-400 animate-pulse-ring" />
            )}
            <div className="min-w-0 flex-1">
              <p className="truncate text-sm font-semibold text-slate-100">
                {auction.currentPlayerName}
              </p>
              <p className="truncate text-xs text-slate-400">
                {auction.currentBidderTeam ? `${auction.currentBidderTeam} leads` : 'no bids yet'}
              </p>
            </div>
            {auction.currentBid !== null && (
              <p className="nums text-lg font-black text-white" title={moneyFull(auction.currentBid)}>
                {money(auction.currentBid)}
              </p>
            )}
          </div>
        ) : (
          <p className="text-sm text-slate-400">Between lots.</p>
        )}
      </div>

      <div className="mt-3 flex flex-wrap items-center gap-2 text-xs text-slate-400">
        <TournamentTypePill type={auction.tournamentType} />
        {auction.visibility === 'Private' && (
          <span className="pill pill-warn" title="Unlisted — only people you send the link to can watch">
            unlisted
          </span>
        )}
        {!scheduled && (
          <span className="nums">
            {auction.playersSold}/{auction.playersTotal} sold
          </span>
        )}
        <span className="ml-auto font-semibold text-brand-300 transition group-hover:text-brand-200">
          {scheduled ? 'Preview →' : 'Watch →'}
        </span>
      </div>
    </Link>
  )
}

export type UserRole = 'TeamOwner' | 'Admin'
export type TournamentType = 'Professional' | 'Corporate'
export type Gender = 'Unspecified' | 'Male' | 'Female'
export type AuctionStatus = 'Draft' | 'Live' | 'Paused' | 'Completed'
export type PlayerStatus = 'Available' | 'OnBlock' | 'Sold' | 'Unsold' | 'Retained'

/** A team the signed-in owner bids for. */
export interface OwnedTeam {
  teamId: number
  teamName: string
  shortName: string | null
  tournamentId: number
  tournamentName: string
  sportName: string
  purse: number
}

export interface User {
  id: number
  /** External identity. Use this anywhere an account is referenced outside the app. */
  publicId: string
  username: string
  displayName: string
  role: UserRole
  email: string | null
  phoneNumber: string | null
  avatarUrl: string | null
  teams: OwnedTeam[]
}

/** An account that can be handed a team. Names only — no contact details. */
export interface OwnerOption {
  id: number
  displayName: string
}

export interface RegisterRequest {
  username: string
  displayName: string
  password: string
  email: string | null
  phoneNumber: string | null
}

export interface UpdateProfileRequest {
  displayName: string
  email: string | null
  phoneNumber: string | null
  avatarUrl: string | null
}

export interface AuthResponse {
  token: string
  expiresAtUtc: string
  user: User
}

export interface Sport {
  id: number
  name: string
  description: string | null
  icon: string | null
  /** Attribute names players of this sport are described by, e.g. ["Batting","Bowling"]. */
  skillFields: string[]
}

export interface SquadRules {
  minSquadSize: number
  maxSquadSize: number
  minWomenPerSquad: number
  maxSpendPerPlayer: number | null
  reservePurseForUnfilledSlots: boolean
}

export interface Tournament {
  id: number
  name: string
  description: string | null
  sportId: number
  sportName: string
  sportIcon: string | null
  type: TournamentType
  teamPurse: number
  rules: SquadRules
  teamCount: number
  playerCount: number
  /** Display name of the auctioneer who created it. */
  ownerName: string | null
  /** True when the signed-in caller may change it — its owner, or an admin. */
  canManage: boolean
}

export interface Player {
  id: number
  name: string
  tournamentId: number
  sportName: string
  role: string | null
  gender: Gender
  country: string | null
  skills: Record<string, string>
  basePrice: number
  /** 96px square, immutable URL. */
  photoThumbUrl: string | null
  /** 480px square, immutable URL. */
  photoUrl: string | null
  status: PlayerStatus
  /** Bought at auction, or retained as the team's owner or a marquee signing. */
  teamRole: TeamRole
  lotOrder: number
  /** Shirt number. Use `squadNumber()` rather than reading this directly. */
  jerseyNumber: number | null
  soldPrice: number | null
  /** When the hammer fell. Orders the sold list by sale rather than by lot. */
  soldAt: string | null
  soldToTeamId: number | null
  soldToTeamName: string | null
  currentBid: number | null
  currentBidderTeamId: number | null
  currentBidderTeam: string | null
}

export type AuctionVisibility = 'Public' | 'Private'

export interface Auction {
  id: number
  /** The room's share address. Spectator routes and hub joins use this, never `id`. */
  publicId: string
  name: string
  tournamentId: number
  tournamentName: string
  sportId: number
  sportName: string
  sportIcon: string | null
  tournamentType: TournamentType
  status: AuctionStatus
  visibility: AuctionVisibility
  minBidIncrement: number
  /** Floor for the opening bid regardless of base price. Zero means base price alone decides. */
  minimumBid: number
  /** Increment ladder, lowest rung first. */
  increments: BidIncrementRule[]
  /** Seconds a lot stays open with no bid. Zero disables the timer. */
  autoTimeoutSeconds: number
  /** When the current lot expires, as an instant every screen counts down to. */
  lotDeadlineAt: string | null
  currentPlayerId: number | null
  /** When the room is due to open. Null means it can be started whenever. */
  scheduledStartAt: string | null
  startedAt: string | null
  endedAt: string | null
}

export interface Bid {
  id: number
  auctionId: number
  playerId: number
  playerName: string
  teamId: number
  teamName: string
  teamShortName: string | null
  amount: number
  placedAt: string
  isManualOverride: boolean
}

/** A team's standing, including its distance from the tournament's squad rules. */
export interface TeamSummary {
  teamId: number
  name: string
  shortName: string | null
  ownerUserId: number | null
  purse: number
  initialPurse: number
  playersWon: number
  womenWon: number
  totalSpent: number
  slotsRemaining: number
  womenRequired: number
  reservedPurse: number
  spendablePurse: number
  canBid: boolean
  blockedReason: string | null
  /** Bought players plus retained ones — what the squad rules are measured against. */
  squadSize: number
  /** The owner and marquee players the team started with, owner first. */
  retained: TeamMember[]
}

export type TeamRole = 'Auctioned' | 'Owner' | 'Marquee'

export type AccountRequestStatus = 'Pending' | 'Approved' | 'Declined'

/** Someone asking the auction admin for an auction of their own. */
export interface AccountRequest {
  id: number
  username: string
  displayName: string
  auctionName: string | null
  contact: string | null
  status: AccountRequestStatus
  requestedAt: string
  decidedAt: string | null
  decidedByName: string | null
  decisionNote: string | null
}

/** The one and only time the generated password is readable. */
export interface ApprovalResult {
  request: AccountRequest
  username: string
  generatedPassword: string
}

export interface CreateAccountRequest {
  username: string
  displayName: string
  auctionName: string | null
  contact: string | null
}

export interface SaveTeamMemberRequest {
  name: string
  teamRole: Exclude<TeamRole, 'Auctioned'>
  role: string | null
  gender: Gender
  country: string | null
  skills: Record<string, string>
}

/** A permanent squad member: the team's owner, or a marquee signing. */
export interface TeamMember {
  playerId: number
  name: string
  teamRole: TeamRole
  role: string | null
  gender: Gender
  photoThumbUrl: string | null
  photoUrl: string | null
}

/** The most recent completed sale — what "reopen last lot" would put back. */
export interface LastSale {
  playerId: number
  playerName: string
  teamId: number
  teamName: string
  amount: number
  soldAt: string
}

/** The whole room in one payload — what the hub broadcasts after every change. */
export interface AuctionState {
  auction: Auction
  rules: SquadRules
  currentPlayer: Player | null
  topBid: Bid | null
  nextMinimumBid: number
  recentBids: Bid[]
  teams: TeamSummary[]
  lastSale: LastSale | null
}

/** One card on the public landing page. */
export interface LiveAuctionCard {
  publicId: string
  visibility: AuctionVisibility
  /** Set on a scheduled card; null on one that is already running. */
  scheduledStartAt: string | null
  auctionName: string
  tournamentName: string
  sportName: string
  sportIcon: string | null
  tournamentType: TournamentType
  status: AuctionStatus
  currentPlayerName: string | null
  currentPlayerPhotoThumbUrl: string | null
  currentBid: number | null
  currentBidderTeam: string | null
  teamCount: number
  playersSold: number
  playersTotal: number
}

export interface SavePlayerRequest {
  name: string
  tournamentId: number
  role: string | null
  gender: Gender
  country: string | null
  skills: Record<string, string>
  basePrice: number
  imageUrl: string | null
  lotOrder: number
  jerseyNumber: number | null
}

export interface SaveTournamentRequest {
  name: string
  description: string | null
  sportId: number
  type: TournamentType
  teamPurse: number
  minSquadSize: number
  maxSquadSize: number
  minWomenPerSquad: number
  maxSpendPerPlayer: number | null
  reservePurseForUnfilledSlots: boolean
}

export interface SaveTeamRequest {
  name: string
  shortName: string | null
  ownerUserId: number | null
}

/** Once the bid reaches `from`, raises go up in `increment`. */
export interface BidIncrementRule {
  from: number
  increment: number
}

/** The bidding rules, identical on create and edit so a rule cannot exist in only one. */
export interface BiddingRuleFields {
  minBidIncrement: number
  minimumBid: number
  increments: BidIncrementRule[]
  autoTimeoutSeconds: number
}

export interface CreateAuctionRequest extends BiddingRuleFields {
  name: string
  tournamentId: number
  visibility: AuctionVisibility
  /** ISO instant, at most MAX_SCHEDULE_DAYS_AHEAD out. Null starts it manually. */
  scheduledStartAt: string | null
}

export interface SaveAuctionRequest extends BiddingRuleFields {
  name: string
  visibility: AuctionVisibility
  scheduledStartAt: string | null
}

/** Matches the server cap on AutoTimeoutSeconds. */
export const MAX_LOT_TIMEOUT_SECONDS = 600

export interface RetentionPolicy {
  enabled: boolean
  auctionDays: number
}

/** Mirrors AuctionSchedule.MaxDaysAhead on the server. */
export const MAX_SCHEDULE_DAYS_AHEAD = 30

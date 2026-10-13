import { API_BASE, api } from './client'
import type {
  AccountRequest,
  AccountRequestStatus,
  ApprovalResult,
  Auction,
  AuctionState,
  AuthResponse,
  Bid,
  CreateAccountRequest,
  CreateAuctionRequest,
  LiveAuctionCard,
  OwnerOption,
  Player,
  RetentionPolicy,
  SaveAuctionRequest,
  SavePlayerRequest,
  SaveTeamMemberRequest,
  SaveTeamRequest,
  SaveTournamentRequest,
  Sport,
  TeamSummary,
  Tournament,
  UpdateProfileRequest,
  User,
} from '../types'

export const authApi = {
  login: (username: string, password: string) =>
    api.post<AuthResponse>('/auth/login', { username, password }).then((r) => r.data),

  me: () => api.get<User>('/auth/me').then((r) => r.data),

  /** Edits the caller's own profile. There is no account id in the route by design. */
  updateProfile: (body: UpdateProfileRequest) =>
    api.put<User>('/auth/me', body).then((r) => r.data),
}

export const sportsApi = {
  list: () => api.get<Sport[]>('/sports').then((r) => r.data),
}

export const tournamentsApi = {
  list: () => api.get<Tournament[]>('/tournaments').then((r) => r.data),

  get: (id: number) => api.get<Tournament>(`/tournaments/${id}`).then((r) => r.data),

  create: (body: SaveTournamentRequest) =>
    api.post<Tournament>('/tournaments', body).then((r) => r.data),

  update: (id: number, body: SaveTournamentRequest) =>
    api.put<Tournament>(`/tournaments/${id}`, body).then((r) => r.data),

  remove: (id: number) => api.delete(`/tournaments/${id}`).then(() => undefined),

  teams: (id: number) => api.get<TeamSummary[]>(`/tournaments/${id}/teams`).then((r) => r.data),

  addTeam: (id: number, body: SaveTeamRequest) =>
    api.post<TeamSummary>(`/tournaments/${id}/teams`, body).then((r) => r.data),

  updateTeam: (teamId: number, body: SaveTeamRequest) =>
    api.put<TeamSummary>(`/tournaments/teams/${teamId}`, body).then((r) => r.data),

  removeTeam: (teamId: number) =>
    api.delete(`/tournaments/teams/${teamId}`).then(() => undefined),

  /** Adds the team's owner or a marquee player — permanent, never auctioned. */
  addMember: (teamId: number, body: SaveTeamMemberRequest) =>
    api.post<TeamSummary>(`/tournaments/teams/${teamId}/members`, body).then((r) => r.data),

  removeMember: (playerId: number) =>
    api.delete<TeamSummary>(`/tournaments/members/${playerId}`).then((r) => r.data),
}

export const accountRequestsApi = {
  /** Open to anyone on the network — asking costs nothing and grants nothing. */
  submit: (body: CreateAccountRequest) =>
    api.post<AccountRequest>('/account-requests', body).then((r) => r.data),

  list: (status?: AccountRequestStatus) =>
    api
      .get<AccountRequest[]>('/account-requests', { params: status ? { status } : undefined })
      .then((r) => r.data),

  /** The response carries the generated password. It cannot be fetched again. */
  approve: (id: number) =>
    api.post<ApprovalResult>(`/account-requests/${id}/approve`).then((r) => r.data),

  decline: (id: number, reason: string | null) =>
    api.post<AccountRequest>(`/account-requests/${id}/decline`, { reason }).then((r) => r.data),

  resetPassword: (userId: number) =>
    api
      .post<ApprovalResult>(`/account-requests/users/${userId}/reset-password`)
      .then((r) => r.data),
}

export const devApi = {
  /** Development only: builds a tournament whose photos cover every orientation. */
  seedMockData: () =>
    api
      .post<{ tournamentId: number; name: string; players: number; message: string }>('/dev/mock-data')
      .then((r) => r.data),
}

export const playersApi = {
  list: (tournamentId: number) =>
    api.get<Player[]>('/players', { params: { tournamentId } }).then((r) => r.data),

  create: (body: SavePlayerRequest) => api.post<Player>('/players', body).then((r) => r.data),

  update: (id: number, body: SavePlayerRequest) =>
    api.put<Player>(`/players/${id}`, body).then((r) => r.data),

  /** The server re-encodes the file into the two sizes the UI uses. */
  uploadPhoto: (id: number, file: File) => {
    const form = new FormData()
    form.append('file', file)
    return api.post<Player>(`/players/${id}/photo`, form).then((r) => r.data)
  },

  remove: (id: number) => api.delete(`/players/${id}`).then(() => undefined),
}

export const auctionsApi = {
  list: () => api.get<Auction[]>('/auctions').then((r) => r.data),

  /** Every auction currently running — the public lobby feed. */
  live: () => api.get<LiveAuctionCard[]>('/auctions/live').then((r) => r.data),

  /** Scheduled rooms not yet open. Private ones appear only for their auctioneer. */
  upcoming: () => api.get<LiveAuctionCard[]>('/auctions/upcoming').then((r) => r.data),

  /** How long finished auctions are kept, stated by the server rather than hard-coded here. */
  retention: () => api.get<RetentionPolicy>('/auctions/retention').then((r) => r.data),

  update: (id: number, body: SaveAuctionRequest) =>
    api.put<Auction>(`/auctions/${id}`, body).then((r) => r.data),

  /** Spreadsheet download, gated by the share link like the results page it sits on. */
  exportUrl: (publicId: string, sheet: 'squads' | 'bids') =>
    `${API_BASE}/api/auctions/link/${publicId}/export/${sheet}.csv`,

  /** Sequential-id lookup. Requires an account — see `stateByLink` for spectators. */
  state: (id: number) => api.get<AuctionState>(`/auctions/${id}/state`).then((r) => r.data),

  /** The spectator path: the share link is the permission, so no account is needed. */
  stateByLink: (publicId: string) =>
    api.get<AuctionState>(`/auctions/link/${publicId}/state`).then((r) => r.data),

  create: (body: CreateAuctionRequest) => api.post<Auction>('/auctions', body).then((r) => r.data),

  start: (id: number) => api.post<AuctionState>(`/auctions/${id}/start`).then((r) => r.data),

  pause: (id: number) => api.post<AuctionState>(`/auctions/${id}/pause`).then((r) => r.data),

  resume: (id: number) => api.post<AuctionState>(`/auctions/${id}/resume`).then((r) => r.data),

  end: (id: number) => api.post<AuctionState>(`/auctions/${id}/end`).then((r) => r.data),

  call: (id: number, playerId: number) =>
    api.post<AuctionState>(`/auctions/${id}/call/${playerId}`).then((r) => r.data),

  sell: (id: number) => api.post<AuctionState>(`/auctions/${id}/sell`).then((r) => r.data),

  markUnsold: (id: number) => api.post<AuctionState>(`/auctions/${id}/unsold`).then((r) => r.data),

  /** Puts the last sold lot back on the block and refunds the team that won it. */
  reopenLastLot: (id: number) =>
    api.post<AuctionState>(`/auctions/${id}/reopen`).then((r) => r.data),

  remove: (id: number) => api.delete(`/auctions/${id}`).then(() => undefined),
}

export const bidsApi = {
  /**
   * `clientRequestId` makes the call safe to retry: the server returns the original
   * bid rather than recording a second one.
   */
  place: (auctionId: number, playerId: number, teamId: number, amount: number, clientRequestId: string) =>
    api
      .post<Bid>(`/auctions/${auctionId}/bids`, { playerId, teamId, amount, clientRequestId })
      .then((r) => r.data),

  placeManual: (auctionId: number, playerId: number, teamId: number, amount: number) =>
    api
      .post<Bid>(`/auctions/${auctionId}/bids/manual`, { playerId, teamId, amount })
      .then((r) => r.data),

  /**
   * Retracts the standing bid on the open lot. Returns the whole room state, since the
   * price, the leader and every team's spendable purse all move with it.
   */
  undo: (auctionId: number) =>
    api.post<AuctionState>(`/auctions/${auctionId}/bids/undo`).then((r) => r.data),

  recent: (auctionId: number, take = 15) =>
    api.get<Bid[]>(`/auctions/${auctionId}/bids`, { params: { take } }).then((r) => r.data),
}

export const usersApi = {
  /** Accounts that can be assigned to a team. Admin only. */
  owners: () => api.get<OwnerOption[]>('/users/owners').then((r) => r.data),
}

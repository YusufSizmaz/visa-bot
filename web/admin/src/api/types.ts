// Web API'nin JSON sozlesmeleri. C# tarafindaki DTO'larin birebir karsiligidir.
// API enum'lari metin olarak doner (JsonStringEnumConverter), bu yuzden string union kullaniyoruz.

export type SourceType = 'Rss' | 'Html'

export type SourceCategory = 'Visa' | 'FlightCampaign'

export type DeliveryStatus = 'Pending' | 'Delivered' | 'Failed' | 'Archived'

export interface HtmlParsingRules {
  itemSelector: string
  titleSelector: string
  linkSelector: string | null
  summarySelector: string | null
  publishedAtSelector: string | null
}

export interface NewsSource {
  id: string
  name: string
  url: string
  type: SourceType
  parsingRules: HtmlParsingRules | null
  keywords: string[] | null
  turkishOnly: boolean
  category: SourceCategory
  fetchIntervalMinutes: number
  isActive: boolean
  createdAtUtc: string
  nextFetchAtUtc: string
  lastFetchedAtUtc: string | null
  lastSucceededAtUtc: string | null
  consecutiveFailureCount: number
  lastFetchError: string | null
}

export interface NewsSourceInput {
  name: string
  url: string
  type: SourceType
  parsingRules: HtmlParsingRules | null
  fetchIntervalMinutes: number
  keywords: string[] | null
  turkishOnly: boolean
  category: SourceCategory
}

export interface FetchResult {
  newsSourceId: string
  status: 'Completed' | 'Skipped'
  skipReason: string | null
  entriesRead: number
  newItems: number
  queuedForDelivery: number
  archived: number
}

export interface NewsItem {
  id: string
  newsSourceId: string
  newsSourceName: string
  title: string
  url: string
  summary: string | null
  publishedAtUtc: string | null
  discoveredAtUtc: string
  deliveryStatus: DeliveryStatus
  deliveryAttempts: number
  nextDeliveryAttemptAtUtc: string
  deliveredAtUtc: string | null
  lastDeliveryError: string | null
  archiveReason: ArchiveReason | null
}

export type ArchiveReason = 'InitialImport' | 'TooOld' | 'NotRelevant' | 'NotTurkish' | 'QueueLimit'

export type ChannelMessageStatus = 'Scheduled' | 'Sent' | 'Failed' | 'Cancelled'

export interface ChannelMessage {
  id: string
  kind: 'Custom'
  title: string | null
  body: string
  linkUrl: string | null
  buttonText: string | null
  buttonUrl: string | null
  hasPhoto: boolean
  status: ChannelMessageStatus
  createdAtUtc: string
  scheduledAtUtc: string
  sentAtUtc: string | null
  attempts: number
  lastError: string | null
}

export interface ChannelMessagePage {
  items: ChannelMessage[]
  page: number
  pageSize: number
  totalCount: number
}

export interface ChannelMessageInput {
  title: string
  body: string
  linkUrl: string
  buttonText: string
  buttonUrl: string
  photo: File | null
  /** ISO 8601, saat dilimiyle. Bos ise hemen gonderilir. */
  scheduledAt: string | null
}

export interface ChannelOverview {
  channel: { title: string; username: string | null; memberCount: number | null } | null
  scheduledMessages: number
}

export interface CursorPage<T> {
  items: T[]
  nextCursor: string | null
}

export interface NewsItemStats {
  pending: number
  delivered: number
  failed: number
  archived: number
  deliveredLast24Hours: number
  lastDeliveredAtUtc: string | null
  lastDiscoveredAtUtc: string | null
}

export interface NewsItemFilter {
  newsSourceId?: string
  status?: DeliveryStatus
  pageSize?: number
}

/** RFC 9457 ProblemDetails; dogrulama hatalarinda errors alani dolu gelir. */
export interface ProblemDetails {
  title?: string
  status?: number
  detail?: string
  code?: string
  errors?: Record<string, string[]>
}

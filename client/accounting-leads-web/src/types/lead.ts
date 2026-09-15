export const CLIENT_TYPES = [
  "Individual",
  "SoleProprietorship",
  "Corporation",
  "LimitedLiabilityCompany",
  "Partnership",
  "NonProfit",
  "Other",
] as const

export type ClientType = (typeof CLIENT_TYPES)[number]

export const CLIENT_TYPE_LABELS: Record<ClientType, string> = {
  Individual: "Individual",
  SoleProprietorship: "Sole Proprietorship",
  Corporation: "Corporation",
  LimitedLiabilityCompany: "LLC",
  Partnership: "Partnership",
  NonProfit: "Non-Profit",
  Other: "Other",
}

export const LEAD_STATUSES = ["New", "Qualified"] as const
export type LeadStatus = (typeof LEAD_STATUSES)[number]
export const LEAD_STATUS_LABELS: Record<LeadStatus, string> = {
  New: "New",
  Qualified: "Qualified",
}
// shadcn's Badge variants (default/secondary/destructive/outline) don't cover a
// blue/green distinction, so these override color directly on top of the "outline" base.
export const LEAD_STATUS_BADGE_CLASSES: Record<LeadStatus, string> = {
  New: "border-transparent bg-blue-100 text-blue-800 dark:bg-blue-900/40 dark:text-blue-300",
  Qualified: "border-transparent bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-300",
}

export const LEAD_PRIORITIES = ["Low", "Medium", "High"] as const
export type LeadPriority = (typeof LEAD_PRIORITIES)[number]
export const LEAD_PRIORITY_LABELS: Record<LeadPriority, string> = {
  Low: "Low",
  Medium: "Medium",
  High: "High",
}
// Traffic-light scheme, also used to color-code the qualification Score on Lead Detail
// (Score directly determines Priority, so they always share a color).
export const LEAD_PRIORITY_BADGE_CLASSES: Record<LeadPriority, string> = {
  Low: "border-transparent bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-300",
  Medium: "border-transparent bg-amber-100 text-amber-800 dark:bg-amber-900/40 dark:text-amber-300",
  High: "border-transparent bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-300",
}

export const AI_PROCESSING_STATUSES = ["Pending", "Processing", "Completed", "Failed"] as const
export type AiProcessingStatus = (typeof AI_PROCESSING_STATUSES)[number]
export const AI_PROCESSING_STATUS_LABELS: Record<AiProcessingStatus, string> = {
  Pending: "Pending",
  Processing: "Processing",
  Completed: "Completed",
  Failed: "Failed",
}
export const AI_PROCESSING_STATUS_BADGE_CLASSES: Record<AiProcessingStatus, string> = {
  Pending: "border-transparent bg-slate-100 text-slate-700 dark:bg-slate-800/60 dark:text-slate-300",
  Processing: "border-transparent bg-blue-100 text-blue-800 dark:bg-blue-900/40 dark:text-blue-300",
  Completed: "border-transparent bg-green-100 text-green-800 dark:bg-green-900/40 dark:text-green-300",
  Failed: "border-transparent bg-red-100 text-red-800 dark:bg-red-900/40 dark:text-red-300",
}

export interface CreateLeadResponse {
  id: string
  leadStatus: string
  aiProcessingStatus: string
}

export interface LeadListItem {
  id: string
  name: string
  companyName: string | null
  leadStatus: LeadStatus
  aiProcessingStatus: AiProcessingStatus
  priority: LeadPriority | null
  createdAtUtc: string
}

export interface PagedResult<T> {
  items: T[]
  totalCount: number
  page: number
  pageSize: number
  totalPages: number
}

export interface LeadAnalysisDetail {
  requestedServices: string[]
  extractedClientType: ClientType | null
  extractedApproximateAnnualRevenue: number | null
  extractedAccountingSoftware: string | null
  urgency: "Low" | "Medium" | "High" | null
  extractedFilingDeadline: string | null
  notes: string | null
  estimatedDeadlineInDays: number | null
  bookkeepingMonthsBehind: number | null
  createdAtUtc: string
}

export interface LeadQualificationDetail {
  score: number
  priority: LeadPriority
  reasons: string[]
  rulesVersionApplied: string
  createdAtUtc: string
}

export interface LeadDetail {
  id: string
  name: string
  email: string
  phone: string | null
  companyName: string | null
  clientType: ClientType | null
  approximateAnnualRevenue: number | null
  accountingSoftware: string | null
  inquiry: string
  leadStatus: LeadStatus
  aiProcessingStatus: AiProcessingStatus
  createdAtUtc: string
  analysis: LeadAnalysisDetail | null
  qualification: LeadQualificationDetail | null
}

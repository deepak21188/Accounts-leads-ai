import { InteractionRequiredAuthError, type AccountInfo, type IPublicClientApplication } from "@azure/msal-browser"
import type { CreateLeadResponse, LeadDetail, LeadListItem, PagedResult } from "@/types/lead"
import type { CreateLeadRequest } from "@/lib/leadSchema"
import { apiTokenRequest } from "@/auth/msalConfig"

export type LeadFormFieldErrors = Partial<Record<keyof CreateLeadRequest, string>>

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5151"

// ASP.NET ValidationProblemDetails keys errors by the C# request property name
// (PascalCase), not the camelCase JSON field name.
const SERVER_FIELD_TO_REQUEST_FIELD: Record<string, keyof CreateLeadRequest> = {
  Name: "name",
  Email: "email",
  Phone: "phone",
  CompanyName: "companyName",
  ClientType: "clientType",
  ApproximateAnnualRevenue: "approximateAnnualRevenue",
  AccountingSoftware: "accountingSoftware",
  Inquiry: "inquiry",
}

export class LeadValidationError extends Error {
  fieldErrors: LeadFormFieldErrors
  // Messages keyed by a field name that doesn't map to a form field, including the empty
  // string — the API returns errors["": [...]] for domain-level failures (e.g.
  // DomainValidationException in CreateLeadCommandHandler) that aren't tied to one input.
  generalErrors: string[]

  constructor(fieldErrors: LeadFormFieldErrors, generalErrors: string[] = []) {
    super("The lead submission failed validation.")
    this.name = "LeadValidationError"
    this.fieldErrors = fieldErrors
    this.generalErrors = generalErrors
  }
}

interface ValidationProblemDetails {
  errors?: Record<string, string[]>
}

export async function createLead(payload: CreateLeadRequest): Promise<CreateLeadResponse> {
  let response: Response
  try {
    response = await fetch(`${API_BASE_URL}/api/leads`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(payload),
    })
  } catch {
    throw new Error(
      "Could not reach the server. Check your connection and try again."
    )
  }

  if (response.status === 201) {
    return (await response.json()) as CreateLeadResponse
  }

  if (response.status === 400) {
    const problem = (await response.json()) as ValidationProblemDetails
    const fieldErrors: LeadFormFieldErrors = {}
    const generalErrors: string[] = []
    for (const [serverField, messages] of Object.entries(problem.errors ?? {})) {
      if (messages.length === 0) continue

      const requestField = SERVER_FIELD_TO_REQUEST_FIELD[serverField]
      if (requestField) {
        fieldErrors[requestField] = messages[0]
      } else {
        // Covers both the empty-string key (domain-level failures) and any server field
        // name this client doesn't recognize — surfaced as a general error rather than
        // silently dropped, since there's no specific field to attach it to.
        generalErrors.push(messages[0])
      }
    }
    throw new LeadValidationError(fieldErrors, generalErrors)
  }

  throw new Error("Something went wrong submitting your inquiry. Please try again.")
}

export async function acquireApiToken(
  msalInstance: IPublicClientApplication,
  account: AccountInfo
): Promise<string> {
  try {
    const result = await msalInstance.acquireTokenSilent({ ...apiTokenRequest, account })
    return result.accessToken
  } catch (error) {
    if (error instanceof InteractionRequiredAuthError) {
      // Navigates away; nothing after this call in the caller will run.
      await msalInstance.acquireTokenRedirect({ ...apiTokenRequest, account })
    }
    throw error
  }
}

export interface LeadFilters {
  status?: string
  priority?: string
  search?: string
  page?: number
  pageSize?: number
  sortBy?: string
  sortDescending?: boolean
}

/**
 * Fetches a page of the accountant-only lead list. 401/403 are mapped to distinct error
 * messages so callers can tell "not signed in" from "signed in but not authorized" apart, per
 * docs/architecture.md §23.
 */
export async function getLeads(
  msalInstance: IPublicClientApplication,
  account: AccountInfo,
  filters: LeadFilters = {}
): Promise<PagedResult<LeadListItem>> {
  const token = await acquireApiToken(msalInstance, account)

  const params = new URLSearchParams()
  if (filters.status) params.set("status", filters.status)
  if (filters.priority) params.set("priority", filters.priority)
  if (filters.search) params.set("search", filters.search)
  if (filters.page) params.set("page", String(filters.page))
  if (filters.pageSize) params.set("pageSize", String(filters.pageSize))
  if (filters.sortBy) params.set("sortBy", filters.sortBy)
  if (filters.sortDescending) params.set("sortDescending", "true")
  const query = params.toString()

  const response = await fetch(`${API_BASE_URL}/api/leads${query ? `?${query}` : ""}`, {
    headers: { Authorization: `Bearer ${token}` },
  })

  if (response.status === 401) {
    throw new Error("SESSION_EXPIRED")
  }
  if (response.status === 403) {
    throw new Error("NOT_AUTHORIZED")
  }
  if (!response.ok) {
    throw new Error("Could not load leads. Please try again.")
  }

  return (await response.json()) as PagedResult<LeadListItem>
}

/**
 * Fetches a single lead's detail. Returns null on a 404 (not found) — a successful request
 * with no matching lead, not an error — so callers can render an explicit "not found" state.
 */
export async function getLeadDetail(
  msalInstance: IPublicClientApplication,
  account: AccountInfo,
  leadId: string
): Promise<LeadDetail | null> {
  const token = await acquireApiToken(msalInstance, account)

  const response = await fetch(`${API_BASE_URL}/api/leads/${leadId}`, {
    headers: { Authorization: `Bearer ${token}` },
  })

  if (response.status === 401) {
    throw new Error("SESSION_EXPIRED")
  }
  if (response.status === 403) {
    throw new Error("NOT_AUTHORIZED")
  }
  if (response.status === 404) {
    return null
  }
  if (!response.ok) {
    throw new Error("Could not load this lead. Please try again.")
  }

  return (await response.json()) as LeadDetail
}

import { afterEach, describe, expect, it, vi } from "vitest"
import type { AccountInfo, IPublicClientApplication } from "@azure/msal-browser"
import { InteractionRequiredAuthError } from "@azure/msal-browser"

import { createLead, getLeadDetail, getLeads, LeadValidationError } from "@/api/leadsApi"
import type { CreateLeadRequest } from "@/lib/leadSchema"
import type { CreateLeadResponse, LeadDetail, LeadListItem, PagedResult } from "@/types/lead"

// createLead's default base URL is http://localhost:5151 (see leadsApi.ts) when
// VITE_API_BASE_URL is not set, which is the case in this test environment.
const EXPECTED_URL = "http://localhost:5151/api/leads"

function jsonResponse(status: number, body: unknown): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  })
}

function validPayload(): CreateLeadRequest {
  return {
    name: "Jane Accountant",
    email: "jane@example.com",
    phone: undefined,
    companyName: undefined,
    clientType: undefined,
    approximateAnnualRevenue: undefined,
    accountingSoftware: undefined,
    inquiry: "I need help with catch-up bookkeeping for my small business.",
  }
}

describe("createLead", () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it("resolves with the parsed body on HTTP 201 Created", async () => {
    const responseBody: CreateLeadResponse = {
      id: "11111111-1111-1111-1111-111111111111",
      leadStatus: "New",
      aiProcessingStatus: "Pending",
    }
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(201, responseBody))
    vi.stubGlobal("fetch", fetchMock)

    const result = await createLead(validPayload())

    expect(result).toEqual(responseBody)
    expect(fetchMock).toHaveBeenCalledTimes(1)
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(url).toBe(EXPECTED_URL)
    expect(init.method).toBe("POST")
    expect(JSON.parse(init.body as string)).toEqual(validPayload())
  })

  it("throws LeadValidationError with camelCase field errors mapped from a 400 ValidationProblemDetails body", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      jsonResponse(400, {
        errors: {
          Name: ["The Name field is required."],
          Email: ["The Email field is not a valid e-mail address."],
        },
      })
    )
    vi.stubGlobal("fetch", fetchMock)

    const error = await createLead(validPayload()).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(LeadValidationError)
    const validationError = error as LeadValidationError
    expect(validationError.fieldErrors).toEqual({
      name: "The Name field is required.",
      email: "The Email field is not a valid e-mail address.",
    })
  })

  it("takes only the first message per field when a field has multiple validation errors", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      jsonResponse(400, {
        errors: {
          Inquiry: ["The Inquiry field is required.", "Inquiry must be 4000 characters or fewer."],
        },
      })
    )
    vi.stubGlobal("fetch", fetchMock)

    const error = (await createLead(validPayload()).catch((e: unknown) => e)) as LeadValidationError

    expect(error.fieldErrors).toEqual({ inquiry: "The Inquiry field is required." })
  })

  it("surfaces a server field name that doesn't map to a known request field as a general error, not a dropped one", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      jsonResponse(400, {
        errors: {
          SomeUnmappedServerField: ["Some message."],
        },
      })
    )
    vi.stubGlobal("fetch", fetchMock)

    const error = (await createLead(validPayload()).catch((e: unknown) => e)) as LeadValidationError

    expect(error).toBeInstanceOf(LeadValidationError)
    expect(error.fieldErrors).toEqual({})
    expect(error.generalErrors).toEqual(["Some message."])
  })

  it("surfaces the empty-string key CreateLeadCommandHandler uses for domain-level validation failures as a general error", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      jsonResponse(400, {
        errors: {
          "": ["Email must be a valid email address."],
        },
      })
    )
    vi.stubGlobal("fetch", fetchMock)

    const error = (await createLead(validPayload()).catch((e: unknown) => e)) as LeadValidationError

    expect(error).toBeInstanceOf(LeadValidationError)
    expect(error.fieldErrors).toEqual({})
    expect(error.generalErrors).toEqual(["Email must be a valid email address."])
  })

  it("throws a generic Error when fetch itself rejects (network failure)", async () => {
    const fetchMock = vi.fn().mockRejectedValue(new TypeError("Failed to fetch"))
    vi.stubGlobal("fetch", fetchMock)

    const error = await createLead(validPayload()).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(Error)
    expect(error).not.toBeInstanceOf(LeadValidationError)
    expect((error as Error).message).toBe(
      "Could not reach the server. Check your connection and try again."
    )
  })

  it("throws a generic Error for an unexpected non-2xx, non-400 status", async () => {
    const fetchMock = vi.fn().mockResolvedValue(new Response(null, { status: 500 }))
    vi.stubGlobal("fetch", fetchMock)

    const error = await createLead(validPayload()).catch((e: unknown) => e)

    expect(error).toBeInstanceOf(Error)
    expect(error).not.toBeInstanceOf(LeadValidationError)
    expect((error as Error).message).toBe(
      "Something went wrong submitting your inquiry. Please try again."
    )
  })
})

const FAKE_ACCOUNT = { homeAccountId: "test-home-account-id" } as AccountInfo

function fakeMsalInstance(overrides: Partial<IPublicClientApplication> = {}): IPublicClientApplication {
  return {
    acquireTokenSilent: vi.fn().mockResolvedValue({ accessToken: "fake-access-token" }),
    acquireTokenRedirect: vi.fn().mockResolvedValue(undefined),
    ...overrides,
  } as unknown as IPublicClientApplication
}

describe("getLeads", () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it("attaches the acquired access token as a Bearer header and returns the parsed page", async () => {
    const page: PagedResult<LeadListItem> = {
      items: [
        {
          id: "11111111-1111-1111-1111-111111111111",
          name: "Jane Accountant",
          companyName: "Jane's Bakery",
          leadStatus: "New",
          aiProcessingStatus: "Pending",
          priority: null,
          createdAtUtc: "2026-09-13T00:00:00Z",
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 20,
      totalPages: 1,
    }
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(200, page))
    vi.stubGlobal("fetch", fetchMock)
    const msalInstance = fakeMsalInstance()

    const result = await getLeads(msalInstance, FAKE_ACCOUNT)

    expect(result).toEqual(page)
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(url).toBe(EXPECTED_URL)
    expect((init.headers as Record<string, string>).Authorization).toBe("Bearer fake-access-token")
  })

  it("builds a query string from the provided filters", async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      jsonResponse(200, { items: [], totalCount: 0, page: 2, pageSize: 10, totalPages: 0 })
    )
    vi.stubGlobal("fetch", fetchMock)
    const msalInstance = fakeMsalInstance()

    await getLeads(msalInstance, FAKE_ACCOUNT, {
      status: "Qualified",
      priority: "High",
      search: "acme",
      page: 2,
      pageSize: 10,
    })

    const [url] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(url).toBe(`${EXPECTED_URL}?status=Qualified&priority=High&search=acme&page=2&pageSize=10`)
  })

  it("throws SESSION_EXPIRED on a 401 response", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 401 })))
    const msalInstance = fakeMsalInstance()

    const error = await getLeads(msalInstance, FAKE_ACCOUNT).catch((e: unknown) => e)

    expect((error as Error).message).toBe("SESSION_EXPIRED")
  })

  it("throws NOT_AUTHORIZED on a 403 response", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 403 })))
    const msalInstance = fakeMsalInstance()

    const error = await getLeads(msalInstance, FAKE_ACCOUNT).catch((e: unknown) => e)

    expect((error as Error).message).toBe("NOT_AUTHORIZED")
  })

  it("falls back to an interactive redirect when silent token acquisition requires interaction", async () => {
    const interactionError = new InteractionRequiredAuthError("interaction_required", "test-correlation-id")
    const msalInstance = fakeMsalInstance({
      acquireTokenSilent: vi.fn().mockRejectedValue(interactionError),
    })

    await expect(getLeads(msalInstance, FAKE_ACCOUNT)).rejects.toBe(interactionError)

    expect(msalInstance.acquireTokenRedirect).toHaveBeenCalledTimes(1)
  })
})

describe("getLeadDetail", () => {
  const LEAD_ID = "11111111-1111-1111-1111-111111111111"
  const DETAIL_URL = `http://localhost:5151/api/leads/${LEAD_ID}`

  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it("attaches the acquired access token and returns the parsed lead", async () => {
    const detail: LeadDetail = {
      id: LEAD_ID,
      name: "Jane Accountant",
      email: "jane@example.com",
      phone: null,
      companyName: "Jane's Bakery",
      clientType: null,
      approximateAnnualRevenue: null,
      accountingSoftware: null,
      inquiry: "I need help with catch-up bookkeeping for my small business.",
      leadStatus: "New",
      aiProcessingStatus: "Pending",
      createdAtUtc: "2026-09-13T00:00:00Z",
      analysis: null,
      qualification: null,
    }
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse(200, detail))
    vi.stubGlobal("fetch", fetchMock)
    const msalInstance = fakeMsalInstance()

    const result = await getLeadDetail(msalInstance, FAKE_ACCOUNT, LEAD_ID)

    expect(result).toEqual(detail)
    const [url, init] = fetchMock.mock.calls[0] as [string, RequestInit]
    expect(url).toBe(DETAIL_URL)
    expect((init.headers as Record<string, string>).Authorization).toBe("Bearer fake-access-token")
  })

  it("returns null on a 404 response instead of throwing", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 404 })))
    const msalInstance = fakeMsalInstance()

    const result = await getLeadDetail(msalInstance, FAKE_ACCOUNT, LEAD_ID)

    expect(result).toBeNull()
  })

  it("throws SESSION_EXPIRED on a 401 response", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 401 })))
    const msalInstance = fakeMsalInstance()

    const error = await getLeadDetail(msalInstance, FAKE_ACCOUNT, LEAD_ID).catch((e: unknown) => e)

    expect((error as Error).message).toBe("SESSION_EXPIRED")
  })

  it("throws NOT_AUTHORIZED on a 403 response", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(new Response(null, { status: 403 })))
    const msalInstance = fakeMsalInstance()

    const error = await getLeadDetail(msalInstance, FAKE_ACCOUNT, LEAD_ID).catch((e: unknown) => e)

    expect((error as Error).message).toBe("NOT_AUTHORIZED")
  })
})

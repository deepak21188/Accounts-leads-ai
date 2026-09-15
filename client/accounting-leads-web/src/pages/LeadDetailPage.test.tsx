import { screen } from "@testing-library/react"
import { MemoryRouter, Route, Routes } from "react-router"
import { afterEach, describe, expect, it, vi } from "vitest"

import { LeadDetailPage } from "@/pages/LeadDetailPage"
import { renderWithQueryClient } from "@/test/renderWithQueryClient"
import type { LeadDetail } from "@/types/lead"

const { useLeadDetailQueryMock, useSessionRecoveryMock } = vi.hoisted(() => ({
  useLeadDetailQueryMock: vi.fn(),
  useSessionRecoveryMock: vi.fn(),
}))

vi.mock("@/api/useLeadDetailQuery", () => ({
  useLeadDetailQuery: useLeadDetailQueryMock,
}))

vi.mock("@/auth/useSessionRecovery", () => ({
  useSessionRecovery: useSessionRecoveryMock,
}))

function baseLead(overrides: Partial<LeadDetail> = {}): LeadDetail {
  return {
    id: "lead-1",
    name: "Jane Accountant",
    email: "jane@example.com",
    phone: null,
    companyName: "Jane's Bakery",
    clientType: null,
    approximateAnnualRevenue: null,
    accountingSoftware: null,
    inquiry: "I need help with catch-up bookkeeping.",
    leadStatus: "New",
    aiProcessingStatus: "Pending",
    createdAtUtc: "2026-09-13T00:00:00Z",
    analysis: null,
    qualification: null,
    ...overrides,
  }
}

function renderPage() {
  return renderWithQueryClient(
    <MemoryRouter initialEntries={["/accountant/leads/lead-1"]}>
      <Routes>
        <Route path="/accountant/leads/:id" element={<LeadDetailPage />} />
      </Routes>
    </MemoryRouter>
  )
}

describe("LeadDetailPage", () => {
  afterEach(() => {
    vi.clearAllMocks()
  })

  it("shows an explicit pending message and no blank AI sections when processing is still pending", () => {
    useSessionRecoveryMock.mockReturnValue({ state: { kind: "ok" }, retry: vi.fn() })
    useLeadDetailQueryMock.mockReturnValue({
      data: baseLead({ aiProcessingStatus: "Pending" }),
      isLoading: false,
      error: null,
      isSuccess: true,
    })

    renderPage()

    expect(screen.getByText(/still being processed/i)).toBeInTheDocument()
    expect(screen.getByText(/qualification will be available/i)).toBeInTheDocument()
  })

  it("shows an explicit failure message when AI processing failed", () => {
    useSessionRecoveryMock.mockReturnValue({ state: { kind: "ok" }, retry: vi.fn() })
    useLeadDetailQueryMock.mockReturnValue({
      data: baseLead({ aiProcessingStatus: "Failed" }),
      isLoading: false,
      error: null,
      isSuccess: true,
    })

    renderPage()

    expect(screen.getByText(/ai processing failed for this lead/i)).toBeInTheDocument()
    expect(screen.getByText(/qualification could not run/i)).toBeInTheDocument()
  })

  it("renders full analysis and qualification when completed", () => {
    useSessionRecoveryMock.mockReturnValue({ state: { kind: "ok" }, retry: vi.fn() })
    useLeadDetailQueryMock.mockReturnValue({
      data: baseLead({
        aiProcessingStatus: "Completed",
        analysis: {
          requestedServices: ["CatchUpBookkeeping"],
          extractedClientType: null,
          extractedApproximateAnnualRevenue: null,
          extractedAccountingSoftware: null,
          urgency: "High",
          extractedFilingDeadline: null,
          notes: "Client needs urgent bookkeeping catch-up.",
          estimatedDeadlineInDays: -5,
          bookkeepingMonthsBehind: 6,
          createdAtUtc: "2026-09-13T00:00:00Z",
        },
        qualification: {
          score: 90,
          priority: "High",
          reasons: ["High urgency."],
          rulesVersionApplied: "v1",
          createdAtUtc: "2026-09-13T00:00:00Z",
        },
      }),
      isLoading: false,
      error: null,
      isSuccess: true,
    })

    renderPage()

    expect(screen.getByText(/client needs urgent bookkeeping catch-up/i)).toBeInTheDocument()
    expect(screen.getByText(/5 days overdue/i)).toBeInTheDocument()
    expect(screen.getByText(/high urgency\./i)).toBeInTheDocument()
    expect(screen.getByText(/score:/i).closest("p")).toHaveTextContent("90")
  })

  it("shows a not-found message when the lead does not exist", () => {
    useSessionRecoveryMock.mockReturnValue({ state: { kind: "ok" }, retry: vi.fn() })
    useLeadDetailQueryMock.mockReturnValue({
      data: null,
      isLoading: false,
      error: null,
      isSuccess: true,
    })

    renderPage()

    expect(screen.getByText(/lead not found/i)).toBeInTheDocument()
  })

  it("renders the SessionRecoveryNotice when the session state is not ok", () => {
    useSessionRecoveryMock.mockReturnValue({ state: { kind: "notAuthorized" }, retry: vi.fn() })
    useLeadDetailQueryMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      error: new Error("NOT_AUTHORIZED"),
      isSuccess: false,
    })

    renderPage()

    expect(screen.getByText(/not authorized to view leads/i)).toBeInTheDocument()
  })
})

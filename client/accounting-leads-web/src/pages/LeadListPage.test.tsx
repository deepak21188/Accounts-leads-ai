import { screen } from "@testing-library/react"
import userEvent from "@testing-library/user-event"
import { MemoryRouter } from "react-router"
import { afterEach, describe, expect, it, vi } from "vitest"

import { LeadListPage } from "@/pages/LeadListPage"
import { renderWithQueryClient } from "@/test/renderWithQueryClient"
import type { LeadListItem, PagedResult } from "@/types/lead"

const { useLeadsQueryMock, useSessionRecoveryMock, navigateMock } = vi.hoisted(() => ({
  useLeadsQueryMock: vi.fn(),
  useSessionRecoveryMock: vi.fn(),
  navigateMock: vi.fn(),
}))

vi.mock("@/api/useLeadsQuery", () => ({
  useLeadsQuery: useLeadsQueryMock,
}))

vi.mock("@/auth/useSessionRecovery", () => ({
  useSessionRecovery: useSessionRecoveryMock,
}))

vi.mock("react-router", async (importOriginal) => {
  const actual = await importOriginal<typeof import("react-router")>()
  return { ...actual, useNavigate: () => navigateMock }
})

function pagedResult(items: LeadListItem[], overrides: Partial<PagedResult<LeadListItem>> = {}): PagedResult<LeadListItem> {
  return { items, totalCount: items.length, page: 1, pageSize: 20, totalPages: 1, ...overrides }
}

function renderPage() {
  return renderWithQueryClient(
    <MemoryRouter>
      <LeadListPage />
    </MemoryRouter>
  )
}

describe("LeadListPage", () => {
  afterEach(() => {
    vi.clearAllMocks()
  })

  it("renders leads with status, AI-processing, and priority badges", () => {
    useSessionRecoveryMock.mockReturnValue({ state: { kind: "ok" }, retry: vi.fn() })
    useLeadsQueryMock.mockReturnValue({
      data: pagedResult([
        {
          id: "1",
          name: "Jane Accountant",
          companyName: "Jane's Bakery",
          leadStatus: "Qualified",
          aiProcessingStatus: "Completed",
          priority: "High",
          createdAtUtc: "2026-09-13T00:00:00Z",
        },
      ]),
      isLoading: false,
      error: null,
      isSuccess: true,
    })

    renderPage()

    expect(screen.getByText("Jane's Bakery")).toBeInTheDocument()
    expect(screen.getByText("Qualified")).toBeInTheDocument()
    expect(screen.getByText("Completed")).toBeInTheDocument()
    expect(screen.getByText("High")).toBeInTheDocument()
  })

  it("navigates to the lead detail page when a row is clicked", async () => {
    const user = userEvent.setup()
    useSessionRecoveryMock.mockReturnValue({ state: { kind: "ok" }, retry: vi.fn() })
    useLeadsQueryMock.mockReturnValue({
      data: pagedResult([
        {
          id: "lead-1",
          name: "Jane Accountant",
          companyName: null,
          leadStatus: "New",
          aiProcessingStatus: "Pending",
          priority: null,
          createdAtUtc: "2026-09-13T00:00:00Z",
        },
      ]),
      isLoading: false,
      error: null,
      isSuccess: true,
    })

    renderPage()

    await user.click(screen.getByText("Jane Accountant"))

    expect(navigateMock).toHaveBeenCalledWith("/accountant/leads/lead-1")
  })

  it("updates search and resets to page 1 when the filter changes", async () => {
    const user = userEvent.setup()
    useSessionRecoveryMock.mockReturnValue({ state: { kind: "ok" }, retry: vi.fn() })
    useLeadsQueryMock.mockReturnValue({
      data: pagedResult([]),
      isLoading: false,
      error: null,
      isSuccess: true,
    })

    renderPage()

    await user.type(screen.getByPlaceholderText(/search name, company, or email/i), "acme")

    const lastCall = useLeadsQueryMock.mock.calls.at(-1)?.[0]
    expect(lastCall).toMatchObject({ search: "acme", page: 1 })
  })

  it("disables Previous on the first page and Next on the last page", () => {
    useSessionRecoveryMock.mockReturnValue({ state: { kind: "ok" }, retry: vi.fn() })
    useLeadsQueryMock.mockReturnValue({
      data: pagedResult([], { page: 1, totalPages: 1, totalCount: 0 }),
      isLoading: false,
      error: null,
      isSuccess: true,
    })

    renderPage()

    expect(screen.getByRole("button", { name: /previous/i })).toBeDisabled()
    expect(screen.getByRole("button", { name: /next/i })).toBeDisabled()
  })

  it("renders the SessionRecoveryNotice when the session state is not ok", () => {
    useSessionRecoveryMock.mockReturnValue({ state: { kind: "notAuthorized" }, retry: vi.fn() })
    useLeadsQueryMock.mockReturnValue({
      data: undefined,
      isLoading: false,
      error: new Error("NOT_AUTHORIZED"),
      isSuccess: false,
    })

    renderPage()

    expect(screen.getByText(/not authorized to view leads/i)).toBeInTheDocument()
  })

  it("sorts ascending on first header click and resets to page 1", async () => {
    const user = userEvent.setup()
    useSessionRecoveryMock.mockReturnValue({ state: { kind: "ok" }, retry: vi.fn() })
    useLeadsQueryMock.mockReturnValue({
      data: pagedResult([], { page: 2 }),
      isLoading: false,
      error: null,
      isSuccess: true,
    })

    renderPage()

    await user.click(screen.getByRole("button", { name: /name \/ company/i }))

    const lastCall = useLeadsQueryMock.mock.calls.at(-1)?.[0]
    expect(lastCall).toMatchObject({ sortBy: "name", sortDescending: false, page: 1 })
  })

  it("toggles to descending on a second click of the same column", async () => {
    const user = userEvent.setup()
    useSessionRecoveryMock.mockReturnValue({ state: { kind: "ok" }, retry: vi.fn() })
    useLeadsQueryMock.mockReturnValue({
      data: pagedResult([]),
      isLoading: false,
      error: null,
      isSuccess: true,
    })

    renderPage()

    const nameHeader = screen.getByRole("button", { name: /name \/ company/i })
    await user.click(nameHeader)
    await user.click(nameHeader)

    const lastCall = useLeadsQueryMock.mock.calls.at(-1)?.[0]
    expect(lastCall).toMatchObject({ sortBy: "name", sortDescending: true })
  })

  it("resets to ascending when switching to a different column", async () => {
    const user = userEvent.setup()
    useSessionRecoveryMock.mockReturnValue({ state: { kind: "ok" }, retry: vi.fn() })
    useLeadsQueryMock.mockReturnValue({
      data: pagedResult([]),
      isLoading: false,
      error: null,
      isSuccess: true,
    })

    renderPage()

    await user.click(screen.getByRole("button", { name: /name \/ company/i }))
    await user.click(screen.getByRole("button", { name: /name \/ company/i }))
    await user.click(screen.getByRole("button", { name: "Status" }))

    const lastCall = useLeadsQueryMock.mock.calls.at(-1)?.[0]
    expect(lastCall).toMatchObject({ sortBy: "status", sortDescending: false })
  })
})

import { renderHook, waitFor } from "@testing-library/react"
import { QueryClient, QueryClientProvider } from "@tanstack/react-query"
import type { ReactNode } from "react"
import { afterEach, describe, expect, it, vi } from "vitest"

import { useLeadsQuery } from "@/api/useLeadsQuery"

const { useMsalMock, getLeadsMock } = vi.hoisted(() => ({
  useMsalMock: vi.fn(),
  getLeadsMock: vi.fn(),
}))

vi.mock("@azure/msal-react", () => ({
  useMsal: useMsalMock,
}))

vi.mock("@/api/leadsApi", () => ({
  getLeads: getLeadsMock,
}))

function wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
}

describe("useLeadsQuery", () => {
  afterEach(() => {
    vi.clearAllMocks()
  })

  const EMPTY_PAGE = { items: [], totalCount: 0, page: 1, pageSize: 20, totalPages: 0 }

  it("fetches with the active account, not accounts[0], when they differ", async () => {
    const activeAccount = { homeAccountId: "active-account-id" }
    const otherAccount = { homeAccountId: "other-account-id" }
    const instance = {
      getActiveAccount: vi.fn().mockReturnValue(activeAccount),
    }
    useMsalMock.mockReturnValue({ instance, accounts: [otherAccount, activeAccount] })
    getLeadsMock.mockResolvedValue(EMPTY_PAGE)

    renderHook(() => useLeadsQuery(), { wrapper })

    await waitFor(() => expect(getLeadsMock).toHaveBeenCalledTimes(1))
    expect(getLeadsMock).toHaveBeenCalledWith(instance, activeAccount, {})
  })

  it("falls back to accounts[0] when there is no active account yet", async () => {
    const onlyAccount = { homeAccountId: "only-account-id" }
    const instance = {
      getActiveAccount: vi.fn().mockReturnValue(null),
    }
    useMsalMock.mockReturnValue({ instance, accounts: [onlyAccount] })
    getLeadsMock.mockResolvedValue(EMPTY_PAGE)

    renderHook(() => useLeadsQuery(), { wrapper })

    await waitFor(() => expect(getLeadsMock).toHaveBeenCalledTimes(1))
    expect(getLeadsMock).toHaveBeenCalledWith(instance, onlyAccount, {})
  })

  it("passes filters through to getLeads and includes them in the query key", async () => {
    const account = { homeAccountId: "account-id" }
    const instance = { getActiveAccount: vi.fn().mockReturnValue(account) }
    useMsalMock.mockReturnValue({ instance, accounts: [account] })
    getLeadsMock.mockResolvedValue(EMPTY_PAGE)

    const filters = { status: "Qualified", priority: "High", search: "acme", page: 2, pageSize: 10 }
    renderHook(() => useLeadsQuery(filters), { wrapper })

    await waitFor(() => expect(getLeadsMock).toHaveBeenCalledTimes(1))
    expect(getLeadsMock).toHaveBeenCalledWith(instance, account, filters)
  })
})

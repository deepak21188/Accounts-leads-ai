import { renderHook, waitFor } from "@testing-library/react"
import { QueryClient, QueryClientProvider } from "@tanstack/react-query"
import type { ReactNode } from "react"
import { afterEach, describe, expect, it, vi } from "vitest"

import { useLeadDetailQuery } from "@/api/useLeadDetailQuery"

const { useMsalMock, getLeadDetailMock } = vi.hoisted(() => ({
  useMsalMock: vi.fn(),
  getLeadDetailMock: vi.fn(),
}))

vi.mock("@azure/msal-react", () => ({
  useMsal: useMsalMock,
}))

vi.mock("@/api/leadsApi", () => ({
  getLeadDetail: getLeadDetailMock,
}))

function wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
}

describe("useLeadDetailQuery", () => {
  afterEach(() => {
    vi.clearAllMocks()
  })

  it("fetches the lead detail with the active account and given id", async () => {
    const account = { homeAccountId: "account-id" }
    const instance = { getActiveAccount: vi.fn().mockReturnValue(account) }
    useMsalMock.mockReturnValue({ instance, accounts: [account] })
    getLeadDetailMock.mockResolvedValue(null)

    renderHook(() => useLeadDetailQuery("lead-1"), { wrapper })

    await waitFor(() => expect(getLeadDetailMock).toHaveBeenCalledTimes(1))
    expect(getLeadDetailMock).toHaveBeenCalledWith(instance, account, "lead-1")
  })

  it("does not fetch when leadId is empty", () => {
    const account = { homeAccountId: "account-id" }
    const instance = { getActiveAccount: vi.fn().mockReturnValue(account) }
    useMsalMock.mockReturnValue({ instance, accounts: [account] })

    renderHook(() => useLeadDetailQuery(""), { wrapper })

    expect(getLeadDetailMock).not.toHaveBeenCalled()
  })
})

import { act, renderHook, waitFor } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"

import { REAUTH_ATTEMPTED_STORAGE_KEY, useSessionRecovery } from "@/auth/useSessionRecovery"

const { useMsalMock } = vi.hoisted(() => ({
  useMsalMock: vi.fn(),
}))

vi.mock("@azure/msal-react", () => ({
  useMsal: useMsalMock,
}))

function mockInstance(loginRedirect: ReturnType<typeof vi.fn> = vi.fn().mockResolvedValue(undefined)) {
  const instance = { loginRedirect }
  useMsalMock.mockReturnValue({ instance })
  return instance
}

describe("useSessionRecovery", () => {
  afterEach(() => {
    sessionStorage.clear()
    vi.clearAllMocks()
  })

  it("triggers a single interactive re-login when the error is SESSION_EXPIRED", () => {
    const instance = mockInstance()

    const { result } = renderHook(() => useSessionRecovery(new Error("SESSION_EXPIRED"), false))

    expect(result.current.state).toEqual({ kind: "redirecting" })
    expect(instance.loginRedirect).toHaveBeenCalledTimes(1)
    expect(sessionStorage.getItem(REAUTH_ATTEMPTED_STORAGE_KEY)).toBe("true")
  })

  it("does not auto-redirect again after landing back with SESSION_EXPIRED a second time", () => {
    sessionStorage.setItem(REAUTH_ATTEMPTED_STORAGE_KEY, "true")
    const instance = mockInstance()

    const { result } = renderHook(() => useSessionRecovery(new Error("SESSION_EXPIRED"), false))

    expect(instance.loginRedirect).not.toHaveBeenCalled()
    expect(result.current.state).toEqual({ kind: "reauthFailed" })
  })

  it("offers a manual retry instead of staying in 'redirecting' forever when loginRedirect itself rejects", async () => {
    mockInstance(vi.fn().mockRejectedValue(new Error("interaction_in_progress")))

    const { result } = renderHook(() => useSessionRecovery(new Error("SESSION_EXPIRED"), false))

    await waitFor(() => expect(result.current.state).toEqual({ kind: "reauthFailed" }))
  })

  it("clears the reauth-attempted flag once the query succeeds", () => {
    sessionStorage.setItem(REAUTH_ATTEMPTED_STORAGE_KEY, "true")
    mockInstance()

    const { result } = renderHook(({ isSuccess }) => useSessionRecovery(null, isSuccess), {
      initialProps: { isSuccess: true },
    })

    expect(result.current.state).toEqual({ kind: "ok" })
    expect(sessionStorage.getItem(REAUTH_ATTEMPTED_STORAGE_KEY)).toBeNull()
  })

  it("maps NOT_AUTHORIZED without redirecting", () => {
    const instance = mockInstance()

    const { result } = renderHook(() => useSessionRecovery(new Error("NOT_AUTHORIZED"), false))

    expect(result.current.state).toEqual({ kind: "notAuthorized" })
    expect(instance.loginRedirect).not.toHaveBeenCalled()
  })

  it("retry() clears the flag and attempts loginRedirect again", () => {
    sessionStorage.setItem(REAUTH_ATTEMPTED_STORAGE_KEY, "true")
    const instance = mockInstance()

    const { result } = renderHook(() => useSessionRecovery(new Error("SESSION_EXPIRED"), false))
    expect(result.current.state).toEqual({ kind: "reauthFailed" })

    act(() => {
      result.current.retry()
    })

    expect(instance.loginRedirect).toHaveBeenCalledTimes(1)
    expect(sessionStorage.getItem(REAUTH_ATTEMPTED_STORAGE_KEY)).toBe("true")
  })
})

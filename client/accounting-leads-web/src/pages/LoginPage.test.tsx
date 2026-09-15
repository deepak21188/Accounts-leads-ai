import { act, render, screen, waitFor } from "@testing-library/react"
import userEvent from "@testing-library/user-event"
import { MemoryRouter } from "react-router"
import { EventType } from "@azure/msal-browser"
import { afterEach, describe, expect, it, vi } from "vitest"

import { LoginPage } from "@/pages/LoginPage"
import { LOGIN_ERROR_STORAGE_KEY } from "@/auth/msalInstance"

const { useMsalMock, useIsAuthenticatedMock } = vi.hoisted(() => ({
  useMsalMock: vi.fn(),
  useIsAuthenticatedMock: vi.fn(),
}))

vi.mock("@azure/msal-react", () => ({
  useMsal: useMsalMock,
  useIsAuthenticated: useIsAuthenticatedMock,
}))

type EventCallback = (event: { eventType: string; error?: { message: string } | null }) => void

function renderLoginPage(instanceOverrides: { loginRedirect?: ReturnType<typeof vi.fn> } = {}) {
  let registeredCallback: EventCallback | undefined
  const instance = {
    loginRedirect: vi.fn().mockResolvedValue(undefined),
    addEventCallback: vi.fn((callback: EventCallback) => {
      registeredCallback = callback
      return "callback-id"
    }),
    removeEventCallback: vi.fn(),
    ...instanceOverrides,
  }
  useMsalMock.mockReturnValue({ instance })
  useIsAuthenticatedMock.mockReturnValue(false)

  render(
    <MemoryRouter>
      <LoginPage />
    </MemoryRouter>
  )

  return { instance, fireEvent: (event: Parameters<EventCallback>[0]) => registeredCallback?.(event) }
}

describe("LoginPage", () => {
  afterEach(() => {
    sessionStorage.clear()
  })

  it("shows and clears a login error already left by msalInstance's ACQUIRE_TOKEN_FAILURE handler before mount", () => {
    sessionStorage.setItem(LOGIN_ERROR_STORAGE_KEY, "AADSTS50105: user is not assigned.")

    renderLoginPage()

    expect(screen.getByText("AADSTS50105: user is not assigned.")).toBeInTheDocument()
    expect(sessionStorage.getItem(LOGIN_ERROR_STORAGE_KEY)).toBeNull()
  })

  it("shows an error fired by ACQUIRE_TOKEN_FAILURE after this page has already mounted", () => {
    const { fireEvent } = renderLoginPage()

    expect(screen.queryByText(/sign-in failed/i)).not.toBeInTheDocument()

    act(() => {
      fireEvent({ eventType: EventType.ACQUIRE_TOKEN_FAILURE, error: { message: "AADSTS500011: resource not found." } })
    })

    expect(screen.getByText("AADSTS500011: resource not found.")).toBeInTheDocument()
  })

  it("shows an error when loginRedirect's own promise rejects", async () => {
    const user = userEvent.setup()
    const { instance } = renderLoginPage({
      loginRedirect: vi.fn().mockRejectedValue(new Error("interaction_in_progress")),
    })

    await user.click(screen.getByRole("button", { name: /sign in with microsoft/i }))

    expect(instance.loginRedirect).toHaveBeenCalledTimes(1)
    await waitFor(() => expect(screen.getByText("interaction_in_progress")).toBeInTheDocument())
  })
})

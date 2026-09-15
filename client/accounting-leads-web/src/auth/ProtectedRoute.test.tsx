import { render, screen } from "@testing-library/react"
import { MemoryRouter, Route, Routes } from "react-router"
import { describe, expect, it, vi } from "vitest"

import { ProtectedRoute } from "@/auth/ProtectedRoute"

const { useIsAuthenticatedMock } = vi.hoisted(() => ({
  useIsAuthenticatedMock: vi.fn(),
}))

vi.mock("@azure/msal-react", () => ({
  useIsAuthenticated: useIsAuthenticatedMock,
}))

function renderProtectedRoute() {
  return render(
    <MemoryRouter initialEntries={["/accountant"]}>
      <Routes>
        <Route path="/accountant/login" element={<p>Login page</p>} />
        <Route path="/accountant" element={<ProtectedRoute />}>
          <Route index element={<p>Dashboard content</p>} />
        </Route>
      </Routes>
    </MemoryRouter>
  )
}

describe("ProtectedRoute", () => {
  it("redirects to /accountant/login when the user is not authenticated", () => {
    useIsAuthenticatedMock.mockReturnValue(false)

    renderProtectedRoute()

    expect(screen.getByText("Login page")).toBeInTheDocument()
    expect(screen.queryByText("Dashboard content")).not.toBeInTheDocument()
  })

  it("renders the protected content when the user is authenticated", () => {
    useIsAuthenticatedMock.mockReturnValue(true)

    renderProtectedRoute()

    expect(screen.getByText("Dashboard content")).toBeInTheDocument()
    expect(screen.queryByText("Login page")).not.toBeInTheDocument()
  })
})

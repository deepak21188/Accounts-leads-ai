import { screen } from "@testing-library/react"
import { describe, expect, it } from "vitest"
import { MemoryRouter } from "react-router"

import App from "@/App"
import { renderWithQueryClient } from "@/test/renderWithQueryClient"

describe("App", () => {
  it("renders the lead capture form at the public route", () => {
    renderWithQueryClient(
      <MemoryRouter initialEntries={["/"]}>
        <App />
      </MemoryRouter>
    )

    expect(screen.getByRole("heading", { name: /talk to an accountant/i })).toBeInTheDocument()
    expect(screen.getByLabelText(/full name/i)).toBeInTheDocument()
    expect(screen.getByRole("button", { name: /submit inquiry/i })).toBeInTheDocument()
  })
})

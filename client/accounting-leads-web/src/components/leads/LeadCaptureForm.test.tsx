import { screen, waitFor } from "@testing-library/react"
import userEvent from "@testing-library/user-event"
import { afterEach, describe, expect, it, vi } from "vitest"

import { LeadCaptureForm } from "@/components/leads/LeadCaptureForm"
import { createLead, LeadValidationError } from "@/api/leadsApi"
import { renderWithQueryClient } from "@/test/renderWithQueryClient"
import type { CreateLeadResponse } from "@/types/lead"

vi.mock("@/api/leadsApi", async (importOriginal) => {
  const actual = await importOriginal<typeof import("@/api/leadsApi")>()
  return {
    ...actual,
    createLead: vi.fn(),
  }
})

const mockedCreateLead = vi.mocked(createLead)

async function fillRequiredFields(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText(/full name/i), "Jane Accountant")
  await user.type(screen.getByLabelText(/^email/i), "jane@example.com")
  await user.type(
    screen.getByLabelText(/what can we help you with/i),
    "I need help with catch-up bookkeeping."
  )
}

describe("LeadCaptureForm", () => {
  afterEach(() => {
    mockedCreateLead.mockReset()
  })

  it("blocks submission and shows inline errors when required fields are empty", async () => {
    const user = userEvent.setup()
    renderWithQueryClient(<LeadCaptureForm />)

    await user.click(screen.getByRole("button", { name: /submit inquiry/i }))

    expect(await screen.findByText("Name is required.")).toBeInTheDocument()
    expect(screen.getByText("Email is required.")).toBeInTheDocument()
    expect(
      screen.getByText("Tell us a bit about what you need help with.")
    ).toBeInTheDocument()
    expect(mockedCreateLead).not.toHaveBeenCalled()
  })

  it("submits successfully and shows the confirmation view", async () => {
    const response: CreateLeadResponse = {
      id: "11111111-1111-1111-1111-111111111111",
      leadStatus: "New",
      aiProcessingStatus: "Pending",
    }
    mockedCreateLead.mockResolvedValueOnce(response)

    const user = userEvent.setup()
    renderWithQueryClient(<LeadCaptureForm />)

    await fillRequiredFields(user)
    await user.click(screen.getByRole("button", { name: /submit inquiry/i }))

    expect(await screen.findByText(/thanks — we've got it/i)).toBeInTheDocument()
    expect(mockedCreateLead).toHaveBeenCalledTimes(1)
  })

  it("surfaces field errors from a LeadValidationError returned by the server", async () => {
    mockedCreateLead.mockRejectedValueOnce(
      new LeadValidationError({ email: "That email address is already in use." })
    )

    const user = userEvent.setup()
    renderWithQueryClient(<LeadCaptureForm />)

    await fillRequiredFields(user)
    await user.click(screen.getByRole("button", { name: /submit inquiry/i }))

    expect(
      await screen.findByText("That email address is already in use.")
    ).toBeInTheDocument()
  })

  it("shows a top-level error banner for a LeadValidationError carrying only general (non-field) errors", async () => {
    mockedCreateLead.mockRejectedValueOnce(
      new LeadValidationError({}, ["Email must be a valid email address."])
    )

    const user = userEvent.setup()
    renderWithQueryClient(<LeadCaptureForm />)

    await fillRequiredFields(user)
    await user.click(screen.getByRole("button", { name: /submit inquiry/i }))

    expect(
      await screen.findByText("Email must be a valid email address.")
    ).toBeInTheDocument()
  })

  it("shows a top-level error banner when submission fails for a non-validation reason", async () => {
    mockedCreateLead.mockRejectedValueOnce(
      new Error("Could not reach the server. Check your connection and try again.")
    )

    const user = userEvent.setup()
    renderWithQueryClient(<LeadCaptureForm />)

    await fillRequiredFields(user)
    await user.click(screen.getByRole("button", { name: /submit inquiry/i }))

    await waitFor(() => {
      expect(
        screen.getByText("Could not reach the server. Check your connection and try again.")
      ).toBeInTheDocument()
    })
  })
})

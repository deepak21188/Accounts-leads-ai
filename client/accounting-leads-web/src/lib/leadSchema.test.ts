import { describe, expect, it } from "vitest"

import { leadFormSchema } from "@/lib/leadSchema"

function minimalInput() {
  return {
    name: "Jane Accountant",
    email: "jane@example.com",
    inquiry: "I need help with catch-up bookkeeping.",
  }
}

describe("leadFormSchema", () => {
  it("accepts the minimum required fields and leaves optionals undefined", () => {
    const result = leadFormSchema.parse(minimalInput())

    expect(result).toEqual({
      ...minimalInput(),
      phone: undefined,
      companyName: undefined,
      clientType: undefined,
      approximateAnnualRevenue: undefined,
      accountingSoftware: undefined,
    })
  })

  it("accepts a fully populated, valid submission and coerces revenue to a number", () => {
    const result = leadFormSchema.parse({
      ...minimalInput(),
      phone: "555-0100",
      companyName: "Foundco LLC",
      clientType: "LimitedLiabilityCompany",
      approximateAnnualRevenue: "750000",
      accountingSoftware: "QuickBooks",
    })

    expect(result.approximateAnnualRevenue).toBe(750000)
    expect(result.clientType).toBe("LimitedLiabilityCompany")
  })

  it("treats blank optional fields as undefined rather than empty strings", () => {
    const result = leadFormSchema.parse({
      ...minimalInput(),
      phone: "   ",
      companyName: "",
      approximateAnnualRevenue: "",
    })

    expect(result.phone).toBeUndefined()
    expect(result.companyName).toBeUndefined()
    expect(result.approximateAnnualRevenue).toBeUndefined()
  })

  it.each(["name", "email", "inquiry"] as const)("rejects a missing %s", (field) => {
    const input = { ...minimalInput(), [field]: "" }
    const result = leadFormSchema.safeParse(input)

    expect(result.success).toBe(false)
  })

  it("rejects an invalid email format", () => {
    const result = leadFormSchema.safeParse({ ...minimalInput(), email: "not-an-email" })

    expect(result.success).toBe(false)
  })

  it("rejects negative revenue", () => {
    const result = leadFormSchema.safeParse({
      ...minimalInput(),
      approximateAnnualRevenue: "-1",
    })

    expect(result.success).toBe(false)
  })

  it("rejects a non-numeric revenue value", () => {
    const result = leadFormSchema.safeParse({
      ...minimalInput(),
      approximateAnnualRevenue: "not-a-number",
    })

    expect(result.success).toBe(false)
  })

  it("rejects a clientType outside the known set", () => {
    const result = leadFormSchema.safeParse({ ...minimalInput(), clientType: "Trust" })

    expect(result.success).toBe(false)
  })
})

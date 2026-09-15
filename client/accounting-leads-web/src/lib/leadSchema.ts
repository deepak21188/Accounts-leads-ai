import { z } from "zod"

import { CLIENT_TYPES } from "@/types/lead"

// Mirrors server/src/AccountingLeads.Domain/Leads/Lead.cs and
// AccountingLeads.Api/Contracts/CreateLeadRequest.cs. UX-only pre-check — the API remains
// the source of truth for validation.
export const NAME_MAX_LENGTH = 150
export const EMAIL_MAX_LENGTH = 254
export const PHONE_MAX_LENGTH = 50
export const COMPANY_NAME_MAX_LENGTH = 200
export const ACCOUNTING_SOFTWARE_MAX_LENGTH = 100
export const INQUIRY_MAX_LENGTH = 4000
// Lead.MaxApproximateAnnualRevenueLiteral is "9999999999999999.99", which exceeds
// Number.MAX_SAFE_INTEGER and can't be represented exactly as a JS number. This UX-only
// bound uses the closest safe value — the API remains the authoritative check.
export const MAX_APPROXIMATE_ANNUAL_REVENUE = Number.MAX_SAFE_INTEGER

// Trims an optional text field down to `undefined` when blank, keeping the input type a
// plain `string | undefined` (rather than `unknown`, which z.preprocess/z.coerce would give)
// so React Hook Form's field values stay cleanly typed.
const optionalText = (max: number, message: string) =>
  z
    .string()
    .max(max, message)
    .optional()
    .transform((value) => {
      const trimmed = value?.trim()
      return trimmed ? trimmed : undefined
    })

export const leadFormSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, "Name is required.")
    .max(NAME_MAX_LENGTH, `Name must be ${NAME_MAX_LENGTH} characters or fewer.`),
  email: z
    .string()
    .trim()
    .min(1, "Email is required.")
    .max(EMAIL_MAX_LENGTH, `Email must be ${EMAIL_MAX_LENGTH} characters or fewer.`)
    .refine((value) => z.email().safeParse(value).success, "Enter a valid email address."),
  phone: optionalText(PHONE_MAX_LENGTH, `Phone must be ${PHONE_MAX_LENGTH} characters or fewer.`),
  companyName: optionalText(
    COMPANY_NAME_MAX_LENGTH,
    `Company name must be ${COMPANY_NAME_MAX_LENGTH} characters or fewer.`
  ),
  clientType: z
    .union([z.enum(CLIENT_TYPES), z.literal("")])
    .optional()
    .transform((value) => (value ? value : undefined)),
  approximateAnnualRevenue: z
    .string()
    .optional()
    .transform((value, ctx) => {
      const trimmed = value?.trim()
      if (!trimmed) return undefined

      const parsed = Number(trimmed)
      if (Number.isNaN(parsed)) {
        ctx.addIssue({ code: "custom", message: "Enter a valid amount." })
        return z.NEVER
      }
      if (parsed < 0) {
        ctx.addIssue({ code: "custom", message: "Revenue cannot be negative." })
        return z.NEVER
      }
      if (parsed > MAX_APPROXIMATE_ANNUAL_REVENUE) {
        ctx.addIssue({ code: "custom", message: "That value is too large." })
        return z.NEVER
      }
      return parsed
    }),
  accountingSoftware: optionalText(
    ACCOUNTING_SOFTWARE_MAX_LENGTH,
    `Accounting software must be ${ACCOUNTING_SOFTWARE_MAX_LENGTH} characters or fewer.`
  ),
  inquiry: z
    .string()
    .trim()
    .min(1, "Tell us a bit about what you need help with.")
    .max(INQUIRY_MAX_LENGTH, `Inquiry must be ${INQUIRY_MAX_LENGTH} characters or fewer.`),
})

// Raw form field values, before Zod's trimming/coercion (what React Hook Form holds).
export type LeadFormInput = z.input<typeof leadFormSchema>
// Validated, coerced payload — this is the exact CreateLeadRequest shape the API expects.
export type CreateLeadRequest = z.output<typeof leadFormSchema>

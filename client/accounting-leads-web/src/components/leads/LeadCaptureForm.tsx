import { useId } from "react"
import { Controller, useForm } from "react-hook-form"
import { zodResolver } from "@hookform/resolvers/zod"
import { CheckCircle2, Loader2 } from "lucide-react"
import type { ReactNode } from "react"

import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Textarea } from "@/components/ui/textarea"
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select"
import { LeadValidationError } from "@/api/leadsApi"
import { useCreateLeadMutation } from "@/api/useCreateLeadMutation"
import { leadFormSchema, type CreateLeadRequest, type LeadFormInput } from "@/lib/leadSchema"
import { CLIENT_TYPES, CLIENT_TYPE_LABELS } from "@/types/lead"

const DEFAULT_VALUES: LeadFormInput = {
  name: "",
  email: "",
  phone: "",
  companyName: "",
  clientType: "",
  approximateAnnualRevenue: "",
  accountingSoftware: "",
  inquiry: "",
}

export function LeadCaptureForm() {
  const formId = useId()
  const {
    register,
    control,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<LeadFormInput, unknown, CreateLeadRequest>({
    resolver: zodResolver(leadFormSchema),
    defaultValues: DEFAULT_VALUES,
  })

  const mutation = useCreateLeadMutation()

  function onSubmit(payload: CreateLeadRequest) {
    mutation.mutate(payload, {
      onError: (error) => {
        if (error instanceof LeadValidationError) {
          for (const [field, message] of Object.entries(error.fieldErrors)) {
            setError(field as keyof LeadFormInput, { message })
          }
        }
      },
    })
  }

  if (mutation.isSuccess) {
    return (
      <div className="flex flex-col items-center gap-3 py-10 text-center">
        <CheckCircle2 className="size-10 text-emerald-600" />
        <h2 className="text-lg font-semibold text-foreground">Thanks — we've got it.</h2>
        <p className="max-w-sm text-sm text-muted-foreground">
          Your inquiry has been received. One of our accountants will review the details and
          get back to you shortly.
        </p>
      </div>
    )
  }

  const formError = mutation.isError
    ? mutation.error instanceof LeadValidationError
      ? mutation.error.generalErrors.join(" ") || null
      : mutation.error.message
    : null

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="flex flex-col gap-8">
      {formError && (
        <div
          role="alert"
          className="rounded-lg border border-destructive/30 bg-destructive/10 px-3 py-2 text-sm text-destructive"
        >
          {formError}
        </div>
      )}

      <section className="flex flex-col gap-4">
        <h2 className="text-sm font-semibold text-foreground">Contact information</h2>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field id={`${formId}-name`} label="Full name" error={errors.name?.message} required>
            <Input
              id={`${formId}-name`}
              autoComplete="name"
              aria-invalid={!!errors.name}
              {...register("name")}
            />
          </Field>
          <Field id={`${formId}-email`} label="Email" error={errors.email?.message} required>
            <Input
              id={`${formId}-email`}
              type="email"
              autoComplete="email"
              aria-invalid={!!errors.email}
              {...register("email")}
            />
          </Field>
          <Field id={`${formId}-phone`} label="Phone" error={errors.phone?.message}>
            <Input
              id={`${formId}-phone`}
              type="tel"
              autoComplete="tel"
              aria-invalid={!!errors.phone}
              {...register("phone")}
            />
          </Field>
        </div>
      </section>

      <section className="flex flex-col gap-4">
        <h2 className="text-sm font-semibold text-foreground">Your business</h2>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field
            id={`${formId}-companyName`}
            label="Company name"
            error={errors.companyName?.message}
          >
            <Input
              id={`${formId}-companyName`}
              aria-invalid={!!errors.companyName}
              {...register("companyName")}
            />
          </Field>
          <Field
            id={`${formId}-clientType`}
            label="Client type"
            error={errors.clientType?.message}
          >
            <Controller
              control={control}
              name="clientType"
              render={({ field }) => (
                <Select value={field.value ?? ""} onValueChange={field.onChange}>
                  <SelectTrigger
                    id={`${formId}-clientType`}
                    className="w-full"
                    aria-invalid={!!errors.clientType}
                  >
                    <SelectValue placeholder="Select an option" />
                  </SelectTrigger>
                  <SelectContent>
                    {CLIENT_TYPES.map((type) => (
                      <SelectItem key={type} value={type}>
                        {CLIENT_TYPE_LABELS[type]}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
          </Field>
          <Field
            id={`${formId}-approximateAnnualRevenue`}
            label="Approximate annual revenue"
            error={errors.approximateAnnualRevenue?.message}
          >
            <Input
              id={`${formId}-approximateAnnualRevenue`}
              type="number"
              min={0}
              inputMode="decimal"
              aria-invalid={!!errors.approximateAnnualRevenue}
              {...register("approximateAnnualRevenue")}
            />
          </Field>
          <Field
            id={`${formId}-accountingSoftware`}
            label="Accounting software"
            error={errors.accountingSoftware?.message}
          >
            <Input
              id={`${formId}-accountingSoftware`}
              placeholder="e.g. QuickBooks, Xero"
              aria-invalid={!!errors.accountingSoftware}
              {...register("accountingSoftware")}
            />
          </Field>
        </div>
      </section>

      <section className="flex flex-col gap-4">
        <h2 className="text-sm font-semibold text-foreground">Your inquiry</h2>
        <Field
          id={`${formId}-inquiry`}
          label="What can we help you with?"
          error={errors.inquiry?.message}
          required
        >
          <Textarea
            id={`${formId}-inquiry`}
            rows={5}
            placeholder="Tell us about your business and what you're looking for help with."
            aria-invalid={!!errors.inquiry}
            {...register("inquiry")}
          />
        </Field>
      </section>

      <Button type="submit" disabled={mutation.isPending} className="self-start">
        {mutation.isPending && <Loader2 className="animate-spin" />}
        {mutation.isPending ? "Submitting…" : "Submit inquiry"}
      </Button>
    </form>
  )
}

interface FieldProps {
  id: string
  label: string
  error?: string
  required?: boolean
  children: ReactNode
}

function Field({ id, label, error, required, children }: FieldProps) {
  return (
    <div className="flex flex-col gap-1.5">
      <Label htmlFor={id}>
        {label}
        {required && <span className="text-destructive"> *</span>}
      </Label>
      {children}
      {error && (
        <p className="text-xs text-destructive" role="alert">
          {error}
        </p>
      )}
    </div>
  )
}

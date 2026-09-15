import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { LeadCaptureForm } from "@/components/leads/LeadCaptureForm"

export function PublicLeadFormPage() {
  return (
    <div className="min-h-screen bg-muted/40">
      <div className="mx-auto flex max-w-2xl flex-col gap-6 px-4 py-12 sm:py-16">
        <header className="flex flex-col gap-2 text-center">
          <p className="text-sm font-medium text-primary">Accounting Leads</p>
          <h1 className="text-2xl font-semibold tracking-tight text-foreground sm:text-3xl">
            Talk to an accountant
          </h1>
          <p className="text-sm text-muted-foreground">
            Tell us about your business and we'll get back to you with next steps.
          </p>
        </header>

        <Card>
          <CardHeader>
            <CardTitle>Request a consultation</CardTitle>
            <CardDescription>
              Fields marked with an asterisk are required — everything else helps us prepare,
              but isn't necessary to get started.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <LeadCaptureForm />
          </CardContent>
        </Card>

        <footer className="text-center text-xs text-muted-foreground">
          Your information is reviewed by a member of our team before any response is sent.
        </footer>
      </div>
    </div>
  )
}

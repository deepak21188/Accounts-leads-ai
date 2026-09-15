import { Link, useParams } from "react-router"
import { useLeadDetailQuery } from "@/api/useLeadDetailQuery"
import { useSessionRecovery } from "@/auth/useSessionRecovery"
import { SessionRecoveryNotice } from "@/auth/SessionRecoveryNotice"
import {
  LEAD_STATUS_LABELS,
  LEAD_STATUS_BADGE_CLASSES,
  LEAD_PRIORITY_BADGE_CLASSES,
  type LeadDetail,
} from "@/types/lead"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card"

function AiAnalysisSection({ lead }: { lead: LeadDetail }) {
  if (lead.aiProcessingStatus === "Pending" || lead.aiProcessingStatus === "Processing") {
    return (
      <Card>
        <CardHeader>
          <CardTitle>AI Analysis</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-muted-foreground">This lead is still being processed by AI.</p>
        </CardContent>
      </Card>
    )
  }

  if (lead.aiProcessingStatus === "Failed") {
    return (
      <Card>
        <CardHeader>
          <CardTitle>AI Analysis</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-destructive">AI processing failed for this lead.</p>
        </CardContent>
      </Card>
    )
  }

  if (!lead.analysis) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>AI Analysis</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-muted-foreground">No analysis data is available for this lead.</p>
        </CardContent>
      </Card>
    )
  }

  const { analysis } = lead

  return (
    <Card>
      <CardHeader>
        <CardTitle>AI Analysis</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-2 text-sm">
        {analysis.requestedServices.length > 0 && (
          <p>
            <span className="font-medium">Requested services:</span> {analysis.requestedServices.join(", ")}
          </p>
        )}
        {analysis.extractedClientType && (
          <p>
            <span className="font-medium">Client type:</span> {analysis.extractedClientType}
          </p>
        )}
        {analysis.extractedApproximateAnnualRevenue != null && (
          <p>
            <span className="font-medium">Approximate revenue:</span> ${analysis.extractedApproximateAnnualRevenue.toLocaleString()}
          </p>
        )}
        {analysis.extractedAccountingSoftware && (
          <p>
            <span className="font-medium">Accounting software:</span> {analysis.extractedAccountingSoftware}
          </p>
        )}
        {analysis.urgency && (
          <p>
            <span className="font-medium">Urgency:</span> {analysis.urgency}
          </p>
        )}
        {analysis.extractedFilingDeadline && (
          <p>
            <span className="font-medium">Filing deadline:</span> {analysis.extractedFilingDeadline}
          </p>
        )}
        {analysis.estimatedDeadlineInDays != null && (
          <p>
            <span className="font-medium">Estimated deadline:</span>{" "}
            {analysis.estimatedDeadlineInDays < 0
              ? `${Math.abs(analysis.estimatedDeadlineInDays)} days overdue`
              : `${analysis.estimatedDeadlineInDays} days`}
          </p>
        )}
        {analysis.bookkeepingMonthsBehind != null && (
          <p>
            <span className="font-medium">Bookkeeping months behind:</span> {analysis.bookkeepingMonthsBehind}
          </p>
        )}
        {analysis.notes && (
          <p>
            <span className="font-medium">Summary:</span> {analysis.notes}
          </p>
        )}
      </CardContent>
    </Card>
  )
}

function QualificationSection({ lead }: { lead: LeadDetail }) {
  if (lead.aiProcessingStatus === "Pending" || lead.aiProcessingStatus === "Processing") {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Qualification</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-muted-foreground">Qualification will be available once AI analysis completes.</p>
        </CardContent>
      </Card>
    )
  }

  if (lead.aiProcessingStatus === "Failed") {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Qualification</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-destructive">Qualification could not run because AI processing failed.</p>
        </CardContent>
      </Card>
    )
  }

  if (!lead.qualification) {
    return (
      <Card>
        <CardHeader>
          <CardTitle>Qualification</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-muted-foreground">This lead has not been qualified yet.</p>
        </CardContent>
      </Card>
    )
  }

  const { qualification } = lead

  return (
    <Card>
      <CardHeader>
        <CardTitle>Qualification</CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-2 text-sm">
        <p className="flex flex-wrap items-center gap-2">
          <span className="font-medium">Score:</span>
          <Badge variant="outline" className={LEAD_PRIORITY_BADGE_CLASSES[qualification.priority]}>
            {qualification.score}
          </Badge>
          <span className="font-medium">Priority:</span>
          <Badge variant="outline" className={LEAD_PRIORITY_BADGE_CLASSES[qualification.priority]}>
            {qualification.priority}
          </Badge>
        </p>
        {qualification.reasons.length > 0 && (
          <ul className="list-inside list-disc">
            {qualification.reasons.map((reason, index) => (
              <li key={index}>{reason}</li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  )
}

export function LeadDetailPage() {
  const { id } = useParams<{ id: string }>()
  const { data, isLoading, error, isSuccess } = useLeadDetailQuery(id ?? "")
  const { state, retry } = useSessionRecovery(error, isSuccess)

  return (
    <div className="flex flex-col gap-6">
      <Button variant="ghost" asChild className="self-start">
        <Link to="/accountant">← Back to leads</Link>
      </Button>

      {isLoading && <p>Loading…</p>}
      {state.kind !== "ok" && <SessionRecoveryNotice state={state} onRetry={retry} />}
      {isSuccess && data === null && <p>Lead not found.</p>}

      {data && (
        <>
          <Card>
            <CardHeader>
              <CardTitle>{data.companyName ?? data.name}</CardTitle>
            </CardHeader>
            <CardContent className="flex flex-col gap-2 text-sm">
              <p>
                <Badge variant="outline" className={LEAD_STATUS_BADGE_CLASSES[data.leadStatus]}>
                  {LEAD_STATUS_LABELS[data.leadStatus]}
                </Badge>
              </p>
              <p>
                <span className="font-medium">Name:</span> {data.name}
              </p>
              <p>
                <span className="font-medium">Email:</span> {data.email}
              </p>
              {data.phone && (
                <p>
                  <span className="font-medium">Phone:</span> {data.phone}
                </p>
              )}
              {data.companyName && (
                <p>
                  <span className="font-medium">Company:</span> {data.companyName}
                </p>
              )}
              {data.clientType && (
                <p>
                  <span className="font-medium">Client type:</span> {data.clientType}
                </p>
              )}
              {data.approximateAnnualRevenue != null && (
                <p>
                  <span className="font-medium">Approximate revenue:</span> ${data.approximateAnnualRevenue.toLocaleString()}
                </p>
              )}
              {data.accountingSoftware && (
                <p>
                  <span className="font-medium">Accounting software:</span> {data.accountingSoftware}
                </p>
              )}
              <p>
                <span className="font-medium">Inquiry:</span> {data.inquiry}
              </p>
            </CardContent>
          </Card>

          <AiAnalysisSection lead={data} />
          <QualificationSection lead={data} />
        </>
      )}
    </div>
  )
}

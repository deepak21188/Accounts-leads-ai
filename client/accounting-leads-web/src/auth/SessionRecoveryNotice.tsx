import type { SessionRecoveryState } from "@/auth/useSessionRecovery"
import { Button } from "@/components/ui/button"

export function SessionRecoveryNotice({
  state,
  onRetry,
}: {
  state: Exclude<SessionRecoveryState, { kind: "ok" }>
  onRetry: () => void
}) {
  switch (state.kind) {
    case "redirecting":
      return <p>Your session has expired. Redirecting to sign-in…</p>
    case "reauthFailed":
      return (
        <div className="flex flex-col gap-2">
          <p>Your session could not be renewed automatically.</p>
          <Button onClick={onRetry} className="self-start">
            Sign in again
          </Button>
        </div>
      )
    case "notAuthorized":
      return <p>You are signed in but not authorized to view leads.</p>
    case "otherError":
      return <p>Something went wrong. Please try again.</p>
  }
}

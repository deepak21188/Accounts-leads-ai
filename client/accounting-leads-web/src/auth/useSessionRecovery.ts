import { useEffect, useState } from "react"
import { useMsal } from "@azure/msal-react"
import { loginRequest } from "@/auth/msalConfig"

/**
 * Set right before the one automatic loginRedirect() attempt below, and read after returning
 * from it. Persisted (not component state) because the redirect round-trip unmounts and
 * remounts the page — without this, a token the API keeps rejecting (a real config mismatch,
 * not a normally expired one) would redirect the browser to Microsoft and back forever.
 */
export const REAUTH_ATTEMPTED_STORAGE_KEY = "accountantReauthAttempted"

export type SessionRecoveryState =
  | { kind: "ok" }
  | { kind: "redirecting" }
  | { kind: "reauthFailed" }
  | { kind: "notAuthorized" }
  | { kind: "otherError" }

/**
 * Shared SESSION_EXPIRED/NOT_AUTHORIZED handling for any authenticated-dashboard query.
 * Extracted from the original AccountantDashboardPage (now LeadListPage) so the Lead Detail
 * page's own query gets the exact same, already-hardened behavior instead of a second
 * hand-rolled copy — this logic took two rounds of real bug fixes to get right.
 *
 * Uses the query's own `isSuccess` flag, not `Boolean(data)`: a successful request can resolve
 * to a falsy value (e.g. getLeadDetail returns null on a 404, which is a legitimate success,
 * not a failure), so checking data's truthiness would wrongly fail to clear the reauth flag on
 * that response.
 */
export function useSessionRecovery(error: Error | null, isSuccess: boolean) {
  const { instance } = useMsal()
  const [reauthFailed, setReauthFailed] = useState(false)

  useEffect(() => {
    if (isSuccess) {
      sessionStorage.removeItem(REAUTH_ATTEMPTED_STORAGE_KEY)
    }
  }, [isSuccess])

  // Per docs/architecture.md §23: a 401 (SESSION_EXPIRED) means "re-authenticate" — triggers
  // interactive login rather than leaving the accountant stranded. Limited to one automatic
  // attempt so a persistently-rejected token can't loop the browser through Microsoft's login
  // page indefinitely.
  useEffect(() => {
    if (error?.message !== "SESSION_EXPIRED") {
      return
    }

    if (sessionStorage.getItem(REAUTH_ATTEMPTED_STORAGE_KEY)) {
      setReauthFailed(true)
      return
    }

    sessionStorage.setItem(REAUTH_ATTEMPTED_STORAGE_KEY, "true")
    instance.loginRedirect(loginRequest).catch(() => {
      // loginRedirect() rejected before navigating away at all (e.g. an interaction already
      // in progress) — without this, the page would show "Redirecting…" forever.
      setReauthFailed(true)
    })
  }, [error, instance])

  const retry = () => {
    sessionStorage.removeItem(REAUTH_ATTEMPTED_STORAGE_KEY)
    setReauthFailed(false)
    sessionStorage.setItem(REAUTH_ATTEMPTED_STORAGE_KEY, "true")
    instance.loginRedirect(loginRequest).catch(() => setReauthFailed(true))
  }

  let state: SessionRecoveryState
  if (!error) {
    state = { kind: "ok" }
  } else if (error.message === "SESSION_EXPIRED") {
    state = reauthFailed ? { kind: "reauthFailed" } : { kind: "redirecting" }
  } else if (error.message === "NOT_AUTHORIZED") {
    state = { kind: "notAuthorized" }
  } else {
    state = { kind: "otherError" }
  }

  return { state, retry }
}

import { useEffect, useState } from "react"
import { Navigate } from "react-router"
import { EventType } from "@azure/msal-browser"
import { useIsAuthenticated, useMsal } from "@azure/msal-react"
import { loginRequest } from "@/auth/msalConfig"
import { LOGIN_ERROR_STORAGE_KEY } from "@/auth/msalInstance"
import { Button } from "@/components/ui/button"

export function LoginPage() {
  const { instance } = useMsal()
  const isAuthenticated = useIsAuthenticated()
  const [error, setError] = useState<string | null>(null)

  // A failure can be recorded (see msalInstance.ts's ACQUIRE_TOKEN_FAILURE handler) either
  // before this component mounts, or after — handleRedirectPromise() can resolve on either
  // side of the mount, so neither a one-time sessionStorage read nor a live event listener
  // alone is sufficient. Both are needed: the read catches a failure that already happened;
  // the listener catches one that fires while this page is already sitting here.
  useEffect(() => {
    const storedError = sessionStorage.getItem(LOGIN_ERROR_STORAGE_KEY)
    if (storedError) {
      setError(storedError)
      sessionStorage.removeItem(LOGIN_ERROR_STORAGE_KEY)
    }

    const callbackId = instance.addEventCallback((event) => {
      if (event.eventType === EventType.ACQUIRE_TOKEN_FAILURE) {
        setError(event.error?.message || "Sign-in failed.")
        sessionStorage.removeItem(LOGIN_ERROR_STORAGE_KEY)
      }
    })

    return () => {
      if (callbackId) {
        instance.removeEventCallback(callbackId)
      }
    }
  }, [instance])

  // MSAL's redirect flow returns the browser to whatever page loginRedirect was called from
  // (navigateToLoginRequestUrl, on by default) — i.e. back here, at /accountant/login. Without
  // this check, a successful sign-in would silently re-render the same "Sign in" button instead
  // of moving on to the dashboard.
  if (isAuthenticated) {
    return <Navigate to="/accountant" replace />
  }

  const handleSignIn = () => {
    setError(null)
    // Catches failures that reject before navigation even starts (e.g. an interaction already
    // in progress); a failure after the redirect round-trip is instead picked up by the effect
    // above on the next mount, via LOGIN_ERROR_STORAGE_KEY.
    instance.loginRedirect(loginRequest).catch((err: unknown) => {
      setError(err instanceof Error ? err.message : "Sign-in failed.")
    })
  }

  return (
    <div className="mx-auto flex max-w-md flex-col items-center gap-4 px-4 py-24 text-center">
      <h1 className="text-xl font-semibold">Accountant sign-in</h1>
      <p className="text-sm text-muted-foreground">
        Sign in with your firm Microsoft account to view leads.
      </p>
      {error && <p className="text-sm text-destructive">{error}</p>}
      <Button onClick={handleSignIn}>Sign in with Microsoft</Button>
    </div>
  )
}

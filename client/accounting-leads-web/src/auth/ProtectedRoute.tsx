import { Navigate, Outlet } from "react-router"
import { useIsAuthenticated } from "@azure/msal-react"

/**
 * Gates /accountant/* routes behind sign-in. Redirects client-side to /accountant/login rather
 * than triggering MSAL's own redirect automatically, so an unauthenticated visit shows a real,
 * visible login page instead of an instant bounce to Microsoft (docs/architecture.md §23).
 */
export function ProtectedRoute() {
  const isAuthenticated = useIsAuthenticated()

  if (!isAuthenticated) {
    return <Navigate to="/accountant/login" replace />
  }

  return <Outlet />
}

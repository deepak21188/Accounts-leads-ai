import { Outlet } from "react-router"
import { useMsal } from "@azure/msal-react"
import { useQueryClient } from "@tanstack/react-query"
import { REAUTH_ATTEMPTED_STORAGE_KEY } from "@/auth/useSessionRecovery"
import { Button } from "@/components/ui/button"

export function AccountantLayout() {
  const { instance } = useMsal()
  const queryClient = useQueryClient()

  const handleSignOut = async () => {
    sessionStorage.removeItem(REAUTH_ATTEMPTED_STORAGE_KEY)
    queryClient.removeQueries({ queryKey: ["leads"] })
    queryClient.removeQueries({ queryKey: ["lead"] })
    await instance.logoutRedirect()
  }

  return (
    <div className="mx-auto flex max-w-5xl flex-col gap-4 px-4 py-12">
      <div className="flex items-center justify-between">
        <h1 className="text-xl font-semibold">AccountingLeads</h1>
        <Button variant="outline" onClick={handleSignOut}>
          Sign out
        </Button>
      </div>
      <Outlet />
    </div>
  )
}

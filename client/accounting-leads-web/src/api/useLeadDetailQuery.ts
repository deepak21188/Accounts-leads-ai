import { useQuery } from "@tanstack/react-query"
import { useMsal } from "@azure/msal-react"
import { getLeadDetail } from "@/api/leadsApi"

export function useLeadDetailQuery(leadId: string) {
  const { instance, accounts } = useMsal()
  const account = instance.getActiveAccount() ?? accounts[0]

  return useQuery({
    queryKey: ["lead", account?.homeAccountId, leadId],
    queryFn: () => getLeadDetail(instance, account, leadId),
    enabled: Boolean(account) && Boolean(leadId),
    retry: false,
  })
}

import { useQuery } from "@tanstack/react-query"
import { useMsal } from "@azure/msal-react"
import { getLeads, type LeadFilters } from "@/api/leadsApi"

/**
 * Keyed on the active account's homeAccountId (not a static key) so a different or absent
 * account automatically gets a different cache entry — this is what satisfies
 * docs/architecture.md §23's "clear accountant-specific data on logout/account change" without
 * a global queryClient.clear() on every MSAL event.
 *
 * Uses instance.getActiveAccount() rather than accounts[0]: msalInstance.ts sets the active
 * account explicitly on LOGIN_SUCCESS, and with multiple cached accounts, accounts[0] is not
 * guaranteed to be the one the accountant actually signed in as. accounts[0] is only a
 * fallback for the brief window before an active account has been set.
 *
 * Filters (including page/pageSize) are part of the queryKey, so a filter or page change
 * fetches fresh and previously-viewed pages come back from cache for free.
 */
export function useLeadsQuery(filters: LeadFilters = {}) {
  const { instance, accounts } = useMsal()
  const account = instance.getActiveAccount() ?? accounts[0]

  return useQuery({
    queryKey: [
      "leads",
      account?.homeAccountId,
      filters.status,
      filters.priority,
      filters.search,
      filters.page,
      filters.pageSize,
      filters.sortBy,
      filters.sortDescending,
    ],
    queryFn: () => getLeads(instance, account, filters),
    enabled: Boolean(account),
    // SESSION_EXPIRED/NOT_AUTHORIZED aren't transient — retrying just multiplies redirect
    // attempts and delays the accountant seeing an outcome. TanStack Query's default retries
    // would otherwise fire the SESSION_EXPIRED effect in AccountantDashboardPage more than
    // once per genuine failure.
    retry: false,
  })
}

import { useMutation } from "@tanstack/react-query"

import { createLead } from "@/api/leadsApi"

export function useCreateLeadMutation() {
  return useMutation({
    mutationFn: createLead,
  })
}

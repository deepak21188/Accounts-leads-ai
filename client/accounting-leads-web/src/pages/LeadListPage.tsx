import { useState, type ReactNode } from "react"
import { useNavigate } from "react-router"
import { ArrowDown, ArrowUp, ArrowUpDown } from "lucide-react"
import { useLeadsQuery } from "@/api/useLeadsQuery"
import { useSessionRecovery } from "@/auth/useSessionRecovery"
import { SessionRecoveryNotice } from "@/auth/SessionRecoveryNotice"
import {
  LEAD_PRIORITIES,
  LEAD_PRIORITY_LABELS,
  LEAD_PRIORITY_BADGE_CLASSES,
  LEAD_STATUSES,
  LEAD_STATUS_LABELS,
  LEAD_STATUS_BADGE_CLASSES,
  AI_PROCESSING_STATUS_LABELS,
  AI_PROCESSING_STATUS_BADGE_CLASSES,
} from "@/types/lead"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table"
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select"

const ALL_STATUSES = "all"
const ALL_PRIORITIES = "all"
const PAGE_SIZE = 10

const SORTABLE_COLUMNS = {
  name: "Name / Company",
  status: "Status",
  aiProcessingStatus: "AI Processing",
  priority: "Priority",
} as const

type SortColumn = keyof typeof SORTABLE_COLUMNS

function SortableHeader({
  column,
  activeSortBy,
  sortDescending,
  onSort,
  children,
}: {
  column: SortColumn
  activeSortBy: string | null
  sortDescending: boolean
  onSort: (column: SortColumn) => void
  children: ReactNode
}) {
  const isActive = activeSortBy === column
  const Icon = isActive ? (sortDescending ? ArrowDown : ArrowUp) : ArrowUpDown

  return (
    <TableHead>
      <button
        type="button"
        onClick={() => onSort(column)}
        className="inline-flex items-center gap-1 font-medium hover:text-foreground"
      >
        {children}
        <Icon className={isActive ? "size-3.5" : "size-3.5 text-muted-foreground"} />
      </button>
    </TableHead>
  )
}

export function LeadListPage() {
  const navigate = useNavigate()
  const [status, setStatus] = useState<string>(ALL_STATUSES)
  const [priority, setPriority] = useState<string>(ALL_PRIORITIES)
  const [search, setSearch] = useState("")
  const [page, setPage] = useState(1)
  const [sortBy, setSortBy] = useState<SortColumn | null>(null)
  const [sortDescending, setSortDescending] = useState(false)

  const { data, isLoading, error, isSuccess } = useLeadsQuery({
    status: status === ALL_STATUSES ? undefined : status,
    priority: priority === ALL_PRIORITIES ? undefined : priority,
    search: search || undefined,
    page,
    pageSize: PAGE_SIZE,
    sortBy: sortBy ?? undefined,
    sortDescending,
  })
  const { state, retry } = useSessionRecovery(error, isSuccess)

  const resetToFirstPage = () => setPage(1)

  const handleSort = (column: SortColumn) => {
    if (sortBy === column) {
      setSortDescending((prev) => !prev)
    } else {
      setSortBy(column)
      setSortDescending(false)
    }
    resetToFirstPage()
  }

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
        <Select
          value={status}
          onValueChange={(value) => {
            setStatus(value)
            resetToFirstPage()
          }}
        >
          <SelectTrigger className="w-full sm:w-40">
            <SelectValue placeholder="Status" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL_STATUSES}>All statuses</SelectItem>
            {LEAD_STATUSES.map((value) => (
              <SelectItem key={value} value={value}>
                {LEAD_STATUS_LABELS[value]}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Select
          value={priority}
          onValueChange={(value) => {
            setPriority(value)
            resetToFirstPage()
          }}
        >
          <SelectTrigger className="w-full sm:w-40">
            <SelectValue placeholder="Priority" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL_PRIORITIES}>All priorities</SelectItem>
            {LEAD_PRIORITIES.map((value) => (
              <SelectItem key={value} value={value}>
                {LEAD_PRIORITY_LABELS[value]}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>

        <Input
          placeholder="Search name, company, or email"
          value={search}
          onChange={(e) => {
            setSearch(e.target.value)
            resetToFirstPage()
          }}
          className="sm:max-w-xs"
        />
      </div>

      {isLoading && <p>Loading leads…</p>}
      {state.kind !== "ok" && <SessionRecoveryNotice state={state} onRetry={retry} />}

      {data && (
        <>
          <Table>
            <TableHeader>
              <TableRow>
                <SortableHeader column="name" activeSortBy={sortBy} sortDescending={sortDescending} onSort={handleSort}>
                  {SORTABLE_COLUMNS.name}
                </SortableHeader>
                <SortableHeader column="status" activeSortBy={sortBy} sortDescending={sortDescending} onSort={handleSort}>
                  {SORTABLE_COLUMNS.status}
                </SortableHeader>
                <SortableHeader
                  column="aiProcessingStatus"
                  activeSortBy={sortBy}
                  sortDescending={sortDescending}
                  onSort={handleSort}
                >
                  {SORTABLE_COLUMNS.aiProcessingStatus}
                </SortableHeader>
                <SortableHeader column="priority" activeSortBy={sortBy} sortDescending={sortDescending} onSort={handleSort}>
                  {SORTABLE_COLUMNS.priority}
                </SortableHeader>
              </TableRow>
            </TableHeader>
            <TableBody>
              {data.items.map((lead) => (
                <TableRow
                  key={lead.id}
                  role="button"
                  tabIndex={0}
                  className="cursor-pointer"
                  onClick={() => navigate(`/accountant/leads/${lead.id}`)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter") navigate(`/accountant/leads/${lead.id}`)
                  }}
                >
                  <TableCell>{lead.companyName ?? lead.name}</TableCell>
                  <TableCell>
                    <Badge variant="outline" className={LEAD_STATUS_BADGE_CLASSES[lead.leadStatus]}>
                      {LEAD_STATUS_LABELS[lead.leadStatus]}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <Badge variant="outline" className={AI_PROCESSING_STATUS_BADGE_CLASSES[lead.aiProcessingStatus]}>
                      {AI_PROCESSING_STATUS_LABELS[lead.aiProcessingStatus]}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    {lead.priority ? (
                      <Badge variant="outline" className={LEAD_PRIORITY_BADGE_CLASSES[lead.priority]}>
                        {LEAD_PRIORITY_LABELS[lead.priority]}
                      </Badge>
                    ) : (
                      <span className="text-muted-foreground">—</span>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>

          <div className="flex items-center justify-between text-sm text-muted-foreground">
            <p>
              {data.totalCount === 0
                ? "No leads match your filters."
                : `Showing ${(data.page - 1) * data.pageSize + 1}–${Math.min(data.page * data.pageSize, data.totalCount)} of ${data.totalCount} leads`}
            </p>
            <div className="flex items-center gap-2">
              <Button
                variant="outline"
                size="sm"
                disabled={data.page <= 1}
                onClick={() => setPage((p) => p - 1)}
              >
                Previous
              </Button>
              <span>
                Page {data.page} of {Math.max(data.totalPages, 1)}
              </span>
              <Button
                variant="outline"
                size="sm"
                disabled={data.page >= data.totalPages}
                onClick={() => setPage((p) => p + 1)}
              >
                Next
              </Button>
            </div>
          </div>
        </>
      )}
    </div>
  )
}

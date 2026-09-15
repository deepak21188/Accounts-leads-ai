import { useEffect } from "react"
import { Routes, Route, useNavigate } from "react-router"
import { msalInstance } from "@/auth/msalInstance"
import { CustomNavigationClient } from "@/auth/CustomNavigationClient"
import { ProtectedRoute } from "@/auth/ProtectedRoute"
import { AccountantLayout } from "@/components/layout/AccountantLayout"
import { PublicLeadFormPage } from "@/pages/PublicLeadFormPage"
import { LoginPage } from "@/pages/LoginPage"
import { LeadListPage } from "@/pages/LeadListPage"
import { LeadDetailPage } from "@/pages/LeadDetailPage"

function App() {
  const navigate = useNavigate()

  useEffect(() => {
    msalInstance.setNavigationClient(new CustomNavigationClient(navigate))
  }, [navigate])

  return (
    <Routes>
      <Route path="/" element={<PublicLeadFormPage />} />
      <Route path="/accountant/login" element={<LoginPage />} />
      <Route path="/accountant" element={<ProtectedRoute />}>
        <Route element={<AccountantLayout />}>
          <Route index element={<LeadListPage />} />
          <Route path="leads/:id" element={<LeadDetailPage />} />
        </Route>
      </Route>
    </Routes>
  )
}

export default App

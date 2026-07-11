import { BrowserRouter, Navigate, Route, Routes, useParams } from 'react-router-dom'
import { OpsLayout } from '@/components/OpsLayout'
import { PublicLayout } from '@/components/PublicLayout'
import { BatchDetailPage } from '@/pages/ops/BatchDetailPage'
import { CategoriesPage } from '@/pages/ops/CategoriesPage'
import { DashboardPage } from '@/pages/ops/DashboardPage'
import { JobDetailPage } from '@/pages/ops/JobDetailPage'
import { JobsPage } from '@/pages/ops/JobsPage'
import { RunsPage } from '@/pages/ops/RunsPage'
import { HomePage } from '@/pages/public/HomePage'
import { PublicJobDetailPage } from '@/pages/public/PublicJobDetailPage'
import { SearchPage } from '@/pages/public/SearchPage'

function LegacyBatchRedirect() {
  const { batchId } = useParams()
  return <Navigate to={`/ops/batches/${batchId}`} replace />
}

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<PublicLayout />}>
          <Route index element={<HomePage />} />
          <Route path="search" element={<SearchPage />} />
          <Route path="jobs/:id" element={<PublicJobDetailPage />} />
        </Route>

        <Route path="ops" element={<OpsLayout />}>
          <Route index element={<DashboardPage />} />
          <Route path="runs" element={<RunsPage />} />
          <Route path="batches/:batchId" element={<BatchDetailPage />} />
          <Route path="categories" element={<CategoriesPage />} />
          <Route path="jobs" element={<JobsPage />} />
          <Route path="jobs/:id" element={<JobDetailPage />} />
        </Route>

        {/* Legacy ops paths */}
        <Route path="runs" element={<Navigate to="/ops/runs" replace />} />
        <Route path="batches/:batchId" element={<LegacyBatchRedirect />} />
        <Route path="categories" element={<Navigate to="/ops/categories" replace />} />
        <Route path="jobs" element={<Navigate to="/ops/jobs" replace />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  )
}

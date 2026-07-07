import { BrowserRouter, Navigate, Route, Routes } from 'react-router-dom'
import { Layout } from '@/components/Layout'
import { BatchDetailPage } from '@/pages/BatchDetailPage'
import { CategoriesPage } from '@/pages/CategoriesPage'
import { DashboardPage } from '@/pages/DashboardPage'
import { JobDetailPage } from '@/pages/JobDetailPage'
import { JobsPage } from '@/pages/JobsPage'
import { RunsPage } from '@/pages/RunsPage'

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route element={<Layout />}>
          <Route index element={<DashboardPage />} />
          <Route path="runs" element={<RunsPage />} />
          <Route path="batches/:batchId" element={<BatchDetailPage />} />
          <Route path="categories" element={<CategoriesPage />} />
          <Route path="jobs" element={<JobsPage />} />
          <Route path="jobs/:id" element={<JobDetailPage />} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Route>
      </Routes>
    </BrowserRouter>
  )
}

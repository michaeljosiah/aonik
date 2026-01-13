import { useState } from 'react';
import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { Sidebar, Header } from '@/components/layout';
import { MySpacePage, LoginPage } from '@/pages';

function AppLayout() {
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);

  return (
    <div className="flex min-h-screen bg-[var(--color-background)]">
      <Sidebar
        collapsed={sidebarCollapsed}
        onToggle={() => setSidebarCollapsed(!sidebarCollapsed)}
      />
      <div className="flex-1 flex flex-col min-w-0 overflow-hidden">
        <Header breadcrumb={['My Space']} />
        <main className="flex-1 overflow-auto">
          <Routes>
            <Route path="/" element={<MySpacePage />} />
            <Route path="/search" element={<PlaceholderPage title="Search" />} />
            <Route path="/all" element={<PlaceholderPage title="All" />} />
            <Route path="/ai/*" element={<PlaceholderPage title="AI Tools" />} />
            <Route path="/workspaces/*" element={<PlaceholderPage title="Workspaces" />} />
            <Route path="/teams" element={<PlaceholderPage title="My Teams" />} />
            <Route path="/marketplace/*" element={<PlaceholderPage title="Marketplaces" />} />
            <Route path="/admin/*" element={<PlaceholderPage title="Admin Centre" />} />
          </Routes>
        </main>
      </div>
    </div>
  );
}

function PlaceholderPage({ title }: { title: string }) {
  return (
    <div className="flex items-center justify-center h-full">
      <div className="text-center">
        <h1 className="text-2xl font-bold text-[var(--color-text-primary)] mb-2">{title}</h1>
        <p className="text-[var(--color-text-secondary)]">This page is under construction.</p>
      </div>
    </div>
  );
}

function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/*" element={<AppLayout />} />
      </Routes>
    </BrowserRouter>
  );
}

export default App;

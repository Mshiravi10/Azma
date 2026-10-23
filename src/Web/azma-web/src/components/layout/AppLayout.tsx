import type { ReactNode } from 'react';
import { AppSidebar } from './Sidebar';
import { Topbar } from './Topbar';
import { SidebarInset, SidebarProvider } from '@/components/ui/sidebar';

export default function AppLayout({ children }: { children: ReactNode }) {
  const defaultOpen =
    typeof window === 'undefined' ? true : localStorage.getItem('azma.sidebar') !== 'collapsed';

  return (
    <SidebarProvider defaultOpen={defaultOpen}>
      <AppSidebar />
      <SidebarInset>
        <Topbar />
        <div className="flex-1 overflow-y-auto scroll-smooth">
          <div className="mx-auto min-h-full max-w-screen-2xl px-6 py-6">{children}</div>
        </div>
      </SidebarInset>
    </SidebarProvider>
  );
}

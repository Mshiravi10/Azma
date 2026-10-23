import React from 'react';
import ReactDOM from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import App from './App.tsx';
import { DirectionProvider } from '@/components/ui/direction';
import { TooltipProvider } from '@/components/ui/tooltip';
import { ThemeProvider } from '@/components/theme-provider';
import { Toaster } from '@/components/ui/sonner';
import '@fontsource/vazirmatn/300.css';
import '@fontsource/vazirmatn/400.css';
import '@fontsource/vazirmatn/500.css';
import '@fontsource/vazirmatn/600.css';
import '@fontsource/vazirmatn/700.css';
import './styles/globals.css';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: 1,
      refetchOnWindowFocus: false,
      staleTime: 30_000,
    },
  },
});

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <QueryClientProvider client={queryClient}>
            <DirectionProvider dir="rtl" direction="rtl">
        <ThemeProvider>
          <TooltipProvider>
            <BrowserRouter>
              <App />
              <Toaster />
            </BrowserRouter>
          </TooltipProvider>
        </ThemeProvider>
      </DirectionProvider>
    </QueryClientProvider>
  </React.StrictMode>,
);

import type { ReactNode } from 'react';
import Navbar from './Navbar';

export default function Layout({ children }: { children: ReactNode }) {
  return (
    <div className="min-h-screen bg-gray-50 flex flex-col">
      <Navbar />
      <main className="flex-1 max-w-5xl mx-auto w-full px-4 py-6">
        {children}
      </main>
    </div>
  );
}

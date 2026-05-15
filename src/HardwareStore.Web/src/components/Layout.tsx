import type { ReactNode } from 'react';
import { useLocation } from 'react-router-dom';
import Navbar from './Navbar';

export default function Layout({ children }: { children: ReactNode }) {
  const location = useLocation();
  const isFullBleedPage = location.pathname === '/jamies-super-good-media';

  return (
    <div className={`min-h-screen flex flex-col ${isFullBleedPage ? 'bg-[#050816]' : 'bg-gray-50'}`}>
      {!isFullBleedPage && <Navbar />}
      <main className={isFullBleedPage ? 'flex-1' : 'flex-1 max-w-5xl mx-auto w-full px-4 py-6'}>
        {children}
      </main>
    </div>
  );
}

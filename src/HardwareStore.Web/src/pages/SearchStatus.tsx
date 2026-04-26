import { useState, useEffect } from 'react';
import { useParams, useSearchParams, useNavigate, Link } from 'react-router-dom';
import { getSearchStatus, getPublicSearchStatus } from '../api';
import type { SearchStatus as SearchStatusType } from '../types';

export default function SearchStatusPage() {
  const { id } = useParams<{ id: string }>();
  const [searchParams] = useSearchParams();
  const isPublic = searchParams.get('public') === 'true';
  const navigate = useNavigate();

  const [status, setStatus] = useState<SearchStatusType | null>(null);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!id) return;

    let cancelled = false;

    const poll = async () => {
      try {
        const res = isPublic
          ? await getPublicSearchStatus(id)
          : await getSearchStatus(id);
        if (cancelled) return;
        setStatus(res.data);

        if (res.data.status === 'Completed' && res.data.reportId) {
          navigate(`/reports/${res.data.reportId}`, { replace: true });
          return;
        }
        if (res.data.status !== 'Failed') {
          setTimeout(poll, 2000);
        }
      } catch {
        if (!cancelled) setError('Failed to get search status.');
      }
    };

    poll();
    return () => { cancelled = true; };
  }, [id, isPublic, navigate]);

  const statusColor: Record<string, string> = {
    Queued: 'bg-yellow-100 text-yellow-800',
    Processing: 'bg-blue-100 text-blue-800',
    Completed: 'bg-green-100 text-green-800',
    Failed: 'bg-red-100 text-red-800',
  };

  return (
    <div className="max-w-lg mx-auto mt-10">
      <div className="bg-white rounded-2xl shadow-md p-8 text-center space-y-6">
        <div className="text-5xl">
          {!status && '⏳'}
          {status?.status === 'Queued' && '⏳'}
          {status?.status === 'Processing' && '🔍'}
          {status?.status === 'Completed' && '✅'}
          {status?.status === 'Failed' && '❌'}
        </div>
        <div>
          <h1 className="text-xl font-bold text-gray-900">
            {!status ? 'Checking status…' : `Search ${status.status}`}
          </h1>
          {status && (
            <span
              className={`inline-block mt-2 text-xs font-semibold px-3 py-1 rounded-full ${
                statusColor[status.status] ?? 'bg-gray-100 text-gray-700'
              }`}
            >
              {status.status}
            </span>
          )}
        </div>

        {(status?.status === 'Queued' || status?.status === 'Processing') && (
          <div className="flex justify-center">
            <div className="w-8 h-8 border-4 border-orange-200 border-t-orange-600 rounded-full animate-spin" />
          </div>
        )}

        {status?.status === 'Failed' && (
          <div className="space-y-3">
            <p className="text-sm text-red-600">
              {status.errorMessage ?? 'An error occurred during the search.'}
            </p>
            <Link
              to={isPublic ? '/' : '/search'}
              className="inline-block px-5 py-2.5 bg-orange-600 text-white rounded-xl font-semibold text-sm hover:bg-orange-700 transition"
            >
              Try Again
            </Link>
          </div>
        )}

        {error && <p className="text-sm text-red-600">{error}</p>}

        {!isPublic && status && (
          <Link to="/reports" className="text-sm text-gray-400 hover:text-gray-600 underline block">
            View all reports
          </Link>
        )}
      </div>
    </div>
  );
}

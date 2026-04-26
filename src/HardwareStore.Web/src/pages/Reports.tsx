import { useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { getReports } from '../api';
import type { ReportSummary } from '../types';

function formatDate(iso: string) {
  return new Date(iso).toLocaleString();
}

export default function Reports() {
  const [reports, setReports] = useState<ReportSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    getReports()
      .then((r) => setReports(r.data))
      .catch(() => setError('Failed to load reports.'))
      .finally(() => setLoading(false));
  }, []);

  if (loading) {
    return (
      <div className="flex justify-center py-20">
        <div className="w-8 h-8 border-4 border-orange-200 border-t-orange-600 rounded-full animate-spin" />
      </div>
    );
  }

  return (
    <div className="max-w-2xl mx-auto space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">My Reports</h1>
          <p className="text-sm text-gray-500">History of your past searches</p>
        </div>
        <Link
          to="/search"
          className="px-4 py-2 bg-orange-600 text-white rounded-xl text-sm font-semibold hover:bg-orange-700 transition"
        >
          + New Search
        </Link>
      </div>

      {error && (
        <p className="text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2">
          {error}
        </p>
      )}

      {reports.length === 0 && !error && (
        <div className="bg-white rounded-2xl shadow-md p-10 text-center text-gray-400">
          <div className="text-4xl mb-3">📋</div>
          <p>No reports yet. Start a search to see results here.</p>
        </div>
      )}

      <div className="space-y-3">
        {reports.map((r) => (
          <Link
            key={r.id}
            to={`/reports/${r.id}`}
            className="block bg-white rounded-2xl shadow-sm border border-gray-100 p-5 hover:border-orange-300 hover:shadow-md transition"
          >
            <div className="flex items-start justify-between gap-3">
              <div className="flex-1 min-w-0">
                <p className="font-semibold text-gray-900 truncate">{r.querySummary || 'Search report'}</p>
                <p className="text-xs text-gray-400 mt-0.5">{formatDate(r.createdAt)}</p>
              </div>
              <div className="text-right shrink-0">
                {r.recommendedRetailerName && (
                  <span className="inline-block text-xs bg-green-100 text-green-800 font-semibold px-2 py-0.5 rounded-full">
                    Best: {r.recommendedRetailerName}
                  </span>
                )}
                <p className="text-xs text-gray-400 mt-1">{r.productCount} product{r.productCount !== 1 ? 's' : ''}</p>
              </div>
            </div>
          </Link>
        ))}
      </div>
    </div>
  );
}

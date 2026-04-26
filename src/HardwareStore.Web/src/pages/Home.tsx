import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { publicSearch } from '../api';
import type { AxiosError } from 'axios';

export default function Home() {
  const [query, setQuery] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const navigate = useNavigate();

  const handlePublicSearch = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!query.trim()) return;
    setLoading(true);
    setError('');
    try {
      const res = await publicSearch(query.trim());
      navigate(`/status/${res.data.searchRequestId}?public=true`);
    } catch (err) {
      const e = err as AxiosError<{ message: string }>;
      setError(e.response?.data?.message ?? 'Search failed. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="space-y-10">
      {/* Hero */}
      <div className="text-center pt-6 pb-2">
        <h1 className="text-3xl sm:text-4xl font-bold text-gray-900 mb-3">
          🔧 Hardware Price Comparison
        </h1>
        <p className="text-gray-500 text-lg max-w-xl mx-auto">
          Compare prices at Home Depot and Lowe's instantly. No login required for a quick search.
        </p>
      </div>

      {/* Public Search Card */}
      <div className="bg-white rounded-2xl shadow-md p-6 max-w-2xl mx-auto">
        <div className="flex items-center gap-3 mb-4">
          <span className="text-2xl">🏠</span>
          <div>
            <h2 className="text-lg font-semibold text-gray-800">Quick Compare</h2>
            <p className="text-sm text-gray-500">Home Depot vs Lowe's</p>
          </div>
        </div>
        <form onSubmit={handlePublicSearch} className="space-y-3">
          <textarea
            className="w-full border border-gray-300 rounded-xl px-4 py-3 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-orange-400"
            rows={3}
            placeholder="Describe what you need, e.g. 'I need supplies to build a 10x10 wooden deck'"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            maxLength={500}
          />
          {error && (
            <p className="text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2">
              {error}
            </p>
          )}
          <button
            type="submit"
            disabled={loading || !query.trim()}
            className="w-full bg-orange-600 text-white font-semibold py-3 rounded-xl hover:bg-orange-700 disabled:opacity-50 disabled:cursor-not-allowed transition"
          >
            {loading ? 'Searching…' : 'Compare Prices'}
          </button>
        </form>
      </div>

      {/* Feature Cards */}
      <div className="grid sm:grid-cols-3 gap-4 max-w-2xl mx-auto">
        <div className="bg-white rounded-xl shadow-sm p-4 text-center">
          <div className="text-3xl mb-2">🗣️</div>
          <h3 className="font-semibold text-gray-800 mb-1">Natural Language</h3>
          <p className="text-xs text-gray-500">Describe your project in plain English</p>
        </div>
        <div className="bg-white rounded-xl shadow-sm p-4 text-center">
          <div className="text-3xl mb-2">📊</div>
          <h3 className="font-semibold text-gray-800 mb-1">Smart Comparison</h3>
          <p className="text-xs text-gray-500">Normalized unit pricing for fair comparisons</p>
        </div>
        <div className="bg-white rounded-xl shadow-sm p-4 text-center">
          <div className="text-3xl mb-2">⭐</div>
          <h3 className="font-semibold text-gray-800 mb-1">Best Pick</h3>
          <p className="text-xs text-gray-500">Get a clear "use this store" recommendation</p>
        </div>
      </div>

      {/* Login CTA */}
      <div className="bg-orange-50 border border-orange-200 rounded-2xl p-6 max-w-2xl mx-auto text-center">
        <h3 className="text-lg font-semibold text-orange-800 mb-2">
          Want more? Sign in for advanced features.
        </h3>
        <p className="text-sm text-orange-700 mb-4">
          Compare multiple products, multiple stores, and keep a history of your searches.
        </p>
        <div className="flex gap-3 justify-center">
          <Link
            to="/login"
            className="px-5 py-2 bg-orange-600 text-white rounded-xl font-semibold hover:bg-orange-700 transition text-sm"
          >
            Log In
          </Link>
          <Link
            to="/signup"
            className="px-5 py-2 border border-orange-400 text-orange-700 rounded-xl font-semibold hover:bg-orange-100 transition text-sm"
          >
            Sign Up
          </Link>
        </div>
      </div>
    </div>
  );
}

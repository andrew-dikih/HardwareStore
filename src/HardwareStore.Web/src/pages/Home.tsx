import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { publicParseQuery, publicSearch } from '../api';
import type { CandidateGroup, ProductCandidate, ProductCandidateConfidence } from '../types';
import type { AxiosError } from 'axios';

type Step = 'input' | 'review';

const confidenceLabel: Record<ProductCandidateConfidence, string> = {
  Exact: 'Exact match',
  SpecMatch: 'Spec match',
  Individual: 'Single retailer',
};

const confidenceStyle: Record<ProductCandidateConfidence, string> = {
  Exact: 'bg-green-100 text-green-800',
  SpecMatch: 'bg-blue-100 text-blue-800',
  Individual: 'bg-gray-100 text-gray-600',
};

export default function Home() {
  const [step, setStep] = useState<Step>('input');
  const [query, setQuery] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');
  const navigate = useNavigate();

  const [summary, setSummary] = useState('');
  const [groups, setGroups] = useState<CandidateGroup[]>([]);
  const [selected, setSelected] = useState<Set<string>>(new Set());

  const handleParse = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!query.trim()) return;
    setLoading(true);
    setError('');
    try {
      const res = await publicParseQuery(query.trim());
      setSummary(res.data.summary);
      setGroups(res.data.candidateGroups);
      // Pre-select the highest-confidence candidate in each group
      const preSelected = new Set<string>();
      for (const group of res.data.candidateGroups) {
        if (group.candidates.length > 0) preSelected.add(group.candidates[0].id);
      }
      setSelected(preSelected);
      setStep('review');
    } catch (err) {
      const e = err as AxiosError<{ message: string }>;
      setError(e.response?.data?.message ?? 'Failed to analyze query. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  const toggleCandidate = (id: string) => {
    setSelected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  const handleLaunch = async () => {
    const selectedCandidates: ProductCandidate[] = groups
      .flatMap((g) => g.candidates)
      .filter((c) => selected.has(c.id));

    if (selectedCandidates.length === 0) {
      setError('Please select at least one product.');
      return;
    }
    setLoading(true);
    setError('');
    try {
      const res = await publicSearch({ query, selectedCandidates });
      navigate(`/public/report/${res.data.reportId}`);
    } catch (err) {
      const e = err as AxiosError<{ message: string }>;
      setError(e.response?.data?.message ?? 'Search failed. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  if (step === 'review') {
    const hasResults = groups.some((g) => g.candidates.length > 0);

    return (
      <div className="max-w-2xl mx-auto space-y-6">
        <div className="flex items-center gap-3">
          <button
            onClick={() => { setStep('input'); setError(''); }}
            className="text-gray-500 hover:text-gray-700 text-sm"
          >
            ← Back
          </button>
          <div>
            <h1 className="text-2xl font-bold text-gray-900">Select Products to Compare</h1>
            {summary && <p className="text-sm text-gray-500">{summary}</p>}
          </div>
        </div>

        {!hasResults && (
          <div className="bg-yellow-50 border border-yellow-200 rounded-xl p-4 text-sm text-yellow-800">
            No products were found at retailers for your query. This may be due to temporary
            availability issues. Try rephrasing your search.
          </div>
        )}

        {groups.map((group) => (
          <div key={group.searchTerm} className="bg-white rounded-2xl shadow-md overflow-hidden">
            <div className="bg-gray-50 px-5 py-3 border-b border-gray-200 flex items-center gap-2">
              <p className="font-semibold text-gray-800">{group.displayName}</p>
              {group.isAdditional && (
                <span className="text-xs bg-blue-100 text-blue-700 px-2 py-0.5 rounded-full">Add-on</span>
              )}
            </div>

            {group.candidates.length === 0 ? (
              <p className="px-5 py-4 text-sm text-gray-400 italic">No results found for this item.</p>
            ) : (
              <div className="divide-y divide-gray-100">
                {group.candidates.map((candidate) => {
                  const thumbnail = candidate.items.find((i) => i.imageUrl)?.imageUrl;
                  return (
                    <label
                      key={candidate.id}
                      className={`flex items-start gap-3 px-5 py-4 cursor-pointer transition ${
                        selected.has(candidate.id) ? 'bg-orange-50' : 'hover:bg-gray-50'
                      }`}
                    >
                      <input
                        type="checkbox"
                        checked={selected.has(candidate.id)}
                        onChange={() => toggleCandidate(candidate.id)}
                        className="mt-1 h-4 w-4 text-orange-600 rounded flex-shrink-0"
                      />
                      {thumbnail && (
                        <img
                          src={thumbnail}
                          alt={candidate.displayName}
                          className="w-16 h-16 object-contain rounded-lg border border-gray-100 flex-shrink-0 bg-white"
                        />
                      )}
                      <div className="flex-1 min-w-0 space-y-1.5">
                        <div className="flex items-center gap-2 flex-wrap">
                          <span className="text-sm font-semibold text-gray-900">{candidate.displayName}</span>
                          <span className={`text-xs font-semibold px-2 py-0.5 rounded-full ${confidenceStyle[candidate.confidence]}`}>
                            {confidenceLabel[candidate.confidence]}
                          </span>
                        </div>
                        <div className="space-y-1">
                          {candidate.items.map((item) => (
                            <div key={item.retailerId} className="flex items-center gap-2">
                              {item.imageUrl && !thumbnail && (
                                <img
                                  src={item.imageUrl}
                                  alt={item.productTitle}
                                  className="w-8 h-8 object-contain rounded border border-gray-100 bg-white flex-shrink-0"
                                />
                              )}
                              <div className="min-w-0">
                                <span className="text-xs font-medium text-gray-500">{item.retailerName}: </span>
                                <span className="text-xs text-gray-900 font-semibold">{item.priceDisplay || `$${item.price.toFixed(2)}`}</span>
                                {item.productTitle && item.productTitle !== candidate.displayName && (
                                  <p className="text-xs text-gray-400 truncate" title={item.productTitle}>{item.productTitle}</p>
                                )}
                                {item.productUrl && (
                                  <a
                                    href={item.productUrl}
                                    target="_blank"
                                    rel="noopener noreferrer"
                                    onClick={(e) => e.stopPropagation()}
                                    className="text-xs text-orange-600 hover:underline"
                                  >
                                    View →
                                  </a>
                                )}
                              </div>
                            </div>
                          ))}
                          {candidate.items.length === 1 && (
                            <span className="text-xs text-gray-400 italic">Not found at other retailers</span>
                          )}
                        </div>
                      </div>
                    </label>
                  );
                })}
              </div>
            )}
          </div>
        ))}

        {error && (
          <p className="text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2">
            {error}
          </p>
        )}

        <button
          onClick={handleLaunch}
          disabled={loading || selected.size === 0}
          className="w-full bg-orange-600 text-white font-semibold py-4 rounded-xl hover:bg-orange-700 disabled:opacity-50 disabled:cursor-not-allowed transition text-base"
        >
          {loading ? 'Comparing prices…' : `🚀 Compare ${selected.size} Selected Product${selected.size !== 1 ? 's' : ''}`}
        </button>
      </div>
    );
  }

  return (
    <div className="space-y-10">
      <div className="text-center pt-6 pb-2">
        <h1 className="text-3xl sm:text-4xl font-bold text-gray-900 mb-3">
          🔧 Hardware Price Comparison
        </h1>
        <p className="text-gray-500 text-lg max-w-xl mx-auto">
          Compare prices at Home Depot and Lowe's instantly. No login required for a quick search.
        </p>
      </div>

      <div className="bg-white rounded-2xl shadow-md p-6 max-w-2xl mx-auto">
        <div className="flex items-center gap-3 mb-4">
          <span className="text-2xl">🏠</span>
          <div>
            <h2 className="text-lg font-semibold text-gray-800">Quick Compare</h2>
            <p className="text-sm text-gray-500">Home Depot vs Lowe's</p>
          </div>
        </div>
        <form onSubmit={handleParse} className="space-y-3">
          <textarea
            className="w-full border border-gray-300 rounded-xl px-4 py-3 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-orange-400"
            rows={3}
            placeholder="Describe what you need, e.g. 'I need a black bathroom sink faucet'"
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
            {loading ? 'Searching retailers…' : 'Find Products to Compare'}
          </button>
        </form>
      </div>

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

      <div className="bg-orange-50 border border-orange-200 rounded-2xl p-6 max-w-2xl mx-auto text-center">
        <h3 className="text-lg font-semibold text-orange-800 mb-2">
          Want more? Sign in for advanced features.
        </h3>
        <p className="text-sm text-orange-700 mb-4">
          Compare multiple products, multiple stores, and keep a history of your searches.
        </p>
        <div className="flex gap-3 justify-center">
          <Link to="/login" className="px-5 py-2 bg-orange-600 text-white rounded-xl font-semibold hover:bg-orange-700 transition text-sm">
            Log In
          </Link>
          <Link to="/signup" className="px-5 py-2 border border-orange-400 text-orange-700 rounded-xl font-semibold hover:bg-orange-100 transition text-sm">
            Sign Up
          </Link>
        </div>
      </div>
    </div>
  );
}

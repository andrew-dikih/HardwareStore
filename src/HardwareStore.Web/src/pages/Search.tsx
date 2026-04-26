import { useCallback, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { parseQuery, createSearch } from '../api';
import { useSpeechRecognition } from '../hooks/useSpeechRecognition';
import type { ProductSelection, RetailerDto, ParseQueryResponse } from '../types';
import type { AxiosError } from 'axios';

type Step = 'input' | 'review';

export default function Search() {
  const navigate = useNavigate();
  const [step, setStep] = useState<Step>('input');
  const [query, setQuery] = useState('');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const handleSpeechResult = useCallback((transcript: string) => {
    setQuery((prev) => (prev ? `${prev} ${transcript}` : transcript));
  }, []);

  const handleSpeechError = useCallback((msg: string) => {
    setError(msg);
  }, []);

  const { isListening, isSupported, start: startListening, stop: stopListening } =
    useSpeechRecognition({ onResult: handleSpeechResult, onError: handleSpeechError });

  const [parsed, setParsed] = useState<ParseQueryResponse | null>(null);
  const [products, setProducts] = useState<ProductSelection[]>([]);
  const [additional, setAdditional] = useState<ProductSelection[]>([]);
  const [retailers, setRetailers] = useState<RetailerDto[]>([]);

  const handleParse = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!query.trim()) return;
    setLoading(true);
    setError('');
    try {
      const res = await parseQuery(query.trim());
      setParsed(res.data);
      setProducts(res.data.suggestedProducts.map((p) => ({ ...p, isSelected: p.isSelected })));
      setAdditional(res.data.additionalItems.map((p) => ({ ...p, isSelected: false })));
      setRetailers(res.data.availableRetailers);
      setStep('review');
    } catch (err) {
      const e = err as AxiosError<{ message: string }>;
      setError(e.response?.data?.message ?? 'Failed to parse query. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  const toggleProduct = (id: string) =>
    setProducts((ps) => ps.map((p) => (p.id === id ? { ...p, isSelected: !p.isSelected } : p)));

  const toggleAdditional = (id: string) =>
    setAdditional((ps) => ps.map((p) => (p.id === id ? { ...p, isSelected: !p.isSelected } : p)));

  const toggleRetailer = (id: string) =>
    setRetailers((rs) => rs.map((r) => (r.id === id ? { ...r, isSelected: !r.isSelected } : r)));

  const handleLaunch = async () => {
    const selectedRetailerIds = retailers.filter((r) => r.isSelected).map((r) => r.id);
    if (selectedRetailerIds.length === 0) {
      setError('Please select at least one retailer.');
      return;
    }
    const selectedProducts = products.filter((p) => p.isSelected);
    const selectedAdditional = additional.filter((p) => p.isSelected);
    if (selectedProducts.length === 0) {
      setError('Please select at least one product.');
      return;
    }
    setLoading(true);
    setError('');
    try {
      const res = await createSearch({
        naturalLanguageQuery: query,
        selectedProducts,
        additionalItems: selectedAdditional,
        selectedRetailerIds,
      });
      navigate(`/status/${res.data.searchRequestId}`);
    } catch (err) {
      const e = err as AxiosError<{ message: string }>;
      if (e.response?.status === 429) {
        setError(e.response.data?.message ?? 'Daily search limit reached. Try again tomorrow.');
      } else {
        setError(e.response?.data?.message ?? 'Failed to launch search.');
      }
    } finally {
      setLoading(false);
    }
  };

  if (step === 'input') {
    return (
      <div className="max-w-2xl mx-auto space-y-6">
        <div>
          <h1 className="text-2xl font-bold text-gray-900">New Search</h1>
          <p className="text-sm text-gray-500 mt-1">
            Describe your project or what you need in natural language.
          </p>
        </div>
        <div className="bg-white rounded-2xl shadow-md p-6">
          <form onSubmit={handleParse} className="space-y-4">
            <div className="relative">
              <textarea
                className="w-full border border-gray-300 rounded-xl pl-4 pr-12 py-3 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-orange-400"
                rows={5}
                placeholder="e.g. I want to build a fence around my backyard. It's about 100 feet long and 6 feet tall."
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                maxLength={500}
              />
              {isSupported && (
                <button
                  type="button"
                  onClick={isListening ? stopListening : startListening}
                  title={isListening ? 'Stop recording' : 'Speak your query'}
                  className={`absolute bottom-3 right-3 p-1.5 rounded-full transition focus:outline-none focus:ring-2 focus:ring-orange-400 ${
                    isListening
                      ? 'bg-red-100 text-red-600 hover:bg-red-200 animate-pulse'
                      : 'text-gray-400 hover:text-orange-600 hover:bg-orange-50'
                  }`}
                >
                  <svg
                    xmlns="http://www.w3.org/2000/svg"
                    viewBox="0 0 24 24"
                    fill="currentColor"
                    className="w-5 h-5"
                    aria-hidden="true"
                  >
                    <path d="M12 1a4 4 0 0 1 4 4v6a4 4 0 0 1-8 0V5a4 4 0 0 1 4-4Z" />
                    <path d="M19 11a1 1 0 1 0-2 0 5 5 0 0 1-10 0 1 1 0 1 0-2 0 7 7 0 0 0 6 6.93V20H9a1 1 0 1 0 0 2h6a1 1 0 1 0 0-2h-2v-2.07A7 7 0 0 0 19 11Z" />
                  </svg>
                  <span className="sr-only">{isListening ? 'Stop recording' : 'Speak your query'}</span>
                </button>
              )}
            </div>
            <div className="flex justify-between items-center text-xs text-gray-400">
              <span>{query.length}/500</span>
            </div>
            {error && (
              <p className="text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2">
                {error}
              </p>
            )}
            <button
              type="submit"
              disabled={loading || !query.trim()}
              className="w-full bg-orange-600 text-white font-semibold py-3 rounded-xl hover:bg-orange-700 disabled:opacity-50 transition"
            >
              {loading ? 'Analyzing…' : 'Analyze My Project'}
            </button>
          </form>
        </div>
      </div>
    );
  }

  // Review step
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
          <h1 className="text-2xl font-bold text-gray-900">Review & Launch</h1>
          <p className="text-sm text-gray-500">{parsed?.summary}</p>
        </div>
      </div>

      {/* Products */}
      <div className="bg-white rounded-2xl shadow-md p-6 space-y-3">
        <h2 className="font-semibold text-gray-800">Products to Compare</h2>
        <p className="text-xs text-gray-500">Select which items to include in the search.</p>
        {products.map((p) => (
          <label key={p.id} className="flex items-start gap-3 cursor-pointer group">
            <input
              type="checkbox"
              checked={p.isSelected}
              onChange={() => toggleProduct(p.id)}
              className="mt-1 h-4 w-4 text-orange-600 rounded"
            />
            <div>
              <p className="text-sm font-medium text-gray-800 group-hover:text-orange-600 transition">
                {p.name}
              </p>
              {p.searchTerm !== p.name && (
                <p className="text-xs text-gray-400">Search term: {p.searchTerm}</p>
              )}
              {p.quantity && p.unit && (
                <p className="text-xs text-gray-400">
                  {p.quantity} {p.unit}
                </p>
              )}
            </div>
          </label>
        ))}
      </div>

      {/* Additional Items */}
      {additional.length > 0 && (
        <div className="bg-white rounded-2xl shadow-md p-6 space-y-3">
          <h2 className="font-semibold text-gray-800">Recommended Add-ons</h2>
          <p className="text-xs text-gray-500">Related items you might also need.</p>
          {additional.map((p) => (
            <label key={p.id} className="flex items-start gap-3 cursor-pointer group">
              <input
                type="checkbox"
                checked={p.isSelected}
                onChange={() => toggleAdditional(p.id)}
                className="mt-1 h-4 w-4 text-orange-600 rounded"
              />
              <div>
                <p className="text-sm font-medium text-gray-800 group-hover:text-orange-600 transition">
                  {p.name}
                </p>
                {p.category && (
                  <p className="text-xs text-gray-400">{p.category}</p>
                )}
              </div>
            </label>
          ))}
        </div>
      )}

      {/* Retailers */}
      <div className="bg-white rounded-2xl shadow-md p-6 space-y-3">
        <h2 className="font-semibold text-gray-800">Retailers to Search</h2>
        <div className="flex flex-wrap gap-3">
          {retailers.map((r) => (
            <label
              key={r.id}
              className={`flex items-center gap-2 border rounded-xl px-4 py-2 cursor-pointer transition text-sm font-medium ${
                r.isSelected
                  ? 'border-orange-400 bg-orange-50 text-orange-700'
                  : 'border-gray-200 text-gray-500 hover:border-gray-300'
              }`}
            >
              <input
                type="checkbox"
                checked={r.isSelected}
                onChange={() => toggleRetailer(r.id)}
                className="sr-only"
              />
              {r.isSelected ? '✓ ' : ''}{r.name}
            </label>
          ))}
        </div>
      </div>

      {error && (
        <p className="text-sm text-red-600 bg-red-50 border border-red-200 rounded-lg px-3 py-2">
          {error}
        </p>
      )}

      <button
        onClick={handleLaunch}
        disabled={loading}
        className="w-full bg-orange-600 text-white font-semibold py-4 rounded-xl hover:bg-orange-700 disabled:opacity-50 transition text-base"
      >
        {loading ? 'Launching…' : '🚀 Launch Search'}
      </button>
    </div>
  );
}

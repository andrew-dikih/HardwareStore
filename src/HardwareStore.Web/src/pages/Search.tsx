import { useCallback, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { parseQuery, createSearch } from '../api';
import { useSpeechRecognition } from '../hooks/useSpeechRecognition';
import type { ProductSelection, RetailerDto, ParseQueryResponse } from '../types';
import type { AxiosError } from 'axios';

type Step = 'input' | 'review';

const CATEGORY_ICONS: Array<{ keywords: string[]; icon: string }> = [
  { keywords: ['lumber', 'wood'], icon: '🪵' },
  { keywords: ['paint'], icon: '🎨' },
  { keywords: ['electric', 'wire', 'lighting'], icon: '💡' },
  { keywords: ['plumb', 'pipe', 'faucet'], icon: '🚿' },
  { keywords: ['fastener', 'screw', 'nail', 'bolt'], icon: '🔩' },
  { keywords: ['tool', 'drill', 'saw'], icon: '🔧' },
  { keywords: ['floor', 'tile', 'carpet'], icon: '🏠' },
  { keywords: ['concrete', 'cement', 'mortar'], icon: '🧱' },
  { keywords: ['insulation'], icon: '🧰' },
  { keywords: ['fence', 'gate'], icon: '🚧' },
  { keywords: ['adhesive', 'glue', 'tape', 'caulk'], icon: '🗜️' },
  { keywords: ['landscape', 'garden', 'soil'], icon: '🌱' },
  { keywords: ['safety', 'protective'], icon: '🦺' },
];

function getCategoryIcon(category?: string): string {
  const lower = (category ?? '').toLowerCase();
  const match = CATEGORY_ICONS.find(({ keywords }) => keywords.some((kw) => lower.includes(kw)));
  return match?.icon ?? '🔨';
}

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

  const buildSearchUrl = (template: string, searchTerm: string): string =>
    template.replace('{searchTerm}', encodeURIComponent(searchTerm));

  const openProductTabs = (searchTerm: string) => {
    retailers
      .filter((r) => r.isSelected && r.searchUrlTemplate)
      .forEach((r) => window.open(buildSearchUrl(r.searchUrlTemplate!, searchTerm), '_blank', 'noopener,noreferrer'));
  };

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
        {products.map((p) => {
          const selectedRetailersWithUrl = retailers.filter((r) => r.isSelected && r.searchUrlTemplate);
          return (
            <div key={p.id} className="flex items-start gap-3">
              <input
                type="checkbox"
                checked={p.isSelected}
                onChange={() => toggleProduct(p.id)}
                className="mt-4 h-4 w-4 text-orange-600 rounded shrink-0 cursor-pointer"
              />
              <div
                className={`flex-1 border rounded-xl p-3 transition ${p.isSelected ? 'border-orange-300 bg-orange-50' : 'border-gray-200 bg-gray-50'} ${selectedRetailersWithUrl.length > 0 ? 'cursor-pointer group hover:border-orange-400' : ''}`}
                onClick={selectedRetailersWithUrl.length > 0 ? () => openProductTabs(p.searchTerm) : undefined}
                title={selectedRetailersWithUrl.length > 0 ? `Search at ${selectedRetailersWithUrl.map((r) => r.name).join(' & ')}` : undefined}
              >
                <div className="flex items-start gap-3">
                  <div className="shrink-0 w-10 h-10 rounded-lg bg-orange-100 flex items-center justify-center text-xl">
                    {getCategoryIcon(p.category)}
                  </div>
                  <div className="flex-1 min-w-0">
                    <p className={`text-sm font-semibold text-gray-800 transition ${selectedRetailersWithUrl.length > 0 ? 'group-hover:text-orange-600 group-hover:underline' : ''}`}>
                      {p.name}
                    </p>
                    {p.category && (
                      <span className="inline-block text-xs text-orange-600 bg-orange-100 rounded-full px-2 py-0.5 mt-0.5">
                        {p.category}
                      </span>
                    )}
                    {p.description && (
                      <p className="text-xs text-gray-500 mt-1 leading-relaxed">{p.description}</p>
                    )}
                    <div className="flex flex-wrap gap-x-4 gap-y-1 mt-1.5">
                      {p.quantity != null && p.unit && (
                        <span className="text-xs text-gray-500">
                          <span className="font-medium text-gray-700">Qty:</span> {p.quantity} {p.unit}
                        </span>
                      )}
                      {p.dimensions && (
                        <span className="text-xs text-gray-500">
                          <span className="font-medium text-gray-700">Size:</span> {p.dimensions}
                        </span>
                      )}
                      {p.searchTerm !== p.name && (
                        <span className="text-xs text-gray-400">
                          <span className="font-medium">Term:</span> {p.searchTerm}
                        </span>
                      )}
                    </div>
                    {p.specifications && Object.keys(p.specifications).length > 0 && (
                      <div className="flex flex-wrap gap-1.5 mt-2">
                        {Object.entries(p.specifications).map(([key, value]) => (
                          <span key={key} aria-label={`${key}: ${value}`} className="text-xs bg-gray-100 text-gray-600 rounded-md px-2 py-0.5">
                            <span className="font-medium">{key}:</span> {value}
                          </span>
                        ))}
                      </div>
                    )}
                    {selectedRetailersWithUrl.length > 0 && (
                      <p className="text-xs text-orange-500 mt-1.5">Click to search at {selectedRetailersWithUrl.map((r) => r.name).join(' & ')} →</p>
                    )}
                  </div>
                </div>
              </div>
            </div>
          );
        })}
      </div>

      {/* Additional Items */}
      {additional.length > 0 && (
        <div className="bg-white rounded-2xl shadow-md p-6 space-y-3">
          <h2 className="font-semibold text-gray-800">Recommended Add-ons</h2>
          <p className="text-xs text-gray-500">Related items you might also need.</p>
          {additional.map((p) => {
            const selectedRetailersWithUrl = retailers.filter((r) => r.isSelected && r.searchUrlTemplate);
            return (
              <div key={p.id} className="flex items-start gap-3">
                <input
                  type="checkbox"
                  checked={p.isSelected}
                  onChange={() => toggleAdditional(p.id)}
                  className="mt-4 h-4 w-4 text-orange-600 rounded shrink-0 cursor-pointer"
                />
                <div
                  className={`flex-1 border rounded-xl p-3 transition ${p.isSelected ? 'border-orange-300 bg-orange-50' : 'border-gray-200 bg-gray-50'} ${selectedRetailersWithUrl.length > 0 ? 'cursor-pointer group hover:border-orange-400' : ''}`}
                  onClick={selectedRetailersWithUrl.length > 0 ? () => openProductTabs(p.searchTerm) : undefined}
                  title={selectedRetailersWithUrl.length > 0 ? `Search at ${selectedRetailersWithUrl.map((r) => r.name).join(' & ')}` : undefined}
                >
                  <div className="flex items-start gap-3">
                    <div className="shrink-0 w-10 h-10 rounded-lg bg-gray-100 flex items-center justify-center text-xl">
                      {getCategoryIcon(p.category)}
                    </div>
                    <div className="flex-1 min-w-0">
                      <p className={`text-sm font-semibold text-gray-800 transition ${selectedRetailersWithUrl.length > 0 ? 'group-hover:text-orange-600 group-hover:underline' : ''}`}>
                        {p.name}
                      </p>
                      {p.category && (
                        <span className="inline-block text-xs text-gray-500 bg-gray-100 rounded-full px-2 py-0.5 mt-0.5">
                          {p.category}
                        </span>
                      )}
                      {p.description && (
                        <p className="text-xs text-gray-500 mt-1 leading-relaxed">{p.description}</p>
                      )}
                      <div className="flex flex-wrap gap-x-4 gap-y-1 mt-1.5">
                        {p.quantity != null && p.unit && (
                          <span className="text-xs text-gray-500">
                            <span className="font-medium text-gray-700">Qty:</span> {p.quantity} {p.unit}
                          </span>
                        )}
                        {p.dimensions && (
                          <span className="text-xs text-gray-500">
                            <span className="font-medium text-gray-700">Size:</span> {p.dimensions}
                          </span>
                        )}
                      </div>
                      {p.specifications && Object.keys(p.specifications).length > 0 && (
                        <div className="flex flex-wrap gap-1.5 mt-2">
                          {Object.entries(p.specifications).map(([key, value]) => (
                            <span key={key} aria-label={`${key}: ${value}`} className="text-xs bg-gray-100 text-gray-600 rounded-md px-2 py-0.5">
                              <span className="font-medium">{key}:</span> {value}
                            </span>
                          ))}
                        </div>
                      )}
                      {selectedRetailersWithUrl.length > 0 && (
                        <p className="text-xs text-orange-500 mt-1.5">Click to search at {selectedRetailersWithUrl.map((r) => r.name).join(' & ')} →</p>
                      )}
                    </div>
                  </div>
                </div>
              </div>
            );
          })}
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

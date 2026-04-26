import { useState, useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import { getReport } from '../api';
import type { SearchReport, ProductComparison, RetailerProductResult } from '../types';

function formatCurrency(n: number) {
  return new Intl.NumberFormat('en-US', { style: 'currency', currency: 'USD' }).format(n);
}

function formatDate(iso: string) {
  return new Date(iso).toLocaleString();
}

function BestBadge() {
  return (
    <span className="inline-block text-xs bg-green-100 text-green-800 font-bold px-2 py-0.5 rounded-full ml-1">
      Best Price
    </span>
  );
}

function ProductCard({ product, bestRetailerId }: { product: ProductComparison; bestRetailerId: string }) {
  const sorted = [...product.retailerResults].sort((a, b) => a.price - b.price);
  const minPrice = sorted[0]?.price;

  return (
    <div className="border border-gray-200 rounded-xl overflow-hidden">
      <div className="bg-gray-50 px-4 py-3 border-b border-gray-200">
        <p className="font-semibold text-gray-800">{product.productName}</p>
        {product.isAdditional && (
          <span className="text-xs bg-blue-100 text-blue-700 px-2 py-0.5 rounded-full">Add-on</span>
        )}
      </div>
      <div className="divide-y divide-gray-100">
        {product.retailerResults.map((r) => (
          <RetailerRow
            key={r.retailerId}
            result={r}
            isBest={r.price === minPrice && r.isAvailable}
            isRecommended={r.retailerId === bestRetailerId}
          />
        ))}
      </div>
    </div>
  );
}

function RetailerRow({
  result,
  isBest,
}: {
  result: RetailerProductResult;
  isBest: boolean;
  isRecommended: boolean;
}) {
  return (
    <div className={`px-4 py-3 flex items-start justify-between gap-3 ${isBest ? 'bg-green-50' : ''}`}>
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-1 flex-wrap">
          <span className="text-sm font-medium text-gray-700">{result.retailerName}</span>
          {isBest && <BestBadge />}
        </div>
        {result.isAvailable ? (
          <>
            {result.productTitle && (
              <p className="text-xs text-gray-500 truncate mt-0.5">{result.productTitle}</p>
            )}
            {result.packageSize && (
              <p className="text-xs text-gray-400">{result.packageSize}</p>
            )}
          </>
        ) : (
          <p className="text-xs text-red-500 mt-0.5">Not available</p>
        )}
      </div>
      <div className="text-right shrink-0">
        {result.isAvailable ? (
          <>
            <p className="font-bold text-gray-900">{result.priceDisplay || formatCurrency(result.price)}</p>
            {result.normalizedPriceDisplay && (
              <p className="text-xs text-gray-400">{result.normalizedPriceDisplay}</p>
            )}
            {result.productUrl && (
              <a
                href={result.productUrl}
                target="_blank"
                rel="noopener noreferrer"
                className="text-xs text-orange-600 hover:underline"
              >
                View →
              </a>
            )}
          </>
        ) : (
          <p className="text-sm text-gray-400">N/A</p>
        )}
      </div>
    </div>
  );
}

export default function ReportDetail() {
  const { id } = useParams<{ id: string }>();
  const [report, setReport] = useState<SearchReport | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  useEffect(() => {
    if (!id) return;
    getReport(id)
      .then((r) => setReport(r.data))
      .catch(() => setError('Failed to load report.'))
      .finally(() => setLoading(false));
  }, [id]);

  if (loading) {
    return (
      <div className="flex justify-center py-20">
        <div className="w-8 h-8 border-4 border-orange-200 border-t-orange-600 rounded-full animate-spin" />
      </div>
    );
  }

  if (error || !report) {
    return (
      <div className="max-w-lg mx-auto mt-10 text-center space-y-3">
        <p className="text-red-600">{error || 'Report not found.'}</p>
        <Link to="/reports" className="text-orange-600 hover:underline text-sm">
          ← Back to reports
        </Link>
      </div>
    );
  }

  const totalsArr = Object.values(report.retailerTotals).sort((a, b) => a.totalPrice - b.totalPrice);
  const mainProducts = report.productComparisons.filter((p) => !p.isAdditional);
  const addOns = report.productComparisons.filter((p) => p.isAdditional);

  return (
    <div className="max-w-2xl mx-auto space-y-6">
      <div className="flex items-center gap-3">
        <Link to="/reports" className="text-gray-500 hover:text-gray-700 text-sm">
          ← Reports
        </Link>
      </div>

      {/* Summary banner */}
      <div className="bg-green-600 text-white rounded-2xl p-6 space-y-2">
        <p className="text-sm opacity-80">Best store recommendation</p>
        <h1 className="text-2xl font-bold">{report.recommendedRetailerName || 'No recommendation'}</h1>
        {report.recommendationReason && (
          <p className="text-sm opacity-90">{report.recommendationReason}</p>
        )}
        <p className="text-xs opacity-70">{formatDate(report.createdAt)}</p>
      </div>

      {/* Totals */}
      {totalsArr.length > 0 && (
        <div className="bg-white rounded-2xl shadow-md p-6">
          <h2 className="font-semibold text-gray-800 mb-4">Store Totals</h2>
          <div className="space-y-3">
            {totalsArr.map((t, i) => (
              <div key={t.retailerId} className="flex items-center justify-between">
                <div className="flex items-center gap-2">
                  {i === 0 && <span className="text-green-600">🏆</span>}
                  <span className="text-sm font-medium text-gray-700">{t.retailerName}</span>
                  <span className="text-xs text-gray-400">
                    ({t.productsFound}/{t.productsFound + t.productsNotFound} found)
                  </span>
                </div>
                <span className="font-bold text-gray-900">{t.totalPriceDisplay || formatCurrency(t.totalPrice)}</span>
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Product comparisons */}
      <div className="space-y-4">
        <h2 className="font-semibold text-gray-800">Product Details</h2>
        {mainProducts.map((p) => (
          <ProductCard key={p.productSelectionId} product={p} bestRetailerId={report.recommendedRetailerId} />
        ))}
      </div>

      {addOns.length > 0 && (
        <div className="space-y-4">
          <h2 className="font-semibold text-gray-800">Add-ons</h2>
          {addOns.map((p) => (
            <ProductCard key={p.productSelectionId} product={p} bestRetailerId={report.recommendedRetailerId} />
          ))}
        </div>
      )}

      <div className="text-center pt-4">
        <Link
          to="/search"
          className="inline-block px-6 py-3 bg-orange-600 text-white font-semibold rounded-xl hover:bg-orange-700 transition"
        >
          New Search
        </Link>
      </div>
    </div>
  );
}

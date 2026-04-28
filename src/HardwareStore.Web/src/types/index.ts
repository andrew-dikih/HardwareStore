export type ProductCandidateConfidence = 'Exact' | 'SpecMatch' | 'Individual';

export interface ProductCandidateItem {
  retailerId: string;
  retailerName: string;
  productTitle: string;
  price: number;
  priceDisplay: string;
  productUrl?: string;
  imageUrl?: string;
}

export interface ProductCandidate {
  id: string;
  searchTerm: string;
  displayName: string;
  brandNames: string[];
  confidence: ProductCandidateConfidence;
  items: ProductCandidateItem[];
}

export interface CandidateGroup {
  searchTerm: string;
  displayName: string;
  isAdditional: boolean;
  candidates: ProductCandidate[];
}

export interface PublicParseResponse {
  summary: string;
  candidateGroups: CandidateGroup[];
}

export interface LoginResponse {
  token: string;
  email: string;
  displayName: string;
  role: string;
  expiresAt: string;
}

export interface UserDto {
  id: string;
  email: string;
  displayName: string;
  role: string;
  status: string;
  dailySearchLimit: number;
  searchesUsedToday: number;
  allowedRetailerIds: string[];
  createdAt: string;
  approvedAt?: string;
}

export interface ProductSelection {
  id: string;
  name: string;
  searchTerm: string;
  category?: string;
  isSelected: boolean;
  isAdditional: boolean;
  unit?: string;
  quantity?: number;
  description?: string;
  specifications?: Record<string, string>;
  dimensions?: string;
}

export interface RetailerDto {
  id: string;
  name: string;
  logoUrl?: string;
  isSelected: boolean;
  searchUrlTemplate?: string;
}

export interface ParseQueryResponse {
  summary: string;
  suggestedProducts: ProductSelection[];
  additionalItems: ProductSelection[];
  availableRetailers: RetailerDto[];
}

export interface RetailerProductResult {
  retailerId: string;
  retailerName: string;
  productTitle: string;
  productUrl: string;
  imageUrl?: string;
  price: number;
  priceDisplay: string;
  packageSize?: string;
  quantityInPackage?: number;
  unit?: string;
  normalizedPrice?: number;
  normalizedPriceDisplay?: string;
  isAvailable: boolean;
  sku?: string;
}

export interface ProductComparison {
  productSelectionId: string;
  productName: string;
  searchTerm: string;
  isAdditional: boolean;
  retailerResults: RetailerProductResult[];
}

export interface RetailerTotal {
  retailerId: string;
  retailerName: string;
  totalPrice: number;
  totalPriceDisplay: string;
  productsFound: number;
  productsNotFound: number;
}

export interface SearchReport {
  id: string;
  searchRequestId: string;
  userId: string;
  querySummary: string;
  productComparisons: ProductComparison[];
  recommendedRetailerId: string;
  recommendedRetailerName: string;
  recommendationReason: string;
  retailerTotals: Record<string, RetailerTotal>;
  createdAt: string;
}

export interface ReportSummary {
  id: string;
  querySummary: string;
  recommendedRetailerName: string;
  createdAt: string;
  productCount: number;
}

export interface SearchStatus {
  id: string;
  status: 'Queued' | 'Processing' | 'Completed' | 'Failed';
  reportId?: string;
  errorMessage?: string;
  createdAt: string;
  completedAt?: string;
}

export interface Retailer {
  id: string;
  name: string;
  baseUrl: string;
  logoUrl?: string;
  isEnabled: boolean;
  isAvailableToAll: boolean;
  scraperType: string;
}

import Dexie, { type Table } from 'dexie'
import type { ProductResult } from '@/api/client'

export interface SearchHistoryItem {
  id?: number
  thumbnail: string // Data URL
  query: string
  resultsCount: number
  results?: ProductResult[] // Top results
  timestamp: number // Unix timestamp
}

export interface FavoriteProduct {
  id?: number
  productId: number
  name: string
  price: number
  imageUrl: string
  providerName: string
  addedAt: Date
}

// Type alias for view compatibility
export type FavoriteItem = FavoriteProduct

export interface RecentlyViewedProduct {
  id?: number
  productId: number
  name: string
  price: number
  imageUrl: string
  providerName: string
  viewedAt: Date
}

export class VisualSearchDatabase extends Dexie {
  searchHistory!: Table<SearchHistoryItem>
  favorites!: Table<FavoriteProduct>
  recentlyViewed!: Table<RecentlyViewedProduct>

  constructor() {
    super('VisualSearchDB')

    this.version(1).stores({
      searchHistory: '++id, timestamp',
      favorites: '++id, productId, addedAt',
      recentlyViewed: '++id, productId, viewedAt',
    })
  }
}

export const db = new VisualSearchDatabase()

// Helper functions
export async function addSearchHistory(
  thumbnail: string,
  query: string,
  resultsCount: number,
  results?: ProductResult[]
): Promise<number> {
  return db.searchHistory.add({
    thumbnail,
    query,
    resultsCount,
    results: results?.slice(0, 5), // Store top 5 results
    timestamp: Date.now(),
  })
}

export async function getSearchHistory(limit = 50): Promise<SearchHistoryItem[]> {
  return db.searchHistory.orderBy('timestamp').reverse().limit(limit).toArray()
}

export async function deleteSearchHistoryItem(id: number): Promise<void> {
  await db.searchHistory.delete(id)
}

export async function clearSearchHistory(): Promise<void> {
  await db.searchHistory.clear()
}

export async function addFavorite(product: Omit<FavoriteProduct, 'id' | 'addedAt'>): Promise<number> {
  // Check if already favorited
  const existing = await db.favorites.where('productId').equals(product.productId).first()
  if (existing) {
    return existing.id!
  }

  return db.favorites.add({
    ...product,
    addedAt: new Date(),
  })
}

export async function removeFavorite(productId: number): Promise<void> {
  await db.favorites.where('productId').equals(productId).delete()
}

export async function isFavorite(productId: number): Promise<boolean> {
  const count = await db.favorites.where('productId').equals(productId).count()
  return count > 0
}

export async function getFavorites(): Promise<FavoriteProduct[]> {
  return db.favorites.orderBy('addedAt').reverse().toArray()
}

export async function clearFavorites(): Promise<void> {
  await db.favorites.clear()
}
export async function addRecentlyViewed(
  product: Omit<RecentlyViewedProduct, 'id' | 'viewedAt'>
): Promise<number> {
  // Remove existing entry if present
  await db.recentlyViewed.where('productId').equals(product.productId).delete()

  // Add new entry
  const id = await db.recentlyViewed.add({
    ...product,
    viewedAt: new Date(),
  })

  // Keep only last 50 items
  const count = await db.recentlyViewed.count()
  if (count > 50) {
    const oldest = await db.recentlyViewed.orderBy('viewedAt').limit(count - 50).toArray()
    await db.recentlyViewed.bulkDelete(oldest.map((item) => item.id!))
  }

  return id
}

export async function getRecentlyViewed(limit = 20): Promise<RecentlyViewedProduct[]> {
  return db.recentlyViewed.orderBy('viewedAt').reverse().limit(limit).toArray()
}

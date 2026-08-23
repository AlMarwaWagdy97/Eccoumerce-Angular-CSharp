export interface AdminProductImageInterface {
  id: number;
  url: string;
  sort: number;
}

export interface AdminProductInterface {
  id: number;
  categoryId: number;
  title: string;
  slug: string;
  sku: string;
  price: number;
  priceAfterSale?: number | null;
  sale?: number | null;
  image?: string | null;
  stockQuantity: number;
  sort?: number | null;
  feature: boolean;
  status: boolean;
  metaDescription?: string | null;
  metaKey?: string | null;
}

export interface AdminProductDetailInterface {
  id: number;
  categoryId: number;
  title: string;
  slug: string;
  sku: string;
  price: number;
  priceAfterSale?: number | null;
  sale?: number | null;
  description?: string | null;
  image?: string | null;
  images: AdminProductImageInterface[];
  stockQuantity: number;
  sort?: number | null;
  feature: boolean;
  status: boolean;
  metaDescription?: string | null;
  metaKey?: string | null;
}

export interface ProductsPageInterface {
  items: AdminProductInterface[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

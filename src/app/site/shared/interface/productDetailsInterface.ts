export interface ReviewInterface {
    id?: number;
    name: string;
    rating: number;      // 1-5
    comment: string;
    date?: string;
    avatar?: string;
}

export interface ProductDetailsInterface {
    id: number;
    categoryId: number;
    categoryTitle?: string;
    title: string;
    slug: string;
    sku: string;
    price: number;
    priceAfterSale?: number;
    sale?: number;              // discount percentage
    description?: string;
    image?: string;            // primary image
    images?: string[];         // gallery
    stockQuantity?: number;
    rating?: number;           // average rating
    reviewsCount?: number;
    reviews?: ReviewInterface[];
    metaDescription?: string;
    metaKey?: string;
}

export interface ProductInterface {
    id: number;
    categoryId: number;
    title: string;
    slug: string;
    sku: string;
    price: number;
    description?: string;
    image?: string;
    priceAfterSale?: number;
    sale?: number;
    sort?: number;
    metaDescription?: string;
    metaKey?: string;
}

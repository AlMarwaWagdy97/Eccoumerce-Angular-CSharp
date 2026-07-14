export interface CartItemInterface {
    id: number;
    title: string;
    slug: string;
    image?: string;
    price: number;           // effective unit price (after any sale)
    originalPrice?: number;  // original unit price, for discount / strikethrough
    quantity: number;
}

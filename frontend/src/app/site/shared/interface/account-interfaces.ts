export interface AuthResponseInterface {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  token: string;
  expiresIn: number;
  refreshToken: string;
  refreshTokenExpiration: string;
}

export interface ProfileResponseInterface {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  createdOn: string;
}

export interface OrderSummaryInterface {
  id: number;
  orderNumber: string;
  status: string;
  total: number;
  createdOn: string;
  trackingNumber?: string;
}

export interface FavoriteInterface {
  id: number;
  productId: number;
  productTitle: string;
  productImage?: string;
  price: number;
  slug: string;
}

export interface ApiEnvelope<T> {
  statusCode: number;
  message: string;
  data: T;
}

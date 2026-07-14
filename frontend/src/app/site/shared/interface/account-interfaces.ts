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
}

export interface UpdateProfileRequest {
  firstName: string;
  lastName: string;
  phoneNumber?: string;
}

export interface OrderSummaryInterface {
  id: number;
  orderNumber: string;
  status: string;
  total: number;
  createdOn: string;
}

export interface OrderItemInterface {
  productId: number;
  productTitle: string;
  productImage?: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
}

export interface OrderDetailInterface {
  id: number;
  orderNumber: string;
  status: string;
  paymentMethod: string;
  paymentStatus: string;
  subTotal: number;
  shippingCost: number;
  total: number;
  shipToName: string;
  shipToPhone: string;
  shipToLine1: string;
  shipToLine2?: string;
  shipToCity: string;
  shipToState: string;
  shipToCountry: string;
  shipToPostalCode?: string;
  createdOn: string;
  items: OrderItemInterface[];
}

export interface CreateOrderItemRequest {
  productId: number;
  quantity: number;
}

export interface CreateOrderRequest {
  addressId: number;
  items: CreateOrderItemRequest[];
}

export interface OrderTrackingStepInterface {
  status: string;
  label: string;
  isCompleted: boolean;
  isCurrent: boolean;
  completedOn?: string;
}

export interface OrderTrackingInterface {
  orderNumber: string;
  status: string;
  createdOn: string;
  steps: OrderTrackingStepInterface[];
}

export interface FavoriteInterface {
  id: number;
  productId: number;
  productTitle: string;
  productImage?: string;
  price: number;
  slug: string;
}

export interface AddressInterface {
  id: number;
  fullName: string;
  phone: string;
  line1: string;
  line2?: string;
  city: string;
  state: string;
  country: string;
  postalCode?: string;
  isDefault: boolean;
}

export interface AddressRequest {
  fullName: string;
  phone: string;
  line1: string;
  line2?: string;
  city: string;
  state: string;
  country: string;
  postalCode?: string;
  isDefault: boolean;
}

export interface CardInterface {
  id: number;
  cardholderName: string;
  brand: string;
  last4: string;
  expiryMonth: number;
  expiryYear: number;
  isDefault: boolean;
}

export interface CardRequest {
  cardholderName: string;
  brand: string;
  last4: string;
  expiryMonth: number;
  expiryYear: number;
  isDefault: boolean;
}

export interface ApiEnvelope<T> {
  statusCode: number;
  message: string;
  data: T;
}

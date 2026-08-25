export interface AdminOrderSummaryInterface {
  id: number;
  orderNumber: string;
  customerName: string;
  customerEmail: string;
  customerMobile: string;
  status: string;
  paymentStatus: string;
  total: number;
  createdOn: string;
}

export interface AdminOrderItemInterface {
  productId: number;
  productTitle: string;
  productImage?: string | null;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
}

export interface AdminOrderDetailInterface {
  id: number;
  orderNumber: string;
  customerName: string;
  customerEmail: string;
  customerMobile: string;
  status: string;
  paymentMethod: string;
  paymentStatus: string;
  subTotal: number;
  shippingCost: number;
  total: number;
  shipToName: string;
  shipToPhone: string;
  shipToLine1: string;
  shipToLine2?: string | null;
  shipToCity: string;
  shipToState: string;
  shipToCountry: string;
  shipToPostalCode?: string | null;
  createdOn: string;
  statusUpdatedOn?: string | null;
  items: AdminOrderItemInterface[];
}

export interface OrdersPageInterface {
  items: AdminOrderSummaryInterface[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

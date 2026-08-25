export interface RecentOrderInterface {
  orderNumber: string;
  customerName: string;
  status: string;
  total: number;
  createdOn: string;
}

export interface DashboardSummaryInterface {
  totalRevenue: number;
  totalOrders: number;
  activeProductCount: number;
  clientCount: number;
  lowStockProductCount: number;
  recentOrders: RecentOrderInterface[];
}

export interface OrderStatusCountInterface {
  status: string;
  count: number;
}

export interface DailyRevenueInterface {
  date: string;
  orderCount: number;
  revenue: number;
}

export interface TopProductInterface {
  productId: number;
  productTitle: string;
  quantitySold: number;
  revenue: number;
}

export interface DashboardReportsInterface {
  ordersByStatus: OrderStatusCountInterface[];
  revenueByDay: DailyRevenueInterface[];
  topProducts: TopProductInterface[];
}

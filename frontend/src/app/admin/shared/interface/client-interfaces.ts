export interface ClientInterface {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber?: string | null;
  isActive: boolean;
  emailConfirmed: boolean;
}

export interface ClientDetailInterface extends ClientInterface {
  orderCount: number;
  lifetimeTotal: number;
}

export interface ClientsPageInterface {
  items: ClientInterface[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface UpdateClientRequest {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber?: string;
}

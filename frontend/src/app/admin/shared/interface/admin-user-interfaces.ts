export interface AdminUserInterface {
  id: number;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber?: string;
  roleId: number;
  roleName: string;
  isActive: boolean;
  createdOn: string;
}

export interface CreateAdminUserRequest {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber?: string;
  roleId: number;
}

export interface UpdateAdminUserRequest {
  firstName: string;
  lastName: string;
  phoneNumber?: string;
  roleId: number;
  isActive: boolean;
}

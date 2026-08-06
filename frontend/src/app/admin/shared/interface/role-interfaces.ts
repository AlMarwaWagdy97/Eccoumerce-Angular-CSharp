export interface PermissionInterface {
  id: number;
  key: string;
  module: string;
  description: string;
}

export interface RoleInterface {
  id: number;
  name: string;
  description?: string;
  isSystem: boolean;
  permissions: PermissionInterface[];
}

export interface RoleRequest {
  name: string;
  description?: string;
  permissionKeys: string[];
}

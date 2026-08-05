export interface AdminAuthResponseInterface {
  id: number;
  email: string;
  firstName: string;
  lastName: string;
  roleName: string;
  permissions: string[];
  token: string;
  expiresIn: number;
  refreshToken: string;
  refreshTokenExpiration: string;
}

export interface AdminApiEnvelope<T> {
  statusCode: number;
  message: string;
  data: T;
}

import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AdminAuthServices } from '../../../core/services/admin-auth-services';

interface AdminNavItem {
  label: string;
  path: string;
  icon: string;
  permission: string;
}

const NAV_ITEMS: AdminNavItem[] = [
  { label: 'Dashboard', path: '.', icon: 'bi-grid-1x2-fill', permission: 'dashboard.view' },
  { label: 'Categories', path: 'categories', icon: 'bi-diagram-3-fill', permission: 'categories.view' },
  { label: 'Products', path: 'products', icon: 'bi-box-seam-fill', permission: 'products.view' },
  { label: 'Clients', path: 'clients', icon: 'bi-person-lines-fill', permission: 'clients.view' },
  { label: 'Sliders', path: 'sliders', icon: 'bi-images', permission: 'sliders.view' },
  { label: 'Orders', path: 'orders', icon: 'bi-receipt', permission: 'orders.view' },
  { label: 'Roles', path: 'roles', icon: 'bi-shield-lock-fill', permission: 'roles.manage' },
  { label: 'Admins', path: 'admins', icon: 'bi-people-fill', permission: 'admins.manage' },
];

@Component({
  selector: 'app-admin-layout',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './main-layout.html',
  styleUrl: './main-layout.scss',
})
export class AdminLayoutComponent {
  private auth = inject(AdminAuthServices);
  private router = inject(Router);

  collapsed = signal(false);
  admin = this.auth.user;

  visibleNavItems = () => NAV_ITEMS.filter(item => this.auth.hasPermission(item.permission));

  toggleCollapsed(): void {
    this.collapsed.update(v => !v);
  }

  logout(): void {
    this.auth.logout();
    this.router.navigateByUrl('/admin/auth/login');
  }
}

import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AccountServices } from '../../../core/services/account-services';

@Component({
  selector: 'app-account-layout',
  imports: [RouterLink, RouterLinkActive, RouterOutlet],
  templateUrl: './account-layout.html',
  styleUrl: './account-layout.scss',
})
export class AccountLayoutComponent {
  private account = inject(AccountServices);
  private router = inject(Router);

  logout(): void {
    this.account.logout();
    this.router.navigateByUrl('/home');
  }
}

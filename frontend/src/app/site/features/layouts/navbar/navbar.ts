import { Component, inject } from '@angular/core';
import { RouterLink } from "@angular/router";
import { CartServices } from '../../../core/services/cart-services';
import { AccountServices } from '../../../core/services/account-services';

@Component({
  selector: 'app-navbar',
  imports: [RouterLink],
  templateUrl: './navbar.html',
  styleUrl: './navbar.scss',
})
export class Navbar {
  protected cart = inject(CartServices);
  protected account = inject(AccountServices);
}
